#!/usr/bin/env python3
"""Loopback-only historical/manual fixed-Cat shared HTTP acceptance.

Uses independent pinned literals, never a generated expected-results file.
All captures and receipts go beneath --out. Does not start/build a server,
access external sources, install dependencies, or exercise a browser. This is
bounded backend evidence, not current-client or complete Feral certification.
Direct-copy parity adds only inputLatencyMs=0 to align /api/sim transport
middleware with the shared endpoint; returned typed request fields stay exact.
"""
import argparse
import concurrent.futures
import copy
from decimal import Decimal
import hashlib
import json
from pathlib import Path
import re
import threading
import urllib.error
import urllib.parse
import urllib.request


SHARED = '/api/forever/character/simulate-level60'
DIRECT = '/api/sim/feral-druid/run'
TALENTS = '/api/forever/talents/druid/feral'
SOURCE_COMMIT = '1cc5ae1e9abdbf941b7326d2d90421822dce31ba'
SOURCE_URL = ('https://github.com/gunba/wow-forever-sim/blob/' + SOURCE_COMMIT
              + '/ui/core/talents/trees/druid.json')
SOURCE_STATUS = 'historical-provisional-not-current-client-certified'
INPUT_MODE = 'historical-primary-resource-proxy-plus-manual-pre-talent-cat-ap'
BASELINE_COMMIT = '7779ebbf79dc7f1341e6ab939b28a3402c9a730a'
GUID = re.compile(r'[0-9a-fA-F]{8}(-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}')

# Independent one-based source positions and disclosed full-parent convention.
# Deliberately exclude direct-runner utility keys and the two cap conflicts.
METADATA = {
    'ferocity': ('feral', 5, 1, 2, None, 0),
    'heart-of-the-wild': ('feral', 5, 1, 3, None, 0),
    'feral-swiftness': ('feral', 2, 2, 1, None, 0),
    'shredding-attacks': ('feral', 3, 3, 1, None, 0),
    'savage-fury': ('feral', 2, 3, 2, None, 0),
    'sharpened-claws': ('feral', 2, 3, 4, None, 0),
    'shifting-power': ('feral', 1, 4, 1, 'shredding-attacks', 3),
    'predatory-strikes': ('feral', 3, 4, 3, None, 0),
    'blood-frenzy': ('feral', 2, 4, 4, 'sharpened-claws', 2),
    'improved-shifting-power': ('feral', 2, 5, 1, 'shifting-power', 1),
    'leader-of-the-pack': ('feral', 1, 5, 2, None, 0),
    'natural-instinct': ('feral', 2, 5, 4, None, 0),
    'berserk': ('feral', 1, 7, 2, 'leader-of-the-pack', 1),
    'furor': ('restoration', 5, 1, 3, None, 0),
}
RANKS = {key: row[1] for key, row in METADATA.items()}
BASELINES = {
    'night-elf': (62, 65, 69, 100, 110, 2173, 2444),
    'tauren': (70, 55, 72, 95, 112, 2203, 2369),
    'troll': (66, 62, 71, 96, 111, 2193, 2384),
}
NORMALIZATION = ('exact Decimal numbers; sorted object keys; ordered arrays; '
                 'GUIDs mapped by first occurrence with identity preserved')
DIRECT_TRANSPORT_NOTE = ('Only the copied /api/sim request adds inputLatencyMs=0: '
                         'its middleware otherwise defaults to 80ms, while the '
                         '/api/forever shared endpoint uses zero. All returned '
                         'typed directRequest fields remain unchanged.')


def decode(raw):
    return json.loads(raw, parse_float=Decimal)


def encode(value, normalize_guids=False):
    """Emit exact JSON decimals; optionally normalize only generated GUID strings.

    The direct request uses normalize_guids=False: no input field is filtered,
    converted to float, or copied from a reconstructed approximation.
    """
    identities = {}

    def emit(v):
        if isinstance(v, dict):
            return '{' + ','.join(json.dumps(k) + ':' + emit(x)
                                  for k, x in sorted(v.items())) + '}'
        if isinstance(v, list):
            return '[' + ','.join(emit(x) for x in v) + ']'
        if isinstance(v, (int, Decimal)) and not isinstance(v, bool):
            number = Decimal(v)
            if not number.is_finite():
                raise ValueError('Non-finite number in JSON')
            s = format(number, 'f')
            return s.rstrip('0').rstrip('.') if '.' in s else s
        if normalize_guids and isinstance(v, str) and GUID.fullmatch(v):
            v = identities.setdefault(v, 'ENGINE-ID-' + str(len(identities)))
        return json.dumps(v, ensure_ascii=False, allow_nan=False)

    return emit(value)


def canonical(value):
    return encode(value, normalize_guids=True)


def digest(value):
    return hashlib.sha256(canonical(value).encode()).hexdigest()


def equal(actual, expected, label):
    if actual != expected:
        if isinstance(actual, str) and isinstance(expected, str) and max(len(actual), len(expected)) > 512:
            first = next((i for i, (a, e) in enumerate(zip(actual, expected)) if a != e),
                         min(len(actual), len(expected)))
            start, stop = max(0, first - 60), first + 100
            actual_hash = hashlib.sha256(actual.encode()).hexdigest()
            expected_hash = hashlib.sha256(expected.encode()).hexdigest()
            raise AssertionError(
                f'{label}: first difference at character {first}; '
                f'expected length={len(expected)} sha256={expected_hash} '
                f'context={expected[start:stop]!r}; '
                f'actual length={len(actual)} sha256={actual_hash} '
                f'context={actual[start:stop]!r}. Full HTTP captures remain in --out.')

        def brief(value):
            rendered = repr(value)
            if len(rendered) <= 800:
                return rendered
            return (rendered[:800] + f'... [repr length={len(rendered)} '
                    f'sha256={hashlib.sha256(rendered.encode()).hexdigest()}]')

        raise AssertionError(f'{label}: expected {brief(expected)}, received {brief(actual)}')


