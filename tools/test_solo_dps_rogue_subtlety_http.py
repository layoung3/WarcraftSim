#!/usr/bin/env python3
"""Pure literal/causal/full-envelope tests. Optional immutable old HTTP reads.

No HTTP, browser, process startup, build or product edit occurs here.
"""
import argparse
import collections
import copy
from decimal import Decimal as D
import importlib.util
from pathlib import Path
import sys
import unittest

S=importlib.util.spec_from_file_location('subtlety_http',Path(__file__).with_name('solo-dps-rogue-subtlety-http.py'))
M=importlib.util.module_from_spec(S);S.loader.exec_module(M)
RETAINED=None


def request(ranks=None):
    return dict(additionalTargets=0,enableBossAttacks=False,useUtilityAbilities=False,useExposeArmor=False,
        mainHandWeaponType='sword',bothWeaponsDaggers=False,targetArmor=0,targetMaximumHealth=1000000000,durationSeconds=20,
        character=dict(baseStats={'values':{'attack-power':0}},equipment={'items':[]},buffs={'selections':[]},talents={'selections':[]}),
        talents={'ranks':ranks or {'ghostly-strike':1}},
        tuning=dict(mainHandWeaponDamage=100,mainHandSpeedSeconds=2,hemorrhageBonusDamage=45,
            ruptureTickDamagePerPoint=24,armorDamageReductionConstant=5500,lethalityBonusCriticalDamagePerRank=D('.04')))


def event(kind,key,time=0,amount=None,critical=False,execution=None,**fields):
    e=dict(type=kind,timeSeconds=time,abilityKey=key,sourceActorKey='rogue',targetActorKey='target',effectKey=key+'-hit',
           abilityExecutionId=execution,auraInstanceId=None,amount=amount,rawAmount=amount,mitigatedAmount=0,
           resultKey='critical' if critical else 'hit',isCritical=critical,isPeriodic=key=='rupture',description='')
    e.update(fields)
    return e


def damage(key,amount,time=0,critical=False,execution=None):
    name={'ghostly-strike':'Ghostly Strike','hemorrhage':'Hemorrhage','rupture':'Rupture'}[key]
    description=(f'{name} critically hit Level 63 Rogue training boss for {M.format_damage(amount)} damage.' if critical else f'{name} dealt {M.format_damage(amount)} damage to Level 63 Rogue training boss.')
    return event(6,key,time,amount,critical,execution,description=description)


def response(events,ending_cp):
    complete=[];sums=collections.defaultdict(lambda:D(0))
    for e in events:
        complete.append(e)
        if e['type']==6:
            sums[e['abilityKey']]+=e['amount']
            complete.append(event(20,e['abilityKey'],e['timeSeconds'],e['amount'],effectKey=e['effectKey'],rawAmount=None,description='Rogue damage threat added to shared table.'))
    total=sum(sums.values(),D(0));abilities={k:{'damageDone':v,'castStartedCount':0} for k,v in sums.items()}
    actor=dict(died=False,damageDone=total,damageDoneByAbility=dict(sums),abilities=copy.deepcopy(abilities))
    target=dict(died=False,damageTaken=total,damageTakenByAbility=dict(sums))
    return dict(damageDone=total,dps=M.dotnet_divide(total,20),endingComboPoints=ending_cp,endingHealth=4000,
        damageByAbility=dict(sums),modelingNotes=['Exact control'],run=dict(timeline=complete,summary=dict(damageDone=total,
        damageDoneByAbility=dict(sums),abilities=abilities,actorSummaries={'rogue':actor,'target':target}),customMetrics={'rogue-boss-threat':total}))


def rupture_fixture():
    es=[]
    for i in range(4):
        es.append(damage('hemorrhage',145,i,execution=f'hemo{i}'))
        es.append(event(9,'hemorrhage',i,effectKey='hemorrhage-mark',auraInstanceId=f'mark{i}'))
    es.extend([event(4,'rupture',4),event(9,'rupture',4,effectKey='rupture-tick',auraInstanceId='rupture'),
               damage('rupture',D('105.6'),6),damage('rupture',96,18)])
    return request({'hemorrhage':1}),response(es,0)


