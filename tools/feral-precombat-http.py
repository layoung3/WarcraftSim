#!/usr/bin/env python3
"""Loopback Feral preparation preservation and Predatory causal oracle.
Freeze must run on the preserved pre-correction binary. Decimal values and
ordered event arrays are retained; generated engine GUID identity is normalized.
"""
import argparse
import copy
from decimal import Decimal
import hashlib
import json
from pathlib import Path
import re
import urllib.parse
import urllib.request

GUID = re.compile(r'[0-9a-fA-F]{8}(-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}')
METRIC = 'feral-heart-of-the-wild-attack-power'


def decode(raw):
    return json.loads(raw, parse_float=Decimal)


def canonical(value):
    ids = {}
    def emit(v):
        if isinstance(v, dict):
            return '{' + ','.join(json.dumps(k) + ':' + emit(x) for k, x in sorted(v.items())) + '}'
        if isinstance(v, list):
            return '[' + ','.join(map(emit, v)) + ']'
        if isinstance(v, (int, Decimal)) and not isinstance(v, bool):
            s = format(Decimal(v), 'f')
            return s.rstrip('0').rstrip('.') if '.' in s else s
        if isinstance(v, str) and GUID.fullmatch(v):
            v = ids.setdefault(v, 'ENGINE-ID-' + str(len(ids)))
        return json.dumps(v, ensure_ascii=False)
    return emit(value)


def digest(value):
    return hashlib.sha256(canonical(value).encode()).hexdigest()


