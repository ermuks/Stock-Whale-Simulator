"""Authoring source for Korean catalog variants. Runtime reads JSON only."""
from pathlib import Path
import json
ROOT=Path(__file__).resolve().parents[2]
path=ROOT/'Assets/Resources/Chat/chat_templates.json'
doc=json.loads(path.read_text(encoding='utf-8'))
doc['templates']=[x for x in doc['templates'] if x['key'].startswith('legacy.')]
legacy=json.loads((ROOT/'Assets/Resources/Chat/BotChatTemplates.ko.json').read_text(encoding='utf-8-sig'))
original={x['id']:x for x in legacy['templates']}
for entry in doc['templates']:
    old=original[entry['key'][7:]]
    entry['requires']=list(old.get('requirements',[]))
    entry['requires'].append('event:'+old['event'])
    if old.get('holdingCondition')=='Holding': entry['requires'].append('held')
    if old.get('holdingCondition')=='NotHolding': entry['requires'].append('notHeld')
# Each row has explicit register versions; no runtime machine translation or LLM.
lines={
'BRAG':[
 ('{symbol} {profitKind}손익이 {pct}%입니다.','{symbol} {profitKind}손익 {pct}%네요.','{symbol} {profitKind}손익 {pct}% 나옴.'),
 ('제 {symbol} {profitKind}손익은 {amount}원입니다.','{symbol} 제 {profitKind}손익 {amount}원이에요.','{symbol} {profitKind}으로 {amount}원. 숫자는 이럼.')],
'LAMENT':[
 ('{symbol} {profitKind}손익 {pct}%, 마음이 무겁습니다.','{symbol} {profitKind}손익 {pct}%라 속 쓰리네.','{symbol} {profitKind}손익 {pct}%. 아프다.'),
 ('제 {symbol} {profitKind}손익은 {amount}원입니다. 잠시 보겠습니다.','{symbol} {profitKind}손익 {amount}원… 잠깐 보려고요.','{symbol} {profitKind}손익 {amount}원. 잠깐 머리 식힘.')],
'ASK':[
 ('{symbol} {price}원, 어떻게 보고 계십니까?','{symbol} {price}원인데 어떻게 보세요?','{symbol} {price}원인데 다들 어떻게 봄?'),
 ('{symbol} 흐름을 더 지켜봐도 될까요?','{symbol} 흐름 좀 더 지켜볼까요?','{symbol} 좀 더 봐야 되나?')],
'ANSWER':[
 ('저는 {symbol} {price}원에서 흐름을 확인하고 있습니다.','저는 {symbol} {price}원 보고 있어요.','나는 {symbol} {price}원 보는 중.'),
 ('제 판단 기준은 {symbol}의 다음 체결입니다.','제 기준은 {symbol} 다음 체결이에요.','{symbol} 다음 체결 보자는 거임.')],
'AGREE':[
 ('말씀하신 판단 과정은 이해합니다.','그렇게 보는 이유는 알겠어요.','그렇게 보는 건 이해함.'),
 ('그 부분에는 동의합니다. 더 지켜보겠습니다.','그 부분은 맞는 것 같아요. 좀 더 보죠.','그 부분은 맞지. 더 보자.')],
'REBUT':[
 ('그 결론에는 동의하기 어렵습니다. 근거부터 보시죠.','그 결론은 좀 다르게 봐요. 근거부터 보죠.','그 결론은 아니지. 근거부터 가져와.'),
 ('같은 상황이어도 판단은 다를 수 있습니다.','같은 걸 봐도 판단은 다를 수 있잖아요.','같은 거 봤다고 결론까지 같아야 됨?')],
'DOUBT':[
 ('그 판단을 뒷받침할 근거가 있는지 궁금합니다.','그 판단 근거는 좀 궁금하네요.','그 판단 근거가 뭐임?'),
 ('확신하기에는 아직 확인할 부분이 있습니다.','확신하기엔 아직 볼 게 있지 않나요?','벌써 확신한다고? 더 봐야지.')],
'MOCK':[
 ('말씀의 자신감은 상당하시군요.','말씀만 들으면 확신은 대단하네요.','말만 들으면 대단한 확신이네.'),
 ('결과보다 설명이 먼저 나오시는군요.','결과보다 설명이 먼저 나오는 느낌인데요.','결과는 아직인데 설명부터 하네.')],
'INSULT':[
 ('그 말씀은 어이가 없습니다.','그 말은 좀 어이없네요.','어이없네. 말 좀 가려 해.'),
 ('지금은 정신을 차리셔야겠습니다.','정신 좀 차려요. 선 넘고 있잖아요.','정신 차려라. 선 넘고 있잖아.')],
'COMFORT':[
 ('마음이 무거우시겠지만 잠시 쉬어 가셔도 됩니다.','속 쓰리겠지만 잠깐 쉬어도 괜찮아요.','속 쓰리겠다. 잠깐 쉬어도 됨.'),
 ('혼자 감당하려 하지 않으셔도 됩니다.','혼자만 그런 건 아닐 거예요. 너무 몰아세우지 마요.','너무 몰아세우지 마. 잠깐 숨 돌려.')],
'APOLOGIZE':[
 ('제 표현이 지나쳤습니다. 죄송합니다.','제 표현이 과했네요. 미안해요.','내가 말이 심했네. 미안.'),
 ('그 말은 거두겠습니다. 감정적으로 반응했습니다.','그 말은 취소할게요. 감정이 앞섰네요.','그 말 취소. 내가 좀 흥분했음.')],
'MEDIATE':[
 ('두 분 모두 잠시 멈추고 근거부터 확인하시죠.','두 분 잠깐 멈추고 근거부터 봐요.','둘 다 잠깐 멈춰. 근거부터 보자.'),
 ('서로에게 향한 말은 줄이셨으면 합니다.','서로 사람 말고 얘기 내용으로 돌아가요.','서로 사람 건드리지 말고 얘기로 돌아가.')],
'PRAISE_PLAYER':[
 ('{symbol} 리딩을 따른 제 실제 수익은 {pct}%입니다. 도움을 받았습니다.','{symbol} 리딩 따라 실제 {pct}% 벌었어요. 이번엔 도움 됐네요.','{symbol} 따라가서 실제 {pct}% 벌었음. 이번 건 인정.'),
 ('{symbol} 지시를 따른 실현손익 {amount}원을 확인했습니다.','{symbol} 지시 따른 실현손익 {amount}원이에요. 고마워요.','{symbol} 따라간 실현손익 {amount}원. 고맙다.')],
'WARN_PLAYER':[
 ('{symbol} 지시를 따른 제 결과는 {pct}% 손실입니다. 직접 확인하시길 바랍니다.','{symbol} 따라간 제 결과는 {pct}%예요. 지시만 믿진 마세요.','{symbol} 따라갔다가 {pct}%. 지시만 믿지 마.'),
 ('제 {symbol} 리딩 실현손익은 {amount}원입니다. 손실 경험을 남깁니다.','{symbol} 지시 따른 실현손익 {amount}원… 제 경험은 이래요.','{symbol} 따라간 실현손익 {amount}원. 내 경험은 이럼.')],
'CALL_PLAYER':[
 ('방장님, 두 분의 대화를 확인해 주시겠습니까?','방장님, 지금 싸움 좀 봐 주세요.','방장, 지금 싸우는 것 좀 봐.'),
 ('방장님의 중재가 필요해 보입니다.','방장님이 한번 정리해 주셔야 할 것 같아요.','방장이 좀 말려야 되는 거 아님?')],
'REACT':[
 ('{symbol} {price}원입니다. 다음 움직임을 보겠습니다.','{symbol} 지금 {price}원이네요. 다음 체결 보죠.','{symbol} 지금 {price}원. 다음 거 보자.'),
 ('{symbol} 쪽을 확인하고 있습니다.','{symbol} 쪽 보고 있어요.','{symbol} 보는 중.')],
'SMALLTALK':[
 ('잠시 물을 마시고 돌아오겠습니다.','물 한잔 마시고 올게요.','물 좀 마시고 옴.'),
 ('화면을 오래 보니 눈이 피곤합니다.','화면 오래 보니까 눈이 피곤하네요.','화면 오래 봤더니 눈 아프네.')],
'FAREWELL':[
 ('이 분위기에서는 더 이야기하기 어렵겠습니다. 나가겠습니다.','이 분위기에서 더 얘기하긴 어렵네요. 나갈게요.','이 분위기에서 얘기 못 하겠다. 나감.'),
 ('저는 여기서 대화를 마치겠습니다.','저는 여기서 그만 얘기할게요.','나는 여기까지만 할게.')]
}
for act,variants in lines.items():
    for style_index,style in enumerate(['formal','casual','rough']):
        for intensity in (1,2,3):
            for i,variants_by_style in enumerate(variants):
                text=variants_by_style[style_index]
                if intensity==2: text += [' 생각이 복잡합니다.',' 생각이 많아지네요.',' 생각 많아지네.'][style_index]
                if intensity==3: text += [' 지금은 마음이 편치 않습니다.',' 지금은 좀 진정이 안 되네요.',' 지금 좀 진정이 안 됨.'][style_index]
                requirements=['leadGain'] if act=='PRAISE_PLAYER' else ['leadLoss'] if act=='WARN_PLAYER' else ['profit'] if act=='BRAG' else ['loss'] if act=='LAMENT' else []
                prefixes=[('', '', ''),('덧붙이겠습니다. ','덧붙이자면요. ','덧붙이면. '),('제 입장에서 말씀드리겠습니다. ','제 입장에서 얘기할게요. ','내 입장에서 말하면. ')]
                for variant_index,prefix in enumerate(prefixes):
                    doc['templates'].append(dict(key=f'{act}.{style}.{intensity}.plain.{i+1+variant_index*2:02}',act=act,style=style,intensity=intensity,variant='plain',text=prefix[style_index]+text,requires=requirements))
for style in ('formal','casual','rough'):
    doc['templates'].append(dict(key=f'REACT.{style}.1.signal',act='REACT',style=style,intensity=1,variant='plain',text='{symbol}: {bidStatus}, {askStatus}. 부임 후 실현손익 {signalResult}원.',requires=['signal']))
path.write_text(json.dumps(doc,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
config=ROOT/'Assets/Resources/Chat/chat_social_config.json'
data=json.loads(config.read_text(encoding='utf-8'))
for key,value in dict(tradeFactCapacity=64,logFlushSeconds=5,legacyTradeSeconds=180,legacyAfterSellRatio=.03,legacyEventSeconds=45,legacyStressDrop=5).items():
    if not any(x['key']==key for x in data['values']): data['values'].append(dict(key=key,value=value))
config.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('Catalog entries:',len(doc['templates']))