class LiteralOracleTests(unittest.TestCase):
    def test_ghostly_main_hand_overrides_legacy_flag(self):
        r=request();e=damage('ghostly-strike',110)
        for main,want in [('dagger',180),('sword',125),('axe',125),('mace',125),('fist',125),('other',125)]:
            for flag in [False,True]:
                with self.subTest(main=main,flag=flag):
                    r.update(mainHandWeaponType=main,bothWeaponsDaggers=flag)
                    self.assertEqual(M.expected_damage(r,e,1,False,True),(D(want),D(want),0))
                    self.assertEqual(M.expected_damage(r,e,1,False,False)[0],135 if flag else 110)
    def test_ap_crit_armor_and_lethality_literal_values(self):
        r=request();r['character']['baseStats']['values']['attack-power']=140;e=damage('ghostly-strike',0)
        for main,normal in [('dagger',216),('sword',150)]:
            r['mainHandWeaponType']=main
            self.assertEqual(M.expected_damage(r,e,1,False,True)[0],normal)
            e.update(isCritical=True,resultKey='critical');self.assertEqual(M.expected_damage(r,e,1,False,True)[0],normal*2)
            r['targetArmor']=5500;self.assertEqual(M.expected_damage(r,e,1,False,True)[1],normal)
            r['targetArmor']=0;r['talents']['ranks']['lethality']=5
            self.assertEqual(M.expected_damage(r,e,1,False,True)[0],D('475.2') if main=='dagger' else D(330))
            r['talents']['ranks'].pop('lethality');e.update(isCritical=False,resultKey='hit')
    def test_rupture_literal_cp_mark_crit(self):
        r=request({'hemorrhage':1});e=damage('rupture',0);r['targetArmor']=5500
        for cp in [1,5]:
            self.assertEqual(M.expected_damage(r,e,cp,False,True),(24*cp,24*cp,0))
            self.assertEqual(M.expected_damage(r,e,cp,True,False),(D('26.4')*cp,D('26.4')*cp,0))
            self.assertEqual(M.expected_damage(r,e,cp,True,True),(D('27.6')*cp,D('27.6')*cp,0))
            e.update(isCritical=True,resultKey='critical');self.assertEqual(M.expected_damage(r,e,cp,True,True)[0],D('55.2')*cp)
            e.update(isCritical=False,resultKey='hit')
    def test_hemo_direct_override_remains_exact(self):
        r=request({'hemorrhage':1});e=damage('hemorrhage',0)
        for bonus in [0,45,73]:
            r['tuning']['hemorrhageBonusDamage']=bonus
            self.assertEqual(M.expected_damage(r,e,1,True,False),M.expected_damage(r,e,1,True,True))
            self.assertEqual(M.expected_damage(r,e,1,True,True)[0],100+bonus)
    def test_failed_results_zero_without_changing_roll(self):
        for outcome in ['miss','dodge','parry']:
            e=damage('ghostly-strike',0);e['resultKey']=outcome
            self.assertEqual(M.expected_damage(request(),e,1,False,True),(0,0,0))
    def test_decimal_division_and_rounding_without_float(self):
        self.assertEqual(M.dotnet_divide(1,3),D('0.3333333333333333333333333333'))
        self.assertEqual(M.dotnet_divide(D('7922.2'),20),D('396.11'))
        self.assertEqual(M.format_damage(D('105.605')),'105.61')
        self.assertEqual(M.format_damage(180),'180')
    def test_decimal_json_is_exact_and_float_rejected(self):
        raw='{"value":0.12345678901234567890123456789,"bool":true,"null":null}'
        self.assertIn('0.12345678901234567890123456789',M.O.encode(M.O.decode(raw)))
        with self.assertRaises(TypeError):M.O.encode({'value':0.1})
        self.assertNotEqual(M.O.stable({'value':True}),M.O.stable({'value':1}))


