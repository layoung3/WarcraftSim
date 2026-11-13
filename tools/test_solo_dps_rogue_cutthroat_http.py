#!/usr/bin/env python3
"""Pure Cutthroat fixture/precision/identity/trace contracts. No HTTP or build."""
import copy
from decimal import Decimal as D
import importlib.util
from pathlib import Path
import unittest

S=importlib.util.spec_from_file_location('cutthroat_http',Path(__file__).with_name('solo-dps-rogue-cutthroat-http.py'))
M=importlib.util.module_from_spec(S);S.loader.exec_module(M)


def defaults():
    return {spec:dict(seed=142001+i,character=dict(id=M.ID,name='Fixture Rogue',race='human',classKey='rogue',specializationKey=spec,level=60),specialization=spec,
        tuning=dict(mutilateBonusDamagePerHand=13,sinisterStrikeBonusDamage=68,eviscerateDamagePerPoint=160,ruptureTickDamagePerPoint=24,garroteTickDamage=65,armorDamageReductionConstant=5500),startingEnergyPercent=100)
        for i,spec in enumerate(M.SPECS)}


class FixtureTests(unittest.TestCase):
    def test_full_inventory_and_default_derivatives(self):
        data=defaults();f=M.fixtures(data);self.assertEqual(len(f),378)
        selected=[c for c in f if c['name'].startswith('default-')];self.assertEqual(len(selected),144)
        self.assertEqual(len({c['name'] for c in f}),len(f))
        self.assertEqual({c['body'].get('inputLatencyMs') for c in selected},{None,0,80})
        self.assertEqual({c['body']['seed'] for c in selected},{142001,142002,142003,7331})
        self.assertEqual({c['body']['durationSeconds'] for c in selected},{20,120})
        self.assertEqual({c['body']['tuning']['mutilateBonusDamagePerHand'] for c in selected},{13,D('17.25')})
        self.assertEqual(data,defaults())
    def test_explicit_preparation_is_not_provisional(self):
        for c in M.fixtures(defaults()):
            if c['name'].startswith('default-') and '-p1-' in c['name']:
                self.assertFalse(c['body']['useProvisionalLevel60Baseline']);self.assertIsNotNone(c['body']['level60Baseline'])
    def test_smoke_is_bounded_and_keeps_real_proc_ranks(self):
        f=M.fixtures(defaults(),'smoke');self.assertEqual(len(f),59)
        self.assertEqual({c['body']['talents']['ranks']['cutthroat'] for c in f if c['name'].startswith('proc-r')},{1,2,3,4,5})
    def test_rank_four_five_are_separate_status_changes(self):
        for c in M.fixtures(defaults()):
            if c['group']=='admission':self.assertEqual((c['baselineStatus'],c['correctedStatus']),(400,200))
            else:self.assertEqual(c['baselineStatus'],c['correctedStatus'])
    def test_assassination_both_previews_use_legacy_caps(self):
        byname={c['name']:c for c in M.fixtures(defaults())}
        for route in ['character','rotation']:
            for rank in range(6):
                c=byname[f'preview-{route}-assassination-r{rank}'];self.assertEqual(c['correctedStatus'],200);self.assertEqual(c['baselineStatus'],400 if rank>3 else 200)
    def test_strict_zero_and_positive_stay_rejected(self):
        f=M.fixtures(defaults());strict=[c for c in f if c['name'].startswith('strict-')]
        self.assertTrue(strict)
        self.assertTrue(all(c['baselineStatus']==c['correctedStatus']==400 for c in strict))
        self.assertTrue(any('-r0-' in c['name'] for c in strict))
    def test_concurrent_requests_equal_sequential_bytes(self):
        f={c['name']:c for c in M.fixtures(defaults())}
        for latency in [0,80,1000]:
            raw=M.O.encode(f[f'timing-{latency}']['body'])
            for label in ['repeat','concurrent']:self.assertEqual(raw,M.O.encode(f[f'{label}-{latency}']['body']))
    def test_no_public_fake_parried_backstab(self):
        self.assertFalse(any('parry' in c['name'] for c in M.fixtures(defaults())))


