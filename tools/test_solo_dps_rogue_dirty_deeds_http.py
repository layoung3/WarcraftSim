#!/usr/bin/env python3
"""Offline fixture/exact-Decimal/causal-contract tests, no API/build lifecycle."""
import copy
from decimal import Decimal as D
import importlib.util
from pathlib import Path
import unittest

S=importlib.util.spec_from_file_location('dirty_deeds_http',Path(__file__).with_name('solo-dps-rogue-dirty-deeds-http.py'))
M=importlib.util.module_from_spec(S);S.loader.exec_module(M)


def defaults():
    return {s:dict(seed=142001+i,specialization=s,character=dict(id=M.ID,name='Fixture Rogue',classKey='rogue',specializationKey=s,race='human',level=60),
        tuning=dict(mutilateBonusDamagePerHand=13,sinisterStrikeBonusDamage=68,eviscerateDamagePerPoint=160,ruptureTickDamagePerPoint=24,garroteTickDamage=65,armorDamageReductionConstant=5500,improvedGarroteDamagePerRank=D('.05')),startingEnergyPercent=100)
        for i,s in enumerate(M.SPECS)}


class FixtureTests(unittest.TestCase):
    def test_no_mutation_or_duplicate(self):
        d=defaults();f=M.fixtures(d);self.assertEqual(d,defaults());self.assertEqual(len(f),len({c['name'] for c in f}))
    def test_all_legacy_specs_both_ranks_both_positions(self):
        f=[c for c in M.fixtures(defaults()) if c['name'].startswith('garrote-')]
        self.assertEqual(len(f),120)
        self.assertEqual({(c['body']['specialization'],c['body']['talents']['ranks']['dirty-deeds'],c['body']['isBehindTarget']) for c in f},
                         {(s,r,b) for s in M.SPECS for r in [1,2] for b in [True,False]})
    def test_opener_reachable_without_fake_priority(self):
        for c in M.fixtures(defaults()):
            if c['name'].startswith('garrote-'):
                r=c['body'];self.assertFalse(r['bothWeaponsDaggers']);self.assertEqual(r['mainHandWeaponType'],'sword');self.assertEqual(r['offHandWeaponType'],'sword')
                self.assertTrue(r['startInStealth']);self.assertNotIn('premeditation',r['talents']['ranks']);self.assertNotIn('rotation',r)
        self.assertNotIn('cheap-shot',M.O.encode(M.fixtures(defaults())))
    def test_exact_threshold_neighborhoods(self):
        for rank in [1,2]:
            values={c['body']['startingEnergyPercent'] for c in M.fixtures(defaults()) if c['name'].startswith('garrote-') and c['body']['talents']['ranks']['dirty-deeds']==rank}
            self.assertEqual(values,{100,50,D(50-10*rank),D(50-10*rank)-D('.01'),D(50-10*rank)+D('.01')})
    def test_no_admission_change(self):
        self.assertTrue(all(c['baselineStatus']==c['correctedStatus'] for c in M.fixtures(defaults())))
    def test_previews_unchanged_and_strict_zero_rejected(self):
        f=M.fixtures(defaults());self.assertEqual(len([c for c in f if c['name'].startswith('preview-')]),12)
        self.assertTrue(all(c['group']=='exact' for c in f if c['name'].startswith('preview-')))
        strict=[c for c in f if c['name'].startswith('strict-')];self.assertTrue(strict);self.assertTrue(all(c['baselineStatus']==400 for c in strict))
    def test_binding_has_no_body_equality_promise(self):
        cases=[c for c in M.fixtures(defaults()) if c['group']=='binding'];self.assertEqual(len(cases),6)
        self.assertTrue(all(c['baselineStatus'] is None for c in cases))
    def test_latency_is_top_level_real_and_repeated_exactly(self):
        f={c['name']:c for c in M.fixtures(defaults())}
        for l in [0,80,1000]:
            a=f[f'timing-{l}']['body'];self.assertEqual(a['inputLatencyMs'],l)
            for label in ['repeat','concurrent']:self.assertEqual(M.O.encode(a),M.O.encode(f[f'{label}-{l}']['body']))
        self.assertNotIn('inputLatencyMs',f['timing-None']['body'])
    def test_explicit_baseline_is_labeled_synthetic(self):
        for c in M.fixtures(defaults()):
            if c['name'].startswith('prepared-'):
                r=c['body'];self.assertFalse(r['useProvisionalLevel60Baseline']);self.assertEqual(r['level60Baseline']['sourceReference'],'literal-dirty-deeds-fixture-not-client-data')
    def test_smoke_bounded(self):
        self.assertLess(len(M.fixtures(defaults(),'smoke')),70)
    def test_noop_positive_does_not_hide_affected_long_run(self):
        for c in M.fixtures(defaults()):
            if c['name'].startswith('no-garrote-'):self.assertEqual(c['body']['durationSeconds'],D('.5'));self.assertTrue(c['body']['bothWeaponsDaggers'])