def require(condition, label):
    # Do not allow python -O to turn an acceptance gate into a false pass.
    if not condition:
        raise AssertionError(label)


def assert_runtime_caveats(notes, label):
    text = ' '.join(notes).lower()
    for phrase in ('omen', '6%', '10-second', 'unresolved',
                   'natural instinct', 'provisional', 'white', 'periodic'):
        require(phrase in text, label + ': missing inherited runtime caveat ' + phrase)


def totals(ranks):
    trees = {tree: sum(rank for key, rank in ranks.items()
                      if key in METADATA and METADATA[key][0] == tree)
             for tree in ('balance', 'feral', 'restoration')}
    points = sum(ranks.values())
    return {**{tree + 'PointsSpent': value for tree, value in trees.items()},
            'totalPoints': points, 'unspentPoints': 51 - points}


def selection(ranks=None, named=True):
    ranks = copy.deepcopy(RANKS if ranks is None else ranks)
    return {'ranks': ranks, **(totals(ranks) if named else {})}


def scenario(race='night-elf', ranks=None, named=True):
    talents = selection(ranks, named)
    return {
        'classKey': 'druid', 'specialization': 'feral',
        'itemizedTalentMode': True, 'itemizedTalentRanks': copy.deepcopy(talents['ranks']),
        'feral': {
            'character': {'classKey': 'druid', 'specializationKey': 'feral',
                          'rulesetKey': 'wow-forever', 'level': 60, 'race': race},
            'role': 'cat', 'useProvisionalLevel60Baseline': True,
            'useReviewedLevel60Baseline': False, 'level60Baseline': None,
            'manualInputs': {'preTalentCatAttackPower': 800, 'weaponDps': 75,
                             'meleeHitPercent': 100, 'meleeCritPercent': 0,
                             'shapeshiftMana': 250, 'shiftingPowerBaseMana': 1244,
                             'shiftingPowerCostReductionPercent': 0},
            'talents': talents, 'seed': 175615, 'durationSeconds': 24,
            'captureTimeline': True, 'targetLevel': 63,
            'targetInitialHealthPercent': 100, 'targetMaximumHealth': 1000000,
            'targetArmor': 0, 'startInProwl': False, 'preferredCatOpener': 'ravage',
            'targetBehind': True, 'useFaerieFire': False, 'useFeralCooldowns': False,
        },
    }


def set_at(body, path, value):
    target = body
    for key in path[:-1]:
        target = target[key]
    target[path[-1]] = value
    return body


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        raise ValueError('Acceptance gate refuses redirects: ' + newurl)


class Gate:
    def __init__(self, base, out, timeout):
        url = urllib.parse.urlsplit(base)
        require(url.scheme == 'http' and url.hostname in ('localhost', '127.0.0.1', '::1'),
                '--base must be an HTTP loopback origin')
        require(not (url.username or url.password or url.query or url.fragment)
                and url.path in ('', '/'), '--base must contain only the loopback origin')
        require(0 < timeout <= 120, '--timeout must be between zero and 120 seconds')
        self.base = base.rstrip('/')
        self.out = Path(out)
        self.out.mkdir(parents=True, exist_ok=True)
        # Do not overwrite an earlier failed/pass receipt or evidence directory.
        require(not any(self.out.iterdir()), '--out must be a new or empty directory')
        self.timeout = timeout
        self.records = []
        self.checks = []
        self.lock = threading.Lock()

    def check(self, label, details=None):
        self.checks.append({'name': label, **(details or {})})

    def request(self, name, path, body=None, expected=200, raw=None):
        require(bool(re.fullmatch(r'[a-zA-Z0-9_.-]+', name)), 'Unsafe capture name')
        require(path.startswith('/api/'), 'Only explicit local API routes are allowed')
        method = 'GET' if body is None and raw is None else 'POST'
        encoded = raw if raw is not None else (None if body is None else encode(body).encode())
        if isinstance(encoded, str):
            encoded = encoded.encode()
        if encoded is not None:
            (self.out / (name + '.request.json')).write_bytes(encoded)
        (self.out / (name + '.request-meta.json')).write_text(json.dumps({
            'method': method, 'path': path, 'expectedStatus': expected}, indent=2) + '\n')
        # Ignore proxy environment variables and refuse all redirects, including
        # redirects away from loopback. Each request owns its opener for threads.
        opener = urllib.request.build_opener(urllib.request.ProxyHandler({}), NoRedirect())
        request = urllib.request.Request(self.base + path, encoded,
                                         {'Content-Type': 'application/json'}, method=method)
        try:
            with opener.open(request, timeout=self.timeout) as response:
                status, raw_response = response.status, response.read()
        except urllib.error.HTTPError as error:
            status, raw_response = error.code, error.read()
        (self.out / (name + '.response.json')).write_bytes(raw_response)
        record = {'name': name, 'path': path, 'method': method,
                  'status': status, 'expectedStatus': expected,
                  'rawSha256': hashlib.sha256(raw_response).hexdigest()}
        with self.lock:
            self.records.append(record)
        equal(status, expected, name + ' HTTP status; body=' + repr(raw_response[:800]))
        value = decode(raw_response)
        record['sha256'] = digest(value)
        if expected == 400:
            require(isinstance(value, dict) and bool(value.get('error') or value.get('errors')),
                    name + ': expected an actionable error or model-binding errors')
            if 'valid' in value:
                equal(value['valid'], False, name + ' valid flag')
            require(not any(key in value for key in ('sim', 'preparation', 'damageDone')),
                    name + ': rejected payload must not carry simulated output')
        return value

    def reject(self, name, body):
        return self.request(name, SHARED, body, 400)

    def reject_binding(self, name, body):
        value = self.reject(name, body)
        require(isinstance(value.get('errors'), dict) and bool(value['errors']),
                name + ': expected actual HTTP model-binding errors')
        return value

    def write_receipt(self, passed, error=None):
        receipt = {
            'schema': 'warcraftsim.feral-historical-http-v1', 'passed': passed,
            'scope': 'bounded historical/manual fixed-Cat backend, not client certification',
            'base': self.base, 'normalization': NORMALIZATION,
            'sourceCommit': SOURCE_COMMIT, 'sourceUrl': SOURCE_URL,
            'sourceStatus': SOURCE_STATUS, 'fixturePoints': 36, 'fixtureUnspent': 15,
            'directTransportOverride': {'path': DIRECT, 'field': 'inputLatencyMs',
                                        'value': 0, 'reason': DIRECT_TRANSPORT_NOTE},
            'limits': [
                'Raw validation catches duplicate JSON rank keys. Typed dictionary dispatch cannot recover collapsed duplicates; no such claim is made.',
                'Fourteen admitted rank caps sum to 36; no cap-valid over-51 allocation exists. Malformed 52-point and synthetic-total probes establish rejection, not isolated budget-branch coverage.',
                'No UI, new preset, new simulation engine, observed Cat AP, racial completeness, or lower-rank Furor certification.',
            ],
            'requests': len(self.records), 'checks': self.checks,
            'records': sorted(self.records, key=lambda r: r['name']),
        }
        if error is not None:
            receipt['error'] = str(error)
        (self.out / 'receipt.json').write_text(json.dumps(receipt, indent=2) + '\n')


