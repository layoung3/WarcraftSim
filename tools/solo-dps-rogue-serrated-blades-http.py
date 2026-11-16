#!/usr/bin/env python3
"""Immutable Serrated Blades HTTP evidence and exact offline arithmetic checks.

The caller owns API/build lifecycle. Capture and analysis are separate commands:
analysis failure never triggers recapture or overwrites original wire evidence.
Personal armor and Rupture composition is a provisional local convention.
"""
import argparse
import collections
import concurrent.futures
import copy
from decimal import Decimal as D, localcontext, ROUND_HALF_EVEN
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
ACCEPTED = '2cef14897fdb9c94390db4c577abec9a127296f3'
ID = '66666666-6666-4666-8666-666666666666'
PRODUCTION = ['WarcraftSim.Data/Forever/Rogue/ForeverRogueTalentBuild.cs', 'WarcraftSim.Api/Simulation/RogueCombatProcessor.cs', 'WarcraftSim.Api/Simulation/RogueSimRunner.cs']
HELPERS = ['tools/solo-dps-rogue-serrated-blades-http.py', 'tools/test_solo_dps_rogue_serrated_blades_http.py', 'tools/solo-dps-rogue-initiative-http.py', 'tools/test_solo_dps_rogue_initiative_http.py', 'tools/solo-dps-rogue-elusiveness-http.py', 'tools/test_solo_dps_rogue_elusiveness_http.py',
           'tools/solo-dps-rogue-dirty-deeds-http.py', 'tools/solo-dps-rogue-cutthroat-http.py',
           'tools/solo-dps-rogue-subtlety-http.py', 'tools/rogue-combat-historical-http.py']
exact_response, checked_body = H.exact_response, K.checked_body


S = importlib.util.spec_from_file_location('elusiveness_contract', ROOT/'tools/solo-dps-rogue-elusiveness-http.py')
E = importlib.util.module_from_spec(S); S.loader.exec_module(E)

KEY = 'serrated-blades'
SMOKE_NAMES = (
    'subtlety-rNone','subtlety-r0','subtlety-r1','subtlety-r2','subtlety-r3',
    'hemorrhage-rNone','hemorrhage-r0','hemorrhage-r3','compose-rNone','compose-r3',
    'subtlety-r-1','subtlety-r4','strict-assassination-r0-typed','strict-combat-r3-full',
    'strict-assassination-validate-itemized-r0','strict-combat-validate-itemized-r0',
    'timing-zero','repeat-zero')

def select_smoke(cases):
    by_name={case['name']:case for case in cases}
    require(len(by_name)==len(cases),'Duplicate fixture name')
    require(set(SMOKE_NAMES)<=set(by_name),'Missing required smoke fixture')
    return [by_name[name] for name in SMOKE_NAMES]


def controlled(defaults, rank, spec='subtlety'):
    r=K.controlled(defaults,None,spec)
    r.update(durationSeconds=40,bothWeaponsDaggers=False,mainHandWeaponType='sword',offHandWeaponType='dagger',targetArmor=100)
    r['talents']=dict(assassinationPointsSpent=0,combatPointsSpent=5,subtletyPointsSpent=21,ranks={} if rank is None else {KEY:rank})
    r['tuning'].update(armorDamageReductionConstant=91,hackAndSlashDaggerCritPercentPerRank=0,hackAndSlashFistCritPercentPerRank=0)
    return r

