#!/usr/bin/env python3
"""Bounded Rogue damage HTTP evidence: exact JSON, immutable captures, causal replay.

Only Ghostly Strike and live own Hemorrhage/Rupture may change. No browser or
source-client certification. Baseline captures require unchanged production at
accepted HEAD; the process owner starts/stops its API in the same invocation.
"""
import argparse
import collections
import copy
from decimal import Decimal, localcontext, ROUND_HALF_UP, ROUND_HALF_EVEN
import hashlib
import importlib.util
import json
import pathlib
import subprocess
import threading
import time
import urllib.error
import urllib.parse
import urllib.request

ROOT = pathlib.Path(__file__).resolve().parents[1]
S = importlib.util.spec_from_file_location('rogue_exact_json', ROOT/'tools/rogue-combat-historical-http.py')
O = importlib.util.module_from_spec(S); S.loader.exec_module(O)
D = Decimal
ACCEPTED = 'fa53137e8194e7c84dd84fdae74c54f3ef265cae'
PRODUCTION = ['WarcraftSim.Data/Forever/Rogue/ForeverRogueSpellbook.cs',
              'WarcraftSim.Api/Simulation/RogueSimRunner.cs',
              'WarcraftSim.Api/Simulation/RogueCombatProcessor.cs']
RUN = '/api/sim/rogue/run'
METADATA = '/api/forever/character/report-metadata'
DEFAULT_CAUSAL_NAMES={f'default-subtlety-120-{prepared}-{bonus}' for prepared in [0,1] for bonus in ['13','17.25']}


def require(condition, text):
    if not condition: raise AssertionError(text)


def sha(raw): return hashlib.sha256(raw).hexdigest()
def read(path): return O.decode(pathlib.Path(path).read_bytes())
def write(path, value): pathlib.Path(path).write_text(O.encode(value)+'\n')
def git(*args): return subprocess.check_output(['git',*args],cwd=ROOT).decode().strip()


