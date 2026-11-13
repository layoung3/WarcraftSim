'use strict';
const assert=require('node:assert/strict');

// Plan finite overlapping tiles from measured CSS pixels, not assumed header
// heights. The final scroll can clamp at the document edge without hiding rows.
function segmentOffsets(length,available,overlap=32){
 assert.ok(Number.isFinite(length)&&length>0&&Number.isFinite(available)&&available>overlap,'Capture requires positive measured geometry');
 const values=[0];
 while(values.at(-1)+available<length){
  assert.ok(values.length<16,'Capture region exceeds bounded sixteen-segment budget');
  values.push(Math.min(values.at(-1)+available-overlap,length-available));
 }
 return values;
}

// This exact function runs in the page and in the local framing contract tests.
// Reading and scrolling only: no style changes, hidden-element reveals, or
// replacement content. Stable rendering is awaited via animation frames.
async function frameRegion(selector,tile=null){
 const selected=[...document.querySelectorAll(selector)];
 if(!selected.length)throw Error('Missing capture region: '+selector);
 const elements=selected.map(element=>element.id==='abilities'?element.closest('table'):element);
 if(elements.some(element=>!element||!element.getClientRects().length))throw Error('Hidden capture region: '+selector);
 const wrappers=[...new Set(elements.flatMap(element=>[element.closest('.table-wrap'),...element.querySelectorAll('.table-wrap')]).filter(Boolean))];
 if(wrappers.length>1)throw Error('Capture one horizontally scrollable table region at a time: '+selector);
 const wrapper=wrappers[0];
 if(tile){
  if(wrapper)wrapper.scrollLeft=tile.x;
  window.scrollTo({top:tile.y,left:0,behavior:'instant'});
 }
 function geometry(){
  const rectangles=elements.map(element=>element.getBoundingClientRect());
  const top=Math.min(...rectangles.map(rect=>rect.top))+window.scrollY;
  const bottom=Math.max(...rectangles.map(rect=>rect.bottom))+window.scrollY;
  const headerBottom=Math.max(0,document.querySelector('.topbar').getBoundingClientRect().bottom);
  const visibleRectangles=elements.map(element=>(element.closest('.table-wrap')||element).getBoundingClientRect());
  const left=Math.min(...visibleRectangles.map(rect=>rect.left)),right=Math.max(...visibleRectangles.map(rect=>rect.right));
  const tableRect=wrapper?.getBoundingClientRect();
  if(left< -1||right>window.innerWidth+1||tableRect&&(tableRect.left< -1||tableRect.right>window.innerWidth+1))throw Error('Capture region is horizontally outside viewport: '+selector);
  return {top,bottom,left,right,height:bottom-top,scrollY:window.scrollY,headerBottom,
   viewportWidth:window.innerWidth,viewportHeight:window.innerHeight,
   visibleTop:window.scrollY+headerBottom+16,visibleBottom:window.scrollY+window.innerHeight-16,
   horizontal:wrapper?{left:wrapper.scrollLeft,width:wrapper.clientWidth,total:wrapper.scrollWidth,viewportLeft:tableRect.left,viewportRight:tableRect.right}:null,
   text:elements.map(element=>element.textContent).join('\n'),
   controls:elements.flatMap(element=>[...element.querySelectorAll('input,select')]).map(element=>({id:element.id,value:element.value,checked:element.checked})),
   rows:elements.flatMap(element=>[...element.querySelectorAll('tr')]).map(row=>[...row.children].map(cell=>cell.textContent))};
 }
 let previous='',stable=0;
 for(let frame=0;frame<60;frame++){
  await new Promise(resolve=>requestAnimationFrame(resolve));
  const current=geometry(),signature=JSON.stringify(current);
  stable=signature===previous?stable+1:0;previous=signature;
  if(stable>=2)return current;
 }
 throw Error('Capture region did not settle: '+selector);
}

function createRegionCapture({evaluate,command,save,receipts}){
 return async function captureRegion(name,selector){
  const measure=tile=>evaluate('('+frameRegion.toString()+')('+JSON.stringify(selector)+','+JSON.stringify(tile)+')');
  const initial=await measure(null),available=initial.viewportHeight-initial.headerBottom-32;
  const vertical=segmentOffsets(initial.height,available);
  const horizontal=initial.horizontal?segmentOffsets(initial.horizontal.total,initial.horizontal.width):[0];
  assert.ok(vertical.length*horizontal.length<=32,'Capture exceeds bounded thirty-two-image region budget');
  const tiles=[];
  try{
  for(let x=0;x<horizontal.length;x++){
   let covered=initial.top;
   for(let y=0;y<vertical.length;y++){
    const actual=await measure({x:horizontal[x],y:initial.top-initial.headerBottom-16+vertical[y]});
    assert.equal(actual.viewportWidth,initial.viewportWidth,'Viewport cannot change within a capture');
    assert.equal(actual.viewportHeight,initial.viewportHeight);
    assert.ok(Math.abs(actual.top-initial.top)<=1&&Math.abs(actual.bottom-initial.bottom)<=1,'Capture region cannot move after planning');
    assert.ok(actual.visibleTop<=covered+1,'Capture must not skip rows behind the sticky header');
    const from=Math.max(actual.top,actual.visibleTop),to=Math.min(actual.bottom,actual.visibleBottom);
    assert.ok(to>from,'Capture must contain actual region pixels');covered=Math.max(covered,to);
    const filename=name+'-v'+(y+1)+'-h'+(x+1)+'.png';
    const shot=await command('Page.captureScreenshot',{format:'png',captureBeyondViewport:false});
    await save(filename,shot.data);
    tiles.push({filename,from,to,...actual});
   }
   assert.ok(covered>=initial.bottom-1,'All rows through the end of the region must be captured');
  }
  if(initial.horizontal){
   let covered=0;
   for(const left of horizontal){
    const actual=tiles.find(tile=>Math.abs(tile.horizontal.left-left)<=1)?.horizontal;
    assert.ok(actual&&actual.left<=covered+1,'Horizontal table coverage cannot skip columns');covered=Math.max(covered,actual.left+actual.width);
   }
   assert.ok(covered>=initial.horizontal.total-1,'Every table column must be captured');
  }
  const receipt={gate:'pixel-region-coverage',name,selector,verticalSegments:vertical.length,horizontalSegments:horizontal.length,tiles};
  receipts.push(receipt);return receipt;
  }finally{
   // Capture-only scrolling must not leak into later native interactions.
   await measure({x:initial.horizontal?.left??0,y:initial.scrollY});
  }
 };
}
module.exports={segmentOffsets,frameRegion,createRegionCapture};
