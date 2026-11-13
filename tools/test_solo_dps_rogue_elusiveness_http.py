#!/usr/bin/env python3
"""Self-contained offline fixture/Decimal/identity/cooldown contracts."""
import copy
from decimal import Decimal as D
import importlib.util
from pathlib import Path
import unittest
S=importlib.util.spec_from_file_location('elusiveness_http',Path(__file__).with_name('solo-dps-rogue-elusiveness-http.py'))
M=importlib.util.module_from_spec(S);S.loader.exec_module(M)

def defaults():
    return {s:dict(seed=142001+i,specialization=s,character=dict(id=M.ID,name='Fixture Rogue',classKey='rogue',specializationKey=s,race='human',level=60),tuning=dict(mutilateBonusDamagePerHand=13,sinisterStrikeBonusDamage=68,backstabBonusDamage=85,eviscerateDamagePerPoint=160,ruptureTickDamagePerPoint=24,garroteTickDamage=65,armorDamageReductionConstant=5500,improvedGarroteDamagePerRank=D('.05')),startingEnergyPercent=100) for i,s in enumerate(M.SPECS)}

def windows():
    return dict(cases={f'window-{s}-r{r}':dict(end=D('263.5') if r==1 else D('226')) for s in M.SPECS for r in [1,2]})

class FixtureTests(unittest.TestCase):
    def test_full_bounded_nonmutating_unique(self):
        d=defaults();f=M.fixtures(d,'full',windows());self.assertEqual(d,defaults());self.assertEqual(len(f),len({c['name'] for c in f}));self.assertLess(len(f),110)
    def test_probe_all_positive_specs(self):
        f=M.fixtures(defaults(),'probe');self.assertEqual(len(f),6);self.assertEqual({(c['body']['specialization'],c['body']['talents']['ranks']['elusiveness']) for c in f},{(s,r) for s in M.SPECS for r in [1,2]})
    def test_end_times_are_not_guessed(self):
        with self.assertRaises(AssertionError):M.fixtures(defaults(),'full')
        f=M.fixtures(defaults(),'full',windows());self.assertTrue(all(c['body']['durationSeconds']==windows()['cases'][c['name']]['end'] for c in f if c['name'].startswith('window-')))
    def test_primary_repeated_natural_without_resets(self):
        for c in M.fixtures(defaults(),'full',windows()):
            if c['name'].startswith(('window-','repeated-')):
                r=c['body'];self.assertFalse(r['useUtilityAbilities']);self.assertFalse(r['useInstantPoison']);self.assertEqual(set(r['talents']['ranks']),{'elusiveness'});self.assertNotIn('rotation',r)
                if c['name'].startswith('repeated-'):self.assertTrue(600<=r['durationSeconds']<=900)
    def test_strict_zero_not_admitted(self):
        f=M.fixtures(defaults(),'full',windows());xs=[c for c in f if c['name'].startswith('strict-')];self.assertEqual(len(xs),33);self.assertTrue(all(c['baselineStatus']==c['correctedStatus']==400 for c in xs))
    def test_numeric_string_full_causal_not_diagnostic(self):
        c=next(c for c in M.fixtures(defaults(),'full',windows()) if c['name']=='binding-numeric-string');self.assertGreater(c['body']['durationSeconds'],270);self.assertTrue(c['trace']);r=M.bound_request(c['body']);self.assertEqual(r['talents']['ranks']['elusiveness'],1);self.assertEqual(c['body']['talents']['ranks']['elusiveness'],'1')
    def test_actual_latency_default_pair_and_overlap(self):
        f={c['name']:c for c in M.fixtures(defaults(),'full',windows())};self.assertNotIn('inputLatencyMs',f['timing-None']['body'])
        for n in [0,80]:self.assertEqual(f[f'timing-{n}']['body']['inputLatencyMs'],n);self.assertEqual(f[f'timing-{n}']['body'],f[f'concurrent-{n}']['body']);self.assertTrue(f[f'concurrent-{n}']['concurrent'])
    def test_preparation_declares_existing_milestone(self):
        c=next(c for c in M.fixtures(defaults(),'full',windows()) if c['name']=='interaction-preparation');self.assertEqual(c['body']['talents']['subtletyPointsSpent'],21)
    def test_current_report_elusiveness_boundaries(self):
        result=M.verify_report_contract();self.assertTrue(result['passed']);self.assertEqual(len(result['proof']['negative']),24)
    def test_smoke_small_no_local_evidence(self):self.assertEqual(len(M.fixtures(defaults(),'smoke')),15)

