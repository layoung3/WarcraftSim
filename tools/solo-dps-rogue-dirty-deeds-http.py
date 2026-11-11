#!/usr/bin/env python3
"""Immutable Dirty Deeds captures; exact wire Decimal and source-aware trace proof.

Only the caller owns API lifecycle. No server/build process is started here.
Cheap Shot has no public priority and is deliberately absent from HTTP evidence.
"""
import argparse
import collections
import concurrent.futures
import copy
from decimal import Decimal as D
import importlib.util
import pathlib
import subprocess
import threading
import time
import urllib.error
import urllib.parse
import urllib.request

ROOT = pathlib.Path(__file__).resolve().parents[1]
S = importlib.util.spec_from_file_location('cutthroat_contract', ROOT/'tools/solo-dps-rogue-cutthroat-http.py')
K = importlib.util.module_from_spec(S); S.loader.exec_module(K)
M, O = K.M, K.O
require, sha, read, write = M.require, M.sha, M.read, M.write
ACCEPTED = '765c6c43267443ebf2ea12e66c40699626d94791'
PRODUCTION = ['WarcraftSim.Data/Forever/Rogue/ForeverRogueSpellbook.cs']
HELPERS = ['tools/solo-dps-rogue-dirty-deeds-http.py','tools/test_solo_dps_rogue_dirty_deeds_http.py',
 'tools/solo-dps-rogue-cutthroat-http.py','tools/solo-dps-rogue-subtlety-http.py','tools/rogue-combat-historical-http.py']
RUN, CHARACTER, ROTATION, SHARED = K.RUN, K.CHARACTER, K.ROTATION, K.SHARED
SPECS = K.SPECS
ID = '55555555-5555-4555-8555-555555555555'
checked_body = K.checked_body


def exact_response(old,new,row,old_stamp,new_stamp):
    # Permit only the three named existing GET identities, never a prefix decoy.
    if row['path'].startswith('/api/sim/rogue/defaults'):
        require(row['path'] in ['/api/sim/rogue/defaults?spec='+s for s in SPECS], 'Unknown default identity route')
    return K.exact_response(old,new,row,old_stamp,new_stamp)


def prepared(request):
    K.prepared(request)
    request['level60Baseline']['sourceReference']='literal-dirty-deeds-fixture-not-client-data'


def controlled(defaults,rank,spec='subtlety'):
    r=K.controlled(defaults,None,spec)
    r.update(startInStealth=True,startingEnergyPercent=100,bothWeaponsDaggers=False,
             mainHandWeaponType='sword',offHandWeaponType='sword',durationSeconds=20)
    r['character']['id']=ID
    r['talents']=dict(assassinationPointsSpent=0,combatPointsSpent=0,subtletyPointsSpent=2,
                     ranks={} if rank is None else {'dirty-deeds':rank})
    r['tuning']['energyPerSecond']=10
    return r


def envelope(r): return K.envelope(r)


def fixtures(defaults,mode='full'):
    cases=[]
    def add(name,r,path=RUN,group='exact',status=200,trace=False,**extra):
        cases.append(dict(name=name,path=path,body=copy.deepcopy(r),group=group,
            baselineStatus=status,correctedStatus=status,trace=trace,**extra))
    # Named GET derivatives cover unmodified defaults and saved tuning explicitly.
    for spec in SPECS:
        for duration in ([20,120] if mode=='full' else [20]):
            for bonus in ([0,13,D('17.25')] if mode=='full' else [13]):
                r=copy.deepcopy(defaults[spec]);r.update(durationSeconds=duration,captureTimeline=True)
                r['character']['id']=ID;r['tuning']['mutilateBonusDamagePerHand']=bonus
                add(f'default-{spec}-{duration}-m{bonus}',r)
    # Narrow source-aware oracle: ordinary regen, non-daggers, no other talent.
    for spec in SPECS:
        for rank in [1,2]:
            for behind in [False,True]:
                for energy in ([100,50,50-10*rank,D(50-10*rank)-D('.01'),D(50-10*rank)+D('.01')] if mode=='full' else [100,50-10*rank]):
                    for duration in ([D('.5'),20] if mode=='full' else [20]):
                        r=controlled(defaults,rank,spec);r.update(isBehindTarget=behind,startingEnergyPercent=energy,durationSeconds=duration)
                        add(f'garrote-{spec}-r{rank}-b{int(behind)}-e{energy}-d{duration}',r,group='causal',trace=True)
    if mode=='full':
        for rank in [None,0,1,2,-1,3]:
            for spec in SPECS:
                for behind in [False,True]:
                    r=controlled(defaults,rank,spec);r.update(isBehindTarget=behind,startInStealth=False,durationSeconds=D('.5'))
                    add(f'admission-{spec}-r{rank}-b{int(behind)}',r,status=400 if rank in [-1,3] else 200)
            for path,label in [(CHARACTER,'character'),(ROTATION,'rotation')]:
                r=controlled(defaults,rank,'assassination')
                add(f'preview-{label}-r{rank}',r if path==CHARACTER else envelope(r),path,status=400 if rank in [-1,3] else 200)
        for rank in [None,0]:
            for spec in SPECS:
                r=controlled(defaults,rank,spec)
                add(f'zero-{spec}-r{rank}',r,trace=True)
        # Repeated preparation, no-op when higher-priority Ambush consumes Stealth.
        for rank in [2,0,1,None]:
            r=controlled(defaults,rank);r.update(bothWeaponsDaggers=True,mainHandWeaponType='dagger',offHandWeaponType='dagger',durationSeconds=D('.5'))
            add(f'no-garrote-r{rank}',r)
        for rank in [1,2]:
            for spec in SPECS:
                r=controlled(defaults,rank,spec);prepared(r)
                add(f'prepared-{spec}-r{rank}',r,group='causal',trace=True)
            for seed in [1,7331]:
                r=controlled(defaults,rank);r.update(seed=seed,durationSeconds=120)
                add(f'long-r{rank}-s{seed}',r,group='causal',trace=True)
            for label,mutate in [
                ('armor',lambda r:r.update(targetArmor=5500)),
                ('crit',lambda r:r['tuning'].update(physicalCritPercent=100)),
                ('miss',lambda r:r['tuning'].update(mainHandHitPercent=0,offHandHitPercent=0)),
                ('dodge',lambda r:r.update(targetDodgePercent=100)),
                ('parry',lambda r:r.update(isBehindTarget=False,targetParryPercent=100)),
                ('zero-damage',lambda r:r['tuning'].update(garroteTickDamage=0,hemorrhageBonusDamage=0,mutilateBonusDamagePerHand=0)),
                ('custom-damage',lambda r:r['tuning'].update(garroteTickDamage=D('73.25'),hemorrhageBonusDamage=73,mutilateBonusDamagePerHand=D('17.25'))),
                ('modifiers',lambda r:r['talents'].update(assassinationPointsSpent=3,subtletyPointsSpent=5,ranks={'dirty-deeds':rank,'opportunity':2,'improved-garrote':3})),
                ('finite-poison',lambda r:r.update(useInstantPoison=True,mainHandPoison='instant',offHandPoison='deadly',poisonChargesPerWeapon=3)),
                ('ordinary-poison',lambda r:r.update(useInstantPoison=True,mainHandPoison='instant',offHandPoison='deadly',poisonChargesPerWeapon=100)),
                ('target-death',lambda r:r.update(targetMaximumHealth=1)),
                ('source-death',lambda r:r.update(enableBossAttacks=True,bossSwingSeconds=2,bossHitDamage=100000))]:
                r=controlled(defaults,rank);mutate(r)
                narrow=label in ['armor','crit','miss','dodge','parry','zero-damage','custom-damage','modifiers']
                add(f'variant-r{rank}-{label}',r,group='causal',trace=narrow)
        # Strict boundaries include explicit zero in either independent map.
        for spec in ['assassination','combat']:
            p=read(ROOT/f'WarcraftSim.Api/wwwroot/itemized-presets/rogue-{spec}.json')
            r=copy.deepcopy(defaults[spec]);r.update(durationSeconds=20,useProvisionalLevel60Baseline=True)
            r['character'].update(id=ID,race='human')
            r['talents']=dict(assassinationPointsSpent=0,combatPointsSpent=0,subtletyPointsSpent=0,ranks=copy.deepcopy(p['ranks']))
            r['talents'][spec+'PointsSpent']=sum(p['ranks'].values())
            for rank in [0,1,2]:
                for owner in ['typed','full','both']:
                    env=envelope(r)
                    if owner in ['typed','both']:env['rogue']['talents']['ranks']['dirty-deeds']=rank
                    if owner in ['full','both']:env['itemizedTalentRanks']['dirty-deeds']=rank
                    add(f'strict-{spec}-r{rank}-{owner}',env,SHARED,status=400)
                probe=copy.deepcopy(r);probe['talents']['ranks']['dirty-deeds']=rank
                if spec=='combat':add(f'strict-preview-combat-r{rank}',probe,CHARACTER,status=400)
                for route in ['validate','validate-itemized']:
                    add(f'strict-{spec}-{route}-r{rank}',probe['talents'],f'/api/forever/talents/rogue/{spec}/{route}',status=400)
            for value in [True,D('1.5'),'invalid','1',None]:
                for route in ['validate','validate-itemized']:
                    add(f'raw-{spec}-{route}-{value}',{'ranks':{'dirty-deeds':value}},f'/api/forever/talents/rogue/{spec}/{route}',status=400)
        for spec,path,label in [('subtlety',CHARACTER,'character'),('subtlety',ROTATION,'rotation'),('combat',ROTATION,'rotation')]:
            r=controlled(defaults,1,spec);add(f'unavailable-{label}-{spec}',r if path==CHARACTER else envelope(r),path,status=400)
        for spec in SPECS:
            r=controlled(defaults,2,spec);r['talents']['subtletyPointsSpent']=1
            add(f'invalid-total-{spec}',r,status=400)
        for path,label in [(CHARACTER,'character'),(ROTATION,'rotation')]:
            for field,value in [('level',59),('specializationKey','combat')]:
                r=controlled(defaults,1,'assassination');r['character'][field]=value
                add(f'identity-{label}-{field}',r if path==CHARACTER else envelope(r),path,status=400)
        # Characterize typed serializer binding separately. Diagnostic trace IDs
        # are never deleted from or compared as an equal complete response.
        for label,value in [('bool',True),('fraction',D('1.5')),('string','invalid'),('numeric-string','1'),('null',None),('null-map',None)]:
            r=controlled(defaults,1)
            if label=='null-map':r['talents']['ranks']=None
            else:r['talents']['ranks']['dirty-deeds']=value
            add('binding-'+label,r,group='binding',status=None)
    # Interleave rank changes and genuine concurrent requests at actual delays.
    for latency in [None,0,80,1000]:
        r=controlled(defaults,2);r.update(durationSeconds=120);r.pop('inputLatencyMs',None)
        if latency is not None:r['inputLatencyMs']=latency
        add(f'timing-{latency}',r,group='causal',trace=True)
        if latency is not None:
            add(f'repeat-{latency}',r,group='causal',trace=True)
            add(f'concurrent-{latency}',r,group='causal',trace=True,concurrent=True)
    require(len(cases)==len({c['name'] for c in cases}),'Duplicate names')
    return cases