def discover(gate):
    capability = gate.request('capability-feral', '/api/forever/shared/capabilities/druid?specialization=feral')
    equal(capability['classKey'], 'druid', 'Feral capability class')
    equal(capability['specializations'], ['feral'], 'specialization-specific capability')
    for name in ('executesThroughCurrentUnifiedEndpoint', 'hasClassTalentValidation'):
        equal(capability[name], True, name)
    for name in ('hasRotationPreview', 'executesArbitraryCrossTreeModeledTalents', 'hasSharedCombatItemProcessor',
                 'hasExecutableCustomRotation', 'hasSeededBatchResults'):
        equal(capability[name], False, name)
    require(capability['knownRestrictions'], 'Feral restrictions must remain visible')
    assert_runtime_caveats(capability['knownRestrictions'], 'Feral capability restrictions')
    default = gate.request('capability-class-default', '/api/forever/shared/capabilities/druid')
    balance = gate.request('capability-balance', '/api/forever/shared/capabilities/druid?specialization=balance')
    equal(default['specializations'], ['balance'], 'class-only Druid remains Balance')
    equal(canonical(default), canonical(balance), 'Druid alias capability preservation')
    catalog = gate.request('catalog-feral', TALENTS)
    equal((catalog['classKey'], catalog['specialization'], catalog['level'], catalog['maxPoints']),
          ('druid', 'feral', 60, 51), 'catalog identity and budget')
    rows = {row['key']: row for row in catalog['talents']}
    equal(len(catalog['talents']), len(METADATA), 'catalog has no duplicate rows')
    equal(set(rows), set(METADATA), 'bounded fourteen canonical talent keys')
    for key, expected in METADATA.items():
        row = rows[key]
        equal(tuple(row[field] for field in ('tree', 'maxRank', 'row', 'column',
                                            'enginePrerequisiteKey', 'enginePrerequisiteRank')),
              expected, key + ' pinned metadata')
        equal(row['requiredEarlierPoints'], (expected[2] - 1) * 5, key + ' tier budget')
        equal(row['historicalValidationSourceStatus'], SOURCE_STATUS, key + ' source status')
        equal(row['historicalValidationSourceCommit'], SOURCE_COMMIT, key + ' source commit')
        equal(row['sourceUrl'], SOURCE_URL, key + ' source URL')
        equal(row['simulationStatus'], 'inactive-in-fixed-cat' if key in ('feral-swiftness', 'furor')
              else 'fixed-cat-partial', key + ' bounded simulation status')
    itemized = gate.request('catalog-itemized-feral', TALENTS + '/itemized-catalog')
    equal(itemized['unmodeled'], [], 'no historical filler admission')
    gate.request('no-feral-preset', TALENTS + '/default-itemized', expected=400)
    gate.check('capability, exact metadata, no filler/preset, and class-only Balance alias')


def validate(gate, name, talent_selection, expected=200, raw=None):
    results = []
    for suffix in ('validate', 'validate-itemized'):
        value = gate.request(name + '-' + suffix, TALENTS + '/' + suffix,
                             talent_selection, expected, raw)
        if expected == 200:
            equal(value['valid'], True, name + ' strict validation')
            points = sum(talent_selection['ranks'].values())
            equal(value['pointsSpent'], points, name + ' points')
            equal(value['remaining'], 51 - points, name + ' remaining')
            equal(value['unmodeledSelections'], [], name + ' no unmodeled selections')
            equal(value['sourceStatus'], SOURCE_STATUS, name + ' validation source')
            tree_points = totals(talent_selection['ranks'])
            for tree in ('balance', 'feral', 'restoration'):
                equal(value['treePoints'].get(tree, 0), tree_points[tree + 'PointsSpent'],
                      name + ' tree points ' + tree)
        results.append(value)
    if expected == 200:
        equal(canonical(results[0]), canonical(results[1]), name + ' validator equivalence')