class ExactTests(unittest.TestCase):
    def reject(self,a,b,path=M.RUN):
        with self.assertRaises(AssertionError):M.exact_response(a,b,dict(path=path),'old','new')
    def test_decimal_unknown_and_types(self):
        for a,b in [({'x':D('0.1234567890123456789012345678')},{'x':D('0.1234567890123456789012345679')}),({'x':True},{'x':1}),({},dict(x=None)),({'unknown':1},{'unknown':2}),({'x':[1,2]},{'x':[2,1]})]:self.reject(a,b)
    def test_event_order_missing_and_extra(self):
        a={'run':{'timeline':[{'timeSeconds':210,'amount':1},{'timeSeconds':211,'amount':2}]}}
        for xs in [a['run']['timeline'][::-1],a['run']['timeline'][:1],a['run']['timeline']+[{}],[dict(a['run']['timeline'][0],timeSeconds=D('210.001')),a['run']['timeline'][1]]]:self.reject(a,{'run':{'timeline':xs}})
    def test_generated_identity_bijection_and_ownership(self):
        a='aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';b='bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb';c='cccccccc-cccc-4ccc-8ccc-cccccccccccc'
        x={'run':{'timeline':[dict(abilityExecutionId=a,targetActorKey='target'),dict(abilityExecutionId=a,targetActorKey='target')]}};y=copy.deepcopy(x)
        for e in y['run']['timeline']:e['abilityExecutionId']=b
        M.exact_response(x,y,dict(path=M.RUN),'old','new')
        y['run']['timeline'][1]['abilityExecutionId']=c;self.reject(x,y)
        y=copy.deepcopy(x);y['run']['timeline'][1]['targetActorKey']='rogue';self.reject(x,y)
        self.reject({'abilityExecutionId':a},{'abilityExecutionId':b})
        y=copy.deepcopy(x);y['run']['timeline'][0]['abilityExecutionId']=b;x['literal']=a;y['literal']=a;self.reject(x,y)
    def test_default_identity_only_named_get(self):
        a={'character':{'id':'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa'}};b={'character':{'id':'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb'}}
        M.exact_response(a,b,dict(path='/api/sim/rogue/defaults?spec=subtlety'),'old','new')
        self.reject(a,b);self.reject(a,b,'/api/sim/rogue/defaults?spec=subtlety&decoy=1')
    def test_stamp_checkout_and_preparation_are_significant(self):
        a=dict(sourceCommit='old',workflowCheckoutCommit='same');b=dict(sourceCommit='new',workflowCheckoutCommit='same');M.exact_response(a,b,dict(path=M.M.METADATA),'old','new')
        b['workflowCheckoutCommit']='changed';self.reject(a,b,M.M.METADATA)
        self.reject({'preparation':{'request':{'rank':1}}},{'preparation':{'request':{'rank':2}}})
    def test_resource_counts_damage_and_internal_count_significant(self):
        for key in ['resourceTotalAfter','damageDone','endingComboPoints','processedEventCount','stealthOpeners']:
            self.reject({key:1},{key:2})
    def test_diagnostic_never_masked(self):self.reject({'traceId':'a'},{'traceId':'b'})
    def test_binary_float_rejected(self):
        with self.assertRaises(TypeError):M.O.encode({'x':.1})