def provenance():
    paths=PRODUCTION+HELPERS+[str(p.relative_to(ROOT)) for p in sorted(ROOT.glob('WarcraftSim.*/bin/Release/net10.0/WarcraftSim.*.dll'))]
    return dict(head=M.git('rev-parse','HEAD'),tree=M.git('rev-parse','HEAD^{tree}'),files={p:sha((ROOT/p).read_bytes()) for p in paths},trackedDiff=M.git('diff','--stat'))


def capture(args):
    u=urllib.parse.urlsplit(args.base)
    require(u.scheme=='http' and u.hostname in ['localhost','127.0.0.1','::1'] and not u.username and not u.password and not u.query and not u.fragment and u.path in ['','/'],'Loopback API root required')
    out=pathlib.Path(args.out);out.mkdir(parents=True,exist_ok=False);started=time.monotonic_ns()
    before=provenance();write(out/'provenance-before.json',before)
    if args.phase=='baseline':
        require(before['head']==ACCEPTED,'Baseline must use accepted 765c6c HEAD')
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
        require(expected is None or status==expected,f'{name}: HTTP {status}, expected {expected}: {raw[:500]!r}')
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
        proof=validate_capture(out,args.phase,mode=args.mode)
        summary=dict(passed=True,phase=args.phase,mode=args.mode,requests=len(rows),elapsedNanoseconds=time.monotonic_ns()-started,sourceCommit=args.source_commit,proof=proof,normalization=O.NORMALIZATION)
        write(out/'summary.json',summary);print(O.encode(summary))
    except BaseException as error:
        write(out/'failure.json',dict(passed=False,error=repr(error),requests=len(rows)));raise


def cost(rank,phase):
    require(rank in [0,1,2] and phase in ['baseline','corrected'],'Unsupported cost oracle input')
    return 50-(10*rank if phase=='corrected' else 0)


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
                    want.update(mitigationPercent=0)  # Failed direct/auto hit carries null block/absorb, not zero.
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
    starvation=starvation_accounting(request,response)
    numeric='damageTaken healingDone healingReceived overhealingDone overhealingReceived absorptionDone absorptionReceived blockedDamageReceived'.split()
    maps='damageTakenByAbility healingDoneByAbility healingReceivedByAbility absorptionDoneByAbility absorptionReceivedByAbility blockedDamageReceivedByAbility'.split()
    summary=response['run']['summary'];expected=dict(seed=request['seed'],durationSeconds=request['durationSeconds'],damageDone=total,damageDoneByAbility=dict(damage),abilities=summary['abilities'],actorSummaries=summary['actorSummaries'],primaryActorDied=False,primaryActorDeathTimeSeconds=None)
    expected.update({k:0 for k in numeric});expected.update({k:{} for k in maps})
    require(O.encode(summary)==O.encode(expected),'Unexpected full run summary field')
    for key in ['rogue','target']:
        actor=summary['actorSummaries'][key];is_rogue=key=='rogue'
        expected=dict(actorKey=key,name=request['character']['name'] if is_rogue else 'Level 63 Rogue training boss',damageDone=total if is_rogue else 0,damageDoneByAbility=dict(damage) if is_rogue else {},abilities=actor['abilities'],startingResources={'energy':D(100)*request['startingEnergyPercent']/100} if is_rogue else {},endingResources={'energy':energy} if is_rogue else {},resourceStarvedSeconds=starvation["total"] if is_rogue else 0,firstResourceStarvedAtSeconds=starvation["first"] if is_rogue else None,died=False,deathTimeSeconds=None)
        expected.update({k:0 for k in numeric});expected.update({k:{} for k in maps})
        if not is_rogue:expected.update(damageTaken=total,damageTakenByAbility=dict(damage),blockedDamageReceivedByAbility={k:0 for k in damage})
        require(O.encode(actor)==O.encode(expected),'Unexpected full actor summary field: '+key)
    require(set(summary['actorSummaries'])=={'rogue','target'},'Actor summary set changed')
    require(set(response['run'])=={'summary','timeline','processedEventCount','customMetrics'},'Run schema differs')
    require(type(response['run']['processedEventCount']) is int and response['run']['processedEventCount']>=len(response['run']['timeline'])-1,'Invalid internal event count')
    require(set(response)==set('specialization damageDone dps endingEnergy maximumEnergy maximumHealth endingHealth endingComboPoints poisonProcs finishersUsed stealthOpeners damageByAbility run modelingNotes'.split()),'Response schema differs')
    require(response['specialization']==request['specialization'],'Specialization changed')


