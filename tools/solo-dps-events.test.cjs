"use strict";
const assert=require("node:assert/strict");
const {inspect}=require("../WarcraftSim.Api/wwwroot/solo-dps-events.js");
const report={iterationStatistics:{iterations:3},rawServerResponse:{sim:{run:{timeline:[
 {type:12,timeSeconds:1,amount:50},{type:6,timeSeconds:1,amount:50,isPeriodic:true,sourceActorKey:"mage"},
 {type:8,timeSeconds:2,resourceKey:"mana",amount:-20,resourceTotalAfter:0},
 {type:4,timeSeconds:2,abilityKey:"arcane-power"},{type:45,timeSeconds:5,description:"Add expired"}
]}}}};
const data=inspect(report);assert.equal(data.rows.length,4);assert.equal(data.rows.filter(x=>x.category==="damage").length,1);
assert.match(data.rows[1].detail,/remaining 0/);assert.equal(data.rows[2].category,"actions");assert.equal(data.rows[3].category,"encounter");assert.equal(data.iterations,3);
assert.equal(inspect({rawServerResponse:{sim:{batch:{representativeRun:report.rawServerResponse.sim.run}}}}).rows.length,4);
assert.equal(inspect({}).captured,0);
console.log("CP175 readable event contracts passed: periodic drivers excluded, resource zero retained, Warrior timeline and representative labels.");
