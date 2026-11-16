#!/usr/bin/env python3
"""Bounded historical Cutthroat HTTP captures and exact response verification.

The API lifecycle belongs to the process owner. Capture directories are immutable.
Full local before/after evidence is separate from the bounded standalone smoke.
"""
import argparse
import collections
import concurrent.futures
import copy
from decimal import Decimal as D, localcontext
import hashlib
import importlib.util
import pathlib
import subprocess
import threading
import time
import urllib.error
import urllib.parse
import urllib.request

ROOT = pathlib.Path(__file__).resolve().parents[1]
S = importlib.util.spec_from_file_location('rogue_damage_contract', ROOT/'tools/solo-dps-rogue-subtlety-http.py')
M = importlib.util.module_from_spec(S); S.loader.exec_module(M)
O = M.O
require, sha, read, write = M.require, M.sha, M.read, M.write
ACCEPTED = '5580292a315ffe66baf16534982186cf1dabb55d'
PRODUCTION = ['WarcraftSim.Data/Forever/Rogue/ForeverRogueTalentBuild.cs', 'WarcraftSim.Api/Simulation/RogueCombatProcessor.cs']
RUN = '/api/sim/rogue/run'
CHARACTER = '/api/forever/character/preview-rogue'
ROTATION = '/api/forever/rotation/preview'
SHARED = '/api/forever/character/simulate-level60'
ID = '44444444-4444-4444-8444-444444444444'
SPECS = ['assassination', 'combat', 'subtlety']
RANKS = [None, 0, 1, 2, 3, 4, 5, -1, 6]


def prepared(request):
    request['character']['race'] = 'human'
    request.update(useProvisionalLevel60Baseline=False, useReviewedLevel60Baseline=False)
    request['level60Baseline'] = dict(race='human', classKey='rogue', level=60,
        strength=1, agility=1, stamina=1, intellect=1, spirit=1,
        observedUnbuffedMaximumHealth=4000, observedUnbuffedMeleeAttackPower=0,
        clientBuild='synthetic-controlled-test', sourceReference='literal-cutthroat-fixture-not-client-data',
        recordedAtUtc='2026-11-11T00:00:00Z')


def controlled(defaults, rank, spec='subtlety'):
    request = copy.deepcopy(defaults[spec])
    request.update(durationSeconds=120, captureTimeline=True, inputLatencyMs=0,
        startInStealth=False, useInstantPoison=False, mainHandPoison='none', offHandPoison='none',
        poisonChargesPerWeapon=0, targetMaximumHealth=1000000000, targetArmor=0,
        targetDodgePercent=0, targetParryPercent=0, isBehindTarget=True, bothWeaponsDaggers=True,
        mainHandWeaponType='dagger', offHandWeaponType='dagger', useUtilityAbilities=False,
        useExposeArmor=False, enableBossAttacks=False, additionalTargets=0,
        useProvisionalLevel60Baseline=False, useReviewedLevel60Baseline=False, level60Baseline=None)
    request['character'].update(id=ID, race='human', baseStats={'values': {'attack-power': 0}},
        equipment={'items': []}, buffs={'selections': []}, talents={'selections': []})
    request['talents'] = dict(assassinationPointsSpent=0, combatPointsSpent=0,
        subtletyPointsSpent=5, ranks={} if rank is None else {'cutthroat': rank})
    request['tuning'].update(energyPerSecond=60, mainHandWeaponDamage=100, mainHandSpeedSeconds=2,
        offHandWeaponDamage=40, offHandSpeedSeconds=2, mainHandHitPercent=100, offHandHitPercent=100,
        physicalCritPercent=0, sliceAndDiceHastePercent=0)
    return request


def envelope(request):
    return dict(classKey='rogue', specialization=request['specialization'],
        itemizedTalentMode=True, itemizedTalentRanks=copy.deepcopy(request['talents']['ranks']), rogue=copy.deepcopy(request))


def fixtures(defaults, mode='full'):
    cases = []
    def add(name, body, path=RUN, group='exact', baseline=200, corrected=None, trace=False, **extra):
        cases.append(dict(name=name, path=path, body=copy.deepcopy(body), group=group,
            baselineStatus=baseline, correctedStatus=baseline if corrected is None else corrected,
            trace=trace, **extra))
    # Genuine server-default derivatives, including explicit (not provisional) baselines.
    for spec in SPECS:
        for duration in ([20, 120] if mode == 'full' else [20]):
            for prep in ([False, True] if mode == 'full' else [False]):
                for latency in ([None, 0, 80] if mode == 'full' else [None]):
                    for seed in ([defaults[spec]['seed'], 7331] if mode == 'full' else [defaults[spec]['seed']]):
                        for bonus in ([13, D('17.25')] if mode == 'full' else [13]):
                            r=copy.deepcopy(defaults[spec]);r.update(durationSeconds=duration,captureTimeline=True,seed=seed)
                            r['character']['id']=ID;r['tuning']['mutilateBonusDamagePerHand']=bonus
                            if prep:prepared(r)
                            if latency is not None:r['inputLatencyMs']=latency
                            add(f'default-{spec}-{duration}-p{int(prep)}-l{latency}-s{seed}-m{bonus}',r)
    for rank in range(1,6):
        for duration in ([20,120] if mode=='full' else [120]):
            for seed in ([142003,7331] if mode=='full' else [142003]):
                for latency in ([0,80] if mode=='full' else [0]):
                    r=controlled(defaults,rank);r.update(durationSeconds=duration,seed=seed,inputLatencyMs=latency)
                    add(f'proc-r{rank}-{duration}-s{seed}-l{latency}',r,group='causal' if rank<=3 else 'admission',
                        baseline=200 if rank<=3 else 400,corrected=200,trace=True)
        if mode=='full':
            r=controlled(defaults,rank);prepared(r)
            add(f'proc-prepared-r{rank}',r,group='causal' if rank<=3 else 'admission',baseline=200 if rank<=3 else 400,corrected=200,trace=True)
    for rank in ([None,0,1,2,3,4,5,-1,6] if mode=='full' else [0,4,5,6]):
        for spec in SPECS:
            for prep in ([False,True] if mode=='full' else [False]):
                r=controlled(defaults,rank,spec);r.update(durationSeconds=20,bothWeaponsDaggers=False)
                if prep:prepared(r)
                valid=rank is None or 0<=rank<=5;new=rank in [4,5]
                add(f'admission-{spec}-r{rank}-p{int(prep)}',r,group='admission' if new else 'exact',baseline=400 if new or not valid else 200,corrected=200 if valid else 400)
        r=controlled(defaults,rank,'assassination');r['durationSeconds']=20
        valid=rank is None or 0<=rank<=5;new=rank in [4,5]
        for route,label in [(CHARACTER,'character'),(ROTATION,'rotation')]:
            add(f'preview-{label}-assassination-r{rank}',r if route==CHARACTER else envelope(r),route,
                group='admission' if new else 'exact',baseline=400 if new or not valid else 200,corrected=200 if valid else 400)
    for spec in ['assassination','combat']:
        strict=copy.deepcopy(defaults[spec]);strict.update(durationSeconds=20,captureTimeline=True,useProvisionalLevel60Baseline=True)
        strict['character'].update(id=ID,race='human')
        # Strict preset ranks are read from the real checked-in preset, not legacy defaults.
        preset=read(ROOT/f'WarcraftSim.Api/wwwroot/itemized-presets/rogue-{spec}.json')
        strict['talents']=dict(assassinationPointsSpent=preset.get('assassinationPointsSpent',0),combatPointsSpent=preset.get('combatPointsSpent',0),subtletyPointsSpent=0,ranks=copy.deepcopy(preset['ranks']))
        # Preset wire format carries tree totals separately in some accepted files.
        strict['talents'][spec+'PointsSpent']=sum(preset['ranks'].values())
        for rank in (range(6) if mode=='full' else [0,5]):
            r=copy.deepcopy(strict);r['talents']['ranks']['cutthroat']=rank
            for owner in (['typed','full','both'] if mode=='full' else ['both']):
                env=envelope(strict)
                if owner in ['typed','both']:env['rogue']['talents']['ranks']['cutthroat']=rank
                if owner in ['full','both']:env['itemizedTalentRanks']['cutthroat']=rank
                add(f'strict-{spec}-r{rank}-{owner}',env,SHARED,baseline=400)
            if spec=='combat':add(f'strict-preview-combat-r{rank}',r,CHARACTER,baseline=400)
            for route in ['validate','validate-itemized']:
                add(f'strict-{spec}-{route}-r{rank}',r['talents'],f'/api/forever/talents/rogue/{spec}/{route}',baseline=400)
        if mode=='full':
            for value in [-1,6,D('1.5'),'invalid']:
                for route in ['validate','validate-itemized']:
                    body={'ranks':{'cutthroat':value}}
                    add(f'malformed-{spec}-{route}-{value}',body,f'/api/forever/talents/rogue/{spec}/{route}',baseline=400)
    for spec,route,label in [('subtlety',CHARACTER,'character'),('subtlety',ROTATION,'rotation'),('combat',ROTATION,'rotation')]:
        r=controlled(defaults,0,spec);add(f'unavailable-{label}-{spec}',r if route==CHARACTER else envelope(r),route,baseline=400)
    for rank in [None,0,3]:
        for label,mutate in [('front',lambda r:r.update(isBehindTarget=False)),('not-daggers',lambda r:r.update(bothWeaponsDaggers=False)),
                ('miss',lambda r:r['tuning'].update(mainHandHitPercent=0,offHandHitPercent=0)),('dodge',lambda r:r.update(targetDodgePercent=100))]:
            if mode!='full' and rank!=3:continue
            r=controlled(defaults,rank);r['durationSeconds']=20;mutate(r)
            add(f'ineligible-r{rank}-{label}',r)
    if mode=='full':
        for label,mutate in [('crit',lambda r:r['tuning'].update(physicalCritPercent=100)),('armor',lambda r:r.update(targetArmor=5500)),('stealth',lambda r:r.update(startInStealth=True))]:
            r=controlled(defaults,3);mutate(r);add('proc-'+label,r,group='causal',trace=True)
        for route,label in [(CHARACTER,'character'),(ROTATION,'rotation')]:
            for field,value in [('level',59),('specializationKey','combat')]:
                r=controlled(defaults,3,'assassination');r['character'][field]=value
                add(f'identity-{label}-{field}',r if route==CHARACTER else envelope(r),route,baseline=400)
        for spec in SPECS:
            r=controlled(defaults,3,spec);r['talents']['subtletyPointsSpent']=2
            add(f'declared-total-{spec}',r,baseline=400)
    # Same bytes for sequential/repeated/concurrent requests of each real latency.
    for latency in [None,0,80,1000]:
        r=controlled(defaults,3);r.pop('inputLatencyMs',None)
        if latency is not None:r['inputLatencyMs']=latency
        add(f'timing-{latency}',r,group='causal',trace=True)
        if latency is not None:
            add(f'repeat-{latency}',r,group='causal',trace=True)
            add(f'concurrent-{latency}',r,group='causal',trace=True,concurrent=True)
    require(len(cases)==len({c['name'] for c in cases}),'Duplicate fixture names')
    return cases