def bound_request(request):
    """Explicit observed ASP.NET numeric-string binding, never rewrite wire data."""
    require(request['talents']['ranks']=={'dirty-deeds':'1'},'Only frozen numeric-string probe may use typed projection')
    result=copy.deepcopy(request);result['talents']['ranks']['dirty-deeds']=1
    return result


def starvation_accounting(request,response):
    """Readiness gaps for this bounded zero-cast-time priority.

    With a live target, positive regeneration and always-configured Sinister
    Strike, all gaps after input/GCD readiness end at the next successful cast.
    Vanish is the only off-GCD ability and retains actual top-level input delay.
    This does not claim reconstruction of unexposed scheduler event counts.
    """
    ready=D(0);total=D(0);first=None;intervals=[];latency=D(request.get('inputLatencyMs',80))/1000
    for e in response['run']['timeline']:
        if e['type']!=3:continue
        t=e['timeSeconds'];require(t>=ready,'Action precedes readiness in starvation accounting')
        if t>ready:
            intervals.append(dict(start=ready,end=t));total+=t-ready
            if first is None:first=ready
        ready=t+(0 if e['abilityKey']=='vanish' else 1)+latency
    if request['durationSeconds']>ready:
        intervals.append(dict(start=ready,end=request['durationSeconds']));total+=request['durationSeconds']-ready
        if first is None:first=ready
    return dict(total=total,first=first,intervals=intervals)


def verify_garrote_lifecycle(request,response):
    """Literal 18s/3s boundary-inclusive ticks, refresh cancellation and ownership."""
    live=None;completed=None;proof=[];dead_at=None
    for i,e in enumerate(response['run']['timeline']):
        if e['type']==13 and e['targetActorKey']=='target':
            dead_at=e['timeSeconds']
            if live is not None:require(live['nextTick']>=dead_at,'Garrote tick missing before target death')
        if e['abilityKey']!='garrote':continue
        t=e['timeSeconds'];kind=e['type']
        if kind==4:completed=dict(execution=e['abilityExecutionId'],time=t,source=e['sourceActorKey'],target=e['targetActorKey'])
        if kind==9:
            require(dead_at is None,'Garrote applied after target death')
            require(completed is not None and completed['time']==t and completed['source']==e['sourceActorKey'] and completed['target']==e['targetActorKey'],'Garrote application lacks same-time completed owner')
            if live is not None:
                require(live['nextTick']>=t and live['expires']>=t,'Garrote refresh skipped due tick/expiry')
                proof.append(dict(action='refresh-invalidates-old-schedule',event=i,oldAura=live['aura']))
            live=dict(aura=e['auraInstanceId'],execution=completed['execution'],source=e['sourceActorKey'],target=e['targetActorKey'],applied=t,nextTick=t+3,expires=t+18,ticks=0)
            proof.append(dict(action='apply',event=i,**live))
        if kind==6 and e['isPeriodic']:
            require(dead_at is None,'Garrote tick after target death')
            require(live is not None and t==live['nextTick'] and t<=live['expires'],'Garrote tick cadence/missing/extra tick differs')
            require(e['abilityExecutionId']==live['execution'] and e['sourceActorKey']==live['source'] and e['targetActorKey']==live['target'],'Garrote tick execution/source/target ownership differs')
            live['nextTick']+=3;live['ticks']+=1;proof.append(dict(action='tick',event=i,time=t,number=live['ticks'],aura=live['aura']))
        if kind==10:
            require(live is not None and e['auraInstanceId']==live['aura'] and t==live['expires'],'Garrote expiry identity/time differs')
            require(dead_at is not None or live['nextTick']>t and live['ticks']==6,'Garrote expiry missing boundary tick')
            require(e['sourceActorKey']==live['source'] and e['targetActorKey']==live['target'],'Garrote expiry ownership differs')
            proof.append(dict(action='expire',event=i,time=t,aura=live['aura']));live=None
    if live is not None:require((live['expires'] if dead_at is not None else min(live['nextTick'],live['expires']))>=request['durationSeconds'],'Missing Garrote tick or expiry before run end')
    return proof


