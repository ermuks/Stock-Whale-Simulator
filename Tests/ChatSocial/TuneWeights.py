from pathlib import Path
import json,sys
root=Path(__file__).resolve().parents[2]
stage=sys.argv[1]
path=root/'Assets/Resources/Chat/chat_act_weights.json'
data=json.loads(path.read_text(encoding='utf-8'))
changes=[]
for argument in sys.argv[2:]:
    key,value=argument.split('=',1); section,row,field=key.split('.')
    entries=next(r['weights'] for r in data[section] if r['key']==row)
    entry=next(v for v in entries if v['key']==field)
    previous=entry['value'];entry['value']=float(value)
    changes.append(dict(key=key,before=previous,after=float(value)))
path.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
out=root/f'docs/chat-social-results/stage-{stage}';out.mkdir(parents=True,exist_ok=True)
with (out/'tuning.jsonl').open('a',encoding='utf-8') as stream:stream.write(json.dumps(changes,ensure_ascii=False)+'\n')
print(changes)
