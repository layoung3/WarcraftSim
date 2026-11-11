#!/usr/bin/env python3
"""Pure identity/exact-JSON oracle tests; optional retained HTTP reads, never writes.

python3 tools/test_rogue_combat_identity.py --retained <Combat HTTP directory> -v
No browser, API, network, build or workflow changes are performed by this file.
"""
import argparse
import copy
from decimal import Decimal
import hashlib
import importlib.util
import json
from pathlib import Path
import unittest

SPEC=importlib.util.spec_from_file_location('rogue_combat_http',Path(__file__).with_name('rogue-combat-historical-http.py'))
ORACLE=importlib.util.module_from_spec(SPEC);SPEC.loader.exec_module(ORACLE)
A='11111111-1111-4111-8111-111111111111'
B='22222222-2222-4222-8222-222222222222'
C='33333333-3333-4333-8333-333333333333'
D='44444444-4444-4444-8444-444444444444'
ZERO='00000000-0000-0000-0000-000000000000'
MARKER='00000000-0000-4000-8000-000000000000'
FIELDS=('abilityExecutionId','autoAttackInstanceId','auraInstanceId','absorbInstanceId','forcedTargetInstanceId')
RETAINED=None

def result(first=A,second=B):
    return {'run':{'timeline':[{'abilityExecutionId':first,'auraInstanceId':second,'amount':Decimal('0.125'),'timeSeconds':0},
                              {'abilityExecutionId':first,'auraInstanceId':second,'amount':0,'timeSeconds':1}]}}


def validate_corpus_shape(summary,rows):
    expected={
        119:{'receipts':0,'completeUnchanged':0,'intentionalAdmissionChanges':0,'metadataOnly':0},
        310:{'receipts':191,'completeUnchanged':155,'intentionalAdmissionChanges':35,'metadataOnly':1},
    }
    count=summary.get('requests')
    if type(count) is not int or count not in expected or count!=len(rows):
        raise AssertionError('Retained corpus must contain exactly119 standalone or310 baseline requests matching summary/index')
    if any(not isinstance(row.get('name'),str) for row in rows):
        raise AssertionError('Retained receipt names must be strings')
    if len({row['name'] for row in rows})!=len(rows):
        raise AssertionError('Retained receipt names must be unique')
    if ORACLE.stable(summary.get('frozen'))!=ORACLE.stable(expected[count]):
        raise AssertionError('Retained frozen classification must match the selected standalone/baseline mode exactly')
    actual_frozen=sum(row['name'].startswith('frozen-') for row in rows)
    if actual_frozen!=expected[count]['receipts'] or count-actual_frozen!=119:
        raise AssertionError('Retained index must contain119 ordinary rows plus exactly the claimed0 or191 frozen rows')
    return count


def verify_capture_hashes(row,response,request):
    if hashlib.sha256(response).hexdigest()!=row['responseSha256']:
        raise AssertionError('Retained response digest differs: '+row['name'])
    expected=row['requestSha256']
    if expected is None:
        if request is not None:raise AssertionError('GET receipt unexpectedly has request bytes: '+row['name'])
    elif request is None or hashlib.sha256(request).hexdigest()!=expected:
        raise AssertionError('Retained request digest differs: '+row['name'])

