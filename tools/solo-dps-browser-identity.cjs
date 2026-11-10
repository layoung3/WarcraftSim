'use strict';
const assert=require('node:assert/strict');
// Identity and source disclosure have different jobs. Canonical selector values
// identify the character; exact option labels retain the provisional disclosures.
const classes={
 mage:{label:'Mage',specs:['arcane','fire','frost']},
 warlock:{label:'Warlock',specs:['affliction','demonology','destruction']},
 hunter:{label:'Hunter',specs:['marksmanship','survival','beast-mastery']},
 warrior:{label:'Warrior',specs:['arms','fury','protection']},
 shaman:{label:'Shaman (Elemental / Enhancement, provisional)',specs:['elemental','enhancement']},
 druid:{label:'Druid (Balance, partial provisional)',specs:['balance']},
 priest:{label:'Priest (Shadow, partial provisional)',specs:['shadow']}
};
const human=value=>value.replaceAll('-',' ').replace(/\b\w/g,c=>c.toUpperCase());
function assertSoloDpsIdentity(identity,expected){
 if(expected){
  assert.equal(identity.classKey,expected.classKey,'Sidebar must retain the requested class');
  assert.equal(identity.specialization,expected.specialization,'Sidebar must retain the requested specialization');
 }
 const entry=classes[identity.classKey];
 assert.ok(entry&&entry.specs.includes(identity.specialization),'Selected class/spec must be a supported Solo DPS identity');
 assert.equal(identity.displayedTitle,human(identity.specialization)+' '+human(identity.classKey),'Sidebar must display exactly the selected class and specialization');
 assert.equal(identity.classLabel,entry.label,'Class selector must preserve its exact availability and provisional-source disclosure');
 assert.ok(typeof identity.race==='string'&&identity.race.length>0,'A selected race label is required');
 assert.ok(identity.subtitle.startsWith(identity.race+' '),'Sidebar race must match the selected race: '+JSON.stringify(identity));
 assert.ok(identity.subtitle.endsWith(' · Level 60'),'Sidebar must retain its level-60 identity');
}
module.exports={assertSoloDpsIdentity};
