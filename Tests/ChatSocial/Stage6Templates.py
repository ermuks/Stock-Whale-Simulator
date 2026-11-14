from pathlib import Path
import json
root=Path(__file__).resolve().parents[2]
path=root/'Assets/Resources/Chat/chat_templates.json'
doc=json.loads(path.read_text(encoding='utf-8'))
doc['templates']=[t for t in doc['templates'] if not t['key'].startswith(('MEMORY.','FOMO.'))]
memory={
 'FOUGHT':(['MOCK','REBUT'],[
 ['{memWhen} 저와 언쟁하셨던 이야기는 기억하십니까?','{memWhen} 저와도 같은 식으로 다투셨습니다.','저와 {memWhen} 언쟁하신 뒤로 달라진 게 있습니까?','{memWhen} 언쟁의 상대가 저였다는 건 기억하시지요.'],
 ['{memWhen} 저랑 언쟁한 건 기억하세요?','{memWhen} 저한테도 그러셨잖아요.','{memWhen} 저랑 다투고 뭐가 달라졌나요?','{memWhen} 싸웠던 상대가 저예요.'],
 ['{memWhen} 나랑 싸운 건 기억함?','{memWhen} 나한테도 그랬잖아.','{memWhen} 싸운 뒤로 뭐가 달라졌는데?','{memWhen} 싸운 상대가 나야.']]),
 'SAW_BRAG_THEN_CRASH':(['MOCK','REBUT'],[
 ['{memWhen} {memSymbol} 자랑 뒤 {memPct}% 손실이라 하신 것도 봤습니다.','{memSymbol} 자랑하시다가 {memPct}%라고 하셨지요.','{memWhen} 하신 {memSymbol} 손실 이야기와는 다르군요.','{memSymbol} {memPct}% 손익 말씀하신 분 아닌가요?'],
 ['{memWhen} {memSymbol} 자랑하다 {memPct}% 됐다고 하셨죠.','{memSymbol} 자랑 뒤 손실 얘기도 봤어요.','{memWhen} {memSymbol} 손실 얘기하셨잖아요.','{memSymbol} {memPct}%라고 하신 분 맞죠?'],
 ['{memWhen} {memSymbol} 자랑하다 {memPct}%라며.','{memSymbol} 자랑 뒤 손실 얘기도 봤다.','{memWhen} {memSymbol} 손실이라 했잖아.','{memSymbol} {memPct}%라고 한 사람이 너잖아.']]),
 'LOSS_BY_LEAD':(['WARN_PLAYER','DOUBT'],[
 ['{memWhen} {memSymbol} 지시를 따른 제 손익은 {memAmount}원이었습니다.','제 {memSymbol} 리딩 결과는 {memPct}%였습니다. 다음엔 근거를 더 보겠습니다.','{memWhen} 리딩을 따랐던 {memSymbol} 손실이 기억납니다.','{memSymbol} 지시에서 손해를 봤던 경험이 있어 조심스럽습니다.'],
 ['{memWhen} {memSymbol} 따라간 제 손익은 {memAmount}원이었어요.','제 {memSymbol} 리딩 결과는 {memPct}%였어요. 근거 좀 더 볼게요.','{memWhen} {memSymbol} 지시에서 본 손실이 기억나요.','{memSymbol} 따라갔다 손해 봐서 조심스러워요.'],
 ['{memWhen} {memSymbol} 따라간 내 손익 {memAmount}원임.','내 {memSymbol} 리딩 결과 {memPct}%. 근거 좀 보자.','{memWhen} {memSymbol} 지시에서 손실 본 거 기억함.','{memSymbol} 따라가다 손해 봐서 조심하는 거야.']]),
 'KICKED':(['WARN_PLAYER','DOUBT'],[
 ['{memWhen} 그 방에서 추방당한 적이 있습니다.','저는 이미 한 번 추방을 경험했습니다.','{memWhen} 내보내신 일이 기억납니다.','추방당한 경험이 있어 믿음이 쉽게 생기지 않습니다.'],
 ['{memWhen} 그 방에서 쫓겨났어요.','전 이미 한 번 추방당했어요.','{memWhen} 내보내신 거 기억나요.','쫓겨난 적 있어서 쉽게 믿기는 어렵네요.'],
 ['{memWhen} 그 방에서 쫓겨났음.','난 이미 한 번 추방당했다.','{memWhen} 내보낸 거 기억함.','쫓겨난 적 있는데 쉽게 믿겠냐.']]),
 'GAIN_BY_LEAD':(['PRAISE_PLAYER'],[
 ['{memWhen} {memSymbol} 지시로 {memPct}% 수익을 얻었습니다.','제 {memSymbol} 리딩 손익은 {memAmount}원이었습니다.','{memWhen} {memSymbol} 지시가 제게 도움이 됐습니다.','{memSymbol}에서 {memPct}% 얻었던 경험은 인정합니다.'],
 ['{memWhen} {memSymbol} 따라가서 {memPct}% 벌었어요.','제 {memSymbol} 리딩 손익은 {memAmount}원이었어요.','{memWhen} {memSymbol} 지시가 도움 됐어요.','{memSymbol}에서 {memPct}% 얻은 건 인정해요.'],
 ['{memWhen} {memSymbol} 따라가서 {memPct}% 벌었다.','내 {memSymbol} 리딩 손익 {memAmount}원이었음.','{memWhen} {memSymbol} 지시는 도움 됐지.','{memSymbol} {memPct}% 얻은 건 인정함.']]),
 'SAW_KICK':(['REACT'],[
 ['{memWhen} 다른 분이 추방되는 것도 봤습니다.','전에도 내보내신 일이 기억납니다.','{memWhen} 추방 장면이 떠오릅니다.','이전에도 누군가 추방되는 걸 봤지요.'],
 ['{memWhen} 다른 분 나가는 것도 봤어요.','전에도 내보내신 거 기억나요.','{memWhen} 추방 장면이 떠오르네요.','이전에도 누가 쫓겨나는 걸 봤어요.'],
 ['{memWhen} 다른 사람 추방도 봤음.','전에도 내보낸 거 기억난다.','{memWhen} 추방 장면 생각나네.','전에 누가 쫓겨나는 것도 봤다.']])}