def trace_accounting(request,response,phase):
    """Bounded independent resource, seeded resolver, CP and priority oracle.

    This is not a second general simulator. Unsupported talents/poisons/deaths
    must use separately labeled broad accounting and full seeded replay.
    """
    ranks=request['talents']['ranks'];require(set(ranks)<={'dirty-deeds','opportunity','improved-garrote'},'Unsupported trace talent')
    require(not request['useInstantPoison'] and not request['enableBossAttacks'] and request['additionalTargets']==0,'Unsupported trace mechanism')
    require(not request['bothWeaponsDaggers'] and request['character']['baseStats']['values']=={'attack-power':0},'Unsupported controlled weapons/stats')
    verify_event_envelopes(request,response)
    garrote_lifecycle=verify_garrote_lifecycle(request,response)
    events=response['run']['timeline'];rank=ranks.get('dirty-deeds',0);garrote=cost(rank,phase);rng=K.SeededRandom(request['seed'])
    cp=0;rupture_cp=0;finishers=0;openers=0;stealth=request['startInStealth'];in_combat=False
    damage=collections.defaultdict(lambda:D(0));threat=collections.defaultdict(collections.deque)
    total=D(0);energy=D(request['startingEnergyPercent']);energy_time=D(0)
    started={};completed={};generators=set();aura_finisher={};resources=[];draws=[];damage_proofs=[];priority=[];auras={};auto_clocks={}
    last_cast=None;vanish_ready=D(0);slice_until=D(0);rupture_until=D(0);latency=D(request.get('inputLatencyMs',80))/1000
    costs={'garrote':garrote,'vanish':0,'sinister-strike':45,'eviscerate':35,'rupture':25,'slice-and-dice':25}
    for i,e in enumerate(events):
        kind,key,t=e['type'],e['abilityKey'],D(e['timeSeconds'])
        require(t>=0 and t<=request['durationSeconds'] and (i==0 or t>=events[i-1]['timeSeconds']),'Trace time differs')
        require(not e['isInternal'],'Internal trace event exposed')
        if kind==3:
            available=min(D(100),energy+(t-energy_time)*request['tuning']['energyPerSecond'])
            candidates=[]
            if stealth and (request['isBehindTarget'] or rank>0):candidates.append('garrote')
            if not stealth and in_combat and t>=vanish_ready:candidates.append('vanish')
            if cp>=2 and t>=slice_until:candidates.append('slice-and-dice')
            if cp>=4 and t>=rupture_until:candidates.append('rupture')
            if cp>=4:candidates.append('eviscerate')
            candidates.append('sinister-strike')
            affordable=[k for k in candidates if costs[k]<=available]
            require(affordable and key==affordable[0],f'Priority/affordability differs at {i}: {key}, {affordable}, {available}')
            if last_cast is not None:
                ready=last_cast['time']+(0 if last_cast['key']=='vanish' else 1)+latency
                require(t>=ready,'Action precedes input/GCD readiness')
            else:ready=D(0)
            execution=e['abilityExecutionId'];require(execution not in started,'Duplicate cast start');started[execution]=key
            priority.append(dict(event=i,time=t,energy=available,candidates=candidates,affordable=affordable,inputReadyAt=ready,comboPoints=cp,stealth=stealth))
            last_cast=dict(key=key,time=t)
            if key=='vanish':vanish_ready=t+300
        if kind==4:
            execution=e['abilityExecutionId'];require(started.get(execution)==key and execution not in completed,'Invalid completion');completed[execution]=key
            if key=='vanish':stealth=True
            if key=='garrote':openers+=1;stealth=False;in_combat=True
            if key in ['rupture','slice-and-dice']:aura_finisher[key]=execution
        if kind==8:
            require(e['resourceKey']=='energy' and e['sourceActorKey']=='rogue','Unsupported resource event')
            energy=min(D(100),energy+(t-energy_time)*request['tuning']['energyPerSecond']);energy_time=t
            require(key in costs and e['amount']==-costs[key] and energy>=costs[key],'Literal cost/spend differs')
            before=energy;energy-=costs[key]
            require(energy==e['resourceTotalAfter'],'Energy regen/cap/spend balance differs')
            resources.append(dict(event=i,key=key,time=t,before=before,cost=costs[key],after=energy))
        if kind==9:
            if key in ['rupture','garrote']:
                result,critical,new=K.roll(rng,request,e,application=True)
                require(result=='hit','Seeded aura application differs');draws.extend(dict(event=i,**d) for d in new)
            if key=='garrote':cp=min(5,cp+1)
            if key in ['rupture','slice-and-dice']:
                require(key in aura_finisher and cp>0,'Finisher lacks completed cast/CP');aura_finisher.pop(key)
                expiry=t+6+(3 if key=='slice-and-dice' else 2)*max(1,cp)
                auras[e['auraInstanceId']]=(key,expiry)
                if key=='slice-and-dice':slice_until=expiry
                else:rupture_until=expiry;rupture_cp=cp
                finishers+=1;cp=0
            if key in ['opening-stealth','vanish']:stealth=True
        if kind==10:
            if key in ['opening-stealth','vanish']:stealth=False
            if key in ['rupture','slice-and-dice']:
                require(auras.pop(e['auraInstanceId'],None)==(key,t),'Finisher expiry differs')
        if kind==32:auto_clocks[key]=t+2
        if kind==34:require(key in auto_clocks,'Auto stop without clock');auto_clocks.pop(key)
        if kind==6:
            if key.startswith('rogue-'):
                require(auto_clocks.get(key)==t,'Auto swing time differs');auto_clocks[key]=t+2
            application=key in ['rupture','garrote'] and not e['isPeriodic']
            result,critical,new=K.roll(rng,request,e,application);draws.extend(dict(event=i,**d) for d in new)
            require(e['resultKey']==result and e['isCritical']==critical,f'Seeded roll differs at {i} {key}')
            base={'sinister-strike':D(100)+request['tuning']['sinisterStrikeBonusDamage'],
                  'rogue-main-hand':D(100),'rogue-off-hand':D(20),
                  'eviscerate':request['tuning']['eviscerateDamagePerPoint']*max(1,cp),
                  'rupture':request['tuning']['ruptureTickDamagePerPoint']*max(1,rupture_cp),
                  'garrote':request['tuning']['garroteTickDamage']*(1+D('.05')*ranks.get('opportunity',0))}
            require(key in base,'Unknown damage key')
            raw=base[key]*(2 if critical else 1) if result in ['hit','critical'] and not application else D(0)
            if key=='garrote':raw*=1+ranks.get('improved-garrote',0)*request['tuning']['improvedGarroteDamagePerRank']
            amount=raw
            if not e['isPeriodic'] and request['targetArmor']:
                require(request['targetArmor']==request['tuning']['armorDamageReductionConstant'],'Unsupported armor oracle');amount=raw/2
            require(e['rawAmount']==raw and e['amount']==amount and e['mitigatedAmount']==raw-amount,f'Literal damage differs at {i} {key}')
            damage[key]=M.dotnet_add(damage[key],amount);total=M.dotnet_add(total,amount)
            damage_proofs.append(dict(event=i,key=key,raw=raw,amount=amount,comboPoints=cp,rupturePoints=rupture_cp))
            if amount>0:threat[(t,key,e['effectKey'])].append(amount)
            if amount>0 and not e['isPeriodic']:
                stealth=False;in_combat=True
                if key=='sinister-strike' and e['abilityExecutionId'] not in generators:cp=min(5,cp+1);generators.add(e['abilityExecutionId'])
            if key=='eviscerate' and result not in ['miss','dodge','parry'] and cp>0:finishers+=1;cp=0
        if kind==20:
            signature=(t,key,e['effectKey']);require(threat[signature] and e['amount']==threat[signature].popleft(),'Threat differs')
    require(all(not q for q in threat.values()),'Missing threat event')
    require(total<request['targetMaximumHealth'],'Target unexpectedly died')
    energy=min(D(100),energy+(D(request['durationSeconds'])-energy_time)*request['tuning']['energyPerSecond'])
    require(response['endingEnergy']==energy and response['endingComboPoints']==cp,'Ending Energy/CP differs')
    require(response['finishersUsed']==finishers and response['stealthOpeners']==openers,'Finisher/opener metric differs')
    require(response['damageDone']==total and response['dps']==M.dotnet_divide(total,request['durationSeconds']),'Damage/DPS differs')
    require(O.encode(response['damageByAbility'])==O.encode(dict(damage)),'Damage breakdown differs')
    K.verify_ability_summaries(response);verify_static_summary(request,response,damage,total,energy)
    metrics=response['run']['customMetrics'];want={'rogue-expose-armor-points':0,'rogue-stealth-openers':openers,'rogue-finisher-count':finishers,'rogue-poison-procs':0,'rogue-deadly-poison-ticks':0,'rogue-main-hand-poison-charges':0,'rogue-off-hand-poison-charges':0,'rogue-preparation-cooldowns-reset':0,'rogue-hack-and-slash-attacks':0,'rogue-quick-recovery-energy':0,'rogue-ending-combo-points':cp,'rogue-boss-threat':total,'rogue-feint-threat-reduced':0,'rogue-boss-attacks-dodged':0,'rogue-boss-attacks-parried':0}
    require(O.encode(metrics)==O.encode(want),'Complete custom metrics differ')
    require(response['maximumEnergy']==100 and response['poisonProcs']==0 and response['endingHealth']==response['maximumHealth'],'Health/caps differ')
    return dict(events=len(events),garroteCasts=openers,garroteCost=garrote,resources=resources,rngDraws=draws,damage=damage_proofs,priority=priority,
                garroteLifecycle=garrote_lifecycle,starvation=starvation_accounting(request,response),processedEventCount=response['run']['processedEventCount'],internalSchedulerLimit='Internal checks are absent from public timeline; the count remains significant in complete seeded replays.')


def ability_summaries(events,actor):
    result={};fields='castStartedCount castCompletedCount castCancelledCount channelStartedCount channelCompletedCount channelCancelledCount damageOccurrenceCount healingOccurrenceCount absorbConsumptionCount criticalDamageCount criticalHealingCount directOccurrenceCount periodicOccurrenceCount channelTickOccurrenceCount'.split()
    for e in events:
        if e['sourceActorKey']!=actor or e['type'] not in [3,4,6]:continue
        key=e['abilityKey'];row=result.setdefault(key,dict(abilityKey=key,**{k:0 for k in fields},damageDone=0,healingDone=0,overhealingDone=0,absorptionDone=0,resultCounts={}))
        if e['type']==3:row['castStartedCount']+=1
        elif e['type']==4:row['castCompletedCount']+=1
        else:
            row['damageOccurrenceCount']+=1;row['damageDone']=M.dotnet_add(row['damageDone'],e['amount']);row['criticalDamageCount']+=int(e['isCritical'])
            row[{0:'directOccurrenceCount',1:'periodicOccurrenceCount',2:'channelTickOccurrenceCount'}[e['effectDeliveryType']]]+=1
            if e['resultKey']:row['resultCounts'][e['resultKey']]=row['resultCounts'].get(e['resultKey'],0)+1
    return result