class ExactContractTests(unittest.TestCase):
    def test_preserves_decimal_and_unknown_fields(self):
        base={'unknown':'keep','amount':D('0.1234567890123456789012345678')}
        for new in [{'unknown':'changed','amount':base['amount']},{'unknown':'keep','amount':D('0.1234567890123456789012345679')},dict(base,extra=None)]:
            with self.assertRaises(AssertionError):M.exact_response(base,new,dict(path=M.RUN),'a','b')
    def test_diagnostic_trace_never_masked(self):
        with self.assertRaises(AssertionError):M.exact_response({'traceId':'a'},{'traceId':'b'},dict(path=M.RUN),'a','b')
    def test_boolean_null_missing_order_remain_distinct(self):
        for a,b in [({'x':False},{'x':0}),({},dict(x=None)),({'x':[1,2]},{'x':[2,1]})]:
            with self.assertRaises(AssertionError):M.exact_response(a,b,dict(path=M.RUN),'a','b')
    def test_generated_identity_bijection_only_real_paths(self):
        a='aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';b='bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb'
        old={'run':{'timeline':[{'abilityExecutionId':a},{'abilityExecutionId':a}]}}
        new={'run':{'timeline':[{'abilityExecutionId':b},{'abilityExecutionId':b}]}}
        M.exact_response(old,new,dict(path=M.RUN),'a','b')
        new['run']['timeline'][1]['abilityExecutionId']='cccccccc-cccc-4ccc-8ccc-cccccccccccc'
        with self.assertRaises(AssertionError):M.exact_response(old,new,dict(path=M.RUN),'a','b')
        with self.assertRaises(AssertionError):M.exact_response({'abilityExecutionId':a},{'abilityExecutionId':b},dict(path=M.RUN),'a','b')
    def test_literal_anchor_is_not_renamed(self):
        a='aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';b='bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb'
        old={'run':{'timeline':[{'abilityExecutionId':a}]},'anchor':a};new=copy.deepcopy(old);new['run']['timeline'][0]['abilityExecutionId']=b
        with self.assertRaises(AssertionError):M.exact_response(old,new,dict(path=M.RUN),'a','b')
    def test_only_named_get_defaults(self):
        a={'character':{'id':'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa'}};b={'character':{'id':'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb'}}
        M.exact_response(a,b,dict(path='/api/sim/rogue/defaults?spec=subtlety'),'a','b')
        for path in [M.RUN,'/api/sim/rogue/defaults?spec=subtlety&decoy=true']:
            with self.assertRaises(AssertionError):M.exact_response(a,b,dict(path=path),'a','b')
    def test_exact_stamp_only(self):
        a=dict(sourceCommit='old',workflowCheckoutCommit='keep');b=dict(sourceCommit='new',workflowCheckoutCommit='keep')
        M.exact_response(a,b,dict(path=M.M.METADATA),'old','new')
        b['workflowCheckoutCommit']='changed'
        with self.assertRaises(AssertionError):M.exact_response(a,b,dict(path=M.M.METADATA),'old','new')
    def test_binary_float_rejected(self):
        with self.assertRaises(TypeError):M.O.encode({'amount':.1})