def assert_preparation(value, body):
    f = body['feral']
    ranks = f['talents']['ranks']
    race = f['character']['race']
    strength, agility, stamina, intellect, spirit, health, mana = BASELINES[race]
    manual = f['manualInputs']
    equal((value['classKey'], value['specialization'], value['level']), ('druid', 'feral', 60),
          'shared result identity')
    equal(value['inputTiming']['additionalInputLatencyMilliseconds'], 0,
          'shared HTTP execution reports actual zero additional latency')
    timing_note = value['inputTiming']['note']
    for phrase in ('shared level-60 HTTP route adds zero input latency',
                   'Legacy /api/sim/ HTTP defaults to 80 ms',
                   'copied direct request with inputLatencyMs matching this receipt',
                   'No strict Feral latency override is accepted'):
        require(phrase in timing_note, 'missing input timing scope/provenance: ' + phrase)
    equal(value['allocatedTalentPoints'], sum(ranks.values()), 'shared allocated points')
    equal(value['unspentTalentPoints'], 51 - sum(ranks.values()), 'shared unspent points')
    equal(value['talentSourceStatus'], SOURCE_STATUS, 'shared talent source status')
    equal(value['talentSourceCommit'], SOURCE_COMMIT, 'shared talent source commit')
    equal(value['talentSourceUrl'], SOURCE_URL, 'shared talent source URL')
    require(value['talentModelingNotes'], 'talent modeling limitations are required')
    assert_runtime_caveats(value['talentModelingNotes'], 'shared talent modeling notes')
    p = value['preparation']
    assert_runtime_caveats(p['modelingNotes'], 'historical preparation modeling notes')
    equal(p['inputMode'], INPUT_MODE, 'outer input ownership mode')
    equal(p['preview']['inputMode'], 'authored-inclusive', 'converted legacy preview mode')
    equal(p['request']['character'], f['character'], 'actual prepared character identity')
    equal(p['request']['manualInputs'], manual, 'actual manual input identity')
    equal(p['request']['talents']['ranks'], ranks, 'actual prepared talent keys, including zeros')
    for field in totals(ranks):
        if field in f['talents']:
            equal(p['request']['talents'][field], f['talents'][field], 'prepared declared total ' + field)
        else:
            require(field not in p['request']['talents'], 'omitted declared total must stay absent: ' + field)
    for field in ('seed', 'durationSeconds', 'captureTimeline', 'role', 'targetLevel',
                  'targetInitialHealthPercent', 'targetMaximumHealth', 'targetArmor',
                  'startInProwl', 'preferredCatOpener', 'targetBehind', 'useFaerieFire', 'useFeralCooldowns'):
        equal(p['request'][field], f[field], 'prepared request ' + field)
    b = p['baseline']
    equal((b['race'], b['classKey'], b['level']), (race, 'druid', 60), 'baseline identity')
    for key, expected in zip(('strength', 'agility', 'stamina', 'intellect', 'spirit'),
                             (strength, agility, stamina, intellect, spirit)):
        equal(b[key], expected, 'independent baseline ' + key)
        equal(value['baseline'][key], expected, 'baseline summary ' + key)
    equal(b['observedUnbuffedMaximumHealth'], health, 'independent historical Health')
    equal(b['observedUnbuffedMaximumMana'], mana, 'independent historical Mana')
    equal(value['baseline']['nakedMaximumHealth'], health, 'summary Health')
    equal(value['baseline']['nakedMaximumMana'], mana, 'summary Mana')
    equal(b['observedUnbuffedMeleeAttackPower'], None, 'manual AP is not an observation')
    equal(value['baseline']['nakedMeleeAttackPower'], None, 'summary has no observed AP')
    equal(b['includesRacialPassives'], False, 'baseline does not contain racials')
    equal(b['sourceKind'], 'historical-classic-stat-proxy', 'baseline source kind')
    equal(b['verificationStatus'], 'provisional-documented-proxy', 'baseline source status')
    require(BASELINE_COMMIT in b['sourceReference'], 'retained historical baseline provenance')
    if race == 'troll':
        require('extrapolated' in b['confidenceClassification'], 'Troll extrapolation must remain explicit')
    notes = ' '.join(p['modelingNotes'])
    for phrase in ('Tauren Endurance', 'not verified Forever', 'Manual pre-talent', 'white attacks'):
        require(phrase.lower() in notes.lower(), 'Missing modeling caveat: ' + phrase)
    d = p['directRequest']
    equal(d['talents']['ranks'], ranks, 'exact converted direct talent map')
    equal(d['maximumHealth'], health, 'direct Health ownership')
    equal(d['maximumMana'], mana, 'direct Mana ownership')
    for key, expected in (('baseStrength', strength), ('baseStamina', stamina),
                          ('baseIntellect', intellect), ('attackPower', manual['preTalentCatAttackPower'])):
        equal(d['tuning'][key], expected, 'direct tuning ' + key)
    for key, expected in manual.items():
        equal(d['tuning']['attackPower' if key == 'preTalentCatAttackPower' else key],
              expected, 'manual tuning roundtrip ' + key)
    for field in ('seed', 'durationSeconds', 'captureTimeline', 'role', 'targetMaximumHealth',
                  'targetArmor', 'startInProwl', 'preferredCatOpener', 'targetBehind',
                  'useFaerieFire', 'useFeralCooldowns'):
        equal(d[field], f[field], 'direct scenario ' + field)
    for field in ('enableBossAttacks', 'useThorns', 'useBearUtility'):
        equal(d[field], False, 'no unsupported ' + field)
    equal(d['additionalTargets'], 0, 'one target')
    equal(d['formTransitions'], [], 'fixed Cat')
    equal(d['targetTransitions'], [], 'fixed target')
    equal(d['initialComboPointsByTarget'], {}, 'no seeded points')
    hotw = ranks.get('heart-of-the-wild', 0)
    predatory = ranks.get('predatory-strikes', 0)
    hotw_ap = strength * Decimal('.04') * hotw
    maximum_mana = mana + intellect * Decimal('.30') * hotw
    preview = p['preview']
    for key, expected in {
        'authoredAttackPower': manual['preTalentCatAttackPower'],
        'heartOfTheWildAttackPowerIncrement': hotw_ap,
        'predatoryStrikesAttackPower': 30 * predatory,
        'spellbookAttackPower': manual['preTalentCatAttackPower'] + hotw_ap + 30 * predatory,
        'maximumHealth': health, 'maximumMana': maximum_mana,
        'initialShiftManaCost': manual['shapeshiftMana'],
        'manaAfterInitialShift': maximum_mana - manual['shapeshiftMana'],
        'initialForm': 'cat', 'maximumEnergy': 100, 'startingEnergy': 0, 'startingRage': 0,
    }.items():
        equal(preview[key], expected, 'independent preview ' + key)
    sim = value['sim']
    equal(sim['damageDone'], sum(sim['damageByAbility'].values()), 'damage reconciliation')
    require(sim['run']['timeline'], 'actual shared execution must capture a timeline')
    metrics = sim['run']['customMetrics']
    equal(metrics['feral-heart-of-the-wild-attack-power'], manual['preTalentCatAttackPower'] + hotw_ap,
          'actual execution HOTW-only AP metric')
    equal(metrics['feral-heart-of-the-wild-maximum-health'], health, 'actual execution Health')
    equal(metrics['feral-heart-of-the-wild-maximum-mana'], maximum_mana, 'actual execution Mana')