def general_accounting(request,response,phase):
    """Bounded poison/target-death/incoming-damage arithmetic and strict schemas.

    The poison proc RNG sequence is deliberately not reconstructed here. Every
    field remains in complete seeded replay; independent accounting covers all
    Energy, CP, damage, poison/charge, threat, health/death and summary values.
    """
    ranks=request['talents']['ranks'];require(set(ranks)=={'dirty-deeds'},'Broad oracle only accepts Dirty Deeds')
    require(request['character']['baseStats']['values']=={'attack-power':0} and request['targetArmor']==0 and request['tuning']['physicalCritPercent']==0,'Broad oracle requires literal AP0/armor0/crit0')
    garrote_lifecycle=verify_garrote_lifecycle(request,response)
    events=response['run']['timeline'];rank=ranks['dirty-deeds'];names={'instant-poison':'Instant Poison','deadly-poison':'Deadly Poison'}
    narrow=[e for e in events if e['abilityKey'] not in names and e['type'] not in [13,17] and e['sourceActorKey']!='target']
    verify_event_envelopes(request,{'run':{'timeline':narrow}})
    costs={'garrote':cost(rank,phase),'vanish':0,'sinister-strike':45,'eviscerate':35,'rupture':25,'slice-and-dice':25}
    energy=D(request['startingEnergyPercent']);at=D(0);cp=0;rupture_cp=0;openers=0;finishers=0;pending={};generated=set();resources=[]
    done={a:collections.defaultdict(lambda:D(0)) for a in ['rogue','target']};taken={a:collections.defaultdict(lambda:D(0)) for a in ['rogue','target']};threat=collections.defaultdict(collections.deque)
    total_done={a:D(0) for a in done};total_taken={a:D(0) for a in done};health=D(request['targetMaximumHealth']);death=None;death_seen=False;last_damage=None
    charges={'main-hand':request['poisonChargesPerWeapon'],'off-hand':request['poisonChargesPerWeapon']};apps=0;ticks=0;eligible=collections.Counter();deadly=None;poison_proof=[];boss_times=[];boss_impacts=[];last_cast=None;vanish_ready=0;stealth=request['startInStealth'];in_combat=False;slice_until=0;rupture_until=0;latency=D(request.get('inputLatencyMs',80))/1000
    for i,e in enumerate(events):
        require(set(e)==EVENT_FIELDS,'Unknown event schema in broad trace')
        t,kind,key=e['timeSeconds'],e['type'],e['abilityKey'];require(0<=t<=request['durationSeconds'] and (i==0 or t>=events[i-1]['timeSeconds']),'Broad event ordering differs')
        if kind==3:
            require(death is None,'Cast after target death');available=min(D(100),energy+(t-at)*request['tuning']['energyPerSecond'])
            candidates=[]
            if stealth:candidates.append('garrote')
            if not stealth and in_combat and t>=vanish_ready:candidates.append('vanish')
            if cp>=2 and t>=slice_until:candidates.append('slice-and-dice')
            if cp>=4 and t>=rupture_until:candidates.append('rupture')
            if cp>=4:candidates.append('eviscerate')
            candidates.append('sinister-strike');affordable=[k for k in candidates if costs[k]<=available]
            require(affordable and key==affordable[0],'Broad priority affordability differs')
            if last_cast:require(t>=last_cast[1]+(0 if last_cast[0]=='vanish' else 1)+latency,'Broad GCD/input readiness differs')
            last_cast=(key,t)
            if key=='vanish':vanish_ready=t+300
        if kind==4:
            if key=='garrote':openers+=1;stealth=False;in_combat=True
            if key=='vanish':stealth=True
            if key in ['rupture','slice-and-dice']:pending[key]=e['abilityExecutionId']
        if kind==8:
            require(key in costs and e['amount']==-costs[key] and e['resourceKey']=='energy','Broad literal spend differs')
            energy=min(D(100),energy+(t-at)*request['tuning']['energyPerSecond']);at=t
            require(energy>=costs[key],'Broad unaffordable spend');energy-=costs[key]
            require(energy==e['resourceTotalAfter'],'Broad Energy balance differs');resources.append(dict(event=i,cost=costs[key],after=energy))
        if kind==9:
            if key=='garrote':cp=min(5,cp+1)
            if key in ['opening-stealth','vanish']:stealth=True
            if key in ['rupture','slice-and-dice']:
                require(key in pending and cp>0,'Broad finisher lacks CP/cast');pending.pop(key)
                if key=='rupture':rupture_cp=cp;rupture_until=t+6+2*cp
                else:slice_until=t+6+3*cp
                cp=0;finishers+=1
        if kind==10 and key in ['opening-stealth','vanish']:stealth=False
        # Full expected envelopes for every special event omitted from the
        # standard envelope validator above. No field is ignored or removed.
        if key in names or kind in [13,17] or e['sourceActorKey']=='target':
            want={f:None for f in EVENT_FIELDS};want.update(timeSeconds=t,type=kind,encounterSequenceSelectedActorKeys=[],isCritical=False,effectDeliveryType=0,isPeriodic=False,isChannelTick=False,isInternal=False)
            want.update(sourceActorKey='rogue',targetActorKey='target',abilityKey=key)
            if key in names:
                if kind==9:
                    require(key=='deadly-poison' and eligible[(t,'off-hand')]>0,'Deadly application lacks eligible hit');eligible[(t,'off-hand')]-=1
                    require(charges['off-hand']>0,'Deadly used exhausted coating');charges['off-hand']-=1;apps+=1
                    was_active=deadly is not None and deadly['expires']>t
                    stacks=min(5,deadly['stacks']+1) if was_active else 1
                    next_tick=deadly['nextTick'] if was_active else t+request['tuning']['deadlyPoisonTickIntervalSeconds']
                    aid=e['auraInstanceId'];require(isinstance(aid,str) and O.GUID.fullmatch(aid) and aid!=O.ZERO_GUID,'Invalid Deadly aura identity')
                    deadly=dict(stacks=stacks,expires=t+request['tuning']['deadlyPoisonDurationSeconds'],nextTick=next_tick,aura=aid)
                    want.update(effectKey='deadly-stack',auraInstanceId=aid,description=f'Deadly Poison applied to Level 63 Rogue training boss (stacks: {stacks}).')
                elif kind==10:
                    require(key=='deadly-poison' and deadly is not None and t==deadly['expires'] and e['auraInstanceId']==deadly['aura'],'Deadly expiry differs')
                    want.update(effectKey='deadly-stack',auraInstanceId=deadly['aura'],description='Deadly Poison expired on Level 63 Rogue training boss.')
                elif kind==6:
                    periodic=key=='deadly-poison'
                    if not periodic:
                        require(eligible[(t,'main-hand')]>0 and charges['main-hand']>0,'Instant application lacks eligible coating/hit');eligible[(t,'main-hand')]-=1;charges['main-hand']-=1;apps+=1
                        raw=request['tuning']['poisonDamage']
                    else:
                        require(deadly is not None and t==deadly['nextTick'] and t<=deadly['expires'],'Deadly tick schedule differs')
                        raw=request['tuning']['deadlyPoisonDamagePerStack']*deadly['stacks'];deadly['nextTick']+=request['tuning']['deadlyPoisonTickIntervalSeconds'];ticks+=1
                    want.update(effectKey='deadly-tick' if periodic else 'poison-hit',schoolKey='nature',resolutionType='spell',mitigationType='none',resultKey='hit',rawAmount=raw,mitigatedAmount=0,mitigationPercent=0,blockedAmount=0,absorbedAmount=0,amount=min(health,raw),effectDeliveryType=1 if periodic else 0,isPeriodic=periodic,description=f'{names[key]} dealt {M.format_damage(min(health,raw))} damage to Level 63 Rogue training boss.')
                    poison_proof.append(dict(event=i,key=key,raw=raw,charges=copy.deepcopy(charges)))
                elif kind==20:want.update(effectKey='deadly-tick' if key=='deadly-poison' else 'poison-hit',amount=e['amount'],description='Rogue damage threat added to shared table.')
                else:raise AssertionError('Unexpected poison event type')
            elif kind==13:
                require(death==t and not death_seen and last_damage is not None,'Death has no lethal damage');death_seen=True
                require(key==last_damage['abilityKey'],'Death ability differs')
                want.update(effectKey=last_damage['effectKey'],description='Level 63 Rogue training boss died.')
            elif kind==17:
                require(request['enableBossAttacks'] and t>0 and t%request['bossSwingSeconds']==0,'Unexpected encounter schedule');boss_times.append(t)
                want.update(sourceActorKey='target',targetActorKey='rogue',abilityKey=None,encounterEventKey='rogue-boss-swing',schoolKey='physical',rawAmount=request['bossHitDamage'],mitigationType='none')
            elif kind==6:
                require(key=='encounter:rogue-boss-swing' and t in boss_times,'Incoming damage lacks scheduled swing');boss_impacts.append(t)
                want.update(sourceActorKey='target',targetActorKey='rogue',encounterEventKey='rogue-boss-swing',schoolKey='physical',mitigationType='none',resultKey='hit',rawAmount=request['bossHitDamage'],mitigatedAmount=0,mitigationPercent=0,absorbedAmount=0,amount=request['bossHitDamage'],description=f'Encounter damage dealt {M.format_damage(request["bossHitDamage"])} damage to {request["character"]["name"]}.')
            else:raise AssertionError('Unexpected broad event type')
            require(O.encode(e)==O.encode(want),'Special event envelope differs at '+str(i)+': '+O.encode(M.differences(want,e)))
        if kind==6:
            source,target=e['sourceActorKey'],e['targetActorKey'];amount=e['amount'];require(source in done and target in done,'Unknown damage actor')
            if source=='rogue':
                require(death is None,'Damage after target death')
                if key not in names:
                    raw={'sinister-strike':D(100)+request['tuning']['sinisterStrikeBonusDamage'],'rogue-main-hand':D(100),'rogue-off-hand':D(20),'eviscerate':request['tuning']['eviscerateDamagePerPoint']*max(1,cp),'rupture':request['tuning']['ruptureTickDamagePerPoint']*max(1,rupture_cp),'garrote':request['tuning']['garroteTickDamage']}[key]
                    require(e['resultKey']=='hit' and not e['isCritical'] and e['rawAmount']==raw and e['mitigatedAmount']==0 and amount==min(health,raw),'Broad literal damage differs')
                health-=amount;last_damage=e
                if health==0:death=t
                if amount>0:threat[(t,key,e['effectKey'])].append(amount)
                if amount>0 and not e['isPeriodic']:
                    stealth=False;in_combat=True
                    if e['weaponHandKey'] in charges:eligible[(t,e['weaponHandKey'])]+=1
                    if key=='sinister-strike' and e['abilityExecutionId'] not in generated:generated.add(e['abilityExecutionId']);cp=min(5,cp+1)
                if key=='eviscerate' and cp>0:finishers+=1;cp=0
            done[source][key]=M.dotnet_add(done[source][key],amount);taken[target][key]=M.dotnet_add(taken[target][key],amount)
            total_done[source]=M.dotnet_add(total_done[source],amount);total_taken[target]=M.dotnet_add(total_taken[target],amount)
        if kind==20:
            q=threat[(t,key,e['effectKey'])];require(q and e['amount']==q.popleft(),'Broad threat differs')
    require(all(not q for q in threat.values()) and (death is None or death_seen),'Missing threat/death event')
    if request['enableBossAttacks']:
        expected=[];t=request['bossSwingSeconds']
        while t<request['durationSeconds']:expected.append(t);t+=request['bossSwingSeconds']
        require(boss_times==boss_impacts==expected,'Missing/extra boss swing')
    if deadly is not None:require(min(deadly['nextTick'],deadly['expires'])>=request['durationSeconds'],'Missing final Deadly tick/expiry')
    energy=min(D(100),energy+(request['durationSeconds']-at)*request['tuning']['energyPerSecond'])
    require(response['endingEnergy']==energy and response['endingComboPoints']==cp and response['finishersUsed']==finishers and response['stealthOpeners']==openers,'Broad resource/CP/openers/finishers differ')
    require(response['poisonProcs']==apps,'Poison proc total differs')
    require(response['damageDone']==total_done['rogue'] and response['dps']==M.dotnet_divide(total_done['rogue'],request['durationSeconds']) and O.encode(response['damageByAbility'])==O.encode(dict(done['rogue'])),'Broad damage result differs')
    require(response['maximumEnergy']==100 and response['endingHealth']==response['maximumHealth']==request['rogueMaximumHealth'],'Health-preservation/caps differ')
    expected_metrics={'rogue-expose-armor-points':0,'rogue-stealth-openers':openers,'rogue-finisher-count':finishers,'rogue-poison-procs':apps,'rogue-deadly-poison-ticks':ticks,'rogue-main-hand-poison-charges':charges['main-hand'],'rogue-off-hand-poison-charges':charges['off-hand'],'rogue-preparation-cooldowns-reset':0,'rogue-hack-and-slash-attacks':0,'rogue-quick-recovery-energy':0,'rogue-ending-combo-points':cp,'rogue-boss-threat':total_done['rogue'],'rogue-feint-threat-reduced':0,'rogue-boss-attacks-dodged':0,'rogue-boss-attacks-parried':0}
    require(O.encode(response['run']['customMetrics'])==O.encode(expected_metrics),'Broad custom metrics differ')
    actors={};numeric='healingDone healingReceived overhealingDone overhealingReceived absorptionDone absorptionReceived blockedDamageReceived'.split();maps='healingDoneByAbility healingReceivedByAbility absorptionDoneByAbility absorptionReceivedByAbility'.split()
    horizon=copy.deepcopy(request);horizon['durationSeconds']=min(request['durationSeconds'],death) if death is not None else request['durationSeconds'];starved=starvation_accounting(horizon,response)
    for actor in ['rogue','target']:
        primary=actor=='rogue';row=dict(actorKey=actor,name=request['character']['name'] if primary else 'Level 63 Rogue training boss',damageDone=total_done[actor],damageTaken=total_taken[actor],damageDoneByAbility=dict(done[actor]),damageTakenByAbility=dict(taken[actor]),abilities=ability_summaries(events,actor),startingResources={'energy':D(request['startingEnergyPercent'])} if primary else {},endingResources={'energy':energy} if primary else {},resourceStarvedSeconds=starved['total'] if primary else 0,firstResourceStarvedAtSeconds=starved['first'] if primary else None,died=not primary and death is not None,deathTimeSeconds=death if not primary else None,blockedDamageReceivedByAbility={key:0 for key in taken[actor]})
        row.update({k:0 for k in numeric});row.update({k:{} for k in maps});actors[actor]=row
    expected={k:actors['rogue'][k] for k in ['damageDone','damageTaken','damageDoneByAbility','damageTakenByAbility','abilities','blockedDamageReceivedByAbility']+numeric+maps}
    expected.update(seed=request['seed'],durationSeconds=request['durationSeconds'],actorSummaries=actors,primaryActorDied=False,primaryActorDeathTimeSeconds=None)
    require(O.encode(response['run']['summary'])==O.encode(expected),'Complete broad summary/schema differs: '+O.encode(M.differences(expected,response['run']['summary'])[:4]))
    require(set(response['run'])=={'summary','timeline','processedEventCount','customMetrics'} and type(response['run']['processedEventCount']) is int and response['run']['processedEventCount']>=len(events)-1,'Broad run schema/count differs')
    require(set(response)==set('specialization damageDone dps endingEnergy maximumEnergy maximumHealth endingHealth endingComboPoints poisonProcs finishersUsed stealthOpeners damageByAbility run modelingNotes'.split()) and response['specialization']==request['specialization'],'Broad response schema/identity differs')
    return dict(garroteLifecycle=garrote_lifecycle,resources=resources,poisonApplications=apps,poisonTicks=ticks,charges=charges,poisonEvents=poison_proof,damageByActor={k:dict(v) for k,v in done.items()},targetDeathTime=death,starvation=starved,
                scope='Bounded all-field event/summary schema and arithmetic reconciliation; poison/encounter RNG is not independently reconstructed and requires complete seeded replay. Incoming boss damage intentionally preserves Rogue health; filenames source-death are historical fixture labels, not source-death execution proof.')


