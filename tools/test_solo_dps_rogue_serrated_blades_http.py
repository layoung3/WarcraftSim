#!/usr/bin/env python3
"""Pure exact-Decimal, request binding, and hostile full-envelope oracle tests."""
import copy
import importlib.util
from decimal import Decimal as D
from pathlib import Path
import unittest

S=importlib.util.spec_from_file_location('serrated_http',Path(__file__).with_name('solo-dps-rogue-serrated-blades-http.py'))
M=importlib.util.module_from_spec(S);S.loader.exec_module(M)
S=importlib.util.spec_from_file_location('subtlety_test_helpers',Path(__file__).with_name('test_solo_dps_rogue_subtlety_http.py'))
T=importlib.util.module_from_spec(S);S.loader.exec_module(T)

def request():
    r=T.request({M.KEY:3});r.update(offHandWeaponType='dagger',targetArmor=100)
    r['tuning'].update(offHandWeaponDamage=40,armorDamageReductionConstant=91,armorReductionPerExposeComboPoint=25,
       hackAndSlashMaceArmorIgnorePercentPerRank=2,sinisterStrikeBonusDamage=68,backstabBonusDamage=30,
       ambushBonusDamage=150,garroteTickDamage=20,eviscerateDamagePerPoint=40)
    return r

def event(key='rogue-main-hand',**kw):
    e=T.event(6,key,amount=0,weaponHandKey='main-hand',mitigationPercent=0);e.update(kw);return e

def fixture():
    r=request();r['targetArmor']=0;r['talents']['ranks']={'hemorrhage':1}
    es=[]
    for i in range(4):
        e=T.damage('hemorrhage',145,i,execution=f'h{i}');e.update(weaponHandKey='main-hand',mitigationPercent=0);es.append(e)
        es.append(T.event(9,'hemorrhage',i,effectKey='hemorrhage-mark',auraInstanceId=f'm{i}'))
    es.extend([T.event(4,'rupture',4),T.event(9,'rupture',4,effectKey='rupture-tick',auraInstanceId='r')])
    for time,value in [(6,D('110.4')),(18,D(96))]:
        e=T.damage('rupture',value,time);e.update(weaponHandKey=None,mitigationPercent=0);es.append(e)
    return r,T.response(es,0)

class Arithmetic(unittest.TestCase):
    def test_literal_armor_all_ranks(self):
        r=request();e=event()
        for rank,armor in enumerate([100,97,94,91]):
            want=M.M.dotnet_add(100,-M.multiply(100,M.M.dotnet_divide(armor,armor+91)))
            self.assertEqual(M.expected_damage(r,e,1,False,rank)[1],want)
        self.assertEqual(M.expected_damage(r,e,1,False,3),(100,50,50,50))
    def test_rupture_cp_mark_crit_all_ranks(self):
        r=request();e=event('rupture',isPeriodic=True)
        for cp in [1,5]:
            for rank,base in enumerate(['24','26.4','28.8','31.2']):
                self.assertEqual(M.expected_damage(r,e,cp,False,rank)[1],D(base)*cp)
            for rank,base in enumerate(['27.6','30.36','33.12','35.88']):
                self.assertEqual(M.expected_damage(r,e,cp,True,rank)[1],D(base)*cp)
        e.update(isCritical=True,resultKey='critical');self.assertEqual(M.expected_damage(r,e,5,True,3)[1],D('358.8'))
    def test_owned_expose_mace_composition_and_hand(self):
        r=request();r.update(targetArmor=200,mainHandWeaponType='mace');r['talents']['ranks']['hack-and-slash']=5;r['tuning']['armorDamageReductionConstant']=D('81.9')
        self.assertEqual(M.expected_damage(r,event(),1,False,3,4)[1],50)
        self.assertNotEqual(M.expected_damage(r,event(weaponHandKey='off-hand'),1,False,3,4)[1],50)
        self.assertNotEqual(M.expected_damage(r,event(),1,False,3,0)[1],50)
        self.assertEqual(M.expected_damage(r,event(),1,False,3,10)[1],100)
    def test_cap_and_zero(self):
        r=request()
        for armor in [299,300,301]:
            r['targetArmor']=armor;out=M.expected_damage(r,event(),1,False,3)
            self.assertEqual(out[1]==25,armor>=300)
        r['targetArmor']=0;self.assertEqual(M.expected_damage(r,event(),1,False,3)[1],100)
        r['tuning']['mainHandWeaponDamage']=0;self.assertEqual(M.expected_damage(r,event(),1,False,3)[1],0)
    def test_no_damage_on_failed_roll(self):
        for result in ['miss','dodge','parry']:self.assertEqual(M.expected_damage(request(),event(resultKey=result),1,False,3),(0,0,0,0))
    def test_decimal_precision_and_rank_types(self):
        self.assertEqual(M.multiply(D('7922816251426433759354395033.5'),D('1.0')),D('7922816251426433759354395033.5'))
        for rank in [True,-1,4,D('1.5')]:
            with self.assertRaises(AssertionError):M.expected_damage(request(),event(),1,False,rank)

