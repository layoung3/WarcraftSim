#!/usr/bin/env python3
"""Immutable Elusiveness HTTP evidence and exact offline causal/replay checks.

The caller owns API/build lifecycle. Capture and analysis are separate commands:
analysis failure never triggers recapture or overwrites original wire evidence.
Historical 45-second rank reductions do not certify provisional base cooldowns.
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
ACCEPTED = '6fae3e8a00f0d9f5f33ffc78a024555d91e460d5'
ID = '66666666-6666-4666-8666-666666666666'
PRODUCTION = ['WarcraftSim.Data/Forever/Rogue/ForeverRogueSpellbook.cs']
HELPERS = ['tools/solo-dps-rogue-elusiveness-http.py', 'tools/test_solo_dps_rogue_elusiveness_http.py',
           'tools/solo-dps-rogue-dirty-deeds-http.py', 'tools/solo-dps-rogue-cutthroat-http.py',
           'tools/solo-dps-rogue-subtlety-http.py', 'tools/rogue-combat-historical-http.py']
exact_response, checked_body = H.exact_response, K.checked_body


def cooldown(key, rank, phase):
    require(type(rank) is int and rank in [0, 1, 2], 'Unsupported literal rank')
    require(key in ['vanish', 'blind'] and phase in ['baseline', 'corrected'], 'Unsupported cooldown oracle')
    return {'baseline': {'vanish': [300, 270, 240], 'blind': [180, 165, 150]},
            'corrected': {'vanish': [300, 255, 210], 'blind': [180, 135, 90]}}[phase][key][rank]


def controlled(defaults, rank, spec='subtlety'):
    r = K.controlled(defaults, None, spec)
    r['character']['id'] = ID
    r['talents'] = dict(assassinationPointsSpent=0, combatPointsSpent=0, subtletyPointsSpent=2,
                        ranks={} if rank is None else {'elusiveness': rank})
    r.update(startInStealth=True, startingEnergyPercent=100, durationSeconds=20)
    r['tuning'].update(ambushBonusDamage=150, sliceAndDiceHastePercent=0)
    return r


def primary(defaults, spec, rank):
    r = controlled(defaults, rank, spec)
    # Pairwise real latency and initial Stealth coverage, not a full Cartesian matrix.
    r['startInStealth'] = rank == 1
    if spec == 'combat': r['inputLatencyMs'] = 80
    if spec == 'assassination' and rank == 2: r.pop('inputLatencyMs', None)
    return r


def fixtures(defaults, mode='full', windows=None):
    require(mode in ['probe', 'full', 'smoke'], 'Unknown fixture mode')
    cases = []
    def add(name, r=None, path=RUN, group='exact', status=200, trace=False, **kw):
        cases.append(dict(name=name, path=path, body=copy.deepcopy(r), group=group,
                          baselineStatus=status, correctedStatus=status, trace=trace, **kw))
    for spec in SPECS:
        for rank in [1, 2]:
            r = primary(defaults, spec, rank)
            if mode == 'probe':
                r['durationSeconds'] = 10
                add(f'probe-{spec}-r{rank}', r, trace=True)
            elif mode == 'full':
                name = f'window-{spec}-r{rank}'
                require(windows is not None and name in windows['cases'], 'Missing trace-first frozen end time: '+name)
                r['durationSeconds'] = windows['cases'][name]['end']
                add(name, r, group='causal', trace=True, requireLaterOpener=True)
                r['durationSeconds'] = 810 if rank == 1 else 660
                add(f'repeated-{spec}-r{rank}', r, group='causal', trace=True, requireRepeated=True)
    if mode == 'probe': return cases
    if mode == 'smoke':
        for spec in SPECS:
            for rank in [1,2]:
                r=primary(defaults,spec,rank);r['durationSeconds']=280 if rank==1 else 240
                name=f'smoke-{spec}-r{rank}';add(name,r,group='causal',trace=True,requireLaterOpener=True)
                if (spec,rank) in [('combat',1),('subtlety',2)]:add('repeat-'+name,r,group='causal',trace=True,replayOf=name)
        for rank in [None,0]:
            r=controlled(defaults,rank);r['durationSeconds']=20
            add(f'zero-subtlety-r{rank}',r,trace=True,**({'replayOf':'zero-subtlety-rNone'} if rank==0 else {}))
        r=controlled(defaults,2);r.update(useUtilityAbilities=True,durationSeconds=110)
        add('smoke-blind-r2',r,group='causal',trace=False,requireBlind=True)
        for rank in [-1,3]:add(f'invalid-r{rank}',controlled(defaults,rank),status=400)
        for spec in ['assassination','combat']:
            add('strict-zero-'+spec,{'ranks':{'elusiveness':0}},f'/api/forever/talents/rogue/{spec}/validate-itemized',status=400)
        return cases
    for spec in SPECS:
        for rank in [None, 0, 1, 2, -1, 3]:
            r = controlled(defaults, rank, spec)
            add(f'admission-{spec}-r{rank}', r, status=400 if rank in [-1, 3] else 200,
                trace=rank in [None, 0, 1, 2])
        r = controlled(defaults, 0, spec); r['durationSeconds'] = 660
        add(f'zero-long-{spec}', r, trace=True)
        r = copy.deepcopy(defaults[spec]); r.update(durationSeconds=660, captureTimeline=True)
        r['character']['id'] = ID
        add(f'default-long-{spec}', r)
    # Non-dagger, low Energy, prepared baseline, armor/crit, saved tuning controls.
    for label, spec, rank, mutate in [
        ('garrote', 'assassination', 1, lambda r: r.update(bothWeaponsDaggers=False,mainHandWeaponType='sword',offHandWeaponType='sword')),
        ('dirty-deeds', 'subtlety', 2, lambda r: (r.update(bothWeaponsDaggers=False,mainHandWeaponType='sword',offHandWeaponType='sword'),r['talents'].update(subtletyPointsSpent=4,ranks={'elusiveness':2,'dirty-deeds':2}))),
        ('low-energy', 'combat', 2, lambda r: (r.update(startingEnergyPercent=10,startInStealth=False),r['tuning'].update(energyPerSecond=10))),
        ('prepared', 'assassination', 1, K.prepared),
        ('armor-crit', 'subtlety', 2, lambda r: (r.update(targetArmor=5500),r['tuning'].update(physicalCritPercent=100))),
        ('saved-tuning', 'combat', 1, lambda r: r['tuning'].update(garroteTickDamage=D('73.25'),hemorrhageBonusDamage=0,mutilateBonusDamagePerHand=D('17.25'))),
    ]:
        r=controlled(defaults,rank,spec);r['durationSeconds']=660;mutate(r)
        if label=='prepared':r['level60Baseline']['sourceReference']='literal-elusiveness-fixture-not-client-data'
        add('variant-'+label,r,group='causal',trace=True)
    # These interactions use bounded arithmetic/structure plus complete replay.
    for label, rank, mutate in [
        ('blind-r1',1,lambda r:r.update(useUtilityAbilities=True)),
        ('blind-dirty-tricks-r2',2,lambda r:(r.update(useUtilityAbilities=True),r['talents'].update(subtletyPointsSpent=4,ranks={'elusiveness':2,'dirty-tricks':2}))),
        ('preparation',2,lambda r:(r.update(useUtilityAbilities=True),r['talents'].update(subtletyPointsSpent=21,ranks={'elusiveness':2,'preparation':1}))),
        ('cutthroat',2,lambda r:r['talents'].update(subtletyPointsSpent=7,ranks={'elusiveness':2,'cutthroat':5})),
        ('poison-incoming',1,lambda r:r.update(useInstantPoison=True,mainHandPoison='instant',offHandPoison='deadly',poisonChargesPerWeapon=10,enableBossAttacks=True,bossSwingSeconds=2,bossHitDamage=100)),
    ]:
        r=controlled(defaults,rank);r['durationSeconds']=660;mutate(r)
        add('interaction-'+label,r,group='causal',trace=False)
    for rank in [None,0,1,2,-1,3]:
        for path,label in [(CHARACTER,'character'),(ROTATION,'rotation')]:
            r=controlled(defaults,rank,'assassination')
            add(f'preview-{label}-r{rank}',r if path==CHARACTER else K.envelope(r),path,status=400 if rank in [-1,3] else 200)
    for spec in ['assassination','combat']:
        p=read(ROOT/f'WarcraftSim.Api/wwwroot/itemized-presets/rogue-{spec}.json')
        r=copy.deepcopy(defaults[spec]);r.update(durationSeconds=20,useProvisionalLevel60Baseline=True)
        r['character'].update(id=ID,race='human')
        r['talents']=dict(assassinationPointsSpent=0,combatPointsSpent=0,subtletyPointsSpent=0,ranks=copy.deepcopy(p['ranks']))
        r['talents'][spec+'PointsSpent']=sum(p['ranks'].values())
        for rank in [0,1,2]:
            for owner in ['typed','full','both']:
                env=K.envelope(r)
                if owner in ['typed','both']:env['rogue']['talents']['ranks']['elusiveness']=rank
                if owner in ['full','both']:env['itemizedTalentRanks']['elusiveness']=rank
                add(f'strict-{spec}-r{rank}-{owner}',env,SHARED,status=400)
            probe=copy.deepcopy(r);probe['talents']['ranks']['elusiveness']=rank
            if spec=='combat':add(f'strict-preview-combat-r{rank}',probe,CHARACTER,status=400)
            for route in ['validate','validate-itemized']:
                add(f'strict-{spec}-{route}-r{rank}',probe['talents'],f'/api/forever/talents/rogue/{spec}/{route}',status=400)
    for spec,path,label in [('subtlety',CHARACTER,'character'),('subtlety',ROTATION,'rotation'),('combat',ROTATION,'rotation')]:
        r=controlled(defaults,1,spec);add(f'unavailable-{label}-{spec}',r if path==CHARACTER else K.envelope(r),path,status=400)
    for label,value in [('bool',True),('fraction',D('1.5')),('string','invalid'),('numeric-string','1'),('null',None),('null-map',None)]:
        r=controlled(defaults,1);r['durationSeconds']=280
        if label=='null-map':r['talents']['ranks']=None
        else:r['talents']['ranks']['elusiveness']=value
        add('binding-'+label,r,group='binding',status=None,trace=True)
    for latency in [None,0,80]:
        r=controlled(defaults,2);r['durationSeconds']=660;r.pop('inputLatencyMs',None)
        if latency is not None:r['inputLatencyMs']=latency
        add(f'timing-{latency}',r,group='causal',trace=True)
        if latency is not None:
            add(f'concurrent-{latency}',r,group='causal',trace=True,concurrent=True,replayOf=f'timing-{latency}')
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
    require(request['talents']['ranks']=={'elusiveness':'1'},'Only frozen numeric-string 1 permits typed projection')
    result=copy.deepcopy(request);result['talents']['ranks']['elusiveness']=1;return result


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
        for key in ['path','group','baselineStatus','correctedStatus','trace','requireLaterOpener','requireRepeated','requireBlind','concurrent','replayOf']:
            require(O.encode(row.get(key))==O.encode(case.get(key)),'Frozen fixture differs: '+key)
        if case.get('body') is not None:require((root/(row['name']+'.request.json')).read_bytes()==O.encode(case['body']).encode(),'Frozen request differs: '+row['name'])
    before,after=read(root/'provenance-before.json'),read(root/'provenance-after.json')
    require(O.encode(before)==O.encode(after),'Captured source/binary changed')
    require(read(root/'report-metadata.response.json')['sourceCommit']==summary['sourceCommit'],'Captured source stamp differs')
    return {r['name']:r for r in rows}


def freeze_windows(args):
    root=pathlib.Path(args.capture);rows=verified_inventory(root)
    require(read(root/'capture-summary.json')['phase']=='baseline' and read(root/'capture-summary.json')['mode']=='probe','Old probe capture required')
    cases={}
    for spec in SPECS:
        for rank in [1,2]:
            name=f'probe-{spec}-r{rank}';value=checked_body(root,rows[name]);request=read(root/(name+'.request.json'))
            trace_accounting(request,value,'baseline')
            starts=[e for e in value['run']['timeline'] if e['type']==3 and e['abilityKey']=='vanish']
            require(len(starts)==1,'Probe needs exactly one actual successful Vanish start')
            s=D(starts[0]['timeSeconds']);a=s+cooldown('vanish',rank,'corrected');b=s+cooldown('vanish',rank,'baseline')
            cases[f'window-{spec}-r{rank}']=dict(firstStart=s,correctedReady=a,baselineReady=b,end=(a+b)/2,probeResponseSha256=rows[name]['responseSha256'])
    p=pathlib.Path(args.out);require(not p.exists(),'Refuse overwrite windows');write(p,dict(cases=cases,probePath=str(root.resolve()),probeIndexSha256=sha((root/'index.json').read_bytes())))
    print(O.encode(cases));return 0


def verify_completion_accounting(request,response):
    """Every completed instant cast has its literal spend/effect, exactly once.

    Resource events intentionally have no execution GUID. Their existing owner,
    ability and same-time completion bind them with a consumption queue instead.
    """
    ranks=request['talents']['ranks']
    costs=dict(ambush=60,backstab=60,garrote=50-10*ranks.get('dirty-deeds',0),vanish=0,sinister_strike=45,eviscerate=35,rupture=25,slice_and_dice=25,evasion=0,feint=20,kick=25,blind=30-5*ranks.get('dirty-tricks',0),preparation=0)
    costs={k.replace('_','-'):v for k,v in costs.items()};starts={};completed={};spends=collections.defaultdict(collections.deque);effects={};aura_casts=collections.defaultdict(collections.deque);proof=[]
    for i,e in enumerate(response['run']['timeline']):
        kind,key,t=e['type'],e['abilityKey'],e['timeSeconds'];eid=e['abilityExecutionId']
        if kind==3:
            require(key in costs and eid not in starts,'Unknown or duplicate cast');starts[eid]=(key,t)
        if kind==4:
            require(starts.get(eid)==(key,t) and eid not in completed,'Cast completion linkage differs');completed[eid]=(key,t)
            if costs[key]>0:spends[(key,t)].append(eid)
            if key!='vanish':effects[eid]=0
            if key in ['garrote','rupture','slice-and-dice']+UTILITIES:aura_casts[(key,t)].append(eid)
        if kind==8:
            require(key in costs and costs[key]>0 and e['amount']==-costs[key] and e['sourceActorKey']==e['targetActorKey']=='rogue' and e['resourceKey']=='energy' and eid is None,'Unexpected or nonliteral Energy spend')
            q=spends[(key,t)];require(q,'Spend lacks unmatched costful completed cast');proof.append(dict(event=i,execution=q.popleft(),key=key,time=t,cost=costs[key]))
        if kind==6 and not e['isPeriodic'] and eid in effects:
            require(eid in completed and completed[eid]==(key,t),'Direct effect lacks same-time completion');effects[eid]+=1
            if key in ['garrote','rupture']:
                q=aura_casts[(key,t)];require(q and q.popleft()==eid,'Failed periodic application linkage differs')
        if kind==9 and key in ['garrote','rupture','slice-and-dice']+UTILITIES:
            q=aura_casts[(key,t)];require(q,'Aura lacks unmatched completed cast');effects[q.popleft()]+=1
    require(set(starts)==set(completed),'Missing instant cast completion')
    require(all(not q for q in spends.values()),'Missing completed-cast Energy spend')
    require(all(n==1 for n in effects.values()) and all(not q for q in aura_casts.values()),'Missing or duplicate completed-cast effect')
    return proof


def verify_stealth_lifecycle(request,response):
    """Bounded live Stealth identity and both auto streams, including refresh.

    Current Vanish emits vanish-stealth then stealth-state. Refresh replaces
    the GUID without a removal event, but later removal retains the original
    vanish-stealth effect key. This verifier preserves that existing behavior.
    """
    live=None;vanish=None;exit_pending=None;restart=None;autos={};opening=0;proof=[]
    hands={'rogue-main-hand','rogue-off-hand'}
    for i,e in enumerate(response['run']['timeline']):
        kind,key,t=e['type'],e['abilityKey'],e['timeSeconds']
        if kind==4 and key=='vanish':
            require(live is None and exit_pending is None and (vanish is None or vanish['applications']==2),'Vanish completed before prior Stealth transition settled')
            require(set(autos)==hands,'Vanish must stop both live auto streams in these no-kill fixtures')
            vanish=dict(time=t,event=i,execution=e['abilityExecutionId'],applications=0,stops=set())
        if kind==9 and key in ['opening-stealth','vanish']:
            require(e['sourceActorKey']==e['targetActorKey']=='rogue','Stealth aura owner differs')
            if key=='opening-stealth':
                require(request['startInStealth'] and t==0 and opening==0 and live is None and not autos and e['effectKey']=='stealth-state','Unexpected initial Stealth application');opening+=1
                live=dict(aura=e['auraInstanceId'],ability=key,effect='stealth-state')
            else:
                require(vanish is not None and vanish['time']==t and vanish['applications']<2,'Vanish aura lacks same-time unmatched completed Vanish')
                if vanish['applications']==0:
                    require(e['effectKey']=='vanish-stealth' and live is None,'Missing/misordered Vanish effect application')
                else:
                    require(e['effectKey']=='stealth-state' and vanish['stops']==hands and not autos and live is not None and e['auraInstanceId']!=live['aura'],'Missing Vanish auto stops or refresh application')
                live=dict(aura=e['auraInstanceId'],ability=key,effect='vanish-stealth');vanish['applications']+=1
            proof.append(dict(event=i,action='apply',time=t,**live))
        triggers=kind==4 and key in ['ambush','garrote','cheap-shot'] or kind==6 and e['sourceActorKey']=='rogue' and not e['isPeriodic'] and e['amount']>0 and key!='blade-flurry-cleave'
        if triggers and exit_pending is None:
            if live is not None:
                exit_pending=dict(time=t,event=i,ability=key,execution=e['abilityExecutionId'])
            elif kind==6 and key in hands and vanish is not None and vanish['applications']==0 and vanish['time']==t and vanish['event']<i:
                # Completion mutates the aura immediately; EmitEvent queues its
                # notifications. Already-queued owned auto damage can therefore
                # break live Stealth before either application is dispatched.
                # Bind only this actual pending transition, never earlier damage.
                require(e['targetActorKey']=='target' and key in autos and e['abilityExecutionId'] is None and e['autoAttackInstanceId'] is None and e['effectKey']=='weapon-hit' and e['weaponHandKey']==key.removeprefix('rogue-'),'Pending Stealth exit lacks owned live auto stream')
                exit_pending=dict(time=t,event=i,ability=key,execution=None,vanishCompletionEvent=vanish['event'],vanishExecution=vanish['execution'],activeAutoAttackInstanceId=autos[key],scope='Owned auto damage after same-time Vanish completion before queued application notifications')
        if kind==10 and key in ['opening-stealth','vanish']:
            require(live is not None and exit_pending is not None and exit_pending['time']==t,'Stealth removal lacks live aura and actual exit trigger')
            require(e['auraInstanceId']==live['aura'] and e['abilityKey']==live['ability'] and e['effectKey']==live['effect'] and e['sourceActorKey']==e['targetActorKey']=='rogue','Stealth removal identity/owner/effect differs')
            require(not autos,'Stealth removal occurs while autos already active');proof.append(dict(event=i,action='remove',time=t,trigger=exit_pending,**live));live=None;exit_pending=None;restart=dict(time=t,remaining=set(hands))
        if kind==34:
            require(key in hands and vanish is not None and vanish['time']==t and vanish['applications']==1 and key not in vanish['stops'],'Unexpected auto stop without completed Vanish')
            require(autos.pop(key,None)==e['autoAttackInstanceId'],'Auto stop identity differs');vanish['stops'].add(key)
        if kind==32:
            require(key in hands and key not in autos,'Unexpected duplicate auto start')
            if not request['startInStealth'] and t==0 and live is None and vanish is None:
                pass  # The runner starts both initial streams before its first decision.
            else:
                require(restart is not None and restart['time']==t and key in restart['remaining'] and live is None,'Auto restart lacks same-time actual Stealth removal')
                restart['remaining'].remove(key)
                if not restart['remaining']:restart=None
            autos[key]=e['autoAttackInstanceId']
        if kind==3:
            require(exit_pending is None and restart is None,'Next action hides missing Stealth removal/auto restart')
            if key!='vanish' and vanish is not None:require(vanish['applications']==2,'Next action hides missing Vanish application')
    require(opening==int(request['startInStealth']),'Missing initial Stealth application')
    require(vanish is None or vanish['applications']==2 and vanish['stops']==hands,'Missing Vanish application or auto stop')
    require(exit_pending is None and restart is None,'Missing final Stealth removal or auto restart')
    require(live is not None and not autos or live is None and set(autos)==hands,'Final Stealth/auto state mismatch')
    return proof


def trace_accounting(request,response,phase,broad=False):
    """Independent narrow priority, costs, damage, CP and lifecycle accounting.

    Imported envelope/summary helpers are used only under their literal zero-
    haste, two-second-auto, no-proc, one-target, live-target assumptions. We do
    not import their fixed-300-second Vanish or non-dagger action oracle.
    """
    ranks=request['talents']['ranks'];require(set(ranks)<=({'elusiveness','dirty-tricks','preparation','cutthroat'} if broad else {'elusiveness','dirty-deeds'}),'Unsupported trace talent')
    require(request['additionalTargets']==0 and (broad or not request['useInstantPoison'] and not request['useUtilityAbilities'] and not request['enableBossAttacks']),'Unsupported trace mechanism')
    if broad:
        require(request['targetArmor']==0 and request['tuning']['physicalCritPercent']==0 and request['level60Baseline'] is None and request['tuning']['mainHandHitPercent']==request['tuning']['offHandHitPercent']==100 and request['targetDodgePercent']==request['targetParryPercent']==0,'Unsupported broad resolver stats')
    require(request['character']['baseStats']['values']=={'attack-power':0},'Narrow AP budget differs')
    tuning=request['tuning'];require(tuning['mainHandSpeedSeconds']==tuning['offHandSpeedSeconds']==2 and tuning['mainHandWeaponDamage']==100 and tuning['offHandWeaponDamage']==40 and tuning['sliceAndDiceHastePercent']==0,'Unsupported narrow weapon/haste tuning')
    require(request['targetArmor'] in [0,tuning['armorDamageReductionConstant']],'Unsupported armor oracle')
    (verify_broad_envelopes if broad else H.verify_event_envelopes)(request,response)
    garrote_lifecycle=H.verify_garrote_lifecycle(request,response)
    completion_proof=verify_completion_accounting(request,response);stealth_proof=verify_stealth_lifecycle(request,response)
    events=response['run']['timeline'];rank=ranks.get('elusiveness',0);rng=K.SeededRandom(request['seed'])
    cp=0;rupture_cp=0;finishers=0;openers=0;stealth=request['startInStealth'];in_combat=False
    damage=collections.defaultdict(lambda:D(0));threat=collections.defaultdict(collections.deque)
    total=D(0);energy=D(request['startingEnergyPercent']);energy_time=D(0)
    started={};completed={};generators=set();pending={};resources=[];draws=[];damage_proofs=[];priority=[];auras={};auto_clocks={};vanishes=[];opener_links=[];last_vanish=None;bleed=None
    utility_ready={k:D(0) for k in UTILITIES};utility_live={};cutthroat=None;cutthroat_eligible=collections.Counter();feints=0;feint_pending=collections.deque();boss_threat=D(0);preparation_resets=0
    charges={k:request['poisonChargesPerWeapon'] for k in ['main-hand','off-hand']};poison_eligible=collections.Counter();deadly=None;poison_apps=0;poison_ticks=0;boss_times=[];boss_impacts=[];dodges=0
    ready=D(0);vanish_ready=D(0);slice_until=D(0);rupture_until=D(0);latency=D(request.get('inputLatencyMs',80))/1000
    costs={'evasion':0,'feint':20,'kick':25,'blind':30-5*ranks.get('dirty-tricks',0),'preparation':0,'ambush':60,'backstab':60,'garrote':50-10*ranks.get('dirty-deeds',0),'vanish':0,'sinister-strike':45,'eviscerate':35,'rupture':25,'slice-and-dice':25}
    for i,e in enumerate(events):
        kind,key,t=e['type'],e['abilityKey'],D(e['timeSeconds'])
        require(0<=t<=request['durationSeconds'] and (i==0 or t>=events[i-1]['timeSeconds']),'Trace order differs')
        if kind==3:
            available=min(D(100),energy+(t-energy_time)*tuning['energyPerSecond']);candidates=[]
            if request['useUtilityAbilities']:
                candidates.extend(k for k in UTILITIES if (k!='preparation' or ranks.get('preparation',0)>0) and t>=utility_ready[k])
            if (stealth or cutthroat is not None and t<cutthroat['expires']) and request['bothWeaponsDaggers'] and request['isBehindTarget']:candidates.append('ambush')
            if stealth and (request['isBehindTarget'] or ranks.get('dirty-deeds',0)>0):candidates.append('garrote')
            if not stealth and in_combat and (cutthroat is None or t>=cutthroat['expires']) and t>=vanish_ready:candidates.append('vanish')
            if cp>=2 and t>=slice_until:candidates.append('slice-and-dice')
            if cp>=4 and t>=rupture_until:candidates.append('rupture')
            if cp>=4:candidates.append('eviscerate')
            if request['specialization']=='subtlety' and request['bothWeaponsDaggers'] and request['isBehindTarget']:candidates.append('backstab')
            candidates.append('sinister-strike');affordable=[k for k in candidates if costs[k]<=available]
            require(affordable and key==affordable[0],f'Priority/affordability differs at {i}: {key}, {affordable}, {available}')
            require(t>=ready,'Action precedes actual input/GCD readiness')
            execution=e['abilityExecutionId'];require(execution not in started,'Duplicate cast start');started[execution]=(key,t)
            priority.append(dict(event=i,time=t,key=key,energy=available,candidates=candidates,affordable=affordable,inputReadyAt=ready,comboPoints=cp,stealth=stealth,vanishReadyAt=vanish_ready))
            ready=t+(0 if key in ['vanish','preparation'] else 1)+latency
            if key in UTILITIES:utility_ready[key]=t+(cooldown('blind',rank,phase) if key=='blind' else {'evasion':300,'feint':10,'kick':10,'preparation':600}[key])
            if key=='vanish':
                vanish_ready=t+cooldown('vanish',rank,phase);last_vanish=dict(event=i,time=t,execution=execution,ready=vanish_ready);vanishes.append(last_vanish)
            if key in ['ambush','garrote'] and last_vanish is not None and stealth:opener_links.append(dict(vanish=last_vanish,openerExecution=execution,openerTime=t,openerKey=key))
        if kind==4:
            execution=e['abilityExecutionId'];require(started.get(execution)==(key,t) and execution not in completed,'Invalid completion');completed[execution]=(key,t)
            if key=='vanish':stealth=True
            if key=='preparation':
                vanish_ready=t
                for eligible in ['evasion','blind']:utility_ready[eligible]=t
                preparation_resets+=4  # Vanish/Evasion/Sprint/Blind exist; optional Cold Blood/Ghostly Strike do not.
            if key=='feint':
                reduction=min(boss_threat,tuning['feintThreatReduction']);boss_threat-=reduction;feints+=reduction
                if reduction:feint_pending.append((t,-reduction))
            if key in ['ambush','garrote']:openers+=1;stealth=False;in_combat=True
            if key in ['rupture','slice-and-dice']:pending[key]=execution
        if kind==8:
            require(key in costs and e['resourceKey']=='energy','Unsupported resource event')
            energy=min(D(100),energy+(t-energy_time)*tuning['energyPerSecond']);energy_time=t
            require(e['amount']==-costs[key] and energy>=costs[key],'Literal cost/spend differs');before=energy;energy-=costs[key]
            require(energy==e['resourceTotalAfter'],'Energy regeneration/cap/spend differs');resources.append(dict(event=i,key=key,time=t,before=before,cost=costs[key],after=energy))
        if kind==9:
            if key in UTILITIES:
                require(any(k==key and at==t for k,at in completed.values()),'Utility aura lacks completed cast')
                if key in utility_live:require(utility_live[key]['expires']>=t,'Utility refresh skips expiry')
                utility_live[key]=dict(aura=e['auraInstanceId'],expires=t+UTILITY_DURATION[key])
            if key=='cutthroat':
                require(ranks.get('cutthroat',0)>0 and cutthroat_eligible[t]>0,'Cutthroat lacks qualifying Backstab');cutthroat_eligible[t]-=1
                cutthroat=dict(aura=e['auraInstanceId'],expires=t+10)
            if key=='deadly-poison':
                require(request['useInstantPoison'] and request['offHandPoison']=='deadly' and poison_eligible[(t,'off-hand')]>0 and charges['off-hand']>0,'Deadly lacks qualifying coating/hit')
                poison_eligible[(t,'off-hand')]-=1;charges['off-hand']-=1;poison_apps+=1
                active=deadly is not None and deadly['expires']>t
                deadly=dict(aura=e['auraInstanceId'],stacks=min(5,deadly['stacks']+1) if active else 1,next=deadly['next'] if active else t+tuning['deadlyPoisonTickIntervalSeconds'],expires=t+tuning['deadlyPoisonDurationSeconds'])
                require(e['description']==f'Deadly Poison applied to Level 63 Rogue training boss (stacks: {deadly["stacks"]}).','Deadly stack count differs')
            if key in ['rupture','garrote']:
                if not broad:
                    result,critical,new=K.roll(rng,request,e,application=True);require(result=='hit','Seeded aura application differs');draws.extend(dict(event=i,**d) for d in new)
            if key=='garrote':cp=min(5,cp+1)
            if key in ['rupture','slice-and-dice']:
                require(key in pending and cp>0,'Finisher lacks completed cast/CP');execution=pending.pop(key)
                expiry=t+6+(3 if key=='slice-and-dice' else 2)*cp;auras[e['auraInstanceId']]=(key,expiry)
                if key=='slice-and-dice':slice_until=expiry
                else:
                    require(bleed is None or bleed['next']>=t,'Rupture refresh skipped tick')
                    rupture_until=expiry;rupture_cp=cp;bleed=dict(next=t+2,expires=expiry,execution=execution,aura=e['auraInstanceId'])
                finishers+=1;cp=0
            if key in ['opening-stealth','vanish']:stealth=True
        if kind==10:
            if key in UTILITIES:
                require(utility_live.get(key)==dict(aura=e['auraInstanceId'],expires=t),'Utility expiry identity/time differs');utility_live.pop(key)
            if key=='cutthroat':
                require(cutthroat is not None and cutthroat['aura']==e['auraInstanceId'],'Cutthroat identity differs')
                if 'expired' in e['description']:require(t==cutthroat['expires'],'Cutthroat expiry differs')
                else:require(any(k=='ambush' and at==t for k,at in completed.values()),'Cutthroat consumed without completed Ambush')
                cutthroat=None
            if key=='deadly-poison':
                require(deadly is not None and deadly['aura']==e['auraInstanceId'] and t==deadly['expires'] and deadly['next']>=t,'Deadly expiry/ticks differ');deadly['removed']=True
            if key in ['opening-stealth','vanish']:stealth=False
            if key in ['rupture','slice-and-dice']:
                require(auras.pop(e['auraInstanceId'],None)==(key,t),'Finisher expiry differs')
                if key=='rupture':require(bleed is not None and bleed['next']>t,'Missing expiration-boundary Rupture tick');bleed=None
        if kind==32:
            require(key not in auto_clocks,'Auto restarted without stop');auto_clocks[key]=t+2
        if kind==34:require(key in auto_clocks and auto_clocks[key]>=t,'Auto stopped with missing swing');auto_clocks.pop(key)
        if kind==17:
            require(request['enableBossAttacks'] and t>0 and t%request['bossSwingSeconds']==0 and t not in boss_times,'Unexpected boss schedule');boss_times.append(t)
        if kind==6 and e['sourceActorKey']=='target':
            require(request['enableBossAttacks'] and t in boss_times and t not in boss_impacts,'Incoming impact lacks unique scheduled swing');boss_impacts.append(t)
            require(e['amount'] in [0,request['bossHitDamage']],'Unsupported incoming damage');dodges+=int(e['amount']==0)
            continue
        if kind==6:
            if key.startswith('rogue-'):require(auto_clocks.get(key)==t,'Auto swing time differs');auto_clocks[key]=t+2
            if key=='rupture' and e['isPeriodic']:
                require(bleed is not None and t==bleed['next'] and t<=bleed['expires'] and e['abilityExecutionId']==bleed['execution'],'Rupture tick time/execution differs');bleed['next']+=2
            application=key in ['rupture','garrote'] and not e['isPeriodic']
            if broad:
                result,critical='hit',False
                require(e['resultKey']=='hit' and not e['isCritical'],'Broad bounded hit/crit differs')
            else:
                result,critical,new=K.roll(rng,request,e,application);draws.extend(dict(event=i,**d) for d in new)
                require(e['resultKey']==result and e['isCritical']==critical,f'Seeded result differs at {i} {key}')
            base={'ambush':D(250)+tuning['ambushBonusDamage'],'backstab':D(150)+tuning['backstabBonusDamage'],
                  'sinister-strike':D(100)+tuning['sinisterStrikeBonusDamage'],'rogue-main-hand':D(100),'rogue-off-hand':D(20),
                  'eviscerate':tuning['eviscerateDamagePerPoint']*max(1,cp),'rupture':tuning['ruptureTickDamagePerPoint']*max(1,rupture_cp),'garrote':tuning['garroteTickDamage']}
            if key=='instant-poison':
                require(request['useInstantPoison'] and request['mainHandPoison']=='instant' and poison_eligible[(t,'main-hand')]>0 and charges['main-hand']>0,'Instant lacks qualifying coating/hit')
                poison_eligible[(t,'main-hand')]-=1;charges['main-hand']-=1;poison_apps+=1;base[key]=tuning['poisonDamage']
            if key=='deadly-poison':
                require(deadly is not None and t==deadly['next'] and t<=deadly['expires'],'Deadly tick schedule differs');base[key]=tuning['deadlyPoisonDamagePerStack']*deadly['stacks'];deadly['next']+=tuning['deadlyPoisonTickIntervalSeconds'];poison_ticks+=1
            require(key in base,'Unsupported damage key');raw=D(base[key])*(2 if critical else 1) if result in ['hit','critical'] and not application else D(0)
            amount=raw/2 if not e['isPeriodic'] and request['targetArmor'] else raw
            require(e['rawAmount']==raw and e['amount']==amount and e['mitigatedAmount']==raw-amount,f'Literal damage differs at {i} {key}')
            damage[key]=M.dotnet_add(damage[key],amount);total=M.dotnet_add(total,amount);damage_proofs.append(dict(event=i,key=key,raw=raw,amount=amount,comboPoints=cp,rupturePoints=rupture_cp))
            if amount>0:threat[(t,key,e['effectKey'])].append(amount);boss_threat=M.dotnet_add(boss_threat,amount)
            if amount>0 and not e['isPeriodic']:
                stealth=False;in_combat=True
                if e['weaponHandKey'] in charges:poison_eligible[(t,e['weaponHandKey'])]+=1
                if key=='backstab':cutthroat_eligible[t]+=1
                if key in ['sinister-strike','backstab','ambush'] and e['abilityExecutionId'] not in generators:cp=min(5,cp+1);generators.add(e['abilityExecutionId'])
            if key=='eviscerate' and result not in ['miss','dodge','parry'] and cp>0:finishers+=1;cp=0
        if kind==20:
            if key=='feint':require(feint_pending and feint_pending.popleft()==(t,e['amount']),'Feint threat change differs')
            else:
                q=threat[(t,key,e['effectKey'])];require(q and e['amount']==q.popleft(),'Damage/threat relationship differs')
    require(all(not q for q in threat.values()) and not feint_pending,'Missing threat event')
    require(all(v['expires']>=request['durationSeconds'] for v in utility_live.values()),'Missing utility expiry')
    require(deadly is None or deadly['expires']>=request['durationSeconds'] and deadly['next']>=request['durationSeconds'] or deadly.get('removed') and deadly['next']>deadly['expires'],'Missing final Deadly tick/expiry')
    if request['enableBossAttacks']:
        expected=[];t=D(request['bossSwingSeconds'])
        while t<request['durationSeconds']:expected.append(t);t+=request['bossSwingSeconds']
        require(boss_times==boss_impacts==expected,'Missing/extra boss swings or impacts')
    require(all(t>=request['durationSeconds'] for t in auto_clocks.values()),'Missing final auto swing')
    require(bleed is None or bleed['next']>=request['durationSeconds'],'Missing final Rupture tick')
    require(total<request['targetMaximumHealth'],'No-kill target died')
    energy=min(D(100),energy+(D(request['durationSeconds'])-energy_time)*tuning['energyPerSecond'])
    require(response['endingEnergy']==energy and response['endingComboPoints']==cp,'Ending Energy/CP differs')
    require(response['finishersUsed']==finishers and response['stealthOpeners']==openers,'Finisher/opener count differs')
    require(response['damageDone']==total and response['dps']==M.dotnet_divide(total,request['durationSeconds']),'Damage/DPS differs')
    require(O.encode(response['damageByAbility'])==O.encode(dict(damage)),'Damage breakdown differs')
    if broad:verify_broad_summary(request,response,energy)
    else:K.verify_ability_summaries(response);H.verify_static_summary(request,response,damage,total,energy)
    metrics={'rogue-expose-armor-points':0,'rogue-stealth-openers':openers,'rogue-finisher-count':finishers,'rogue-poison-procs':poison_apps,'rogue-deadly-poison-ticks':poison_ticks,'rogue-main-hand-poison-charges':charges['main-hand'],'rogue-off-hand-poison-charges':charges['off-hand'],'rogue-preparation-cooldowns-reset':preparation_resets,'rogue-hack-and-slash-attacks':0,'rogue-quick-recovery-energy':0,'rogue-ending-combo-points':cp,'rogue-boss-threat':boss_threat,'rogue-feint-threat-reduced':feints,'rogue-boss-attacks-dodged':dodges,'rogue-boss-attacks-parried':0}
    require(O.encode(response['run']['customMetrics'])==O.encode(metrics),'Complete custom metrics differ')
    require(response['maximumEnergy']==100 and response['poisonProcs']==poison_apps and response['endingHealth']==response['maximumHealth'],'Health/caps differ')
    for link in opener_links:
        hits=[e for e in events if e['type']==6 and e['abilityExecutionId']==link['openerExecution'] and e['amount']>0]
        link['damageEvents']=len(hits);link['damage']=sum((e['amount'] for e in hits),D(0))
    return dict(events=len(events),vanishes=vanishes,openerLinks=opener_links,resources=resources,rngDraws=draws,damage=damage_proofs,priority=priority,garroteLifecycle=garrote_lifecycle,completionSpends=completion_proof,stealthLifecycle=stealth_proof,starvation=starvation_accounting(request,response),broadScope='Bounded all-field event/summary arithmetic and lifecycle checks; Cutthroat/poison/incoming-avoidance RNG is not independently reconstructed. Complete seeded replay is separately required.' if broad else None,processedEventCount=response['run']['processedEventCount'],internalSchedulerLimit='Internal no-action checks are not public timeline events. Their count is retained without projection and compared exactly in old-old/new-new replay, not independently reconstructed.')


def broad_accounting(request,response,phase):
    return trace_accounting(request,response,phase,broad=True)


UTILITIES = ['evasion','feint','kick','blind','preparation']
UTILITY_DURATION = dict(evasion=15,feint=1,kick=1,blind=10,preparation=1)
UTILITY_AURA = dict(evasion='rogue-evasion',feint='rogue-feint-threat',kick='rogue-interrupt-attempt',blind='rogue-blind-control',preparation='rogue-preparation-activated')


def starvation_accounting(request,response):
    # Same explicitly bounded zero-cast-time/live-target model, now including
    # Preparation's real off-GCD input registration. Not a generic scheduler.
    ready=D(0);total=D(0);first=None;intervals=[];latency=D(request.get('inputLatencyMs',80))/1000
    for e in response['run']['timeline']:
        if e['type']!=3:continue
        t=e['timeSeconds'];require(t>=ready,'Action before GCD/input ready')
        if t>ready:
            intervals.append(dict(start=ready,end=t));total+=t-ready
            if first is None:first=ready
        ready=t+(0 if e['abilityKey'] in ['vanish','preparation'] else 1)+latency
    if request['durationSeconds']>ready:
        intervals.append(dict(start=ready,end=request['durationSeconds']));total+=request['durationSeconds']-ready
        if first is None:first=ready
    return dict(total=total,first=first,intervals=intervals)


def verify_broad_envelopes(request,response):
    events=response['run']['timeline'];special=set(UTILITIES+['instant-poison','deadly-poison']);actor=request['character']['name'];boss='Level 63 Rogue training boss'
    # This partition does not mask a field: every excluded event is checked
    # below against a complete literal envelope, and included events use the
    # established all-field validator. Whole raw bodies remain replayed.
    H.verify_event_envelopes(request,dict(run=dict(timeline=[e for e in events if e['abilityKey'] not in special and e['sourceActorKey']!='target'])))
    starts={};completed={};identities={};applied={}
    for i,e in enumerate(events):
        require(set(e)==H.EVENT_FIELDS,'Unexpected event schema')
        for field in O.IDS:
            v=e[field]
            if v is not None:
                require(isinstance(v,str) and O.GUID.fullmatch(v) and v!=O.ZERO_GUID,'Malformed generated identity')
                require(v not in identities or identities[v]==field,'Identity cross-domain collision');identities[v]=field
        kind,key,t=e['type'],e['abilityKey'],e['timeSeconds']
        if kind==3:require(e['abilityExecutionId'] not in starts,'Joined execution identity');starts[e['abilityExecutionId']]=(key,t)
        if kind==4:require(starts.get(e['abilityExecutionId'])==(key,t) and e['abilityExecutionId'] not in completed,'Completion link differs');completed[e['abilityExecutionId']]=(key,t)
        if key not in special and e['sourceActorKey']!='target':continue
        want={f:None for f in H.EVENT_FIELDS};want.update(timeSeconds=t,type=kind,encounterSequenceSelectedActorKeys=[],isCritical=False,effectDeliveryType=0,isPeriodic=False,isChannelTick=False,isInternal=False,sourceActorKey='rogue',targetActorKey='target',abilityKey=key)
        if key in UTILITIES:
            if kind in [3,4]:want.update(abilityExecutionId=e['abilityExecutionId'],description=f'{actor} started casting {key}.' if kind==3 else f'{actor} completed {key}.')
            elif kind==8:
                require(any(k==key and at==t for k,at in completed.values()),'Utility spend lacks completion')
                want.update(targetActorKey='rogue',amount=e['amount'],resourceKey='energy',resourceTotalAfter=e['resourceTotalAfter'],description=f'{actor} spent {M.format_damage(-e["amount"])} energy.')
            elif kind in [9,10]:
                target=actor if key in ['evasion','feint','preparation'] else boss
                want.update(targetActorKey='rogue' if target==actor else 'target',effectKey=key+'-effect',auraInstanceId=e['auraInstanceId'],description=f'{UTILITY_AURA[key]} applied to {target} (stacks: 1).' if kind==9 else f'{UTILITY_AURA[key]} expired on {target}.')
                if kind==9:require(e['auraInstanceId'] not in applied,'Joined utility aura identity');applied[e['auraInstanceId']]=key
                else:require(applied.get(e['auraInstanceId'])==key,'Utility aura owner differs')
            elif kind==20:
                require(key=='feint','Unexpected utility threat')
                want.update(amount=e['amount'],description='Feint reduced real enemy threat (provisional fixed amount).')
            else:raise AssertionError('Unexpected utility event kind')
        elif key in ['instant-poison','deadly-poison']:
            periodic=key=='deadly-poison';effect='deadly-tick' if periodic else 'poison-hit'
            if kind in [9,10]:
                require(periodic,'Instant poison cannot apply aura')
                want.update(effectKey='deadly-stack',auraInstanceId=e['auraInstanceId'],description=e['description'] if kind==9 else f'Deadly Poison expired on {boss}.')
                if kind==9:require(e['auraInstanceId'] not in applied,'Joined poison aura');applied[e['auraInstanceId']]=key
                else:require(applied.get(e['auraInstanceId'])==key,'Poison aura owner differs')
            elif kind==6:
                want.update(effectKey=effect,schoolKey='nature',resolutionType='spell',mitigationType='none',resultKey='hit',rawAmount=e['rawAmount'],amount=e['amount'],mitigatedAmount=0,mitigationPercent=0,blockedAmount=0,absorbedAmount=0,isPeriodic=periodic,effectDeliveryType=1 if periodic else 0,description=f'{"Deadly Poison" if periodic else "Instant Poison"} dealt {M.format_damage(e["amount"])} damage to {boss}.')
            elif kind==20:want.update(effectKey=effect,amount=e['amount'],description='Rogue damage threat added to shared table.')
            else:raise AssertionError('Unexpected poison event kind')
        else:
            want.update(sourceActorKey='target',targetActorKey='rogue',encounterEventKey='rogue-boss-swing',schoolKey='physical',mitigationType='none')
            if kind==17:want.update(abilityKey=None,rawAmount=request['bossHitDamage'])
            elif kind==6:want.update(abilityKey='encounter:rogue-boss-swing',resultKey='hit',rawAmount=e['amount'],amount=e['amount'],mitigatedAmount=0,mitigationPercent=0,absorbedAmount=0,description=f'Encounter damage dealt {M.format_damage(e["amount"])} damage to {actor}.')
            else:raise AssertionError('Unexpected incoming event kind')
        require(O.encode(e)==O.encode(want),'Complete broad event differs at '+str(i)+': '+O.encode(M.differences(want,e)))


def verify_broad_summary(request,response,energy):
    events=response['run']['timeline'];done={a:collections.defaultdict(lambda:D(0)) for a in ['rogue','target']};taken={a:collections.defaultdict(lambda:D(0)) for a in done};total_done={a:D(0) for a in done};total_taken={a:D(0) for a in done}
    for e in events:
        if e['type']!=6:continue
        a,b,k,x=e['sourceActorKey'],e['targetActorKey'],e['abilityKey'],e['amount'];require(a in done and b in done,'Unknown actor')
        done[a][k]=M.dotnet_add(done[a][k],x);taken[b][k]=M.dotnet_add(taken[b][k],x);total_done[a]=M.dotnet_add(total_done[a],x);total_taken[b]=M.dotnet_add(total_taken[b],x)
    starvation=starvation_accounting(request,response);actors={}
    nums='healingDone healingReceived overhealingDone overhealingReceived absorptionDone absorptionReceived blockedDamageReceived'.split();maps='healingDoneByAbility healingReceivedByAbility absorptionDoneByAbility absorptionReceivedByAbility'.split()
    for actor in ['rogue','target']:
        primary=actor=='rogue';row=dict(actorKey=actor,name=request['character']['name'] if primary else 'Level 63 Rogue training boss',damageDone=total_done[actor],damageTaken=total_taken[actor],damageDoneByAbility=dict(done[actor]),damageTakenByAbility=dict(taken[actor]),abilities=H.ability_summaries(events,actor),startingResources={'energy':D(request['startingEnergyPercent'])} if primary else {},endingResources={'energy':energy} if primary else {},resourceStarvedSeconds=starvation['total'] if primary else 0,firstResourceStarvedAtSeconds=starvation['first'] if primary else None,died=False,deathTimeSeconds=None,blockedDamageReceivedByAbility={k:0 for k in taken[actor]})
        row.update({k:0 for k in nums});row.update({k:{} for k in maps});actors[actor]=row
    expected={k:actors['rogue'][k] for k in ['damageDone','damageTaken','damageDoneByAbility','damageTakenByAbility','abilities','blockedDamageReceivedByAbility']+nums+maps};expected.update(seed=request['seed'],durationSeconds=request['durationSeconds'],actorSummaries=actors,primaryActorDied=False,primaryActorDeathTimeSeconds=None)
    require(O.encode(response['run']['summary'])==O.encode(expected),'Complete broad summary differs: '+O.encode(M.differences(expected,response['run']['summary'])[:4]))
    require(set(response['run'])=={'summary','timeline','processedEventCount','customMetrics'} and type(response['run']['processedEventCount']) is int and response['run']['processedEventCount']>=len(events)-1,'Broad run schema/count differs')
    require(set(response)==set('specialization damageDone dps endingEnergy maximumEnergy maximumHealth endingHealth endingComboPoints poisonProcs finishersUsed stealthOpeners damageByAbility run modelingNotes'.split()) and response['specialization']==request['specialization'],'Broad response schema differs')
    require(response['maximumHealth']==request['rogueMaximumHealth'],'Public preserved Health cap differs')


def verify_report_contract():
    """Pure current-reader checks; parsed artifacts are not wire-Decimal proof."""
    inputs=['WarcraftSim.Api/wwwroot/solo-dps-comparison.js','tools/solo-dps-assassination-report.test.cjs','tools/solo-dps-combat-comparison.test.js','tools/solo-dps-combat-comparison-fixture.json']
    inputs += [str(p.relative_to(ROOT)) for p in sorted((ROOT/'tools/fixtures/assassination-reports').glob('*.json'))]
    before={p:sha((ROOT/p).read_bytes()) for p in inputs}
    script=r'''
const assert=require('node:assert/strict'),api=require('./WarcraftSim.Api/wwwroot/solo-dps-comparison.js'),a=require('./tools/solo-dps-assassination-report.test.cjs'),c=require('./tools/solo-dps-combat-comparison.test.js');
const clone=x=>JSON.parse(JSON.stringify(x)),proof={positive:[],negative:[],comparison:[]};
for(const [name,r] of a.positives()) {const bytes=JSON.stringify(r);api.inspectReport(r);assert.equal(api.compareReports(r,r).comparable,true);assert.equal(JSON.stringify(r),bytes);proof.positive.push('Assassination '+name);}
for(const points of [31,30]) {const r=c.report(points);assert.equal(api.inspectReport(r).integrityWarnings.length,0);proof.positive.push('Combat '+points);}
for(const [spec,original] of [['assassination',a.original('rogue-manual')],['combat',c.report()]])for(const rank of [0,1,2])for(const index of [0,1,2,'all']) {
 const r=clone(original),maps=[r.build.talents.ranks,r.originalSimulationRequest.itemizedTalentRanks,r.originalSimulationRequest.rogue.talents.ranks];if(index==='all'){for(const map of maps)map.elusiveness=rank;if(spec==='combat'){r.combatPreparation.request.talents.ranks.elusiveness=rank;r.rawServerResponse.preparation.request.talents.ranks.elusiveness=rank;}}else maps[index].elusiveness=rank;
 const bytes=JSON.stringify(r);let error;try{api.inspectReport(r);}catch(e){error=e.message;}assert.ok(error);assert.match(error,spec==='assassination'?/unsupported talent key/i:index===2?/owned request/i:/unsupported exact rank|ranks/i);assert.equal(JSON.stringify(r),bytes);proof.negative.push({spec,rank,map:index,error});
}
const empty=a.allocation(a.original('rogue-manual'),{}),zero=a.allocation(a.original('rogue-manual'),{malice:0});let q=api.compareReports(empty,zero);assert.equal(q.comparable,true);assert.equal(q.dpsDelta,0);assert.ok(q.changes.some(x=>x.includes('zero-key presence')));proof.comparison.push('Supported-zero presence remains comparable');
const original=a.original('rogue-manual');
for(const [name,value] of [['weapon',234],['targetArmor',0]]) {const r=clone(original);if(name==='weapon')r.build.encounter.rogue.tuning.mainHandWeaponDamage=r.originalSimulationRequest.rogue.tuning.mainHandWeaponDamage=value;else {r.build.encounter.rogue.targetArmor=r.originalSimulationRequest.rogue.targetArmor=value;r.combatSummary.targetSemantics.targetArmor=value;}q=api.compareReports(original,r);assert.equal(q.comparable,name==='weapon');proof.comparison.push({name,comparable:q.comparable});}
assert.equal(api.compareReports(c.report(31),c.report(30)).comparable,true);proof.comparison.push('Combat legal underallocation remains comparable');
process.stdout.write(JSON.stringify(proof));
'''
    value=O.decode(subprocess.check_output(['node','-e',script],cwd=ROOT))
    require(before=={p:sha((ROOT/p).read_bytes()) for p in inputs},'Report fixture/module bytes changed')
    return dict(passed=True,scope='Current parsed-report admission/comparison only; no raw API replay, server attestation, or fabricated historical report claim.',inputs=before,proof=value)


def scheduler_pair(before,after):
    """Bind actual isolated private dispatch witnesses, not inferred HTTP events."""
    result={};sources=[]
    for phase,root in [('baseline',pathlib.Path(before)),('corrected',pathlib.Path(after))]:
        receipt=read(root/'receipt.json');raw=(root/'scheduler-witness.json').read_bytes()
        require(receipt['files']['scheduler-witness.json']==sha(raw),'Scheduler witness receipt hash differs')
        rows=O.decode(raw);require(len(rows)==3 and {r['rank'] for r in rows}=={0,1,2},'Scheduler rank coverage differs')
        for row in rows:
            s=D(row['actualFirstStart']);expected=s+cooldown('vanish',row['rank'],phase)
            require(row['firstAmbushDamage']==400 and row['expectedNextDecision']==s+cooldown('vanish',row['rank'],'corrected'),'Scheduler literal setup differs')
            require(row['actualDecisions'] and all(x['TimeSeconds']==expected and x['SourceActorKey']=='rogue' and x['TargetActorKey']=='target' and x['IsInternal'] is True for x in row['actualDecisions']),'Actual private dispatch differs')
            require([x['TimeSeconds'] for x in row['actualCasts']]==[s,expected],'Actual successful starts differ')
        sources.append(receipt['sourceSha256']);result[phase]=dict(path=str(root.resolve()),receiptSha256=sha((root/'receipt.json').read_bytes()),witnessSha256=sha(raw),focusedSourceSha256=receipt['sourceSha256'],observations=rows)
    require(len(set(sources))==1,'Focused old/new source changed')
    require(sha((ROOT/'WarcraftSim.Tests/Consolidated/CP175_RogueElusivenessSourceCorrections.cs').read_bytes())==sources[0],'Final focused source differs from witnesses')
    result['scope']='Isolated real PriorityRotationExecutor cooldown scheduling, no injected boundary or reset. Public internal event counts stay significant under complete replay; they are not all independently reconstructed.'
    return result


def first_cause(request,old,new):
    H.causal_invariants(old,new)
    xs=O.decode(O.stable(old))['run']['timeline'];ys=O.decode(O.stable(new))['run']['timeline'];prefix=0
    while prefix<min(len(xs),len(ys)) and O.encode(xs[prefix])==O.encode(ys[prefix]):prefix+=1
    changes=M.differences(O.decode(O.stable(old)),O.decode(O.stable(new)))
    if not changes:return dict(kind='complete unchanged response',equalEventPrefix=prefix,changedPaths=[])
    require(prefix<min(len(xs),len(ys)),'Summary-only or truncated unexplained divergence')
    rank=request['talents']['ranks'].get('elusiveness',0);require(rank in [1,2],'Changed response without positive Elusiveness')
    a,b=xs[prefix],ys[prefix]
    require(b['type']==3 and b['abilityKey'] in ['vanish','blind'],'First public divergence is not corrected cooldown use')
    key=b['abilityKey'];starts=[e for e in xs[:prefix] if e['type']==3 and e['abilityKey']==key]
    require(starts,'No actual prior start establishes cooldown');start=starts[-1]['timeSeconds'];new_ready=start+cooldown(key,rank,'corrected');old_ready=start+cooldown(key,rank,'baseline')
    require(new_ready<=b['timeSeconds']<old_ready,'First change lacks old/new cooldown eligibility split')
    return dict(kind='cooldown-aware scheduling/readiness',equalEventPrefix=prefix,establishingStart=start,correctedReady=new_ready,baselineReady=old_ready,firstOldEvent=a,firstNewEvent=b,changedPaths=changes,processedEventCountBefore=old['run']['processedEventCount'],processedEventCountAfter=new['run']['processedEventCount'],schedulingLimit='First public divergence is shown. Earlier unexposed next-decision scheduling/count differences are retained, never masked or described as unchanged.')


def analyze(args):
    root=pathlib.Path(args.capture);out=pathlib.Path(args.out);out.mkdir(parents=True,exist_ok=False)
    rows=verified_inventory(root);summary=read(root/'capture-summary.json');proofs=[];failures=[]
    for name,row in rows.items():
        try:
            value=checked_body(root,row);require(row[summary['phase']+'Status'] is None or row['status']==row[summary['phase']+'Status'],'Unexpected status')
            proof=dict(name=name,status=row['status'])
            if row['status']==200 and row['path']==RUN and (row['trace'] or row['group']=='causal'):
                request=read(root/(name+'.request.json'))
                if row['group']=='binding':request=bound_request(request)
                proof['trace']=(trace_accounting if row['trace'] else broad_accounting)(request,value,summary['phase'])
                vanishes=proof['trace']['vanishes']
                if row.get('requireRepeated'):require(len(vanishes)>=3,'Repeated fixture lacks three natural Vanish starts')
                if row.get('requireBlind') and summary['phase']=='corrected':require(len([p for p in proof['trace']['priority'] if p['key']=='blind'])>=2,'Corrected utility fixture lacks repeated Blind')
                if row.get('requireLaterOpener') and summary['phase']=='corrected':
                    require(len(vanishes)>=2,'Corrected fixture lacks later Vanish')
                    require(any(x['vanish']==vanishes[1] and x['damage']>0 for x in proof['trace']['openerLinks']),'Later Vanish lacks actual damaging opener')
                write(out/(name+'.trace.json'),proof['trace'])
            elif row['group']=='binding':proof['diagnosticOnly']=True;proof['body']=value
            if row.get('replayOf'):exact_response(checked_body(root,rows[row['replayOf']]),value,row,summary['sourceCommit'],summary['sourceCommit'])
            proofs.append({k:v for k,v in proof.items() if k not in ['trace','body']})
        except Exception as error:failures.append(dict(name=name,error=str(error)))
    concurrent=[r for r in rows.values() if r.get('concurrent')]
    overlap=None
    if concurrent:
        overlap=min(r['completedMonotonicNs'] for r in concurrent)-max(r['startedMonotonicNs'] for r in concurrent)
        if overlap<=0:failures.append(dict(name='concurrency',error='No genuine request overlap'))
    if summary['mode']=='full':
        try:exact_response(read(root/'timing-None.response.json'),read(root/'timing-80.response.json'),dict(path=RUN),'same','same')
        except Exception as error:failures.append(dict(name='default-latency',error=str(error)))
    result=dict(passed=not failures,capturePath=str(root.resolve()),rawIndexSha256=sha((root/'index.json').read_bytes()),requests=len(rows),proofs=proofs,failures=failures,concurrentOverlapNanoseconds=overlap,analysisFiles={p:sha((ROOT/p).read_bytes()) for p in HELPERS},normalization=O.NORMALIZATION)
    write(out/'summary.json',result);print(O.encode({k:v for k,v in result.items() if k!='proofs'}));return 0 if result['passed'] else 1


def compare(args):
    before,after=pathlib.Path(args.baseline),pathlib.Path(args.corrected);out=pathlib.Path(args.out);out.mkdir(parents=True,exist_ok=False)
    rows,others=verified_inventory(before),verified_inventory(after);require(set(rows)==set(others),'Inventories differ')
    scheduling=scheduler_pair(args.baseline_scheduler,args.corrected_scheduler) if args.command=='compare' else None
    if scheduling is not None:write(out/'scheduler-proof.json',scheduling)
    proofs=[];failures=[]
    for name,row in rows.items():
        try:
            other=others[name]
            for key in ['path','group','trace','requestSha256','status']:require(O.encode(row[key])==O.encode(other[key]),'Capture receipt differs: '+key)
            old,new=checked_body(before,row),checked_body(after,other);proof=dict(name=name)
            request=None
            if row['requestSha256'] is not None:
                raw=(before/(name+'.request.json')).read_bytes();require(raw==(after/(name+'.request.json')).read_bytes(),'Raw input differs');request=O.decode(raw)
            if row['group']=='binding' and row['status']!=200:proof['diagnostic']=H.binding_comparison(old,new,row,other)
            elif args.command=='replay-equality' or row['group'] not in ['causal','binding']:
                proof['allowances']=exact_response(old,new,row,args.old_source_commit,args.source_commit)
            else:
                if row['group']=='binding':request=bound_request(request)
                oracle=trace_accounting if row['trace'] else broad_accounting
                proof.update(before=oracle(request,old,'baseline'),after=oracle(request,new,'corrected'),cause=first_cause(request,old,new))
            write(out/(name+'.proof.json'),proof);proofs.append(dict(name=name,comparison='diagnostic only' if 'diagnostic' in proof and not proof['diagnostic']['bodyEqualityClaim'] else 'causal accounting' if 'cause' in proof else 'complete response equality'))
        except Exception as error:failures.append(dict(name=name,error=str(error)))
    result=dict(passed=not failures,requests=len(rows),compared=len(proofs),proofs=proofs,failures=failures,normalization=O.NORMALIZATION)
    write(out/'summary.json',result);print(O.encode({k:v for k,v in result.items() if k!='proofs'}));return 0 if result['passed'] else 1


def main():
    parser=argparse.ArgumentParser(description=__doc__);commands=parser.add_subparsers(dest='command',required=True)
    cap=commands.add_parser('capture');cap.add_argument('--base',required=True);cap.add_argument('--out',required=True);cap.add_argument('--phase',choices=['baseline','corrected'],required=True);cap.add_argument('--mode',choices=['probe','full','smoke'],default='full');cap.add_argument('--baseline');cap.add_argument('--windows');cap.add_argument('--source-commit',required=True)
    ana=commands.add_parser('analyze');ana.add_argument('--capture',required=True);ana.add_argument('--out',required=True)
    win=commands.add_parser('freeze-windows');win.add_argument('--capture',required=True);win.add_argument('--out',required=True)
    for name in ['compare','replay-equality']:
        p=commands.add_parser(name);p.add_argument('--baseline',required=True);p.add_argument('--corrected',required=True);p.add_argument('--out',required=True);p.add_argument('--old-source-commit',required=True);p.add_argument('--source-commit',required=True)
        if name=='compare':p.add_argument('--baseline-scheduler',required=True);p.add_argument('--corrected-scheduler',required=True)
    args=parser.parse_args()
    return capture(args) if args.command=='capture' else analyze(args) if args.command=='analyze' else freeze_windows(args) if args.command=='freeze-windows' else compare(args)

if __name__=='__main__':raise SystemExit(main())