class SyntheticIdentityTests(unittest.TestCase):
    def same(self,a,b):self.assertEqual(ORACLE.stable(a),ORACLE.stable(b))
    def different(self,a,b):self.assertNotEqual(ORACLE.stable(a),ORACLE.stable(b))
    def test_each_generated_field_at_only_both_supported_boundaries(self):
        for field in FIELDS:
            for wrapper in [False,True]:
                with self.subTest(field=field,wrapper=wrapper):
                    a={'run':{'timeline':[{field:A},{field:A}]}};b={'run':{'timeline':[{field:C},{field:C}]}}
                    self.same({'sim':a} if wrapper else a,{'sim':b} if wrapper else b)
    def test_one_map_preserves_relationships_across_events_and_fields(self):
        a=result(A,A);b=result(C,C);self.same(a,b)
        self.same({'sim':result()},{'sim':result(C,D)})
    def test_identity_split_is_significant(self):
        b=result();b['run']['timeline'][1]['abilityExecutionId']=C;self.different(result(),b)
    def test_identity_join_is_significant(self):self.different(result(),result(A,A))
    def test_unknown_wrappers_do_not_gain_normalization(self):
        for name in ['source','provenance','request','preparation','preview','unknown','timeline']:
            with self.subTest(name=name):self.different({name:result()},{name:result(C,D)})
    def test_nested_event_objects_arrays_and_extra_timeline_paths_stay_exact(self):
        factories=[lambda x:{'run':{'timeline':[{'nested':{'auraInstanceId':x}}]}},
                   lambda x:{'run':{'timeline':[{'nested':[{'auraInstanceId':x}]}]}},
                   lambda x:{'run':{'timeline':[[{'auraInstanceId':x}]]}},
                   lambda x:{'sim':{'sim':{'run':{'timeline':[{'auraInstanceId':x}]}}}},
                   lambda x:{'timeline':[{'auraInstanceId':x}]}]
        for i,f in enumerate(factories):
            with self.subTest(decoy=i):self.different(f(A),f(C))
    def test_dotted_keys_and_string_indices_are_not_paths(self):
        factories=[lambda x:{'sim.run.timeline.0.auraInstanceId':x},
                   lambda x:{'run.timeline':[{'auraInstanceId':x}]},
                   lambda x:{'run':{'timeline':{'0':{'auraInstanceId':x}}}},
                   lambda x:{'run':{'timeline[]':{'auraInstanceId':x}}}]
        for i,f in enumerate(factories):
            with self.subTest(decoy=i):self.different(f(A),f(C))
    def test_unrecognized_event_fields_and_provenance_remain_exact(self):
        for field in ['id','executionId','periodicEffectInstanceId','sourceActorKey','sourceId']:
            with self.subTest(field=field):self.different({'run':{'timeline':[{field:A}]}},{'run':{'timeline':[{field:C}]}})
        for field in FIELDS:
            self.different({'provenance':{field:A}},{'provenance':{field:C}})
    def test_non_guid_strings_are_never_renamed(self):
        for value in ['', 'authored-id','engine-id-1','ENGINE-ID-0',A+'x',' '+A,A.replace('-',''),'{'+A+'}']:
            with self.subTest(value=value):
                self.different({'run':{'timeline':[{'auraInstanceId':A}]}},{'run':{'timeline':[{'auraInstanceId':value}]}})
    def test_zero_guid_is_preserved_and_distinct_from_generated(self):
        value={'run':{'timeline':[{'auraInstanceId':ZERO}]}}
        self.assertIn(ZERO,ORACLE.stable(value))
        self.different(value,{'run':{'timeline':[{'auraInstanceId':A}]}})
    def test_identity_null_empty_zero_false_number_and_missing_are_distinct(self):
        values=[{},*({'auraInstanceId':x} for x in [None,'',ZERO,False,0,1,'0',{},[]])]
        canonical=[ORACLE.stable({'run':{'timeline':[value]}}) for value in values]
        self.assertEqual(len(set(canonical)),len(values))
    def test_boolean_and_number_are_distinct_everywhere(self):
        for factory in [lambda x:{'amount':x},lambda x:{'run':{'timeline':[{'amount':x}]}},lambda x:{'run':{'timeline':[{'auraInstanceId':x}]}}]:
            self.different(factory(True),factory(1));self.different(factory(False),factory(0))
    def test_guid_keys_and_outside_envelope_guids_are_exact(self):
        self.different({A:0},{C:0});self.different(A,C);self.different([A,B],[C,D])
    def test_array_order_and_unknown_numeric_fields_remain_significant(self):
        a=result();b=copy.deepcopy(a);b['run']['timeline'].reverse();self.different(a,b)
        a=result();b=copy.deepcopy(a);b['run']['timeline'][0]['newAmount']=1;self.different(a,b)
    def test_generated_guids_cannot_collide_with_old_non_guid_markers(self):
        self.different({'run':{'timeline':[{'auraInstanceId':A}]}},{'run':{'timeline':[{'auraInstanceId':'engine-id-1'}]}})
    def test_representatives_do_not_collide_with_preserved_guid_literals(self):
        a=result();a['provenance']={'literal':MARKER}
        parsed=json.loads(ORACLE.stable(a))
        self.assertEqual(parsed['provenance']['literal'],MARKER)
        self.assertNotEqual(parsed['run']['timeline'][0]['abilityExecutionId'],MARKER)
    def test_join_or_split_with_preserved_literal_guid_is_significant(self):
        a=result();a['provenance']={'literal':A};b=result(C,B);b['provenance']={'literal':A}
        self.different(a,b)
    def test_object_key_order_is_not_significant(self):self.same({'z':0,'a':result()},{'a':result(),'z':0})
    def test_canonical_output_is_a_string_and_numbers_are_exact(self):
        a={'amount':Decimal('0.123456789012345678901234567890123456789')}
        b={'amount':Decimal('0.123456789012345678901234567890123456788')}
        self.different(a,b);self.assertEqual(ORACLE.stable(a),'{"amount":0.123456789012345678901234567890123456789}')
        self.different({'amount':9007199254740992},{'amount':9007199254740993})
        self.same({'amount':Decimal('1.000')},{'amount':1})
        self.different({'amount':Decimal('0.000000000000000000000000001')},{'amount':0})
    def test_exact_input_encoding_never_normalizes_or_mutates(self):
        a=result();a['number']=Decimal('0.12345678901234567890123456789');before=copy.deepcopy(a)
        raw=ORACLE.encode(a);self.assertIn(A,raw);self.assertIn(B,raw)
        self.assertEqual(ORACLE.encode(ORACLE.decode(raw)),raw);self.assertEqual(a,before)
        ORACLE.stable(a);self.assertEqual(a,before)
    def test_nonfinite_numbers_are_rejected(self):
        for value in [Decimal('NaN'),Decimal('Infinity'),Decimal('-Infinity'),float('nan'),float('inf')]:
            with self.subTest(value=value),self.assertRaises((ValueError,TypeError)):ORACLE.stable({'amount':value})
    def test_binary_floats_are_rejected_instead_of_claimed_lossless(self):
        with self.assertRaises(TypeError):ORACLE.encode({'amount':0.1})
    def test_parser_rejects_nonfinite_json_constants(self):
        for value in ['NaN','Infinity','-Infinity']:
            with self.subTest(value=value),self.assertRaises(ValueError):ORACLE.decode('{"amount":'+value+'}')


    def test_metadata_only_expected_source_stamp_is_projected(self):
        before={'sourceCommit':'old-build','workflowCheckoutCommit':'same-checkout','schemaVersion':1}
        after={'sourceCommit':'new-build','workflowCheckoutCommit':'same-checkout','schemaVersion':1}
        a,b,receipt=ORACLE.metadata_projection(after,before,'new-build')
        self.assertEqual(ORACLE.stable(a),ORACLE.stable(b))
        self.assertEqual(receipt['before'],'old-build');self.assertEqual(receipt['after'],'new-build')
        self.assertEqual(after['sourceCommit'],'new-build');self.assertEqual(before['sourceCommit'],'old-build')
    def test_metadata_changed_checkout_is_never_masked(self):
        before={'sourceCommit':'old-build','workflowCheckoutCommit':'old-checkout'}
        after={'sourceCommit':'new-build','workflowCheckoutCommit':'new-checkout'}
        with self.assertRaises(AssertionError):ORACLE.metadata_projection(after,before,'new-build')
    def test_metadata_unexpected_or_invalid_stamp_is_rejected(self):
        before={'sourceCommit':'old-build','workflowCheckoutCommit':'same-checkout'}
        for after in [{'sourceCommit':'unexpected','workflowCheckoutCommit':'same-checkout'},
                      {'sourceCommit':'new-build','workflowCheckoutCommit':False},
                      {'sourceCommit':None,'workflowCheckoutCommit':'same-checkout'},
                      {'sourceCommit':'new-build'}]:
            with self.subTest(after=after),self.assertRaises(AssertionError):ORACLE.metadata_projection(after,before,'new-build')
    def test_metadata_other_field_and_boolean_changes_remain_significant(self):
        before={'sourceCommit':'old-build','workflowCheckoutCommit':'same-checkout','schemaVersion':1}
        after={'sourceCommit':'new-build','workflowCheckoutCommit':'same-checkout','schemaVersion':True}
        a,b,_=ORACLE.metadata_projection(after,before,'new-build');self.assertNotEqual(ORACLE.stable(a),ORACLE.stable(b))


    def test_standalone_mode_requires_exact119_and_no_frozen_classifications(self):
        rows=[{'name':str(i)} for i in range(119)]
        summary={'requests':119,'frozen':{'receipts':0,'completeUnchanged':0,'intentionalAdmissionChanges':0,'metadataOnly':0}}
        self.assertEqual(validate_corpus_shape(summary,rows),119)
        summary['frozen']['receipts']=1
        with self.assertRaises(AssertionError):validate_corpus_shape(summary,rows)
    def test_baseline_mode_requires_exact310_and_all191_classifications(self):
        rows=[{'name':('frozen-' if i>=119 else 'request-')+str(i)} for i in range(310)]
        summary={'requests':310,'frozen':{'receipts':191,'completeUnchanged':155,'intentionalAdmissionChanges':35,'metadataOnly':1}}
        self.assertEqual(validate_corpus_shape(summary,rows),310)
        for field in summary['frozen']:
            changed=copy.deepcopy(summary);changed['frozen'][field]-=1
            with self.subTest(field=field),self.assertRaises(AssertionError):validate_corpus_shape(changed,rows)
        changed=copy.deepcopy(summary);changed['frozen']['metadataOnly']=True
        with self.assertRaises(AssertionError):validate_corpus_shape(changed,rows)
    def test_corpus_rejects_summary_index_mismatch_unsupported_count_and_duplicate_names(self):
        rows=[{'name':str(i)} for i in range(119)]
        summary={'requests':119,'frozen':{'receipts':0,'completeUnchanged':0,'intentionalAdmissionChanges':0,'metadataOnly':0}}
        with self.assertRaises(AssertionError):validate_corpus_shape(summary,rows[:-1])
        for count in [0,118,120,309,311,True,'119']:
            changed=copy.deepcopy(summary);changed['requests']=count
            with self.subTest(count=count),self.assertRaises(AssertionError):validate_corpus_shape(changed,rows)
        rows[-1]['name']=rows[0]['name']
        with self.assertRaises(AssertionError):validate_corpus_shape(summary,rows)
    def test_actual_frozen_prefix_rows_must_match_the_claimed_mode(self):
        for count,frozen_count in [(119,0),(310,191)]:
            summary={'requests':count,'frozen':{'receipts':frozen_count,'completeUnchanged':155 if frozen_count else 0,
                                              'intentionalAdmissionChanges':35 if frozen_count else 0,'metadataOnly':1 if frozen_count else 0}}
            rows=[{'name':('frozen-' if i>=119 else 'request-')+str(i)} for i in range(count)]
            validate_corpus_shape(summary,rows)
            wrong=copy.deepcopy(rows);wrong[0]['name']='frozen-extra'
            with self.subTest(count=count,fault='extra-frozen'),self.assertRaises(AssertionError):validate_corpus_shape(summary,wrong)
            if frozen_count:
                wrong=copy.deepcopy(rows);wrong[-1]['name']='request-missing-frozen'
                with self.subTest(count=count,fault='missing-frozen'),self.assertRaises(AssertionError):validate_corpus_shape(summary,wrong)

    def test_retained_request_and_response_hash_corruption_is_detected(self):
        request=b'{"test":1}';response=b'{"value":2}'
        row={'name':'synthetic','requestSha256':hashlib.sha256(request).hexdigest(),'responseSha256':hashlib.sha256(response).hexdigest()}
        verify_capture_hashes(row,response,request)
        for bad_response,bad_request in [(b'{"value":3}',request),(response,b'{"test":3}'),(response,None)]:
            with self.subTest(response=bad_response,request=bad_request),self.assertRaises(AssertionError):verify_capture_hashes(row,bad_response,bad_request)
        row['requestSha256']=None;verify_capture_hashes(row,response,None)
        with self.assertRaises(AssertionError):verify_capture_hashes(row,response,request)