class WholeEnvelope(unittest.TestCase):
    def test_live_mark_expiry_and_cp(self):
        r,v=fixture();out,proof=M.causal_expected(r,v,0,3)
        self.assertEqual([p['newAmount'] for p in proof['damageOracles'] if p['ability']=='rupture'],[D('143.52'),D('124.8')])
        M.causal_expected(r,out,3,3)
    def test_mutated_numerical_state_rejected(self):
        r,v=fixture()
        for mutate in [lambda x:x['run']['timeline'].__delitem__(0),lambda x:x['run']['timeline'].insert(0,copy.deepcopy(x['run']['timeline'][0])),lambda x:x['run']['timeline'].reverse(),lambda x:x['run']['timeline'][0].update(sourceActorKey='foreign'),lambda x:x['run']['timeline'][0].update(amount=146),lambda x:x.update(damageDone=1),lambda x:x['run']['customMetrics'].update({'rogue-boss-threat':1})]:
            broken=copy.deepcopy(v);mutate(broken)
            with self.assertRaises((AssertionError,KeyError)):M.causal_expected(r,broken,0,3)
    def test_wrong_armor_and_mark_rejected(self):
        r,v=fixture();r['targetArmor']=100
        with self.assertRaises(AssertionError):M.causal_expected(r,v,0,3)
        r,v=fixture();next(e for e in v['run']['timeline'] if e['type']==9)['sourceActorKey']='foreign'
        with self.assertRaises(AssertionError):M.causal_expected(r,v,0,3)
    def test_extra_or_changed_remainder_is_not_masked(self):
        r,v=fixture();want,_=M.causal_expected(r,v,0,3);wrong=copy.deepcopy(want);wrong['modelingNotes']=['changed']
        with self.assertRaises(AssertionError):M.exact_response(want,wrong,{'path':M.RUN},'same','same')
    def test_strict_status_only_is_insufficient(self):
        row=dict(name='strict-assassination-r0-typed',path=M.SHARED,status=400)
        with self.assertRaises(AssertionError):M.verify_rejection(row,{'error':'unrelated failure'})
        self.assertTrue(M.verify_rejection(row,{'error':'Strict historical talent maps require exact canonical reconciled keys; case aliases, whitespace and unknown keys are not accepted.'}))

class SmokeContract(unittest.TestCase):
    def test_exact_smoke_inventory_reuses_full_requests(self):
        defaults={spec:dict(specialization=spec,character={'specializationKey':spec},talents={},tuning={}) for spec in M.SPECS}
        full=M.fixtures(defaults,'full');smoke=M.fixtures(defaults,'smoke')
        self.assertEqual(len(full)+4,155);self.assertEqual(len(smoke)+4,22)
        self.assertEqual(tuple(c['name'] for c in smoke),M.SMOKE_NAMES)
        by_name={c['name']:c for c in full}
        for case in smoke:self.assertEqual(M.O.encode(case),M.O.encode(by_name[case['name']]))
        with self.assertRaises(AssertionError):M.select_smoke([c for c in full if c['name']!='subtlety-r3'])
        with self.assertRaises(AssertionError):M.select_smoke(full+[full[0]])
    def test_primary_coverage_is_not_vacuous(self):
        events=[dict(type=6,amount=1,abilityKey=key,weaponHandKey=hand) for key,hand in [('rupture',None),('rogue-main-hand','main-hand'),('rogue-off-hand','off-hand'),('sinister-strike','main-hand')]]
        response={'run':{'timeline':events}};M.verify_smoke_coverage('subtlety-r3',response,{})
        for key in ['rupture','rogue-off-hand','sinister-strike']:
            bad=copy.deepcopy(response);bad['run']['timeline']=[e for e in events if e['abilityKey']!=key]
            with self.assertRaises(AssertionError):M.verify_smoke_coverage('subtlety-r3',bad,{})
    def test_mark_and_expose_coverage_required(self):
        events=[dict(type=6,amount=1,abilityKey=key,weaponHandKey=hand,timeSeconds=2) for key,hand in [('rupture',None),('rogue-main-hand','main-hand'),('rogue-off-hand','off-hand'),('hemorrhage','main-hand')]]
        response={'run':{'timeline':events}}
        with self.assertRaises(AssertionError):M.verify_smoke_coverage('hemorrhage-r3',response,{'damageOracles':[]})
        M.verify_smoke_coverage('hemorrhage-r3',response,{'damageOracles':[dict(ability='rupture',liveOwnMark=True,newAmount=1)]})
        with self.assertRaises(AssertionError):M.verify_smoke_coverage('compose-r3',response,{})
        events.append(dict(type=9,abilityKey='expose-armor',sourceActorKey='rogue',targetActorKey='target',timeSeconds=1))
        M.verify_smoke_coverage('compose-r3',response,{})
        events[-1]['timeSeconds']=3
        with self.assertRaises(AssertionError):M.verify_smoke_coverage('compose-r3',response,{})
    def test_invalid_ranks_need_exact_bodies(self):
        for rank in [-1,4]:
            name='subtlety-r'+str(rank)
            M.verify_legacy_rank_rejection(name,400,{'error':f'Unsupported or invalid Rogue talent: serrated-blades={rank}.'})
            with self.assertRaises(AssertionError):M.verify_legacy_rank_rejection(name,400,{'error':'Wrong reason'})
            with self.assertRaises(AssertionError):M.verify_legacy_rank_rejection(name,200,{'error':f'Unsupported or invalid Rogue talent: serrated-blades={rank}.'})
    def test_repeat_cannot_mask_positive_damage(self):
        r,v=fixture();positive,_=M.causal_expected(r,v,0,3);changed=copy.deepcopy(positive);changed['run']['timeline'][0]['amount']+=1
        with self.assertRaises(AssertionError):M.exact_response(positive,changed,{'path':M.RUN},'same','same')

if __name__=='__main__':unittest.main()