extra_fought=[
 ['이번 말씀을 들으니 {memWhen} 저와 언쟁하신 일이 생각납니다.','{memWhen} 다퉜던 기억 때문에 말씀을 그대로 받아들이기 어렵습니다.','{memWhen} 이미 한 번 부딪혔습니다. 이번에도 의견이 다르군요.','{memWhen} 저와 싸웠던 일부터 떠오르는군요.'],
 ['이번 말 들으니 {memWhen} 저랑 언쟁했던 일이 생각나요.','{memWhen} 다툰 게 기억나서 그 말을 쉽게 받아들이긴 어렵네요.','{memWhen} 한 번 부딪혔는데 이번에도 의견이 다르네요.','{memWhen} 저랑 싸웠던 일부터 떠오르네요.'],
 ['이 말 들으니 {memWhen} 나랑 싸운 거 생각난다.','{memWhen} 싸운 거 기억나는데 그 말을 쉽게 믿겠냐.','{memWhen} 한 번 부딪혔는데 이번에도 생각 다르네.','{memWhen} 나랑 싸웠던 거부터 떠오르네.']]
for index,phrases in enumerate(extra_fought): memory['FOUGHT'][1][index].extend(phrases)
for kind,(acts,styles) in memory.items():
    for act in acts:
        for si,style in enumerate(('formal','casual','rough')):
            for intensity in (1,2,3):
                for i,text in enumerate(styles[si]):
                    doc['templates'].append(dict(key=f'MEMORY.{kind}.{act}.{style}.{intensity}.{i}',act=act,style=style,intensity=intensity,variant='memory',text=text,requires=['memory:'+kind]))
fomo=[
 ['{symbol} 기회를 놓칠까 조급해집니다.','지금 {symbol}을 따라가야 할지 고민됩니다.','{symbol}, 늦었다고만 생각하기 어렵습니다.','{symbol}을 보고 있자니 마음이 급합니다.','{symbol} 수익 이야기를 보니 흔들립니다.','{symbol} 진입 여부를 다시 생각하고 있습니다.'],
 ['{symbol} 놓칠까 조급하네요.','지금 {symbol} 따라가야 하나 고민돼요.','{symbol}, 늦었다고만 생각하긴 어렵네요.','{symbol} 보니까 마음이 급해요.','{symbol} 수익 얘기 들으니 흔들리네요.','{symbol} 진입할지 다시 생각 중이에요.'],
 ['{symbol} 놓칠까 조급하다.','지금 {symbol} 따라가야 하나.','{symbol}, 늦었다고만 보긴 어렵네.','{symbol} 보니까 마음 급해진다.','{symbol} 수익 얘기에 흔들리네.','{symbol} 진입할지 다시 생각 중.']]
for si,style in enumerate(('formal','casual','rough')):
    for intensity in (1,2,3):
        for i,text in enumerate(fomo[si]):
            doc['templates'].append(dict(key=f'FOMO.{style}.{intensity}.{i}',act='REACT',style=style,intensity=intensity,variant='plain',text=text,requires=['notice:FOMO']))
path.write_text(json.dumps(doc,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
config=root/'Assets/Resources/Chat/chat_social_config.json'; settings=json.loads(config.read_text(encoding='utf-8'))
for key,value in dict(fomoObservationMinutes=5,fomoBuyBias=.12,fomoPickChance=.35,leadingStopBasePct=4,leadingStopRiskPct=8,rememberedBrags=16,relationshipSpeechMinutes=60,relationshipPublicChance=.5,ambivalentPlayerDoubtScale=3).items():
    if not any(v['key']==key for v in settings['values']): settings['values'].append(dict(key=key,value=value))
config.write_text(json.dumps(settings,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
acts_path=root/'Assets/Resources/Chat/chat_act_weights.json'; acts=json.loads(acts_path.read_text(encoding='utf-8'))
row=next(t for t in acts['motives'] if t['key']=='SELF_EVENT')
for key,value in [('DOUBT',5),('MOCK',3)]:
    if not any(w['key']==key for w in row['weights']): row['weights'].append(dict(key=key,value=value))
acts_path.write_text(json.dumps(acts,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