class CauseTests(unittest.TestCase):
    def test_literal_cost_array(self):
        self.assertEqual([M.cost(r,'corrected') for r in [0,1,2]],[50,40,30]);self.assertEqual([M.cost(r,'baseline') for r in [0,1,2]],[50,50,50])
        with self.assertRaises(AssertionError):M.cost(3,'corrected')
    def pair(self):
        r=M.controlled(defaults(),2);r['durationSeconds']=D('.5')
        e=dict(type=8,abilityKey='garrote',timeSeconds=0,amount=-50,resourceTotalAfter=50,description='Fixture Rogue spent 50 energy.')
        a=dict(run=dict(timeline=[e],summary=dict(actorSummaries=dict(rogue=dict(endingResources=dict(energy=55))))),endingEnergy=55)
        b=copy.deepcopy(a);b['run']['timeline'][0].update(amount=-30,resourceTotalAfter=70,description='Fixture Rogue spent 30 energy.');b['endingEnergy']=75;b['run']['summary']['actorSummaries']['rogue']['endingResources']['energy']=75
        return r,a,b
    def test_complete_cost_only_allowance(self):
        r,a,b=self.pair();self.assertTrue(M.first_cause(r,a,b)['costOnlyCompleteEnvelope'])
    def test_cost_only_rejects_damage_notes_count_changes(self):
        for mutation in [lambda b:b.update(notes=['changed']),lambda b:b['run'].update(processedEventCount=1),lambda b:b['run']['timeline'][0].update(timeSeconds=D('.01'))]:
            r,a,b=self.pair();mutation(b)
            with self.assertRaises(AssertionError):M.first_cause(r,a,b)
    def test_summary_only_change_rejected(self):
        r,a,b=self.pair();b=copy.deepcopy(a);b['endingEnergy']+=1
        with self.assertRaises(AssertionError):M.first_cause(r,a,b)
    def test_affordability_first_predicate(self):
        r=M.controlled(defaults(),1);r['startingEnergyPercent']=40
        a={'run':{'timeline':[dict(type=1,timeSeconds=20,abilityKey=None)]}}
        b={'run':{'timeline':[dict(type=3,timeSeconds=0,abilityKey='garrote')]}}
        self.assertEqual(M.first_cause(r,a,b)['kind'],'literal affordability')
        r['startingEnergyPercent']=50
        with self.assertRaises(AssertionError):M.first_cause(r,a,b)
    def test_binding_errors_are_retained_not_equalized(self):
        a=dict(type='x',title='bad',status=400,errors={'x':['bad']},traceId='a');b=dict(a,traceId='b');row=dict(status=400)
        result=M.binding_comparison(a,b,row,row);self.assertFalse(result['bodyEqualityClaim']);self.assertEqual(result['before'],a);self.assertEqual(result['after'],b)
        b['errors']={}
        with self.assertRaises(AssertionError):M.binding_comparison(a,b,row,row)


class BindingGuardTests(unittest.TestCase):
    def test_numeric_string_common_invariants_reject_notes_and_caps(self):
        old=dict(specialization='subtlety',maximumEnergy=100,maximumHealth=4000,modelingNotes=['keep'],run={})
        M.causal_invariants(old,copy.deepcopy(old))
        for field,value in [('modelingNotes',['changed']),('maximumHealth',5000),('maximumEnergy',110)]:
            new=copy.deepcopy(old);new[field]=value
            with self.assertRaises(AssertionError):M.causal_invariants(old,new)
    def test_success_cannot_take_diagnostic_shortcut(self):
        with self.assertRaises(AssertionError):M.binding_comparison({}, {},dict(status=200),dict(status=200))