def fixtures(defaults, mode='full', windows=None):
    require(mode in ['full','smoke'] and windows is None,'Only reviewed full/smoke inventories are available')
    cases=[]
    def add(name,r=None,path=RUN,group='exact',baseline=200,corrected=None,trace=False,**kw):
        cases.append(dict(name=name,path=path,body=copy.deepcopy(r),group=group,baselineStatus=baseline,correctedStatus=baseline if corrected is None else corrected,trace=trace,**kw))
    def admission(name,r,path=RUN,trace=False,**kw):
        value=r['talents']['ranks'].get(KEY)
        add(name,r if path!=ROTATION else K.envelope(r),path,group='admission' if value is not None else 'exact',baseline=200 if value is None else 400,corrected=200 if value is None or value in [0,1,2,3] else 400,trace=trace,**kw)
    # Pairwise primary coverage: no Cartesian crossing of every input.
    for spec,latency in [('subtlety',0),('assassination',80),('combat',0)]:
        for rank in [None,0,1,2,3,-1,4]:
            r=controlled(defaults,rank,spec);r['inputLatencyMs']=latency
            admission(f'{spec}-r{rank}',r,trace=True)
    variants=[('zero-armor',lambda r:r.update(targetArmor=0)),
      ('below-cap',lambda r:r.update(targetArmor=299)),('at-cap',lambda r:r.update(targetArmor=300)),('above-cap',lambda r:r.update(targetArmor=301)),
      ('hemorrhage',lambda r:r['talents']['ranks'].update(hemorrhage=1)),
      ('expose',lambda r:r.update(useExposeArmor=True,targetArmor=200)),
      ('mace-main',lambda r:r.update(mainHandWeaponType='mace')),
      ('mace-off',lambda r:r.update(offHandWeaponType='mace')),
      ('compose',lambda r:r.update(mainHandWeaponType='mace',offHandWeaponType='mace',useExposeArmor=True,targetArmor=200)),
      ('crit',lambda r:r['tuning'].update(physicalCritPercent=100)),
      ('miss',lambda r:r['tuning'].update(mainHandHitPercent=0,offHandHitPercent=0)),
      ('dodge',lambda r:r.update(targetDodgePercent=100)),
      ('parry',lambda r:r.update(isBehindTarget=False,targetParryPercent=100)),
      ('zero-raw',lambda r:r['tuning'].update(mainHandWeaponDamage=0,offHandWeaponDamage=0)),
      ('finite',lambda r:r.update(targetMaximumHealth=82)),
      ('poison',lambda r:r.update(useInstantPoison=True,mainHandPoison='instant',offHandPoison='deadly',poisonChargesPerWeapon=100)),
      ('prepared',lambda r:K.prepared(r)),
      ('cleave',lambda r:r.update(specialization='combat',additionalTargets=1))]
    for label,mutate in variants:
        for rank in [None,0,3]:
            r=controlled(defaults,rank);mutate(r)
            if 'cap' in label:r['tuning']['armorDamageReductionConstant']=91
            if label=='cleave':r['character']['specializationKey']='combat';r['talents']['combatPointsSpent']=21;r['talents']['ranks']['blade-flurry']=1
            if label in ['mace-main','mace-off','compose']:
                r['talents']['ranks']['hack-and-slash']=5;r['tuning']['hackAndSlashMaceArmorIgnorePercentPerRank']=2
            if label in ['expose','compose']:r['tuning']['armorReductionPerExposeComboPoint']=25
            admission(f'{label}-r{rank}',r,trace=label not in ['finite','poison','prepared','cleave'])
            if label in ['finite','poison','prepared','cleave'] and rank==3:admission(f'repeat-{label}-r3',r,replayOf=f'{label}-r3')
    for spec in SPECS:
        r=copy.deepcopy(defaults[spec]);r.update(durationSeconds=40,captureTimeline=True);r['character']['id']=ID
        add('default-run-'+spec,r)
    for rank in [None,0,1,2,3,-1,4]:
        r=controlled(defaults,rank,'assassination');K.prepared(r)
        for path,label in [(CHARACTER,'character'),(ROTATION,'rotation')]:admission(f'prepared-{label}-r{rank}',r,path)
    for label,latency in [('zero',0),('eighty',80)]:
        r=controlled(defaults,3);r['inputLatencyMs']=latency
        admission('timing-'+label,r,trace=True)
        admission('repeat-'+label,r,trace=True,replayOf='timing-'+label)
        admission('concurrent-'+label,r,trace=True,concurrent=True,replayOf='timing-'+label)
    for spec in ['assassination','combat']:
        add('strict-catalog-'+spec,path=f'/api/forever/talents/rogue/{spec}/itemized-catalog')
        add('modeled-catalog-'+spec,path=f'/api/forever/talents/rogue/{spec}')
        p=read(ROOT/f'WarcraftSim.Api/wwwroot/itemized-presets/rogue-{spec}.json')
        r=copy.deepcopy(defaults[spec]);r.update(durationSeconds=20,useProvisionalLevel60Baseline=True)
        r['character'].update(id=ID,race='human')
        r['talents']=dict(assassinationPointsSpent=0,combatPointsSpent=0,subtletyPointsSpent=0,ranks=copy.deepcopy(p['ranks']))
        r['talents'][spec+'PointsSpent']=sum(p['ranks'].values())
        for rank in [0,1,2,3]:
            for owner in ['typed','full','both']:
                env=K.envelope(r)
                if owner in ['typed','both']:env['rogue']['talents']['ranks'][KEY]=rank
                if owner in ['full','both']:env['itemizedTalentRanks'][KEY]=rank
                add(f'strict-{spec}-r{rank}-{owner}',env,SHARED,baseline=400)
            probe=copy.deepcopy(r);probe['talents']['ranks'][KEY]=rank
            if spec=='combat':add(f'strict-preview-combat-r{rank}',probe,CHARACTER,baseline=400)
            add(f'strict-{spec}-validate-itemized-r{rank}',probe['talents'],f'/api/forever/talents/rogue/{spec}/validate-itemized',baseline=400)
    for label,value in [('bool',True),('fraction',D('1.5')),('string','invalid'),('numeric-string','1'),('null',None),('null-map',None)]:
        r=controlled(defaults,1)
        if label=='null-map':r['talents']['ranks']=None
        else:r['talents']['ranks'][KEY]=value
        add('binding-'+label,r,group='binding',baseline=None)
    for spelling in ['Serrated-Blades',' serrated-blades','serrated-blades ']:
        r=controlled(defaults,None);r['talents']['ranks'][spelling]=1
        add('key-'+spelling.replace(' ','_'),r,group='binding',baseline=None)
    require(len(cases)==len({c['name'] for c in cases}),'Duplicate fixture name')
    return select_smoke(cases) if mode=='smoke' else cases

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
    require(request['talents']['ranks']=={KEY:'1'},'Only frozen numeric-string 1 permits typed projection')
    result=copy.deepcopy(request);result['talents']['ranks'][KEY]=1;return result


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


