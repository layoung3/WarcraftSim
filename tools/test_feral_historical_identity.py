#!/usr/bin/env python3
"""Pure stdlib identity-oracle regressions; no HTTP, server or importer tests.

Run synthetic cases: python3 tools/test_feral_historical_identity.py -v
Add retained-real comparisons with --retained <http-feral-historical directory>.
The optional captures are read only and are never regenerated as expectations.
"""
import argparse
from collections import Counter
import copy
from decimal import Decimal
import importlib.util
from pathlib import Path
import unittest


SPEC = importlib.util.spec_from_file_location(
    'feral_historical_http', Path(__file__).with_name('feral-historical-http.py'))
ORACLE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(ORACLE)
A = '11111111-1111-4111-8111-111111111111'
B = '22222222-2222-4222-8222-222222222222'
C = '33333333-3333-4333-8333-333333333333'
D = '44444444-4444-4444-8444-444444444444'
EVENT_IDS = ('abilityExecutionId', 'autoAttackInstanceId', 'auraInstanceId',
             'absorbInstanceId', 'forcedTargetInstanceId')
RETAINED = None


def envelope(first=A, second=B):
    return {'preparation': {'preview': {'configuredRotation': {'id': first}}},
            'sim': {'run': {'timeline': [
                {'abilityExecutionId': first, 'auraInstanceId': second,
                 'amount': Decimal('0.125'), 'timeSeconds': 0},
                {'abilityExecutionId': first, 'auraInstanceId': second,
                 'amount': 0, 'timeSeconds': 1}]}}}