class ExactContractTests(unittest.TestCase):
    def setUp(self):
        self.row={'path':M.RUN}
        self.old={'run':{'timeline':[{'abilityExecutionId':'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa','amount':D('0.1234567890123456789012345678')}]},'notes':['keep'],'unknown':False}
    def test_valid_generated_guid_rename(self):
        new=copy.deepcopy(self.old);new['run']['timeline'][0]['abilityExecutionId']='bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb'
        M.exact_response(self.old,new,self.row,'a','b')
    def test_unknown_fields_are_not_masked(self):
        for mutate in [lambda x:x.update(extra=1),lambda x:x.update(unknown=0),lambda x:x['notes'].append('changed'),lambda x:x['run']['timeline'][0].update(amount=D('0.1234567890123456789012345679'))]:
            new=copy.deepcopy(self.old);mutate(new)
            with self.assertRaises(AssertionError):M.exact_response(self.old,new,self.row,'a','b')
    def test_guid_joins_splits_literal_anchors_preserved(self):
        old=copy.deepcopy(self.old);old['anchor']='aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';new=copy.deepcopy(old);new['run']['timeline'][0]['abilityExecutionId']='bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb'
        with self.assertRaises(AssertionError):M.exact_response(old,new,self.row,'a','b')
    def test_source_stamp_only_exact_expected_value(self):
        old={'sourceCommit':'old','workflowCheckoutCommit':'same','note':'unchanged'};new=dict(old,sourceCommit='new')
        M.exact_response(old,new,{'path':M.M.METADATA},'old','new')
        for changed in [dict(new,workflowCheckoutCommit='other'),dict(new,note='changed'),dict(new,sourceCommit='unexpected')]:
            with self.assertRaises(AssertionError):M.exact_response(old,changed,{'path':M.M.METADATA},'old','new')
    def test_only_real_default_path_admits_character_id(self):
        old={'character':{'id':'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa'}};new={'character':{'id':'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb'}}
        M.exact_response(old,new,{'path':'/api/sim/rogue/defaults?spec=subtlety'},'a','b')
        with self.assertRaises(AssertionError):M.exact_response(old,new,self.row,'a','b')
    def test_diagnostic_trace_is_not_silently_masked(self):
        with self.assertRaises(AssertionError):M.exact_response({'traceId':'a'},{'traceId':'b'},self.row,'a','b')
    def test_missing_null_boolean_and_array_order(self):
        for old,new in [({}, {'v':None}),({'v':False},{'v':0}),({'v':[1,2]},{'v':[2,1]})]:
            with self.assertRaises(AssertionError):M.exact_response(old,new,self.row,'a','b')
    def test_binary_float_rejected(self):
        with self.assertRaises(TypeError):M.O.encode({'amount':.1})


class RandomTests(unittest.TestCase):
    def test_known_seed_one_integer_sequence(self):
        r=M.SeededRandom(1);self.assertEqual([r.sample() for _ in range(5)],[534011718,237820880,1002897798,1657007234,1412011072]);self.assertEqual(r.count,5)
    def test_seed_sign_and_minimum_special_case(self):
        for seed in [1,100,2147483647]:
            a,b=M.SeededRandom(seed),M.SeededRandom(-seed);self.assertEqual([a.sample() for _ in range(8)],[b.sample() for _ in range(8)])
        a,b=M.SeededRandom(-2147483648),M.SeededRandom(2147483647);self.assertEqual([a.sample() for _ in range(8)],[b.sample() for _ in range(8)])
    def test_hit_avoidance_crit_draws_even_at_zero_percent(self):
        r=M.controlled(defaults(),3);rng=M.SeededRandom(1)
        result,crit,draws=M.roll(rng,r,dict(isPeriodic=False,weaponHandKey='main-hand'))
        self.assertEqual((result,crit,rng.count),('hit',False,3));self.assertEqual([x['kind'] for x in draws],['hit','avoidance','critical'])
    def test_periodic_application_and_tick_draws_are_distinct(self):
        r=M.controlled(defaults(),3);rng=M.SeededRandom(1)
        M.roll(rng,r,dict(isPeriodic=False),application=True);self.assertEqual(rng.count,2)
        M.roll(rng,r,dict(isPeriodic=True));self.assertEqual(rng.count,3)
    def test_miss_does_not_consume_avoidance_or_crit(self):
        r=M.controlled(defaults(),3);r['tuning']['mainHandHitPercent']=0;rng=M.SeededRandom(1)
        result,_,_=M.roll(rng,r,dict(isPeriodic=False));self.assertEqual((result,rng.count),('miss',1))
    def test_literal_thresholds_are_not_production_derived(self):
        self.assertEqual([3*rank for rank in range(1,6)],[3,6,9,12,15])