class CausalTests(unittest.TestCase):
    def test_literal_cooldown_tables(self):
        for key,old,new in [('vanish',[300,270,240],[300,255,210]),('blind',[180,165,150],[180,135,90])]:
            self.assertEqual([M.cooldown(key,r,'baseline') for r in range(3)],old);self.assertEqual([M.cooldown(key,r,'corrected') for r in range(3)],new)
        for rank in [True,-1,3,'1']:
            with self.assertRaises(AssertionError):M.cooldown('vanish',rank,'corrected')
    def pair(self):
        r=M.controlled(defaults(),2)
        start=dict(type=3,timeSeconds=D('1.08'),abilityKey='vanish')
        common=dict(specialization='subtlety',maximumEnergy=100,maximumHealth=4000,modelingNotes=['unchanged'])
        a=dict(common,run=dict(timeline=[start,dict(type=3,timeSeconds=212,abilityKey='backstab')],processedEventCount=12))
        b=copy.deepcopy(a);b['run']['timeline'][1]=dict(type=3,timeSeconds=D('211.08'),abilityKey='vanish');b['run']['processedEventCount']=13
        return r,a,b
    def test_start_relative_not_simulation_zero(self):
        r,a,b=self.pair();p=M.first_cause(r,a,b);self.assertEqual(p['correctedReady'],D('211.08'));self.assertEqual(p['baselineReady'],D('241.08'));self.assertEqual(p['equalEventPrefix'],1);self.assertNotEqual(p['processedEventCountBefore'],p['processedEventCountAfter'])
    def test_timing_before_after_and_missing_start_rejected(self):
        for t in [210,D('211.079'),D('241.08')]:
            r,a,b=self.pair();b['run']['timeline'][1]['timeSeconds']=t
            with self.assertRaises(AssertionError):M.first_cause(r,a,b)
        r,a,b=self.pair();a['run']['timeline'].pop(0);b['run']['timeline'].pop(0)
        with self.assertRaises(AssertionError):M.first_cause(r,a,b)
    def test_summary_only_and_unrelated_metadata_rejected(self):
        r,a,b=self.pair();b=copy.deepcopy(a);b['run']['processedEventCount']+=1
        with self.assertRaises(AssertionError):M.first_cause(r,a,b)
        r,a,b=self.pair();b['modelingNotes']=['wrong']
        with self.assertRaises(AssertionError):M.first_cause(r,a,b)
    def test_success_binding_cannot_use_diagnostic_shortcut(self):
        with self.assertRaises(AssertionError):M.H.binding_comparison({}, {},dict(status=200),dict(status=200))
    def test_broad_not_silently_accepted(self):
        with self.assertRaises((AssertionError,KeyError)):M.broad_accounting({}, {},'baseline')