def provenance():
    paths=PRODUCTION+[str(p.relative_to(ROOT)) for p in sorted(ROOT.glob('WarcraftSim.*/bin/Release/net10.0/WarcraftSim.*.dll'))]
    return dict(head=M.git('rev-parse','HEAD'),tree=M.git('rev-parse','HEAD^{tree}'),files={p:sha((ROOT/p).read_bytes()) for p in paths},trackedDiff=M.git('diff','--stat'))


def checked_body(root,row):
    raw=(root/(row['name']+'.response.json')).read_bytes()
    require(sha(raw)==row['responseSha256'],'Raw response hash differs: '+row['name'])
    if row['requestSha256'] is not None:
        request=(root/(row['name']+'.request.json')).read_bytes()
        require(sha(request)==row['requestSha256'],'Raw request hash differs: '+row['name'])
    return O.decode(raw)


def exact_response(old,new,row,old_stamp,new_stamp):
    left,right=copy.deepcopy(old),copy.deepcopy(new);allowances=[]
    if row['path'].startswith('/api/sim/rogue/defaults?'):
        a,b=left['character']['id'],right['character']['id']
        require(all(isinstance(v,str) and O.GUID.fullmatch(v) and v!=O.ZERO_GUID for v in [a,b]),'Invalid generated default ID')
        right['character']['id']=a;allowances.append(dict(path=['character','id'],before=a,after=b))
    elif row['path']==M.METADATA:
        require(left['sourceCommit']==old_stamp,'Old source stamp differs')
        right,left,allowance=O.metadata_projection(right,left,new_stamp);allowances.append(allowance)
    diffs=M.differences(O.decode(O.stable(left)),O.decode(O.stable(right)))
    require(not diffs,'Complete response differs: '+O.encode(diffs[:5]))
    return allowances


def capture(args):
    u=urllib.parse.urlsplit(args.base)
    require(u.scheme=='http' and u.hostname in ['localhost','127.0.0.1','::1'] and not u.username and not u.password and not u.query and not u.fragment and u.path in ['','/'],'Loopback API root required')
    out=pathlib.Path(args.out);out.mkdir(parents=True,exist_ok=False);started=time.monotonic_ns()
    before=provenance();write(out/'provenance-before.json',before)
    if args.phase=='baseline':
        require(before['head']==ACCEPTED,'Baseline must use accepted 5580292a HEAD')
        for p in PRODUCTION:require((ROOT/p).read_bytes()==subprocess.check_output(['git','show',ACCEPTED+':'+p],cwd=ROOT),'Baseline production changed: '+p)
    rows=[];lock=threading.Lock()
    def call(case,barrier=None):
        body=case.get('raw')
        if body is None and case.get('body') is not None:body=O.encode(case['body']).encode()
        name=case['name'];expected=case[args.phase+'Status']
        if body is not None:(out/(name+'.request.json')).write_bytes(body)
        req=urllib.request.Request(args.base.rstrip('/')+case['path'],data=body,headers={'Content-Type':'application/json'})
        if barrier is not None:barrier.wait(timeout=30)
        begin=time.monotonic_ns()
        opener=urllib.request.build_opener(M.NoRedirect)
        try:
            with opener.open(req,timeout=90) as response:raw,status,headers=response.read(),response.status,dict(response.headers)
        except urllib.error.HTTPError as error:raw,status,headers=error.read(),error.code,dict(error.headers)
        end=time.monotonic_ns();(out/(name+'.response.json')).write_bytes(raw)
        row={k:v for k,v in case.items() if k not in ['body','raw','status','requestSha256','responseSha256','headers','startedMonotonicNs','completedMonotonicNs','responseBytes','expectedStatus']}
        row.update(status=status,expectedStatus=expected,requestSha256=sha(body) if body is not None else None,responseSha256=sha(raw),responseBytes=len(raw),headers=headers,startedMonotonicNs=begin,completedMonotonicNs=end)
        with lock:rows.append(row);write(out/'index.json',rows)
        require(status==expected,f'{name}: HTTP {status}, expected {expected}: {raw[:500]!r}')
        return O.decode(raw)
    try:
        if args.baseline:
            prior=pathlib.Path(args.baseline);cases=[]
            for row in read(prior/'index.json'):
                checked_body(prior,row)
                raw=(prior/(row['name']+'.request.json')).read_bytes() if row['requestSha256'] is not None else None
                cases.append(dict(row,raw=raw))
        else:
            defaults={spec:call(dict(name='defaults-'+spec,path='/api/sim/rogue/defaults?spec='+spec,group='default',baselineStatus=200,correctedStatus=200,trace=False)) for spec in SPECS}
            call(dict(name='report-metadata',path=M.METADATA,group='metadata',baselineStatus=200,correctedStatus=200,trace=False))
            cases=fixtures(defaults,args.mode)
        write(out/'fixture-inventory.json',[{k:v for k,v in c.items() if k not in ['body','raw','headers']} for c in cases])
        for case in cases:
            if not case.get('concurrent'):call(case)
        concurrent_cases=[c for c in cases if c.get('concurrent')];barrier=threading.Barrier(len(concurrent_cases))
        with concurrent.futures.ThreadPoolExecutor(max_workers=len(concurrent_cases)) as pool:list(pool.map(lambda c:call(c,barrier),concurrent_cases))
        metadata=read(out/'report-metadata.response.json');require(metadata['sourceCommit']==args.source_commit,'Actual API stamp differs')
        after=provenance();write(out/'provenance-after.json',after)
        require(all(before[k]==after[k] for k in ['head','tree','files']),'Source or binary changed during capture')
        proof=validate_capture(out,args.phase)
        summary=dict(passed=True,phase=args.phase,mode=args.mode,requests=len(rows),elapsedNanoseconds=time.monotonic_ns()-started,sourceCommit=args.source_commit,proof=proof,normalization=O.NORMALIZATION)
        write(out/'summary.json',summary);print(O.encode(summary))
    except BaseException as error:
        write(out/'failure.json',dict(passed=False,error=repr(error),requests=len(rows)));raise