def fixtures(defaults):
    """Only current request fields plus the middleware-owned inputLatencyMs field.

    Prepared synthetic observations set observed total AP, not an additive AP
    guess; primary stats stay positive to satisfy the real baseline validator.
    """
    result=[]
    def add(name,r,group,positive=None):
        result.append(dict(name=name,path=RUN,status=200,body=copy.deepcopy(r),group=group,positive=positive))
    def prepared(r,ap):
        r['character']['race']='human'
        r['level60Baseline']=dict(race='human',classKey='rogue',level=60,strength=1,agility=1,stamina=1,intellect=1,spirit=1,
            observedUnbuffedMaximumHealth=4000,observedUnbuffedMeleeAttackPower=ap,
            clientBuild='synthetic-controlled-test',sourceReference='literal-rogue-damage-fixture-not-client-data',recordedAtUtc='2026-11-11T00:00:00Z')
    def base(ranks,ap=0):
        r=copy.deepcopy(defaults['subtlety'])
        r.update(durationSeconds=20,captureTimeline=True,inputLatencyMs=0,startInStealth=False,useInstantPoison=False,
                 mainHandPoison='none',offHandPoison='none',poisonChargesPerWeapon=0,targetMaximumHealth=1000000000,
                 targetArmor=0,targetDodgePercent=0,targetParryPercent=0,isBehindTarget=True,
                 bothWeaponsDaggers=False,mainHandWeaponType='sword',offHandWeaponType='dagger',
                 useUtilityAbilities=False,useExposeArmor=False,useProvisionalLevel60Baseline=False,useReviewedLevel60Baseline=False,level60Baseline=None)
        r['character'].update(id='33333333-3333-4333-8333-333333333333',race='human',baseStats={'values':{'attack-power':ap}},equipment={'items':[]},buffs={'selections':[]},talents={'selections':[]})
        r['talents']=dict(assassinationPointsSpent=5,combatPointsSpent=0,subtletyPointsSpent=31,ranks=copy.deepcopy(ranks))
        r['tuning'].update(energyPerSecond=60,mainHandWeaponDamage=100,mainHandSpeedSeconds=2,
            offHandWeaponDamage=40,offHandSpeedSeconds=2,mainHandHitPercent=100,offHandHitPercent=100,
            physicalCritPercent=0,sliceAndDiceHastePercent=0)
        return r
    for spec in ['assassination','combat','subtlety']:
        for duration in [20,120]:
            for prep in [False,True]:
                for bonus in [13,D('17.25')]:
                    r=copy.deepcopy(defaults[spec]);r.update(durationSeconds=duration,captureTimeline=True)
                    r['character']['id']='33333333-3333-4333-8333-333333333333'
                    r['tuning']['mutilateBonusDamagePerHand']=bonus
                    if prep:r['useProvisionalLevel60Baseline']=True;r['character']['race']='human'
                    add(f'default-{spec}-{duration}-{int(prep)}-{bonus}',r,'unchanged')
    for main in ['dagger','sword','axe','mace','fist','other']:
        for off in ['dagger','sword']:
            for flag in [False,True]:
                for behind in [False,True]:
                    r=base({'ghostly-strike':1});r.update(mainHandWeaponType=main,offHandWeaponType=off,bothWeaponsDaggers=flag,isBehindTarget=behind)
                    add(f'gs-{main}-{off}-flag{int(flag)}-behind{int(behind)}',r,'causal','ghostly-strike')
    for main in ['dagger','sword']:
        for armor,crit,lethality in [(0,0,0),(0,100,0),(5500,0,0),(5500,100,0),(0,100,5)]:
            r=base({'ghostly-strike':1,'lethality':lethality},140);r['mainHandWeaponType']=main;r['targetArmor']=armor;r['tuning']['physicalCritPercent']=crit
            add(f'gs-ap140-{main}-armor{armor}-crit{crit}-lethality{lethality}',r,'causal','ghostly-strike')
    for label,mutate in [('offhand',lambda r:r['tuning'].update(offHandWeaponDamage=123,offHandSpeedSeconds=3)),
                         ('prepared',lambda r:prepared(r,0)),('duration120',lambda r:r.update(durationSeconds=120))]:
        r=base({'ghostly-strike':1});mutate(r);add('gs-'+label,r,'causal','ghostly-strike')
    for key in ['ghostly-strike','hemorrhage']:
        for rank in [None,0]:
            r=base({} if rank is None else {key:rank});add(f'{key}-rank-{rank}',r,'unchanged')
        for outcome in ['miss','dodge','parry']:
            r=base({key:1});r['isBehindTarget']=False
            if outcome=='miss':r['tuning'].update(mainHandHitPercent=0,offHandHitPercent=0)
            else:r['target'+outcome.title()+'Percent']=100
            add(f'{key}-{outcome}',r,'unchanged')
    for bonus in [0,45,73]:
        for armor in [0,5500]:
            for crit in [0,100]:
                r=base({'hemorrhage':1});r['tuning'].update(hemorrhageBonusDamage=bonus,physicalCritPercent=crit);r['targetArmor']=armor
                add(f'hemo-bonus{bonus}-armor{armor}-crit{crit}',r,'causal','rupture')
    for label,mutate in [('prepared',lambda r:prepared(r,0)),('ap140',lambda r:r['character']['baseStats']['values'].update({'attack-power':140})),
            ('energy0',lambda r:r.update(startingEnergyPercent=0)),('energy25',lambda r:r.update(startingEnergyPercent=25)),
            ('duration120',lambda r:r.update(durationSeconds=120)),
            ('poison',lambda r:r.update(useInstantPoison=True,mainHandPoison='instant',offHandPoison='deadly',poisonChargesPerWeapon=100)),
            ('with-gs',lambda r:r['talents']['ranks'].update({'ghostly-strike':1}))]:
        r=base({'hemorrhage':1});mutate(r);add('hemo-'+label,r,'causal','rupture')
    for spec in ['assassination','combat']:
        r=base({'ghostly-strike':1,'hemorrhage':1});r['specialization']=spec;r['character']['specializationKey']=spec
        add('cross-tree-'+spec,r,'unchanged')
    r=base({'hemorrhage':1})
    for latency in [None,0,80]:
        probe=copy.deepcopy(r);probe.pop('inputLatencyMs',None)
        if latency is not None:probe['inputLatencyMs']=latency
        add('inputLatencyMs-'+str(latency),probe,'causal','rupture')
    for label in ['repeat-a','repeat-b','concurrent-a','concurrent-b']:
        add(label,r,'causal','rupture')
    require(len({x['name'] for x in result})==len(result),'Fixture names must be unique')
    return result


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self,*args):raise ValueError('Loopback redirects forbidden')