class CausalContractTests(unittest.TestCase):
    def test_ghostly_reconstruction_and_nonmutation(self):
        r=request();v=response([damage('ghostly-strike',110,execution='cast1')],1);frozen=copy.deepcopy(v)
        expected,proof=M.causal_expected(r,v)
        self.assertEqual(v,frozen);self.assertEqual(expected['damageDone'],125);self.assertEqual(expected['dps'],D('6.25'))
        self.assertEqual(expected['run']['timeline'][1]['amount'],125)
        self.assertEqual(proof['targetHealth']['after'],D(1000000000)-125)
        self.assertTrue(proof['changes'])
    def test_current_mode_accepts_new_and_rejects_old_coefficient(self):
        r=request();old=response([damage('ghostly-strike',110,execution='cast1')],1)
        new=response([damage('ghostly-strike',125,execution='cast1')],1)
        expected,proof=M.causal_expected(r,new,input_corrected=True)
        self.assertEqual(expected,new);self.assertEqual(proof['changes'],[])
        with self.assertRaisesRegex(AssertionError,'Old literal oracle'):M.causal_expected(r,old,input_corrected=True)
        self.assertEqual(M.causal_expected(r,old)[0],new)
    def test_current_rupture_mode_checks_new_tick(self):
        r,old=rupture_fixture();new,_=M.causal_expected(r,old)
        self.assertEqual(M.causal_expected(r,new,input_corrected=True)[1]['changes'],[])
        with self.assertRaises(AssertionError):M.causal_expected(r,old,input_corrected=True)
    def test_default_request_guard_is_exact(self):
        defaults={'character':{'id':'generated','race':None},'durationSeconds':120,'captureTimeline':False,'tuning':{'mutilateBonusDamagePerHand':13},'useProvisionalLevel60Baseline':False}
        r=copy.deepcopy(defaults);r['captureTimeline']=True;r['character']['id']='33333333-3333-4333-8333-333333333333'
        M.verify_default_request('default-subtlety-120-0-13',r,defaults)
        for mutate in [lambda x:x.update(inputLatencyMs=0),lambda x:x['tuning'].update(mutilateBonusDamagePerHand=14),lambda x:x.update(unrecognized=True)]:
            changed=copy.deepcopy(r);mutate(changed)
            with self.assertRaises(AssertionError):M.verify_default_request('default-subtlety-120-0-13',changed,defaults)
        with self.assertRaises(AssertionError):M.verify_default_request('default-subtlety-20-0-13',r,defaults)
    def test_decimal_addition_96bit_half_even(self):
        self.assertEqual(M.dotnet_add(D('7922816251426433759354395033.5'),D('.05')),D('7922816251426433759354395034'))
        self.assertEqual(M.dotnet_add(D('10000.123456789012345678901234'),D('0.0000000000000000000000005')),D('10000.123456789012345678901234'))
    def test_live_mark_half_open_and_cp_snapshot(self):
        r,v=rupture_fixture();expected,proof=M.causal_expected(r,v)
        ticks=[p for p in proof['damageOracles'] if p['ability']=='rupture']
        self.assertEqual([p['cpSnapshot'] for p in ticks],[4,4]);self.assertEqual([p['liveOwnMark'] for p in ticks],[True,False])
        self.assertEqual([p['newAmount'] for p in ticks],[D('110.4'),96])
        self.assertEqual(expected['damageByAbility']['hemorrhage'],580)
    def test_old_literal_damage_mismatch_fails(self):
        r=request();v=response([damage('ghostly-strike',111,execution='cast1')],1)
        with self.assertRaisesRegex(AssertionError,'Old literal oracle'):M.causal_expected(r,v)
    def test_old_threat_mismatch_fails(self):
        r=request();v=response([damage('ghostly-strike',110,execution='cast1')],1);v['run']['timeline'][1]['amount']=109
        with self.assertRaisesRegex(AssertionError,'Old threat'):M.causal_expected(r,v)
    def test_missing_threat_fails(self):
        r=request();v=response([damage('ghostly-strike',110,execution='cast1')],1);v['run']['timeline'].pop()
        with self.assertRaisesRegex(AssertionError,'matching threat'):M.causal_expected(r,v)
    def test_snapshot_generator_mutation_fails(self):
        r,v=rupture_fixture();v['run']['timeline'][0]['abilityKey']='not-generator'
        with self.assertRaises(AssertionError):M.causal_expected(r,v)
    def test_unsupported_conditional_scope_fails(self):
        r=request();r['talents']['ranks']['master-of-subtlety']=1;v=response([damage('ghostly-strike',110,execution='cast1')],1)
        with self.assertRaisesRegex(AssertionError,'Unmodeled causal talent'):M.causal_expected(r,v)
    def test_unaffected_remainder_and_causal_leaves_are_strict(self):
        r,v=rupture_fixture();expected,_=M.causal_expected(r,v)
        mutators=[lambda x:x.update(endingHealth=3999),lambda x:x['modelingNotes'].append('Changed'),
                  lambda x:x['run']['timeline'][0].update(unknownField=1),lambda x:x['run']['timeline'].reverse(),
                  lambda x:x['run']['timeline'][1].update(amount=True),lambda x:x['run']['summary'].update(damageDone=1),
                  lambda x:x['run']['summary']['abilities']['rupture'].update(castStartedCount=1)]
        for mutate in mutators:
            actual=copy.deepcopy(expected);mutate(actual)
            self.assertTrue(M.differences(M.O.decode(M.O.stable(expected)),M.O.decode(M.O.stable(actual))))
    def test_generated_ids_only_normalize_with_relationships(self):
        a={'run':{'timeline':[{'abilityExecutionId':'11111111-1111-4111-8111-111111111111'},{'abilityExecutionId':'11111111-1111-4111-8111-111111111111'}]}}
        b=copy.deepcopy(a)
        for e in b['run']['timeline']:e['abilityExecutionId']='22222222-2222-4222-8222-222222222222'
        self.assertEqual(M.O.stable(a),M.O.stable(b))
        b['run']['timeline'][1]['abilityExecutionId']='33333333-3333-4333-8333-333333333333'
        self.assertNotEqual(M.O.stable(a),M.O.stable(b))


class CorpusProjectionTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        spec=importlib.util.spec_from_file_location('rogue_corpus_compare',Path(__file__).with_name('solo-dps-rogue-damage-corpus-compare.py'))
        cls.C=importlib.util.module_from_spec(spec);spec.loader.exec_module(cls.C)
    def test_character_replacement_requires_verified_origin_pair(self):
        old={'character':{'id':'old'}};new={'character':{'id':'new'}}
        self.assertEqual(len(self.C.project_character(old,new,[('character','id')],('old','new'))),1)
        self.assertEqual(old,new)
        with self.assertRaises(AssertionError):self.C.project_character(old,{'character':{'id':'unverified'}},[('character','id')],('old','new'))
    def test_projection_never_changes_unknown_guid_fields(self):
        old={'character':{'id':'old'},'unknown':'old'};new={'character':{'id':'new'},'unknown':'new'}
        self.C.project_character(old,new,[('character','id')],('old','new'))
        self.assertNotEqual(M.O.stable(old),M.O.stable(new));self.assertEqual(new['unknown'],'new')
    def test_report_projection_keeps_external_guid_anchors(self):
        a='11111111-1111-4111-8111-111111111111';b='22222222-2222-4222-8222-222222222222'
        old={'rawServerResponse':{'sim':{'run':{'timeline':[{'abilityExecutionId':a}]}}},'unknown':a}
        new=copy.deepcopy(old);new['rawServerResponse']['sim']['run']['timeline'][0]['abilityExecutionId']=b
        self.assertNotEqual(self.C.normalized_report(old),self.C.normalized_report(new))
    def test_raw_corpus_inventory_scope_is_exact(self):
        self.assertEqual(self.C.COUNTS,{'http-combat-historical':310,'http-combat-standalone':119,'http-stage-regression':317,'http-combat-ui':50,'http-combat-report':14})
        self.assertEqual(sum(self.C.COUNTS.values()),810)


class RetainedOldCaptureTests(unittest.TestCase):
    def test_all_old_causal_fixtures_and_hashes(self):
        if RETAINED is None:self.skipTest('Pass --retained for immutable old capture checks')
        rows=M.read(RETAINED/'index.json');self.assertEqual(len(rows),127);causal=0
        for row in rows:
            with self.subTest(name=row['name']):
                raw=(RETAINED/(row['name']+'.response.json')).read_bytes();self.assertEqual(M.sha(raw),row['responseSha256'])
                if row['requestSha256'] is not None:
                    req=(RETAINED/(row['name']+'.request.json')).read_bytes();self.assertEqual(M.sha(req),row['requestSha256'])
                if row['group']=='causal' or row['name'] in M.DEFAULT_CAUSAL_NAMES:
                    M.causal_expected(M.O.decode(req),M.O.decode(raw),default_control=row['name'] in M.DEFAULT_CAUSAL_NAMES);causal+=1
        self.assertEqual(causal,91)


if __name__=='__main__':
    parser=argparse.ArgumentParser(add_help=False);parser.add_argument('--retained',type=Path);a,rest=parser.parse_known_args();RETAINED=a.retained
    unittest.main(argv=[sys.argv[0],*rest])
