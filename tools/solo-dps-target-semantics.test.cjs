"use strict";
const assert=require("node:assert/strict"),fs=require("node:fs"),vm=require("node:vm");
const source=fs.readFileSync("WarcraftSim.Api/wwwroot/solo-dps.js","utf8").replace(/\r\n/g,"\n");
// Execute the actual exported summary object, not a test copy of its mapping.
const matches=[...source.matchAll(/combatSummary:\{([\s\S]*?)\n  \},\n  warnings:/g)];
assert.equal(matches.length,1,"Locate the single actual simulation report summary");
const summaryCode="({"+matches[0][1]+"})";
let checks=0;
function verify(b,expected){
 const before=JSON.stringify(b);
 const summary=vm.runInNewContext(summaryCode,{b,data:{maximumHealth:5100},clone:structuredClone});
 assert.deepEqual(JSON.parse(JSON.stringify(summary.targetSemantics)),expected,b.classKey+" exact target contract");
 assert.equal(summary.maximumHealth,5100,"Player resource Health stays independent from target Health");
 assert.equal(JSON.stringify(b),before,"Report generation cannot mutate the simulation request");
 checks++;
}
for(const targetCount of [1,3])verify({classKey:"shaman",elemental:{targetCount}},
 {level:63,mitigation:"unmitigated",targetCount,initialHealthPercent:100});
for(const additionalTargets of [0,2])verify({classKey:"druid",balance:{additionalTargets}},
 {level:63,mitigation:"unmitigated",targetCount:1+additionalTargets,initialHealthPercent:100});
for(const initialHealthPercent of [100,20,65.5])verify({classKey:"priest",shadow:{targetInitialHealthPercent:initialHealthPercent,priestMaximumHealth:4800}},
 {level:63,mitigation:"unmitigated",targetCount:1,initialHealthPercent});
for(const classKey of ["mage","warlock","hunter","warrior"])verify({classKey},null);
console.log(`Actual exported shared target-semantics contract passed: ${checks} cases.`);