def first_cause(request,old,new,phase_old='baseline',phase_new='corrected'):
    left,right=O.decode(O.stable(old)),O.decode(O.stable(new));xs,ys=left['run']['timeline'],right['run']['timeline'];prefix=0
    while prefix<min(len(xs),len(ys)) and O.encode(xs[prefix])==O.encode(ys[prefix]):prefix+=1
    changes=M.differences(left,right)
    if not changes:return dict(kind='exact unchanged execution',equalEventPrefix=prefix,changedPaths=[])
    rank=request['talents']['ranks'].get('dirty-deeds',0);require(rank in [1,2],'Changed response without positive rank')
    require(prefix<max(len(xs),len(ys)),'Summary-only unexplained divergence')
    a=xs[prefix] if prefix<len(xs) else None;b=ys[prefix] if prefix<len(ys) else None
    if a and b and a['type']==b['type']==8 and a['abilityKey']==b['abilityKey']=='garrote' and a['timeSeconds']==b['timeSeconds']:
        require(a['amount']==-50 and b['amount']==-cost(rank,'corrected'),'First spend is not literal correction')
        require(b['resourceTotalAfter']-a['resourceTotalAfter']==10*rank,'First post-spend balance differs')
        expected=copy.deepcopy(a);expected.update(amount=b['amount'],resourceTotalAfter=b['resourceTotalAfter'],description=b['description'])
        require(O.encode(expected)==O.encode(b),'First resource event has unrelated changes')
        require(a['description']==f'{request["character"]["name"]} spent 50 energy.' and b['description']==f'{request["character"]["name"]} spent {cost(rank,"corrected")} energy.','First spend description differs')
        cause=dict(kind='literal spend',before=a,after=b)
    else:
        require(b is not None and b['type']==3 and b['abilityKey']=='garrote','First changed action is not corrected Garrote affordability')
        t=b['timeSeconds'];energy=D(request['startingEnergyPercent']);at=D(0)
        for e in xs[:prefix]:
            if e['type']==8 and e['resourceKey']=='energy':energy=min(D(100),energy+(e['timeSeconds']-at)*request['tuning']['energyPerSecond'])+e['amount'];at=e['timeSeconds']
        available=min(D(100),energy+(t-at)*request['tuning']['energyPerSecond'])
        require(cost(rank,'corrected')<=available<50,'First predicate lacks old/new affordability split')
        require(request['startInStealth'] and not any(e['type']==4 and e['abilityKey'] in ['garrote','ambush','cheap-shot'] for e in xs[:prefix]),'First-affordability proof requires original live Stealth')
        cause=dict(kind='literal affordability',time=t,energy=available,oldCost=50,newCost=cost(rank,'corrected'),before=a,after=b)
    # In isolated 0.5-second equal-cast runs enumerate every permissible path.
    if request['durationSeconds']==D('.5') and cause['kind']=='literal spend':
        expected=copy.deepcopy(old)
        for e in expected['run']['timeline']:
            if e['type']==8 and e['abilityKey']=='garrote':
                e['amount']+=10*rank;e['resourceTotalAfter']+=10*rank
                e['description']=f'{request["character"]["name"]} spent {cost(rank,"corrected")} energy.'
        expected['endingEnergy']+=10*rank
        expected['run']['summary']['actorSummaries']['rogue']['endingResources']['energy']+=10*rank
        require(O.stable(expected)==O.stable(new),'Cost-only response contains unrelated changed fields')
        cause['costOnlyCompleteEnvelope']=True
    cause.update(equalEventPrefix=prefix,changedPaths=changes)
    return cause