def report_contract(args):
    """Actual immutable UI export readers, separate from exact HTTP arithmetic."""
    originals=[pathlib.Path(x).resolve() for x in args.report]
    catalogs={spec:pathlib.Path(args.catalog)/('modeled-catalog-'+spec+'.response.json') for spec in ['assassination','combat']}
    inputs=[ROOT/'WarcraftSim.Api/wwwroot/solo-dps-comparison.js',ROOT/'WarcraftSim.Api/wwwroot/solo-dps.js',ROOT/'tools/solo-dps-combat-contract.test.cjs',ROOT/'WarcraftSim.Api/wwwroot/solo-dps-assumptions.js']+originals+list(catalogs.values())
    before={str(p):sha(p.read_bytes()) for p in inputs}
    script=r'''
const fs=require('node:fs'),assert=require('node:assert/strict'),api=require('./WarcraftSim.Api/wwwroot/solo-dps-comparison.js');
const clone=x=>JSON.parse(JSON.stringify(x)),proof={originals:[],negative:[],build:[]};
const vm=require('node:vm'),h=require('./tools/solo-dps-combat-contract.test.cjs'),catalogRoot=process.argv[1];
const gate=h.part(' if(request&&data.talents){'," if(request&&data.encounter?.combatItems");
for(const file of process.argv.slice(2)) {
 const raw=fs.readFileSync(file,'utf8'),original=JSON.parse(raw),spec=original.build.specialization;
 assert.ok(['assassination','combat'].includes(spec));api.inspectReport(original);assert.equal(api.compareReports(original,original).comparable,true);
 assert.equal(JSON.stringify(original),JSON.stringify(JSON.parse(raw)));proof.originals.push({file,spec});
 const names=['build','full','typed'];if(spec==='combat')names.push('preparation','raw-preparation');
 for(const rank of [0,1,2,3])for(const owner of [...names,'all']) {
  const r=clone(original),maps={build:r.build.talents.ranks,full:r.originalSimulationRequest.itemizedTalentRanks,typed:r.originalSimulationRequest.rogue.talents.ranks};
  if(spec==='combat'){maps.preparation=r.combatPreparation.request.talents.ranks;maps['raw-preparation']=r.rawServerResponse.preparation.request.talents.ranks;}
  for(const k of owner==='all'?names:[owner])maps[k]['serrated-blades']=rank;
  const bytes=JSON.stringify(r);let error;try{api.inspectReport(r);}catch(e){error=e.message;}
  assert.ok(error);assert.match(error,spec==='assassination'?/unsupported talent key/i:/unsupported exact rank|ranks|owned request|preparation copy disagrees/i);assert.equal(JSON.stringify(r),bytes);
  proof.negative.push({file,spec,rank,owner,error});
 }
 const {c,elements}=h.environment();elements.spec.value=spec;c.talentCatalog=JSON.parse(fs.readFileSync(catalogRoot+'/modeled-catalog-'+spec+'.response.json','utf8')).talents;
 assert.ok(Array.isArray(c.talentCatalog)&&!c.talentCatalog.some(t=>t.key==='serrated-blades'));
 vm.runInContext(h.part('const talentTrees=','async function validateTalentSelection(){'),c);
 c.isFeral=()=>false;c.data=clone(original.build);c.request={talents:clone(c.data.talents)};c.validateRogueImport(c.data);vm.runInContext(gate,c);
 for(const rank of [0,1,2,3]) {
  c.data=clone(original.build);c.data.talents.ranks['serrated-blades']=rank;c.request={talents:clone(c.data.talents)};
  c.validateRogueImport(c.data); // Shape alone intentionally does not admit talents.
  assert.throws(()=>vm.runInContext(gate,c),/Imported build contains a talent outside the current class catalog\./);
  proof.build.push({file,spec,rank,gate:'actual catalog-driven applyConfig fragment'});
 }
 assert.equal(fs.readFileSync(file,'utf8'),raw);
}
assert.ok(proof.originals.some(x=>x.spec==='combat')&&proof.originals.some(x=>x.spec==='assassination'));
process.stdout.write(JSON.stringify(proof));
'''
    proof=O.decode(subprocess.check_output(['node','-e',script,str(pathlib.Path(args.catalog).resolve()),*map(str,originals)],cwd=ROOT))
    require(before=={str(p):sha(p.read_bytes()) for p in inputs},'Original/module changed during report checks')
    out=pathlib.Path(args.out);require(not out.exists(),'Refuse overwrite report evidence')
    result=dict(passed=True,scope='Current parsed report admission on actual saved UI exports; derived rejection inputs only. Not HTTP Decimal equality or fresh simulation.',inputs=before,proof=proof)
    write(out,result);print(O.encode(dict(passed=True,originals=len(proof['originals']),negative=len(proof['negative']))));return 0



