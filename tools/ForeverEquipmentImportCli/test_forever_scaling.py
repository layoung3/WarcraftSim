import unittest
import forever_scaling as fs

def sparse(**x):
    base={"ID":"19102","ItemLevel":"65","OverallQualityID":"3",
          "InventoryType":"17","ItemDelay":"3100","DmgVariance":"0.4",
          "QualityModifier":"0"}
    for n in range(10):
        base[f"StatModifier_bonusStat_{n}"]="0"
        base[f"StatPercentEditor_{n}"]="0"
    base.update({k:str(v) for k,v in x.items()})
    return base

def armor_tables():
    return {
       "ItemArmorTotal": {92:{"Cloth":"200","Leather":"300","Mail":"400","Plate":"500"}},
       "ItemArmorQuality": {92:{"Quality_4":"1.5"}},
       "ArmorLocation": {5:{"Modifier_0":"0.46","Modifier_1":"0.5","Modifier_2":"0.7","Modifier_3":"0.8"}}
    }
class ScalingCandidateTests(unittest.TestCase):
    def test_stat_budget_is_half_up_and_uses_slot_quality(self):
        row=sparse(ItemLevel=65,InventoryType=5,OverallQualityID=3,
                   StatModifier_bonusStat_0=5,StatPercentEditor_0=2500,
                   StatModifier_bonusStat_1=7,StatPercentEditor_1=5000)
        total,issues=fs.derive_stats(row,{"ClassID":"4","SubclassID":"1"},
            {"RandPropPoints":{65:{"Superior_0":"102"}}})
        self.assertEqual({"intellect":26,"stamina":51},total)
        self.assertEqual([],issues)
    def test_unsupported_id_does_not_discard_independent_stat(self):
        row=sparse(ItemLevel=65,InventoryType=5,OverallQualityID=3,
                   StatModifier_bonusStat_0=5,StatPercentEditor_0=2500,
                   StatModifier_bonusStat_1=999,StatPercentEditor_1=5000)
        total,issues=fs.derive_stats(row,{"ClassID":"4","SubclassID":"1"},
            {"RandPropPoints":{65:{"Superior_0":"102"}}})
        self.assertEqual({"intellect":26},total)
        self.assertIn("unsupported-stat-id-999",issues)
    def test_chest_armor_three_table_join(self):
        row=sparse(ItemLevel=92,InventoryType=5,OverallQualityID=4)
        armor,issues=fs.derive_armor(row,{"ClassID":"4","SubclassID":"1"},armor_tables())
        self.assertEqual(138,armor)
        self.assertEqual([],issues)
    def test_robe_uses_chest_modifier(self):
        row=sparse(ItemLevel=92,InventoryType=20,OverallQualityID=4)
        armor,issues=fs.derive_armor(row,{"ClassID":"4","SubclassID":"1"},armor_tables())
        self.assertEqual(138,armor)
        self.assertEqual([],issues)
    def test_ranged_weapon_formula_floor_and_half_up(self):
        row=sparse(ItemLevel=65,InventoryType=15,OverallQualityID=3,
                   ItemDelay=2800,DmgVariance="0.5",QualityModifier=0)
        weapon,issues=fs.derive_weapon(row,{"ClassID":"2","SubclassID":"2"},
            {"ItemDamageRanged":{65:{"Quality_3":"30"}}})
        self.assertEqual(63,weapon["minimumDamage"])
        self.assertEqual(105,weapon["maximumDamage"])
        self.assertEqual(2.8,weapon["speedSeconds"])
        self.assertEqual([],issues)
    def test_caster_two_hand_without_flag_remains_unresolved(self):
        weapon,issues=fs.derive_weapon(sparse(),{"ClassID":"2","SubclassID":"10"},
            {"ItemDamageTwoHand":{65:{"Quality_3":"30"}}})
        self.assertIsNone(weapon)
        self.assertIn("caster-weapon-dps-flag-not-certified",issues)
    def test_missing_budget_does_not_generate_fake_stat(self):
        row=sparse(InventoryType=5,StatModifier_bonusStat_0=5,StatPercentEditor_0=2500)
        stats,issues=fs.derive_stats(row,{"ClassID":"4","SubclassID":"1"},{})
        self.assertEqual({},stats)
        self.assertTrue(issues)
    def test_nonarmor_ring_has_no_fabricated_armor(self):
        a,issues=fs.derive_armor(sparse(InventoryType=11),
                                {"ClassID":"4","SubclassID":"0"},armor_tables())
        self.assertIsNone(a)
        self.assertEqual([],issues)
    def test_missing_weapon_damage_budget_is_unresolved(self):
        w,issues=fs.derive_weapon(sparse(InventoryType=15),
                                 {"ClassID":"2","SubclassID":"2"},{})
        self.assertIsNone(w)
        self.assertTrue(issues)

if __name__=="__main__":
    unittest.main()
