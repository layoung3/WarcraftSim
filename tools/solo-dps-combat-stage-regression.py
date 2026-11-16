#!/usr/bin/env python3
"""Replay the accepted 310-call backend receipt without weakening its comparison policy.

No baseline is overwritten. Only the four enumerated UI-stage transitions are
allowed. Discovery is a separate seven-call phase; UI assets are not frozen here.
"""
import argparse
import copy
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import urllib.error
import urllib.parse
import urllib.request

ROOT = Path(__file__).resolve().parents[1]
_spec = importlib.util.spec_from_file_location('combat_backend_oracle', ROOT / 'tools/rogue-combat-historical-http.py')
oracle = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(oracle)  # guarded main; importing performs no HTTP
BASELINE = ROOT / '.task-evidence/rogue-combat-ui-20261111/baseline'
CHANGES = {
    'no-default': '/api/forever/talents/rogue/combat/default-itemized',
    'frozen-combat-catalog-default-itemized': '/api/forever/talents/rogue/combat/default-itemized',
    'frozen-combat-catalog-itemized-catalog': '/api/forever/talents/rogue/combat/itemized-catalog',
    'frozen-report-metadata': '/api/forever/character/report-metadata',
}
DEFAULTS = {'defaults', 'frozen-assassination-defaults', 'frozen-combat-defaults', 'frozen-subtlety-defaults'}
NOTE = ('Restricted fixed-sword Combat supports exactly eleven historical keys and a partial historical-provisional '
        '0/31/0 default allocation: 31 allocated, 20 unspent. Legal underallocations remain allowed, including '
        '0/30/0 without Adrenaline Rush. Precision retains its strict cap-three compatibility contract; '
        'no unmodeled fillers or automatic completion.')
OLD_NOTE = ('Restricted fixed-sword Combat supports exactly eleven historical keys, including the explicit '
            'Precision cap-three compatibility contract; no unmodeled fillers or curated default are available.')
DISCOVERY_NAMES = {'capabilities', 'rogue-class-capability', 'rogue-assassination-capability',
                   'rogue-combat-capability', 'simulation-specs', 'races', 'baseline-coverage'}


def require(condition, message):
    if not condition:
        raise AssertionError(message)


def validate_receipt_name(name):
    # Historical numeric tuning labels contain interior decimal points.
    # Still reject hidden basenames, separators and traversal sequences.
    require(isinstance(name, str) and re.fullmatch(r'[a-zA-Z0-9][a-zA-Z0-9_.-]*', name) is not None
            and not name.endswith('.') and '..' not in name, 'Unsafe receipt name')


def sha(data):
    return hashlib.sha256(data).hexdigest()


def expected_preset():
    value = oracle.decode((ROOT / 'WarcraftSim.Api/wwwroot/itemized-presets/rogue-combat.json').read_bytes())
    require(value['ranks'] == oracle.RANKS and value['totalRankedPoints'] == 31, 'Source preset must retain eleven exact ranks')
    require(value['allocationStatus'] == 'source-backed-partial-historical-provisional-31-ranks', 'Source preset status')
    value['unspentPoints'] = 20
    return value


def expected_capability():
    # These arrays are exact published source strings, not a response-derived oracle.
    # Restrict extraction to the one explicitly admitted constructor. All flags,
    # identities and controls remain independently specified here.
    source = (ROOT / 'WarcraftSim.Api/Simulation/ForeverClassSimulationCapabilities.cs').read_text()
    block = source.split('private static readonly ForeverClassSimulationCapability Combat = new(', 1)[1].split('\n\n', 1)[0]
    strings = re.findall(r'"(?:[^"\\]|\\.)*"', block)
    values = [json.loads(s) for s in strings]
    controls = ['target-health', 'target-armor', 'target-avoidance', 'player-maximum-health', 'starting-energy',
                'duration', 'seed', 'sword-weapon-budgets', 'weapon-poisons', 'stealth', 'positioning', 'expose-armor']
    require(values[:17] == ['rogue', 'combat', 'energy', 'combo-points', 'health'] + controls, 'Combat capability source structure changed')
    notes = values[17:]
    require(len(notes) == 7 and 'character/preview-rogue' in notes[1], 'Combat capability source notes changed')
    return dict(classKey='rogue', specializations=['combat'], resources=['energy', 'combo-points', 'health'],
                executesThroughCurrentUnifiedEndpoint=True, hasClassTalentValidation=True,
                executesArbitraryCrossTreeModeledTalents=False, hasSharedCombatItemProcessor=False,
                hasRotationPreview=False, hasExecutableCustomRotation=False, hasSeededBatchResults=False,
                encounterControls=controls, knownRestrictions=notes)


