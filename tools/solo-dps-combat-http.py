#!/usr/bin/env python3
"""Additive fixed-sword Combat UI HTTP gate. No browser or current-client claim.

Uses the existing exact-Decimal, anchor-aware Combat oracle. Every request and
response, including failures, is retained in a fresh output directory.
"""
import argparse, copy, hashlib, importlib.util, json, os, pathlib, tempfile, time
import urllib.request, urllib.error, urllib.parse
SPEC = importlib.util.spec_from_file_location('combat_oracle', pathlib.Path(__file__).with_name('rogue-combat-historical-http.py'))
ORACLE = importlib.util.module_from_spec(SPEC); SPEC.loader.exec_module(ORACLE)
RANKS = ORACLE.RANKS
SOURCE = '1cc5ae1e9abdbf941b7326d2d90421822dce31ba'
RACES = ['human', 'dwarf', 'night-elf', 'gnome', 'orc', 'undead', 'troll']


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--base',default=os.environ.get('CP174_BASE_URL','http://127.0.0.1:5142'))
    p.add_argument('--out',default=os.environ.get('CP174_REPORT_DIR','/tmp/cp174-full-reports/combat-solo-ui'))
    a=p.parse_args();u=urllib.parse.urlsplit(a.base)
    assert u.scheme=='http' and u.hostname in ['localhost','127.0.0.1','::1'] and not u.username and not u.password and not u.query and not u.fragment
    root=pathlib.Path(a.out);root.mkdir(parents=True,exist_ok=True)
    out=pathlib.Path(tempfile.mkdtemp(prefix='http-',dir=root));receipts=[];proofs=[];start=time.monotonic()
    def equal(name,left,right):
        assert ORACLE.stable(left)==ORACLE.stable(right),name
        proofs.append(name)
    def call(name,route,body=None,status=200):
        payload=None if body is None else ORACLE.encode(body).encode()
        if payload is not None:(out/(name+'.request.json')).write_bytes(payload)
        request=urllib.request.Request(a.base+route,data=payload,headers={'Content-Type':'application/json'})
        try:
            with urllib.request.urlopen(request,timeout=45) as r:raw=r.read();actual=r.status
        except urllib.error.HTTPError as e:raw=e.read();actual=e.code
        (out/(name+'.response.json')).write_bytes(raw)
        receipts.append({'name':name,'route':route,'status':actual,'expectedStatus':status,'requestSha256':hashlib.sha256(payload).hexdigest() if payload else None,'responseSha256':hashlib.sha256(raw).hexdigest()})
        (out/'index.json').write_text(json.dumps(receipts,indent=2)+'\n')
        assert actual==status,(name,actual,status,raw[:1000])
        return ORACLE.decode(raw)
    def envelope(r):return {'classKey':'rogue','specialization':'combat','itemizedTalentMode':True,'itemizedTalentRanks':copy.deepcopy(r['talents']['ranks']),'rogue':copy.deepcopy(r)}
    try:
        capability=call('capability','/api/forever/shared/capabilities/rogue?specialization=combat')
        assert capability['specializations']==['combat']
        assert capability['executesThroughCurrentUnifiedEndpoint'] is True
        assert capability['hasRotationPreview'] is False
        for flag in ['hasExecutableCustomRotation','hasSeededBatchResults','hasSharedCombatItemProcessor','executesArbitraryCrossTreeModeledTalents']:assert capability[flag] is False,flag
        preset=call('partial-default','/api/forever/talents/rogue/combat/default-itemized')
        equal('exact-eleven-key-default',preset['ranks'],RANKS)
        equal('partial31-unspent20',[preset['totalRankedPoints'],preset['unspentPoints']],[31,20])
        on_disk=ORACLE.decode((pathlib.Path(__file__).resolve().parents[1]/'WarcraftSim.Api/wwwroot/itemized-presets/rogue-combat.json').read_bytes())
        equal('served-preset-matches-source',preset['ranks'],on_disk['ranks'])
        assert preset['allocationStatus']=='source-backed-partial-historical-provisional-31-ranks'
        catalog=call('catalog','/api/forever/talents/rogue/combat')
        equal('exact-source-caps',{t['key']:t['maxRank'] for t in catalog['talents']},RANKS)
        for t in catalog['talents']:
            assert t['tree']=='combat' and t['historicalValidationSourceCommit']==SOURCE
            if t['key'] in ['lightning-reflexes','puncturing-wounds','endurance']:assert t['simulationStatus']=='inactive-in-fixed-sword-combat'
        default=call('direct-default','/api/sim/rogue/defaults?spec=combat')
        default.update(durationSeconds=12,captureTimeline=True,useProvisionalLevel60Baseline=True,startingEnergyPercent=25,rogueMaximumHealth=4900)
        default['character'].update(race='human',id='22222222-2222-4222-8222-222222222222')
        default['talents']={'assassinationPointsSpent':0,'combatPointsSpent':31,'subtletyPointsSpent':0,'ranks':RANKS.copy()}
        def parity(label,request,replay=False):
            preview=call(label+'-preview','/api/forever/character/preview-rogue',request)
            assert 'request' not in preview and 'preview' not in preview,'Preview is flat, never an invented owned envelope'
            result=call(label+'-run','/api/forever/character/simulate-level60',envelope(request))
            equal(label+'-owned-request',result['preparation']['request'],request)
            equal(label+'-flat-preview-owned-receipt',preview,result['preparation']['preview'])
            equal(label+'-baseline-receipt',preview['baseline'],result['baseline'])
            assert result['classKey']=='rogue' and result['specialization']=='combat' and result['level']==60
            assert result['talentSourceCommit']==SOURCE
            assert result['talentSourceStatus']=='historical-provisional-not-current-client-certified'
            total=sum(request['talents']['ranks'].values())
            equal(label+'-allocation',[result['allocatedTalentPoints'],result['unspentTalentPoints']],[total,51-total])
            equal(label+'-timing',result['inputTiming']['additionalInputLatencyMilliseconds'],0)
            sim=result['sim'];actor=sim['run']['summary']['actorSummaries']['rogue']
            equal(label+'-energy',[preview['maximumEnergy'],preview['startingEnergy'],sim['maximumEnergy'],actor['startingResources']['energy']],[100,request['startingEnergyPercent'],100,request['startingEnergyPercent']])
            equal(label+'-health',preview['maximumHealth'],sim['maximumHealth'])
            equal(label+'-ap',preview['attackPower'],preview['effectiveStats']['values']['attack-power'])
            assert preview['maximumHealth']!=4900,'Owned historical Health supersedes fallback'
            assert actor['damageTaken']==0 and preview['rotation']['entries']
            assert not any(k in sim['damageByAbility'] for k in ['backstab','mutilate','blade-flurry-cleave'])
            notes=' '.join(result['talentModelingNotes'])
            for phrase in ['30/60','10/20','inactive','provisional','not optimized']:assert phrase in notes,phrase
            if replay:
                again=call(label+'-replay','/api/forever/character/simulate-level60',envelope(request))
                equal(label+'-complete-response-replay',result,again)
            return result
        for race in RACES:
            r=copy.deepcopy(default);r['character']['race']=race
            result=parity('race-'+race,r,replay=race=='human')
            assert result['baseline']['race']==race and result['baseline']['verificationStatus']=='provisional-documented-proxy'
            assert result['baseline']['includesRacialPassives'] is False
        for race in ['high-order-skyborne','windshaper-skyborne']:
            r=copy.deepcopy(default);r['character']['race']=race
            call(race+'-preview','/api/forever/character/preview-rogue',r,400)
            call(race+'-run','/api/forever/character/simulate-level60',envelope(r),400)
        reviewed=copy.deepcopy(default);reviewed.update(useProvisionalLevel60Baseline=False,useReviewedLevel60Baseline=True)
        call('reviewed-unavailable-preview','/api/forever/character/preview-rogue',reviewed,400)
        call('reviewed-unavailable-run','/api/forever/character/simulate-level60',envelope(reviewed),400)
        for label,ranks in [('without-ar',{k:v for k,v in RANKS.items() if k!='adrenaline-rush'}),('explicit-ar-zero',dict(RANKS,**{'adrenaline-rush':0})),('empty',{}),('early',{'improved-eviscerate':1})]:
            r=copy.deepcopy(default);r['talents']['ranks']=ranks;r['talents']['combatPointsSpent']=sum(ranks.values())
            validation=call(label+'-validate','/api/forever/talents/rogue/combat/validate-itemized',r['talents'])
            equal(label+'-validated-points',[validation['pointsSpent'],validation['remaining']],[sum(ranks.values()),51-sum(ranks.values())])
            parity(label,r,replay=label=='without-ar')
        zero=copy.deepcopy(default);zero.update(startingEnergyPercent=0,targetArmor=0,targetDodgePercent=0,targetParryPercent=0,poisonChargesPerWeapon=0)
        zero['tuning'].update(mainHandWeaponDamage=0,offHandWeaponDamage=0,improvedExposeArmorFivePointRefund=0)
        parity('explicit-zero',zero)
        # The earlier backend gate covers the complete key/scope matrix. These
        # UI regressions verify its critical controls, not a duplicate 119 calls.
        for rank in [4,5]:
            r=copy.deepcopy(default);r['talents']['ranks']['precision']=rank;r['talents']['combatPointsSpent']=28+rank
            call('precision-'+str(rank),'/api/forever/talents/rogue/combat/validate-itemized',r['talents'],400)
        for key in ['precision','blade-flurry']:
            r=copy.deepcopy(default);r['talents']['ranks'][key]-=1;r['talents']['combatPointsSpent']-=1
            call('parent-'+key,'/api/forever/talents/rogue/combat/validate-itemized',r['talents'],400)
        call('illegal-tier','/api/forever/talents/rogue/combat/validate-itemized',{'ranks':{'adrenaline-rush':1}},400)
        for key,value in [('mainHandWeaponType','dagger'),('bothWeaponsDaggers',True),('additionalTargets',1),('enableBossAttacks',True),('useUtilityAbilities',True)]:
            r=copy.deepcopy(default);r[key]=value;call('fixed-scope-'+key,'/api/forever/character/preview-rogue',r,400)
        summary={'status':'passed','requests':len(receipts),'proofs':proofs,'elapsedSeconds':round(time.monotonic()-start,3),'output':str(out),'normalization':ORACLE.NORMALIZATION,'scope':'Historical provisional; seven known race proxies, neither Skyborne nor reviewed Rogue; no current-client or optimal-build claim.'}
        (out/'summary.json').write_text(json.dumps(summary,indent=2)+'\n');print(json.dumps(summary,indent=2))
    except BaseException as e:
        (out/'failure.json').write_text(json.dumps({'status':'failed','error':repr(e),'requestsCompleted':len(receipts),'proofs':proofs,'elapsedSeconds':time.monotonic()-start},indent=2)+'\n')
        print('Combat UI HTTP failed; retained evidence: '+str(out));raise

if __name__=='__main__':main()
