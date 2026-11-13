"use strict";
(function(root){
 const labels={4:"Cast completed",6:"Damage",8:"Resource change",9:"Aura applied",10:"Aura removed",11:"Aura expired",14:"Phase started",15:"Phase ended",16:"Cast cancelled",28:"Channel started",30:"Channel completed",44:"Equipment activated",45:"Target state changed"};
 const category=type=>type===8?"resources":[9,10,11].includes(type)?"auras":[14,15,45].includes(type)?"encounter":[4,16,28,30,44].includes(type)?"actions":"damage";
 function inspect(report){
  const sim=report.rawServerResponse?.sim||{},run=sim.run||sim.batch?.representativeRun||{};
  const events=Array.isArray(run.timeline)?run.timeline:[];
  const rows=events.filter(e=>Object.hasOwn(labels,e.type)).map(e=>({
   time:e.timeSeconds,category:category(e.type),event:labels[e.type],actor:e.sourceActorKey||"-",target:e.targetActorKey||"-",ability:e.abilityKey||e.encounterPhaseKey||e.encounterEventKey||"-",
   detail:e.type===8?[e.resourceKey,e.amount,e.resourceTotalAfter===null||e.resourceTotalAfter===undefined?"":"(remaining "+e.resourceTotalAfter+")"].filter(v=>v!==undefined&&v!==null).join(" "):
    e.type===6?[e.amount,e.resultKey,e.isPeriodic?"periodic":""].filter(v=>v!==undefined&&v!==null&&v!=="").join(" "):e.description||"-"
  }));
  return {rows,captured:events.length,iterations:report.iterationStatistics?.iterations||1};
 }
 const api={inspect};if(typeof module!=="undefined"&&module.exports)module.exports=api;
 if(!root?.document)return;
 const document=root.document,$=id=>document.getElementById(id);let current=null;
 function render(){
  if(!current)return;const selected=$("reportEventFilter").value;
  const rows=current.rows.filter(e=>selected==="all"||e.category===selected);
  $("reportEventStatus").textContent=current.captured===0?"No timeline captured. Enable timeline capture in Encounter and run again.":
   (current.iterations>1?"Representative seeded run, not averaged events. ":"")+rows.length+" matching events; showing the first 200. Cast receipts show actual action timing, not inferred cooldown availability.";
  $("reportEvents").replaceChildren();
  for(const e of rows.slice(0,200)){const tr=document.createElement("tr");for(const value of [e.time,e.event,e.actor,e.target,e.ability,e.detail]){const td=document.createElement("td");td.textContent=String(value??"-");tr.append(td);}$("reportEvents").append(tr);}
 }
 document.addEventListener("warcraftsim:simulation-complete",e=>{current=inspect(e.detail);render();});
 $("reportEventFilter").addEventListener("change",render);
})(typeof window!=="undefined"?window:null);