def provenance():
    paths=PRODUCTION+[str(p.relative_to(ROOT)) for p in sorted(ROOT.glob('WarcraftSim.*/bin/Release/net10.0/WarcraftSim.*.dll'))]
    return dict(head=git('rev-parse','HEAD'),tree=git('rev-parse','HEAD^{tree}'),files={p:sha((ROOT/p).read_bytes()) for p in paths},
                trackedDiff=git('diff','--stat'),note='Actual API stamp is retained separately; a HEAD label alone is not proof of a fresh build.')


def capture(args):
    u=urllib.parse.urlsplit(args.base)
    require(u.scheme=='http' and u.hostname in ['localhost','127.0.0.1','::1'] and not u.username and not u.password and not u.query and not u.fragment and u.path in ['','/'],'Loopback API root required')
    out=pathlib.Path(args.out);out.mkdir(parents=True,exist_ok=False)
    before=provenance();write(out/'provenance-before.json',before)
    if args.phase=='baseline':
        require(before['head']==ACCEPTED,'Baseline must use exact accepted HEAD')
        for p in PRODUCTION:require((ROOT/p).read_bytes()==subprocess.check_output(['git','show',ACCEPTED+':'+p],cwd=ROOT),'Baseline production differs: '+p)
    opener=urllib.request.build_opener(NoRedirect);rows=[];receipt_lock=threading.Lock()
    def call(case):
        body=case.get('raw')
        if body is None and case.get('body') is not None:body=O.encode(case['body']).encode()
        name=case['name']
        if body is not None:(out/(name+'.request.json')).write_bytes(body)
        req=urllib.request.Request(args.base.rstrip('/')+case['path'],data=body,headers={'Content-Type':'application/json'})
        started=time.monotonic_ns()
        try:
            with opener.open(req,timeout=90) as response:raw,status,headers=response.read(),response.status,dict(response.headers)
        except urllib.error.HTTPError as error:raw,status,headers=error.read(),error.code,dict(error.headers)
        (out/(name+'.response.json')).write_bytes(raw)
        row={k:case[k] for k in ['name','path','group','positive'] if k in case}
        row.update(status=status,expectedStatus=case['status'],requestSha256=sha(body) if body is not None else None,responseSha256=sha(raw),responseBytes=len(raw),headers=headers)
        row.update(startedMonotonicNs=started,completedMonotonicNs=time.monotonic_ns())
        with receipt_lock:
            rows.append(row);write(out/'index.json',rows)
        require(status==case['status'],f'{name}: HTTP {status}, expected {case["status"]}: {raw[:500]!r}')
        return O.decode(raw)
    try:
        if args.baseline:
            prior=pathlib.Path(args.baseline); cases=[]
            for row in read(prior/'index.json'):
                raw=(prior/(row['name']+'.request.json')).read_bytes() if row['requestSha256'] is not None else None
                require(raw is None or sha(raw)==row['requestSha256'],'Baseline request hash mismatch')
                require(sha((prior/(row['name']+'.response.json')).read_bytes())==row['responseSha256'],'Baseline response hash mismatch')
                cases.append(dict(row,raw=raw))
            for case in cases:
                if not case['name'].startswith('concurrent-'):call(case)
            import concurrent.futures
            with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:list(pool.map(call,[c for c in cases if c['name'].startswith('concurrent-')]))
        else:
            defaults={spec:call(dict(name='defaults-'+spec,path='/api/sim/rogue/defaults?spec='+spec,status=200,group='default')) for spec in ['assassination','combat','subtlety']}
            call(dict(name='report-metadata',path=METADATA,status=200,group='metadata'))
            cases=fixtures(defaults);write(out/'fixture-inventory.json',[{k:v for k,v in c.items() if k!='body'} for c in cases])
            for case in cases:
                if case['name'].startswith('concurrent-'):continue
                call(case)
            # Both calls complete before the index is written by each; actual HTTP
            # overlap is intentional. Only these independent request objects run concurrently.
            import concurrent.futures
            with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:list(pool.map(call,[c for c in cases if c['name'].startswith('concurrent-')]))
        metadata=read(out/'report-metadata.response.json')
        require(metadata['sourceCommit']==args.source_commit,'API source stamp differs from supplied exact stamp')
        after=provenance();write(out/'provenance-after.json',after)
        require(all(before[k]==after[k] for k in ['head','tree','files']),'Source/binary identities changed during HTTP capture')
        proof=validate_capture(out,args.phase)
        write(out/'summary.json',dict(passed=True,phase=args.phase,requests=len(rows),sourceCommit=args.source_commit,proof=proof,normalization=O.NORMALIZATION))
        print(O.encode(read(out/'summary.json')))
    except BaseException as e:
        write(out/'failure.json',dict(passed=False,error=repr(e),requests=len(rows)));raise