def minimal_trace():
    request=M.controlled(defaults(),3);request.update(seed=1,durationSeconds=1)
    eid='aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa'
    def event(kind,**fields):
        e={k:None for k in M.EVENT_FIELDS};e.update(timeSeconds=0,type=kind,encounterSequenceSelectedActorKeys=[],isCritical=False,effectDeliveryType=0,isPeriodic=False,isChannelTick=False,isInternal=False);e.update(fields);return e
    shared=dict(sourceActorKey='rogue',targetActorKey='target',abilityKey='backstab')
    events=[event(0,description='Simulation started.'),
        event(3,**shared,abilityExecutionId=eid,description='Fixture Rogue started casting Backstab.'),
        event(4,**shared,abilityExecutionId=eid,description='Fixture Rogue completed Backstab.'),
        event(8,sourceActorKey='rogue',targetActorKey='rogue',abilityKey='backstab',resourceKey='energy',amount=-60,resourceTotalAfter=40,description='Fixture Rogue spent 60 energy.'),
        event(6,**shared,abilityExecutionId=eid,effectKey='backstab-hit',schoolKey='physical',resolutionType='melee',weaponHandKey='main-hand',mitigationType='none',resultKey='hit',rawAmount=235,mitigatedAmount=0,mitigationPercent=0,blockedAmount=0,absorbedAmount=0,amount=235,description='Backstab dealt 235 damage to Level 63 Rogue training boss.'),
        event(20,**shared,effectKey='backstab-hit',amount=235,description='Rogue damage threat added to shared table.'),
        event(1,timeSeconds=1,description='Simulation ended.')]
    numeric='damageTaken healingDone healingReceived overhealingDone overhealingReceived absorptionDone absorptionReceived blockedDamageReceived'.split()
    maps='damageTakenByAbility healingDoneByAbility healingReceivedByAbility absorptionDoneByAbility absorptionReceivedByAbility blockedDamageReceivedByAbility'.split()
    ability=dict(abilityKey='backstab',castStartedCount=1,castCompletedCount=1,castCancelledCount=0,channelStartedCount=0,channelCompletedCount=0,channelCancelledCount=0,damageOccurrenceCount=1,healingOccurrenceCount=0,absorbConsumptionCount=0,criticalDamageCount=0,criticalHealingCount=0,directOccurrenceCount=1,periodicOccurrenceCount=0,channelTickOccurrenceCount=0,damageDone=235,healingDone=0,overhealingDone=0,absorptionDone=0,resultCounts={'hit':1})
    actors={}
    for key,name in [('rogue','Fixture Rogue'),('target','Level 63 Rogue training boss')]:
        actor=dict(actorKey=key,name=name,damageDone=235 if key=='rogue' else 0,damageDoneByAbility={'backstab':235} if key=='rogue' else {},abilities={'backstab':copy.deepcopy(ability)} if key=='rogue' else {},startingResources={'energy':100} if key=='rogue' else {},endingResources={'energy':100} if key=='rogue' else {},resourceStarvedSeconds=0,firstResourceStarvedAtSeconds=None,died=False,deathTimeSeconds=None)
        actor.update({k:0 for k in numeric});actor.update({k:{} for k in maps})
        if key=='target':actor.update(damageTaken=235,damageTakenByAbility={'backstab':235},blockedDamageReceivedByAbility={'backstab':0})
        actors[key]=actor
    summary=dict(seed=1,durationSeconds=1,damageDone=235,damageDoneByAbility={'backstab':235},abilities={'backstab':ability},actorSummaries=actors,primaryActorDied=False,primaryActorDeathTimeSeconds=None)
    summary.update({k:0 for k in numeric});summary.update({k:{} for k in maps})
    metrics={k:0 for k in ['rogue-expose-armor-points','rogue-stealth-openers','rogue-finisher-count','rogue-poison-procs','rogue-deadly-poison-ticks','rogue-main-hand-poison-charges','rogue-off-hand-poison-charges','rogue-preparation-cooldowns-reset','rogue-hack-and-slash-attacks','rogue-quick-recovery-energy','rogue-feint-threat-reduced','rogue-boss-attacks-dodged','rogue-boss-attacks-parried']};metrics.update({'rogue-ending-combo-points':1,'rogue-boss-threat':235})
    response=dict(specialization='subtlety',damageDone=235,dps=235,endingEnergy=100,maximumEnergy=100,maximumHealth=4000,endingHealth=4000,endingComboPoints=1,poisonProcs=0,finishersUsed=0,stealthOpeners=0,damageByAbility={'backstab':235},run=dict(summary=summary,timeline=events,processedEventCount=12,customMetrics=metrics),modelingNotes=['unchanged'])
    return request,response