def verified_inventory(root,analysis=None,mode=None):
    rows=read(root/'index.json');require(len(rows)==len({r['name'] for r in rows}),'Duplicate capture rows')
    if mode is None:
        summary=read(pathlib.Path(analysis)/'summary.json') if analysis else read(root/'summary.json')
        require(summary['passed'] is True,'Capture/analysis lacks passing receipt');mode=summary['mode']
        require(summary['requests']==len(rows),'Passing receipt inventory count differs')
        if analysis:require(summary['rawIndexSha256']==sha((root/'index.json').read_bytes()) and summary['capturePath']==str(root.resolve()),'Analysis index/path differs')
    require(mode in ['full','smoke'],'Unknown mode')
    defaults={s:read(root/f'defaults-{s}.response.json') for s in SPECS}
    expected={c['name']:c for c in fixtures(defaults,mode)}
    expected.update({f'defaults-{s}':dict(path='/api/sim/rogue/defaults?spec='+s,group='default',baselineStatus=200,correctedStatus=200,trace=False) for s in SPECS})
    expected['report-metadata']=dict(path=M.METADATA,group='metadata',baselineStatus=200,correctedStatus=200,trace=False)
    require(set(expected)=={r['name'] for r in rows},'Complete frozen inventory differs')
    for row in rows:
        checked_body(root,row);case=expected[row['name']]
        for field in ['path','group','baselineStatus','correctedStatus','trace']:require(row[field]==case[field],'Frozen fixture differs: '+field)
        if 'body' in case:require((root/(row['name']+'.request.json')).read_bytes()==O.encode(case['body']).encode(),'Frozen request differs: '+row['name'])
    return {r['name']:r for r in rows}


def validate_capture(out,phase,proof_out=None,mode='full'):
    proof_out=out if proof_out is None else proof_out
    rows=verified_inventory(out,mode=mode);totals=collections.Counter();bindings=[]
    for name,row in rows.items():
        value=checked_body(out,row)
        require(row[phase+'Status'] is None or row['status']==row[phase+'Status'],'Unexpected frozen status')
        if row['group']=='binding':
            bindings.append(dict(name=name,status=row['status'],responseSha256=row['responseSha256'],characterization=value if row['status']!=200 else 'Numeric string 1 accepted as typed integer; complete raw response retained and source-aware trace verified'))
            if row['status']==200:
                request=bound_request(read(out/(name+'.request.json')))
                proof=trace_accounting(request,value,phase);write(proof_out/(name+'.trace.json'),dict(binding='Observed typed numeric-string admission; raw input unchanged',proof=proof))
                totals['typedNumericStringTraceCases']+=1
            continue
        if row['status']==200 and row['trace']:
            proof=trace_accounting(read(out/(name+'.request.json')),value,phase);write(proof_out/(name+'.trace.json'),proof)
            totals['traceCases']+=1;totals['garroteCasts']+=proof['garroteCasts']
        elif row['status']==200 and row['group']=='causal':
            proof=general_accounting(read(out/(name+'.request.json')),value,phase);write(proof_out/(name+'.trace.json'),proof);totals['broadCases']+=1
        if row['path']==RUN and row['status']==200:
            require(not any(e['abilityKey']=='cheap-shot' for e in value['run']['timeline']),'Cheap Shot unexpectedly reached public priority')
    require(totals['garroteCasts']>0,'No actual Garrote')
    for latency in [0,80,1000]:
        ref=read(out/f'timing-{latency}.response.json')
        for label in ['repeat','concurrent']:exact_response(ref,read(out/f'{label}-{latency}.response.json'),dict(path=RUN),'same','same')
    require(O.stable(read(out/'timing-None.response.json'))==O.stable(read(out/'timing-80.response.json')),'Omitted delay differs from 80')
    require(O.stable(read(out/'timing-0.response.json'))!=O.stable(read(out/'timing-80.response.json')),'Latency fails to differ')
    concurrent=[r for r in rows.values() if r.get('concurrent')]
    overlap=min(r['completedMonotonicNs'] for r in concurrent)-max(r['startedMonotonicNs'] for r in concurrent)
    require(len(concurrent)==3 and overlap>0,'Real 0/80/1000 overlap required')
    return dict(coverage=dict(totals),concurrentOverlapNanoseconds=overlap,typedBindingCharacterization=bindings)


