'use strict';
// Runs in the real page, using observation-only HTTP receipts and rendered output.
function magePreviewReady(after){
 const get=id=>document.querySelector('#'+id);
 if(get('classKey').value!=='mage'||get('status').dataset.buildState!=='ready'||get('statStatus').textContent!=='Up to date')return false;
 const latest=window.__combatHttp.filter(x=>x.url==='/api/forever/character/generate-level60').at(-1);
 if(!latest||latest.sequence<=after||latest.status!==200||latest.deliveredAt===undefined)return false;
 const c=latest.request?.character;
 if(c?.classKey!=='mage'||c.specializationKey!==get('spec').value||c.race!==get('race').value||c.level!==60||latest.request.useProvisionalBaseline!==get('provisional').checked||latest.request.useReviewedBaseline!==!get('provisional').checked)return false;
 const stats=latest.response?.effectiveStats;
 if(!stats)return false;
 const expected=Object.entries(stats.values||stats).filter(([,v])=>v!==null&&typeof v!=='object');
 const rendered=[...get('stats').children];
 if(!expected.length||rendered.length!==expected.length||rendered.some((row,i)=>row.querySelector('strong')?.textContent!==String(expected[i][1])))return false;
 try{if(JSON.stringify(JSON.parse(get('contributions').textContent))!==JSON.stringify(latest.response.contributions||[]))return false;}catch{return false;}
 return {sequence:latest.sequence,classKey:c.classKey,specialization:c.specializationKey,race:c.race,stats:expected,contributions:latest.response.contributions||[]};
}
module.exports={magePreviewReady};