def validate_capture(out,phase):
    counts=collections.Counter()
    index=read(out/'index.json')
    require(len(index)==127 and len({r['name'] for r in index})==127,'Complete127-call inventory required')
    overlap=[r for r in index if r['name'].startswith('concurrent-')]
    require(len(overlap)==2 and max(r['startedMonotonicNs'] for r in overlap)<min(r['completedMonotonicNs'] for r in overlap),'Concurrent requests did not overlap')
    for row in read(out/'index.json'):
        if row.get('group') not in ['causal','unchanged']:continue
        v=read(out/(row['name']+'.response.json'))
        events=v['run']['timeline'];damage=[e for e in events if e['type']==6]
        for key in ['ghostly-strike','rupture']:
            positive=[e for e in damage if e['abilityKey']==key and e['amount']>0]
            counts[key+'PositiveEvents']+=len(positive)
            if row.get('positive')==key:require(positive,row['name']+': mandatory positive '+key+' coverage absent')
        if row['group']=='causal' or row['name'] in DEFAULT_CAUSAL_NAMES:
            req=read(out/(row['name']+'.request.json'))
            if row['name'] in DEFAULT_CAUSAL_NAMES:verify_default_request(row['name'],req,read(out/'defaults-subtlety.response.json'))
            causal_expected(req,v,input_corrected=phase=='corrected',default_control=row['name'] in DEFAULT_CAUSAL_NAMES)
            counts['literalCausalFixtures']+=1
            require(not any(a['died'] for a in v['run']['summary']['actorSummaries'].values()),row['name']+': no-kill fixture died')
            require(v['run']['summary']['actorSummaries']['target']['damageTaken']<1000000000,row['name']+': target health exhausted')
    require(counts['ghostly-strikePositiveEvents']>0 and counts['rupturePositiveEvents']>0,'Both corrections need positive HTTP evidence')
    # Program.cs middleware reads raw top-level inputLatencyMs before DTO binding.
    baseline=read(out/'inputLatencyMs-None.response.json')
    require(O.stable(baseline)==O.stable(read(out/'inputLatencyMs-80.response.json')),'Default latency must equal explicit 80 ms')
    zero=read(out/'inputLatencyMs-0.response.json')
    require(O.stable(baseline)!=O.stable(zero),'Zero latency must exercise different middleware timing')
    for name in ['repeat-a','repeat-b','concurrent-a','concurrent-b']:
        require(O.stable(zero)==O.stable(read(out/(name+'.response.json'))),name+': repeat equivalence failed')
    return dict(counts,inputLatencyMs='Program.cs raw-body middleware: absent equals 80 ms; explicit 0 ms differs. Repeat/concurrent controls use 0 ms.')



def format_damage(value):
    return format(D(value).quantize(D('.01'),rounding=ROUND_HALF_UP),'f').rstrip('0').rstrip('.') if D(value)%1 else str(int(value))


def dotnet_divide(left,right):
    """System.Decimal division for these bounded DPS values, no tolerance.

    Scale is at most28 and the unsigned coefficient at most2**96-1.
    Exact finite divisions retain their exact value; midpoint ties use even.
    """
    with localcontext() as ctx:
        ctx.prec=100
        result=D(left)/D(right)
        for scale in range(28,-1,-1):
            rounded=result.quantize(D(1).scaleb(-scale),rounding=ROUND_HALF_EVEN)
            if abs(rounded.scaleb(scale))<=79228162514264337593543950335:return rounded
    raise AssertionError('Decimal division overflow')



