"""Summarize only explicit evasion diagnostic records; never copy other log content."""
import argparse,collections,json,statistics
from pathlib import Path

def main():
 parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('log',type=Path);parser.add_argument('--output',type=Path);args=parser.parse_args()
 rows=[];stats=None;bad=0
 for line in args.log.open(encoding='utf-8-sig',errors='replace'):
  try:
   if ' EVASION_CONTACT_CANDIDATE ' in line:rows.append(json.loads(line.split(' EVASION_CONTACT_CANDIDATE ',1)[1])['candidate'])
   elif ' EVASION_CONTACT_STATS ' in line:stats=json.loads(line.split(' EVASION_CONTACT_STATS ',1)[1])
  except (ValueError,KeyError,TypeError):bad+=1
 scoped=[r for r in rows if r.get('Scope') is not None];rates=[]
 for a,b in zip(scoped,scoped[1:]):
  x,y=a.get('WindowTimer'),b.get('WindowTimer');dt=b['Time']-a['Time']
  if x is not None and y is not None and x>y>0 and .03<=dt<=.2 and a.get('SourceFlags')==b.get('SourceFlags'):rates.append((x-y)/dt)
 report={'scope':'evasion_diagnostic_records_only','sampled_contacts':len(rows),'scoped_contacts':len(scoped),'scoped_positive_window':sum((r.get('WindowTimer') or 0)>0 for r in scoped),'body_owner_matches':sum(r.get('BodyOwnerMatches') is True for r in rows),'native_filter_true':sum(r.get('InvulnerabilityFilterCandidate') is True for r in rows),'native_filter_known':sum(r.get('InvulnerabilityFilterCandidate') is not None for r in rows),'latest_stats':stats,'source_flag_samples':dict(collections.Counter(str(r.get('SourceFlags')) for r in rows)),'observed_counter_decrement_per_second_median':statistics.median(rates) if rates else None,'malformed_diagnostic_rows':bad,'note':'Counts are sampled contacts, never success counts. Timer units and callback contact semantics require validation.'}
 text=json.dumps(report,ensure_ascii=False,indent=2)
 if args.output:args.output.parent.mkdir(parents=True,exist_ok=True);args.output.write_text(text,encoding='utf-8')
 print(text)
if __name__=='__main__':main()