def multiply(left,right):
    # The existing helper exposes Decimal add/divide. Multiplication uses the
    # same 96-bit, scale-28, half-even contract, without Python context rounding.
    with localcontext() as context:
        context.prec=100
        result=D(left)*D(right)
        scale=min(28,max(0,-D(left).as_tuple().exponent)+max(0,-D(right).as_tuple().exponent))
        for digits in range(scale,-1,-1):
            rounded=result.quantize(D(1).scaleb(-digits),rounding=ROUND_HALF_EVEN)
            if abs(rounded.scaleb(digits))<=79228162514264337593543950335:return rounded
    raise AssertionError('Decimal multiplication overflow')

def expected_damage(request,event,points,marked,rank,expose_points=0):
    t=request['tuning'];key=event['abilityKey']
    require(type(rank) is int and 0<=rank<=3,'Rank must be integer 0..3')
    require(request['character']['baseStats']['values']=={'attack-power':0},'Literal AP0 arithmetic scope')
    if request.get('level60Baseline'):require(request['level60Baseline']['observedUnbuffedMeleeAttackPower']==0,'Only literal synthetic observed AP0 is covered')
    mh=D(t['mainHandWeaponDamage']);oh=multiply(t['offHandWeaponDamage'],D('.5'))
    bases={'rogue-main-hand':mh,'rogue-off-hand':oh,'sinister-strike':M.dotnet_add(mh,t['sinisterStrikeBonusDamage']),
      'backstab':M.dotnet_add(multiply(mh,D('1.5')),t['backstabBonusDamage']),
      'hemorrhage':M.dotnet_add(mh,t['hemorrhageBonusDamage']),
      'ambush':M.dotnet_add(multiply(mh,D('2.5')),t['ambushBonusDamage']),
      'garrote':D(t['garroteTickDamage']), 'eviscerate':multiply(t['eviscerateDamagePerPoint'],points),
      'rupture':D(t['ruptureTickDamagePerPoint']), 'hack-and-slash-extra':oh if event['weaponHandKey']=='off-hand' else mh}
    require(key in bases,'Unmodeled damage ability '+str(key))
    raw=bases[key]
    if key=='rupture':
        # Production multiplies the roll factor first, then the base tick.
        factor=multiply(points,D('1.15') if marked else 1)
        factor=multiply(factor,[D(1),D('1.1'),D('1.2'),D('1.3')][rank])
        raw=multiply(raw,multiply(factor,2 if event['isCritical'] else 1))
    elif event['isCritical']:raw=multiply(raw,2)
    if all(k in request for k in ['targetDodgePercent','targetParryPercent','isBehindTarget']) and 'mainHandHitPercent' in t:
        hit=t['offHandHitPercent'] if event['weaponHandKey']=='off-hand' else t['mainHandHitPercent']
        require(hit in [0,100] and t['physicalCritPercent'] in [0,100],'Isolated oracle requires deterministic boundary rolls')
        want='miss' if hit==0 else 'dodge' if not event['isPeriodic'] and request['targetDodgePercent']==100 else 'parry' if not event['isPeriodic'] and not request['isBehindTarget'] and request['targetParryPercent']==100 else 'critical' if t['physicalCritPercent']==100 else 'hit'
        require(event['resultKey']==want,'Outcome differs from bound deterministic request')
    if event['resultKey'] in ['miss','dodge','parry']:return D(0),D(0),D(0),D(0)
    require(event['resultKey'] in ['hit','critical'],'Unknown damage result')
    require(event['isCritical']==(event['resultKey']=='critical'),'Critical flag disagrees')
    armor=max(D(0),M.dotnet_add(request['targetArmor'],-multiply(expose_points,t['armorReductionPerExposeComboPoint'])))
    hand=event['weaponHandKey'];hand_type=request['offHandWeaponType'] if hand=='off-hand' else request['mainHandWeaponType']
    if hand_type=='mace':armor=multiply(armor,M.dotnet_add(1,-M.dotnet_divide(min(100,multiply(request['talents']['ranks'].get('hack-and-slash',0),t['hackAndSlashMaceArmorIgnorePercentPerRank'])),100)))
    armor=multiply(armor,[D(1),D('.97'),D('.94'),D('.91')][rank])
    pct=D(0) if event['isPeriodic'] or key in ['garrote','rupture'] or request['targetArmor']==0 else min(D('.75'),M.dotnet_divide(armor,M.dotnet_add(armor,t['armorDamageReductionConstant'])))
    mitigated=multiply(raw,pct)
    return raw,M.dotnet_add(raw,-mitigated),mitigated,multiply(pct,100)