class WholeTraceMutationTests(unittest.TestCase):
    def test_minimal_literal_trace(self):
        r,v=minimal_trace();frozen=copy.deepcopy(v);p=M.trace_accounting(r,v,'baseline')
        self.assertEqual((p['backstabs'],p['opportunities'],p['nonStealthAmbushes']),(1,0,0));self.assertEqual(v,frozen)
    def test_every_event_field_is_fail_closed(self):
        for field,value in [('schoolKey','fire'),('description','wrong'),('unknown',False),('sourceActorKey','other'),('targetActorKey','other'),('weaponHandKey','off-hand'),('effectKey','wrong'),('isInternal',True),('mitigationPercent',1)]:
            r,v=minimal_trace();v['run']['timeline'][4][field]=value
            with self.subTest(field=field),self.assertRaises(AssertionError):M.trace_accounting(r,v,'baseline')
    def test_damage_cannot_change_without_literal_oracle(self):
        for field in ['rawAmount','amount','mitigatedAmount']:
            r,v=minimal_trace();v['run']['timeline'][4][field]+=1
            with self.assertRaises(AssertionError):M.trace_accounting(r,v,'baseline')
    def test_resource_cp_and_threat_mutations_rejected(self):
        for mutate in [lambda v:v.update(endingEnergy=99),lambda v:v.update(endingComboPoints=2),lambda v:v['run']['timeline'][3].update(resourceTotalAfter=41),lambda v:v['run']['timeline'][5].update(amount=234)]:
            r,v=minimal_trace();mutate(v)
            with self.assertRaises(AssertionError):M.trace_accounting(r,v,'baseline')
    def test_summary_timing_and_unknown_fields_rejected(self):
        for mutate in [lambda v:v['run']['summary'].update(extra=0),lambda v:v['run']['summary'].update(healingDone=1),lambda v:v['run']['summary']['actorSummaries']['rogue'].update(resourceStarvedSeconds=1),lambda v:v['run']['summary']['actorSummaries']['target'].update(name='other'),lambda v:v['run']['summary']['abilities']['backstab'].update(castStartedCount=2)]:
            r,v=minimal_trace();mutate(v)
            with self.assertRaises(AssertionError):M.trace_accounting(r,v,'baseline')
    def test_wrong_cast_identity_rejected(self):
        r,v=minimal_trace();v['run']['timeline'][4]['abilityExecutionId']='bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb'
        with self.assertRaises(AssertionError):M.trace_accounting(r,v,'baseline')
    def test_unexplained_internal_count_difference_rejected(self):
        r,v=minimal_trace();p=M.trace_accounting(r,v,'baseline');w=copy.deepcopy(v);w['run']['processedEventCount']+=1
        with self.assertRaises(AssertionError):M.first_cause(r,v,w,p,p)
    def test_first_cause_does_not_allow_unrelated_prefix_change(self):
        r,v=minimal_trace();p=M.trace_accounting(r,v,'baseline');w=copy.deepcopy(v);w['run']['timeline'][1]['timeSeconds']=D('.1')
        with self.assertRaises(AssertionError):M.first_cause(r,v,w,p,p)
    def test_equal_trace_complete_envelope_controls(self):
        r,v=minimal_trace();p=M.trace_accounting(r,v,'baseline')
        self.assertEqual(M.first_cause(r,v,v,p,p)['kind'],'no exposed divergence')
        w=copy.deepcopy(v);w['modelingNotes'].append('changed')
        with self.assertRaises(AssertionError):M.first_cause(r,v,w,p,p)