class SeededRandom:
    """The compatibility subtractive sequence selected by .NET Random(int seed).

    Keep integer samples, not binary floats. Comparison margins are checked so
    .NET's Double-to-Decimal rounding cannot move any retained threshold result.
    """
    def __init__(self,seed):
        self.state=[0]*56;sub=2147483647 if seed==-2147483648 else abs(seed)
        mj=161803398-sub;self.state[55]=mj;mk=1
        for i in range(1,55):
            ii=(21*i)%55;self.state[ii]=mk;mk=mj-mk
            if mk<0:mk+=2147483647
            mj=self.state[ii]
        for _ in range(4):
            for i in range(1,56):
                self.state[i]-=self.state[1+(i+30)%55]
                if self.state[i]<0:self.state[i]+=2147483647
        self.a=0;self.b=21;self.count=0
    def sample(self):
        self.a=1 if self.a+1>=56 else self.a+1;self.b=1 if self.b+1>=56 else self.b+1
        value=self.state[self.a]-self.state[self.b]
        if value==2147483647:value-=1
        if value<0:value+=2147483647
        self.state[self.a]=value;self.count+=1
        return value
    def percent(self):
        with localcontext() as ctx:
            ctx.prec=50
            return D(self.sample())*100/D(2147483647)


def roll(rng,request,event,application=False):
    draws=[];periodic=event.get('isPeriodic',False)
    tuning=request['tuning'];hand=event.get('weaponHandKey')
    def take(label,threshold):
        value=rng.percent();require(abs(value-D(threshold))>D('0.00000000001'),'RNG threshold too close for independent decimal conversion')
        draws.append(dict(draw=rng.count,kind=label,percent=value,threshold=threshold));return value
    hit=tuning['offHandHitPercent'] if hand=='off-hand' else tuning['mainHandHitPercent']
    if not periodic and take('hit',hit)>=hit:return 'miss',False,draws
    if not periodic:
        avoid=take('avoidance',request['targetDodgePercent'])
        if avoid<request['targetDodgePercent']:return 'dodge',False,draws
        if not request['isBehindTarget'] and avoid<request['targetDodgePercent']+request['targetParryPercent']:return 'parry',False,draws
    if application:return 'hit',False,draws
    # Prepared agility1 contributes 1/29% through the existing runtime calculator.
    crit=tuning['physicalCritPercent']+(M.dotnet_divide(D(1),D(29)) if request.get('level60Baseline') is not None else 0)
    critical=take('critical',min(100,crit))<min(100,crit)
    return 'critical' if critical else 'hit',critical,draws


EVENT_FIELDS = set('timeSeconds type sourceActorKey targetActorKey abilityKey abilityExecutionId autoAttackInstanceId effectKey auraInstanceId absorbInstanceId forcedTargetInstanceId forcedTargetActorKey encounterPhaseKey encounterEventKey encounterSequenceHitNumber encounterSequenceSelectedActorKeys schoolKey resolutionType weaponHandKey mitigationType resultKey rawAmount mitigatedAmount mitigationPercent blockedAmount absorbedAmount amount resourceKey resourceTotalAfter overhealingAmount isCritical effectDeliveryType isPeriodic isChannelTick isInternal description'.split())


def verify_event_envelopes(request,response):
    """Fail closed on every serialized event field, including text and decoys."""
    actor=request['character']['name'];boss='Level 63 Rogue training boss';executions={};completed={};autos={};auras={};identity_domains={}
    names={'backstab':'Backstab','ambush':'Ambush','sinister-strike':'Sinister Strike','eviscerate':'Eviscerate','rupture':'Rupture','garrote':'Garrote','slice-and-dice':'Slice and Dice','vanish':'Vanish','rogue-main-hand':'rogue-main-hand','rogue-off-hand':'rogue-off-hand'}
    for index,e in enumerate(response['run']['timeline']):
        require(set(e)==EVENT_FIELDS,'Event schema differs at '+str(index))
        kind,key,t=e['type'],e['abilityKey'],e['timeSeconds']
        want={k:None for k in EVENT_FIELDS};want.update(timeSeconds=t,type=kind,encounterSequenceSelectedActorKeys=[],isCritical=False,effectDeliveryType=0,isPeriodic=False,isChannelTick=False,isInternal=False)
        def identity(field):
            value=e[field];require(isinstance(value,str) and O.GUID.fullmatch(value) and value!=O.ZERO_GUID,'Invalid event identity '+field)
            require(value not in identity_domains or identity_domains[value]==field,'Identity joined across event domains')
            identity_domains[value]=field
            want[field]=value;return value
        if kind in [0,1]:
            require(t==(0 if kind==0 else request['durationSeconds']),'Simulation boundary time differs')
            want['description']='Simulation started.' if kind==0 else 'Simulation ended.'
        else:
            want.update(sourceActorKey='rogue',targetActorKey='target',abilityKey=key)
            if kind in [3,4]:
                require(key in names and not key.startswith('rogue-'),'Unknown cast key')
                eid=identity('abilityExecutionId')
                if kind==3:require(eid not in executions,'Joined cast identities');executions[eid]=(key,t)
                else:require(executions.get(eid)==(key,t) and eid not in completed,'Cast completion linkage differs');completed[eid]=(key,t)
                want['description']=f'{actor} started casting {names[key]}.' if kind==3 else f'{actor} completed {names[key]}.'
            elif kind==8:
                want.update(targetActorKey='rogue',amount=e['amount'],resourceKey='energy',resourceTotalAfter=e['resourceTotalAfter'],description=f'{actor} spent {M.format_damage(-e["amount"])} energy.')
                require(any(k==key and at==t for k,at in completed.values()),'Resource spend lacks same-time cast')
            elif kind==6:
                require(key in names and key not in ['vanish','slice-and-dice'],'Unexpected damage ability')
                periodic=key in ['rupture','garrote'] and e['isPeriodic'];auto=key.startswith('rogue-')
                effect='weapon-hit' if auto else key+('-tick' if key in ['rupture','garrote'] else '-hit')
                if not auto:
                    eid=identity('abilityExecutionId');require(eid in completed and completed[eid][0]==key,'Damage/cast identity linkage differs')
                    require(periodic or completed[eid][1]==t,'Direct damage time differs from cast')
                result=e['resultKey'];require(result in ['hit','critical','miss','dodge','parry'],'Unexpected damage result')
                critical=result=='critical';amount=e['amount']
                want.update(effectKey=effect,schoolKey='physical',resolutionType='melee',weaponHandKey=None if key in ['rupture','garrote'] else 'off-hand' if key=='rogue-off-hand' else 'main-hand',mitigationType='none',resultKey=result,rawAmount=e['rawAmount'],mitigatedAmount=e['mitigatedAmount'],amount=amount,isCritical=critical,effectDeliveryType=1 if periodic else 0,isPeriodic=periodic)
                if result in ['hit','critical']:
                    want.update(mitigationPercent=50 if request['targetArmor'] and not periodic else 0,blockedAmount=0,absorbedAmount=0)
                    want['description']=f'{names[key]} critically hit {boss} for {M.format_damage(amount)} damage.' if critical else f'{names[key]} dealt {M.format_damage(amount)} damage to {boss}.'
                elif key in ['rupture','garrote'] and not periodic:
                    want['description']=f'{names[key]} failed to apply to {boss} ({result}).'
                else:
                    want.update(mitigationPercent=0,blockedAmount=0,absorbedAmount=0)
                    want['description']=f'{names[key]} missed {boss}.' if result=='miss' else f'{boss} {"dodged" if result=="dodge" else "parried"} {names[key]}.'
            elif kind==20:
                require(key in names,'Unknown threat ability')
                want.update(effectKey='weapon-hit' if key.startswith('rogue-') else key+('-tick' if key in ['rupture','garrote'] else '-hit'),amount=e['amount'],description='Rogue damage threat added to shared table.')
            elif kind in [32,34]:
                require(key in ['rogue-main-hand','rogue-off-hand'],'Unknown auto lifecycle ability');aid=identity('autoAttackInstanceId')
                if kind==32:require(aid not in autos.values(),'Joined auto identity');autos[key]=aid
                else:require(autos.get(key)==aid,'Auto stop identity differs');autos.pop(key)
                want['description']=f'{actor} started {key} against {boss}.' if kind==32 else f'{actor} stopped {key}.'
            elif kind in [9,10]:
                aid=identity('auraInstanceId');effect=e['effectKey'];want['effectKey']=effect
                if key=='cutthroat':
                    require(effect=='cutthroat-opportunity','Cutthroat effect differs');aura='Cutthroat opportunity';target=actor
                elif key in ['rupture','garrote']:
                    require(effect==key+'-tick','Bleed aura effect differs');aura=key;target=boss
                elif key=='slice-and-dice':
                    require(effect=='slice-and-dice-aura','Slice aura effect differs');aura=key;target=actor
                elif key in ['vanish','opening-stealth']:
                    require(effect in ['vanish-stealth','stealth-state'],'Stealth aura effect differs');aura='rogue-stealth' if effect=='vanish-stealth' and kind==9 else 'Stealth';target=actor
                else:raise AssertionError('Unknown aura mechanism: '+str(key))
                want['targetActorKey']='target' if target==boss else 'rogue'
                if kind==9:
                    require(aid not in auras,'Aura identity reused');auras[aid]=key
                    want['description']=f'{aura} applied to {target} (stacks: 1).'
                else:
                    require(auras.get(aid)==key,'Aura removal identity differs');auras.pop(aid)
                    reason='removed' if key=='cutthroat' and 'removed on' in e['description'] else 'stealth broken' if key in ['vanish','opening-stealth'] else 'expired'
                    want['description']=f'{aura} {reason} on {target}.'
            else:raise AssertionError('Unknown event type '+str(kind))
        require(O.encode(e)==O.encode(want),'Complete event fields differ at '+str(index)+': '+O.encode(M.differences(want,e)))