class LifecycleTests(unittest.TestCase):
    def fixture(self):
        r=dict(durationSeconds=20)
        def e(kind,t,**kw):
            return dict(type=kind,timeSeconds=D(t),abilityKey='garrote',sourceActorKey='rogue',targetActorKey='target',**kw)
        xs=[e(4,0,abilityExecutionId='execution'),e(9,0,auraInstanceId='aura')]
        xs += [e(6,i*3,abilityExecutionId='execution',isPeriodic=True) for i in range(1,7)]
        xs += [e(10,18,auraInstanceId='aura')]
        return r,dict(run=dict(timeline=xs))
    def test_six_ticks_boundary_inclusive(self):
        r,v=self.fixture();proof=M.verify_garrote_lifecycle(r,v)
        self.assertEqual(len([x for x in proof if x['action']=='tick']),6)
    def test_moved_tick_and_expiry_rejected(self):
        for index in [2,8]:
            r,v=self.fixture();v['run']['timeline'][index]['timeSeconds']+=D('.001')
            with self.assertRaises(AssertionError):M.verify_garrote_lifecycle(r,v)
    def test_missing_and_extra_tick_rejected(self):
        for extra in [False,True]:
            r,v=self.fixture()
            if extra:v['run']['timeline'].insert(3,copy.deepcopy(v['run']['timeline'][2]))
            else:v['run']['timeline'].pop(2)
            with self.assertRaises(AssertionError):M.verify_garrote_lifecycle(r,v)
    def test_wrong_source_target_execution_rejected(self):
        for field in ['sourceActorKey','targetActorKey','abilityExecutionId']:
            r,v=self.fixture();v['run']['timeline'][2][field]='other'
            with self.assertRaises(AssertionError):M.verify_garrote_lifecycle(r,v)
    def test_missing_expiry_rejected(self):
        r,v=self.fixture();v['run']['timeline'].pop()
        with self.assertRaises(AssertionError):M.verify_garrote_lifecycle(r,v)
    def test_target_death_cancels_ticks_but_not_expiry(self):
        r,v=self.fixture();xs=v['run']['timeline'];v['run']['timeline']=xs[:2]+[dict(type=13,timeSeconds=2,abilityKey='rogue-main-hand',targetActorKey='target')]+xs[-1:]
        M.verify_garrote_lifecycle(r,v)
        v['run']['timeline'][-1]['timeSeconds']+=D('.001')
        with self.assertRaises(AssertionError):M.verify_garrote_lifecycle(r,v)
    def test_half_open_run_end(self):
        r,v=self.fixture();r['durationSeconds']=18;v['run']['timeline']=v['run']['timeline'][:-2]
        M.verify_garrote_lifecycle(r,v)
    def test_refreshed_schedule_replaces_stale_ticks(self):
        r,v=self.fixture();xs=v['run']['timeline'];fresh=copy.deepcopy(xs)
        for e in fresh:
            e['timeSeconds']+=1
            if 'abilityExecutionId' in e:e['abilityExecutionId']='execution2'
            if 'auraInstanceId' in e:e['auraInstanceId']='aura2'
        v['run']['timeline']=xs[:2]+fresh
        M.verify_garrote_lifecycle(r,v)
        stale=copy.deepcopy(xs[2]);v['run']['timeline'].insert(4,stale)
        with self.assertRaises(AssertionError):M.verify_garrote_lifecycle(r,v)
    def test_missing_final_pending_tick(self):
        r,v=self.fixture();v['run']['timeline']=v['run']['timeline'][:2]
        with self.assertRaises(AssertionError):M.verify_garrote_lifecycle(r,v)


class StarvationTests(unittest.TestCase):
    def test_literal_observed_interval_oracle(self):
        casts=[(0,'garrote'),(1,'vanish'),(1,'garrote'),(D('2.5'),'slice-and-dice'),(7,'sinister-strike'),(D('11.5'),'sinister-strike'),(15,'slice-and-dice'),(D('18.5'),'sinister-strike')]
        response=dict(run=dict(timeline=[dict(type=3,timeSeconds=t,abilityKey=k) for t,k in casts]))
        actual=M.starvation_accounting(dict(durationSeconds=20,inputLatencyMs=0),response)
        self.assertEqual((actual['total'],actual['first']),(13,2));self.assertEqual(len(actual['intervals']),6)
    def test_initial_starvation_and_no_cast(self):
        self.assertEqual(M.starvation_accounting(dict(durationSeconds=D('.5'),inputLatencyMs=0),dict(run=dict(timeline=[]))),dict(total=D('.5'),first=0,intervals=[dict(start=0,end=D('.5'))]))
    def test_zero_starvation_when_input_locked(self):
        r=dict(durationSeconds=D('.5'),inputLatencyMs=80);v=dict(run=dict(timeline=[dict(type=3,timeSeconds=0,abilityKey='garrote')]))
        result=M.starvation_accounting(r,v);self.assertEqual(result['total'],0);self.assertIsNone(result['first'])
    def test_input_readiness_violation_rejected(self):
        r=dict(durationSeconds=20,inputLatencyMs=80);v=dict(run=dict(timeline=[dict(type=3,timeSeconds=0,abilityKey='garrote'),dict(type=3,timeSeconds=1,abilityKey='garrote')]))
        with self.assertRaises(AssertionError):M.starvation_accounting(r,v)
    def test_numeric_string_binding_projection_is_exact_and_nonmutating(self):
        request=M.controlled(defaults(),1);request['talents']['ranks']['dirty-deeds']='1'
        bound=M.bound_request(request);self.assertEqual(bound['talents']['ranks']['dirty-deeds'],1);self.assertEqual(request['talents']['ranks']['dirty-deeds'],'1')
        request['talents']['ranks']['dirty-deeds']='2'
        with self.assertRaises(AssertionError):M.bound_request(request)