def causal_expected(request,before,source_rank=0,target_rank=0):
    """Reconstruct old/new events from requests and real ordered aura lifecycle.

    The model deliberately rejects conditional talents, multi-target/death cases,
    and prepared proxies with nonliteral stats rather than broadening a mask.
    Every leaf not explicitly reconstructed remains in the full comparison.
    Equal source/target ranks validate a standalone capture; different ranks
    reconstruct the complete expected positive response from an absent control.
    """
    require(request['additionalTargets']==0 and not request['enableBossAttacks'] and not request['useUtilityAbilities'],'Unmodeled causal encounter')
    require(set(request['talents']['ranks'])<={KEY,'hemorrhage','hack-and-slash'},'Unmodeled causal talent')
    require(request['character']['equipment']=={'items':[]} and request['character']['buffs']=={'selections':[]} and request['character']['talents']=={'selections':[]},'Unmodeled authored stat contribution')
    if not request.get('level60Baseline'):require(set(request['character']['baseStats']['values'])<={'attack-power'},'Unmodeled primary stats')
    require(not any(a['died'] for a in before['run']['summary']['actorSummaries'].values()),'Causal fixture must not kill')
    expected=copy.deepcopy(before);timeline=expected['run']['timeline'];points=collections.defaultdict(int);snap={};marks={};pending={};awarded=set();threats=collections.defaultdict(collections.deque);changes=[];damage_proofs=[];expose_points=0;expose_until=D(-1)
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
                    if key=='expose-armor':expose_points=points[spent_target];expose_until=time_s+30
                    points[spent_target]=0
            if kind==10 and key=='hemorrhage' and event['effectKey']=='hemorrhage-mark':
                mark=marks.get((target,source))
                if mark and mark[0]==event['auraInstanceId']:marks.pop((target,source))
            if kind==6:
                old_amount=event['amount'];new_amount=old_amount
                if source=='rogue':
                    require(source=='rogue' and target=='target','Unexpected affected actor/target')
                    mark=marks.get((target,source));marked=bool(mark and mark[1]<=time_s<mark[2]);cp=max(1,points[target]) if key=='eviscerate' else snap.get(target,1)
                    old_raw,old_expected,old_mitigated,old_pct=expected_damage(request,event,cp,marked,source_rank,expose_points if time_s<expose_until else 0)
                    require(event['mitigationPercent']==old_pct,'Wrong mitigation percent')
                    require((event['rawAmount'],old_amount,event['mitigatedAmount'])==(old_raw,old_expected,old_mitigated),f'Old literal oracle disagrees at event{i} {key}: {(event["rawAmount"],old_amount,event["mitigatedAmount"])} != {(old_raw,old_expected,old_mitigated)} CP{cp} mark{marked}')
                    new_raw,new_amount,new_mitigated,new_pct=expected_damage(request,event,cp,marked,target_rank,expose_points if time_s<expose_until else 0)
                    for field,value in [('rawAmount',new_raw),('amount',new_amount),('mitigatedAmount',new_mitigated),('mitigationPercent',new_pct)]:set_value(event,field,value,path)
                    if old_amount!=new_amount:
                        name={'rogue-main-hand':'rogue-main-hand','rogue-off-hand':'rogue-off-hand','sinister-strike':'Sinister Strike','backstab':'Backstab','hemorrhage':'Hemorrhage','ambush':'Ambush','garrote':'Garrote','eviscerate':'Eviscerate','rupture':'Rupture','hack-and-slash-extra':'Hack and Slash (extra attack)'}[key];boss='Level 63 Rogue training boss'
                        description=(f'{name} critically hit {boss} for {M.format_damage(new_amount)} damage.' if event['isCritical'] else f'{name} dealt {M.format_damage(new_amount)} damage to {boss}.')
                        old_description=(f'{name} critically hit {boss} for {M.format_damage(old_amount)} damage.' if event['isCritical'] else f'{name} dealt {M.format_damage(old_amount)} damage to {boss}.')
                        if key in ['rogue-main-hand','rogue-off-hand']:
                            # Auto attacks use their real generated ability names.
                            old_description=event['description']
                            require(M.format_damage(old_amount) in old_description,'Auto damage description lacks amount')
                            description=old_description.replace('for '+M.format_damage(old_amount)+' damage','for '+M.format_damage(new_amount)+' damage').replace('dealt '+M.format_damage(old_amount)+' damage','dealt '+M.format_damage(new_amount)+' damage')
                            require(description!=old_description,'Auto damage description not reconstructed')
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
                if e['type']==6 and e['sourceActorKey']=='rogue' and e['targetActorKey']=='target':totals[e['abilityKey']]=M.dotnet_add(totals[e['abilityKey']],e['amount'])
            return totals
        old_totals,new_totals=sums(before['run']['timeline']),sums(timeline)
        def total(events):
            result=D(0)
            for e in events:
                if e['type']==6 and e['sourceActorKey']=='rogue' and e['targetActorKey']=='target':result=M.dotnet_add(result,e['amount'])
            return result
        old_total,new_total=total(before['run']['timeline']),total(timeline)
        require(new_total<request['targetMaximumHealth'],'Corrected target would die')
        def reconcile(obj,field,old,new,path):
            require(obj[field]==old,'Old summary disagrees with event reconstruction: '+str(path+[field]))
            set_value(obj,field,new,path)
        for obj,path in [(expected,[]),(expected['run']['summary'],['run','summary']),(expected['run']['summary']['actorSummaries']['rogue'],['run','summary','actorSummaries','rogue'])]:
            reconcile(obj,'damageDone',old_total,new_total,path)
        reconcile(expected,'dps',M.dotnet_divide(old_total,request['durationSeconds']),M.dotnet_divide(new_total,request['durationSeconds']),[])
        summary=expected['run']['summary'];rogue=summary['actorSummaries']['rogue'];target=summary['actorSummaries']['target']
        reconcile(target,'damageTaken',old_total,new_total,['run','summary','actorSummaries','target'])
        reconcile(expected['run']['customMetrics'],'rogue-boss-threat',old_total,new_total,['run','customMetrics'])
        for obj,path in [(expected['damageByAbility'],['damageByAbility']),(summary['damageDoneByAbility'],['run','summary','damageDoneByAbility']),(rogue['damageDoneByAbility'],['run','summary','actorSummaries','rogue','damageDoneByAbility']),(target['damageTakenByAbility'],['run','summary','actorSummaries','target','damageTakenByAbility'])]:
            require(set(obj)==set(old_totals),'Damage ability keys changed')
            for key in old_totals:reconcile(obj,key,old_totals[key],new_totals[key],path)
        for obj,path in [(summary['abilities'],['run','summary','abilities']),(rogue['abilities'],['run','summary','actorSummaries','rogue','abilities'])]:
            for key in old_totals:reconcile(obj[key],'damageDone',old_totals[key],new_totals[key],path+[key])
    return expected,dict(changes=changes,damageOracles=damage_proofs,targetHealth=dict(before=D(request['targetMaximumHealth'])-old_total,after=D(request['targetMaximumHealth'])-new_total,exposure='No target-current-health field exists in this response; no-kill is independently checked. Rogue Health is unaffected and compared exactly.'))


