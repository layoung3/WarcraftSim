'use strict';
// Pure offline harness/normalizer contract. Never launches Chromium or an API.
const assert=require('node:assert/strict'),fs=require('node:fs'),vm=require('node:vm'),path=require('node:path');
const {spawnSync}=require('node:child_process');
const {canonical,installObserver,ranks,rejectionCases,gateNames,assertRejectedReportState,frameScrollable}=require('./solo-dps-combat-browser-support.cjs');
const A='11111111-1111-4111-8111-111111111111',B='22222222-2222-4222-8222-222222222222',C='33333333-3333-4333-8333-333333333333',D='44444444-4444-4444-8444-444444444444',ZERO='00000000-0000-0000-0000-000000000000',MARKER='00000000-0000-4000-8000-000000000000';
const fields=['abilityExecutionId','autoAttackInstanceId','auraInstanceId','absorbInstanceId','forcedTargetInstanceId'];
const result=(a=A,b=B)=>({run:{timeline:[{abilityExecutionId:a,auraInstanceId:b,amount:.125,timeSeconds:0},{abilityExecutionId:a,auraInstanceId:b,amount:0,timeSeconds:1}]}});
const vectors=[],same=(name,a,b)=>vectors.push({name,a,b,same:true}),different=(name,a,b)=>vectors.push({name,a,b,same:false});let checks=0;
for(const field of fields)for(const outer of [false,true]){const a={run:{timeline:[{[field]:A},{[field]:A}]}},b={run:{timeline:[{[field]:C},{[field]:C}]}};same('generated-'+field+'-'+outer,outer?{sim:a}:a,outer?{sim:b}:b);}
same('cross-field-identity',result(A,A),result(C,C));same('shared-envelope', {sim:result()}, {sim:result(C,D)});
let b=result();b.run.timeline[1].abilityExecutionId=C;different('identity-split',result(),b);different('identity-join',result(),result(A,A));
for(const name of ['source','provenance','request','preparation','preview','unknown','timeline'])different('unknown-wrapper-'+name,{[name]:result()},{[name]:result(C,D)});
for(const [name,f]of [
 ['nested-object',x=>({run:{timeline:[{nested:{auraInstanceId:x}}]}})],['nested-array',x=>({run:{timeline:[{nested:[{auraInstanceId:x}]}]}})],
 ['array-in-timeline',x=>({run:{timeline:[[{auraInstanceId:x}]]}})],['double-sim',x=>({sim:{sim:{run:{timeline:[{auraInstanceId:x}]}}}})],
 ['dotted-key',x=>({'sim.run.timeline.0.auraInstanceId':x})],['string-index',x=>({run:{timeline:{'0':{auraInstanceId:x}}}})],['bracket-key',x=>({run:{'timeline[]':{auraInstanceId:x}}})]
])different(name,f(A),f(C));
for(const field of ['id','executionId','periodicEffectInstanceId','sourceActorKey','sourceId'])different('unrecognized-'+field,{run:{timeline:[{[field]:A}]}},{run:{timeline:[{[field]:C}]}});
for(const value of ['','authored-id','engine-id-1',A+'x',' '+A,A.replaceAll('-',''),'{'+A+'}',ZERO])different('literal-'+value,{run:{timeline:[{auraInstanceId:A}]}},{run:{timeline:[{auraInstanceId:value}]}});
const typed=[{},...([null,'',ZERO,false,0,1,'0',{},[]]).map(auraInstanceId=>({auraInstanceId}))];for(let i=0;i<typed.length;i++)for(let j=i+1;j<typed.length;j++)different('typed-'+i+'-'+j,{run:{timeline:[typed[i]]}},{run:{timeline:[typed[j]]}});
different('bool-number',{amount:true},{amount:1});different('false-zero',{amount:false},{amount:0});different('guid-key',{[A]:0},{[C]:0});different('outside-guid',A,C);different('outside-array',[A,B],[C,D]);
b=result();b.run.timeline.reverse();different('array-order',result(),b);b=result();b.run.timeline[0].newAmount=1;different('new-field',result(),b);
let a=result();a.provenance={literal:MARKER};b=result(C,D);b.provenance={literal:MARKER};same('collision-safe-markers',a,b);assert.notEqual(JSON.parse(canonical(a)).run.timeline[0].abilityExecutionId,MARKER);
a=result();a.provenance={literal:A};b=result(C,B);b.provenance={literal:A};different('literal-anchor-join-split',a,b);
a=result();a[A]='literal-key';b=result(C,B);b[A]='literal-key';different('guid-key-anchor',a,b);
a=result();a.preparation={request:{character:{id:A}}};b=result(C,B);b.preparation={request:{character:{id:A}}};different('character-anchor',a,b);
same('key-order',{z:0,a:result()},{a:result(),z:0});different('null-missing',{a:null},{});different('empty-null',{a:''},{a:null});different('negative-zero-resource',{energy:1},{energy:0});different('preparation-no-drop',{preparation:{maximumEnergy:100}},{preparation:{maximumEnergy:99}});different('timing-no-drop',{inputTiming:{additionalInputLatencyMilliseconds:0}},{inputTiming:{additionalInputLatencyMilliseconds:80}});
const production=require('../WarcraftSim.Api/wwwroot/solo-dps-combat-identity.js');
for(const v of vectors){const left=canonical(v.a),right=canonical(v.b);assert.equal(production.signature(v.a),left,v.name+' production left');assert.equal(production.signature(v.b),right,v.name+' production right');assert.equal(left===right,v.same,v.name);checks++;}
// Independent exact-Decimal Python oracle checks every portable parsed fixture.
// Arbitrary decimal-token precision is purposefully a separate Python test.
const script="import importlib.util,json,pathlib,sys\np=pathlib.Path('tools/rogue-combat-historical-http.py');s=importlib.util.spec_from_file_location('o',p);o=importlib.util.module_from_spec(s);s.loader.exec_module(o)\nv=o.decode(sys.stdin.read());print(json.dumps([o.stable(x['a'])==o.stable(x['b']) for x in v]))\n";
const oracle=spawnSync('python3',['-c',script],{input:JSON.stringify(vectors),encoding:'utf8'});assert.equal(oracle.status,0,oracle.stderr);assert.deepEqual(JSON.parse(oracle.stdout),vectors.map(v=>v.same));checks++;
for(const number of [NaN,Infinity,-Infinity])assert.throws(()=>canonical({number}),/Non-finite/);checks++;
const untouched=result();const before=structuredClone(untouched);canonical(untouched);assert.deepEqual(untouched,before);checks++;
const prior={labelA:'a',labelB:'b',rows:'123',lastComparison:{comparable:true,dpsDelta:10}};assertRejectedReportState(prior,structuredClone(prior),'Report import rejected: missing receipt','fixture');assert.throws(()=>assertRejectedReportState(prior,{...prior,labelB:'changed'},'Report import rejected: missing receipt','fixture'));checks++;
assert.equal(Object.keys(ranks).length,11);assert.equal(Object.values(ranks).reduce((a,b)=>a+b,0),31);assert.equal(ranks.precision,3);assert.equal(new Set(rejectionCases.map(x=>x.name)).size,rejectionCases.length);assert.equal(new Set(gateNames).size,gateNames.length);checks++;
(async()=>{
 // Real transport semantics modeled offline: hold is claimed on initiation,
 // delivery waits, actual status/body is retained, latest request wins observer.
 const listeners={},responses=[];let clock=0;
 const context={window:{fetch:async(...args)=>{const response={status:args[0].includes('invalid')?400:200,clone(){return {json:async()=>({requestUrl:args[0]})};}};responses.push(response);return response;}},document:{addEventListener:(name,fn)=>listeners[name]=fn},performance:{now:()=>++clock},JSON};vm.createContext(context);vm.runInContext('('+installObserver.toString()+')()',context);
 context.window.__combatHold={url:'/api/forever/character/preview-rogue',armed:true};let delivered=false;const held=context.window.fetch('/api/forever/character/preview-rogue',{body:'{"seed":1}'}).then(x=>{delivered=true;return x;});
 await new Promise(setImmediate);assert.equal(delivered,false);assert.equal(context.window.__combatHold.held,true);assert.equal(context.window.__combatHttp[0].status,200);assert.equal(context.window.__combatHttp[0].deliveredAt,undefined);
 const current=await context.window.fetch('/api/forever/character/preview-rogue',{body:'{"seed":2}'});assert.equal(context.window.__latestCombatPreview,2);assert.equal(current,responses[1]);context.window.__combatHold.release();assert.equal(await held,responses[0]);assert.equal(delivered,true);assert.equal(context.window.__latestCombatPreview,2);checks++;
 context.window.__combatHold={url:'/invalid',armed:true};const failure=context.window.fetch('/invalid');await new Promise(setImmediate);assert.equal(context.window.__combatHttp.at(-1).status,400);context.window.__combatHold.release();assert.equal((await failure).status,400);checks++;
 listeners['warcraftsim:simulation-complete']({detail:{actual:true}});assert.equal(context.window.__combatCompleted[0].actual,true);checks++;
 // Nested priority capture rejects hidden/overflowing/obscured content and
 // measures real scroll offsets rather than calling an unearned complete flag.
 let hidden=false,left=10,top=150,width=370,height=350,total=1800,scroll=0;
 const pre={textContent:'All eight configured actions',getClientRects:()=>hidden?[]:[1],getBoundingClientRect:()=>({left,right:left+width,top,bottom:top+height}),clientHeight:height,scrollHeight:total,clientWidth:width,scrollWidth:width,scrollLeft:0,scrollIntoView(){top=150;},get scrollTop(){return scroll;},set scrollTop(value){scroll=Math.min(total-height,Math.max(0,value));}};
 const frameContext={document:{querySelector:selector=>selector==='.topbar'?{getBoundingClientRect:()=>({bottom:120})}:pre},window:{scrollBy:(x,y)=>{top-=y;}},innerWidth:390,innerHeight:844,requestAnimationFrame:resolve=>resolve()};vm.createContext(frameContext);
 const frame=value=>vm.runInContext('('+frameScrollable.toString()+')("#priority",'+JSON.stringify(value)+')',frameContext);
 assert.equal((await frame(0)).scrollTop,0);assert.equal((await frame(9999)).scrollTop,1450);checks++;
 hidden=true;await assert.rejects(()=>frame(0),/hidden/);hidden=false;checks++;
 left=-3;await assert.rejects(()=>frame(0),/clipped/);left=10;checks++;
 pre.scrollWidth=800;await assert.rejects(()=>frame(0),/clipped/);pre.scrollWidth=width;checks++;
 const browser=fs.readFileSync(path.join(__dirname,'solo-dps-combat-browser.test.cjs'),'utf8');assert.ok(!browser.includes("dispatchEvent(new CustomEvent('warcraftsim:simulation-complete'"));assert.ok(!browser.includes('.click()'));assert.ok(browser.includes('createBrowserClick'));assert.ok(browser.includes('DOM.setFileInputFiles'));assert.ok(browser.includes('Input.dispatchKeyEvent'));assert.ok(browser.includes('Promise.race([main(),deadline])'));assert.ok(browser.includes('capturePending:true'));assert.ok(browser.includes('capturePriorityContents(size)'));for(const gate of gateNames)assert.ok(browser.includes("mark('"+gate+"'"),'Missing named browser gate '+gate);checks++;
 // Initial red acceptance fixture: the actual document must expose Combat
 // selectors. This is source reachability only, never a pixel/browser claim.
 const html=fs.readFileSync('WarcraftSim.Api/wwwroot/solo-dps.html','utf8');for(const id of ['rogueSettingsTitle','rogueCombatScope','rogueAssassinationScope','rogueCombatTalentScope','rogueCombatTalentLimitations','roguePriorityTitle'])assert.ok(html.includes('id="'+id+'"'),'Missing production Combat selector '+id);checks++;
 console.log('Combat browser support passed: '+checks+' offline checks; '+vectors.length+' cases agree with exact-Decimal Python oracle. Browser NOT RUN.');
})().catch(error=>{console.error(error);process.exitCode=1;});