def dotnet_add(left,right):
    """Exact decimal addition then96-bit coefficient/scale28 rounding, like .NET.

    Addition preserves the larger operand scale until the96-bit coefficient
    forces a scale reduction. Do not round to a blanket decimal precision.
    """
    with localcontext() as ctx:
        ctx.prec=100
        result=D(left)+D(right)
        for scale in range(min(28,max(0,-D(left).as_tuple().exponent,-D(right).as_tuple().exponent)),-1,-1):
            rounded=result.quantize(D(1).scaleb(-scale),rounding=ROUND_HALF_EVEN)
            if abs(rounded.scaleb(scale))<=79228162514264337593543950335:return rounded
    raise AssertionError('Decimal addition overflow')


def verify_default_request(name,request,default):
    require(name in DEFAULT_CAUSAL_NAMES,'Only four captured120s default controls are admitted')
    _,_,_,prep,bonus=name.split('-')
    expected=copy.deepcopy(default);expected.update(durationSeconds=120,captureTimeline=True)
    expected['character']['id']='33333333-3333-4333-8333-333333333333'
    expected['tuning']['mutilateBonusDamagePerHand']=D(bonus)
    if prep=='1':expected['useProvisionalLevel60Baseline']=True;expected['character']['race']='human'
    require(O.encode(request)==O.encode(expected),'Default causal input differs from exact captured default derivative')

def expected_damage(request,event,points,marked,corrected):
    """Independent literal coefficient oracle for the deliberately isolated inputs."""
    t=request['tuning'];key=event['abilityKey']
    ap=request['level60Baseline']['observedUnbuffedMeleeAttackPower'] if request.get('level60Baseline') else request['character']['baseStats']['values'].get('attack-power',0)
    weapon=D(t['mainHandWeaponDamage'])+D(ap)*D(t['mainHandSpeedSeconds'])/14
    if key=='ghostly-strike':
        coefficient=(D('1.8') if request['mainHandWeaponType']=='dagger' else D('1.25')) if corrected else (D('1.35') if request['bothWeaponsDaggers'] else D('1.1'))
        raw=weapon*coefficient
    elif key=='hemorrhage':raw=weapon+D(t['hemorrhageBonusDamage'])
    elif key=='rupture':raw=D(t['ruptureTickDamagePerPoint'])*points*((D('1.15') if corrected else D('1.1')) if marked else 1)
    else:raise AssertionError('Unsupported oracle ability')
    if event['resultKey'] in ['miss','dodge','parry']:return D(0),D(0),D(0)
    require(event['resultKey'] in ['hit','critical'],'Unexpected damage result')
    require(event['isCritical']==(event['resultKey']=='critical'),'Critical flag/result mismatch')
    if event['isCritical']:
        raw*=2+(D(request['talents']['ranks'].get('lethality',0))*D(t['lethalityBonusCriticalDamagePerRank']) if key in ['ghostly-strike','hemorrhage'] else 0)
    armor=D(request['targetArmor'])
    require(armor in [0,5500] and D(t['armorDamageReductionConstant'])==5500,'Oracle fixtures only permit exact0/half armor')
    mitigated=raw/2 if armor and key!='rupture' else D(0)
    return raw,raw-mitigated,mitigated