def verify_static_summary(request,response,damage,total,energy):
    """All summary fields not derived by the numerical oracle are literal controls."""
    numeric='damageTaken healingDone healingReceived overhealingDone overhealingReceived absorptionDone absorptionReceived blockedDamageReceived'.split()
    maps='damageTakenByAbility healingDoneByAbility healingReceivedByAbility absorptionDoneByAbility absorptionReceivedByAbility blockedDamageReceivedByAbility'.split()
    summary=response['run']['summary'];expected=dict(seed=request['seed'],durationSeconds=request['durationSeconds'],damageDone=total,damageDoneByAbility=dict(damage),abilities=summary['abilities'],actorSummaries=summary['actorSummaries'],primaryActorDied=False,primaryActorDeathTimeSeconds=None)
    expected.update({k:0 for k in numeric});expected.update({k:{} for k in maps})
    require(O.encode(summary)==O.encode(expected),'Unexpected full run summary field')
    for key in ['rogue','target']:
        actor=summary['actorSummaries'][key];is_rogue=key=='rogue'
        expected=dict(actorKey=key,name=request['character']['name'] if is_rogue else 'Level 63 Rogue training boss',damageDone=total if is_rogue else 0,damageDoneByAbility=dict(damage) if is_rogue else {},abilities=actor['abilities'],startingResources={'energy':D(100)*request['startingEnergyPercent']/100} if is_rogue else {},endingResources={'energy':energy} if is_rogue else {},resourceStarvedSeconds=0,firstResourceStarvedAtSeconds=None,died=False,deathTimeSeconds=None)
        expected.update({k:0 for k in numeric});expected.update({k:{} for k in maps})
        if not is_rogue:expected.update(damageTaken=total,damageTakenByAbility=dict(damage),blockedDamageReceivedByAbility={k:0 for k in damage})
        require(O.encode(actor)==O.encode(expected),'Unexpected full actor summary field: '+key)
    require(set(summary['actorSummaries'])=={'rogue','target'},'Actor summary set changed')
    require(set(response['run'])=={'summary','timeline','processedEventCount','customMetrics'},'Run schema differs')
    require(type(response['run']['processedEventCount']) is int and response['run']['processedEventCount']>=len(response['run']['timeline'])-1,'Invalid internal event count')
    require(set(response)==set('specialization damageDone dps endingEnergy maximumEnergy maximumHealth endingHealth endingComboPoints poisonProcs finishersUsed stealthOpeners damageByAbility run modelingNotes'.split()),'Response schema differs')
    require(response['specialization']==request['specialization'],'Specialization changed')


def first_cause(request,old,new,a,b):
    left,right=O.decode(O.stable(old)),O.decode(O.stable(new));xs,ys=left['run']['timeline'],right['run']['timeline'];prefix=0
    while prefix<min(len(xs),len(ys)) and O.encode(xs[prefix])==O.encode(ys[prefix]):prefix+=1
    changes=M.differences(left,right);divergence=None
    for x,y in zip(a['procDraws'],b['procDraws']):
        require((x['draw'],x['percent'])==(y['draw'],y['percent']),'Proc RNG diverged before any explained chance outcome')
        if x['succeeded']!=y['succeeded']:divergence=dict(before=x,after=y);break
    if prefix==len(xs)==len(ys):
        expected=copy.deepcopy(old)
        # Identical exposed gameplay can retain a different number of consumed
        # aura expiry checks before the half-open run end. No other field differs.
        expiry_delta=sum(p['time']+10<request['durationSeconds'] for p in a['procDraws'] if p['succeeded'])-sum(p['time']+12<request['durationSeconds'] for p in a['procDraws'] if p['succeeded'])
        expected['run']['processedEventCount']+=expiry_delta
        require(O.stable(expected)==O.stable(new),'Equal public trace has unrelated envelope changes')
        return dict(equalEventPrefix=prefix,kind='no exposed divergence',scheduledExpiryCountDelta=expiry_delta,changedPaths=changes)
    ends=[events[prefix] for events in [xs,ys] if prefix<len(events)]
    if divergence is not None:
        cause=divergence['before'];require(cause['event']<prefix,'Probability cause is not before first divergence')
        require(any(e['type']==9 and e['abilityKey']=='cutthroat' and e['timeSeconds']==cause['time'] for e in ends),'First event divergence is not the proved Cutthroat application')
        require(cause['succeeded'] and not divergence['after']['succeeded'],'Corrected lower probability unexpectedly creates a first proc')
        return dict(equalEventPrefix=prefix,kind='literal probability',decision=divergence,changedPaths=changes)
    # A visible duration-only first difference must be the corrected expiry of
    # a shared opportunity, not an unrelated amount, metric or event.
    removed=next((e for e in ends if e['type']==10 and e['abilityKey']=='cutthroat'),None)
    require(removed is not None,'First divergence has no chance or window cause')
    require(any(p['succeeded'] and p['time']+10==removed['timeSeconds'] for p in a['procDraws']),'First removal does not match literal corrected expiry')
    return dict(equalEventPrefix=prefix,kind='literal window',expiryEvent=removed,changedPaths=changes)


