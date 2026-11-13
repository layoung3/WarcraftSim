"""Pure, no-network checks for the UI-stage transition whitelist."""
import copy
import importlib.util
from pathlib import Path
import unittest
import tempfile
from unittest.mock import patch

_spec = importlib.util.spec_from_file_location('stage', Path(__file__).with_name('solo-dps-combat-stage-regression.py'))
stage = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(stage)


class StageRegressionTests(unittest.TestCase):
    def compare(self, name, actual, prior):
        return stage.compare_backend(name, actual, prior, 'new-stamp', stage.expected_preset())

    def test_receipt_names_are_self_contained_without_stage_evidence(self):
        decimal_names = (
            'frozen-assassination-mutilate-override-17.25',
            'frozen-assassination-mutilate-override-17.25-preview',
            'frozen-combat-mutilate-override-17.25',
            'frozen-combat-mutilate-override-17.25-preview',
            'frozen-subtlety-mutilate-override-17.25',
            'frozen-subtlety-mutilate-override-17.25-preview',
        )
        ordinary_names = ('defaults', 'no-default', '31-shared', '30-replay',
                          'frozen-report-metadata', 'rogue-class-capability',
                          'frozen-combat-catalog-default-itemized')
        # The HTTP replay validates all 310 actual names. This pure test must
        # also work in a clean checkout without private stage receipt files.
        with tempfile.TemporaryDirectory() as directory:
            missing_baseline = Path(directory) / 'nonexistent-stage-evidence'
            self.assertFalse(missing_baseline.exists())
            with patch.object(stage, 'BASELINE', missing_baseline):
                for name in decimal_names + ordinary_names:
                    with self.subTest(name=name):
                        stage.validate_receipt_name(name)

    def test_receipt_names_forbid_traversal_hidden_paths_and_separators(self):
        for name in ('', '.', '..', '.hidden', '../escape', 'a/../b', 'a/b', 'a\\b',
                     '/absolute', 'C:\\absolute', 'a..b', 'a.', 'a\n', 'a%2fb'):
            with self.subTest(name=name), self.assertRaises(AssertionError):
                stage.validate_receipt_name(name)

    def test_four_only_transition_names_and_source_contract(self):
        self.assertEqual(len(stage.CHANGES), 4)
        preset = stage.expected_preset()
        self.assertEqual((sum(preset['ranks'].values()), preset['unspentPoints']), (31, 20))
        capability = stage.expected_capability()
        self.assertEqual(capability['specializations'], ['combat'])
        self.assertFalse(capability['hasRotationPreview'])

    def test_both_default_admissions_require_entire_exact_preset(self):
        for name in ('no-default', 'frozen-combat-catalog-default-itemized'):
            prior = {'error': 'Unsupported class/spec.'}
            good = stage.expected_preset()
            self.compare(name, good, prior)
            for bad in (dict(good, unspentPoints=19), dict(good, surprise=True)):
                with self.assertRaises(AssertionError): self.compare(name, bad, prior)
            with self.assertRaises(AssertionError): self.compare(name, good, {'error': 'different'})

    def test_itemized_note_only_and_no_input_mutation(self):
        prior = {'classKey': 'rogue', 'specialization': 'combat', 'unmodeled': [], 'note': stage.OLD_NOTE}
        actual = dict(prior, note=stage.NOTE)
        before = copy.deepcopy(prior)
        self.compare('frozen-combat-catalog-itemized-catalog', actual, prior)
        self.assertEqual(prior, before)
        with self.assertRaises(AssertionError):
            self.compare('frozen-combat-catalog-itemized-catalog', dict(actual, unmodeled=[{}]), prior)

    def test_metadata_only_expected_stamp_never_checkout(self):
        prior = {'sourceCommit': 'old-stamp', 'workflowCheckoutCommit': 'same-checkout', 'level': 60}
        actual = dict(prior, sourceCommit='new-stamp')
        self.compare('frozen-report-metadata', actual, prior)
        for field, value in [('workflowCheckoutCommit', 'different'), ('sourceCommit', 'unexpected'), ('level', 61)]:
            with self.assertRaises(AssertionError): self.compare('frozen-report-metadata', dict(actual, **{field: value}), prior)

    def test_generated_default_identity_exception_is_exactly_whitelisted(self):
        prior = {'character': {'id': '11111111-1111-1111-1111-111111111111'}, 'value': 1}
        actual = {'character': {'id': '22222222-2222-2222-2222-222222222222'}, 'value': 1}
        for name in stage.DEFAULTS: self.compare(name, actual, prior)
        for name in ('31-shared', 'unknown-defaults'):
            with self.assertRaises(AssertionError): self.compare(name, actual, prior)
        with self.assertRaises(AssertionError): self.compare('defaults', dict(actual, value=2), prior)
        with self.assertRaises(AssertionError): self.compare('defaults', {'character': {'id': stage.oracle.ZERO_GUID}, 'value': 1}, prior)

    def test_exact_decimal_and_zero_guid_semantics_survive_gate(self):
        prior = stage.oracle.decode('{"number":0.123456789012345678901}')
        actual = stage.oracle.decode('{"number":0.123456789012345678902}')
        with self.assertRaises(AssertionError): self.compare('31-shared', actual, prior)
        self.compare('31-shared', prior, prior)
        with self.assertRaises(AssertionError): self.compare('31-shared', {'number': False}, {'number': 0})
        prior = {'run': {'timeline': [{'abilityExecutionId': stage.oracle.ZERO_GUID}]}}
        actual = {'run': {'timeline': [{'abilityExecutionId': '11111111-1111-1111-1111-111111111111'}]}}
        with self.assertRaises(AssertionError): self.compare('31-shared', actual, prior)


if __name__ == '__main__':
    unittest.main()
