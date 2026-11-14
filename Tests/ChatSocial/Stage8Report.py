"""Package validation evidence, without changing rules, thresholds or source assets."""
from pathlib import Path
import json, hashlib, subprocess, re, collections
root=Path(__file__).resolve().parents[2]
out=root/'docs/chat-social-results/stage-8'
for source,target in [('stage8-full-final.txt','results.txt'),('compile-stage8-final.txt','compile.txt'),('market-compile-stage8.txt','market-core-compile.txt'),('polish-first.txt','polish-initial-failure.txt')]:
    path=root/'Validation/ChatSocial'/source
    if path.exists(): (out/target).write_bytes(path.read_bytes())
before=json.loads(subprocess.check_output(['git','show','HEAD:Assets/Resources/Chat/chat_social_config.json'],cwd=root))
after=json.loads((root/'Assets/Resources/Chat/chat_social_config.json').read_text(encoding='utf-8'))
a={v['key']:v['value'] for v in before['values']};b={v['key']:v['value'] for v in after['values']}
delta=[dict(key=k,before=a.get(k),after=b.get(k)) for k in sorted(a.keys()|b.keys()) if a.get(k)!=b.get(k)]
assert not any(re.match(r'R\d',v['key']) or v['key'] in ('volumeShareMax','hotLeaderIntervalMax','leaderTimeShareMax','stockBreadthMin') for v in delta)
(out/'settings-diff.json').write_text(json.dumps(delta,indent=2)+'\n',encoding='utf-8')
# Explicit, reviewable reading labels, not a manufactured simulation acceptance criterion.
switches={73129:[4,5,7,9,10,11,12,15,16,17,18,24,26,27,28,29,30,31,33,34,35,37],
          73130:[10,11,12,17,18,22,23,25,26,29,30,31,33,34,36,37,38,44],
          73131:[3,4,5,7,8,9,10,11,13,14,16,17,20,21,23,24,25,26,34]}
reading=[]
for seed, labels in switches.items():
    lines=(out/f'seed-{seed}-2h.txt').read_text(encoding='utf-8-sig').splitlines()
    assert all(2<=i<=len(lines) for i in labels)
    texts=[re.sub(r'^@\S+ ', '', x.split('|',4)[4]) for x in lines]
    reading.append(dict(seed=seed,messages=len(lines),adjacentPairs=len(lines)-1,topicSwitchOrOtherThreadEndingLines=labels,
                        repeatedBodies=[dict(text=t,count=c) for t,c in collections.Counter(texts).items() if c>1]))
(out/'reading-labels.json').write_text(json.dumps(reading,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
base=root/'docs/chat-social-results/stage-7/A-relative-verified-73129/minutes.csv'
latest=out/'final-context-73129/minutes.csv'
if latest.exists():
    comparison=dict(previous=hashlib.sha256(base.read_bytes()).hexdigest(),final=hashlib.sha256(latest.read_bytes()).hexdigest(),sameMinutes=base.read_bytes()==latest.read_bytes())
    (out/'market-repeat-check.json').write_text(json.dumps(comparison,indent=2)+'\n',encoding='utf-8')
rows=[json.loads(line) for line in (out/'S2-ignored-fight.jsonl').read_text(encoding='utf-8-sig').splitlines()]
departure=next(x for x in rows if x.get('kind')=='VOLUNTARY_EXIT')
messages=[x for x in rows if 'templateKey' in x and departure['time']-120<=x['at']<=departure['time']+8 and
          (x['senderId'] in (departure['actor'],departure['target']) or x['act'] in (14,17))]
(out/'S2-first-departure-readable.txt').write_text('\n'.join(f"{x['at']:.1f} | #{x['senderId']} -> #{x['targetId']} | {x['text']}" for x in messages)+'\n'+
    json.dumps(departure,ensure_ascii=False)+'\n',encoding='utf-8')
sources=['Assets/Scripts/NightShift/ChatSocialData.cs','Assets/Scripts/NightShift/ChatSocialPipeline.cs','Assets/Scripts/NightShift/ChatSocialAssets.cs','Assets/Scripts/NightShift/ChatSocialUnity.cs','Assets/Scripts/TradeBot/TradeBot.cs']
patch=subprocess.check_output(['git','-c','core.autocrlf=true','diff','--function-context','--',*sources],cwd=root,stderr=subprocess.PIPE).decode('utf-8')
(out/'function-comparisons.md').write_text('# 8단계 함수 단위 원본·수정본 비교\n\n7단계 커밋 대비 전체 함수 문맥입니다. `-`는 원본, `+`는 수정본이며 실행용 패치가 아닌 검토용 기록입니다.\n\n```diff\n'+
    '\n'.join(line.rstrip() for line in patch.splitlines())+'\n```\n',encoding='utf-8')
# Unity logs contain indentation-only trailing whitespace; remove that without dropping any diagnostic line.
for path in out.glob('*/unity.log'):
    path.write_text('\n'.join(line.rstrip() for line in path.read_text(encoding='utf-8-sig',errors='replace').splitlines())+'\n',encoding='utf-8')
print(json.dumps(dict(settings=delta,reading=reading),ensure_ascii=False))