def cases():
    for role in ('cat', 'bear'):
        for hotw in (0, 5):
            for crit in (0, 100, 23):
                for rank in range(4):
                    yield f'{role}-hotw{hotw}-crit{crit}-rank{rank}', {
                        'role': role, 'seed': 175611, 'durationSeconds': 36, 'captureTimeline': True,
                        'talents': {'ranks': {'heart-of-the-wild': hotw, 'predatory-strikes': rank}},
                        'tuning': {'attackPower': 800, 'weaponDps': 75, 'meleeHitPercent': 100,
                                   'meleeCritPercent': crit, 'omenProcPercent': 0}}
        for name in ('zero', 'armor', 'prowl', 'ownership', 'mixed', 'savage', 'natural', 'tank', 'miss', 'death', 'finisher'):
            for rank in range(4):
                body = {'role': role, 'seed': 175612, 'durationSeconds': 45, 'captureTimeline': True,
                        'talents': {'ranks': {'predatory-strikes': rank, 'heart-of-the-wild': 5}},
                        'tuning': {'attackPower': 800, 'meleeHitPercent': 100, 'meleeCritPercent': 31,
                                   'omenProcPercent': 6}}
                if name == 'zero':
                    body['tuning'].update(attackPower=0, weaponDps=0, baseStrength=0, baseStamina=0,
                                          baseIntellect=0, shapeshiftMana=0, meleeCritPercent=0)
                elif name == 'armor':
                    body.update(targetArmor=10557.5, useFaerieFire=True)
                elif name == 'prowl':
                    body.update(startInProwl=True, preferredCatOpener='pounce')
                    body['talents']['ranks'].update({'shredding-attacks': 3, 'shifting-power': 1})
                elif name == 'ownership':
                    body.update(additionalTargets=2, initialComboPointsByTarget={'target': 5, 'target-1': 4},
                                targetTransitions=[{'atSeconds': 9, 'targetKey': 'target-1'}, {'atSeconds': 22, 'targetKey': 'target'}])
                elif name == 'mixed':
                    body['formTransitions'] = [{'atSeconds': 9, 'form': 'bear' if role == 'cat' else 'cat'},
                                               {'atSeconds': 19, 'form': 'caster'}, {'atSeconds': 25, 'form': role}]
                elif name == 'savage':
                    body.update(useFeralCooldowns=True, additionalTargets=2)
                    body['talents']['ranks'].update({'savage-fury': 2, 'primal-bite': 1,
                        'leader-of-the-pack': 1, 'berserk': 1, 'feral-instinct': 3})
                elif name == 'natural':
                    body['talents']['ranks']['natural-instinct'] = 2
                    body['tuning']['meleeCritPercent'] = 100
                elif name == 'tank':
                    body.update(enableBossAttacks=True, useThorns=True, useBearUtility=True,
                                useFeralCooldowns=True, competingThreatOnBoss=10000)
                elif name == 'miss':
                    body['tuning']['meleeHitPercent'] = 0
                elif name == 'death':
                    body.update(targetMaximumHealth=900, additionalTargets=1,
                                targetTransitions=[{'atSeconds': 20, 'targetKey': 'target-1'}])
                elif name == 'finisher':
                    body.update(initialComboPointsByTarget={'target': 5}, targetBehind=False)
                    body['tuning'].update(meleeCritPercent=100, omenProcPercent=0)
                    body['talents']['ranks'].update({'shredding-attacks': 3, 'shifting-power': 1})
                yield f'{role}-{name}-rank{rank}', body


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--base', default='http://127.0.0.1:5273')
    p.add_argument('--out', required=True)
    p.add_argument('--baseline')
    p.add_argument('--phase', choices=('freeze', 'preservation', 'correction'), required=True)
    args = p.parse_args()
    url = urllib.parse.urlsplit(args.base)
    assert url.scheme == 'http' and url.hostname in ('localhost', '127.0.0.1', '::1')
    assert not (url.username or url.password or url.query or url.fragment)
    out = Path(args.out); out.mkdir(parents=True, exist_ok=True)
    records = []
    for name, body in cases():
        rank = body['talents']['ranks']['predatory-strikes']
        for variant in ('original', 'counterfactual'):
            request = copy.deepcopy(body)
            if variant == 'counterfactual':
                request['talents']['ranks']['predatory-strikes'] = 0
                request['tuning']['attackPower'] += 30 * rank
            key = name + '-' + variant
            encoded = json.dumps(request).encode()
            (out / (key + '.request.json')).write_bytes(encoded)
            with urllib.request.urlopen(urllib.request.Request(args.base + '/api/sim/feral-druid/run', encoded,
                        {'Content-Type': 'application/json'}), timeout=30) as response:
                raw = response.read()
            (out / (key + '.response.json')).write_bytes(raw)
            value = decode(raw)
            record = {'name': key, 'sha256': digest(value)}
            if args.phase != 'freeze':
                oldkey = key if args.phase == 'preservation' or variant == 'counterfactual' else name + '-counterfactual'
                old = decode((Path(args.baseline) / (oldkey + '.response.json')).read_bytes())
                assert encoded == (Path(args.baseline) / (key + '.request.json')).read_bytes(), key
                if args.phase == 'correction' and variant == 'original':
                    # The only intentional response mismatch: counterfactual authored AP
                    # changes this HOTW-only metric; selected Predatory must not change it.
                    delta = old['run']['customMetrics'][METRIC] - value['run']['customMetrics'][METRIC]
                    assert delta == 30 * rank, (key, 'HOTW-only metric', delta)
                    old['run']['customMetrics'][METRIC] = value['run']['customMetrics'][METRIC]
                    record['reconciledHotwMetricDelta'] = str(delta)
                assert canonical(value) == canonical(old), (key, record['sha256'], digest(old))
                record['comparison'] = 'equal-after-named-HOTW-metric-reconciliation' if args.phase == 'correction' and variant == 'original' else 'whole-response-equal'
            records.append(record)
    (out / 'receipt.json').write_text(json.dumps({'phase': args.phase,
        'normalization': 'exact decimal values; engine GUIDs mapped by first occurrence preserving identity; sorted object keys; array order unchanged',
        'checks': len(records), 'records': records}, indent=2) + '\n')
    print(f'{args.phase}: {len(records)} complete request/response checks passed')


if __name__ == '__main__':
    main()