class PositiveOpportunityTests(unittest.TestCase):
    def test_real_literal_first_probability_divergence(self):
        r,new=minimal_trace();r['seed']=142003;new['run']['summary']['seed']=142003
        old=copy.deepcopy(new);aura={k:None for k in M.EVENT_FIELDS};aura.update(timeSeconds=0,type=9,sourceActorKey='rogue',targetActorKey='rogue',abilityKey='cutthroat',effectKey='cutthroat-opportunity',auraInstanceId='bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb',encounterSequenceSelectedActorKeys=[],isCritical=False,effectDeliveryType=0,isPeriodic=False,isChannelTick=False,isInternal=False,description='Cutthroat opportunity applied to Fixture Rogue (stacks: 1).')
        old['run']['timeline'].insert(5,aura);old['run']['processedEventCount']+=1
        a=M.trace_accounting(r,old,'baseline');b=M.trace_accounting(r,new,'corrected')
        proof=M.first_cause(r,old,new,a,b);self.assertEqual(proof['kind'],'literal probability');self.assertEqual(proof['equalEventPrefix'],5)
        with self.assertRaises(AssertionError):M.trace_accounting(r,old,'corrected')
    def test_cross_domain_identity_join_rejected(self):
        r,v=minimal_trace();e=copy.deepcopy(v['run']['timeline'][0]);e.update(type=32,sourceActorKey='rogue',targetActorKey='target',abilityKey='rogue-main-hand',autoAttackInstanceId=v['run']['timeline'][1]['abilityExecutionId'],description='Fixture Rogue started rogue-main-hand against Level 63 Rogue training boss.')
        v['run']['timeline'].insert(1,e)
        with self.assertRaisesRegex(AssertionError,'Identity joined'):M.verify_event_envelopes(r,v)
    def lifecycle(self,refresh=False):
        events=[dict(type=6,abilityKey='backstab',timeSeconds=2,amount=235,isPeriodic=False),dict(type=9,abilityKey='cutthroat',timeSeconds=2,auraInstanceId='first')]
        decisions=[dict(event=0,time=2,succeeded=True)]
        if refresh:
            events.extend([dict(type=6,abilityKey='backstab',timeSeconds=5,amount=235,isPeriodic=False),dict(type=9,abilityKey='cutthroat',timeSeconds=5,auraInstanceId='second')]);decisions.append(dict(event=2,time=5,succeeded=True))
        return events,decisions
    def test_shared_success_half_open_consumption(self):
        es,ds=self.lifecycle();es.extend([dict(type=3,abilityKey='ambush',timeSeconds=D('11.999')),dict(type=4,abilityKey='ambush',timeSeconds=D('11.999')),dict(type=10,abilityKey='cutthroat',timeSeconds=D('11.999'),auraInstanceId='first')])
        M.verify_opportunity_lifecycle(es,ds,'corrected')
        es.append(dict(type=3,abilityKey='ambush',timeSeconds=12))
        with self.assertRaises(AssertionError):M.verify_opportunity_lifecycle(es,ds,'corrected')
    def test_exact_boundary_rejects_old_only_window(self):
        es,ds=self.lifecycle();es.append(dict(type=3,abilityKey='ambush',timeSeconds=12))
        M.verify_opportunity_lifecycle(es,ds,'baseline')
        with self.assertRaises(AssertionError):M.verify_opportunity_lifecycle(es,ds,'corrected')
    def test_refresh_retains_one_new_expiry_identity(self):
        es,ds=self.lifecycle(True);es.append(dict(type=10,abilityKey='cutthroat',timeSeconds=15,auraInstanceId='second'))
        M.verify_opportunity_lifecycle(es,ds,'corrected')
        for wrong in [dict(type=10,abilityKey='cutthroat',timeSeconds=12,auraInstanceId='first'),dict(type=10,abilityKey='cutthroat',timeSeconds=17,auraInstanceId='second')]:
            bad=copy.deepcopy(es);bad[-1]=wrong
            with self.assertRaises(AssertionError):M.verify_opportunity_lifecycle(bad,ds,'corrected')
    def test_unknown_opportunity_application_rejected(self):
        es,ds=self.lifecycle();ds[0]['succeeded']=False
        with self.assertRaises(AssertionError):M.verify_opportunity_lifecycle(es,ds,'corrected')
    def test_bounded_input_clock_detects_drift(self):
        r,v=minimal_trace();v['run']['timeline'][1]['timeSeconds']=D('.01')
        with self.assertRaises(AssertionError):M.verify_controlled_timing(r,v,'baseline')
    def test_integer_armor_arithmetic_proof_is_serializable(self):
        # Tuning fields arrive as JSON integer tokens. The same branch is used
        # by integer Eviscerate/periodic base values and Decimal generator values.
        amount=M.half_mitigation(640)
        self.assertIsInstance(amount,D);self.assertEqual(amount,D(320));self.assertEqual(M.O.encode({'raw':640,'amount':amount}),'{'+'"amount":320,"raw":640'+'}')
        r,v=minimal_trace();r['targetArmor']=5500;damage=v['run']['timeline'][4];damage.update(amount=D('117.5'),mitigatedAmount=D('117.5'),mitigationPercent=50,description='Backstab dealt 117.5 damage to Level 63 Rogue training boss.');v['run']['timeline'][5]['amount']=D('117.5')
        v.update(damageDone=D('117.5'),dps=D('117.5'),damageByAbility={'backstab':D('117.5')});summary=v['run']['summary'];summary.update(damageDone=D('117.5'),damageDoneByAbility={'backstab':D('117.5')});summary['abilities']['backstab']['damageDone']=D('117.5')
        rogue=summary['actorSummaries']['rogue'];rogue.update(damageDone=D('117.5'),damageDoneByAbility={'backstab':D('117.5')});rogue['abilities']['backstab']['damageDone']=D('117.5')
        target=summary['actorSummaries']['target'];target.update(damageTaken=D('117.5'),damageTakenByAbility={'backstab':D('117.5')});v['run']['customMetrics']['rogue-boss-threat']=D('117.5')
        M.O.encode(M.trace_accounting(r,v,'baseline'))