class RetainedBroadTests(unittest.TestCase):
    def test_every_broad_balance_and_nested_schema_is_significant(self):
        root=M.ROOT/'.task-evidence/rogue-dirty-deeds-corrections-20261111/http-baseline-r1'
        if not root.exists():self.skipTest('Optional retained local evidence is not a clean-checkout dependency')
        for label in ['finite-poison','ordinary-poison','target-death','source-death']:
            name='variant-r1-'+label;r=M.read(root/(name+'.request.json'));v=M.read(root/(name+'.response.json'))
            M.general_accounting(r,v,'baseline')
            mutations=[
                lambda b:b.update(endingEnergy=b['endingEnergy']+1),
                lambda b:b.update(endingComboPoints=b['endingComboPoints']+1),
                lambda b:next(e for e in b['run']['timeline'] if e['type']==8).update(resourceTotalAfter=999),
                lambda b:b['run']['customMetrics'].update(**{'rogue-boss-threat':999}),
                lambda b:b['run']['customMetrics'].update(**{'rogue-main-hand-poison-charges':999}),
                lambda b:b['run']['summary']['actorSummaries']['target'].update(deathTimeSeconds=999),
                lambda b:b['run']['summary']['actorSummaries']['rogue'].update(extra='unknown'),
                lambda b:b['run']['timeline'][0].update(extra='unknown')]
            for kind in [6,10]:
                candidates=[e for e in v['run']['timeline'] if e['abilityKey']=='garrote' and e['type']==kind and (kind!=6 or e['isPeriodic'])]
                if candidates:
                    b=copy.deepcopy(v);e=next(e for e in b['run']['timeline'] if e['abilityKey']=='garrote' and e['type']==kind and (kind!=6 or e['isPeriodic']));at=e['timeSeconds'];e['timeSeconds']+=D('.001')
                    if kind==6:
                        next(e for e in b['run']['timeline'] if e['abilityKey']=='garrote' and e['type']==20 and e['timeSeconds']==at)['timeSeconds']+=D('.001')
                    b['run']['timeline'].sort(key=lambda e:e['timeSeconds'])
                    with self.subTest(case=label,movedKind=kind),self.assertRaises(AssertionError):M.general_accounting(r,b,'baseline')
            for index,mutation in enumerate(mutations):
                b=copy.deepcopy(v);mutation(b)
                with self.subTest(case=label,mutation=index),self.assertRaises(AssertionError):M.general_accounting(r,b,'baseline')


class RetainedTraceTests(unittest.TestCase):
    def test_retained_full_trace_timing_mutations(self):
        root=M.ROOT/'.task-evidence/rogue-cutthroat-corrections-20261111/http-corrected-r1'
        if not root.exists():self.skipTest('Optional retained local evidence is not a clean-checkout dependency')
        name='admission-subtlety-rNone-p0';r=M.read(root/(name+'.request.json'));v=M.read(root/(name+'.response.json'))
        M.trace_accounting(r,v,'baseline')
        for change in ['tick-and-threat','expiry','spend-text']:
            b=copy.deepcopy(v);events=b['run']['timeline']
            if change=='tick-and-threat':
                tick=next(e for e in events if e['type']==6 and e['abilityKey']=='garrote' and e['isPeriodic']);at=tick['timeSeconds'];tick['timeSeconds']+=D('.001')
                threat=next(e for e in events if e['type']==20 and e['abilityKey']=='garrote' and e['timeSeconds']==at);threat['timeSeconds']+=D('.001')
                events.sort(key=lambda e:e['timeSeconds'])
            elif change=='expiry':
                e=next(e for e in events if e['type']==10 and e['abilityKey']=='garrote');e['timeSeconds']+=D('.001');events.sort(key=lambda e:e['timeSeconds'])
            else:next(e for e in events if e['type']==8)['description']='Tampered'
            with self.subTest(change=change),self.assertRaises(AssertionError):M.trace_accounting(r,b,'baseline')


if __name__=='__main__':unittest.main()
