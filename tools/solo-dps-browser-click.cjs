'use strict';
const assert=require('node:assert/strict');

// The core browser harness already waits for stable rendered geometry after
// scrolling. Share that boundary for the restricted-class suites as well.
// Observe native events; never synthesize a DOM click or retry a missed input.
function createBrowserClick({evaluate,command}){
 return async function click(selector,button='left'){
  const point=await evaluate(`(async()=>{
   const element=document.querySelector(${JSON.stringify(selector)});
   if(!element)throw Error('Missing click target: '+${JSON.stringify(selector)});
   window.__soloDpsClickTarget=element;
   const sequence=(window.__soloDpsClickSequence||0)+1;window.__soloDpsClickSequence=sequence;
   const trace={sequence,selector:${JSON.stringify(selector)},events:[],point:null,armed:false};
   window.__soloDpsClickCurrent=trace;
   window.__soloDpsClickHistory=[...(window.__soloDpsClickHistory||[]),trace].slice(-12);
   if(!window.__soloDpsClickObserverInstalled){
    window.__soloDpsClickObserverInstalled=true;
    for(const type of ['mousedown','mouseup','click','contextmenu'])document.addEventListener(type,event=>{
     const current=window.__soloDpsClickCurrent;if(!current.armed)return;
     const target=event.target,expected=window.__soloDpsClickTarget;
     current.events.push({sequence:current.sequence,type,trusted:event.isTrusted,matched:target===expected||expected.contains(target),target:target?.id||target?.tagName,x:event.clientX,y:event.clientY,button:event.button});
    },true);
   }
   async function settle(){
    let previous=null,stable=0;
    for(let frame=0;frame<60;frame++){
     await new Promise(resolve=>requestAnimationFrame(resolve));
     const rect=element.getBoundingClientRect();
     const geometry=[rect.x,rect.y,rect.width,rect.height,window.scrollX,window.scrollY].join(',');
     stable=geometry===previous?stable+1:0;previous=geometry;
     if(stable>=2)return;
    }
    throw Error('Click target layout did not settle: '+${JSON.stringify(selector)});
   }
   function sample(){
    const rect=element.getBoundingClientRect();
    const x=rect.x+rect.width/2,y=rect.y+rect.height/2,hit=document.elementFromPoint(x,y);
    return {sequence,x,y,visible:element.isConnected&&rect.width>0&&rect.height>0,hit:hit===element||element.contains(hit),disabled:!!element.disabled};
   }
   element.scrollIntoView({block:'center',behavior:'instant'});await settle();let point=sample();
   if(!point.hit){
    // Sticky headers can obscure a centered target. Reposition before input,
    // as in the core harness, then re-settle and hit-test. Still only one click.
    window.scrollTo({top:0,left:0,behavior:'instant'});
    element.scrollIntoView({block:'nearest',behavior:'instant'});await settle();point=sample();
   }
   trace.point=point;trace.armed=point.visible&&point.hit&&!point.disabled;
   return point;
  })()`);
  assert.ok(point.visible&&point.hit,'Click target obscured: '+selector+' '+JSON.stringify(point));
  assert.equal(point.disabled,false,'Click target disabled: '+selector);
  for(const type of ['mousePressed','mouseReleased'])
   await command('Input.dispatchMouseEvent',{type,x:point.x,y:point.y,button,clickCount:1});
  const trace=await evaluate('(()=>{const trace=window.__soloDpsClickCurrent;trace.armed=false;return trace;})()');
  const eventType=button==='right'?'contextmenu':'click';
  const buttonNumber=button==='right'?2:button==='middle'?1:0;
  const events=trace.events.filter(event=>event.trusted);
  // Chromium can round fractional CDP coordinates in MouseEvent properties.
  // A right-click talent action can re-render its tile after contextmenu and
  // before mouseup. The original tile must receive press + activation; the
  // later release must still belong to this dispatch, but can hit its replacement.
  const matches=event=>event.sequence===point.sequence&&(event.matched||button==='right'&&event.type==='mouseup')&&event.button===buttonNumber&&Math.abs(event.x-point.x)<1&&Math.abs(event.y-point.y)<1;
  const press=events.findIndex(event=>event.type==='mousedown'),release=events.findIndex(event=>event.type==='mouseup'),activation=events.findIndex(event=>event.type===eventType);
  assert.ok(trace.sequence===point.sequence&&events.length===3&&events.every(matches)&&press>=0&&release>press&&activation>press&&(button==='right'||activation>release),
   'Trusted click did not reach '+selector+': '+JSON.stringify(trace));
 };
}

module.exports={createBrowserClick};