class FrozenInventoryTests(unittest.TestCase):
    def test_duplicate_truncated_and_changed_classification_fail(self):
        import tempfile
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);data=defaults();cases=M.fixtures(data,'smoke');rows=[]
            for spec in M.SPECS:
                M.write(root/f'defaults-{spec}.response.json',data[spec]);rows.append(dict(name='defaults-'+spec,path='/api/sim/rogue/defaults?spec='+spec,group='default',trace=False,baselineStatus=200,correctedStatus=200))
            rows.append(dict(name='report-metadata',path=M.M.METADATA,group='metadata',trace=False,baselineStatus=200,correctedStatus=200))
            for case in cases:
                row={k:v for k,v in case.items() if k!='body'};rows.append(row);(root/(case['name']+'.request.json')).write_bytes(M.O.encode(case['body']).encode())
            M.write(root/'summary.json',dict(passed=True,mode='smoke',requests=63));M.write(root/'index.json',rows);M.verified_inventory(root)
            variants=[rows+[rows[-1]],rows[:-1]]
            changed=copy.deepcopy(rows);changed[-1]['group']='exact';variants.append(changed)
            for invalid in variants:
                M.write(root/'index.json',invalid)
                with self.assertRaises(AssertionError):M.verified_inventory(root)
    def test_rank_four_five_body_is_not_unchecked(self):
        r=M.controlled(defaults(),4,'assassination');control=copy.deepcopy(r);control['talents']['ranks']['cutthroat']=0
        M.admission_control(r,control,{'prepared':'same'},{'prepared':'same'},4,{'path':M.CHARACTER},'stamp')
        with self.assertRaises(AssertionError):M.admission_control(r,control,{}, {'prepared':'same'},4,{'path':M.CHARACTER},'stamp')
        control['seed']=999
        with self.assertRaises(AssertionError):M.admission_control(r,control,{'prepared':'same'},{'prepared':'same'},4,{'path':M.CHARACTER},'stamp')
if __name__=='__main__':unittest.main()