def verify_rejection(row,value):
    name=row['name']
    if name.startswith('strict-preview-'):
        expected={'error':'Historical Combat requires a valid strict historical itemized allocation.'}
    elif 'validate-itemized' in row['path']:
        expected={'valid':False,'error':'Strict Rogue ranks require unique exact canonical reconciled keys.'}
    elif name.startswith('strict-') and row['path']==SHARED:
        expected={'error':'Strict historical talent maps require exact canonical reconciled keys; case aliases, whitespace and unknown keys are not accepted.'}
    else:return False
    require(row['status']==400 and O.encode(value)==O.encode(expected),'Exact strict rejection differs')
    return True

def verify_smoke_coverage(name,response,trace):
    """Require the named real effects, not merely a nonempty damage timeline."""
    if name not in ['subtlety-rNone','subtlety-r0','subtlety-r1','subtlety-r2','subtlety-r3','hemorrhage-rNone','hemorrhage-r0','hemorrhage-r3','compose-rNone','compose-r3']:return
    events=response['run']['timeline'];damage=[e for e in events if e['type']==6 and e['amount']>0]
    require(any(e['abilityKey']=='rupture' for e in damage),'Smoke lacks positive Rupture')
    for hand in ['main-hand','off-hand']:
        require(any(e['weaponHandKey']==hand for e in damage),'Smoke lacks positive '+hand)
    require(any(e['abilityKey'] in ['sinister-strike','hemorrhage','backstab'] for e in damage),'Smoke lacks positive physical special')
    if name.startswith('hemorrhage-'):
        require(any(p['ability']=='rupture' and p['liveOwnMark'] and p['newAmount']>0 for p in trace['damageOracles']),'Smoke lacks live owned marked Rupture')
    if name.startswith('compose-'):
        applications=[e['timeSeconds'] for e in events if e['type']==9 and e['abilityKey']=='expose-armor' and e['sourceActorKey']=='rogue' and e['targetActorKey']=='target']
        require(applications and any(e['weaponHandKey'] in ['main-hand','off-hand'] and any(t<=e['timeSeconds']<t+30 for t in applications) for e in damage),'Smoke lacks a post-Expose mace hit')

def verify_legacy_rank_rejection(name,status,value):
    require(name in ['subtlety-r-1','subtlety-r4'],'Unknown smoke invalid-rank fixture')
    rank=name.rsplit('-r',1)[1]
    require(status==400 and value=={'error':f'Unsupported or invalid Rogue talent: {KEY}={rank}.'},'Exact invalid-rank rejection differs')


