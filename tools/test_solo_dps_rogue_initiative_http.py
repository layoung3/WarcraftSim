#!/usr/bin/env python3
"""Self-contained Initiative contract mutations; no local evidence dependencies."""
import copy
from decimal import Decimal as D
import importlib.util
from pathlib import Path
import unittest
S=importlib.util.spec_from_file_location('initiative_http',Path(__file__).with_name('solo-dps-rogue-initiative-http.py'))
M=importlib.util.module_from_spec(S);S.loader.exec_module(M)
S=importlib.util.spec_from_file_location('literal_elusiveness_fixtures',Path(__file__).with_name('test_solo_dps_rogue_elusiveness_http.py'))
F=importlib.util.module_from_spec(S);S.loader.exec_module(F)


def tiny(rank=3):
    r,v=F.tiny_trace();r['talents']['ranks']={} if rank is None else {'initiative':rank};r['talents']['subtletyPointsSpent']=3
    if rank==3:v['endingComboPoints']=2;v['run']['customMetrics']['rogue-ending-combo-points']=2
    return r,v

class ContractTests(unittest.TestCase):
    def test_inventory_bounded_and_admissions(self):
        defaults=F.defaults();before=copy.deepcopy(defaults);xs=M.fixtures(defaults)
        self.assertEqual(defaults,before);self.assertEqual(len(xs),len({c['name'] for c in xs}));self.assertLess(len(xs),90)
        for c in xs:
            if c['name'].startswith('strict-') and not c['name'].startswith('strict-catalog-'):self.assertEqual((c['baselineStatus'],c['correctedStatus']),(400,400))
            if c['name'] in ['ambush-r0','garrote-r3']:self.assertEqual((c['baselineStatus'],c['correctedStatus']),(400,200))
    def test_latency_replays_and_no_cheap_shot(self):
        xs={c['name']:c for c in M.fixtures(F.defaults())}
        for latency in [0,80]:
            for kind in ['repeat','concurrent']:self.assertEqual(xs[f'{kind}-{latency}']['body'],xs[f'timing-{latency}']['body'])
        self.assertNotIn('inputLatencyMs',xs['timing-None']['body'])
        self.assertNotIn('cheap-shot',str(xs))
    def test_smoke_bounded_full_oracle_and_replay(self):
        xs=M.fixtures(F.defaults(),'smoke');self.assertEqual(len(xs),14)
        cases={c['name']:c for c in xs}
        for opener,latency in [('ambush',0),('garrote',80)]:
            for rank in [None,0,1,2,3]:
                c=cases[f'{opener}-r{rank}'];self.assertTrue(c['trace']);self.assertEqual(c['body']['inputLatencyMs'],latency);self.assertEqual(c['body']['durationSeconds'],20)
                self.assertEqual(c['correctedStatus'],200)
            c=cases[f'repeat-{opener}-r3'];self.assertEqual(c['body'],cases[c['replayOf']]['body'])
        for name in ['strict-assassination-zero','strict-combat-zero']:
            self.assertEqual((cases[name]['baselineStatus'],cases[name]['correctedStatus']),(400,400))
        strict=cases['strict-assassination-zero']['body'];self.assertEqual(strict['itemizedTalentRanks'],strict['rogue']['talents']['ranks']);self.assertEqual(strict['rogue']['talents']['assassinationPointsSpent'],sum(strict['itemizedTalentRanks'].values()));self.assertEqual(strict['itemizedTalentRanks']['initiative'],0)
        self.assertFalse(any(c.get('later') or c.get('concurrent') for c in xs))
    def test_smoke_requires_exact_canonical_rejections(self):
        cases={
            'strict-assassination-zero':{'error':'Strict historical talent maps require exact canonical reconciled keys; case aliases, whitespace and unknown keys are not accepted.'},
            'strict-combat-zero':{'valid':False,'error':'Strict Rogue ranks require unique exact canonical reconciled keys.'}}
        for name,body in cases.items():
            M.verify_smoke_rejection(name,400,body)
            for status,bad in [(200,body),(400,{'error':'Unrelated invalid build.'}),(400,dict(body,unknown=1)),(400,{})]:
                with self.assertRaises(AssertionError):M.verify_smoke_rejection(name,status,bad)
        for bad in [{'error':cases['strict-combat-zero']['error']},dict(cases['strict-combat-zero'],valid=0),dict(cases['strict-combat-zero'],valid=True)]:
            with self.assertRaises(AssertionError):M.verify_smoke_rejection('strict-combat-zero',400,bad)
    def test_literal_positive_absent_zero(self):
        for rank in [None,0,3]:
            r,v=tiny(rank);p=M.trace_accounting(r,v,'corrected');draws=[x for x in p['rngDraws'] if x['kind']=='initiative']
            self.assertEqual(len(draws),int(rank==3))
            if draws:self.assertEqual((draws[0]['before'],draws[0]['after']),(1,2))
    def test_rank_probabilities_and_capped_draw(self):
        class Rng:
            count=0
            def percent(self):self.count+=1;return D('66.5')
        for rank,expected in [(0,1),(1,1),(2,2),(3,2)]:
            rng=Rng();proof=[];self.assertEqual(M.initiative_roll(rng,rank,1,1,proof),expected);self.assertEqual(rng.count,int(rank>0))
        rng=Rng();self.assertEqual(M.initiative_roll(rng,3,1,5,[]),5);self.assertEqual(rng.count,1)
    def test_missing_extra_resource_damage_aura_and_events(self):
        for mutate in [lambda v:v['run']['timeline'].pop(4),lambda v:v['run']['timeline'].insert(5,copy.deepcopy(v['run']['timeline'][4])),lambda v:v['run']['timeline'].pop(1),lambda v:v['run']['timeline'].insert(2,copy.deepcopy(v['run']['timeline'][1])),lambda v:v['run']['timeline'].pop(5),lambda v:v['run']['timeline'].insert(6,copy.deepcopy(v['run']['timeline'][5])),lambda v:v['run']['timeline'].reverse()]:
            r,v=tiny();mutate(v)
            with self.assertRaises(AssertionError):M.trace_accounting(r,v,'corrected')
    def test_cp_damage_energy_ownership_unknowns(self):
        for mutate in [lambda v:v.update(endingComboPoints=1),lambda v:v.update(endingComboPoints=3),lambda v:v['run']['customMetrics'].update({'rogue-ending-combo-points':1}),lambda v:v.update(endingEnergy=71),lambda v:v['run']['timeline'][4].update(resourceTotalAfter=41),lambda v:v['run']['timeline'][5].update(amount=401),lambda v:v['run']['timeline'][5].update(sourceActorKey='foreign'),lambda v:v['run']['timeline'][5].update(targetActorKey='rogue'),lambda v:v['run']['timeline'][1].update(targetActorKey='target'),lambda v:v['run']['timeline'][5].update(unknown=1),lambda v:v.update(unknown=1)]:
            r,v=tiny();mutate(v)
            with self.assertRaises(AssertionError):M.trace_accounting(r,v,'corrected')
    def test_exact_decimal_order_unknown_and_identity(self):
        for a,b in [({'x':D('.1234567890123456789012345678')},{'x':D('.1234567890123456789012345679')}),({'x':True},{'x':1}),({},dict(x=None)),({'x':[1,2]},{'x':[2,1]})]:
            with self.assertRaises(AssertionError):M.exact_response(a,b,dict(path=M.RUN),'old','new')
        r,a=tiny();b=copy.deepcopy(a);b['run']['timeline'][5]['abilityExecutionId']='eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee'
        with self.assertRaises(AssertionError):M.exact_response(a,b,dict(path=M.RUN),'old','new')
    def test_finisher_cause_ignores_index_and_absolute_time(self):
        a={'finishers':[dict(event=5,key='rupture',spent=4,duration=14,expiry=18,after=0)]}
        b=copy.deepcopy(a);b['finishers'][0].update(event=7,expiry=19)
        self.assertEqual(M.finisher_signature(a),M.finisher_signature(b))
        b['finishers'][0].update(spent=5,duration=16)
        self.assertNotEqual(M.finisher_signature(a),M.finisher_signature(b))
    def test_null_map_remains_required_field_rejection(self):
        self.assertEqual(M.binding_status_transition('binding-null-map'),(400,400))
        self.assertEqual(M.binding_status_transition('binding-numeric-string'),(400,200))
        for label in ['bool','fraction','string','null']:
            self.assertEqual(M.binding_status_transition('binding-'+label),(400,400))
    def test_bind_projection_narrow(self):
        r=M.controlled(F.defaults(),1);r['talents']['ranks']['initiative']='1';self.assertEqual(M.bound_request(r)['talents']['ranks'],{'initiative':1});self.assertEqual(r['talents']['ranks']['initiative'],'1')
        r['talents']['ranks']['initiative']='2'
        with self.assertRaises(AssertionError):M.bound_request(r)

if __name__=='__main__':unittest.main()