class SyntheticIdentityTests(unittest.TestCase):
    def assertSame(self, a, b):
        self.assertEqual(ORACLE.canonical(a), ORACLE.canonical(b))

    def assertDifferent(self, a, b):
        self.assertNotEqual(ORACLE.canonical(a), ORACLE.canonical(b))

    def test_generated_fields_at_each_approved_response_boundary(self):
        for field in EVENT_IDS:
            for nested in (False, True):
                with self.subTest(field=field, nested=nested):
                    a = {'run': {'timeline': [{field: A}, {field: A}]}}
                    b = {'run': {'timeline': [{field: C}, {field: C}]}}
                    self.assertSame({'sim': a} if nested else a,
                                    {'sim': b} if nested else b)
        for nested in (False, True):
            a = {'preview': {'configuredRotation': {'id': A}}}
            b = {'preview': {'configuredRotation': {'id': C}}}
            self.assertSame({'preparation': a} if nested else a,
                            {'preparation': b} if nested else b)

    def test_one_identity_map_covers_entire_envelope(self):
        self.assertSame(envelope(), envelope(C, D))

    def test_split_relationship_is_significant_across_envelope(self):
        changed = envelope()
        changed['preparation']['preview']['configuredRotation']['id'] = C
        self.assertDifferent(envelope(), changed)
        changed = envelope()
        changed['sim']['run']['timeline'][1]['abilityExecutionId'] = C
        self.assertDifferent(envelope(), changed)

    def test_join_relationship_is_significant(self):
        self.assertDifferent(envelope(), envelope(A, A))

    def test_provenance_and_source_guids_are_exact(self):
        for field in ('id', 'executionId', 'abilityExecutionId', 'source', 'sourceId'):
            with self.subTest(field=field):
                self.assertDifferent({'provenance': {field: A}},
                                     {'provenance': {field: C}})
        a, b = envelope(), envelope()
        a['preparation']['request'] = {'source': {'id': A}}
        b['preparation']['request'] = {'source': {'id': C}}
        self.assertDifferent(a, b)
        self.assertDifferent({'source': 'reference ' + A},
                             {'source': 'reference ' + C})

    def test_unknown_nested_paths_and_fields_are_exact(self):
        for wrapper in ('provenance', 'source', 'request', 'unknown'):
            with self.subTest(wrapper=wrapper):
                self.assertDifferent({wrapper: envelope()},
                                     {wrapper: envelope(C, D)})
        # CombatEvent has no periodicEffectInstanceId or generic id field.
        for field in ('id', 'executionId', 'periodicEffectInstanceId', 'sourceActorKey'):
            with self.subTest(field=field):
                self.assertDifferent({'run': {'timeline': [{field: A}]}},
                                     {'run': {'timeline': [{field: C}]}})
        self.assertDifferent({'configuredRotation': {'id': A}},
                             {'configuredRotation': {'id': C}})

    def test_key_shaped_paths_and_string_indices_are_not_structure(self):
        factories = (
            lambda x: {'preparation.preview.configuredRotation.id': x},
            lambda x: {'preview': {'configuredRotation.id': x}},
            lambda x: {'sim.run.timeline.0.auraInstanceId': x},
            lambda x: {'sim': {'run.timeline': [{'auraInstanceId': x}]}},
            lambda x: {'run': {'timeline': {'0': {'auraInstanceId': x}}}},
            lambda x: {'run': {'timeline[]': {'auraInstanceId': x}}},
            lambda x: {'run': {'timeline': [{'nested': {'auraInstanceId': x}}]}},
        )
        for index, factory in enumerate(factories):
            with self.subTest(decoy=index):
                self.assertDifferent(factory(A), factory(C))

    def test_guid_dictionary_keys_remain_exact(self):
        self.assertDifferent({A: 0}, {C: 0})
        self.assertDifferent({'run': {'timeline': [{A: 0}]}},
                             {'run': {'timeline': [{C: 0}]}})

    def test_array_order_remains_significant(self):
        a, b = envelope(), envelope()
        b['sim']['run']['timeline'].reverse()
        self.assertDifferent(a, b)
        self.assertDifferent({'source': [A, B]}, {'source': [B, A]})

    def test_decimal_precision_and_every_number_remain_exact(self):
        a = ORACLE.decode('{"amount":0.123456789012345678901234567890123456789}')
        b = ORACLE.decode('{"amount":0.123456789012345678901234567890123456788}')
        self.assertDifferent(a, b)
        self.assertEqual(ORACLE.canonical(a),
                         '{"amount":0.123456789012345678901234567890123456789}')
        self.assertDifferent({'amount': 9007199254740992}, {'amount': 9007199254740993})
        self.assertSame({'amount': Decimal('1.000')}, {'amount': 1})
        self.assertDifferent({'amount': Decimal('0.000000000000000000000000001')},
                             {'amount': 0})

    def test_zero_false_null_and_missing_are_distinct(self):
        values = ({}, {'value': 0}, {'value': False}, {'value': None}, {'value': '0'})
        self.assertEqual(len({ORACLE.canonical(value) for value in values}), len(values))

    def test_non_guid_identity_values_stay_exact(self):
        for value in ('', 'authored-id', 0, None, False, {}, []):
            with self.subTest(value=value):
                self.assertDifferent({'preview': {'configuredRotation': {'id': A}}},
                                     {'preview': {'configuredRotation': {'id': value}}})

    def test_generated_ids_cannot_collide_with_literal_old_markers(self):
        self.assertDifferent({'preview': {'configuredRotation': {'id': A}}},
                             {'preview': {'configuredRotation': {'id': 'ENGINE-ID-0'}}})

    def test_identity_presence_remains_significant(self):
        values = ({}, {'auraInstanceId': None}, {'auraInstanceId': A})
        results = {ORACLE.canonical({'run': {'timeline': [value]}}) for value in values}
        self.assertEqual(len(results), len(values))

    def test_object_key_order_is_not_significant(self):
        self.assertSame({'z': 0, 'a': envelope()}, {'a': envelope(), 'z': 0})

    def test_raw_encoding_never_normalizes_or_mutates_input(self):
        a = envelope()
        before = copy.deepcopy(a)
        raw = ORACLE.encode(a)
        self.assertIn(A, raw)
        self.assertIn(B, raw)
        self.assertEqual(ORACLE.decode(raw), before)
        ORACLE.canonical(a)
        self.assertEqual(a, before)

    def test_nonfinite_decimals_are_rejected(self):
        for value in ('NaN', 'Infinity', '-Infinity'):
            with self.subTest(value=value), self.assertRaises(ValueError):
                ORACLE.canonical({'amount': Decimal(value)})

    def test_guids_outside_an_envelope_are_exact(self):
        self.assertDifferent(A, C)
        self.assertDifferent([A, B], [C, D])


