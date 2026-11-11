#!/usr/bin/env python3
"""Bounded historical Combat HTTP gate. No current-client or optimal-build claim."""
import argparse, copy, hashlib, json, os, pathlib, re, urllib.request, urllib.error, urllib.parse
from decimal import Decimal

IDS={'abilityExecutionId','autoAttackInstanceId','auraInstanceId','absorbInstanceId','forcedTargetInstanceId'}
RANKS={'improved-eviscerate':3,'lightning-reflexes':5,'puncturing-wounds':3,'precision':3,'endurance':2,'flawless-execution':1,'dual-wield-specialization':5,'blade-flurry':1,'hack-and-slash':5,'weapon-expertise':2,'adrenaline-rush':1}

GUID=re.compile(r'[0-9a-fA-F]{8}(-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}')
ZERO_GUID='00000000-0000-0000-0000-000000000000'
NORMALIZATION=('Canonical exact-Decimal JSON strings distinguish booleans, numbers, null and missing; '
               'sorted object keys and ordered arrays; only valid nonzero GUID values in actual '
               '(sim.)run.timeline[array index] root CombatEvent fields '
               '(abilityExecutionId, autoAttackInstanceId, auraInstanceId, absorbInstanceId, forcedTargetInstanceId) '
               'may be renamed with joins/splits preserved. Unknown, provenance, nested and decoy paths, '
               'zero/empty/invalid IDs and literal GUID anchors remain exact. Representatives cannot collide '
               'with preserved GUID literals. GET-default generated Character IDs and the explicitly expected API sourceCommit '
               'stamp are handled separately; workflowCheckoutCommit and every other metadata field remain exact.')


def decode(raw):
    def invalid(value):
        raise ValueError('Non-finite JSON constant: '+value)
    return json.loads(raw,parse_float=Decimal,parse_constant=invalid)


def generated_path(path):
    if path[:1]==('sim',):path=path[1:]
    return (len(path)==4 and path[:2]==('run','timeline') and
            type(path[2]) is int and path[3] in IDS)


def encode(value,normalize_guids=False):
    """Exact JSON output. Raw request encoding never renames IDs or uses floats.

    GUID literals at unrecognized paths/keys are exact and also anchor any same
    identity in a generated field. Thus neither marker collisions nor joins or
    splits between generated IDs and retained literal GUIDs can become equal.
    """
    identities={};literals=set();next_id=0
    def collect(v,path=()):
        if isinstance(v,dict):
            for key,child in v.items():
                if not isinstance(key,str):raise TypeError('JSON object keys must be strings')
                if GUID.fullmatch(key):literals.add(key)
                collect(child,path+(key,))
        elif isinstance(v,list):
            for i,child in enumerate(v):collect(child,path+(i,))
        elif isinstance(v,str) and GUID.fullmatch(v) and (not generated_path(path) or v==ZERO_GUID):
            literals.add(v)
    if normalize_guids:collect(value)
    def representative(v):
        nonlocal next_id
        if v not in identities:
            while True:
                candidate=f'00000000-0000-4000-8000-{next_id:012x}';next_id+=1
                if candidate not in literals:break
            identities[v]=candidate
        return identities[v]
    def emit(v,path=()):
        if isinstance(v,dict):
            if not all(isinstance(key,str) for key in v):raise TypeError('JSON object keys must be strings')
            return '{'+','.join(json.dumps(key,ensure_ascii=False)+':'+emit(child,path+(key,)) for key,child in sorted(v.items()))+'}'
        if isinstance(v,list):return '['+','.join(emit(child,path+(i,)) for i,child in enumerate(v))+']'
        if isinstance(v,(int,Decimal)) and not isinstance(v,bool):
            number=Decimal(v)
            if not number.is_finite():raise ValueError('Non-finite JSON number')
            number=format(number,'f')
            return number.rstrip('0').rstrip('.') if '.' in number else number
        if isinstance(v,float):raise TypeError('Binary floats are not accepted; decode JSON numbers as Decimal')
        if normalize_guids and isinstance(v,str) and GUID.fullmatch(v) and v!=ZERO_GUID and generated_path(path) and v not in literals:
            v=representative(v)
        return json.dumps(v,ensure_ascii=False,allow_nan=False)
    return emit(value)


