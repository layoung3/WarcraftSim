'use strict';
// Pure observation/fixture contracts only. This does not launch a browser,
// start an API, or establish real-browser acceptance.
const assert=require('node:assert/strict'),fs=require('node:fs'),vm=require('node:vm');
const {installObserver,canonical,rejectionCases,gateNames,assertRejectedReportState}=require('./solo-dps-feral-browser-support.cjs');
const {build,environment}=require('./solo-dps-feral-fixture.cjs');
let checks=0;const test=(name,body)=>Promise.resolve().then(body).then(()=>{checks++;});
// Responsive source model: parse the real HTML hierarchy and CSS media rules.
// Only geometry is modeled. Visibility is derived from production rules and
// actual hidden ancestors; this is not a browser layout or pixel substitute.
function responsiveCaptureModel(width){
 const html=fs.readFileSync('WarcraftSim.Api/wwwroot/solo-dps.html','utf8'),css=fs.readFileSync('WarcraftSim.Api/wwwroot/solo-dps.css','utf8'),ui=fs.readFileSync('WarcraftSim.Api/wwwroot/solo-dps.js','utf8');
 const decode=s=>s.replaceAll('&amp;','&').replaceAll('&lt;','<').replaceAll('&gt;','>');
 const root={children:[],childNodes:[]},stack=[root],rules=[];let frames=0;
 const all=node=>node.children.flatMap(child=>[child,...all(child)]);
 const simple=(node,selector)=>{
  if(selector.includes(':not([id])')){if(node.id)return false;selector=selector.replace(':not([id])','');}
  const has=selector.match(/:has\(([^)]+)\)/);if(has){if(!node.querySelector(has[1]))return false;selector=selector.replace(has[0],'');}
  if(selector.includes(':'))return false;
  for(const [,name,value] of selector.matchAll(/\[([\w-]+)(?:=["']?([^\]"']+)["']?)?\]/g))if(name==='hidden'?!node.hidden:!(name in node.attributes)||value!==undefined&&node.attributes[name]!==value)return false;
  selector=selector.replace(/\[[^\]]+\]/g,'');
  const tag=selector.match(/^[\w-]+/);if(tag&&node.tagName!==tag[0].toUpperCase())return false;
  for(const [,symbol,name] of selector.matchAll(/([.#])([\w-]+)/g))if(symbol==='#'?node.id!==name:!node.className.split(/\s+/).includes(name))return false;
  return !!selector||node.hidden;
 };
 function matches(node,selector){
  const direct=selector.lastIndexOf('>');if(direct>=0)return simple(node,selector.slice(direct+1).trim())&&!!node.parentElement&&matches(node.parentElement,selector.slice(0,direct).trim());
  const parts=selector.trim().split(/\s+/);if(!simple(node,parts.pop()))return false;let ancestor=node.parentElement;
  while(parts.length){const part=parts.pop();while(ancestor&&!simple(ancestor,part))ancestor=ancestor.parentElement;if(!ancestor)return false;ancestor=ancestor.parentElement;}return true;
 }
 function query(node,selector){return all(node).filter(el=>selector.split(',').some(s=>matches(el,s.trim())));}
 function element(tag,attributes={}){
  const node={tagName:tag.toUpperCase(),attributes,id:attributes.id||'',className:attributes.class||'',hidden:'hidden' in attributes,children:[],childNodes:[],parentElement:null,value:attributes.value||'',checked:'checked' in attributes,dataset:Object.fromEntries(Object.entries(attributes).filter(([k])=>k.startsWith('data-')).map(([k,v])=>[k.slice(5),v]))};
  Object.defineProperties(node,{textContent:{get(){return this.childNodes.map(x=>x.textContent).join('');},set(value){this.children=[];this.childNodes=[{nodeType:3,textContent:String(value)}];}},options:{get(){return this.children;}},selectedOptions:{get(){return this.options.filter(x=>x.value===this.value);}}});
  node.append=(...children)=>{for(const child of children){child.parentElement=node;node.children.push(child);node.childNodes.push(child);}};
  node.querySelectorAll=selector=>query(node,selector);node.querySelector=selector=>query(node,selector)[0]||null;
  node.closest=selector=>{for(let n=node;n;n=n.parentElement)if(matches(n,selector))return n;return null;};
  node.contains=other=>other===node||all(node).includes(other);
  node.setAttribute=(key,value)=>node.attributes[key]=String(value);
  node.classList={toggle:(key,on)=>{const classes=new Set(node.className.split(/\s+/));if(on)classes.add(key);else classes.delete(key);node.className=[...classes].join(' ');}};
  node.getClientRects=()=>visible(node)?[node.getBoundingClientRect()]:[];
  node.getBoundingClientRect=()=>node.rect||{left:15,right:width-15,top:400+all(root).indexOf(node)*2,bottom:480+all(root).indexOf(node)*2,width:width-30,height:80};
  return node;
 }
 for(const match of html.replace(/<!--[\s\S]*?-->/g,'').matchAll(/<\/?([a-z][\w-]*)\b([^>]*)>|([^<]+)/gi)){
  if(match[3]){stack.at(-1).childNodes.push({nodeType:3,textContent:decode(match[3])});continue;}
  const [,tag,raw]=match;if(match[0].startsWith('</')){while(stack.length>1&&stack.at(-1).tagName!==tag.toUpperCase())stack.pop();if(stack.length>1)stack.pop();continue;}
  const attrs=Object.fromEntries([...raw.matchAll(/([\w:-]+)(?:="([^"]*)")?/g)].map(([,k,v])=>[k,v??''])),node=element(tag,attrs),parent=stack.at(-1);node.parentElement=parent===root?null:parent;parent.children.push(node);parent.childNodes.push(node);
  if(!['input','meta','link','img','br','hr','source','wbr'].includes(tag.toLowerCase()))stack.push(node);
 }
 function parseRules(source,media=[]){
  for(let pos=0;pos<source.length;){const open=source.indexOf('{',pos);if(open<0)break;const selector=source.slice(pos,open).trim();let end=open+1,depth=1;while(depth&&end<source.length){if(source[end]==='{')depth++;if(source[end]==='}')depth--;end++;}const body=source.slice(open+1,end-1);pos=end;
   if(selector.startsWith('@media')){parseRules(body,[...media,selector.slice(6).trim()]);continue;}if(selector.startsWith('@'))continue;
   rules.push({selectors:selector.split(',').map(x=>x.trim()),media,values:Object.fromEntries([...body.matchAll(/(?:^|;)\s*([\w-]+)\s*:\s*([^;]+)/g)].map(([,key,value])=>[key,value.replace(/!important/g,'').trim()]))});
  }
 }
 parseRules(css.replace(/\/\*[\s\S]*?\*\//g,''));
 const mediaMatches=condition=>{const max=condition.match(/max-width\s*:\s*(\d+)px/),min=condition.match(/min-width\s*:\s*(\d+)px/);return (max||min)&&(!max||width<=Number(max[1]))&&(!min||width>=Number(min[1]));};
 function style(node){const result={display:'block',visibility:'visible',position:'static'};for(const rule of rules)if(rule.media.every(mediaMatches)&&rule.selectors.some(sel=>matches(node,sel)))Object.assign(result,rule.values);return result;}
 function visible(node){for(let n=node;n;n=n.parentElement){if(n.hidden||style(n).display==='none'||['hidden','collapse'].includes(style(n).visibility))return false;if(n.parentElement?.tagName==='DETAILS'&&!('open' in n.parentElement.attributes)&&n.tagName!=='SUMMARY')return false;}return true;}
 const find=selector=>query(root,selector)[0]||null;
 const document={querySelector:find,querySelectorAll:selector=>query(root,selector),getElementById:id=>find('#'+id),elementFromPoint:(x,y)=>['classKey','spec','race','provisional'].map(id=>find('#'+id)).find(n=>{const r=n.getBoundingClientRect();return x>=r.left&&x<=r.right&&y>=r.top&&y<=r.bottom;})||find('.topbar')};
 const headerHeight=width<=540?284:180,rect=(left,top,w,h)=>({left,top,right:left+w,bottom:top+h,width:w,height:h});find('.topbar').rect=rect(0,0,width,headerHeight);
 for(const [index,id] of ['classKey','spec','race','provisional'].entries()){const n=find('#'+id),left=index===3?15:15+index*((width-30)/3),top=index===3?headerHeight-42:headerHeight-140,w=index===3?16:(width-42)/3;n.rect=rect(left,top,w,35);n.closest('label').rect=rect(left,top-18,index===3?width-30:w,53);}
 find('.level-chip').rect=rect(15,headerHeight-88,70,32);
 const selected=build();find('#classKey').value=selected.classKey;
 const specMap=vm.runInNewContext('('+ui.match(/const specMap=(\{[^\n]+\});/)[1]+')');for(const value of specMap.druid){const n=element('option',{value});n.textContent=value;find('#spec').append(n);}find('#spec').value=selected.specialization;
 const raceSource=fs.readFileSync('WarcraftSim.Api/Simulation/ForeverPlayableRaceCatalog.cs','utf8');for(const [,value,name] of raceSource.matchAll(/new\("([^"]+)","([^"]+)",/g)){const n=element('option',{value});n.textContent=name;find('#race').append(n);}find('#race').value=selected.race;find('#provisional').checked=selected.provisional;
 const character={classKey:'druid',specializationKey:'feral',race:'night-elf',level:60},report={build:selected,originalSimulationRequest:{feral:{character:{...character}}}};
 const context={document,window:{innerWidth:width,innerHeight:844,scrollY:0,matchMedia:condition=>({matches:!!mediaMatches(condition)}),__feralCompleted:[report],__feralHttp:[{sequence:7,response:{request:{character:{...character}},baseline:{race:'night-elf'}}}],__latestFeralPreview:7},getComputedStyle:style,requestAnimationFrame:resolve=>{frames++;resolve();}};
 vm.createContext(context);
 const navigate=ui.slice(ui.indexOf('function navigate(view){'),ui.indexOf('function updateIdentity(){'));vm.runInContext(navigate,context);
 // Actual Feral visibility setup and actual result/preparation visibility writes.
 context.$=id=>find('#'+id);context.isFeral=()=>true;context.isRogue=()=>false;context.setRogueScope=()=>{};context.isEnhancement=()=>false;
 vm.runInContext(ui.slice(ui.indexOf('function setRestrictedPanels(){'),ui.indexOf('\nconst beforeElementalImport='))+';setRestrictedPanels();',context);
 context.warnings=['Source warning unit model'];for(const pattern of [/\$\('statScope'\)\.hidden=false;/,/\$\('results'\)\.hidden=false;/,/\$\('resultWarnings'\)\.hidden=warnings.length===0;/]){const statement=ui.match(pattern)?.[0];assert.ok(statement,'Actual preparation/result visibility statement is present');vm.runInContext(statement,context);}
 const warning=element('div');warning.textContent='Source warning unit model';find('#resultWarningList').append(warning);
 return {context,find,visible,style,element,rules,html,css,frames:()=>frames};
}
(async()=>{
 await test('complete-envelope-guid-identity',()=>{
  const a='11111111-1111-1111-1111-111111111111',b='22222222-2222-2222-2222-222222222222',c='33333333-3333-3333-3333-333333333333',d='44444444-4444-4444-4444-444444444444';
  const first={preparation:{preview:{configuredRotation:{id:a},spellbookAttackPower:902.4},baseline:{source:a}},sim:{run:{timeline:[{abilityExecutionId:b,auraInstanceId:a,at:0},{abilityExecutionId:b,auraInstanceId:a,at:1}]}},timing:0};
  const equivalent={timing:0,sim:{run:{timeline:[{at:0,auraInstanceId:c,abilityExecutionId:d},{at:1,auraInstanceId:c,abilityExecutionId:d}]}},preparation:{baseline:{source:a},preview:{spellbookAttackPower:902.4,configuredRotation:{id:c}}}};
  assert.equal(canonical(first),canonical(equivalent));
  for(const mutate of [x=>x.sim.run.timeline.reverse(),x=>x.sim.run.timeline[1].abilityExecutionId=a,x=>x.preparation.preview.spellbookAttackPower=902.41,x=>x.timing=80,x=>delete x.preparation,x=>x.sim.run.timeline.push({abilityExecutionId:b,auraInstanceId:a,at:2}),x=>x.preparation.baseline.source=c,x=>x.preparation.preview.configuredRotation.name=c]){const changed=structuredClone(first);mutate(changed);assert.notEqual(canonical(changed),canonical(first));}
 });
 await test('structural-allowlist-decoys-and-marker-collisions',()=>{
  const a='11111111-1111-4111-8111-111111111111',b='22222222-2222-4222-8222-222222222222';
  const {c}=environment();
  const same=(left,right)=>{assert.equal(canonical(left),canonical(right));for(const value of [left,right])assert.equal(canonical(value),c.feralReplaySignature(value),'Browser and production replay policies agree');};
  const different=(left,right)=>{assert.notEqual(canonical(left),canonical(right));for(const value of [left,right])assert.equal(canonical(value),c.feralReplaySignature(value),'Browser and production replay policies agree');};
  for(const field of ['abilityExecutionId','autoAttackInstanceId','auraInstanceId','absorbInstanceId','forcedTargetInstanceId'])for(const nested of [false,true]){
   const shape=id=>{const run={run:{timeline:[{[field]:id},{[field]:id}]}};return nested?{sim:run}:run;};same(shape(a),shape(b));
   for(const literal of ['ENGINE-ID-0','ENGINE-ID-1','not-a-guid',null,0])different(shape(a),shape(literal));
  }
  for(const nested of [false,true]){const shape=id=>{const preview={preview:{configuredRotation:{id}}};return nested?{preparation:preview}:preview;};same(shape(a),shape(b));different(shape(a),shape('ENGINE-ID-0'));}
  for(const shape of [
   id=>({'preparation.preview.configuredRotation.id':id}),
   id=>({preview:{'configuredRotation.id':id}}),
   id=>({'sim.run.timeline.0.auraInstanceId':id}),
   id=>({sim:{'run.timeline':[{auraInstanceId:id}]}}),
   id=>({run:{timeline:{'0':{auraInstanceId:id}}}}),
   id=>({run:{timeline:[{nested:{auraInstanceId:id}}]}}),
   id=>({run:{timeline:[{periodicEffectInstanceId:id}]}}),
   id=>({run:{timeline:[{id}]}}),
   id=>({configuredRotation:{id}}),
   id=>({provenance:{preview:{configuredRotation:{id}}}}),
   id=>({source:{abilityExecutionId:id}})
  ])different(shape(a),shape(b));
 });
 await test('hold-actual-response-not-newer-request',async()=>{
  const listeners={},requests=[],responses=[];let now=0;
  const document={addEventListener:(type,fn)=>listeners[type]=fn};
  const window={fetch:(...args)=>new Promise(resolve=>requests.push({args,resolve}))};
  vm.runInNewContext('('+installObserver.toString()+')()',{window,document,performance:{now:()=>++now},JSON,Promise});
  const hold={url:'/api/forever/character/preview-feral',armed:true,prefix:false};window.__feralHold=hold;
  const old=window.fetch(hold.url,{body:'{"manualInputs":{"preTalentCatAttackPower":800}}'});
  assert.equal(hold.armed,false);assert.equal(hold.held,undefined);
  const newer=window.fetch(hold.url,{body:'{"manualInputs":{"preTalentCatAttackPower":900}}'});
  const response=value=>({status:200,clone:()=>({json:async()=>value})});
  responses[1]=response({manual:900});requests[1].resolve(responses[1]);assert.equal(await newer,responses[1]);
  responses[0]=response({manual:800});requests[0].resolve(responses[0]);await new Promise(setImmediate);assert.equal(hold.held,true);assert.equal(hold.released,undefined);
  assert.equal(window.__feralHttp[0].request.manualInputs.preTalentCatAttackPower,900);assert.equal(window.__feralHttp[1].request.manualInputs.preTalentCatAttackPower,800);
  assert.deepEqual(JSON.parse(JSON.stringify(window.__feralHttp.map(x=>x.response))),[{manual:900},{manual:800}]);
  assert.equal(window.__feralHttp[1].deliveredAt,undefined);hold.release();assert.equal(await old,responses[0]);assert.equal(hold.released,true);assert.ok(window.__feralHttp[1].deliveredAt>window.__feralHttp[0].deliveredAt);
 });
 await test('trusted-input-observation-does-not-dispatch',()=>{
  const listeners={},window={fetch:async()=>{}},document={addEventListener:(type,fn)=>listeners[type]=fn};
  vm.runInNewContext('('+installObserver.toString()+')()',{window,document,performance:{now:()=>1},JSON,Promise});
  listeners.input({isTrusted:true,target:{id:'feralPreTalentCatAttackPower',value:'',checked:false}});assert.equal(window.__feralInputs[0].trusted,true);assert.equal(window.__feralInputs[0].value,'');
  const detail={originalSimulationRequest:{feral:{manualInputs:{preTalentCatAttackPower:800}}}};listeners['warcraftsim:simulation-complete']({detail});detail.originalSimulationRequest.feral.manualInputs.preTalentCatAttackPower=0;assert.equal(window.__feralCompleted[0].originalSimulationRequest.feral.manualInputs.preTalentCatAttackPower,800);
 });
 await test('strict-rejection-fixtures-are-real-mutations',()=>{
  assert.equal(rejectionCases.length,40);assert.equal(new Set(rejectionCases.map(x=>x.name)).size,40);const original=build();const {c}=environment();
  for(const {name,mutate} of rejectionCases){const changed=structuredClone(original);mutate(changed);assert.notEqual(canonical(changed),canonical(original),name);if(!['unknown-rank','unaffordable-shift'].includes(name))assert.throws(()=>c.validateFeralImport(changed),undefined,name);}
 });
 await test('rejected-report-transaction-preserves-valid-or-empty-state',()=>{
  const empty={labelA:'valid A',labelB:'No report selected',hidden:true,rows:'',warnings:'',changes:'',useA:false,useB:false,lastComparison:null};
  const valid={...empty,labelB:'valid B',hidden:false,rows:'<tr><td>+15.2</td></tr>',warnings:'<div>Historical source</div>',changes:'<div>Weapon DPS changed</div>',lastComparison:{comparable:true,dpsDelta:15.2,a:{report:{id:'valid-A'}},b:{report:{id:'valid-B'}}}};
  for(const before of [empty,valid]){
   assertRejectedReportState(before,structuredClone(before),'Report import rejected: candidate invalid','valid/empty preservation');
   assert.throws(()=>assertRejectedReportState(before,structuredClone(before),'Encounter settings match','missing rejection'));
   for(const mutate of [x=>x.labelB='invalid candidate',x=>x.rows=before.rows?'':'<tr><td>+999</td></tr>',x=>x.hidden=!before.hidden,x=>x.warnings='changed',x=>x.changes='changed',x=>x.lastComparison={comparable:true,dpsDelta:999}]){
    const after=structuredClone(before);mutate(after);assert.throws(()=>assertRejectedReportState(before,after,'Report import rejected: candidate invalid','rejected mutation'));
   }
  }
 });
 await test('actual-rejected-import-settles-prior-readiness',async()=>{
  const source=fs.readFileSync('tools/solo-dps-feral-browser.test.cjs','utf8');
  const helper=source.slice(source.indexOf('async function rejectedImport('),source.indexOf('async function captureAll('));
  const readyHead=source.slice(source.indexOf('async function ready('),source.indexOf(" const receipt=await evaluate('window.__feralHttp.find",source.indexOf('async function ready(')))+'}';
  const plain=x=>JSON.parse(JSON.stringify(x)),actual=environment(),request=plain(actual.c.feralRequest(build()));
  const deferred=()=>{let resolve;const promise=new Promise(r=>resolve=r);return {promise,resolve};};
  function scenario({kind='queued',classKey='druid',spec='feral',invalid=false,invalidState='ready',server=false}={}){
   const feral=classKey==='druid'&&spec==='feral';
   const previous={sequence:41,url:'/api/forever/character/preview-feral',status:200,deliveredAt:1,request:plain(request)},draft=[{id:'race',value:'night-elf'},{id:'feralPreTalentCatAttackPower',value:invalid?'':'800'},{id:'seed',value:invalid?'0.5':'144001'}];
   const elements={classKey:{value:classKey},spec:{value:spec},race:{value:'night-elf'},status:{dataset:{buildState:invalid?invalidState:'ready'},textContent:'Previous status'},statStatus:{textContent:'Up to date'},file:{value:''},characterTitle:{textContent:spec+' '+classKey},characterSubtitle:{textContent:'Night Elf · Level 60'}};
   for(const id of ['run','save','exportBuild'])elements[id]={disabled:invalid};
   const browser={window:{__feralHttp:[previous],__latestFeralPreview:41},document:{querySelector:selector=>elements[selector.slice(1)],getElementById:id=>elements[id]}};vm.createContext(browser);
   const began=deferred(),released=deferred();let readyCalls=0,blockedCalls=0,finished=false;
   const evaluate=async expression=>expression==='rawDraft'?plain(draft):plain(vm.runInContext(expression,browser));
   const until=async expression=>assert.equal(vm.runInContext(expression,browser),true,'Actual import completion predicate');
   const readyGate=vm.runInNewContext(readyHead+';ready;',{until});
   const context={assert,canonical,draftExpression:'rawDraft',evaluate,sequence:async()=>41,observeImport:async()=>{},text:async selector=>elements[selector.slice(1)].textContent,
    selectFile:async()=>{browser.window.__importChanged=true;browser.window.__importStatus=true;elements.status.textContent='Feral talent is outside the current catalog';if(invalid)elements.status.dataset.buildState='input-required';if(feral&&!invalid&&kind==='queued'){elements.status.dataset.buildState='input-required';elements.statStatus.textContent='Updating…';for(const id of ['run','save','exportBuild'])elements[id].disabled=true;}
     if(server)browser.window.__feralHttp.push({sequence:42,url:'/api/forever/character/preview-feral',status:400,request:{manualInputs:{shapeshiftMana:999999}}});},until,
    ready:async after=>{readyCalls++;assert.equal(after,40,'A retained current receipt is allowed; a newer request is not forced');began.resolve();if(kind==='queued')await released.promise;await readyGate(after);return plain(browser.window.__feralHttp.find(x=>x.sequence===browser.window.__latestFeralPreview));},
    assertBlockedEditable:async()=>{blockedCalls++;for(const id of ['run','save','exportBuild'])assert.equal(elements[id].disabled,true);}
   };
   const result=vm.runInNewContext(helper+';rejectedImport("candidate.json","model",{server:'+server+'});',context).then(x=>{finished=true;return x;});
   function recover(mutate=()=>{}){const current=plain(previous);current.sequence=43;current.deliveredAt=2;mutate(current.request);browser.window.__feralHttp.push(current);browser.window.__latestFeralPreview=43;elements.status.dataset.buildState='ready';elements.statStatus.textContent='Up to date';for(const id of ['run','save','exportBuild'])elements[id].disabled=false;released.resolve();}
   return {result,began,readyGate,browser,elements,previous,draft,recover,get finished(){return finished;},get readyCalls(){return readyCalls;},get blockedCalls(){return blockedCalls;}};
  }
  // Reproduce the real failure boundary: file cleared and raw identity restored,
  // but the queued preparation has not completed. Export must remain blocked.
  const queued=scenario();await new Promise(setImmediate);assert.equal(queued.readyCalls,1,'Rejected valid draft must wait for preparation before returning');assert.equal(queued.finished,false);assert.equal(queued.elements.exportBuild.disabled,true);await assert.rejects(()=>queued.readyGate(40));
  queued.recover();assert.match(await queued.result,/outside the current catalog/);assert.equal(queued.readyCalls,1);
  // Local validation never invalidated the old ready receipt. No new response
  // is needed and requiring sequence>the import start would deadlock this case.
  const local=scenario({kind:'local'});await local.result;assert.equal(local.browser.window.__feralHttp.length,1);assert.equal(local.readyCalls,1);
  for(const invalidState of ['ready','input-required'])for(const server of [false,true]){const invalid=scenario({invalid:true,invalidState,server});await invalid.result;assert.equal(invalid.readyCalls,0);assert.equal(invalid.blockedCalls,1);assert.equal(invalid.elements.exportBuild.disabled,true);assert.equal(invalid.draft[1].value,'');assert.equal(invalid.draft[2].value,'0.5');}
  for(const [classKey,spec] of [['druid','balance'],['mage','arcane']]){const foreign=scenario({classKey,spec,server:true});await foreign.result;assert.equal(foreign.readyCalls,0);assert.equal(foreign.elements.status.dataset.buildState,'ready');}
  // Full exact request, not just race/AP, owns restored readiness.
  const wrong=scenario();await new Promise(setImmediate);assert.equal(wrong.readyCalls,1);const rejected=assert.rejects(()=>wrong.result,/exact prior request/);wrong.recover(r=>r.seed++);await rejected;
  // A ready-looking DOM cannot substitute a missing/failed/undelivered/stale
  // receipt or one whose race no longer owns the current selector.
  const gate=scenario({kind:'local'});await gate.result;
  for(const mutate of [r=>r.status=400,r=>delete r.deliveredAt,r=>r.sequence=39,r=>r.request.character.race='troll']){const original=plain(gate.previous);mutate(gate.previous);await assert.rejects(()=>gate.readyGate(40));Object.keys(gate.previous).forEach(k=>delete gate.previous[k]);Object.assign(gate.previous,original);}
 });
 await test('source-css-reproduces-old-mobile-capture-failure',async()=>{
  const {frameRegion}=require('./solo-dps-rogue-capture.cjs');
  const source=fs.readFileSync('tools/solo-dps-feral-browser.test.cjs','utf8'),originalSelector=source.match(/if\(!narrow\)\{await capture\('identity-stats-'\+size,'([^']+)'\);return;\}/)?.[1];
  assert.equal(originalSelector,'.character-card,#stats,#statScope','The unchanged desktop recipe is the old all-size recipe');
  for(const width of [390,790,791]){
   const model=responsiveCaptureModel(width),evaluate=expression=>vm.runInContext(expression,model.context);
   const hiddenRule=model.rules.find(r=>r.selectors.includes('.character-card')&&r.values.display==='none');
   assert.deepEqual(hiddenRule.media,['(max-width:790px)'],'Visibility comes from the actual source breakpoint');
   assert.equal(model.visible(model.find('.character-card')),width>790);assert.equal(model.visible(model.find('#stats')),true);assert.equal(model.visible(model.find('#statScope')),true);
   const old=()=>evaluate('('+frameRegion.toString()+')('+JSON.stringify(originalSelector)+')');
   if(width<=790)await assert.rejects(old,/Hidden capture region: \.character-card,#stats,#statScope/);else assert.ok((await old()).height>0);
   const current=await evaluate('('+frameRegion.toString()+')("#stats,#statScope")');assert.equal(current.viewportWidth,width);assert.ok(current.height>0);
   model.find('#statScope').hidden=true;await assert.rejects(()=>evaluate('('+frameRegion.toString()+')("#stats,#statScope")'),/Hidden capture region/,'The replacement never drops a hidden required stats/scope member');
  }
 });
 await test('actual-responsive-header-capture-and-negative-contracts',async()=>{
  const source=fs.readFileSync('tools/solo-dps-feral-browser.test.cjs','utf8'),page=source.slice(source.indexOf('async function mobileIdentityFrame(){'),source.indexOf('async function captureIdentity(')),helper=source.slice(source.indexOf('async function captureIdentity('),source.indexOf('async function captureAll('));
  assert.ok(page.startsWith('async function mobileIdentityFrame(){')&&helper.startsWith('async function captureIdentity('));
  assert.equal(/\.style\s*[.=]|\.hidden\s*=|\.scrollTo\s*\(|dispatchEvent\s*\(/.test(page+helper),false,'Identity framing does not reveal/reposition content or dispatch inputs');
  const plain=x=>JSON.parse(JSON.stringify(x));
  function runner(width=390,mutate=()=>{},screenshot=()=>{}){
   const model=responsiveCaptureModel(width);mutate(model);const captures=[],files=[],commands=[],coverage=[];
   const evaluate=async expression=>plain(await vm.runInContext(expression,model.context));
   const capture=async(name,selector)=>{captures.push({name,selector});return evaluate('('+require('./solo-dps-rogue-capture.cjs').frameRegion.toString()+')('+JSON.stringify(selector)+')');};
   const host={assert:{...assert,deepEqual:(a,b,...rest)=>assert.deepEqual(plain(a),plain(b),...rest)},Buffer,mobileIdentityFrame:vm.runInContext(page+';mobileIdentityFrame;',model.context),evaluate,capture,assertIdentity:async()=>{},stamp:'pure-model',screenshotDir:'/unit-model',path:require('node:path'),coverage,
    fs:{writeFileSync:(filename,bytes,options)=>{assert.deepEqual(plain(options),{flag:'wx'});files.push({filename,length:bytes.length});}},
    command:async(method,args)=>{commands.push({method,args:plain(args)});assert.equal(method,'Page.captureScreenshot');assert.deepEqual(plain(args),{format:'png',captureBeyondViewport:false});
     // Dimension-only test buffer, never saved as a screenshot or acceptance
     // artifact. Real CDP bytes and pixels remain mandatory in Actions.
     const bytes=Buffer.alloc(24);Buffer.from('89504e470d0a1a0a','hex').copy(bytes);bytes.writeUInt32BE(width,16);bytes.writeUInt32BE(844,20);screenshot(bytes,model);return {data:bytes.toString('base64')};}
   };
   const captureIdentity=vm.runInNewContext(helper+';captureIdentity;',host);
   return {model,captures,files,commands,coverage,capture:()=>captureIdentity(width<=790?'mobile':'desktop')};
  }
  for(const width of [390,790,791]){const r=runner(width);await r.capture();assert.equal(r.files.length,width<=790?1:0);assert.equal(r.commands.length,width<=790?1:0);assert.deepEqual(r.captures,[{name:'identity-stats-'+(width<=790?'mobile':'desktop'),selector:width<=790?'#stats,#statScope':'.character-card,#stats,#statScope'}]);
   if(width<=790){const receipt=r.coverage[0];assert.equal(receipt.gate,'mobile-identity-coverage');assert.deepEqual(plain(receipt.viewport),{width,height:844});assert.equal(receipt.screenshotWidth,width);assert.equal(receipt.screenshotHeight,844);assert.equal(receipt.cardHidden,true);assert.equal(receipt.level.text,'60');assert.equal(receipt.fields.provisional.checked,true);assert.ok(r.model.frames()>=6);}
  }
  const frame=async model=>vm.runInContext(page+';mobileIdentityFrame();',model.context);
  await assert.rejects(()=>frame(responsiveCaptureModel(791)),/narrow responsive layout/);
  for(const [name,mutate,pattern] of [
   ['missing control',m=>{const query=m.context.document.querySelector;m.context.document.querySelector=s=>s==='#race'?null:query(s);},/missing #race/],
   ['visible collapsed card',m=>m.find('.character-card').getClientRects=()=>[{}],/card must follow/],
   ['hidden header',m=>m.find('.topbar').hidden=true,/header is not fully visible/],
   ['nonsticky header',m=>{const style=m.context.getComputedStyle;m.context.getComputedStyle=n=>({...style(n),...(n===m.find('.topbar')?{position:'static'}:{})});},/header is not fully visible/],
   ['clipped header',m=>m.find('.topbar').rect.right=500,/header is not fully visible/],
   ['hidden race',m=>m.find('#race').hidden=true,/hidden or clipped identity control race/],
   ['clipped label',m=>m.find('#race').closest('label').rect.left=-10,/hidden or clipped identity control race/],
   ['occluded control',m=>m.context.document.elementFromPoint=()=>m.find('.brand'),/obscured identity control/],
   ['missing selected label',m=>m.find('#race').value='not-a-race',/missing selected identity label race/],
   ['clipped level',m=>m.find('.level-chip').rect.bottom=900,/level indicator is hidden or clipped/],
   ['unstable header',m=>{const rect=m.find('.topbar').rect;m.find('.topbar').getBoundingClientRect=()=>({...rect,height:rect.height+m.frames()/100});},/did not settle/],
   ['wrong report race',m=>m.context.window.__feralCompleted[0].build.race='troll',/completed report/],
   ['wrong original request level',m=>m.context.window.__feralCompleted[0].originalSimulationRequest.feral.character.level=59,/strictly equal/],
   ['wrong prepared race',m=>m.context.window.__feralHttp[0].response.request.character.race='troll',/strictly equal/],
   ['wrong baseline race',m=>m.context.window.__feralHttp[0].response.baseline.race='troll',/strictly equal/],
   ['wrong source label',m=>m.find('#classKey').closest('label').childNodes.find(n=>n.nodeType===3).textContent='Different label',/deep-equal/],
   ['hidden scope',m=>m.find('#statScope').hidden=true,/Hidden capture region/]
  ])await assert.rejects(()=>runner(390,mutate).capture(),pattern,name);
  for(const [name,mutate] of [['wrong width',b=>b.writeUInt32BE(391,16)],['wrong height',b=>b.writeUInt32BE(845,20)],['wrong signature',b=>b[0]=0],['post-shot identity change',(_b,m)=>m.find('#provisional').checked=false]])await assert.rejects(()=>runner(390,()=>{},mutate).capture(),undefined,name);
 });
 await test('actual-capture-all-source-backed-tab-route-audit',async()=>{
  const source=fs.readFileSync('tools/solo-dps-feral-browser.test.cjs','utf8'),flow=source.slice(source.indexOf('async function captureAll('),source.indexOf('async function main(')),plain=x=>JSON.parse(JSON.stringify(x));
  const expected=[['baseline-source','#feralSettings > h2,#feralSettings > p,#feralBaseline','encounter'],['manual-controls','#feralSettings .settings-grid','encounter'],['encounter-controls','#view-encounter > .settings-group:not([id])','encounter'],['talent-allocation','#talentSummary,#talentCoverage,#feralTalentScope','talents'],['talent-trees','#talents','talents'],['configured-priority','#feralFixedRotation','rotation'],['metrics','#metrics','results'],['abilities','#abilityCountNote,#abilities','results'],['warnings','#resultWarnings','results'],['actions-events','table:has(#reportEvents)','results'],['resources-events','table:has(#reportEvents)','results']];
  for(const width of [390,790,791]){
   const model=responsiveCaptureModel(width),seen=[],size=width<=790?'mobile':'desktop';let view='equipment',identityCalls=0;
   const rows=['actions','resources'].map(category=>({category,time:0,event:category,actor:'druid',target:'target',ability:'unit-model',detail:'Pure selector audit; no gameplay evidence'}));
   const host={assert:{...assert,deepEqual:(a,b,...rest)=>assert.deepEqual(plain(a),plain(b),...rest)},captureIdentity:async label=>{assert.equal(label,size);identityCalls++;},click:async selector=>{view=selector.match(/data-view=(\w+)/)[1];model.context.navigate(view);},
    readable:async selector=>{const nodes=model.context.document.querySelectorAll(selector);assert.ok(nodes.length);assert.ok(nodes.every(model.visible),'Every readable source node is visible on its actual active tab');},
    capture:async(name,selector)=>{const nodes=model.context.document.querySelectorAll(selector);assert.ok(nodes.length,'Actual HTML contains '+selector);assert.ok(nodes.every(model.visible),'Every required source region is visible: '+selector);seen.push([name.slice(0,-size.length-1),selector,view]);
     const measured=await vm.runInContext('('+require('./solo-dps-rogue-capture.cjs').frameRegion.toString()+')('+JSON.stringify(selector)+')',model.context);assert.equal(measured.viewportWidth,width);if(name==='abilities-'+size){assert.match(measured.text,/Damage events include misses, zero-damage results and periodic ticks/);assert.ok(measured.rows.some(row=>row.includes('Damage events')),'Combined note/table framing retains the ability headers');}},
    inspectEvents:()=>({rows}),evaluate:async expression=>plain(await vm.runInContext(expression,model.context)),
    setInput:async(selector,category)=>{assert.equal(selector,'#reportEventFilter');const body=model.find('#reportEvents');body.children=[];body.childNodes=[];for(const e of rows.filter(x=>x.category===category)){const tr=model.element('tr');for(const value of [e.time,e.event,e.actor,e.target,e.ability,e.detail]){const td=model.element('td');td.textContent=String(value);tr.append(td);}body.append(tr);}}
   };
   await vm.runInNewContext(flow+';captureAll('+JSON.stringify(size)+');',host);assert.equal(identityCalls,1);assert.deepEqual(seen,expected);
   const comparison=fs.readFileSync('WarcraftSim.Api/wwwroot/solo-dps-comparison.js','utf8'),show=comparison.match(/\$\("compareDetails"\)\.hidden=false;/)?.[0];assert.ok(show);vm.runInContext(show,model.context);assert.ok(model.visible(model.find('#compareDetails')));
   await vm.runInContext('('+require('./solo-dps-rogue-capture.cjs').frameRegion.toString()+')(".comparison-workspace")',model.context);
  }
  // Comparison captures are outside captureAll, but must remain present at both
  // viewport sizes and preserve the original strict region selection.
  for(const kind of ['compatible','incompatible'])for(const size of ['desktop','mobile'])assert.ok(source.includes("capture('"+kind+"-values-"+size+"','.comparison-workspace')"));
 });
 await test('every-planned-gate-is-asserted-once',()=>{
  const source=fs.readFileSync('tools/solo-dps-feral-browser.test.cjs','utf8');const marks=[...source.matchAll(/mark\('([^']+)'/g)].map(x=>x[1]);assert.equal(gateNames.length,34);assert.deepEqual(marks.sort(),[...gateNames].sort());assert.equal(new Set(marks).size,marks.length);
  assert.ok(source.includes('createBrowserClick'));assert.ok(source.includes('createRegionCapture'));assert.ok(source.includes('DOM.setFileInputFiles'));assert.ok(source.includes("viewport(390,844,true)"));assert.ok(source.includes("setInput('#feralPreTalentCatAttackPower','',false)"));
  assert.equal(/dispatchEvent\s*\(|\.click\s*\(|Fetch\.fulfillRequest|new Response\s*\(/.test(source),false,'No synthetic successful response/input or renderer injection');
  assert.ok(source.includes("holdResponse('/api/forever/talents/druid/feral/validate-itemized')"));
  const flow=source.slice(source.indexOf('async function main(){')),fixtureSelection=flow.indexOf('await selectFeralFixtureRace();'),manualEntry=flow.indexOf("await setInput('#feralPreTalentCatAttackPower',800)");
  assert.ok(fixtureSelection>=0&&fixtureSelection<manualEntry,'The executed workflow explicitly establishes Night Elf before its first AP800 preparation');
  assert.equal((flow.match(/await selectFeralFixtureRace\(\);/g)||[]).length,1,'Exactly one explicit initial numerical-fixture race selection');
 });
 console.log('Feral browser support pure contracts passed: '+checks+' groups; 34 planned browser gates and 40 rejection fixtures. No browser or API executed.');
})().catch(error=>{console.error(error);process.exitCode=1;});
