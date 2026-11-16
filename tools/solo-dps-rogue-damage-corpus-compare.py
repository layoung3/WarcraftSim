#!/usr/bin/env python3
"""Read-only complete before/after Rogue corpus comparison, no HTTP or processes.

Raw810 indexed responses cover original191 inside historical310, standalone119,
accepted stage317, Combat UI50 and production report14. Parsed report artifacts
are separately labeled; they are never described as exact raw Decimal captures.
"""
import argparse
import copy
import importlib.util
from pathlib import Path

S=importlib.util.spec_from_file_location('rogue_damage_http',Path(__file__).with_name('solo-dps-rogue-subtlety-http.py'))
M=importlib.util.module_from_spec(S);S.loader.exec_module(M)
O=M.O
COUNTS={'http-combat-historical':310,'http-combat-standalone':119,'http-stage-regression':317,'http-combat-ui':50,'http-combat-report':14}


def at(value,path):
    for key in path:
        if not isinstance(value,dict) or key not in value:return None
        value=value[key]
    return value


def set_at(value,path,item):
    for key in path[:-1]:value=value[key]
    value[path[-1]]=item


def project_character(prior,current,paths,pair):
    changes=[]
    for path in paths:
        old,new=at(prior,path),at(current,path)
        if old==new:continue
        M.require(pair is not None and (old,new)==pair,'Character identity differs without its retained generated-default origin: '+str(path))
        set_at(current,path,old);changes.append({'path':list(path),'before':old,'after':new})
    return changes


def select(root,family):
    matches=list((root/family).rglob('index.json'))
    M.require(len(matches)==1,'Expected one indexed capture for '+family)
    return matches[0].parent


def normalized_report(value):
    # Keep the complete remainder beside one supported event-envelope path.
    # This preserves any literal GUID anchors outside the generated event paths.
    copied=copy.deepcopy(value);sim=copied['rawServerResponse'].pop('sim')
    return O.stable({'sim':sim,'retainedReportRemainder':copied})