def tiny_trace():
    """Literal .5s Ambush fixture built here, never loaded from local evidence."""
    r=M.controlled(defaults(),2);r['durationSeconds']=D('.5');r['rogueMaximumHealth']=4000
    actor=r['character']['name'];boss='Level 63 Rogue training boss';execution='aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';aura='bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb'
    def event(kind,**kw):
        e={k:None for k in M.H.EVENT_FIELDS};e.update(timeSeconds=0,type=kind,encounterSequenceSelectedActorKeys=[],isCritical=False,effectDeliveryType=0,isPeriodic=False,isChannelTick=False,isInternal=False);e.update(kw);return e
    aura_base=dict(sourceActorKey='rogue',targetActorKey='rogue',abilityKey='opening-stealth',effectKey='stealth-state',auraInstanceId=aura)
    cast=dict(sourceActorKey='rogue',targetActorKey='target',abilityKey='ambush',abilityExecutionId=execution)
    events=[event(0,description='Simulation started.'),event(9,**aura_base,description=f'Stealth applied to {actor} (stacks: 1).'),event(3,**cast,description=f'{actor} started casting Ambush.'),event(4,**cast,description=f'{actor} completed Ambush.'),event(8,sourceActorKey='rogue',targetActorKey='rogue',abilityKey='ambush',resourceKey='energy',amount=-60,resourceTotalAfter=40,description=f'{actor} spent 60 energy.'),event(6,**cast,effectKey='ambush-hit',schoolKey='physical',resolutionType='melee',weaponHandKey='main-hand',mitigationType='none',resultKey='hit',rawAmount=400,mitigatedAmount=0,mitigationPercent=0,blockedAmount=0,absorbedAmount=0,amount=400,description=f'Ambush dealt 400 damage to {boss}.'),event(10,**aura_base,description=f'Stealth stealth broken on {actor}.')]
    for key,guid in [('rogue-main-hand','cccccccc-cccc-4ccc-8ccc-cccccccccccc'),('rogue-off-hand','dddddddd-dddd-4ddd-8ddd-dddddddddddd')]:events.append(event(32,sourceActorKey='rogue',targetActorKey='target',abilityKey=key,autoAttackInstanceId=guid,description=f'{actor} started {key} against {boss}.'))
    events.extend([event(20,sourceActorKey='rogue',targetActorKey='target',abilityKey='ambush',effectKey='ambush-hit',amount=400,description='Rogue damage threat added to shared table.'),event(1,timeSeconds=D('.5'),description='Simulation ended.')])
    countfields='castStartedCount castCompletedCount castCancelledCount channelStartedCount channelCompletedCount channelCancelledCount damageOccurrenceCount healingOccurrenceCount absorbConsumptionCount criticalDamageCount criticalHealingCount directOccurrenceCount periodicOccurrenceCount channelTickOccurrenceCount'.split()
    ability=dict(abilityKey='ambush',**{k:0 for k in countfields},damageDone=400,healingDone=0,overhealingDone=0,absorptionDone=0,resultCounts={'hit':1});ability.update(castStartedCount=1,castCompletedCount=1,damageOccurrenceCount=1,directOccurrenceCount=1)
    nums='healingDone healingReceived overhealingDone overhealingReceived absorptionDone absorptionReceived blockedDamageReceived'.split();maps='healingDoneByAbility healingReceivedByAbility absorptionDoneByAbility absorptionReceivedByAbility'.split()
    actors={}
    for key in ['rogue','target']:
        own=key=='rogue';row=dict(actorKey=key,name=actor if own else boss,damageDone=400 if own else 0,damageTaken=0 if own else 400,damageDoneByAbility={'ambush':400} if own else {},damageTakenByAbility={} if own else {'ambush':400},abilities={'ambush':copy.deepcopy(ability)} if own else {},startingResources={'energy':100} if own else {},endingResources={'energy':70} if own else {},resourceStarvedSeconds=0,firstResourceStarvedAtSeconds=None,died=False,deathTimeSeconds=None,blockedDamageReceivedByAbility={} if own else {'ambush':0});row.update({k:0 for k in nums});row.update({k:{} for k in maps});actors[key]=row
    summary={k:actors['rogue'][k] for k in ['damageDone','damageTaken','damageDoneByAbility','damageTakenByAbility','abilities','blockedDamageReceivedByAbility']+nums+maps};summary.update(seed=r['seed'],durationSeconds=D('.5'),actorSummaries=actors,primaryActorDied=False,primaryActorDeathTimeSeconds=None)
    metrics={k:0 for k in ['rogue-expose-armor-points','rogue-stealth-openers','rogue-finisher-count','rogue-poison-procs','rogue-deadly-poison-ticks','rogue-main-hand-poison-charges','rogue-off-hand-poison-charges','rogue-preparation-cooldowns-reset','rogue-hack-and-slash-attacks','rogue-quick-recovery-energy','rogue-ending-combo-points','rogue-boss-threat','rogue-feint-threat-reduced','rogue-boss-attacks-dodged','rogue-boss-attacks-parried']};metrics.update({'rogue-stealth-openers':1,'rogue-ending-combo-points':1,'rogue-boss-threat':400})
    response=dict(specialization=r['specialization'],damageDone=400,dps=800,endingEnergy=70,maximumEnergy=100,maximumHealth=4000,endingHealth=4000,endingComboPoints=1,poisonProcs=0,finishersUsed=0,stealthOpeners=1,damageByAbility={'ambush':400},run=dict(summary=summary,timeline=events,processedEventCount=20,customMetrics=metrics),modelingNotes=['literal fixture'])
    return r,response

