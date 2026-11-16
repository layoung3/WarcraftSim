'use strict';
// Exact fixture outcomes, shared by the real browser and executable local tests.
// These are assertions about validation, not an alternative implementation of it.
const enhancementRejectionCases=[
 {name:'gear',mutate:x=>x.gear.items.push({slotKey:'main-hand',itemId:19102}),error:'Enhancement equipment is pending eligibility evidence.'},
 {name:'enchant',mutate:x=>x.gear.items.push({slotKey:'main-hand',itemId:19102,enchantId:1}),error:'Enhancement equipment is pending eligibility evidence.'},
 {name:'buff',mutate:x=>x.modifiers.buffs.push('arcane-intellect'),error:'Enhancement external modifiers are unavailable.'},
 {name:'racial',mutate:x=>x.racialActive=true,error:'Enhancement racial actives are unavailable.'},
 {name:'batch',mutate:x=>x.encounter.iterations=2,error:'Enhancement supports one deterministic run only.'},
 {name:'script',mutate:x=>x.encounter.soloScript.enabled=true,error:'Enhancement generic encounter scripts are unavailable.'},
 {name:'adds',mutate:x=>x.encounter.soloScript.addWindows.push({}),error:'Enhancement generic encounter scripts are unavailable.'},
 {name:'priority',mutate:x=>x.encounter.customPriority=['stormstrike'],error:'Unsupported Enhancement setting: customPriority'},
 {name:'unknown-rank',mutate:x=>x.talents.ranks['unknown-canonical-rank']=1,error:'Imported build contains a talent outside the current class catalog.'},
 {name:'phantom-enabled',mutate:x=>{x.encounter.combatItems.enabled=false;x.encounter.combatItems.majorManaPotionCharges=1;x.preparedCombatConsumables=[{key:'majorManaPotionCharges',charges:1}];},error:'Unsupported Enhancement combat inventory field: enabled'},
 {name:'offhand-windfury',mutate:x=>x.encounter.enhancement.offHandImbue='windfury',error:'Enhancement imbues require a valid hand; off-hand requires dual wield and cannot use Windfury.'}
];
const serverTalentRejection={name:'server-talent',mutate:x=>{x.talents.ranks['thundering-strikes']=6;x.talents.enhancementPointsSpent=37;},error:'Unrecognized talent or invalid rank: thundering-strikes.',server:{url:'/api/forever/talents/shaman/enhancement/validate-itemized',rankKey:'thundering-strikes',rank:6}};
function importRejectionMatches(state,expectation,after){
 if(state.fileChanged!==true||state.statusChanged!==true||state.fileValue!==''||state.status==='Build imported.'||state.status!==expectation.error)return false;
 if(!expectation.server)return true;
 const server=expectation.server;
 return state.receipts.some(x=>x.sequence>after&&x.status===400&&x.url===server.url&&x.request?.ranks?.[server.rankKey]===server.rank&&(x.response?.error??x.response?.Error)===expectation.error);
}
function rejectionExpression(expectation,after){
 return '('+importRejectionMatches.toString()+')({fileChanged:window.__fileChanged,statusChanged:window.__statusChanged,fileValue:document.querySelector("#file").value,status:document.querySelector("#status").textContent,receipts:window.__enhancementHttp},'+JSON.stringify({error:expectation.error,server:expectation.server})+','+JSON.stringify(after)+')';
}
module.exports={enhancementRejectionCases,serverTalentRejection,importRejectionMatches,rejectionExpression};