def compare(args):
    before=Path(args.baseline);after=Path(args.corrected);out=Path(args.out);out.mkdir(parents=True,exist_ok=False)
    proofs=[];failures=[];summary={'passed':False,'rawResponseComparisons':0,'parsedReportComparisons':0,'failures':failures,'proofs':proofs}
    for family,count in COUNTS.items():
        left,right=select(before,family),select(after,family)
        old_rows={r['name']:r for r in M.read(left/'index.json')};new_rows={r['name']:r for r in M.read(right/'index.json')}
        M.require(set(old_rows)==set(new_rows) and len(old_rows)==count,family+': full inventory differs')
        generated_pair=None
        if family=='http-combat-report':
            generated_pair=(M.read(left/'defaults.response.json')['character']['id'],M.read(right/'defaults.response.json')['character']['id'])
            M.require(all(O.GUID.fullmatch(x) and x!=O.ZERO_GUID for x in generated_pair),'Report generated defaults invalid')
        for name,old_row in old_rows.items():
            try:
                new_row=new_rows[name];route=old_row.get('path',old_row.get('route'));M.require(route==new_row.get('path',new_row.get('route')),'Route differs');M.require(old_row['status']==new_row['status'],'Status differs')
                old_raw=(left/(name+'.response.json')).read_bytes();new_raw=(right/(name+'.response.json')).read_bytes()
                for row,raw in [(old_row,old_raw),(new_row,new_raw)]:
                    if 'responseSha256' in row:M.require(row['responseSha256']==M.sha(raw),'Recorded raw response hash differs')
                prior,current=O.decode(old_raw),O.decode(new_raw);exceptions=[]
                old_request=left/(name+'.request.json');new_request=right/(name+'.request.json');M.require(old_request.exists()==new_request.exists(),'Request presence differs')
                if old_request.exists():
                    a,b=old_request.read_bytes(),new_request.read_bytes()
                    for row,raw in [(old_row,a),(new_row,b)]:
                        if 'requestSha256' in row:M.require(row['requestSha256']==M.sha(raw),'Recorded raw request hash differs')
                    if a!=b:
                        # Production report serializer deliberately inherits the
                        # random GET-default Character ID in these exact paths.
                        M.require(family=='http-combat-report','Raw request differs outside report-generated identity')
                        parsed_a,parsed_b=O.decode(a),O.decode(b)
                        exceptions+=project_character(parsed_a,parsed_b,[('character','id'),('rogue','character','id')],generated_pair)
                        M.require(O.encode(parsed_a)==O.encode(parsed_b),'Request differs beyond verified generated Character ID')
                if route.startswith('/api/sim/rogue/defaults'):
                    old_id,new_id=prior['character']['id'],current['character']['id']
                    M.require(all(isinstance(x,str) and O.GUID.fullmatch(x) and x!=O.ZERO_GUID for x in [old_id,new_id]),'Generated GET-default ID invalid')
                    current['character']['id']=old_id;exceptions.append({'path':['character','id'],'before':old_id,'after':new_id})
                elif route==M.METADATA:
                    M.require(prior['sourceCommit']==args.old_source_commit,'Old API source stamp not exact expected')
                    current,prior,stamp=O.metadata_projection(current,prior,args.source_commit);exceptions.append(stamp)
                if family=='http-combat-report':exceptions+=project_character(prior,current,[('preparation','request','character','id')],generated_pair)
                M.require(O.stable(prior)==O.stable(current),'Complete response differs beyond explicit source/default IDs')
                proofs.append({'family':family,'name':name,'status':old_row['status'],'requestBeforeSha256':M.sha(old_request.read_bytes()) if old_request.exists() else None,'requestAfterSha256':M.sha(new_request.read_bytes()) if new_request.exists() else None,'responseBeforeSha256':M.sha(old_raw),'responseAfterSha256':M.sha(new_raw),'exceptions':exceptions});summary['rawResponseComparisons']+=1
            except Exception as error:failures.append({'family':family,'name':name,'error':str(error)})
        if family=='http-combat-report':
            for points in [31,30]:
                name=str(points)+'-report.json'
                try:
                    prior,current=M.read(left/name),M.read(right/name)
                    exceptions=project_character(prior,current,[('originalSimulationRequest','rogue','character','id'),('combatPreparation','request','character','id'),('rawServerResponse','preparation','request','character','id')],generated_pair)
                    M.require(prior['metadata']['sourceCommit']==args.old_source_commit,'Old report source stamp differs')
                    current['metadata'],prior['metadata'],stamp=O.metadata_projection(current['metadata'],prior['metadata'],args.source_commit);exceptions.append(stamp)
                    M.require(normalized_report(prior)==normalized_report(current),'Complete parsed report differs beyond exact source/default IDs')
                    proofs.append({'family':family,'name':name,'comparison':'complete parsed artifact; NOT wire-Decimal proof','exceptions':exceptions});summary['parsedReportComparisons']+=1
                except Exception as error:failures.append({'family':family,'name':name,'error':str(error)})
    summary['passed']=not failures and summary['rawResponseComparisons']==810 and summary['parsedReportComparisons']==2
    summary['normalization']=O.NORMALIZATION
    summary['coverage']='Raw historical310 includes complete original191, standalone119, accepted-stage317, Combat UI50 and report14. Complete31/30 parsed report artifacts are additional. Older JS wrapper-only suites and other-class raw suites are separately retained and are NOT included in this810-response equality claim.'
    M.write(out/'summary.json',summary);print(O.encode({k:v for k,v in summary.items() if k!='proofs'}));return 0 if summary['passed'] else 1


def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--baseline',required=True);p.add_argument('--corrected',required=True);p.add_argument('--out',required=True);p.add_argument('--old-source-commit',required=True);p.add_argument('--source-commit',required=True)
    return compare(p.parse_args())

if __name__=='__main__':raise SystemExit(main())
