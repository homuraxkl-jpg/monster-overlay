"""Extract table cells for numeric research, preserving original row numbers."""
from html.parser import HTMLParser
from pathlib import Path
import json
root=Path(__file__).resolve().parents[1]/'research/weapons/motion-source'
class Table(HTMLParser):
    def __init__(self):super().__init__();self.rows=[];self.row=None;self.cell=None;self.span=1
    def handle_starttag(self,tag,attrs):
        if tag=='tr':self.row=[]
        if tag in ('td','th') and self.row is not None:self.cell=[];self.span=int(dict(attrs).get('colspan','1'))
        if tag=='br' and self.cell is not None:self.cell.append(' ')
    def handle_data(self,data):
        if self.cell is not None:self.cell.append(data)
    def handle_endtag(self,tag):
        if tag in ('td','th') and self.cell is not None:
            self.row.append(' '.join(''.join(self.cell).split()));self.row.extend(['']*(self.span-1));self.cell=None
        if tag=='tr' and self.row is not None:self.rows.append(self.row);self.row=None
out=[]
for sheet in json.loads((root/'index.json').read_text()):
    parser=Table();parser.feed((root/(sheet['gid']+'.html')).read_text(encoding='utf-8'))
    out.append(dict(name=sheet['name'],gid=sheet['gid'],url=sheet['url'],rows=parser.rows))
(root/'tables.json').write_text(json.dumps(out,ensure_ascii=False,indent=2),encoding='utf-8')
for sheet in out:
    print(sheet['name'],len(sheet['rows']))
    if sheet['name'] in ('Info','Charge Blade','Long Sword','Hammer','Switch Axe'):
        for row in sheet['rows'][:14]:print(row)
