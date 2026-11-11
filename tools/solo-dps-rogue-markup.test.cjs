'use strict';
// Production HTML + production scope switching + unchanged strict capture helper.
// DOM ancestry/visibility and content are real source contracts; measured geometry
// is modeled. These are not browser execution or screenshot/pixel evidence.
const assert=require('node:assert/strict'),fs=require('node:fs'),vm=require('node:vm');
const {frameRegion,createRegionCapture}=require('./solo-dps-rogue-capture.cjs');
const expectedAssassination=[
 'Partial historical-provisional 38/0/0 starting allocation: 38 allocated, 13 unspent. Not an optimized or complete 51-point build, current-client certification, or complete Rogue coverage. Only twelve reconciled Assassination talents are selectable.',
 'Two daggers are required and fixed for this adapter. Base damage and swing times below are manual weapon budgets, not catalog items. Weapon, hit, crit, armor and most combat coefficients remain provisional.',
 'One level-63 target starts at 100% Health. Target maximum Health is separate from the player Health fallback. Physical non-bleed damage uses target armor; Nature damage is unmitigated. Positioning is an explicit assumption, not movement simulation.',
 'Supplied observed or historical-proxy naked player Health takes precedence over the manual fallback. The exact preview shows maximum Energy including Vigor once, separate starting Energy, and effective player Health. Source provenance is not server attestation.',
 'Each poison coating and its charges are explicit. None disables poison on that hand; weapon attacks still run. Instant Poison has no lingering poison aura; utility coatings apply existing provisional auras, not PvP, movement or enemy casting simulation. Expose Armor is opt-in. Imported explicit legacy tuning is preserved in exports and reports.',
 'Catalog gear and enchants, external modifiers, racial combat effects, companions, incoming attacks, utility actions, custom priorities, generic scripts, adds and batch statistics are unavailable. Partial mechanics and source limits remain visible in run warnings.'
];
function fixture(width,specialization){
 const html=fs.readFileSync('WarcraftSim.Api/wwwroot/solo-dps.html','utf8'),ui=fs.readFileSync('WarcraftSim.Api/wwwroot/solo-dps.js','utf8');
 const root={children:[],childNodes:[]},stack=[root];let scrollY=0;const height=width===390?844:1000,header=width===390?284:76;
 const all=n=>n.children.flatMap(x=>[x,...all(x)]);
 const decode=s=>s.replaceAll('&amp;','&').replaceAll('&lt;','<').replaceAll('&gt;','>').replaceAll('&quot;','"').replaceAll('&#39;',"'");
 function matches(n,selector){
  selector=selector.trim();const direct=selector.lastIndexOf('>');if(direct>=0)return matches(n,selector.slice(direct+1))&&!!n.parentElement&&matches(n.parentElement,selector.slice(0,direct));
  const parts=selector.split(/\s+/);if(parts.length>1){const last=parts.pop();if(!matches(n,last))return false;for(let p=n.parentElement;p;p=p.parentElement)if(matches(p,parts.join(' ')))return true;return false;}
  assert.ok(/^(?:[a-z][\w-]*)?(?:[.#][\w-]+)*$/i.test(selector),'Unsupported selector in bounded markup contract: '+selector);
  const tag=selector.match(/^[a-z][\w-]*/i);if(tag&&n.tagName!==tag[0].toUpperCase())return false;
  for(const [,kind,value]of selector.matchAll(/([.#])([\w-]+)/g))if(kind==='#'?n.id!==value:!n.className.split(/\s+/).includes(value))return false;
  return true;
 }
 function query(n,selector){return all(n).filter(x=>selector.split(',').some(s=>matches(x,s)));}
 function visible(n){for(let p=n;p;p=p.parentElement)if(p.hidden)return false;return true;}
 function element(tag,attrs={}){
  const n={tagName:tag.toUpperCase(),id:attrs.id||'',className:attrs.class||'',hidden:'hidden'in attrs,children:[],childNodes:[],parentElement:null,dataset:Object.fromEntries(Object.entries(attrs).filter(([key])=>key.startsWith('data-')).map(([key,value])=>[key.slice(5),value])),value:attrs.value||'',checked:'checked'in attrs};
  Object.defineProperty(n,'textContent',{get(){return this.childNodes.map(x=>x.textContent).join('');},set(text){this.children=[];this.childNodes=[{textContent:String(text)}];}});
  n.append=(...nodes)=>{for(const child of nodes){child.parentElement=n;n.children.push(child);n.childNodes.push(child);}};n.replaceChildren=(...nodes)=>{n.children=[];n.childNodes=[];n.append(...nodes);};
  n.querySelectorAll=s=>query(n,s);n.querySelector=s=>query(n,s)[0]||null;n.closest=s=>{for(let p=n;p;p=p.parentElement)if(matches(p,s))return p;return null;};
  n.setAttribute=()=>{};n.classList={toggle:(k,on)=>{const values=new Set(n.className.split(/\s+/));if(on)values.add(k);else values.delete(k);n.className=[...values].join(' ');}};
  n.getClientRects=()=>visible(n)?[n.getBoundingClientRect()]:[];n.getBoundingClientRect=()=>{const top=n.top??1000+all(root).indexOf(n)*3,bottom=n.bottom??top+100;return {left:15,right:width-15,top:top-scrollY,bottom:bottom-scrollY,width:width-30,height:bottom-top};};return n;
 }
 for(const match of html.replace(/<!--[\s\S]*?-->/g,'').matchAll(/<\/?([a-z][\w-]*)\b([^>]*)>|([^<]+)/gi)){
  if(match[3]){stack.at(-1).childNodes.push({textContent:decode(match[3])});continue;}
  const [,tag,raw]=match;if(match[0].startsWith('</')){while(stack.length>1&&stack.at(-1).tagName!==tag.toUpperCase())stack.pop();if(stack.length>1)stack.pop();continue;}
  const attrs=Object.fromEntries([...raw.matchAll(/([\w:-]+)(?:="([^"]*)")?/g)].map(([,k,v])=>[k,v??''])),node=element(tag,attrs),parent=stack.at(-1);node.parentElement=parent===root?null:parent;parent.children.push(node);parent.childNodes.push(node);
  if(!['input','meta','link','img','br','hr','source','wbr'].includes(tag.toLowerCase()))stack.push(node);
 }
 const find=id=>query(root,'#'+id)[0];const document={querySelectorAll:s=>query(root,s),querySelector:s=>query(root,s)[0]||null,createElement:tag=>element(tag)};
 document.querySelector('.topbar').getBoundingClientRect=()=>({bottom:header});
 find('classKey').value='rogue';find('spec').value=specialization;
 const context={document,$:find,isRogue:()=>true,isFeral:()=>false,isEnhancement:()=>false,isCombatRogue:()=>find('spec').value==='combat',talentCatalog:[],human:s=>s,
  window:{innerWidth:width,innerHeight:height,get scrollY(){return scrollY;},scrollTo:position=>{scrollY=Math.max(0,position.top);}},requestAnimationFrame:resolve=>resolve()};vm.createContext(context);
 const displayStart=ui.indexOf('function capabilityNotesForDisplay(');if(displayStart>=0)vm.runInContext(ui.slice(displayStart,ui.indexOf('function setRogueScope(){')),context);
 const scope=ui.slice(ui.indexOf('function setRogueScope(){'),ui.indexOf('const enhancementNumericKeys='));assert.ok(scope.startsWith('function setRogueScope(){'));
 vm.runInContext(ui.slice(ui.indexOf('function navigate(view){'),ui.indexOf('function updateIdentity(){'))+scope+ui.slice(ui.indexOf('function setRestrictedPanels(){'),ui.indexOf('const beforeElementalImport='))+';navigate("encounter");setRestrictedPanels();',context);
 for(const id of ['rogueAssassinationScope','rogueCombatScope']){const region=find(id),paragraphs=region.querySelectorAll('p');region.top=1000;region.bottom=1000+paragraphs.length*150;paragraphs.forEach((p,i)=>{p.top=1000+i*150;p.bottom=p.top+150;});}
 find('rogueSettingsTitle').top=950;find('rogueSettingsTitle').bottom=1000;
 const receipts=[],files=[];const evaluate=async s=>JSON.parse(JSON.stringify(await vm.runInContext(s,context)));
 const capture=createRegionCapture({evaluate,command:async(method,args)=>{assert.equal(method,'Page.captureScreenshot');assert.equal(args.captureBeyondViewport,false);return {data:'modeled-not-pixels'};},save:async name=>files.push(name),receipts});
 return {context,find,document,visible,capture,receipts,files,evaluate};
}
async function talentCoverageContract(){
 const ui=fs.readFileSync('WarcraftSim.Api/wwwroot/solo-dps.js','utf8'),record=require('./solo-dps-combat-comparison-fixture.json'),plain=x=>JSON.parse(JSON.stringify(x));
 const start=ui.indexOf('const previousSourceCoveragePreset=loadPreset;'),end=ui.indexOf('// Add actor keys',start);assert.ok(start>=0&&end>start);
 const wrapper=ui.slice(start,end),summary=ui.slice(ui.indexOf('function showTalentSummary(){'),ui.indexOf('async function checkBaseline(){'));
 const f=fixture(1440,'combat'),c=f.context,coverage={rows:[{classKey:'mage',tree:'arcane',engineDeclaredConnectedKeys:7,historicalOnlyKeys:9}]};
 Object.assign(c,{loadPreset:async()=>{},generation:1,request:{talents:{ranks:{}}},talentCatalog:[],api:async route=>{assert.equal(route,'/api/forever/talents/source-coverage');return coverage;},human:value=>String(value).replaceAll('-',' ').replace(/\b\w/g,x=>x.toUpperCase()),
  isCombatRogue:()=>f.find('classKey').value==='rogue'&&f.find('spec').value==='combat',isFeral:()=>f.find('classKey').value==='druid'&&f.find('spec').value==='feral',isEnhancement:()=>f.find('classKey').value==='shaman'&&f.find('spec').value==='enhancement'});
 vm.runInContext(wrapper+summary,c);
 const expectedCombat='Historical provisional partial Combat starting preset (0/31/0; 31 allocated, 20 unspent), eleven reconciled keys only; no automatic 51-point filler. Lightning Reflexes, Puncturing Wounds and Endurance inactive in fixed-sword Combat; Blade Flurry cleave inactive with one target.';
 const expectedAssassination='Historical provisional partial Assassination starting preset (38/0/0; 13 unspent), twelve reconciled keys only; other historical talents are unavailable.';
 const source='1cc5ae1e9abdbf941b7326d2d90421822dce31ba',rawCoverage=JSON.stringify(coverage);
 for(const points of [31,30,0]){
  f.find('classKey').value='rogue';f.find('spec').value='combat';c.request={talents:{ranks:points?plain(record[points].preparation.request.talents.ranks):{}}};c.talentCatalog=plain(record.catalog);const rawCatalog=JSON.stringify(c.talentCatalog),rawRequest=JSON.stringify(c.request);
  await c.loadPreset();c.showTalentSummary();vm.runInContext('navigate("talents");setRogueScope();',c);
  const text=f.find('talentCoverage').textContent;assert.ok(text.startsWith(expectedCombat),'Actual selected Combat source-coverage wrapper must label the Combat31/20 starting preset, never Assassination38/13');assert.ok(!/partial Assassination|38\/0\/0|13 unspent|twelve reconciled/.test(text));
  assert.ok(text.includes('Assassination: 0 historically validated runner keys, 0 historical-only visual keys | Combat: 11 historically validated runner keys, 0 historical-only visual keys | Subtlety: 0 historically validated runner keys, 0 historical-only visual keys'));
  assert.ok(text.endsWith('. Source '+source+'. Not current-client certification; rank tooltips and complete mechanical coverage remain unverified.'));
  assert.ok(f.find('talentSummary').textContent.startsWith(points+' rank-derived points · '+(51-points)+' remaining / 51.'),'Starting-preset caption is distinct from current legal allocation');assert.ok(f.visible(f.find('talentCoverage'))&&f.visible(f.find('rogueCombatTalentScope')));
  assert.match(f.find('rogueCombatTalentScope').textContent,/Endurance source 30\/60% differs from legacy 10\/20%, inaccessible without utility/);assert.equal(f.find('rogueCombatTalentLimitations').children.length,11);
  assert.equal(JSON.stringify(c.request),rawRequest);assert.equal(JSON.stringify(c.talentCatalog),rawCatalog);assert.equal(JSON.stringify(coverage),rawCoverage);
 }
 const preset=JSON.parse(fs.readFileSync('WarcraftSim.Api/wwwroot/itemized-presets/rogue-assassination.json','utf8'));
 f.find('spec').value='assassination';c.request={talents:{ranks:preset.ranks}};c.talentCatalog=Object.keys(preset.ranks).map(key=>({key,tree:'assassination',historicalValidationSourceStatus:'historical-provisional-not-current-client-certified',historicalValidationSourceCommit:source}));
 await c.loadPreset();c.showTalentSummary();vm.runInContext('setRogueScope();',c);const assassination=f.find('talentCoverage').textContent;
 assert.ok(assassination.startsWith(expectedAssassination));assert.ok(assassination.includes('Assassination: 12 historically validated runner keys'));assert.ok(!assassination.includes('partial Combat starting preset'));assert.ok(f.find('talentSummary').textContent.startsWith('38 rank-derived points · 13 remaining / 51.'));assert.ok(f.find('rogueCombatTalentScope').hidden);
 for(const spec of ['subtlety','Combat','unknown']){f.find('spec').value=spec;await c.loadPreset();assert.equal(f.find('talentCoverage').textContent,assassination,'Unsupported/unknown contexts are not newly rewritten or admitted');}
 const contexts=[['priest','shadow','partial Shadow starting preset (0/0/36; 15 unspent)'],['druid','feral','partial Feral starting preset (0/31/5; 36 allocated, 15 unspent), fourteen retained keys; Furor and Feral Swiftness inactive in fixed Cat'],['druid','balance','partial Balance starting preset (35/0/0; 16 unspent)'],['shaman','enhancement','partial Enhancement starting preset (0/36/0; 36 allocated, 15 unspent), fifteen selectable keys including Elemental Convection, Concussion and Reverberation. Optional 15/36/0 allocates all 51 points without filler; fully allocated does not mean fully modeled or optimized. Other historical talents remain unavailable'],['shaman','elemental','Elemental allocation']];
 for(const [cls,spec,label]of contexts){f.find('classKey').value=cls;f.find('spec').value=spec;c.talentCatalog=[];await c.loadPreset();assert.ok(f.find('talentCoverage').textContent.startsWith('Historical provisional '+label+'. '));}
 f.find('classKey').value='mage';f.find('spec').value='arcane';await c.loadPreset();assert.equal(f.find('talentCoverage').textContent,'Arcane: 7 runner-declared, 9 historical-only. Current 70707 rank tooltips are not yet verified in the repository.');assert.equal(JSON.stringify(coverage),rawCoverage);
 console.log('Actual source-coverage renderer passed: Combat31/20 starting caption with current31/30/zero summaries, eleven genuine catalog rows/source/inactive disclosures, exact Assassination38/13 preservation, other/unknown contexts and raw receipts unchanged.');
}
async function run(){
 await talentCoverageContract();
 const css=fs.readFileSync('WarcraftSim.Api/wwwroot/solo-dps.css','utf8');assert.match(css,/^#rogueCombatTalentLimitations li\{overflow-wrap:anywhere\}$/m,'Combat-only source-token wrapping preserves all text without clipping or truncation');
 const rogue=fs.readFileSync('tools/solo-dps-rogue-browser.test.cjs','utf8'),combat=fs.readFileSync('tools/solo-dps-combat-browser.test.cjs','utf8'),ui=fs.readFileSync('WarcraftSim.Api/wwwroot/solo-dps.js','utf8');
 const capabilitySource=fs.readFileSync('WarcraftSim.Api/Simulation/ForeverClassSimulationCapabilities.cs','utf8').split('new("rogue",')[1].split('new("priest",')[0];
 const strings=[...capabilitySource.matchAll(/"(?:[^"\\]|\\.)*"/g)].map(x=>JSON.parse(x[0])),notes=strings.slice(strings.findIndex(x=>x.startsWith('Assassination only:')));assert.equal(notes.length,6);
 const display=ui.match(/  \$\('classCapabilityNotes'\)\.textContent=[\s\S]*?;/)?.[0];assert.ok(display,'Actual capability display assignment');
 const expectedFirst=notes[0].replace('Assassination only:','Selected Assassination preset:').replace('Combat and Subtlety are unavailable.','Combat is available as a separate partial historical-provisional preset; Subtlety remains unavailable.');
 const feral=fs.readFileSync('tools/solo-dps-feral-browser.test.cjs','utf8');for(const browser of [rogue,combat,feral])assert.match(browser,/require\(['"]\.\/solo-dps-rogue-capture\.cjs['"]\)/,'All three consumers keep the same strict capture helper');assert.ok(!feral.includes('#rogueSettings'),'Feral captures do not depend on changed Rogue scope ancestry');
 const selected=rogue.match(/captureRegion\('rogue-source-notes-mobile','([^']+)'\)/);assert.ok(selected,'Actual Assassination mobile source capture must be present');const selector=selected[1];
 for(const width of [1440,390]){
  const f=fixture(width,'assassination');
  f.context.capability={classKey:'rogue',specializations:['assassination'],knownRestrictions:[...notes]};const raw=JSON.stringify(f.context.capability);vm.runInContext(display,f.context);assert.equal(f.find('classCapabilityNotes').textContent,[expectedFirst,...notes.slice(1)].join(' · '),'Selected-preset display retains all limits without claiming selectable Combat unavailable');assert.equal(JSON.stringify(f.context.capability),raw,'Legacy capability response remains byte-equivalent after display');
  const capabilitySelector=rogue.match(new RegExp("captureRegion\\('rogue-capability-notes-"+(width===390?'mobile':'desktop')+"','([^']+)'\\)"))?.[1];assert.equal(capabilitySelector,'#classCapabilityNotes');const capabilityElement=f.find('classCapabilityNotes');capabilityElement.top=400;capabilityElement.bottom=width===390?1100:700;assert.ok(f.visible(capabilityElement));const capabilityReceipt=await f.capture('production-capability-'+width,capabilitySelector);assert.equal(capabilityReceipt.tiles[0].text,[expectedFirst,...notes.slice(1)].join(' · '));assert.equal(capabilityReceipt.tiles.at(-1).to,capabilityElement.bottom);assert.ok(rogue.includes('#classCapabilityNotes,#rogueSettings p,#rogueSettings label'),'Actual native readability includes corrected capability note');
  const unknown=[notes[0]+' Extra limitation.','Unknown restriction'];assert.deepEqual(Array.from(f.context.capabilityNotesForDisplay({knownRestrictions:unknown})),unknown,'Only the exact known legacy note is projected');f.find('classKey').value='mage';assert.deepEqual(Array.from(f.context.capabilityNotesForDisplay(f.context.capability)),notes,'Other classes retain all raw notes');f.find('classKey').value='rogue';
  assert.deepEqual(f.document.querySelectorAll('#rogueAssassinationScope > p').map(p=>p.textContent),expectedAssassination,'Every original Assassination scope paragraph is unchanged');
  assert.equal(f.document.querySelectorAll('#rogueSettings > p').length,0,'Old direct-child selector is genuinely absent in actual production markup');
  await assert.rejects(()=>f.evaluate('('+frameRegion.toString()+')("#rogueSettings > p")'),/Missing capture region/,'Unchanged strict helper rejects stale selector');
  const scopeParagraphs=f.document.querySelectorAll(selector);assert.equal(scopeParagraphs.length,6,'Actual browser selector must capture all six Assassination paragraphs');assert.ok(scopeParagraphs.every(f.visible));
  const parent=scopeParagraphs.at(-1).parentElement,children=parent.children;parent.children=children.slice(0,-1);assert.throws(()=>assert.deepEqual(f.document.querySelectorAll(selector).map(n=>n.textContent),expectedAssassination),'Dropping a required paragraph must fail full-content guard');parent.children=children;
  const receipt=await f.capture('production-assassination-'+width,selector);assert.equal(receipt.tiles[0].text,expectedAssassination.join('\n'));assert.equal(receipt.tiles.at(-1).to,1900);assert.ok(receipt.verticalSegments>1);
  const fields=f.document.querySelectorAll('#rogueSettings input,#rogueSettings select');assert.equal(fields.length,15);assert.ok(fields.every(f.visible));
  for(const scripts of [rogue,combat])for(const common of ['#rogueSettings .settings-grid','#rogueSettings p,#rogueSettings label'])assert.ok(scripts.includes(common),'Both suites retain shared selector '+common);
  assert.equal(f.document.querySelectorAll('#rogueSettings .settings-grid').length,1);
  const readable=()=>f.document.querySelectorAll('#rogueSettings p,#rogueSettings label').filter(f.visible);
  assert.equal(readable().filter(n=>n.tagName==='P').length,6);
  assert.ok(f.find('rogueCombatScope').hidden);assert.ok(!f.find('rogueAssassinationScope').hidden);
  f.find('spec').value='combat';vm.runInContext('setRogueScope();'+display,f.context);assert.equal(f.find('classCapabilityNotes').textContent,notes.join(' · '),'Mapping is restricted to selected Assassination context');assert.equal(JSON.stringify(f.context.capability),raw);
  assert.ok(f.find('rogueAssassinationScope').hidden);assert.ok(!f.find('rogueCombatScope').hidden);assert.ok(fields.every(f.visible));
  assert.equal(readable().filter(n=>n.tagName==='P').length,8);assert.equal(f.document.querySelectorAll('#rogueCombatScope > p').length,8);
  await assert.rejects(()=>f.capture('hidden-assassination',selector),/Hidden capture region/,'Strict capture never filters away hidden scope');
  const combatSelector=combat.match(/capture\('scope-'\+size,'([^']+)'\)/)?.[1];assert.equal(combatSelector,'#rogueSettingsTitle,#rogueCombatScope');
  const assertMembers=()=>assert.deepEqual(f.document.querySelectorAll(combatSelector).map(n=>n.id),['rogueSettingsTitle','rogueCombatScope']);assertMembers();const title=f.find('rogueSettingsTitle');title.id='missing-title';assert.throws(assertMembers,'Missing union member must fail source selector guard');title.id='rogueSettingsTitle';title.hidden=true;await assert.rejects(()=>f.capture('hidden-required-title',combatSelector),/Hidden capture region/);title.hidden=false;
  const combatReceipt=await f.capture('production-combat-'+width,combatSelector);assert.equal(combatReceipt.tiles.at(-1).to,2200);assert.ok(combatReceipt.tiles[0].text.includes('Exactly eleven source-backed keys'));assert.ok(!combatReceipt.tiles[0].text.includes('38/0/0'));
  assert.ok(f.document.querySelectorAll('#rogueRotationEntries').length===1&&f.document.querySelectorAll('#rogueFixedRotation').length===1);vm.runInContext('navigate("rotation");',f.context);assert.ok(f.visible(f.find('rogueFixedRotation'))&&f.visible(f.find('rogueRotationEntries')));vm.runInContext('navigate("encounter");',f.context);
  f.find('spec').value='assassination';vm.runInContext('setRogueScope();',f.context);assert.ok(scopeParagraphs.every(f.visible));assert.equal(readable().filter(n=>n.tagName==='P').length,6);
 }
 console.log('Rogue actual-markup capture contracts passed: desktop/mobile, six exact Assassination paragraphs, eight Combat paragraphs, shared controls/readability selectors, scope switching and unchanged strict missing/hidden/full-coverage behavior. Geometry modeled; browser/pixels not run.');
}
if(require.main===module)run().catch(error=>{console.error(error);process.exitCode=1;});
module.exports={run};