class RetainedRealTests(unittest.TestCase):
    def read(self,name):return ORACLE.decode((RETAINED/(name+'.response.json')).read_bytes())
    def test_retained_mode_index_and_all_request_response_digests(self):
        rows=json.loads((RETAINED/'index.json').read_text())
        summary=json.loads((RETAINED/'summary.json').read_text())
        validate_corpus_shape(summary,rows)
        for row in rows:
            with self.subTest(name=row['name']):
                request_path=RETAINED/(row['name']+'.request.json')
                verify_capture_hashes(row,(RETAINED/(row['name']+'.response.json')).read_bytes(),
                                      request_path.read_bytes() if request_path.exists() else None)
    def test_two_complete_replay_and_two_direct_shared_pairs(self):
        for points in [31,30]:
            with self.subTest(points=points):
                self.assertEqual(ORACLE.stable(self.read(f'{points}-shared')),ORACLE.stable(self.read(f'{points}-replay')))
                self.assertEqual(ORACLE.stable(self.read(f'{points}-direct')),ORACLE.stable(self.read(f'{points}-shared')['sim']))
    def test_two_exact_prepared_previews(self):
        for points in [31,30]:
            self.assertEqual(ORACLE.stable(self.read(f'{points}-preview')),ORACLE.stable(self.read(f'{points}-shared')['preparation']['preview']))
    def test_default_input_roundtrip_is_exact_decimal(self):
        original=self.read('defaults');roundtrip=ORACLE.decode(ORACLE.encode(original))
        self.assertEqual(ORACLE.encode(original),ORACLE.encode(roundtrip))
    def test_real_envelope_type_and_nested_id_faults_fail(self):
        original=self.read('31-shared')
        for path,value in [('level',True),('allocatedTalentPoints',True)]:
            changed=copy.deepcopy(original);changed[path]=value
            self.assertNotEqual(ORACLE.stable(original),ORACLE.stable(changed))
        a=copy.deepcopy(original);b=copy.deepcopy(original)
        a['sim']['run']['timeline'][0]['unknown']={'abilityExecutionId':A}
        b['sim']['run']['timeline'][0]['unknown']={'abilityExecutionId':C}
        self.assertNotEqual(ORACLE.stable(a),ORACLE.stable(b))

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--retained',type=Path);parser.add_argument('-v','--verbose',action='store_true');args=parser.parse_args();RETAINED=args.retained
    suite=unittest.defaultTestLoader.loadTestsFromTestCase(SyntheticIdentityTests)
    if RETAINED is not None:suite.addTests(unittest.defaultTestLoader.loadTestsFromTestCase(RetainedRealTests))
    result=unittest.TextTestRunner(verbosity=2 if args.verbose else 1).run(suite)
    raise SystemExit(not result.wasSuccessful())