def run_parity(gate, name, body):
    before = encode(body)
    shared = gate.request(name, SHARED, body)
    assert_preparation(shared, body)
    returned_direct = shared['preparation']['directRequest']
    returned_before = encode(returned_direct)
    copied_direct = copy.deepcopy(returned_direct)
    require('inputLatencyMs' not in copied_direct, 'transport override must not replace a typed field')
    # Program.cs applies latency middleware only to /api/sim, defaulting to
    # 80ms. Match shared execution's zero latency without changing any typed
    # field, filtering a result, or permitting this knob in strict Feral input.
    copied_direct['inputLatencyMs'] = 0
    without_transport = copy.deepcopy(copied_direct)
    equal(without_transport.pop('inputLatencyMs'), 0, 'single direct transport override')
    equal(encode(without_transport), returned_before, 'all returned direct request fields copied exactly')
    direct = gate.request(name + '-direct-copy', DIRECT, copied_direct)
    equal(encode(returned_direct), returned_before, 'returned direct request remains unchanged')
    equal(canonical(shared['sim']), canonical(direct),
          name + ' whole-result direct-copy parity with zero-latency transport alignment')
    equal(encode(body), before, name + ' unchanged caller request')
    gate.check(name + '-whole-result-parity-zero-latency-transport', {
        'wholeResultSha256': digest(direct),
        'preparedInputSha256': digest(shared['preparation']['request']),
        'returnedDirectRequestSha256': digest(returned_direct),
        'directTransportOverride': {'inputLatencyMs': 0},
        'note': DIRECT_TRANSPORT_NOTE,
    })
    return shared


def positive_cases(gate):
    baseline_results = {}
    for race in BASELINES:
        baseline_results[race] = run_parity(gate, 'fixture-' + race, scenario(race))
    night = baseline_results['night-elf']
    p = night['preparation']['preview']
    equal((p['spellbookAttackPower'], p['maximumHealth'], p['maximumMana'], p['manaAfterInitialShift']),
          (Decimal('902.4'), 2173, 2594, 2344), 'literal Night Elf acceptance oracle')
    replay = gate.request('exact-seed-replay', SHARED, scenario())
    equal(canonical(night), canonical(replay), 'whole-envelope exact seed replay')
    gate.check('literal AP902.4/HP2173/Mana2594/post-shift2344 and whole-envelope exact replay')

    reduced = {
        'without-furor': {k: v for k, v in RANKS.items() if k != 'furor'},
        'without-berserk': {k: v for k, v in RANKS.items() if k != 'berserk'},
        'without-predatory-and-berserk': {k: v for k, v in RANKS.items()
                                       if k not in ('predatory-strikes', 'berserk')},
        'rows-one-through-three': {k: v for k, v in RANKS.items() if METADATA[k][2] <= 3},
        'row-one-without-hotw': {'ferocity': 5, 'furor': 5},
        'empty': {},
        'explicit-zero-map': {k: 0 for k in RANKS},
    }
    for name, ranks in reduced.items():
        validate(gate, 'legal-' + name, selection(ranks))
        run_parity(gate, 'legal-' + name, scenario(ranks=ranks))
    run_parity(gate, 'omitted-named-totals', scenario(named=False))
    validate(gate, 'omitted-named-totals', selection(named=False))
    zero_ap = scenario()
    zero_ap['feral']['manualInputs']['preTalentCatAttackPower'] = 0
    run_parity(gate, 'explicit-zero-manual-ap', zero_ap)
    zero_manual = scenario(ranks={})
    zero_manual['feral']['manualInputs'] = {key: 0 for key in zero_manual['feral']['manualInputs']}
    run_parity(gate, 'all-manual-inputs-explicit-zero', zero_manual)
    for field, changed in (('targetArmor', Decimal('10557.5')),
                           ('preTalentCatAttackPower', Decimal('1234.5')),
                           ('weaponDps', Decimal('123.25'))):
        body = scenario()
        owner = body['feral'] if field == 'targetArmor' else body['feral']['manualInputs']
        owner[field] = changed
        result = run_parity(gate, 'actual-input-' + field, body)
        require(digest(result['preparation']['request']) != digest(night['preparation']['request']),
                field + ' must be represented in actual request identity')
        require(digest(result['sim']) != digest(night['sim']), field + ' must reach actual execution')
    affordability = scenario(ranks={'heart-of-the-wild': 5})
    affordability['feral']['manualInputs']['shapeshiftMana'] = 2594
    run_parity(gate, 'exact-startup-mana-boundary', affordability)
    affordability['feral']['manualInputs']['shapeshiftMana'] = Decimal('2594.01')
    gate.reject('above-startup-mana-boundary', affordability)
    original = scenario()
    before = encode(original)
    with concurrent.futures.ThreadPoolExecutor(max_workers=6) as pool:
        results = list(pool.map(lambda i: gate.request('concurrent-' + str(i), SHARED, original), range(6)))
    for result in results:
        equal(canonical(result), canonical(night), 'concurrent whole-envelope deterministic replay')
    equal(encode(original), before, 'concurrent caller input is unchanged')
    # An altered request between replays must not poison later shared preparation.
    final = gate.request('replay-after-mutated-and-concurrent-requests', SHARED, original)
    equal(canonical(final), canonical(night), 'sequential cross-request isolation')
    gate.check('six concurrent HTTP replays and replay after altered requests')
    return night