def analyze(args):
    root=pathlib.Path(args.capture);out=pathlib.Path(args.out);out.mkdir(parents=True,exist_ok=False)
    try:
        require(read(root/'report-metadata.response.json')['sourceCommit']==args.source_commit,'Captured stamp differs')
        captured_before,captured_after=read(root/'provenance-before.json'),read(root/'provenance-after.json')
        require(all(captured_before[k]==captured_after[k] for k in ['head','tree','files']),'Captured sources/binaries changed')
        proof=validate_capture(root,args.phase,out,args.mode)
        summary=dict(passed=True,capturePath=str(root.resolve()),rawIndexSha256=sha((root/'index.json').read_bytes()),phase=args.phase,mode=args.mode,
                     requests=len(read(root/'index.json')),sourceCommit=args.source_commit,proof=proof,normalization=O.NORMALIZATION,analysisToolFiles={p:sha((ROOT/p).read_bytes()) for p in HELPERS},capturedProvenanceSha256=sha((root/'provenance-before.json').read_bytes()))
        write(out/'summary.json',summary);print(O.encode(summary));return 0
    except BaseException as error:write(out/'failure.json',dict(passed=False,error=repr(error)));raise


def binding_comparison(old,new,a,b):
    require(a['status']==b['status'],'Typed binding status changed')
    # Preserve raw diagnostic fields; status/structural characterization is NOT
    # complete body equality, even if the particular execution happens to match.
    require(type(old)==type(new),'Typed binding response type changed')
    if isinstance(old,dict) and 'errors' in old:
        for key in ['type','title','status','errors']:require(O.encode(old.get(key))==O.encode(new.get(key)),'Typed binding diagnostic shape/errors changed')
        return dict(kind='Typed malformed-binding characterization only',before=old,after=new,bodyEqualityClaim=False)
    require(a['status']!=200,'Successful typed binding requires complete replay or causal proof, not diagnostic characterization')
    require(O.encode(old)==O.encode(new),'Deterministic domain binding failure differs')
    return dict(kind='Deterministic typed domain error equality',bodyEqualityClaim=True)


def causal_invariants(old,new):
    for key in ['specialization','maximumEnergy','maximumHealth','modelingNotes']:
        require(O.encode(old[key])==O.encode(new[key]),'Unrelated invariant changed: '+key)
    require(set(old)==set(new) and set(old['run'])==set(new['run']),'Schema changed')


def compare(args):
    before=pathlib.Path(args.baseline);after=pathlib.Path(args.corrected);out=pathlib.Path(args.out);out.mkdir(parents=True,exist_ok=False)
    if args.command=='preserve':
        old_rows={r['name']:r for r in read(before/'index.json')};new_rows={r['name']:r for r in read(after/'index.json')}
        require(len(old_rows)==args.expected_count,'Prior corpus inventory count differs')
    else:
        old_rows=verified_inventory(before,args.baseline_analysis);new_rows=verified_inventory(after,args.corrected_analysis)
    require(set(old_rows)==set(new_rows),'Inventories differ');proofs=[];failures=[]
    for name,row in old_rows.items():
        try:
            other=new_rows[name]
            for field in ['path','group','trace','requestSha256','status']:require(row.get(field)==other.get(field),'Receipt differs: '+field)
            old,new=checked_body(before,row),checked_body(after,other)
            request=None
            if row['requestSha256'] is not None:
                raw=(before/(name+'.request.json')).read_bytes();require(raw==(after/(name+'.request.json')).read_bytes(),'Raw input differs');request=O.decode(raw)
            proof=dict(name=name,status=row['status'])
            if args.command=='preserve':
                typed=request.get('rogue',request) if isinstance(request,dict) else {}
                ranks=(typed.get('talents') or {}).get('ranks') or {}
                require(not any(k.lower()=='dirty-deeds' and isinstance(v,(int,D)) and not isinstance(v,bool) and v>0 for k,v in ranks.items()),'Prior positive Dirty Deeds requires classification before blanket preservation')
                proof['allowances']=exact_response(old,new,row,args.old_source_commit,args.source_commit)
            elif row['group']=='binding':
                if row['status']==200:
                    if args.command=='replay-equality':proof['allowances']=exact_response(old,new,row,args.old_source_commit,args.source_commit)
                    else:
                        typed=bound_request(request);proof['before']=trace_accounting(typed,old,'baseline');proof['after']=trace_accounting(typed,new,'corrected');proof['firstCause']=first_cause(typed,old,new)
                    proof['binding']='Observed numeric-string admission, full-response replay or source-aware causal proof; retained raw bytes unchanged'
                else:proof['binding']=binding_comparison(old,new,row,other)
            elif args.command=='replay-equality' or row['group']!='causal':proof['allowances']=exact_response(old,new,row,args.old_source_commit,args.source_commit)
            else:
                oracle=trace_accounting if row['trace'] else general_accounting
                proof['before']=oracle(request,old,'baseline');proof['after']=oracle(request,new,'corrected')
                proof['firstCause']=first_cause(request,old,new)
            if 'firstCause' in proof:causal_invariants(old,new)
            write(out/(name+'.proof.json'),proof);proofs.append(dict(name=name,status=row['status'],comparison='binding characterization' if row['group']=='binding' and row['status']!=200 else ('causal independent resolver/trace' if row['trace'] or row['group']=='binding' else 'causal bounded arithmetic/structure; seeded replay required') if 'firstCause' in proof else 'complete response equality'))
        except Exception as error:failures.append(dict(name=name,error=str(error)))
    summary=dict(passed=not failures,requests=len(old_rows),compared=len(proofs),failures=failures,proofs=proofs,normalization=O.NORMALIZATION)
    write(out/'summary.json',summary);print(O.encode({k:v for k,v in summary.items() if k!='proofs'}));return 0 if not failures else 1


def main():
    parser=argparse.ArgumentParser(description=__doc__);commands=parser.add_subparsers(dest='command',required=True)
    cap=commands.add_parser('capture');cap.add_argument('--base',required=True);cap.add_argument('--out',required=True);cap.add_argument('--phase',choices=['baseline','corrected'],required=True)
    cap.add_argument('--mode',choices=['full','smoke'],default='full');cap.add_argument('--baseline');cap.add_argument('--source-commit',required=True)
    ana=commands.add_parser('analyze');ana.add_argument('--capture',required=True);ana.add_argument('--out',required=True);ana.add_argument('--phase',choices=['baseline','corrected'],required=True);ana.add_argument('--mode',choices=['full','smoke'],default='full');ana.add_argument('--source-commit',required=True)
    for name in ['compare','preserve','replay-equality']:
        p=commands.add_parser(name);p.add_argument('--baseline',required=True);p.add_argument('--corrected',required=True);p.add_argument('--out',required=True);p.add_argument('--old-source-commit',required=True);p.add_argument('--source-commit',required=True);p.add_argument('--baseline-analysis');p.add_argument('--corrected-analysis')
        if name=='preserve':p.add_argument('--expected-count',type=int,choices=[127,382,63],required=True)
    args=parser.parse_args()
    return capture(args) if args.command=='capture' else analyze(args) if args.command=='analyze' else compare(args)

if __name__=='__main__':raise SystemExit(main())
