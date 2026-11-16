'use strict';
const assert=require('node:assert/strict');
const {createRegionCapture}=require('./solo-dps-rogue-capture.cjs');
// Inspect the actual production CSS pseudo-element, never create replacement text.
function tooltipGeometry(selector){
 const e=document.querySelector(selector),r=e?.getBoundingClientRect();
 window.__catalogTooltipGeometry={selector,found:!!e,ownerBounds:r?{left:r.left,top:r.top,width:r.width,height:r.height}:null,hovered:!!e?.matches(':hover'),viewport:{width:innerWidth,height:innerHeight}};
 if(!e||!r.width||!r.height||!e.matches(':hover'))throw Error('Native hover required');
 const s=getComputedStyle(e,'::after'),owner=getComputedStyle(e),n=key=>Number.parseFloat(s[key]);
 window.__catalogTooltipGeometry.pseudo={width:s.width,height:s.height,left:s.left,top:s.top,boxSizing:s.boxSizing,display:s.display,visibility:s.visibility,content:s.content};
 if(s.display==='none'||s.visibility!=='visible'||s.content==='none'||Number(s.opacity)===0)throw Error('Tooltip not rendered');
 const width=n('width')+(s.boxSizing==='border-box'?0:n('paddingLeft')+n('paddingRight')+n('borderLeftWidth')+n('borderRightWidth'));
 const height=n('height')+(s.boxSizing==='border-box'?0:n('paddingTop')+n('paddingBottom')+n('borderTopWidth')+n('borderBottomWidth'));
 const left=r.left+Number.parseFloat(owner.borderLeftWidth)+n('left'),top=r.top+Number.parseFloat(owner.borderTopWidth)+n('top');
 const bounds={left,top,right:left+width,bottom:top+height,width,height};
 const headerBottom=document.querySelector('.topbar').getBoundingClientRect().bottom;
 window.__catalogTooltipGeometry={selector,bounds,headerBottom,viewport:{width:innerWidth,height:innerHeight},ownerBounds:{left:r.left,top:r.top,width:r.width,height:r.height},pseudo:{width:s.width,height:s.height,left:s.left,top:s.top,boxSizing:s.boxSizing,display:s.display,visibility:s.visibility,content:s.content},text:e.dataset.tooltip,selected:e.getAttribute('aria-pressed')};
 if(!Object.values(bounds).every(Number.isFinite)||width<=0||height<=0||left<0||bounds.right>innerWidth||top<headerBottom+8||bounds.bottom>innerHeight-8)throw Error('Actual tooltip must fit below sticky header and inside viewport: '+JSON.stringify(bounds));
 if(document.querySelector('dialog[open]'))throw Error('Dialog may obscure tooltip');
 for(let p=e.parentElement;p&&p!==document.body;p=p.parentElement){const style=getComputedStyle(p);if(['hidden','clip','scroll','auto'].includes(style.overflowX)||['hidden','clip','scroll','auto'].includes(style.overflowY)){const b=p.getBoundingClientRect();if(left<b.left||bounds.right>b.right||top<b.top||bounds.bottom>b.bottom)throw Error('Ancestor clips tooltip');}}
 return {selector,bounds,headerBottom,viewport:{width:innerWidth,height:innerHeight},text:e.dataset.tooltip,content:s.content,selected:e.getAttribute('aria-pressed'),hovered:true};
}
async function captureCatalogRegions({evaluate,command,save,receipts}){
 const region=createRegionCapture({evaluate,command,save,receipts});
 const keys=['elixir-of-the-mongoose','elixir-of-the-grizzly','elixir-of-the-owl'];
 const selectors=keys.map(key=>'[data-modifier-key="'+key+'"]');
 const selectedTiles=await evaluate(`JSON.parse(JSON.stringify(${JSON.stringify(selectors)}.map(selector=>{const e=document.querySelector(selector);return {selector,selected:e?.getAttribute('aria-pressed'),name:e?.getAttribute('aria-label'),tooltip:e?.dataset.tooltip};})))`);
 assert.ok(selectedTiles.every(tile=>tile.selected==='true'&&tile.name&&tile.tooltip?.includes('counted once')),'All three actual shared-elixir tiles must be selected and labeled');
 const initial=await evaluate('({width:innerWidth,height:innerHeight,scrollY:window.scrollY})');
 async function tooltip(label,selector){
  // Existing region helper supplies bounded stable positioning and receipts.
  await region(label+'-tile',selector);
  const point=await evaluate(`(async()=>{const e=document.querySelector(${JSON.stringify(selector)}),h=document.querySelector('.topbar');window.scrollBy({top:e.getBoundingClientRect().top-h.getBoundingClientRect().bottom-20,behavior:'instant'});await new Promise(r=>requestAnimationFrame(()=>requestAnimationFrame(r)));const b=e.getBoundingClientRect(),x=b.left+b.width/2,y=b.top+b.height/2;const hit=document.elementFromPoint(x,y);if(hit!==e&&!e.contains(hit))throw Error('Tooltip tile obscured');return {x,y};})()`);
  await command('Input.dispatchMouseEvent',{type:'mouseMoved',x:point.x,y:point.y});
  await evaluate('new Promise(r=>requestAnimationFrame(()=>requestAnimationFrame(r)))');
  const measure=async()=>{try{return await evaluate('('+tooltipGeometry.toString()+')('+JSON.stringify(selector)+')');}catch(error){
   const failure={gate:'tooltip-geometry-failure',label,selector,error:String(error)};receipts.push(failure);
   try{failure.raw=await evaluate('window.__catalogTooltipGeometry||null');}catch(diagnosticError){failure.rawError=String(diagnosticError);}
   try{const shot=await command('Page.captureScreenshot',{format:'png',captureBeyondViewport:false});const filename=label+'-failure.png';await save(filename,shot.data);failure.filename=filename;}catch(diagnosticError){failure.captureError=String(diagnosticError);}
   throw error;
  }};
  const before=await measure();assert.equal(before.selected,'true');assert.match(before.text,/Independent:/);assert.match(before.text,/Shared elixir critical chance \(counted once/);
  for(const key of ['attack-critical-chance-percent','spell-critical-chance-percent'])assert.ok(before.text.includes(key)||before.text.toLowerCase().includes(key.replaceAll('-',' ')),'Both critical channels must be labeled');
  const shot=await command('Page.captureScreenshot',{format:'png',captureBeyondViewport:false});
  assert.deepEqual(await measure(),before,'Tooltip geometry/content must survive screenshot');
  const filename=label+'.png';await save(filename,shot.data);receipts.push({gate:'native-hover-tooltip',label,filename,...before,evidenceScope:'Native mouse hover in stated viewport; not touch-device acceptance or contribution JSON pixels'});
  await command('Input.dispatchMouseEvent',{type:'mouseMoved',x:1,y:1});
 }
 try{
  for(const viewport of [{label:'desktop',width:1440,height:1000,mobile:false},{label:'narrow',width:780,height:1000,mobile:false},{label:'mobile-viewport',width:390,height:844,mobile:true}]){
   await command('Emulation.setDeviceMetricsOverride',{width:viewport.width,height:viewport.height,deviceScaleFactor:1,mobile:viewport.mobile});
   await command('Input.dispatchMouseEvent',{type:'mouseMoved',x:1,y:1});
   const actual=await evaluate('({width:innerWidth,height:innerHeight})');assert.deepEqual(actual,{width:viewport.width,height:viewport.height});
   const selected=await region('cp175-catalog-selected-shared-elixirs-'+viewport.label,selectors.join(','));selected.selectedTiles=selectedTiles;
   for(let i=0;i<selectors.length;i++)await tooltip('cp175-catalog-shared-tooltip-'+viewport.label+'-'+keys[i],selectors[i]);
  }
 }finally{
  await command('Input.dispatchMouseEvent',{type:'mouseMoved',x:1,y:1});
  // This core harness enters without a metrics override. Clear restores its
  // original native viewport; verify rather than silently retain our last size.
  await command('Emulation.clearDeviceMetricsOverride');
  const restored=await evaluate('({width:innerWidth,height:innerHeight})');assert.deepEqual(restored,{width:initial.width,height:initial.height});
  await evaluate('window.scrollTo({top:'+initial.scrollY+',behavior:"instant"})');
 }
}
module.exports={tooltipGeometry,captureCatalogRegions};