def talent_rejections(gate):
    validate(gate, 'fixture-36-15', selection())
    equal(totals(RANKS), {'balancePointsSpent': 0, 'feralPointsSpent': 31,
                          'restorationPointsSpent': 5, 'totalPoints': 36, 'unspentPoints': 15},
          'independent bounded fixture totals')
    for key in RANKS:
        for label, bad in (('negative', -1), ('over-cap', METADATA[key][1] + 1)):
            ranks = dict(RANKS, **{key: bad})
            validate(gate, 'rank-' + key + '-' + label, selection(ranks, named=False), 400)
            gate.reject('dispatch-rank-' + key + '-' + label, scenario(ranks=ranks, named=False))
    for rank in (1, 2, 3, 4):
        ranks = dict(RANKS, furor=rank)
        validate(gate, 'furor-partial-' + str(rank), selection(ranks), 400)
        gate.reject('dispatch-furor-partial-' + str(rank), scenario(ranks=ranks))
    for key in ('Ferocity', 'ferocity ', ' ferocity', 'unknown', 'rend-and-tear', 'natural-reaction',
                'feral-instinct', 'thick-hide', 'brutal-impact', 'primal-bite', 'feral-charge', 'naturalist'):
        for rank in (0, 1):
            ranks = dict(RANKS, **{key: rank})
            safe = re.sub(r'[^a-zA-Z0-9-]', '_', key)
            label = 'canonical-' + safe + '-' + str(rank)
            validate(gate, label, selection(ranks, named=False), 400)
            gate.reject('dispatch-' + label, scenario(ranks=ranks, named=False))
    for raw_value, name in (('1.5', 'fraction'), ('"1"', 'string'), ('null', 'null'),
                             ('true', 'boolean'), ('2147483648', 'overflow')):
        validate(gate, 'rank-type-' + name, {}, 400,
                 raw='{"ranks":{"ferocity":' + raw_value + '}}')
    # These are deliberately raw JSON. Never parse them to a dict before POST.
    for name, raw in (
        ('duplicate-zero', '{"ranks":{"ferocity":0,"ferocity":0}}'),
        ('duplicate-selected', '{"ranks":{"ferocity":5,"ferocity":5}}'),
        ('duplicate-zero-then-selected', '{"ranks":{"ferocity":0,"ferocity":5}}'),
        ('duplicate-selected-then-zero', '{"ranks":{"ferocity":5,"ferocity":0}}'),
        ('case-alias-zero', '{"ranks":{"ferocity":0,"Ferocity":0}}'),
        ('unknown-zero', '{"ranks":{"unrecognized":0}}'),
        ('missing-ranks', '{}'), ('null-ranks', '{"ranks":null}'),
    ):
        validate(gate, 'raw-' + name, {}, 400, raw=raw)
    for key, row in METADATA.items():
        if row[2] > 1:
            validate(gate, 'tier-no-earlier-' + key, selection({key: 1}), 400)
            gate.reject('dispatch-tier-no-earlier-' + key, scenario(ranks={key: 1}))
        if row[4] is not None:
            ranks = dict(RANKS)
            ranks[row[4]] = row[5] - 1
            # Remove the seventh-row requirement when it is not the tested child;
            # this leaves lower-row budget sufficient for each parent probe.
            if key != 'berserk':
                ranks.pop('berserk')
            validate(gate, 'parent-' + key, selection(ranks), 400)
            gate.reject('dispatch-parent-' + key, scenario(ranks=ranks))
    for field, correct in totals(RANKS).items():
        for name, bad in (('plus-one', correct + 1), ('negative', -1), ('synthetic-51', 51)):
            t = selection()
            t[field] = bad
            validate(gate, 'named-' + field + '-' + name, t, 400)
            body = scenario()
            body['feral']['talents'] = t
            gate.reject('dispatch-named-' + field + '-' + name, body)
        for name, bad in (('null', None), ('string', str(correct)), ('boolean', True),
                          ('fraction', Decimal('0.5'))):
            t = selection()
            t[field] = bad
            # The declared-points converter distinguishes omitted properties
            # from explicit null and rejects all non-integer JSON tokens.
            validate(gate, 'raw-named-' + field + '-' + name, t, 400)
            body = scenario()
            body['feral']['talents'] = t
            gate.reject_binding('binding-named-' + field + '-' + name, body)
    for key, rank in RANKS.items():
        body = scenario()
        body['feral']['talents']['ranks'][key] = str(rank)
        # The outer full map retains valid integers. If typed binding coerces
        # this string, the maps would match and the otherwise valid request
        # would run; rejection must occur at the Feral DTO boundary instead.
        gate.reject_binding('binding-string-typed-rank-' + key, body)
    ranks = dict(RANKS, ferocity=21)  # 52 actual points; also intentionally above cap.
    equal(sum(ranks.values()), 52, 'malformed over-budget probe')
    validate(gate, 'malformed-52-point-budget', selection(ranks, named=False), 400)
    gate.reject('dispatch-malformed-52-point-budget', scenario(ranks=ranks, named=False))
    for field in ('unknown', 'maximumHealth', 'attackPower', 'ranksExtra'):
        t = selection()
        t[field] = 0
        validate(gate, 'unknown-selection-field-' + field, t, 400)
    for owner in ('outer', 'typed'):
        body = scenario(ranks={})
        target = body['itemizedTalentRanks'] if owner == 'outer' else body['feral']['talents']['ranks']
        target['ferocity'] = 0
        gate.reject('zero-presence-mismatch-' + owner, body)
        body = scenario()
        target = body['itemizedTalentRanks'] if owner == 'outer' else body['feral']['talents']['ranks']
        target['ferocity'] = 4
        gate.reject('rank-value-mismatch-' + owner, body)
    gate.check('strict ranks, raw duplicates including zero, full-map presence, rows, parents, totals and malformed budget')


