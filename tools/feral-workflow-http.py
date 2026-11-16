#!/usr/bin/env python3
"""Loopback-only Feral workflow acceptance. Synthetic inputs are not client evidence."""
import argparse
import concurrent.futures
import copy
from decimal import Decimal
import hashlib
import json
from pathlib import Path
import re
import urllib.error
import urllib.parse
import urllib.request


def canonical(value):
    if isinstance(value, dict):
        return '{' + ','.join(json.dumps(k) + ':' + canonical(v) for k, v in sorted(value.items())) + '}'
    if isinstance(value, list):
        return '[' + ','.join(canonical(v) for v in value) + ']'
    if isinstance(value, (int, Decimal)) and not isinstance(value, bool):
        return format(Decimal(value), 'f').rstrip('0').rstrip('.') if '.' in format(Decimal(value), 'f') else str(value)
    if isinstance(value, str) and re.fullmatch(r'[0-9a-fA-F]{8}(-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}', value):
        value = 'INSTANCE-ID'
    return json.dumps(value, ensure_ascii=False)


def decode(raw):
    return json.loads(raw, parse_float=Decimal)


def fingerprint(raw):
    return hashlib.sha256(canonical(decode(raw)).encode()).hexdigest()


def assert_fast_trace(value):
    """Independent expected times/damage; no frozen response files are needed."""
    events = value['run']['timeline']
    casts = [e for e in events if e['type'] == 4]
    assert [(e['abilityKey'], e['timeSeconds']) for e in casts[:4]] == [
        ('rip', 3), ('rake', Decimal('6.5')), ('claw', Decimal('10.5')), ('ferocious-bite', 14)]
    bite = next(e for e in events if e['type'] == 6 and e['abilityKey'] == 'ferocious-bite')
    assert (bite['timeSeconds'], bite['amount'], bite['targetActorKey']) == (14, 1600, 'target')
    assert bite['abilityExecutionId'] == casts[3]['abilityExecutionId']
    spends = [e for e in events if e['resourceKey'] == 'energy' and
              e['abilityKey'] == 'ferocious-bite' and e['timeSeconds'] == 14 and e['amount'] < 0]
    assert len(spends) == 1 and spends[0]['amount'] == -35
    ticks = [e for e in events if e['type'] == 6 and e['abilityKey'] == 'rip' and
             e['isPeriodic'] and e['timeSeconds'] <= 15]
    assert [(e['timeSeconds'], e['amount']) for e in ticks] == [(t, 450) for t in (5, 7, 9, 11, 13, 15)]
    assert all(e['targetActorKey'] == 'target' and
               e['abilityExecutionId'] == casts[0]['abilityExecutionId'] for e in ticks)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--base', default='http://127.0.0.1:5273')
    parser.add_argument('--out', required=True)
    parser.add_argument('--baseline', help='Optional frozen 25-case request/response directory')
    args = parser.parse_args()
    url = urllib.parse.urlsplit(args.base)
    assert url.scheme == 'http' and url.hostname in ('localhost', '127.0.0.1', '::1')
    assert not (url.username or url.password or url.query or url.fragment)
    out = Path(args.out)
    out.mkdir(parents=True, exist_ok=True)
    records = []

    def request(name, body, expected=200):
        encoded = json.dumps(body).encode()
        (out / (name + '.request.json')).write_bytes(encoded)
        try:
            with urllib.request.urlopen(urllib.request.Request(args.base + '/api/sim/feral-druid/run', encoded,
                                        {'Content-Type': 'application/json'}), timeout=30) as response:
                status, raw = response.status, response.read()
        except urllib.error.HTTPError as error:
            status, raw = error.code, error.read()
        (out / (name + '.response.json')).write_bytes(raw)
        assert status == expected, (name, expected, status, raw[:500])
        value = decode(raw)
        record = {'name': name, 'status': status, 'sha256': fingerprint(raw)}
        if status == 400:
            assert isinstance(value.get('error'), str) and value['error'], (name, value)
            record['error'] = value['error']
        else:
            assert value['damageDone'] == sum(value['damageByAbility'].values()), name
            record.update(damageDone=str(value['damageDone']), endingMana=str(value['endingMana']))
        records.append(record)
        return value, raw

    with urllib.request.urlopen(args.base + '/api/sim/feral-druid/defaults', timeout=10) as response:
        default = json.loads(response.read())
    def case(**kwargs):
        r = copy.deepcopy(default)
        r.update(durationSeconds=20, captureTimeline=True, inputLatencyMs=0)
        r['tuning'].update(meleeHitPercent=100, meleeCritPercent=0, omenProcPercent=0)
        r.update(kwargs)
        return r

    if args.baseline:
        baseline = Path(args.baseline)
        old_statuses = {r['name']: r['status'] for r in json.loads((baseline / 'summary.json').read_text())}
        for path in sorted(baseline.glob('*.request.json')):
            name = path.name.removesuffix('.request.json')
            body = json.loads(path.read_bytes())
            expected = 400 if old_statuses[name] == 500 or name == 'prowl-ravage-front' else 200
            value, raw = request('replay-' + name, body, expected)
            records[-1]['beforeStatus'] = old_statuses[name]
            if expected == 200:
                before = (baseline / (name + '.response.json')).read_bytes()
                records[-1]['beforeSha256'] = fingerprint(before)
                records[-1]['unchanged'] = fingerprint(before) == fingerprint(raw)
                if name != 'fast-combos-rip-active':
                    assert records[-1]['unchanged'], name
                else:
                    assert_fast_trace(value)
                    events = value['run']['timeline']
                    old_events = decode(before)['run']['timeline']
                    assert canonical([e for e in old_events if e['timeSeconds'] < 14]) == canonical([e for e in events if e['timeSeconds'] < 14])
                    records[-1]['firstNewBite'] = {'time': 14, 'points': 4, 'damage': 1600, 'paidEnergy': 35}

    for role in ('cat', 'bear'):
        for rank in range(6):
            for delta in (-0.01, 0, 0.01):
                r = case(role=role, durationSeconds=1, maximumMana=200)
                r['talents']['ranks'] = {'heart-of-the-wild': rank}
                r['tuning']['shapeshiftMana'] = 200 + 54 * rank + delta
                value, _ = request(f'{role}-hotw{rank}-delta{delta}', r, 400 if delta > 0 else 200)
                if delta <= 0:
                    assert value['run']['customMetrics']['feral-heart-of-the-wild-maximum-mana'] == 200 + 54 * rank
                    assert value['endingMana'] == Decimal(str(-delta))
        for field in ('baseIntellect', 'manaPerIntellect'):
            r = case(role=role, maximumMana=200, durationSeconds=1)
            r['talents']['ranks'] = {'heart-of-the-wild': 5}
            r['tuning'][field] = 0
            request(role + '-zero-' + field, r, 400)
        r = case(role=role, maximumMana=250, durationSeconds=8)
        r['formTransitions'] = [{'atSeconds': 6, 'form': 'bear' if role == 'cat' else 'cat'}]
        value, _ = request(role + '-later-unfunded', r)
        assert value['formChanges'] == 1 and value['run']['customMetrics']['feral-failed-form-shifts'] == 1
    r = case(durationSeconds=3.5, targetBehind=False, initialComboPointsByTarget={'target': 5})
    r['tuning']['meleeHitPercent'] = 0
    value, _ = request('first-paid-rip-miss', r)
    events = value['run']['timeline']
    refunds = [e for e in events if e['abilityKey'] == 'rip-miss-refund']
    assert len(refunds) == 1 and refunds[0]['timeSeconds'] == 3 and refunds[0]['amount'] == 24
    assert value['comboPointsByTarget']['target'] == 5
    assert not any(e['type'] == 9 and e['abilityKey'] == 'rip' for e in events)
    for role, opener in (('Cat', 'ravage'), ('cat', 'Ravage'), ('bear', 'RAVAGE')):
        value, _ = request('case-' + role + '-' + opener, case(role=role, preferredCatOpener=opener), 400)
        assert value['error'] == 'Invalid Feral simulation request.'
    r = case(startInProwl=True, targetBehind=False, durationSeconds=8, formTransitions=[{'atSeconds': 6, 'form': 'bear'}])
    value, _ = request('impossible-even-with-transition', r, 400)
    assert 'TargetBehind=true' in value['error'] and 'Pounce' in value['error']
    r = case(durationSeconds=1, maximumMana=0)
    r['tuning']['shapeshiftMana'] = 0
    request('raw-zero-mana-remains-invalid', r, 400)
    r = case(startInProwl=True, targetBehind=False)
    r['tuning']['shapeshiftMana'] = -1
    value, _ = request('negative-tuning-before-opener', r, 400)
    assert 'TargetBehind=true' not in value['error']
    # Explicit synthetic fixed-Cat fixture. Expected scheduling and coefficients
    # are authored independently of both runtime output and optional local history.
    r = case(seed=144001, role='cat', durationSeconds=40, targetBehind=False,
             initialComboPointsByTarget={'target': 5}, targetArmor=0, additionalTargets=0,
             maximumMana=4000, maximumHealth=5500, targetMaximumHealth=1000000,
             startInProwl=False, preferredCatOpener='ravage', useFeralCooldowns=False,
             useFaerieFire=False, useThorns=False, useBearUtility=False,
             enableBossAttacks=False, targetTransitions=[], formTransitions=[])
    r['tuning'].update(weaponDps=75, attackPower=800, meleeHitPercent=100,
                       meleeCritPercent=100, omenProcPercent=0, shapeshiftMana=250,
                       catRakeDamage=80, catRakeTick=40, catClawBonus=100,
                       ripTickPerPoint=45, bitePerPoint=200)
    r['talents']['ranks'] = {'ferocity': 5, 'blood-frenzy': 2}
    value, _ = request('fast-bite-rip', r)
    assert_fast_trace(value)
    records[-1]['firstBite'] = {'time': 14, 'points': 4, 'damage': 1600, 'paidEnergy': 35}
    before = json.dumps(r)
    with concurrent.futures.ThreadPoolExecutor(max_workers=6) as pool:
        results = list(pool.map(lambda i: request('concurrent-' + str(i), r)[1], range(6)))
    assert len({fingerprint(raw) for raw in results}) == 1
    assert before == json.dumps(r)
    summary = {'schema': 'warcraftsim.feral-workflow-http-v1', 'scope': 'bounded preview policy; not client certification',
               'frozenReplayVerified': bool(args.baseline), 'standaloneFastTraceVerified': True,
               'requests': len(records), 'records': sorted(records, key=lambda r: r['name'])}
    (out / 'summary.json').write_text(json.dumps(summary, indent=2) + '\n')
    checks = 'standalone fast Bite/Rip trace and concurrent isolation verified'
    if args.baseline:
        checks += '; optional frozen exact decimal/GUID-canonical replay verified'
    print(f'Feral workflow HTTP: {len(records)} POSTs passed; {checks}.')


if __name__ == '__main__':
    main()