def compare_backend(name, actual, prior, stamp, preset):
    current, previous = copy.deepcopy(actual), copy.deepcopy(prior)
    detail = {'comparison': 'complete-response'}
    if name in ('no-default', 'frozen-combat-catalog-default-itemized'):
        require(oracle.stable(previous) == oracle.stable({'error': 'Unsupported class/spec.'}), 'Unexpected old default rejection')
        previous = preset
        detail['comparison'] = 'exact-31-point-default-admission'
    elif name == 'frozen-combat-catalog-itemized-catalog':
        require(previous.get('note') == OLD_NOTE, 'Unexpected old itemized note')
        previous['note'] = NOTE
        detail['comparison'] = 'itemized-note-only'
    elif name == 'frozen-report-metadata':
        current, previous, receipt = oracle.metadata_projection(current, previous, stamp)
        detail.update(comparison='source-stamp-only', sourceStamp=receipt)
    elif name in DEFAULTS:
        require(oracle.GUID.fullmatch(current['character']['id']) is not None, 'Default generated identity must be GUID')
        require(current['character']['id'] != oracle.ZERO_GUID, 'Default generated identity must be nonzero')
        # Same sole generated Character ID exception as prior frozen-default proof.
        detail['generatedCharacterId'] = {'before': previous['character']['id'], 'after': current['character']['id']}
        current['character']['id'] = previous['character']['id']
    require(oracle.stable(current) == oracle.stable(previous), f'{name}: complete response differs beyond admitted transition')
    return detail


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        raise ValueError('Redirect forbidden during loopback regression: ' + newurl)


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--base', default='http://127.0.0.1:5142')
    p.add_argument('--out', required=True)
    p.add_argument('--source-commit', default=os.environ.get('CP174_SOURCE_COMMIT') or os.environ.get('GITHUB_SHA') or 'not-stamped-local-build')
    a = p.parse_args()
    url = urllib.parse.urlsplit(a.base)
    require(url.scheme == 'http' and url.hostname in ('localhost', '127.0.0.1', '::1') and not url.username and not url.password and not url.query and not url.fragment and url.path in ('', '/'), 'Only loopback HTTP API roots are allowed')
    out = Path(a.out)
    out.mkdir(parents=True, exist_ok=False)  # never overwrite prior receipts, including failures
    receipts, failures = [], []
    counts = {'backendCalls': 0, 'backendCompleteUnchanged': 0, 'backendExpectedTransitions': 0, 'discoveryCalls': 0}
    summary = {'stage': 'combat-ui-backend310-regression', 'passed': False, 'counts': counts,
               'comparisonPolicy': oracle.NORMALIZATION, 'failures': failures,
               'baselineIndexSha256': sha((BASELINE / 'http-backend-r3/index.json').read_bytes()),
               'expectedApiSourceCommit': a.source_commit}
    def save():
        (out / 'index.json').write_text(json.dumps(receipts, indent=2) + '\n')
        (out / 'summary.json').write_text(json.dumps(summary, indent=2) + '\n')
    opener = urllib.request.build_opener(NoRedirect)
    def call(old, directory, phase, expected_status):
        name = old['name']; output_name = phase + '-' + name
        validate_receipt_name(name)
        path = old['path']; require(path.startswith('/') and not path.startswith('//'), 'Unsafe request path')
        backend = phase == 'backend'
        response_file = directory / (name + ('.response.json' if backend else '.response'))
        prior_raw = response_file.read_bytes()
        require(sha(prior_raw) == old['responseSha256' if backend else 'sha256'], 'Baseline response hash mismatch: ' + name)
        payload = None
        if backend and old['requestSha256'] is not None:
            payload = (directory / (name + '.request.json')).read_bytes()
            require(sha(payload) == old['requestSha256'], 'Baseline request hash mismatch: ' + name)
            # Send original bytes, including intentional duplicate/invalid bodies.
            (out / (output_name + '.request.json')).write_bytes(payload)
        row = dict(name=output_name, originalName=name, phase=phase, path=path,
                   method='GET' if payload is None else 'POST', expectedStatus=expected_status,
                   requestSha256=sha(payload) if payload is not None else None, baselineStatus=old['status'],
                   baselineResponseSha256=sha(prior_raw), comparisonPassed=False)
        receipts.append(row); save()
        request = urllib.request.Request(a.base.rstrip('/') + path, data=payload, headers={'Content-Type': 'application/json'})
        try:
            with opener.open(request, timeout=90) as response:
                raw, status = response.read(), response.status
        except urllib.error.HTTPError as error:
            raw, status = error.read(), error.code
        (out / (output_name + '.response.json')).write_bytes(raw)
        row.update(status=status, responseSha256=sha(raw), responseBytes=len(raw)); save()
        require(status == expected_status, f'{name}: HTTP {status}, expected {expected_status}')
        return oracle.decode(raw), oracle.decode(prior_raw), row
    try:
        index = oracle.decode((BASELINE / 'http-backend-r3/index.json').read_bytes())
        names = [x['name'] for x in index]
        require(len(names) == 310 and len(set(names)) == 310, 'Expected exactly 310 distinct accepted backend calls')
        require(set(CHANGES).issubset(names) and DEFAULTS.issubset(names), 'Accepted baseline case inventory changed')
        for old in index:
            if old['name'] in CHANGES:
                require(old['path'] == CHANGES[old['name']], 'Transition path mismatch')
                require(old['status'] == (400 if old['name'].endswith('default-itemized') or old['name'] == 'no-default' else 200), 'Transition baseline status mismatch')
        preset, capability = expected_preset(), expected_capability()
        summary['expectedPresetSha256'] = sha(oracle.encode(preset).encode())
        summary['expectedCapabilitySha256'] = sha(oracle.encode(capability).encode())
        (out / 'expected-preset.json').write_text(oracle.encode(preset) + '\n')
        (out / 'expected-capability.json').write_text(oracle.encode(capability) + '\n')
        for old in index:
            name = old['name']
            try:
                actual, prior, row = call(old, BASELINE / 'http-backend-r3', 'backend', 200 if name in CHANGES else old['status'])
                row.update(compare_backend(name, actual, prior, a.source_commit, preset), comparisonPassed=True)
                counts['backendExpectedTransitions' if name in CHANGES else 'backendCompleteUnchanged'] += 1
            except Exception as error:
                failures.append({'phase': 'backend', 'name': name, 'error': str(error)})
            counts['backendCalls'] += 1; save()
        discovery = oracle.decode((BASELINE / 'http-discovery/index.json').read_bytes())
        require(len(discovery) == 10 and {x['name'] for x in discovery} == DISCOVERY_NAMES | {'solo-html', 'solo-js', 'comparison-js'}, 'Discovery inventory changed')
        for old in discovery:
            name = old['name']
            if name not in DISCOVERY_NAMES: continue
            try:
                actual, prior, row = call(old, BASELINE / 'http-discovery', 'discovery', 200)
                expected = copy.deepcopy(prior)
                if name == 'rogue-combat-capability':
                    require(old['status'] == 400 and prior == {'error': "Unsupported rogue starting specialization 'combat'."}, 'Unexpected prior capability rejection')
                    expected = capability
                elif name == 'capabilities':
                    require(not any(x['classKey'] == 'rogue' for x in expected['specializationOverrides']), 'Combat override already in baseline')
                    expected['specializationOverrides'].append(capability)
                require(oracle.stable(actual) == oracle.stable(expected), name + ': unexpected discovery change')
                row.update(comparisonPassed=True, comparison='exact-new-override' if name == 'rogue-combat-capability' else 'append-combat-override-only' if name == 'capabilities' else 'complete-response')
            except Exception as error:
                failures.append({'phase': 'discovery', 'name': name, 'error': str(error)})
            counts['discoveryCalls'] += 1; save()
        require(counts == {'backendCalls': 310, 'backendCompleteUnchanged': 306, 'backendExpectedTransitions': 4, 'discoveryCalls': 7}, 'Acceptance counts not met')
        require(not failures, 'One or more response comparisons failed')
        summary['passed'] = True
    except Exception as error:
        failures.append({'phase': 'gate', 'error': str(error)})
    finally:
        save()
    print(json.dumps(summary, indent=2))
    return 0 if summary['passed'] else 1


if __name__ == '__main__':
    raise SystemExit(main())