def verified_inventory(root,analysis=None,mode=None):
    rows=read(root/'index.json');require(len(rows)==len({r['name'] for r in rows}),'Duplicate capture rows')
    summary=dict(mode=mode,requests=len(rows)) if mode else read(pathlib.Path(analysis)/'summary.json') if analysis else read(root/'summary.json')
    if mode is None:require(summary['passed'] is True,'Capture has no passing receipt')
    if analysis:require(summary['rawIndexSha256']==sha((root/'index.json').read_bytes()) and summary['capturePath']==str(root.resolve()),'Offline acceptance does not identify these raw receipts')
    mode=summary['mode'];require(mode in ['full','smoke'],'Unknown inventory mode')
    defaults={spec:read(root/f'defaults-{spec}.response.json') for spec in SPECS}
    expected={c['name']:c for c in fixtures(defaults,mode)}
    expected.update({f'defaults-{spec}':dict(path='/api/sim/rogue/defaults?spec='+spec,group='default',baselineStatus=200,correctedStatus=200,trace=False) for spec in SPECS})
    expected['report-metadata']=dict(path=M.METADATA,group='metadata',baselineStatus=200,correctedStatus=200,trace=False)
    require(set(expected)=={r['name'] for r in rows} and summary['requests']==len(expected),'Frozen complete inventory differs')
    for row in rows:
        case=expected[row['name']]
        for field in ['path','group','baselineStatus','correctedStatus','trace']:require(row[field]==case[field],'Frozen fixture classification differs')
        if 'body' in case:require((root/(row['name']+'.request.json')).read_bytes()==O.encode(case['body']).encode(),'Captured request is not its exact frozen GET-default derivative')
    return {r['name']:r for r in rows}


def verify_opportunity_lifecycle(events,decisions,phase):
    window=12 if phase=='baseline' else 10;live=None;pending_removals={};stealth=False;proof=[]
    successful=collections.deque(d for d in decisions if d['succeeded'])
    for i,e in enumerate(events):
        t=e['timeSeconds'];kind=e['type'];key=e['abilityKey']
        if kind==9 and key=='cutthroat':
            require(successful and successful[0]['event']<i and successful[0]['time']==t,'Opportunity has no exact seeded source event')
            cause=successful.popleft();live=dict(id=e['auraInstanceId'],applied=t,expires=t+window,event=i,cause=cause['event'])
            proof.append(dict(action='apply-or-refresh',**live))
        if kind==9 and key in ['vanish','opening-stealth']:stealth=True
        if kind==3 and key=='ambush':
            require(stealth or live is not None and live['applied']<=t<live['expires'],'Stale/consumed opportunity cannot enable Ambush')
            proof.append(dict(action='ambush-start',event=i,time=t,stealth=stealth,opportunity=copy.deepcopy(live)))
        if kind==4 and key in ['ambush','garrote','cheap-shot']:
            if key=='ambush' and live is not None:
                pending_removals[live['id']]=t;live=None
            stealth=False
        if kind==6 and e['amount']>0 and not e['isPeriodic']:stealth=False
        if kind==10 and key=='cutthroat':
            aid=e['auraInstanceId']
            if aid in pending_removals:
                require(pending_removals.pop(aid)==t,'Consumed opportunity removal timestamp differs')
            else:
                require(live is not None and aid==live['id'] and t==live['expires'],'Stale refresh identity or wrong expiry removed opportunity');live=None
            proof.append(dict(action='remove',event=i,time=t,id=aid))
        if kind==10 and key in ['vanish','opening-stealth']:stealth=False
    require(not successful and not pending_removals,'Missing opportunity application/consumption event')
    return proof


def verify_controlled_timing(request,response,phase):
    """Only high-regeneration isolated fixtures, never a general rotation model."""
    require(request['tuning']['energyPerSecond']==60 and request['startingEnergyPercent']==100 and request['tuning']['sliceAndDiceHastePercent']==0,'Unsupported controlled timing budget')
    require(request['bothWeaponsDaggers'] and request['isBehindTarget'],'Controlled priority requires reachable Backstab/Ambush')
    require(request['tuning']['mainHandSpeedSeconds']==2 and request['tuning']['offHandSpeedSeconds']==2,'Unsupported controlled auto clock')
    latency=D(request.get('inputLatencyMs',80))/D(1000);require(latency in [0,D('.08'),1],'Unsupported controlled input delay')
    duration=request['durationSeconds'];last=None;cp=0;in_combat=False;stealth=request['startInStealth'];op=None;vanish_ready=0;slice_until=0;rupture_until=0;auras={};autos={};proof=[]
    for i,e in enumerate(response['run']['timeline']):
        t=e['timeSeconds'];kind=e['type'];key=e['abilityKey']
        if kind==3:
            due=0 if last is None else last['time']+(0 if last['key']=='vanish' else 1)+latency
            require(t==due,f'Controlled action/input readiness differs at {i}: {t} != {due}')
            live=op is not None and t<op
            expected='ambush' if stealth or live else 'vanish' if in_combat and t>=vanish_ready else 'slice-and-dice' if cp>=2 and t>=slice_until else 'rupture' if cp>=4 and t>=rupture_until else 'eviscerate' if cp>=4 else 'backstab'
            require(key==expected,f'Controlled priority differs at {i}: {key} != {expected}')
            if key=='vanish':vanish_ready=t+300
            last=dict(time=t,key=key);proof.append(dict(event=i,expectedKey=expected,expectedTime=due,comboPoints=cp,liveOpportunity=live,stealth=stealth))
        if kind==4 and key in ['ambush','garrote','cheap-shot']:
            in_combat=True;stealth=False
            if key=='ambush':op=None
        if kind==9:
            if key=='cutthroat':op=t+(12 if phase=='baseline' else 10)
            elif key in ['vanish','opening-stealth']:stealth=True
            elif key in ['slice-and-dice','rupture']:
                expiry=t+6+(3 if key=='slice-and-dice' else 2)*max(1,cp);auras[e['auraInstanceId']]=(key,expiry)
                if key=='slice-and-dice':slice_until=expiry
                else:rupture_until=expiry
                cp=0
        if kind==10:
            if key in ['vanish','opening-stealth']:stealth=False
            if key in ['slice-and-dice','rupture']:require(auras.pop(e['auraInstanceId'],None)==(key,t),'Finisher aura expiration time differs')
        if kind==32:autos[key]=t+2
        if kind==34:require(key in autos,'Auto stop without active clock');autos.pop(key)
        if kind==6:
            if key.startswith('rogue-'):
                require(autos.get(key)==t,'Auto damage timestamp differs from fixed swing clock');autos[key]=t+2
            if e['amount']>0 and not e['isPeriodic']:
                in_combat=True;stealth=False
                if key in ['backstab','ambush','sinister-strike']:cp=min(5,cp+1)
            if key=='eviscerate' and e['resultKey'] not in ['miss','dodge','parry']:cp=0
    require(last is not None and last['time']+(0 if last['key']=='vanish' else 1)+latency>=duration,'Missing final eligible action')
    require(all(at>=duration for at in autos.values()),'Missing scheduled auto damage before run end')
    return proof


def half_mitigation(raw):
    return D(raw)/D(2)


def admission_control(request,control_request,actual,control,rank,row,stamp):
    expected=copy.deepcopy(request);expected.get('rogue',expected)['talents']['ranks']['cutthroat']=0
    if 'itemizedTalentRanks' in expected:expected['itemizedTalentRanks']['cutthroat']=0
    require(rank in [4,5] and O.encode(expected)==O.encode(control_request),'Admission control is not an exact rank-zero derivative')
    return exact_response(control,actual,row,stamp,stamp)