def causal_expected(request,before,input_corrected=False,default_control=False):
    """Reconstruct old/new events from requests and real ordered aura lifecycle.

    The model deliberately rejects conditional talents, multi-target/death cases,
    and prepared proxies with nonliteral stats rather than broadening a mask.
    Every leaf not explicitly reconstructed remains in the full comparison.
    input_corrected=True additionally validates a standalone current-code capture
    against literal new coefficients without requiring an untracked old corpus.
    """
    require(request['additionalTargets']==0 and not request['enableBossAttacks'] and not request['useUtilityAbilities'] and not request['useExposeArmor'],'Unmodeled causal encounter')
    ranks=request['talents']['ranks']
    if default_control:
        require(ranks=={'hemorrhage':1,'malice':5,'opportunity':2,'thousand-cuts':1} and request['specialization']=='subtlety' and request['durationSeconds']==120,'Unexpected default causal talent/scope')
    else:require(set(ranks)<={'ghostly-strike','hemorrhage','lethality'},'Unmodeled causal talent')
    require(request['character']['equipment']=={'items':[]} and request['character']['buffs']=={'selections':[]} and request['character']['talents']=={'selections':[]},'Unmodeled authored stat contribution')
    if not default_control and not request.get('level60Baseline'):require(set(request['character']['baseStats']['values'])<={'attack-power'},'Unmodeled primary stats')
    require(not any(a['died'] for a in before['run']['summary']['actorSummaries'].values()),'Causal fixture must not kill')
    expected=copy.deepcopy(before);timeline=expected['run']['timeline'];points=collections.defaultdict(int);snap={};marks={};pending={};awarded=set();threats=collections.defaultdict(collections.deque);changes=[];damage_proofs=[]
    generators={'sinister-strike','backstab','mutilate','hemorrhage','ambush','garrote','ghostly-strike','cheap-shot'}
    finishers={'slice-and-dice','rupture','venom','expose-armor','kidney-shot'}
    def set_value(obj,field,value,path):
        prior=obj[field]
        if O.encode(prior)!=O.encode(value):changes.append(dict(path=list(path)+( [field]),before=prior,after=value));obj[field]=value
    def signature(e):return (e['timeSeconds'],e['sourceActorKey'],e['targetActorKey'],e['abilityKey'],e['effectKey'])
    with localcontext() as ctx:
        ctx.prec=100
        for i,event in enumerate(timeline):
            key=event['abilityKey'];target=event['targetActorKey'];source=event['sourceActorKey'];time_s=D(event['timeSeconds']);kind=event['type'];path=['run','timeline',i]
            if kind==4 and key in finishers:pending[key]=target
            if kind==9:
                if key=='hemorrhage' and event['effectKey']=='hemorrhage-mark':
                    require(source=='rogue' and target=='target','HTTP own-mark scope changed')
                    marks[(target,source)]=(event['auraInstanceId'],time_s,time_s+15)
                if key=='garrote' and source=='rogue':points[target]=min(5,points[target]+1)
                if key in finishers and key in pending:
                    spent_target=pending.pop(key)
                    if key=='rupture':snap[spent_target]=max(1,points[spent_target])
                    points[spent_target]=0
            if kind==10 and key=='hemorrhage' and event['effectKey']=='hemorrhage-mark':
                mark=marks.get((target,source))
                if mark and mark[0]==event['auraInstanceId']:marks.pop((target,source))
            if kind==6:
                old_amount=event['amount'];new_amount=old_amount
                if key in (['rupture'] if default_control else ['ghostly-strike','hemorrhage','rupture']):
                    require(source=='rogue' and target=='target','Unexpected affected actor/target')
                    mark=marks.get((target,source));marked=bool(mark and mark[1]<=time_s<mark[2]);cp=snap.get(target,1)
                    old_raw,old_expected,old_mitigated=expected_damage(request,event,cp,marked,input_corrected)
                    require((event['rawAmount'],old_amount,event['mitigatedAmount'])==(old_raw,old_expected,old_mitigated),f'Old literal oracle disagrees at event{i} {key}: {(event["rawAmount"],old_amount,event["mitigatedAmount"])} != {(old_raw,old_expected,old_mitigated)} CP{cp} mark{marked}')
                    new_raw,new_amount,new_mitigated=expected_damage(request,event,cp,marked,True)
                    for field,value in [('rawAmount',new_raw),('amount',new_amount),('mitigatedAmount',new_mitigated)]:set_value(event,field,value,path)
                    if old_amount!=new_amount:
                        name={'ghostly-strike':'Ghostly Strike','rupture':'Rupture'}[key];boss='Level 63 Rogue training boss'
                        description=(f'{name} critically hit {boss} for {format_damage(new_amount)} damage.' if event['isCritical'] else f'{name} dealt {format_damage(new_amount)} damage to {boss}.')
                        old_description=(f'{name} critically hit {boss} for {format_damage(old_amount)} damage.' if event['isCritical'] else f'{name} dealt {format_damage(old_amount)} damage to {boss}.')
                        require(event['description']==old_description,'Unexpected old damage description')
                        set_value(event,'description',description,path)
                    damage_proofs.append(dict(event=i,ability=key,time=time_s,cpSnapshot=cp if key=='rupture' else None,liveOwnMark=marked if key=='rupture' else None,oldRaw=old_raw,newRaw=new_raw,oldAmount=old_amount,newAmount=new_amount))
                if old_amount>0:threats[signature(event)].append((old_amount,new_amount))
                if source=='rogue' and old_amount>0 and not event['isPeriodic'] and key in generators and event['abilityExecutionId'] not in awarded:
                    awarded.add(event['abilityExecutionId']);points[target]=min(5,points[target]+(2 if key=='mutilate' else 1))
                if source=='rogue' and key=='eviscerate' and event['resultKey'] not in ['miss','dodge','parry']:points[target]=0
            if kind==20:
                require(event['description']=='Rogue damage threat added to shared table.','Unexpected threat mechanic')
                require(threats[signature(event)],'Threat event lacks causal damage')
                old,new=threats[signature(event)].popleft();require(event['amount']==old,'Old threat != matching damage')
                set_value(event,'amount',new,path)
        require(all(not queue for queue in threats.values()),'Damage lacks matching threat')
        require(points['target']==before['endingComboPoints'],'Independent combo-point reconstruction disagrees')
        def sums(events):
            totals=collections.defaultdict(lambda:D(0))
            for e in events:
                if e['type']==6 and e['sourceActorKey']=='rogue' and e['targetActorKey']=='target':totals[e['abilityKey']]=dotnet_add(totals[e['abilityKey']],e['amount'])
            return totals
        old_totals,new_totals=sums(before['run']['timeline']),sums(timeline)
        def total(events):
            result=D(0)
            for e in events:
                if e['type']==6 and e['sourceActorKey']=='rogue' and e['targetActorKey']=='target':result=dotnet_add(result,e['amount'])
            return result
        old_total,new_total=total(before['run']['timeline']),total(timeline)
        require(new_total<request['targetMaximumHealth'],'Corrected target would die')
        def reconcile(obj,field,old,new,path):
            require(obj[field]==old,'Old summary disagrees with event reconstruction: '+str(path+[field]))
            set_value(obj,field,new,path)
        for obj,path in [(expected,[]),(expected['run']['summary'],['run','summary']),(expected['run']['summary']['actorSummaries']['rogue'],['run','summary','actorSummaries','rogue'])]:
            reconcile(obj,'damageDone',old_total,new_total,path)
        reconcile(expected,'dps',dotnet_divide(old_total,request['durationSeconds']),dotnet_divide(new_total,request['durationSeconds']),[])
        summary=expected['run']['summary'];rogue=summary['actorSummaries']['rogue'];target=summary['actorSummaries']['target']
        reconcile(target,'damageTaken',old_total,new_total,['run','summary','actorSummaries','target'])
        reconcile(expected['run']['customMetrics'],'rogue-boss-threat',old_total,new_total,['run','customMetrics'])
        for obj,path in [(expected['damageByAbility'],['damageByAbility']),(summary['damageDoneByAbility'],['run','summary','damageDoneByAbility']),(rogue['damageDoneByAbility'],['run','summary','actorSummaries','rogue','damageDoneByAbility']),(target['damageTakenByAbility'],['run','summary','actorSummaries','target','damageTakenByAbility'])]:
            require(set(obj)==set(old_totals),'Damage ability keys changed')
            for key in old_totals:reconcile(obj,key,old_totals[key],new_totals[key],path)
        for obj,path in [(summary['abilities'],['run','summary','abilities']),(rogue['abilities'],['run','summary','actorSummaries','rogue','abilities'])]:
            for key in old_totals:reconcile(obj[key],'damageDone',old_totals[key],new_totals[key],path+[key])
    return expected,dict(changes=changes,damageOracles=damage_proofs,targetHealth=dict(before=D(request['targetMaximumHealth'])-old_total,after=D(request['targetMaximumHealth'])-new_total,exposure='No target-current-health field exists in this response; no-kill is independently checked. Rogue Health is unaffected and compared exactly.'))