class TraceMutationTests(unittest.TestCase):
    def test_literal_trace_clean_checkout(self):
        r,v=tiny_trace();p=M.trace_accounting(r,v,'baseline');self.assertEqual(p['resources'][0]['cost'],60);self.assertEqual(p['damage'][0]['amount'],400)
    def test_missing_spend_and_stealth_exit_rejected_independently(self):
        for index in [4,6,7,8]:
            r,v=tiny_trace();v['run']['timeline'].pop(index)
            with self.assertRaises(AssertionError):M.trace_accounting(r,v,'baseline')
    def test_damage_resources_cp_counts_and_unknowns_rejected(self):
        for mutate in [lambda v:v.update(endingEnergy=71),lambda v:v.update(endingComboPoints=2),lambda v:v.update(stealthOpeners=2),lambda v:v['run']['timeline'][4].update(resourceTotalAfter=41),lambda v:v['run']['timeline'][5].update(amount=401),lambda v:v['run']['timeline'][1].update(targetActorKey='target'),lambda v:v['run']['timeline'][5].update(abilityExecutionId='eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee'),lambda v:v['run']['summary']['abilities']['ambush'].update(castCompletedCount=2),lambda v:v.update(unknown=1),lambda v:v['run']['timeline'][5].update(unknown=1)]:
            r,v=tiny_trace();mutate(v)
            with self.assertRaises(AssertionError):M.trace_accounting(r,v,'baseline')
    def test_missing_extra_reordered_events_rejected(self):
        for mutate in [lambda v:v['run']['timeline'].pop(9),lambda v:v['run']['timeline'].insert(6,copy.deepcopy(v['run']['timeline'][5])),lambda v:v['run']['timeline'].reverse()]:
            r,v=tiny_trace();mutate(v)
            with self.assertRaises(AssertionError):M.trace_accounting(r,v,'baseline')
    def test_float_midpoint_never_generated(self):
        for s in [1,D('1.08')]:self.assertIsInstance((D(s)+255+D(s)+270)/2,D)
    def test_broad_small_literal_trace(self):
        r,v=tiny_trace();self.assertIn('not independently reconstructed',M.broad_accounting(r,v,'baseline')['broadScope'])
        v['run']['summary']['actorSummaries']['target']['damageTaken']+=1
        with self.assertRaises(AssertionError):M.broad_accounting(r,v,'baseline')
    def test_preparation_off_gcd_keeps_input_delay(self):
        r=dict(durationSeconds=2,inputLatencyMs=80);v=dict(run=dict(timeline=[dict(type=3,timeSeconds=0,abilityKey='preparation'),dict(type=3,timeSeconds=D('.08'),abilityKey='blind')]))
        p=M.starvation_accounting(r,v);self.assertEqual(p['total'],D('.84'))
        v['run']['timeline'][1]['timeSeconds']=D('.079')
        with self.assertRaises(AssertionError):M.starvation_accounting(r,v)