def trace_accounting(request,response,phase):
    """Independent literal accounting for controlled, single-target fixtures.

    Public timeline excludes internal scheduling checks. Their total remains in
    complete-response replay; exact stale-expiry mechanics belong to .NET tests.
    """
    ranks=request['talents']['ranks'];require(set(ranks)<= {'cutthroat'},'Trace oracle only accepts isolated Cutthroat ranks')
    require(not request['useInstantPoison'] and not request['enableBossAttacks'] and request['additionalTargets']==0,'Unsupported trace mechanism')
    require(request['character']['baseStats']['values']=={'attack-power':0},'Trace oracle requires AP0 authored stats')
    verify_event_envelopes(request,response)
    events=response['run']['timeline'];rank=ranks.get('cutthroat',0);window=12 if phase=='baseline' else 10
    chance=(20 if phase=='baseline' else 3)*rank;rng=SeededRandom(request['seed'])
    cp=0;rupture_cp=0;finishers=0;openers=0;stealth=request['startInStealth'];opportunity=None
    pending_procs=collections.deque();procs=[];nonstealth=[];damage_proofs=[];draws=[]
    damage=collections.defaultdict(lambda:D(0));threat=collections.defaultdict(collections.deque)
    total=D(0);energy=D(100)*request['startingEnergyPercent']/100;energy_time=D(0);started={};completed={}
    resource_proofs=[];aura_proofs=[];generators=set();aura_finisher={};first=True
    for i,event in enumerate(events):
        kind,key,t=event['type'],event['abilityKey'],D(event['timeSeconds'])
        require(t>=0 and t<=request['durationSeconds'],'Trace time outside run')
        require(i==0 or t>=events[i-1]['timeSeconds'],'Trace time reversal')
        require(not event['isInternal'],'Public trace unexpectedly contains internal event')
        if kind==3:
            execution=event['abilityExecutionId'];require(execution not in started,'Duplicate cast start');started[execution]=key
            if key=='ambush':
                live=opportunity is not None and opportunity['time']<=t<opportunity['expires']
                require(stealth or live,'Ambush started without live Stealth/Cutthroat')
                if not stealth:nonstealth.append(dict(event=i,time=t,opportunity=opportunity.copy()))
        if kind==4:
            execution=event['abilityExecutionId'];require(started.get(execution)==key and execution not in completed,'Unmatched/duplicate completed cast');completed[execution]=key
            if key=='vanish':stealth=True
            if key in ['ambush','garrote','cheap-shot']:
                openers+=1;stealth=False
                if key=='ambush':opportunity=None
            if key in ['rupture','slice-and-dice']:aura_finisher[key]=execution
        if kind==8:
            require(event['resourceKey']=='energy' and event['sourceActorKey']=='rogue','Unexpected resource mechanism')
            energy=min(D(100),energy+(t-energy_time)*request['tuning']['energyPerSecond']);energy_time=t
            costs={'backstab':60,'ambush':60,'sinister-strike':45,'eviscerate':35,'rupture':25,'slice-and-dice':25,'garrote':50}
            require(key in costs and event['amount']==-costs[key],'Unexpected energy cost')
            require(energy>=costs[key],'Insufficient energy before spend');energy-=costs[key]
            require(energy==event['resourceTotalAfter'],'Energy timeline balance differs')
            resource_proofs.append(dict(event=i,cost=costs[key],after=energy))
        if kind==9 and key in ['rupture','garrote']:
            result,critical,new_draws=roll(rng,request,event,application=True);draws.extend(dict(event=i,**d) for d in new_draws)
            require(result=='hit','Seeded periodic application should not have landed')
        if kind==9 and key in ['rupture','slice-and-dice']:
            require(key in aura_finisher and cp>0,'Aura finisher lacks completed execution or CP')
            aura_finisher.pop(key)
            if key=='rupture':rupture_cp=cp
            finishers+=1;cp=0
        if kind==9 and key=='cutthroat':
            require(pending_procs,'Cutthroat application lacks eligible seeded Backstab')
            cause=pending_procs.popleft();require(cause['time']==t,'Cutthroat application time differs')
            opportunity=dict(time=t,expires=t+window,id=event['auraInstanceId'],causeEvent=cause['event'])
            aura_proofs.append(dict(event=i,**opportunity));cause['auraEvent']=i
        if kind==10 and key=='cutthroat':
            if opportunity is not None and event['auraInstanceId']==opportunity['id']:
                require(t==opportunity['expires'] or any(e['type']==4 and e['abilityKey']=='ambush' and e['timeSeconds']==t for e in events[:i]),'Unexplained Cutthroat removal')
                opportunity=None
        if kind==9 and event['effectKey'] in ['stealth-state','vanish-stealth']:stealth=True
        if kind==10 and key in ['opening-stealth','vanish']:stealth=False
        if kind==6:
            require(event['sourceActorKey']=='rogue' and event['targetActorKey']=='target','Unexpected damage ownership')
            application=key in ['rupture','garrote'] and not event['isPeriodic']
            result,critical,new_draws=roll(rng,request,event,application);draws.extend(dict(event=i,**d) for d in new_draws)
            require(event['resultKey']==result and event['isCritical']==critical,f'Seeded roll differs at event {i} {key}: {result}, actual {event["resultKey"]}')
            base={'backstab':D(235),'ambush':D(400),'sinister-strike':D(100)+request['tuning']['sinisterStrikeBonusDamage'],
                'rogue-main-hand':D(100),'rogue-off-hand':D(20),'eviscerate':request['tuning']['eviscerateDamagePerPoint']*max(1,cp),
                'rupture':request['tuning']['ruptureTickDamagePerPoint']*max(1,rupture_cp),'garrote':request['tuning']['garroteTickDamage']}
            require(key in base,'Unexpected trace ability: '+str(key))
            raw=base[key]*(2 if critical else 1) if result in ['hit','critical'] and not application else D(0)
            amount=raw
            if not event['isPeriodic'] and request['targetArmor']:
                armor=request['targetArmor'];constant=request['tuning']['armorDamageReductionConstant']
                require(armor==constant,'Trace armor oracle only admits exact half mitigation');amount=half_mitigation(raw)
            require(event['rawAmount']==raw and event['amount']==amount and event['mitigatedAmount']==raw-amount,f'Literal damage differs at {i}: {key}')
            damage[key]=M.dotnet_add(damage[key],amount);total=M.dotnet_add(total,amount)
            damage_proofs.append(dict(event=i,key=key,raw=raw,amount=amount,cp=cp,ruptureSnapshot=rupture_cp))
            if amount>0:threat[(t,key,event['effectKey'])].append(amount)
            if amount>0 and not event['isPeriodic']:
                stealth=False
                if key=='backstab' and rank>0:
                    value=rng.percent();require(abs(value-chance)>D('0.00000000001'),'Ambiguous proc boundary')
                    cause=dict(event=i,time=t,draw=rng.count,percent=value,threshold=chance,succeeded=value<chance)
                    procs.append(cause);draws.append(dict(event=i,kind='cutthroat',draw=rng.count,percent=value,threshold=chance))
                    if cause['succeeded']:pending_procs.append(cause)
                if key in ['backstab','ambush','sinister-strike']:
                    execution=event['abilityExecutionId']
                    if execution not in generators:cp=min(5,cp+1);generators.add(execution)
            if key=='eviscerate' and result not in ['miss','dodge','parry'] and cp>0:finishers+=1;cp=0
        if kind==20:
            signature=(t,key,event['effectKey']);require(threat[signature],'Threat lacks damage')
            require(event['amount']==threat[signature].popleft(),'Threat amount differs')
    require(not pending_procs and all(not q for q in threat.values()),'Missing proc or threat event')
    require(total<request['targetMaximumHealth'],'Controlled target died')
    energy=min(D(100),energy+(D(request['durationSeconds'])-energy_time)*request['tuning']['energyPerSecond'])
    require(response['endingEnergy']==energy and response['endingComboPoints']==cp,'Ending Energy/CP differs')
    require(response['finishersUsed']==finishers and response['stealthOpeners']==openers,'Finisher/opener count differs')
    require(response['damageDone']==total and response['dps']==M.dotnet_divide(total,request['durationSeconds']),'Overall damage/DPS differs')
    require(O.encode(response['damageByAbility'])==O.encode(dict(damage)),'Damage breakdown differs')
    summary=response['run']['summary'];rogue=summary['actorSummaries']['rogue'];target=summary['actorSummaries']['target']
    for obj,field in [(summary,'damageDone'),(rogue,'damageDone'),(target,'damageTaken')]:require(obj[field]==total,'Summary total differs')
    for obj,field in [(summary,'damageDoneByAbility'),(rogue,'damageDoneByAbility'),(target,'damageTakenByAbility')]:require(O.encode(obj[field])==O.encode(dict(damage)),'Summary breakdown differs')
    require(rogue['endingResources']['energy']==energy,'Ending resource summary differs')
    require(summary['seed']==request['seed'] and summary['durationSeconds']==request['durationSeconds'],'Run provenance differs')
    require(not rogue['died'] and not target['died'] and not summary['primaryActorDied'],'Unexpected death')
    metrics=response['run']['customMetrics'];want={'rogue-expose-armor-points':0,'rogue-stealth-openers':openers,'rogue-finisher-count':finishers,'rogue-poison-procs':0,'rogue-deadly-poison-ticks':0,'rogue-main-hand-poison-charges':0,'rogue-off-hand-poison-charges':0,'rogue-preparation-cooldowns-reset':0,'rogue-hack-and-slash-attacks':0,'rogue-quick-recovery-energy':0,'rogue-ending-combo-points':cp,'rogue-boss-threat':total,'rogue-feint-threat-reduced':0,'rogue-boss-attacks-dodged':0,'rogue-boss-attacks-parried':0}
    require(O.encode(metrics)==O.encode(want),'Complete custom metrics differ')
    require(response['maximumEnergy']==100 and response['poisonProcs']==0 and response['endingHealth']==response['maximumHealth'],'Unexpected resource/health cap')
    verify_ability_summaries(response)
    verify_static_summary(request,response,damage,total,energy)
    lifecycle=verify_opportunity_lifecycle(events,procs,phase)
    timing=verify_controlled_timing(request,response,phase)
    return dict(events=len(events),damageEvents=len(damage_proofs),backstabs=sum(1 for e in events if e['type']==6 and e['abilityKey']=='backstab' and e['amount']>0),
        opportunities=len(aura_proofs),nonStealthAmbushes=len(nonstealth),procDraws=procs,rngDraws=draws,
        damage=damage_proofs,resources=resource_proofs,auras=aura_proofs,ambushes=nonstealth,lifecycle=lifecycle,priorityTiming=timing,
        processedEventCount=response['run']['processedEventCount'],
        internalSchedulerProof='Public timeline excludes internal checks. Complete-response repeats retain this count; focused .NET tests prove stale expiry scheduling.')