def stable(value):
    return encode(value,normalize_guids=True)

def metadata_projection(actual,previous,expected_source_commit):
    """Only the explicitly expected API build stamp may differ, never checkout."""
    for value in (actual,previous):
        if not isinstance(value.get('sourceCommit'),str) or not isinstance(value.get('workflowCheckoutCommit'),str):
            raise AssertionError('Report metadata must retain explicit string source and checkout stamps')
    if stable(actual['workflowCheckoutCommit'])!=stable(previous['workflowCheckoutCommit']):
        raise AssertionError('Report workflowCheckoutCommit changed; checkout provenance cannot be normalized')
    if actual['sourceCommit']!=expected_source_commit:
        raise AssertionError('Report sourceCommit does not match the explicitly expected API build stamp')
    current_copy=copy.deepcopy(actual);prior_copy=copy.deepcopy(previous)
    current_copy.pop('sourceCommit');prior_copy.pop('sourceCommit')
    receipt={'field':'sourceCommit','before':previous['sourceCommit'],'after':actual['sourceCommit'],
             'expectedApiBuildStamp':expected_source_commit,'workflowCheckoutCommit':actual['workflowCheckoutCommit'],
             'workflowCheckoutCommitUnchanged':True,'allOtherMetadataFieldsComparedExactly':True}
    return current_copy,prior_copy,receipt


