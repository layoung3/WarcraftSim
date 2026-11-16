"use strict";
// Execute the actual editor mutation and request guard, not copied algorithms.
const assert=require("node:assert/strict"),fs=require("node:fs"),vm=require("node:vm");
const source=fs.readFileSync("WarcraftSim.Api/wwwroot/solo-dps.js","utf8");
const helpers=source.slice(source.indexOf("function isRemovedPriorityTarget("),source.indexOf("const baseSoloAddRender="));
assert.ok(helpers.includes("function removeSoloEnemy("));
const plain=value=>JSON.parse(JSON.stringify(value));
function editor(cls){
 const prefix=["mage","warlock"].includes(cls)?"target-":"add-";
 const fields={classKey:{value:cls},priorityStatus:{},soloAddGroupCount:{value:"1"},duration:{value:"60"},addSoloEnemy:{addEventListener:(name,fn)=>fields.add=fn}};
 const rows=[1,2,3].map(i=>({key:"action-"+i,enabled:true,targetActorKey:prefix+i,
  conditions:[{conditionType:"target-health-percent",comparisonOperator:"less-than-or-equal",value:80}],
  orGroups:[[{conditionType:"active-enemy-count",comparisonOperator:"greater-than",value:0}]],minimumRage:10+i}));
 rows.push({key:"boss-action",enabled:true,targetActorKey:null});
 const windows=[1,2,3].map(i=>({targetKey:prefix+i,spawnTimeSeconds:i*5,despawnTimeSeconds:40+i,
  startingHealthPercent:90-i,healthMilestonesRelativeToSpawn:i%2===1,
  healthMilestones:[{timeSeconds:2,healthPercent:40+i}]}));
 const context={priorityRows:rows,soloScript:{enabled:true,addWindows:windows},request:{customPriority:rows.map(x=>x.key),rotationMode:"custom",customRotation:rows.map(x=>({action:x.key,enabled:x.enabled,minimumRage:x.minimumRage}))},
  $:id=>fields[id],syncPriorityText:()=>{},renderSoloScript:()=>context.renders++,drawPriorityRows:()=>context.draws++,renders:0,draws:0,buildDirty:false,note:message=>{throw Error(message);}};
 vm.createContext(context);vm.runInContext(helpers,context);
 const addStart=source.indexOf("$('addSoloEnemy').addEventListener('click'");
 vm.runInContext(source.slice(addStart,source.indexOf("const baseAddBody=",addStart)),context);
 return {context,fields,rows,windows,prefix};
}
for(const cls of ["mage","warlock","hunter","warrior"]){
 for(const removedIndex of [0,1,2]){
  const {context:c,fields,rows,windows,prefix}=editor(cls),before=plain(rows),oldWindows=plain(windows);
  c.removeSoloEnemy(removedIndex);
  assert.deepEqual(plain(c.soloScript.addWindows),oldWindows.filter((_,i)=>i!==removedIndex).map((x,i)=>({...x,targetKey:prefix+(i+1)})),cls+" preserves survivor timing/health while reindexing");
  rows.forEach((row,i)=>{
   if(i===removedIndex)assert.deepEqual(plain(row),{...before[i],enabled:false,targetActorKey:"removed:"+prefix+(i+1)});
   else assert.deepEqual(plain(row),{...before[i],targetActorKey:i===3?null:prefix+(i>removedIndex?i:i+1)});
  });
  assert.ok(!c.request.customPriority.includes(rows[removedIndex].key));
  assert.equal(c.request.customRotation[removedIndex].enabled,false);
  assert.equal(c.renders,1);assert.equal(c.draws,1);assert.equal(c.buildDirty,true);
  assert.doesNotThrow(()=>c.validateRemovedPriorityTargets());
  fields.add();
  assert.deepEqual(c.soloScript.addWindows.map(x=>x.targetKey),[prefix+1,prefix+2,prefix+3]);
  assert.equal(rows[removedIndex].targetActorKey,"removed:"+prefix+(removedIndex+1),"Reused actor IDs cannot revive removed actions");
  assert.equal(rows[removedIndex].enabled,false);assert.equal(c.draws,2);
  rows[removedIndex].enabled=true;
  assert.throws(()=>c.validateRemovedPriorityTargets(),/Select a configured target/);
 }
 const {context:c}=editor(cls);
 c.priorityRows[1].enabled=false;
 c.removeSoloEnemy(0);assert.equal(c.priorityRows[1].enabled,false,"Already-disabled survivor stays disabled");
 c.removeSoloEnemy(0);c.removeSoloEnemy(0);
 assert.equal(c.soloScript.addWindows.length,0);
 assert.ok(c.priorityRows.slice(0,3).every(x=>!x.enabled&&x.targetActorKey.startsWith("removed:")));
 assert.doesNotThrow(()=>c.validateRemovedPriorityTargets(),"Unchanged explicit boss action may still run");
 c.request.customPriority=[];c.request.customRotation=[];
 assert.throws(()=>c.validateRemovedPriorityTargets(),/Apply a remaining custom priority/,"No fallback to automatic defaults");
 c.request.customPriority=["action-1"];c.request.customRotation=[{action:"action-1",enabled:true}];
 assert.throws(()=>c.validateRemovedPriorityTargets(),/Apply a remaining custom priority/,"Stale applied order must fail closed");
 const snapshot=JSON.stringify(c.soloScript);c.removeSoloEnemy(-1);c.removeSoloEnemy(8);assert.equal(JSON.stringify(c.soloScript),snapshot);
}
const armor=source.split(String.fromCharCode(10)).find(line=>line.includes("$('warriorArmor').value=request.targetArmor??"));
assert.ok(armor);
for(const value of [0,2500,null,undefined]){
 const field={};vm.runInNewContext(armor,{request:{targetArmor:value},$:()=>field});
 assert.equal(field.value,value??3500,"Import preserves zero armor and only defaults missing values");
}
console.log("CP175 encounter editor behavioral regressions passed: 4 classes x first/middle/last removal, sequential actor identities, conditions/windows, disabled survivors, repeated deletion/addition, fail-closed custom orders, zero armor.");