def verify_ability_summaries(response):
    actual=response['run']['summary']['abilities'];expected={}
    count_fields=['castStartedCount','castCompletedCount','castCancelledCount','channelStartedCount','channelCompletedCount','channelCancelledCount','damageOccurrenceCount','healingOccurrenceCount','absorbConsumptionCount','criticalDamageCount','criticalHealingCount','directOccurrenceCount','periodicOccurrenceCount','channelTickOccurrenceCount']
    for e in response['run']['timeline']:
        if e['type'] not in [3,4,6]:continue
        require(e['sourceActorKey']=='rogue','Unexpected actor ability metric')
        key=e['abilityKey']
        if key not in expected:expected[key]=dict(abilityKey=key,**{k:0 for k in count_fields},damageDone=0,healingDone=0,overhealingDone=0,absorptionDone=0,resultCounts={})
        row=expected[key]
        if e['type']==3:row['castStartedCount']+=1
        if e['type']==4:row['castCompletedCount']+=1
        if e['type']==6:
            row['damageOccurrenceCount']+=1;row['damageDone']=M.dotnet_add(row['damageDone'],e['amount']);row['criticalDamageCount']+=int(e['isCritical'])
            field={0:'directOccurrenceCount',1:'periodicOccurrenceCount',2:'channelTickOccurrenceCount'}[e['effectDeliveryType']];row[field]+=1
            if e['resultKey']:row['resultCounts'][e['resultKey']]=row['resultCounts'].get(e['resultKey'],0)+1
    require(O.encode(actual)==O.encode(expected),'Complete ability summary differs from timeline')
    require(O.encode(response['run']['summary']['actorSummaries']['rogue']['abilities'])==O.encode(expected),'Actor ability summary differs')
    require(response['run']['summary']['actorSummaries']['target']['abilities']=={},'Unexpected target ability summary')


def validate_capture(out,phase,proof_out=None):
    proof_out=out if proof_out is None else proof_out
    rows=read(out/'index.json');require(len(rows)==len({r['name'] for r in rows}),'Duplicate capture rows')
    totals=collections.Counter();proofs=[]
    for row in rows:
        value=checked_body(out,row);require(row['status']==row[phase+'Status'],'Unexpected status in receipt')
        if row.get('trace') and row['status']==200:
            proof=trace_accounting(read(out/(row['name']+'.request.json')),value,phase)
            write(proof_out/(row['name']+'.trace.json'),proof)
            for k in ['backstabs','opportunities','nonStealthAmbushes']:totals[k]+=proof[k]
            proofs.append(row['name'])
    require(all(totals[k]>0 for k in ['backstabs','opportunities','nonStealthAmbushes']),'Real Backstab, Cutthroat and non-Stealth Ambush coverage required')
    for latency in [0,80,1000]:
        reference=read(out/f'timing-{latency}.response.json')
        for label in ['repeat','concurrent']:require(O.stable(reference)==O.stable(read(out/f'{label}-{latency}.response.json')),'Full latency replay differs')
    require(O.stable(read(out/'timing-None.response.json'))==O.stable(read(out/'timing-80.response.json')),'Omitted latency differs from 80')
    require(O.stable(read(out/'timing-0.response.json'))!=O.stable(read(out/'timing-80.response.json')),'Latency controls failed to differ')
    concurrent=[r for r in rows if r.get('concurrent')]
    require(len(concurrent)==3 and max(r['startedMonotonicNs'] for r in concurrent)<min(r['completedMonotonicNs'] for r in concurrent),'0/80/1000 requests did not actually overlap')
    return dict(traceCases=len(proofs),coverage=dict(totals),concurrentOverlapNanoseconds=min(r['completedMonotonicNs'] for r in concurrent)-max(r['startedMonotonicNs'] for r in concurrent))