def main():
    p=argparse.ArgumentParser();p.add_argument('--base',default='http://127.0.0.1:5142');p.add_argument('--out',required=True);p.add_argument('--baseline');p.add_argument('--source-commit',default=os.environ.get('CP174_SOURCE_COMMIT') or os.environ.get('GITHUB_SHA') or 'not-stamped-local-build');a=p.parse_args()
    u=urllib.parse.urlsplit(a.base)
    assert u.scheme=='http' and u.hostname in ['localhost','127.0.0.1','::1'] and not u.username and not u.password and not u.query and not u.fragment
    out=pathlib.Path(a.out);out.mkdir(parents=True,exist_ok=True);receipts=[];proofs=[]
    def call(name,path,body=None,status=200,raw=None):
        payload=raw.encode() if raw is not None else None if body is None else encode(body).encode()
        if payload is not None:(out/(name+'.request.json')).write_bytes(payload)
        req=urllib.request.Request(a.base+path,data=payload,headers={'Content-Type':'application/json'})
        try:
            with urllib.request.urlopen(req,timeout=90) as r:data=r.read();actual=r.status
        except urllib.error.HTTPError as e:data=e.read();actual=e.code
        (out/(name+'.response.json')).write_bytes(data)
        receipts.append({'name':name,'path':path,'status':actual,'expectedStatus':status,'requestSha256':hashlib.sha256(payload).hexdigest() if payload is not None else None,'responseSha256':hashlib.sha256(data).hexdigest(),'responseBytes':len(data)})
        (out/'index.json').write_text(json.dumps(receipts,indent=2)+'\n')
        assert actual==status,(name,actual,status,data[:1000])
        return decode(data)
    def equal(name,left,right):
        assert stable(left)==stable(right),name
        proofs.append({'name':name,'completeResponseEqual':True})
    def envelope(inp):return {'classKey':'rogue','specialization':'combat','itemizedTalentMode':True,'itemizedTalentRanks':copy.deepcopy(inp['talents']['ranks']),'rogue':copy.deepcopy(inp)}
    # Keep every server default decimal exact through decode -> request encode.
    default=call('defaults','/api/sim/rogue/defaults?spec=combat')
    assert encode(decode(encode(default)))==encode(default), 'Exact Decimal default roundtrip'
    default.update(durationSeconds=20,captureTimeline=True,useProvisionalLevel60Baseline=True)
    default['character'].update(race='human',id='22222222-2222-2222-2222-222222222222')
    default['talents']={'assassinationPointsSpent':0,'combatPointsSpent':31,'subtletyPointsSpent':0,'ranks':RANKS.copy()}
    c=call('catalog','/api/forever/talents/rogue/combat')
    assert stable({x['key']:x['maxRank'] for x in c['talents']})==stable(RANKS)
    assert all(x['tree']=='combat' for x in c['talents'])
    for x in c['talents']:
        assert x['historicalValidationSourceCommit']=='1cc5ae1e9abdbf941b7326d2d90421822dce31ba'
        if x['key'] in ['endurance','lightning-reflexes','puncturing-wounds']:assert x['simulationStatus']=='inactive-in-fixed-sword-combat'
    call('no-default','/api/forever/talents/rogue/combat/default-itemized',status=400)
    call('no-subtlety','/api/forever/talents/rogue/subtlety',status=400)
    for points in [31,30]:
        inp=copy.deepcopy(default)
        if points==30:inp['talents']['ranks'].pop('adrenaline-rush');inp['talents']['combatPointsSpent']=30
        for route in ['validate','validate-itemized']:
            v=call(f'{points}-{route}',f'/api/forever/talents/rogue/combat/{route}',inp['talents']);assert stable([v['pointsSpent'],v['remaining']])==stable([points,51-points])
        result=call(f'{points}-shared','/api/forever/character/simulate-level60',envelope(inp))
        replay=call(f'{points}-replay','/api/forever/character/simulate-level60',envelope(inp));equal(f'{points}-shared-replay',result,replay)
        direct=copy.deepcopy(inp);direct['inputLatencyMs']=0
        d=call(f'{points}-direct','/api/sim/rogue/run',direct);equal(f'{points}-direct-shared',d,result['sim'])
        preview=call(f'{points}-preview','/api/forever/character/preview-rogue',inp);equal(f'{points}-prepared-preview',preview,result['preparation']['preview'])
        assert stable(result['inputTiming']['additionalInputLatencyMilliseconds'])==stable(0)
        assert stable([preview['maximumEnergy'],preview['startingEnergy']])==stable([100,100])
        assert stable([result['allocatedTalentPoints'],result['unspentTalentPoints']])==stable([points,51-points])
        assert '30/60' in ' '.join(result['talentModelingNotes']) and 'discard' in ' '.join(preview['modelingNotes'])
    for rank in range(6):
        inp=copy.deepcopy(default);inp['talents']={'assassinationPointsSpent':0,'combatPointsSpent':8+rank,'subtletyPointsSpent':0,'ranks':{'improved-eviscerate':3,'lightning-reflexes':5,'precision':rank}}
        for route in ['validate','validate-itemized']:
            call(f'precision-{rank}-{route}',f'/api/forever/talents/rogue/combat/{route}',inp['talents'],status=200 if rank<=3 else 400)
        call(f'precision-{rank}-shared','/api/forever/character/simulate-level60',envelope(inp),status=200 if rank<=3 else 400)
    for key in ['aggression','deflection','riposte','malice','hemorrhage','unknown','PRECISION',' precision ']:
        for rank in [0,1]:
            for owner in ['typed','full','both']:
                env=envelope(default)
                if owner in ['typed','both']:env['rogue']['talents']['ranks'][key]=rank
                if owner in ['full','both']:env['itemizedTalentRanks'][key]=rank
                call(f'key-{key.strip().replace(" ","-")}-{rank}-{owner}','/api/forever/character/simulate-level60',env,status=400)
    for field,value in [('bothWeaponsDaggers',True),('mainHandWeaponType','dagger'),('offHandWeaponType','axe'),('useUtilityAbilities',True),('enableBossAttacks',True),('additionalTargets',1),('specialization','Combat')]:
        inp=copy.deepcopy(default);inp[field]=value
        call('boundary-'+field,'/api/forever/character/simulate-level60',envelope(inp),status=400)
        call('preview-boundary-'+field,'/api/forever/character/preview-rogue',inp,status=400)
    raw_cases=['{"ranks":{},"ranks":{}}','{"ranks":{},"Ranks":{}}','{"ranks":{},"totalPoints":1}','{"ranks":{},"unspentPoints":50}','{"ranks":{},"totalPoints":0,"totalPoints":0}','{"ranks":{},"totalPoints":null}','{"ranks":{},"inputLatencyMs":0}','{"ranks":{"precision":0,"precision":0}}']
    for i,raw in enumerate(raw_cases):
        for route in ['validate','validate-itemized']:call(f'raw-{i}-{route}',f'/api/forever/talents/rogue/combat/{route}',status=400,raw=raw)
    for route in ['validate','validate-itemized']:call('valid-totals-'+route,f'/api/forever/talents/rogue/combat/{route}',raw='{"ranks":{},"totalPoints":0,"unspentPoints":51}')
    for field in ['inputLatencyMs','customRotation','batch']:
        env=envelope(default);env[field]=0
        call('envelope-'+field,'/api/forever/character/simulate-level60',env,status=400)
    # Demonstrate, rather than overclaim, the legacy nested DTO binding boundary.
    env=envelope(default);env['rogue']['inputLatencyMs']=80
    nested=call('nested-latency-discarded','/api/forever/character/simulate-level60',env)
    assert stable(nested['inputTiming']['additionalInputLatencyMilliseconds'])==stable(0)
    call('nested-preview-field-discarded','/api/forever/character/preview-rogue',dict(default,inputLatencyMs=80))
    frozen_counts={'receipts':0,'completeUnchanged':0,'intentionalAdmissionChanges':0,'metadataOnly':0}
    if a.baseline:
        baseline=pathlib.Path(a.baseline)
        for old in json.loads((baseline/'index.json').read_text()):
            name=old['name'];body=decode((baseline/(name+'.request.json')).read_bytes()) if old['method']=='POST' else None
            changed=name in ['combat-catalog','combat-catalog-itemized-catalog'] or (name.startswith('combat-partial-') and not name.endswith('-direct'))
            expected=200 if changed else old['status']
            result=call('frozen-'+name,old['path'],body,status=expected)
            previous=decode((baseline/(name+'.response.json')).read_bytes())
            frozen_counts['receipts']+=1
            if changed or (name.startswith('combat-') and name.endswith('-preview')):
                frozen_counts['intentionalAdmissionChanges']+=1
                if not changed:assert result.get('error') # original unsupported preview is now bounded-validation rejection
            elif name=='report-metadata':
                current,prior,stamp=metadata_projection(result,previous,a.source_commit)
                equal('frozen-'+name,current,prior);frozen_counts['metadataOnly']+=1
                (out/'source-stamp-change.json').write_text(json.dumps(stamp,indent=2)+'\n')
            else:
                if name.endswith('-defaults'):result['character']['id']=previous['character']['id'] # generated request identity only
                equal('frozen-'+name,result,previous);frozen_counts['completeUnchanged']+=1
    summary={'requests':len(receipts),'fullResponseComparisons':len(proofs),'frozen':frozen_counts,'sourceConfidence':'historical-provisional, not current-client certified','comparisonPolicy':NORMALIZATION,'requestEncoding':'Server defaults and frozen request bodies decode JSON decimal tokens to Decimal and serialize exact numeric JSON, without binary float conversion.'}
    (out/'comparisons.json').write_text(json.dumps(proofs,indent=2)+'\n');(out/'summary.json').write_text(json.dumps(summary,indent=2)+'\n');print(json.dumps(summary,indent=2))

if __name__=='__main__':main()
