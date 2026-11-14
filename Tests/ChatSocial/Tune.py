"""Apply auditable config-only trials. Acceptance bounds are deliberately not editable here."""
from pathlib import Path
import json,sys,re
root=Path(__file__).resolve().parents[2]
stage=sys.argv[1]
path=root/'Assets/Resources/Chat/chat_social_config.json'
data=json.loads(path.read_text(encoding='utf-8'))
lookup={x['key']:x for x in data['values']}
changes=[]
for argument in sys.argv[2:]:
    key,value=argument.split('=',1)
    if re.match(r'R\d',key) or key in ('volumeShareMax','hotLeaderIntervalMax','leaderTimeShareMax','stockBreadthMin'):
        raise ValueError('Do not widen acceptance bounds')
    previous=lookup[key]['value'];lookup[key]['value']=float(value)
    changes.append(dict(key=key,before=previous,after=float(value)))
path.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
out=root/f'docs/chat-social-results/stage-{stage}'
out.mkdir(parents=True,exist_ok=True)
with (out/'tuning.jsonl').open('a',encoding='utf-8') as stream: stream.write(json.dumps(changes,ensure_ascii=False)+'\n')
print(changes)
