#!/usr/bin/env python3
"""Immutable Initiative HTTP evidence and exact offline CP/RNG/finisher checks.

The caller owns API/build lifecycle. Capture and analysis are separate commands:
analysis failure never triggers recapture or overwrites original wire evidence.
Historical 33/67/100 probabilities do not certify server RNG semantics.
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
S = importlib.util.spec_from_file_location('dirty_deeds_contract', ROOT/'tools/solo-dps-rogue-dirty-deeds-http.py')
H = importlib.util.module_from_spec(S); S.loader.exec_module(H)
K, M, O = H.K, H.M, H.O
require, sha, read, write = M.require, M.sha, M.read, M.write
RUN, CHARACTER, ROTATION, SHARED = K.RUN, K.CHARACTER, K.ROTATION, K.SHARED
SPECS = K.SPECS
ACCEPTED = '3c18f284669881ea82f23dc9924282b6fe715154'
ID = '66666666-6666-4666-8666-666666666666'
PRODUCTION = ['WarcraftSim.Data/Forever/Rogue/ForeverRogueTalentBuild.cs', 'WarcraftSim.Api/Simulation/RogueCombatProcessor.cs']
HELPERS = ['tools/solo-dps-rogue-initiative-http.py', 'tools/test_solo_dps_rogue_initiative_http.py', 'tools/solo-dps-rogue-elusiveness-http.py', 'tools/test_solo_dps_rogue_elusiveness_http.py',
           'tools/solo-dps-rogue-dirty-deeds-http.py', 'tools/solo-dps-rogue-cutthroat-http.py',
           'tools/solo-dps-rogue-subtlety-http.py', 'tools/rogue-combat-historical-http.py']
exact_response, checked_body = H.exact_response, K.checked_body


S = importlib.util.spec_from_file_location('elusiveness_contract', ROOT/'tools/solo-dps-rogue-elusiveness-http.py')
E = importlib.util.module_from_spec(S); S.loader.exec_module(E)

def controlled(defaults, rank, spec='subtlety', opener='ambush'):
    r=E.controlled(defaults,None,spec)
    r['talents']=dict(assassinationPointsSpent=0,combatPointsSpent=0,subtletyPointsSpent=3,
                      ranks={} if rank is None else {'initiative':rank})
    r['durationSeconds']=20
    if opener=='garrote':r.update(bothWeaponsDaggers=False,mainHandWeaponType='sword',offHandWeaponType='sword')
    return r


def fixtures(defaults, mode='full', windows=None):
    require(mode in ['full','smoke'] and windows is None,'Only bounded full/smoke inventories supported')
    cases=[]
    def add(name,r=None,path=RUN,group='exact',baseline=200,corrected=None,trace=False,**kw):
        cases.append(dict(name=name,path=path,body=copy.deepcopy(r),group=group,
                          baselineStatus=baseline,correctedStatus=baseline if corrected is None else corrected,trace=trace,**kw))
    def admission(name,r,path=RUN,trace=False,**kw):
        ranks=r['talents']['ranks'];value=ranks.get('initiative')
        add(name,r if path!=ROTATION else K.envelope(r),path,group='admission' if value is not None else 'exact',
            baseline=200 if value is None else 400,corrected=200 if value is None or value in [0,1,2,3] else 400,trace=trace,**kw)
    if mode=='smoke':
        # CI-only bounded verification mode, not a simulator/product mode.
        for opener,spec,latency in [('ambush','subtlety',0),('garrote','assassination',80)]:
            for rank in [None,0,1,2,3]:
                r=controlled(defaults,rank,spec,opener);r['inputLatencyMs']=latency
                admission(f'{opener}-r{rank}',r,trace=True)
                if rank==3:admission(f'repeat-{opener}-r3',r,trace=True,replayOf=f'{opener}-r3')
        r=controlled(defaults,None,'assassination');r['useProvisionalLevel60Baseline']=True
        preset=read(ROOT/'WarcraftSim.Api/wwwroot/itemized-presets/rogue-assassination.json')
        r['talents']=dict(assassinationPointsSpent=sum(preset['ranks'].values()),combatPointsSpent=0,subtletyPointsSpent=0,ranks=dict(preset['ranks'],initiative=0))
        add('strict-assassination-zero',K.envelope(r),SHARED,baseline=400)
        add('strict-combat-zero',{'ranks':{'initiative':0}},'/api/forever/talents/rogue/combat/validate-itemized',baseline=400)
        return cases
    # Pairwise spec/opener/latency, not a cross-product of irrelevant axes.
    for opener,spec,latency in [('ambush','subtlety',0),('garrote','assassination',80)]:
        for rank in [None,0,1,2,3,-1,4]:
            r=controlled(defaults,rank,spec,opener);r['inputLatencyMs']=latency
            admission(f'{opener}-r{rank}',r,trace=True)
    for spec in SPECS:
        r=copy.deepcopy(defaults[spec]);r.update(durationSeconds=20,captureTimeline=True);r['character']['id']=ID
        add('default-run-'+spec,r)
    for rank in [None,0,3]:
        r=controlled(defaults,rank,'combat');r['inputLatencyMs']=80
        admission(f'combat-r{rank}',r,trace=True)
        r=controlled(defaults,rank,'assassination');K.prepared(r)
        r['level60Baseline']['sourceReference']='literal-initiative-fixture-not-client-data'
        admission(f'prepared-run-r{rank}',r)
        for path,label in [(CHARACTER,'character'),(ROTATION,'rotation')]:admission(f'prepared-{label}-r{rank}',r,path)
    # Later natural Vanish opening and actual finisher consequences.
    for rank in [None,0,3]:
        r=controlled(defaults,rank);r['durationSeconds']=320
        admission(f'later-opener-r{rank}',r,trace=True,later=True)
    for latency in [None,0,80]:
        r=controlled(defaults,3);r.pop('inputLatencyMs',None)
        if latency is not None:r['inputLatencyMs']=latency
        admission(f'timing-{latency}',r,trace=True)
        if latency is not None:
            admission(f'repeat-{latency}',r,trace=True,replayOf=f'timing-{latency}')
            admission(f'concurrent-{latency}',r,trace=True,concurrent=True,replayOf=f'timing-{latency}')
    for spec in ['assassination','combat']:
        add('strict-catalog-'+spec,path=f'/api/forever/talents/rogue/{spec}/itemized-catalog')
        add('strict-catalog-modeled-'+spec,path=f'/api/forever/talents/rogue/{spec}')
        p=read(ROOT/f'WarcraftSim.Api/wwwroot/itemized-presets/rogue-{spec}.json')
        r=copy.deepcopy(defaults[spec]);r.update(durationSeconds=20,useProvisionalLevel60Baseline=True)
        r['character'].update(id=ID,race='human')
        r['talents']=dict(assassinationPointsSpent=0,combatPointsSpent=0,subtletyPointsSpent=0,ranks=copy.deepcopy(p['ranks']))
        r['talents'][spec+'PointsSpent']=sum(p['ranks'].values())
        for rank in [0,3]:
            for owner in ['typed','full','both']:
                env=K.envelope(r)
                if owner in ['typed','both']:env['rogue']['talents']['ranks']['initiative']=rank
                if owner in ['full','both']:env['itemizedTalentRanks']['initiative']=rank
                add(f'strict-{spec}-r{rank}-{owner}',env,SHARED,baseline=400)
            probe=copy.deepcopy(r);probe['talents']['ranks']['initiative']=rank
            if spec=='combat':add(f'strict-preview-combat-r{rank}',probe,CHARACTER,baseline=400)
            for route in ['validate','validate-itemized']:
                add(f'strict-{spec}-{route}-r{rank}',probe['talents'],f'/api/forever/talents/rogue/{spec}/{route}',baseline=400)
    for spec,path,label in [('subtlety',CHARACTER,'character'),('subtlety',ROTATION,'rotation'),('combat',ROTATION,'rotation')]:
        r=controlled(defaults,0,spec);add(f'unavailable-{label}-{spec}',r if path==CHARACTER else K.envelope(r),path,baseline=400)
    for label,value in [('bool',True),('fraction',D('1.5')),('string','invalid'),('numeric-string','1'),('null',None),('null-map',None)]:
        r=controlled(defaults,1)
        if label=='null-map':r['talents']['ranks']=None
        else:r['talents']['ranks']['initiative']=value
        add('binding-'+label,r,group='binding',baseline=None,trace=True)
    require(len(cases)==len({c['name'] for c in cases}),'Duplicate names')
    return cases


def provenance():
    # Every tracked input, not only the one planned product file. New helpers
    # are explicitly included before they become tracked. No prose exclusions.
    paths=set(M.git('ls-files').splitlines()+HELPERS)
    paths.update(str(p.relative_to(ROOT)) for p in ROOT.glob('WarcraftSim.*/bin/Release/net10.0/WarcraftSim.*.dll'))
    return dict(head=M.git('rev-parse','HEAD'),tree=M.git('rev-parse','HEAD^{tree}'),
                files={p:sha((ROOT/p).read_bytes()) for p in sorted(paths)})


def capture(args):
    u=urllib.parse.urlsplit(args.base)
    require(u.scheme=='http' and u.hostname in ['localhost','127.0.0.1','::1'] and not u.username and not u.password and not u.query and not u.fragment and u.path in ['','/'],'Loopback API root required')
    out=pathlib.Path(args.out);out.mkdir(parents=True,exist_ok=False);started=time.monotonic_ns()
    before=provenance();write(out/'provenance-before.json',before)
    if args.phase=='baseline':
        require(before['head']==ACCEPTED,'Baseline HEAD differs')
        for p in PRODUCTION:require((ROOT/p).read_bytes()==subprocess.check_output(['git','show',ACCEPTED+':'+p],cwd=ROOT),'Baseline product changed')
    rows=[];lock=threading.Lock()
    def call(case,barrier=None):
        body=case.get('raw')
        if body is None and case.get('body') is not None:body=O.encode(case['body']).encode()
        name=case['name'];expected=case[args.phase+'Status']
        if body is not None:(out/(name+'.request.json')).write_bytes(body)
        if barrier is not None:barrier.wait(timeout=30)
        begin=time.monotonic_ns();url=args.base.rstrip('/')+case['path']
        req=urllib.request.Request(url,data=body,headers={'Content-Type':'application/json'})
        try:
            with urllib.request.build_opener(M.NoRedirect).open(req,timeout=90) as response:raw,status,headers=response.read(),response.status,dict(response.headers)
        except urllib.error.HTTPError as error:raw,status,headers=error.read(),error.code,dict(error.headers)
        end=time.monotonic_ns();(out/(name+'.response.json')).write_bytes(raw)
        row={k:v for k,v in case.items() if k not in ['body','raw','status','requestSha256','responseSha256','headers','startedMonotonicNs','completedMonotonicNs','responseBytes','expectedStatus','url','method']}
        row.update(url=url,method='GET' if body is None else 'POST',status=status,expectedStatus=expected,requestSha256=sha(body) if body is not None else None,responseSha256=sha(raw),responseBytes=len(raw),headers=headers,startedMonotonicNs=begin,completedMonotonicNs=end)
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
            for name in ['windows.json','fixture-inventory.json']:
                p=prior/name
                if p.exists():(out/name).write_bytes(p.read_bytes())
        else:
            defaults={s:call(dict(name='defaults-'+s,path='/api/sim/rogue/defaults?spec='+s,group='default',baselineStatus=200,correctedStatus=200,trace=False)) for s in SPECS}
            call(dict(name='report-metadata',path=M.METADATA,group='metadata',baselineStatus=200,correctedStatus=200,trace=False))
            windows=read(args.windows) if args.windows else None
            if windows is not None:(out/'windows.json').write_bytes(pathlib.Path(args.windows).read_bytes())
            cases=fixtures(defaults,args.mode,windows)
            write(out/'fixture-inventory.json',[{k:v for k,v in c.items() if k not in ['body','raw']} for c in cases])
        for case in cases:
            if not case.get('concurrent'):call(case)
        concurrent_cases=[c for c in cases if c.get('concurrent')]
        if concurrent_cases:
            barrier=threading.Barrier(len(concurrent_cases))
            with concurrent.futures.ThreadPoolExecutor(max_workers=len(concurrent_cases)) as pool:list(pool.map(lambda c:call(c,barrier),concurrent_cases))
        require(read(out/'report-metadata.response.json')['sourceCommit']==args.source_commit,'Actual source stamp differs')
        after=provenance();write(out/'provenance-after.json',after);require(O.encode(before)==O.encode(after),'Inputs changed during capture')
        summary=dict(passed=True,captureComplete=True,analysisPerformed=False,phase=args.phase,mode=args.mode,requests=len(rows),elapsedNanoseconds=time.monotonic_ns()-started,sourceCommit=args.source_commit)
        write(out/'capture-summary.json',summary);print(O.encode(summary));return 0
    except BaseException as error:write(out/'failure.json',dict(passed=False,error=repr(error),requests=len(rows)));raise


def bound_request(request):
    require(request['talents']['ranks']=={'initiative':'1'},'Only frozen numeric-string 1 permits typed projection')
    result=copy.deepcopy(request);result['talents']['ranks']['initiative']=1;return result


def verified_inventory(root):
    summary=read(root/'capture-summary.json');require(summary['passed'] and summary['captureComplete'],'Incomplete capture')
    rows=read(root/'index.json');require(len(rows)==summary['requests']==len({r['name'] for r in rows}),'Capture inventory differs')
    defaults={s:read(root/f'defaults-{s}.response.json') for s in SPECS}
    windows=read(root/'windows.json') if (root/'windows.json').exists() else None
    cases=fixtures(defaults,summary['mode'],windows)
    expected={c['name']:c for c in cases}
    for s in SPECS:expected['defaults-'+s]=dict(path='/api/sim/rogue/defaults?spec='+s,group='default',baselineStatus=200,correctedStatus=200,trace=False)
    expected['report-metadata']=dict(path=M.METADATA,group='metadata',baselineStatus=200,correctedStatus=200,trace=False)
    require(set(expected)=={r['name'] for r in rows},'Complete frozen inventory differs')
    for row in rows:
        checked_body(root,row);case=expected[row['name']]
        for key in ['path','group','baselineStatus','correctedStatus','trace','later','concurrent','replayOf']:
            require(O.encode(row.get(key))==O.encode(case.get(key)),'Frozen fixture differs: '+key)
        if case.get('body') is not None:require((root/(row['name']+'.request.json')).read_bytes()==O.encode(case['body']).encode(),'Frozen request differs: '+row['name'])
    before,after=read(root/'provenance-before.json'),read(root/'provenance-after.json')
    require(O.encode(before)==O.encode(after),'Captured source/binary changed')
    require(read(root/'report-metadata.response.json')['sourceCommit']==summary['sourceCommit'],'Captured source stamp differs')
    return {r['name']:r for r in rows}


def initiative_roll(rng,rank,event,cp,proof):
    if rank==0:return cp
    require(type(rank) is int and rank in [1,2,3],'Unexpected Initiative rank')
    threshold=[33,67,100][rank-1];value=rng.percent()
    require(abs(value-D(threshold))>D('0.00000000001'),'Independent Decimal conversion too near boundary')
    after=min(5,cp+int(value<threshold))
    proof.append(dict(event=event,draw=rng.count,kind='initiative',percent=value,threshold=threshold,before=cp,after=after))
    return after


def trace_accounting(request,response,phase):
    """Narrow public fixed-priority oracle, independent literal CP/damage math.

    No Seal Fate, poison, mitigation or synthetic events. Public timeline does
    not expose processor state or draws; reconstruct the known single stream
    for these bounded fixtures, then check each observable result and finisher.
    Real executor correlation/zero-damage/duplicate proof belongs to .NET tests.
    """
    ranks=request['talents']['ranks'];require(set(ranks)<= {'initiative'},'Unsupported trace talents')
    rank=ranks.get('initiative',0);require(type(rank) is int and 0<=rank<=3,'Unsupported trace rank')
    require(request['additionalTargets']==0 and not request['useInstantPoison'] and not request['useUtilityAbilities'] and not request['enableBossAttacks'],'Unsupported trace mechanism')
    require(request['targetArmor']==0 and request['tuning']['physicalCritPercent']==0 and request['level60Baseline'] is None,'Unsupported trace stats')
    require(request['tuning']['mainHandHitPercent']==request['tuning']['offHandHitPercent']==100 and request['targetDodgePercent']==request['targetParryPercent']==0 and request['isBehindTarget'],'Unsupported hit/avoidance')
    require(request['character']['baseStats']['values']=={'attack-power':0},'Narrow AP differs')
    tuning=request['tuning'];require(tuning['mainHandSpeedSeconds']==tuning['offHandSpeedSeconds']==2 and tuning['mainHandWeaponDamage']==100 and tuning['offHandWeaponDamage']==40 and tuning['sliceAndDiceHastePercent']==0,'Unsupported weapon/haste')
    H.verify_event_envelopes(request,response)
    garrote=H.verify_garrote_lifecycle(request,response)
    completion=E.verify_completion_accounting(request,response);stealth_proof=E.verify_stealth_lifecycle(request,response)
    events=response['run']['timeline'];rng=K.SeededRandom(request['seed'])
    cp=0;rupture_cp=0;finishers=0;openers=0;stealth=request['startInStealth'];in_combat=False
    damage=collections.defaultdict(lambda:D(0));threat=collections.defaultdict(collections.deque)
    total=D(0);energy=D(request['startingEnergyPercent']);energy_time=D(0)
    started={};completed={};generators=set();pending={};resources=[];draws=[];damage_proofs=[];priority=[];auras={};auto_clocks={};vanishes=[];opener_links=[];last_vanish=None;bleed=None;finisher_proofs=[]
    ready=D(0);vanish_ready=D(0);slice_until=D(0);rupture_until=D(0);latency=D(request.get('inputLatencyMs',80))/1000
    costs={'ambush':60,'backstab':60,'garrote':50,'vanish':0,'sinister-strike':45,'eviscerate':35,'rupture':25,'slice-and-dice':25}
    for i,e in enumerate(events):
        kind,key,t=e['type'],e['abilityKey'],D(e['timeSeconds'])
        require(0<=t<=request['durationSeconds'] and (i==0 or t>=events[i-1]['timeSeconds']),'Trace order differs')
        if kind==3:
            available=min(D(100),energy+(t-energy_time)*tuning['energyPerSecond']);candidates=[]
            if stealth and request['bothWeaponsDaggers']:candidates.append('ambush')
            if stealth:candidates.append('garrote')
            if not stealth and in_combat and t>=vanish_ready:candidates.append('vanish')
            if cp>=2 and t>=slice_until:candidates.append('slice-and-dice')
            if cp>=4 and t>=rupture_until:candidates.append('rupture')
            if cp>=4:candidates.append('eviscerate')
            if request['specialization']=='subtlety' and request['bothWeaponsDaggers']:candidates.append('backstab')
            candidates.append('sinister-strike');affordable=[k for k in candidates if costs[k]<=available]
            require(affordable and key==affordable[0],f'Priority/CP differs at {i}: {key}, {affordable}, {cp}')
            require(t>=ready,'Action precedes input/GCD readiness')
            execution=e['abilityExecutionId'];require(execution not in started,'Duplicate start');started[execution]=(key,t)
            priority.append(dict(event=i,time=t,key=key,energy=available,comboPoints=cp))
            ready=t+(0 if key=='vanish' else 1)+latency
            if key=='vanish':
                vanish_ready=t+300;last_vanish=dict(event=i,time=t,execution=execution,ready=vanish_ready);vanishes.append(last_vanish)
            if key in ['ambush','garrote'] and last_vanish is not None and stealth:opener_links.append(dict(vanish=last_vanish,openerExecution=execution,openerTime=t,openerKey=key))
        if kind==4:
            execution=e['abilityExecutionId'];require(started.get(execution)==(key,t) and execution not in completed,'Invalid completion');completed[execution]=(key,t)
            if key=='vanish':stealth=True
            if key in ['ambush','garrote']:openers+=1;stealth=False;in_combat=True
            if key in ['rupture','slice-and-dice']:pending[key]=execution
        if kind==8:
            require(key in costs and e['resourceKey']=='energy','Unexpected resource')
            energy=min(D(100),energy+(t-energy_time)*tuning['energyPerSecond']);energy_time=t
            require(e['amount']==-costs[key] and energy>=costs[key],'Literal spend differs');before=energy;energy-=costs[key]
            require(energy==e['resourceTotalAfter'],'Energy/cap differs');resources.append(dict(event=i,key=key,before=before,cost=costs[key],after=energy))
        if kind==9:
            if key in ['rupture','garrote']:
                result,critical,new=K.roll(rng,request,e,application=True);require(result=='hit','Seeded application differs');draws.extend(dict(event=i,**d) for d in new)
            if key=='garrote':cp=initiative_roll(rng,rank,i,min(5,cp+1),draws)
            if key in ['rupture','slice-and-dice']:
                require(key in pending and cp>0,'Finisher lacks completed cast/CP');execution=pending.pop(key)
                expiry=t+6+(3 if key=='slice-and-dice' else 2)*cp;auras[e['auraInstanceId']]=(key,expiry)
                if key=='slice-and-dice':slice_until=expiry
                else:
                    require(bleed is None or bleed['next']>=t,'Rupture skipped tick')
                    rupture_until=expiry;rupture_cp=cp;bleed=dict(next=t+2,expires=expiry,execution=execution)
                finisher_proofs.append(dict(event=i,key=key,spent=cp,duration=expiry-t,expiry=expiry,after=0));finishers+=1;cp=0
            if key in ['opening-stealth','vanish']:stealth=True
        if kind==10:
            if key in ['opening-stealth','vanish']:stealth=False
            if key in ['rupture','slice-and-dice']:
                require(auras.pop(e['auraInstanceId'],None)==(key,t),'Finisher expiry differs')
                if key=='rupture':require(bleed is not None and bleed['next']>t,'Missing expiry-boundary tick');bleed=None
        if kind==32:require(key not in auto_clocks,'Duplicate auto start');auto_clocks[key]=t+2
        if kind==34:require(key in auto_clocks and auto_clocks[key]>=t,'Missing auto swing');auto_clocks.pop(key)
        if kind==6:
            if key.startswith('rogue-'):require(auto_clocks.get(key)==t,'Auto time differs');auto_clocks[key]=t+2
            if key=='rupture' and e['isPeriodic']:
                require(bleed is not None and t==bleed['next'] and t<=bleed['expires'] and e['abilityExecutionId']==bleed['execution'],'Rupture tick differs');bleed['next']+=2
            application=key in ['rupture','garrote'] and not e['isPeriodic']
            result,critical,new=K.roll(rng,request,e,application);draws.extend(dict(event=i,**d) for d in new)
            require(e['resultKey']==result and e['isCritical']==critical,f'Seeded result differs at {i} {key}')
            base={'ambush':D(250)+tuning['ambushBonusDamage'],'backstab':D(150)+tuning['backstabBonusDamage'],'sinister-strike':D(100)+tuning['sinisterStrikeBonusDamage'],'rogue-main-hand':D(100),'rogue-off-hand':D(20),'eviscerate':tuning['eviscerateDamagePerPoint']*max(1,cp),'rupture':tuning['ruptureTickDamagePerPoint']*max(1,rupture_cp),'garrote':tuning['garroteTickDamage']}
            require(key in base,'Unsupported damage');raw=D(base[key])*(2 if critical else 1) if result in ['hit','critical'] and not application else D(0)
            require(e['rawAmount']==raw and e['amount']==raw and e['mitigatedAmount']==0,f'Literal damage differs at {i} {key}')
            damage[key]=M.dotnet_add(damage[key],raw);total=M.dotnet_add(total,raw);damage_proofs.append(dict(event=i,key=key,amount=raw,comboPoints=cp,rupturePoints=rupture_cp))
            if raw>0:threat[(t,key,e['effectKey'])].append(raw)
            if raw>0 and not e['isPeriodic']:
                stealth=False;in_combat=True
                if key in ['sinister-strike','backstab','ambush'] and e['abilityExecutionId'] not in generators:cp=min(5,cp+1);generators.add(e['abilityExecutionId'])
            if key=='ambush' and not e['isPeriodic'] and result in ['hit','critical']:cp=initiative_roll(rng,rank,i,cp,draws)
            if key=='eviscerate' and result not in ['miss','dodge','parry'] and cp>0:
                finisher_proofs.append(dict(event=i,key=key,spent=cp,damage=raw,after=0));finishers+=1;cp=0
        if kind==20:
            q=threat[(t,key,e['effectKey'])];require(q and e['amount']==q.popleft(),'Damage/threat differs')
    require(all(not q for q in threat.values()),'Missing threat')
    require(all(t>=request['durationSeconds'] for t in auto_clocks.values()),'Missing final auto swing')
    require(bleed is None or bleed['next']>=request['durationSeconds'],'Missing final Rupture tick')
    require(total<request['targetMaximumHealth'],'No-kill target died')
    energy=min(D(100),energy+(D(request['durationSeconds'])-energy_time)*tuning['energyPerSecond'])
    require(response['endingEnergy']==energy and response['endingComboPoints']==cp,'Ending Energy/CP differs')
    require(response['finishersUsed']==finishers and response['stealthOpeners']==openers,'Finisher/opener count differs')
    require(response['damageDone']==total and response['dps']==M.dotnet_divide(total,request['durationSeconds']),'Damage/DPS differs')
    require(O.encode(response['damageByAbility'])==O.encode(dict(damage)),'Damage breakdown differs')
    K.verify_ability_summaries(response);H.verify_static_summary(request,response,damage,total,energy)
    metrics={k:0 for k in ['rogue-expose-armor-points','rogue-poison-procs','rogue-deadly-poison-ticks','rogue-main-hand-poison-charges','rogue-off-hand-poison-charges','rogue-preparation-cooldowns-reset','rogue-hack-and-slash-attacks','rogue-quick-recovery-energy','rogue-feint-threat-reduced','rogue-boss-attacks-dodged','rogue-boss-attacks-parried']}
    metrics.update({'rogue-stealth-openers':openers,'rogue-finisher-count':finishers,'rogue-ending-combo-points':cp,'rogue-boss-threat':total})
    require(O.encode(response['run']['customMetrics'])==O.encode(metrics),'Complete custom metrics differ')
    require(response['maximumEnergy']==100 and response['poisonProcs']==0 and response['endingHealth']==response['maximumHealth'],'Health/caps differ')
    return dict(events=len(events),vanishes=vanishes,openerLinks=opener_links,resources=resources,rngDraws=draws,damage=damage_proofs,priority=priority,finishers=finisher_proofs,garroteLifecycle=garrote,completionSpends=completion,stealthLifecycle=stealth_proof,starvation=E.starvation_accounting(request,response),processedEventCount=response['run']['processedEventCount'],limit='Public timeline reconstruction for no-other-proc fixtures only. Internal scheduler count retained and checked by complete replay, not independently reconstructed. Application/execution ownership requires separate real-executor proof.')
def finisher_signature(trace):
    # Event indices and absolute scheduling are not a resource/duration cause.
    return [{k:p[k] for k in ['key','spent','duration','damage','after'] if k in p} for p in trace['finishers']]


def verify_smoke_rejection(name,status,value):
    expected={
        'strict-assassination-zero':{'error':'Strict historical talent maps require exact canonical reconciled keys; case aliases, whitespace and unknown keys are not accepted.'},
        'strict-combat-zero':{'valid':False,'error':'Strict Rogue ranks require unique exact canonical reconciled keys.'}}
    require(name in expected,'Unknown smoke rejection fixture')
    require(status==400 and O.encode(value)==O.encode(expected[name]),'Smoke negative lacks exact canonical-key rejection')


def analyze(args):
    root=pathlib.Path(args.capture);out=pathlib.Path(args.out);out.mkdir(parents=True,exist_ok=False)
    rows=verified_inventory(root);summary=read(root/'capture-summary.json');proofs=[];failures=[];traces={}
    for name,row in rows.items():
        try:
            value=checked_body(root,row);expected=row[summary['phase']+'Status']
            require(expected is None or row['status']==expected,'Unexpected status')
            proof=dict(name=name,status=row['status'])
            if summary['mode']=='smoke' and name in ['strict-assassination-zero','strict-combat-zero']:
                verify_smoke_rejection(name,row['status'],value);proof['canonicalKeyRejection']=True
            if row['status']==200 and row['path']==RUN and row['trace']:
                request=read(root/(name+'.request.json'))
                if name=='binding-numeric-string':request=bound_request(request)
                if name=='binding-null-map':
                    require(request['talents']['ranks'] is None,'Wrong null-map binder');request=copy.deepcopy(request);request['talents']['ranks']={}
                traces[name]=trace_accounting(request,value,summary['phase']);write(out/(name+'.trace.json'),traces[name])
                if row.get('later'):require(len(traces[name]['vanishes'])>=2 and len(traces[name]['openerLinks'])>=2,'Missing later natural opener')
            if row['group']=='binding':proof.update(diagnosticOnly=row['status']!=200,body=value if row['status']!=200 else None)
            if row.get('replayOf'):exact_response(checked_body(root,rows[row['replayOf']]),value,row,summary['sourceCommit'],summary['sourceCommit'])
            proofs.append(proof)
        except Exception as error:failures.append(dict(name=name,error=str(error)))
    concurrency=[r for r in rows.values() if r.get('concurrent')];overlap=None
    if concurrency:
        overlap=min(r['completedMonotonicNs'] for r in concurrency)-max(r['startedMonotonicNs'] for r in concurrency)
        if overlap<=0:failures.append(dict(name='concurrency',error='No genuine request overlap'))
    try:
        if summary['mode']=='full':exact_response(read(root/'timing-None.response.json'),read(root/'timing-80.response.json'),dict(path=RUN),'same','same')
        if summary['phase']=='corrected':
            for label in (['ambush','garrote','combat','later-opener'] if summary['mode']=='full' else ['ambush','garrote']):
                # Run response has no echoed talent request: no bookkeeping mask.
                exact_response(read(root/f'{label}-rNone.response.json'),read(root/f'{label}-r0.response.json'),dict(path=RUN),'same','same')
            changed=[]
            for label in (['ambush','garrote','later-opener'] if summary['mode']=='full' else ['ambush','garrote']):
                a,b=traces[label+'-rNone'],traces[label+'-r3']
                require(any(x['kind']=='initiative' and x['after']>x['before'] for x in b['rngDraws']),'Positive rank lacks bonus CP')
                if finisher_signature(a)!=finisher_signature(b):changed.append(dict(case=label,absent=a['finishers'],rank3=b['finishers']))
            require(changed,'No actual public finisher consequence');write(out/'finisher-consequences.json',changed)
    except Exception as error:failures.append(dict(name='paired-controls',error=str(error)))
    result=dict(passed=not failures,capturePath=str(root.resolve()),rawIndexSha256=sha((root/'index.json').read_bytes()),requests=len(rows),proofs=proofs,failures=failures,concurrentOverlapNanoseconds=overlap,normalization=O.NORMALIZATION)
    write(out/'summary.json',result);print(O.encode({k:v for k,v in result.items() if k!='proofs'}));return 0 if result['passed'] else 1


def binding_status_transition(name):
    # Numeric strings bind to int; null rank maps fail required-field validation.
    return {'binding-numeric-string':(400,200),'binding-null-map':(400,400)}.get(name,(400,400))


def compare(args):
    before,after=pathlib.Path(args.baseline),pathlib.Path(args.corrected);out=pathlib.Path(args.out);out.mkdir(parents=True,exist_ok=False)
    rows,others=verified_inventory(before),verified_inventory(after);require(set(rows)==set(others),'Inventories differ')
    replay=args.command=='replay-equality';proofs=[];failures=[]
    for name,row in rows.items():
        try:
            other=others[name]
            for key in ['path','group','trace','requestSha256']:require(O.encode(row[key])==O.encode(other[key]),'Receipt differs: '+key)
            old,new=checked_body(before,row),checked_body(after,other);proof=dict(name=name)
            if row['requestSha256'] is not None:require((before/(name+'.request.json')).read_bytes()==(after/(name+'.request.json')).read_bytes(),'Raw request changed')
            if row['group']=='binding' and (row['status']!=200 or other['status']!=200):
                proof.update(comparison='explicit binder outcomes, not body equality',beforeStatus=row['status'],afterStatus=other['status'],before=old,after=new)
                if replay and row['status']==other['status'] and row['status']!=200:proof['diagnostic']=H.binding_comparison(old,new,row,other)
                elif not replay:
                    expected=binding_status_transition(name)
                    require((row['status'],other['status'])==expected,'Unexpected binder status transition')
            elif replay or row['group']=='exact':
                require(row['status']==other['status'],'Unexpected status change')
                exact_response(old,new,row,args.old_source_commit,args.source_commit);proof['comparison']='complete response equality'
            else:
                require(row['status']==row['baselineStatus'] and other['status']==row['correctedStatus'],'Wrong admission statuses')
                # Store full diagnostics; do not label formerly rejected requests numerical failures.
                proof.update(comparison='intentional new legacy admission' if other['status']==200 else 'retained invalid rank rejection',before=old,afterStatus=other['status'])
                if other['status']!=200:proof['after']=new
            write(out/(name+'.proof.json'),proof);proofs.append(dict(name=name,comparison=proof['comparison']))
        except Exception as error:failures.append(dict(name=name,error=str(error)))
    result=dict(passed=not failures,requests=len(rows),compared=len(proofs),proofs=proofs,failures=failures,normalization=O.NORMALIZATION)
    write(out/'summary.json',result);print(O.encode({k:v for k,v in result.items() if k!='proofs'}));return 0 if result['passed'] else 1


def report_contract(args):
    """Actual immutable UI export readers, separate from exact HTTP arithmetic."""
    originals=[pathlib.Path(x).resolve() for x in args.report]
    inputs=[ROOT/'WarcraftSim.Api/wwwroot/solo-dps-comparison.js',ROOT/'WarcraftSim.Api/wwwroot/solo-dps.js',ROOT/'tools/solo-dps-combat-contract.test.cjs',ROOT/'WarcraftSim.Api/wwwroot/solo-dps-assumptions.js']+originals
    before={str(p):sha(p.read_bytes()) for p in inputs}
    script=r'''
const fs=require('node:fs'),assert=require('node:assert/strict'),api=require('./WarcraftSim.Api/wwwroot/solo-dps-comparison.js');
const clone=x=>JSON.parse(JSON.stringify(x)),proof={originals:[],negative:[]};
for(const file of process.argv.slice(1)) {
 const raw=fs.readFileSync(file,'utf8'),original=JSON.parse(raw),spec=original.build.specialization;
 assert.ok(['assassination','combat'].includes(spec));api.inspectReport(original);assert.equal(api.compareReports(original,original).comparable,true);
 assert.equal(JSON.stringify(original),JSON.stringify(JSON.parse(raw)));proof.originals.push({file,spec});
 const names=['build','full','typed'];if(spec==='combat')names.push('preparation','raw-preparation');
 for(const rank of [0,1,2,3])for(const owner of [...names,'all']) {
  const r=clone(original),maps={build:r.build.talents.ranks,full:r.originalSimulationRequest.itemizedTalentRanks,typed:r.originalSimulationRequest.rogue.talents.ranks};
  if(spec==='combat'){maps.preparation=r.combatPreparation.request.talents.ranks;maps['raw-preparation']=r.rawServerResponse.preparation.request.talents.ranks;}
  for(const k of owner==='all'?names:[owner])maps[k].initiative=rank;
  const bytes=JSON.stringify(r);let error;try{api.inspectReport(r);}catch(e){error=e.message;}
  assert.ok(error);assert.match(error,spec==='assassination'?/unsupported talent key/i:/unsupported exact rank|ranks|owned request|preparation copy disagrees/i);assert.equal(JSON.stringify(r),bytes);
  proof.negative.push({file,spec,rank,owner,error});
 }
 assert.equal(fs.readFileSync(file,'utf8'),raw);
}
assert.ok(proof.originals.some(x=>x.spec==='combat')&&proof.originals.some(x=>x.spec==='assassination'));
process.stdout.write(JSON.stringify(proof));
'''
    proof=O.decode(subprocess.check_output(['node','-e',script,*map(str,originals)],cwd=ROOT))
    require(before=={str(p):sha(p.read_bytes()) for p in inputs},'Original/module changed during report checks')
    out=pathlib.Path(args.out);require(not out.exists(),'Refuse overwrite report evidence')
    result=dict(passed=True,scope='Current parsed report admission on actual saved UI exports; derived rejection inputs only. Not HTTP Decimal equality or fresh simulation.',inputs=before,proof=proof)
    write(out,result);print(O.encode(dict(passed=True,originals=len(proof['originals']),negative=len(proof['negative']))));return 0


def main():
    p=argparse.ArgumentParser(description=__doc__);commands=p.add_subparsers(dest='command',required=True)
    cap=commands.add_parser('capture');cap.add_argument('--base',required=True);cap.add_argument('--out',required=True);cap.add_argument('--phase',choices=['baseline','corrected'],required=True);cap.add_argument('--mode',choices=['full','smoke'],default='full');cap.add_argument('--baseline');cap.add_argument('--source-commit',required=True);cap.set_defaults(windows=None)
    report=commands.add_parser('report-contract');report.add_argument('--report',action='append',required=True);report.add_argument('--out',required=True)
    ana=commands.add_parser('analyze');ana.add_argument('--capture',required=True);ana.add_argument('--out',required=True)
    for name in ['compare','replay-equality']:
        c=commands.add_parser(name);c.add_argument('--baseline',required=True);c.add_argument('--corrected',required=True);c.add_argument('--out',required=True);c.add_argument('--old-source-commit',required=True);c.add_argument('--source-commit',required=True)
    args=p.parse_args();return report_contract(args) if args.command=='report-contract' else capture(args) if args.command=='capture' else analyze(args) if args.command=='analyze' else compare(args)

if __name__=='__main__':raise SystemExit(main())