class RetainedRealEnvelopeTests(unittest.TestCase):
    """Original final-r5 responses, not synthetic or rewritten golden fixtures."""
    def setUp(self):
        if RETAINED is None:
            self.skipTest('Retained-real comparisons require explicit --retained captures')

    def read(self, name):
        return ORACLE.decode((RETAINED / (name + '.response.json')).read_bytes())

    def test_all_977_captures_have_only_observed_guid_paths(self):
        paths = Counter()

        def visit(value, path=()):
            if isinstance(value, dict):
                for key, child in value.items():
                    visit(child, path + (key,))
            elif isinstance(value, list):
                for child in value:
                    visit(child, path + ('[]',))
            elif isinstance(value, str) and ORACLE.GUID.fullmatch(value):
                paths['.'.join(path)] += 1

        captures = sorted(RETAINED.glob('*.response.json'))
        self.assertEqual(len(captures), 977)
        for capture in captures:
            visit(ORACLE.decode(capture.read_bytes()))
        self.assertEqual(dict(paths), {
            'preparation.preview.configuredRotation.id': 25,
            'sim.run.timeline.[].auraInstanceId': 337,
            'sim.run.timeline.[].autoAttackInstanceId': 25,
            'sim.run.timeline.[].abilityExecutionId': 1051,
            'run.timeline.[].auraInstanceId': 209,
            'run.timeline.[].autoAttackInstanceId': 17,
            'run.timeline.[].abilityExecutionId': 667,
        })

    def test_17_complete_strict_direct_result_pairs(self):
        direct_paths = sorted(RETAINED.glob('*-direct-copy.response.json'))
        self.assertEqual(len(direct_paths), 17)
        for path in direct_paths:
            name = path.name.removesuffix('-direct-copy.response.json')
            with self.subTest(pair=name):
                self.assertEqual(ORACLE.canonical(self.read(name)['sim']),
                                 ORACLE.canonical(ORACLE.decode(path.read_bytes())))

    def test_eight_complete_replay_and_concurrency_envelopes(self):
        expected = ORACLE.canonical(self.read('fixture-night-elf'))
        names = ['exact-seed-replay', 'replay-after-mutated-and-concurrent-requests']
        names += ['concurrent-' + str(index) for index in range(6)]
        for name in names:
            with self.subTest(replay=name):
                self.assertEqual(ORACLE.canonical(self.read(name)), expected)

    def test_real_preparation_boundary_replay(self):
        first = self.read('fixture-night-elf')['preparation']
        replay = self.read('exact-seed-replay')['preparation']
        self.assertNotEqual(first['preview']['configuredRotation']['id'],
                            replay['preview']['configuredRotation']['id'])
        self.assertEqual(ORACLE.canonical(first), ORACLE.canonical(replay))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--retained', type=Path,
                        help='Read-only original final-r5 http-feral-historical directory')
    parser.add_argument('-v', '--verbose', action='store_true')
    args = parser.parse_args()
    RETAINED = args.retained
    suite = unittest.defaultTestLoader.loadTestsFromTestCase(SyntheticIdentityTests)
    if RETAINED is not None:
        suite.addTests(unittest.defaultTestLoader.loadTestsFromTestCase(RetainedRealEnvelopeTests))
    result = unittest.TextTestRunner(verbosity=2 if args.verbose else 1).run(suite)
    raise SystemExit(not result.wasSuccessful())
