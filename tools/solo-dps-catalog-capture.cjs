'use strict';
const assert=require('node:assert/strict');
const {createRegionCapture}=require('./solo-dps-rogue-capture.cjs');
// Inspect the actual production CSS pseudo-element, never create replacement text.
function tooltipGeometry(selector){
 const e=document.querySelector(selector),r=e?.getBoundingClientRect();
 if(!e||!r.width||!r.height||!e.matches(':hover'))throw Error('Native hover required');
 const s=getComputedStyle(e,'::after'),owner=getComputedStyle(e),n=key=>Number.parseFloat(s[key]);
 if(s.display==='none'||s.visibility!=='visible'||s.content==='none'||Number(s.opacity)===0)throw Error('Tooltip not rendered');
 const width=n('width')+(s.boxSizing==='border-box'?0:n('paddingLeft')+n('paddingRight')+n('borderLeftWidth')+n('borderRightWidth'));
 const height=n('height')+(s.boxSizing==='border-box'?0:n('paddingTop')+n('paddingBottom')+n('borderTopWidth')+n('borderBottomWidth'));
 const left=r.left+Number.parseFloat(owner.borderLeftWidth)+n('left'),top=r.top+Number.parseFloat(owner.borderTopWidth)+n('top');
 const bounds={left,top,right:left+width,bottom:top+height,width,height};
 const headerBottom=document.querySelector('.topbar').getBoundingClientRect().bottom;
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
 const desktop=await region('cp175-catalog-selected-shared-elixirs-desktop',selectors.join(','));desktop.selectedTiles=selectedTiles;
 async function tooltip(label,selector){
  // Existing region helper supplies bounded stable positioning and receipts.
  await region(label+'-tile',selector);
  const point=await evaluate(`(async()=>{const e=document.querySelector(${JSON.stringify(selector)}),h=document.querySelector('.topbar');window.scrollBy({top:e.getBoundingClientRect().top-h.getBoundingClientRect().bottom-20,behavior:'instant'});await new Promise(r=>requestAnimationFrame(()=>requestAnimationFrame(r)));const b=e.getBoundingClientRect(),x=b.left+b.width/2,y=b.top+b.height/2;const hit=document.elementFromPoint(x,y);if(hit!==e&&!e.contains(hit))throw Error('Tooltip tile obscured');return {x,y};})()`);
  await command('Input.dispatchMouseEvent',{type:'mouseMoved',x:point.x,y:point.y});
  await evaluate('new Promise(r=>requestAnimationFrame(()=>requestAnimationFrame(r)))');
  const measure=()=>evaluate('('+tooltipGeometry.toString()+')('+JSON.stringify(selector)+')');
  const before=await measure();assert.equal(before.selected,'true');assert.match(before.text,/Independent:/);assert.match(before.text,/Shared elixir critical chance \(counted once/);
  for(const key of ['attack-critical-chance-percent','spell-critical-chance-percent'])assert.ok(before.text.includes(key)||before.text.toLowerCase().includes(key.replaceAll('-',' ')),'Both critical channels must be labeled');
  const shot=await command('Page.captureScreenshot',{format:'png',captureBeyondViewport:false});
  assert.deepEqual(await measure(),before,'Tooltip geometry/content must survive screenshot');
  const filename=label+'.png';await save(filename,shot.data);receipts.push({gate:'native-hover-tooltip',label,filename,...before,evidenceScope:'Native mouse hover in stated viewport; not touch-device acceptance or contribution JSON pixels'});
  await command('Input.dispatchMouseEvent',{type:'mouseMoved',x:1,y:1});
 }
 await tooltip('cp175-catalog-shared-tooltip-desktop',selectors[0]);
 await command('Emulation.setDeviceMetricsOverride',{width:390,height:844,deviceScaleFactor:1,mobile:true});
 try{await tooltip('cp175-catalog-shared-tooltip-mobile-viewport',selectors[0]);}
 finally{await command('Emulation.clearDeviceMetricsOverride');}
}
module.exports={tooltipGeometry,captureCatalogRegions};