def analyze(args):
    root=pathlib.Path(args.capture);out=pathlib.Path(args.out);out.mkdir(parents=True,exist_ok=False)
    rows=verified_inventory(root);summary=read(root/'capture-summary.json');failures=[];proofs=[]
    for name,row in rows.items():
        try:
            value=checked_body(root,row);proof={'name':name,'status':row['status']}
            expected=row[summary['phase']+'Status'];require(expected is None or row['status']==expected,'Unexpected status')
            if row['status']==400:proof['strictExactRejection']=verify_rejection(row,value)
            if name in ['subtlety-r-1','subtlety-r4']:verify_legacy_rank_rejection(name,row['status'],value)
            if row['group']=='binding':proof.update(classification='typed success' if row['status']==200 else 'binder or key validation diagnostic',diagnostic=value if row['status']!=200 else None)
            if row['status']==200 and row['path']==RUN and row['trace']:
                req=read(root/(name+'.request.json'));rank=req['talents']['ranks'].get(KEY,0)
                _,trace=causal_expected(req,value,rank,rank);write(out/(name+'.trace.json'),trace)
                proof['arithmeticEvents']=len(trace['damageOracles'])
                if summary['mode']=='smoke':verify_smoke_coverage(name,value,trace)
            if row.get('replayOf'):
                # Baseline positive requests reject: compare the exact rejection too.
                exact_response(checked_body(root,rows[row['replayOf']]),value,row,summary['sourceCommit'],summary['sourceCommit'])
            proofs.append(proof)
        except Exception as error:failures.append(dict(name=name,error=str(error)))
    if summary['phase']=='corrected':
        for name,row in rows.items():
            if row['path']!=RUN or not name.endswith('-r0'):continue
            try:
                absent=name[:-1]+'None';require(absent in rows,'Zero lacks absent pair')
                exact_response(checked_body(root,rows[absent]),checked_body(root,row),row,'same','same')
            except Exception as error:failures.append(dict(name=name,error=str(error)))
        # Every isolated positive rank is compared to a full independently rebuilt
        # absent response. No blanket masks of amounts, metadata or positive ranks.
        for name,row in rows.items():
            if not row.get('trace') or row['path']!=RUN or '-r' not in name:continue
            rank_text=name.rsplit('-r',1)[1]
            if rank_text not in ['1','2','3']:continue
            try:
                absent=name.rsplit('-r',1)[0]+'-rNone';req=read(root/(name+'.request.json'))
                expected,trace=causal_expected(req,checked_body(root,rows[absent]),0,int(rank_text))
                exact_response(expected,checked_body(root,row),row,'same','same')
                write(out/(name+'.paired-proof.json'),trace)
            except Exception as error:failures.append(dict(name=name+'-paired',error=str(error)))
    if summary['phase']=='corrected' and summary['mode']=='full':
        try:
            absent=read(root/'finite-rNone.response.json');positive=read(root/'finite-r3.response.json')
            old_death=absent['run']['summary']['actorSummaries']['target'];new_death=positive['run']['summary']['actorSummaries']['target']
            require(old_death['died'] and new_death['died'],'Finite fixture must kill')
            old_events=[e for e in absent['run']['timeline'] if e['type']==6];new_events=[e for e in positive['run']['timeline'] if e['type']==6]
            require(new_death['deathTimeSeconds']<=old_death['deathTimeSeconds'],'Higher damage killed later')
            require(old_death['deathTimeSeconds']!=new_death['deathTimeSeconds'] or len(old_events)!=len(new_events),'Finite fixture lacks observed causal consequence')
            write(out/'finite-consequence.json',dict(absentDeath=old_death['deathTimeSeconds'],rank3Death=new_death['deathTimeSeconds'],absentDamageEvents=len(old_events),rank3DamageEvents=len(new_events),limit='Death changes later scheduling and draw opportunities. Complete independent replay is required; no unchanged hidden-RNG claim.'))
        except Exception as error:failures.append(dict(name='finite-consequence',error=str(error)))
    concurrency=[r for r in rows.values() if r.get('concurrent')]
    overlap=None
    if summary['mode']=='full' or concurrency:
        require(concurrency,'Full capture lacks concurrent fixtures')
        overlap=min(r['completedMonotonicNs'] for r in concurrency)-max(r['startedMonotonicNs'] for r in concurrency)
        if overlap<=0:failures.append(dict(name='concurrency',error='Requests did not overlap'))
    result=dict(passed=not failures,requests=len(rows),proofs=proofs,failures=failures,concurrentOverlapNanoseconds=overlap,rawIndexSha256=sha((root/'index.json').read_bytes()),normalization=O.NORMALIZATION)
    write(out/'summary.json',result);print(O.encode({k:v for k,v in result.items() if k!='proofs'}));return 0 if result['passed'] else 1

def compare(args):
    before,after=pathlib.Path(args.baseline),pathlib.Path(args.corrected);out=pathlib.Path(args.out);out.mkdir(parents=True,exist_ok=False)
    old,new=verified_inventory(before),verified_inventory(after);require(set(old)==set(new),'Case inventory changed');proofs=[];failures=[]
    for name,row in old.items():
        try:
            other=new[name]
            for key in ['path','requestSha256','group','baselineStatus','correctedStatus']:require(row.get(key)==other.get(key),'Identity changed '+key)
            a,b=checked_body(before,row),checked_body(after,other)
            if row['group']=='binding':
                # Keep whole diagnostics, including request hash and source identity;
                # do not normalize arbitrary trace IDs to claim response equality.
                proof=dict(name=name,classification='binder diagnostics',beforeStatus=row['status'],afterStatus=other['status'],before=a,after=b)
            elif row['group']=='admission' and row['baselineStatus']!=row['correctedStatus']:
                require(row['status']==400 and other['status']==200,'Wrong intentional admission')
                req=read(before/(name+'.request.json'));rank=(req.get('rogue',req))['talents']['ranks'][KEY]
                require(a=={'error':f'Unsupported or invalid Rogue talent: {KEY}={rank}.'},'Old admission must reject exact key')
                proof=dict(name=name,classification='intentional legacy admission, not old numerical red')
            else:
                exact_response(a,b,row,args.old_source_commit,args.source_commit);proof=dict(name=name,classification='complete unchanged envelope')
            proofs.append(proof)
        except Exception as error:failures.append(dict(name=name,error=str(error)))
    result=dict(passed=not failures,requests=len(old),proofs=proofs,failures=failures,normalization=O.NORMALIZATION)
    write(out/'summary.json',result);print(O.encode({k:v for k,v in result.items() if k!='proofs'}));return 0 if result['passed'] else 1