def boundary_rejections(gate, valid_result):
    def reject_change(name, path, value):
        gate.reject(name, set_at(scenario(), path, value))

    for owner, fields in (
        ((), {'classKey': ('Druid', ' druid', 'mage', ''),
              'specialization': ('Feral', 'feral ', 'balance', 'bear', '')}),
        (('feral', 'character'), {'classKey': ('Druid', 'mage', ''),
                                  'specializationKey': ('Feral', 'balance', ''),
                                  'rulesetKey': ('classic', 'WOW-FOREVER', ''),
                                  'level': (59, 61),
                                  'race': ('Night-Elf', 'night-elf ', 'human', '', None)}),
        (('feral',), {'role': ('Cat', 'bear', 'caster', 'moonkin'),
                     'targetLevel': (60, 62, 64), 'targetInitialHealthPercent': (0, 99, 101),
                     'targetMaximumHealth': (0, -1), 'targetArmor': (-1,),
                     'durationSeconds': (0, -1, 1201), 'preferredCatOpener': ('Ravage', 'unknown'),
                     'character': (None,), 'manualInputs': (None,), 'talents': (None,)}),
    ):
        for field, values in fields.items():
            for i, value in enumerate(values):
                reject_change('identity-' + '-'.join(owner + (field, str(i))), owner + (field,), value)
    for field, value in (('itemizedTalentMode', False), ('itemizedTalentRanks', None), ('feral', None)):
        reject_change('required-wrapper-' + field, (field,), value)
    for field in ('feral', 'itemizedTalentMode', 'itemizedTalentRanks'):
        body = scenario()
        body.pop(field)
        gate.reject('missing-wrapper-' + field, body)
    for extra in ('balance', 'elemental', 'enhancement', 'rogue', 'shadow', 'mage', 'warlock',
                  'marksmanship', 'survival', 'beastMastery'):
        reject_change('multiple-typed-payloads-' + extra, (extra,), {})
    for field in ('character', 'manualInputs'):
        body = scenario()
        body['feral'].pop(field)
        gate.reject('missing-feral-' + field, body)
    for field in ('preTalentCatAttackPower',):
        body = scenario()
        body['feral']['manualInputs'].pop(field)
        gate.reject('missing-manual-' + field, body)
        reject_change('null-manual-' + field, ('feral', 'manualInputs', field), None)
    reject_change('missing-provisional-opt-in', ('feral', 'useProvisionalLevel60Baseline'), False)
    body = scenario()
    body['feral'].pop('useProvisionalLevel60Baseline')
    gate.reject('omitted-provisional-opt-in', body)
    body = scenario()
    body['feral'].update(useProvisionalLevel60Baseline=False, useReviewedLevel60Baseline=True)
    gate.reject('unavailable-reviewed-baseline', body)
    reject_change('conflicting-baseline-modes', ('feral', 'useReviewedLevel60Baseline'), True)
    baseline = valid_result['preparation']['baseline']
    reject_change('inline-embedded-baseline-still-forbidden', ('feral', 'level60Baseline'), baseline)
    for field, value in (
        ('race', 'tauren'), ('classKey', 'mage'), ('level', 59),
        ('observedUnbuffedMeleeAttackPower', 800), ('sourceReference', 'forged-source'),
        ('sourceKind', 'in-game-character-sheet'), ('verificationStatus', 'reviewed-forever-in-game'),
        ('includesRacialPassives', True),
    ):
        forged = copy.deepcopy(baseline)
        forged[field] = value
        reject_change('inline-forged-' + field, ('feral', 'level60Baseline'), forged)
    body = scenario()
    body['feral'].update(startInProwl=True, targetBehind=False, preferredCatOpener='ravage')
    gate.reject('impossible-prowl-ravage-front', body)
    for field in scenario()['feral']['manualInputs']:
        reject_change('manual-negative-' + field, ('feral', 'manualInputs', field), -1)
    for field in ('meleeHitPercent', 'meleeCritPercent', 'shiftingPowerCostReductionPercent'):
        reject_change('manual-over-100-' + field, ('feral', 'manualInputs', field), 101)
    for field, valid_number in scenario()['feral']['manualInputs'].items():
        body = set_at(scenario(), ('feral', 'manualInputs', field), str(valid_number))
        gate.reject_binding('binding-string-manual-' + field, body)
    gate.reject_binding('binding-string-character-level',
                        set_at(scenario(), ('feral', 'character', 'level'), '60'))
    for field in ('seed', 'durationSeconds', 'targetLevel', 'targetInitialHealthPercent',
                  'targetMaximumHealth', 'targetArmor'):
        body = scenario()
        body['feral'][field] = str(body['feral'][field])
        gate.reject_binding('binding-string-feral-' + field, body)
    decimal_max = Decimal('79228162514264337593543950335')
    for path in (('feral', 'manualInputs', 'preTalentCatAttackPower'),
                 ('feral', 'manualInputs', 'weaponDps'), ('feral', 'targetArmor')):
        name = 'arithmetic-overflow-' + path[-1]
        rejected = gate.reject(name, set_at(scenario(), path, decimal_max))
        require(isinstance(rejected.get('error'), str) and 'representable' in rejected['error'],
                name + ': representable JSON decimal must reach guarded simulation arithmetic')
        require('errors' not in rejected, name + ': must not be a model-binding rejection')
    gate.check('actual HTTP binding rejects numeric strings and explicit non-integer declared totals; omitted totals remain absent')
    gate.check('identity, baseline ownership/provenance, required AP, unsupported scenario and numeric bounds')