def compare(args):
    before=pathlib.Path(args.baseline);after=pathlib.Path(args.corrected);out=pathlib.Path(args.out);out.mkdir(parents=True,exist_ok=False)
    old_rows=verified_inventory(before,args.baseline_analysis);new_rows=verified_inventory(after,args.corrected_analysis)
    require(set(old_rows)==set(new_rows),'Complete capture inventories differ')
    proofs=[];failures=[]
    for name,row in old_rows.items():
        try:
            other=new_rows[name]
            for field in ['path','group','trace','baselineStatus','correctedStatus','requestSha256']:require(row.get(field)==other.get(field),'Receipt differs: '+field)
            old,new=checked_body(before,row),checked_body(after,other)
            if row['requestSha256'] is not None:require((before/(name+'.request.json')).read_bytes()==(after/(name+'.request.json')).read_bytes(),'Original request bytes differ')
            proof=dict(name=name,group=row['group'],beforeStatus=row['status'],afterStatus=other['status'])
            require(row['status']==row['baselineStatus'] and other['status']==row['correctedStatus'],'Unexpected status change')
            if row['group']=='admission':
                require(row['status']==400 and other['status']==200,'Admission receipt must be 400 to 200')
                req=read(before/(name+'.request.json'));typed=req.get('rogue',req);rank=typed['talents']['ranks']['cutthroat'];require(rank in [4,5],'Admission rank outside 4/5')
                require(old=={'error':f'Unsupported or invalid Rogue talent: cutthroat={rank}.'},'Old rejection cause differs')
                proof['comparison']='Explicit rank-4/5 admission; never compare an old error to new combat'
                if row['trace']:proof['newTrace']=trace_accounting(typed,new,'corrected')
                else:
                    control=name.replace(f'-r{rank}-','-r0-') if '-r'+str(rank)+'-' in name else name[:-1]+'0'
                    require(control in new_rows,'Missing newly admitted preparation/run control')
                    admission_control(req,read(after/(control+'.request.json')),new,checked_body(after,new_rows[control]),rank,other,args.source_commit)
                    proof['equalAcceptedControl']=control
            elif row['group']=='causal':
                req=read(before/(name+'.request.json'));a=trace_accounting(req,old,'baseline');b=trace_accounting(req,new,'corrected')
                proof.update(comparison='Complete exposed trace accounting, first cause, all changed paths; not old/new equality',beforeTrace=a,afterTrace=b)
                proof['firstCause']=first_cause(req,old,new,a,b)
                for field in ['specialization','maximumEnergy','maximumHealth','endingHealth','poisonProcs','modelingNotes']:require(O.encode(old[field])==O.encode(new[field]),'Unrelated response field changed: '+field)
                require(set(old)==set(new) and set(old['run'])==set(new['run']),'Response schema changed')
            else:
                require(row['status']==other['status'],'Unexpected unchanged-control status')
                proof['allowances']=exact_response(old,new,row,args.old_source_commit,args.source_commit);proof['comparison']='Complete exact response'
            write(out/(name+'.proof.json'),proof);proofs.append({k:v for k,v in proof.items() if k not in ['beforeTrace','afterTrace','newTrace','changedPaths']})
        except Exception as error:failures.append(dict(name=name,error=str(error)))
    summary=dict(passed=not failures,requests=len(old_rows),compared=len(proofs),failures=failures,comparisons=proofs,normalization=O.NORMALIZATION)
    write(out/'summary.json',summary);print(O.encode({k:v for k,v in summary.items() if k!='comparisons'}));return 0 if not failures else 1


def preserve_damage(args):
    before=pathlib.Path(args.baseline);after=pathlib.Path(args.corrected);out=pathlib.Path(args.out);out.mkdir(parents=True,exist_ok=False)
    rows={r['name']:r for r in read(before/'index.json')};others={r['name']:r for r in read(after/'index.json')}
    require(set(rows)==set(others) and len(rows)==127,'All accepted 127 damage cases are required');proofs=[]
    for name,row in rows.items():
        other=others[name]
        for field in ['path','status','requestSha256','group','positive']:require(row.get(field)==other.get(field),'Accepted damage receipt differs: '+name+' '+field)
        old,new=checked_body(before,row),checked_body(after,other)
        if row['requestSha256'] is not None:require((before/(name+'.request.json')).read_bytes()==(after/(name+'.request.json')).read_bytes(),'Accepted damage request bytes differ')
        proofs.append(dict(name=name,allowances=exact_response(old,new,row,args.old_source_commit,args.source_commit)))
    summary=dict(passed=True,completeResponses=len(proofs),proofs=proofs,normalization=O.NORMALIZATION)
    write(out/'summary.json',summary);print(O.encode(dict(passed=True,completeResponses=len(proofs))));return 0



def replay_equality(args):
    """Full same-phase deterministic replay, including all affected responses."""
    before=pathlib.Path(args.baseline);after=pathlib.Path(args.corrected);out=pathlib.Path(args.out);out.mkdir(parents=True,exist_ok=False)
    rows=verified_inventory(before,args.baseline_analysis);others=verified_inventory(after,args.corrected_analysis)
    require(set(rows)==set(others),'Replay inventories differ');proofs=[]
    for name,row in rows.items():
        other=others[name]
        for field in ['path','status','requestSha256','group','trace']:require(row.get(field)==other.get(field),'Replay receipt differs: '+name+' '+field)
        old,new=checked_body(before,row),checked_body(after,other)
        if row['requestSha256'] is not None:require((before/(name+'.request.json')).read_bytes()==(after/(name+'.request.json')).read_bytes(),'Replay raw request differs')
        proofs.append(dict(name=name,allowances=exact_response(old,new,row,args.old_source_commit,args.source_commit)))
    summary=dict(passed=True,completeResponses=len(proofs),proofs=proofs,normalization=O.NORMALIZATION)
    write(out/'summary.json',summary);print(O.encode(dict(passed=True,completeResponses=len(proofs))));return 0



def analyze(args):
    """New analysis receipt over frozen raw evidence; never edit its directory."""
    root=pathlib.Path(args.capture);out=pathlib.Path(args.out);out.mkdir(parents=True,exist_ok=False)
    rows=read(root/'index.json')
    for row in rows:checked_body(root,row)
    before=read(root/'provenance-before.json');after=read(root/'provenance-after.json')
    require(all(before[k]==after[k] for k in ['head','tree','files']),'Captured source/binary identities changed')
    if args.phase=='baseline':require(before['head']==ACCEPTED,'Old raw capture does not identify accepted head')
    require(read(root/'report-metadata.response.json')['sourceCommit']==args.source_commit,'Actual captured API stamp differs')
    summary=dict(passed=False,mode=args.mode,phase=args.phase,sourceCommit=args.source_commit,requests=len(rows),capturePath=str(root.resolve()),rawIndexSha256=sha((root/'index.json').read_bytes()),analysis='Offline reanalysis of complete frozen raw capture; original failure preserved')
    # Verify complete fixture inventory before proof generation, retaining an
    # unsuccessful analysis receipt separately if any subsequent check fails.
    write(out/'summary.json',summary)
    try:
        verified_inventory(root,mode=args.mode)
        summary['proof']=validate_capture(root,args.phase,out)
        summary['toolSha256']=sha(pathlib.Path(__file__).read_bytes());summary['passed']=True
        write(out/'summary.json',summary);print(O.encode(summary));return 0
    except BaseException as error:
        summary.update(passed=False,error=repr(error));write(out/'summary.json',summary);raise


def main():
    parser=argparse.ArgumentParser(description=__doc__);commands=parser.add_subparsers(dest='command',required=True)
    cap=commands.add_parser('capture');cap.add_argument('--base',default='http://127.0.0.1:5271');cap.add_argument('--out',required=True);cap.add_argument('--phase',choices=['baseline','corrected'],required=True);cap.add_argument('--mode',choices=['full','smoke'],default='full');cap.add_argument('--baseline');cap.add_argument('--source-commit',required=True)
    ana=commands.add_parser('analyze');ana.add_argument('--capture',required=True);ana.add_argument('--out',required=True);ana.add_argument('--phase',choices=['baseline','corrected'],required=True);ana.add_argument('--mode',choices=['full','smoke'],default='full');ana.add_argument('--source-commit',required=True)
    for name in ['compare','preserve-damage','replay-equality']:
        p=commands.add_parser(name);p.add_argument('--baseline',required=True);p.add_argument('--corrected',required=True);p.add_argument('--out',required=True);p.add_argument('--old-source-commit',required=True);p.add_argument('--source-commit',required=True);p.add_argument('--baseline-analysis');p.add_argument('--corrected-analysis')
    args=parser.parse_args()
    return analyze(args) if args.command=='analyze' else capture(args) if args.command=='capture' else preserve_damage(args) if args.command=='preserve-damage' else replay_equality(args) if args.command=='replay-equality' else compare(args)

if __name__=='__main__':raise SystemExit(main())