class VanishLifecycleTests(unittest.TestCase):
    def fixture(self):
        r,v=tiny_trace();r['startInStealth']=False;xs=v['run']['timeline'];first=copy.deepcopy(xs[1]);first.update(timeSeconds=1,abilityKey='vanish',effectKey='vanish-stealth',auraInstanceId='eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee');second=copy.deepcopy(first);second.update(effectKey='stealth-state',auraInstanceId='ffffffff-ffff-4fff-8fff-ffffffffffff')
        start=copy.deepcopy(xs[2]);start.update(timeSeconds=1,abilityKey='vanish',abilityExecutionId='11111111-1111-4111-8111-111111111111');completed=copy.deepcopy(start);completed['type']=4
        initial=[copy.deepcopy(xs[7]),copy.deepcopy(xs[8])];stops=[dict(e,type=34,timeSeconds=1) for e in initial]
        hit=copy.deepcopy(xs[5]);hit['timeSeconds']=1;remove=copy.deepcopy(xs[6]);remove.update(timeSeconds=1,abilityKey='vanish',effectKey='vanish-stealth',auraInstanceId=second['auraInstanceId']);restart=[dict(e,timeSeconds=1) for e in initial]
        events=[xs[0]]+initial+[start,completed,first]+stops+[second,hit,remove]+restart+[xs[-1]]
        return r,dict(run=dict(timeline=events))
    def test_double_application_refresh_and_actual_removal(self):
        r,v=self.fixture();proof=M.verify_stealth_lifecycle(r,v);self.assertEqual([x['action'] for x in proof],['apply','apply','remove'])
    def test_each_application_removal_and_auto_transition_required(self):
        for index in [5,6,7,8,10,11,12]:
            r,v=self.fixture();v['run']['timeline'].pop(index)
            with self.assertRaises(AssertionError):M.verify_stealth_lifecycle(r,v)
    def test_removed_identity_must_be_refreshed_not_original(self):
        r,v=self.fixture();v['run']['timeline'][10]['auraInstanceId']=v['run']['timeline'][5]['auraInstanceId']
        with self.assertRaises(AssertionError):M.verify_stealth_lifecycle(r,v)


    def pending_fixture(self):
        r,v=self.fixture();xs=v['run']['timeline'];hits=[]
        for key,amount in [('rogue-main-hand',100),('rogue-off-hand',20)]:
            hit=copy.deepcopy(xs[9]);hit.update(abilityKey=key,abilityExecutionId=None,autoAttackInstanceId=None,effectKey='weapon-hit',weaponHandKey=key.removeprefix('rogue-'),amount=amount);hits.append(hit)
        v['run']['timeline']=xs[:5]+hits+xs[5:9]+xs[10:]
        return r,v
    def test_pending_owned_auto_damage_before_queued_applications(self):
        r,v=self.pending_fixture();p=M.verify_stealth_lifecycle(r,v);trigger=p[-1]['trigger']
        self.assertEqual(trigger['event'],5);self.assertEqual(trigger['vanishCompletionEvent'],4);self.assertEqual(trigger['activeAutoAttackInstanceId'],v['run']['timeline'][1]['autoAttackInstanceId'])
        self.assertEqual([x['event'] for x in p],[7,10,11])
    def test_pending_trigger_missing_time_owner_and_damage_kind_rejected(self):
        mutations=[dict(timeSeconds=0),dict(timeSeconds=2),dict(sourceActorKey='other'),dict(targetActorKey='rogue'),dict(isPeriodic=True),dict(amount=0),dict(amount=-1),dict(abilityExecutionId='aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa'),dict(effectKey='wrong'),dict(weaponHandKey='wrong')]
        for change in mutations:
            r,v=self.pending_fixture()
            for e in v['run']['timeline'][5:7]:e.update(change)
            with self.subTest(change=change),self.assertRaises(AssertionError):M.verify_stealth_lifecycle(r,v)
        r,v=self.pending_fixture();del v['run']['timeline'][5:7]
        with self.assertRaises(AssertionError):M.verify_stealth_lifecycle(r,v)
    def test_pending_trigger_before_completion_and_missing_transitions_rejected(self):
        r,v=self.pending_fixture();xs=v['run']['timeline'];v['run']['timeline']=xs[:4]+xs[5:7]+[xs[4]]+xs[7:]
        with self.assertRaises(AssertionError):M.verify_stealth_lifecycle(r,v)
        for index in [4,7,8,9,10,11,12,13]:
            r,v=self.pending_fixture();v['run']['timeline'].pop(index)
            with self.subTest(index=index),self.assertRaises(AssertionError):M.verify_stealth_lifecycle(r,v)

if __name__=='__main__':unittest.main()