def unsupported_matrix(gate):
    # Every unsupported legacy request/tuning knob is tested as a benign zero,
    # false or empty value at all five typed/wrapper ownership boundaries. A
    # silently ignored unknown field is a failure even when it changes no DPS.
    forbidden = {
        'tuning': {}, 'maximumHealth': 0, 'maximumMana': 0, 'attackPower': 0,
        'strength': 0, 'agility': 0, 'stamina': 0, 'intellect': 0, 'spirit': 0,
        'baseStats': {}, 'stats': {}, 'observedUnbuffedMeleeAttackPower': 0,
        'baseStrength': 0, 'baseStamina': 0, 'baseIntellect': 0,
        'attackPowerPerStrength': 0, 'healthPerStamina': 0, 'manaPerIntellect': 0,
        'equipment': {}, 'equipmentIds': [], 'enchants': [], 'modifiers': {}, 'buffs': [],
        'food': None, 'racialActives': [], 'racialActive': None, 'combatItems': [],
        'consumables': [], 'companion': None, 'pet': None, 'iterations': 0,
        'rotation': {}, 'rotationPolicy': {}, 'customRotation': [], 'customPriority': [],
        'encounter': {}, 'encounterScript': [], 'inputLatencyMs': 0,
        'useBearUtility': False, 'useThorns': False, 'competingThreatOnBoss': 0,
        'additionalTargets': 0, 'targetTransitions': [], 'initialComboPointsByTarget': {},
        'formTransitions': [], 'enableBossAttacks': False, 'bossHitDamage': 0,
        'bossSwingSeconds': 0, 'bearBaseArmor': 0, 'bearDodgePercent': 0,
        'incomingRageExpectedCreatureHealth': 0, 'incomingRageExpectedArmorReductionPercent': 0,
        'tankBurstWindowSeconds': 0, 'startingEnergy': 0, 'startingRage': 0,
        'omenProcPercent': 0, 'catClawBonus': 0, 'catShredBonus': 0, 'catRakeDamage': 0,
        'catRakeTick': 0, 'ripTickPerPoint': 0, 'bitePerPoint': 0, 'bearMaulBonus': 0,
        'bearSwipeBase': 0, 'lacerateTickPerStack': 0, 'thickHideArmorPercentPerRank': 0,
        'defenseSkill': 0, 'thickHideFormArmorMultiplier': 0,
        'feralInstinctSwipeDamagePercentPerRank': 0, 'bearBaseThreatMultiplier': 0,
        'primalBiteThreatMultiplier': 0, 'bearAutoRageOnHit': 0, 'catOpenersBonus': 0,
        'faerieFireArmorReduction': 0, 'thornsBaseDamage': 0, 'thornsSpellPower': 0,
        'berserkCooldownSeconds': 0, 'berserkDurationSeconds': 0,
        'frenziedRegenerationCooldownSeconds': 0, 'frenziedRegenerationDurationSeconds': 0,
        'tankArmorReductionConstant': 0, 'unknown': 0,
    }
    owners = {'wrapper': (), 'feral': ('feral',), 'character': ('feral', 'character'),
              'manual': ('feral', 'manualInputs'), 'talents': ('feral', 'talents')}
    for owner, path in owners.items():
        for field, value in forbidden.items():
            body = set_at(scenario(), path + (field,), copy.deepcopy(value))
            gate.reject('unsupported-' + owner + '-' + field, body)
    # Supported fields also cannot be smuggled into the wrong ownership block.
    for owner, path in owners.items():
        for field, value in (('preTalentCatAttackPower', 0), ('weaponDps', 0), ('meleeHitPercent', 0),
                             ('targetArmor', 0), ('useProvisionalLevel60Baseline', True)):
            if owner == 'manual' and field in ('preTalentCatAttackPower', 'weaponDps', 'meleeHitPercent'):
                continue
            if owner == 'feral' and field in ('targetArmor', 'useProvisionalLevel60Baseline'):
                continue
            gate.reject('misplaced-' + owner + '-' + field, set_at(scenario(), path + (field,), value))
    gate.check('unknown-field matrix rejects benign values at every strict ownership boundary',
               {'unsupportedFields': len(forbidden), 'boundaries': len(owners)})


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--base', default='http://127.0.0.1:5273')
    parser.add_argument('--out', required=True, help='New/empty directory for untracked receipts and captures')
    parser.add_argument('--timeout', type=float, default=30, help='Per-request timeout, at most 120 seconds')
    parser.add_argument('--fixture', type=Path, help='Optional retained 36/15 source-audit allocation to cross-check')
    args = parser.parse_args()
    gate = Gate(args.base, args.out, args.timeout)
    try:
        if args.fixture:
            fixture = decode(args.fixture.read_bytes())
            equal(fixture['ranks'], RANKS, 'retained source-audit fixture ranks')
            for field, expected in totals(RANKS).items():
                equal(fixture[field], expected, 'retained source-audit fixture ' + field)
            gate.check('optional retained source-audit allocation matches independent literals')
        discover(gate)
        talent_rejections(gate)
        result = positive_cases(gate)
        boundary_rejections(gate, result)
        unsupported_matrix(gate)
    except Exception as error:
        gate.write_receipt(False, error)
        raise
    gate.write_receipt(True)
    print(f'Feral historical HTTP: {len(gate.records)} requests passed; exact-decimal '
          'direct-copy parity with zero-latency transport alignment, strict rejection '
          'matrix, and concurrent replay verified.')


if __name__ == '__main__':
    main()