def replay(args):
    """Full same-version equality; callable only after recorded owner shutdown."""
    before,after=pathlib.Path(args.baseline),pathlib.Path(args.corrected)
    owner=pathlib.Path(args.owner_receipt)
    require((owner/'exit.txt').read_text().strip()=='0' and (owner/'shutdown.txt').is_file(),'Successful owner shutdown receipt required')
    inputs=set(p for directory in [before,after] for p in directory.iterdir() if p.is_file())
    inputs.update([owner/'exit.txt',owner/'shutdown.txt'])
    inputs.update(ROOT/p for p in HELPERS)
    inputs.update(ROOT/f'WarcraftSim.Api/wwwroot/itemized-presets/rogue-{spec}.json' for spec in ['assassination','combat'])
    hashes={str(p.resolve()):sha(p.read_bytes()) for p in inputs}
    left,right=verified_inventory(before),verified_inventory(after)
    require(set(left)==set(right),'Replay inventory differs')
    for directory,stamp in [(before,args.old_source_commit),(after,args.source_commit)]:
        require(read(directory/'capture-summary.json')['sourceCommit']==stamp,'Replay source stamp differs')
    require(read(before/'capture-summary.json')['phase']==read(after/'capture-summary.json')['phase'],'Replay phase differs')
    out=pathlib.Path(args.out);out.mkdir(parents=True,exist_ok=False);proofs=[];failures=[]
    for name,row in left.items():
        try:
            other=right[name]
            for field in ['path','method','group','status','requestSha256','baselineStatus','correctedStatus','trace','replayOf','concurrent']:
                require(O.encode(row.get(field))==O.encode(other.get(field)),'Replay identity differs '+field)
            a,b=checked_body(before,row),checked_body(after,other)
            if row['requestSha256'] is not None:require((before/(name+'.request.json')).read_bytes()==(after/(name+'.request.json')).read_bytes(),'Replay raw request differs')
            proof=dict(name=name,status=row['status'],requestSha256=row['requestSha256'],beforeResponseSha256=row['responseSha256'],afterResponseSha256=other['responseSha256'])
            if row['group']=='binding' and row['status']!=200:
                proof.update(classification='binder/key diagnostic',diagnostic=H.binding_comparison(a,b,row,other))
            else:
                exact_response(a,b,row,args.old_source_commit,args.source_commit)
                proof.update(classification='complete response equality',completeEnvelopeEquality=True)
            proofs.append(proof)
        except Exception as error:failures.append(dict(name=name,error=str(error)))
    require(hashes=={str(p.resolve()):sha(p.read_bytes()) for p in inputs},'Replay input changed during analysis')
    result=dict(passed=not failures,requests=len(left),proofs=proofs,failures=failures,inputs=hashes,ownerReceipt=str(owner.resolve()),normalization=O.NORMALIZATION)
    write(out/'summary.json',result);print(O.encode({k:v for k,v in result.items() if k not in ['proofs','inputs']}));return 0 if result['passed'] else 1


def main():
    p=argparse.ArgumentParser(description=__doc__);commands=p.add_subparsers(dest='command',required=True)
    cap=commands.add_parser('capture');cap.add_argument('--base',required=True);cap.add_argument('--out',required=True);cap.add_argument('--phase',choices=['baseline','corrected'],required=True);cap.add_argument('--mode',choices=['full','smoke'],default='full');cap.add_argument('--baseline');cap.add_argument('--source-commit',required=True);cap.set_defaults(windows=None)
    a=commands.add_parser('analyze');a.add_argument('--capture',required=True);a.add_argument('--out',required=True)
    c=commands.add_parser('compare');c.add_argument('--baseline',required=True);c.add_argument('--corrected',required=True);c.add_argument('--out',required=True);c.add_argument('--old-source-commit',required=True);c.add_argument('--source-commit',required=True)
    replay_parser=commands.add_parser('replay-equality');replay_parser.add_argument('--baseline',required=True);replay_parser.add_argument('--corrected',required=True);replay_parser.add_argument('--out',required=True);replay_parser.add_argument('--old-source-commit',required=True);replay_parser.add_argument('--source-commit',required=True);replay_parser.add_argument('--owner-receipt',required=True)
    r=commands.add_parser('report-contract');r.add_argument('--report',action='append',required=True);r.add_argument('--catalog',required=True);r.add_argument('--out',required=True)
    args=p.parse_args();return {'capture':capture,'analyze':analyze,'compare':compare,'report-contract':report_contract,'replay-equality':replay}[args.command](args)

if __name__=='__main__':raise SystemExit(main())