def differences(left,right,path=()):
    if isinstance(left,dict) and isinstance(right,dict):
        if set(left)!=set(right):return [dict(path=list(path),error='Object keys differ')]
        return [d for k in left for d in differences(left[k],right[k],path+(k,))]
    if isinstance(left,list) and isinstance(right,list):
        if len(left)!=len(right):return [dict(path=list(path),error='Array length differs')]
        return [d for i,(a,b) in enumerate(zip(left,right)) for d in differences(a,b,path+(i,))]
    return [] if O.encode(left)==O.encode(right) else [dict(path=list(path),expected=left,actual=right)]


def compare(args):
    before=pathlib.Path(args.baseline);after=pathlib.Path(args.corrected);out=pathlib.Path(args.out);out.mkdir(parents=True,exist_ok=False)
    old_rows={r['name']:r for r in read(before/'index.json')};new_rows={r['name']:r for r in read(after/'index.json')}
    require(set(old_rows)==set(new_rows) and len(old_rows)==127,'Complete immutable127-call inventory required')
    rows=[];failures=[]
    for name,old_row in old_rows.items():
        try:
            new_row=new_rows[name]
            for field in ['status','path','requestSha256','group','positive']:require(old_row.get(field)==new_row.get(field),name+': changed receipt '+field)
            raw_old=(before/(name+'.response.json')).read_bytes();raw_new=(after/(name+'.response.json')).read_bytes()
            require(sha(raw_old)==old_row['responseSha256'] and sha(raw_new)==new_row['responseSha256'],'Response raw hash mismatch')
            old,new=O.decode(raw_old),O.decode(raw_new);proof={};expected=copy.deepcopy(old)
            if old_row['requestSha256'] is not None:
                request_raw=(before/(name+'.request.json')).read_bytes();require(sha(request_raw)==old_row['requestSha256'],'Request raw hash mismatch');require(request_raw==(after/(name+'.request.json')).read_bytes(),'Raw request changed')
            if old_row['group']=='default':
                for value in [old['character']['id'],new['character']['id']]:require(isinstance(value,str) and O.GUID.fullmatch(value) and value!=O.ZERO_GUID,'Generated default Character ID invalid')
                proof['generatedDefaultCharacterId']={'before':old['character']['id'],'after':new['character']['id']};expected['character']['id']=new['character']['id']
            elif old_row['group']=='metadata':
                current,previous,stamp=O.metadata_projection(new,old,args.source_commit);new=current;expected=previous;proof['sourceStamp']=stamp
            elif old_row['group']=='causal' or name in DEFAULT_CAUSAL_NAMES:
                req=O.decode(request_raw)
                if name in DEFAULT_CAUSAL_NAMES:verify_default_request(name,req,read(before/'defaults-subtlety.response.json'))
                expected,proof=causal_expected(req,old,default_control=name in DEFAULT_CAUSAL_NAMES)
            # Preserve all literal anchors, identity joins/splits, event order and
            # unknown fields by normalizing the COMPLETE expected/current objects.
            diffs=differences(O.decode(O.stable(expected)),O.decode(O.stable(new)))
            write(out/(name+'.proof.json'),proof)
            require(not diffs,name+': full-envelope mismatch '+O.encode(diffs[:8]))
            rows.append(dict(name=name,comparison='causal-full-envelope' if old_row['group']=='causal' or name in DEFAULT_CAUSAL_NAMES else 'full-envelope',changedFields=len(proof.get('changes',[])),passed=True))
        except Exception as e:failures.append(dict(name=name,error=str(e)))
    summary=dict(passed=not failures,requests=len(old_rows),compared=len(rows),failures=failures,comparisons=rows,normalization=O.NORMALIZATION,
                 causalPolicy='Only individually reconstructed GS/Rupture damage/raw/mitigated/display text, corresponding threat, exact ability/player/target totals and DPS. No tolerance or blanket metadata/spec mask. Health unchanged except independently computed unexposed target current Health. All unchanged remainder compared.')
    write(out/'summary.json',summary);print(O.encode(summary));return 0 if not failures else 1

def main():
    p=argparse.ArgumentParser(description=__doc__);sub=p.add_subparsers(dest='command',required=True)
    cap=sub.add_parser('capture');cap.add_argument('--base',default='http://127.0.0.1:5269');cap.add_argument('--out',required=True);cap.add_argument('--phase',choices=['baseline','corrected'],required=True);cap.add_argument('--baseline');cap.add_argument('--source-commit',required=True)
    cmp=sub.add_parser('compare');cmp.add_argument('--baseline',required=True);cmp.add_argument('--corrected',required=True);cmp.add_argument('--out',required=True);cmp.add_argument('--source-commit',required=True)
    a=p.parse_args();return capture(a) if a.command=='capture' else compare(a)

if __name__=='__main__':raise SystemExit(main())
