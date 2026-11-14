from pathlib import Path
import json
root=Path(__file__).resolve().parents[2]
path=root/'Assets/Resources/Chat/chat_templates.json'
doc=json.loads(path.read_text(encoding='utf-8'))
doc['templates']=[t for t in doc['templates'] if not t['key'].startswith('MODERATION.')]
texts={
 'DISCIPLINE':[
 ['싸우면 바로 조치하시는군요.','방장님이 보고 계십니다. 말조심해야겠습니다.','여기는 싸움에 제재가 있군요.','다들 투자 이야기로 돌아가시죠.','조치하셨으니 언쟁은 여기까지 하시죠.','서로 자극하는 말은 줄이는 게 좋겠습니다.'],
 ['싸우면 바로 조치하시네요.','방장님 보고 계시네요. 말조심해야겠어요.','여긴 싸우면 제재가 있네요.','다들 종목 얘기로 돌아가죠.','조치했으니 싸움은 이쯤 하죠.','서로 자극하는 말은 좀 줄여요.'],
 ['싸우면 바로 정리하네.','방장 보고 있다. 말조심하자.','여긴 싸우면 제재 받는구나.','그만하고 종목 얘기나 하자.','조치했으니까 이제 끝내자.','서로 긁는 말 좀 줄이자.']],
 'CHILL':[
 ['요즘은 말을 꺼내기가 조심스럽습니다.','갑자기 나가는 분이 많군요. 잠시 지켜보겠습니다.','말 한마디도 신중해야겠습니다.','분위기가 무거워졌습니다.','오늘은 조용히 관찰하겠습니다.','의견을 쓰려다 잠시 멈췄습니다.'],
 ['요즘 말하기 좀 조심스럽네요.','갑자기 나가는 분이 많네요. 좀 지켜볼게요.','말 한마디도 신중해야겠어요.','분위기가 무거워졌네요.','오늘은 조용히 볼게요.','의견 쓰려다 멈췄어요.'],
 ['말하기 좀 무섭네.','갑자기 많이 나가네. 난 좀 볼게.','말 한마디도 조심해야겠다.','분위기 무겁다.','오늘은 조용히 봐야지.','쓰려다 지웠다.']],
 'UNMUTED':[
 ['이제 다시 말할 수 있군요.','제 말만 막는 것으로 끝나지는 않겠지요.','다음에는 양쪽 이야기를 들어주시기 바랍니다.','금지는 풀렸지만 기억은 남습니다.','조용히 보고 있었습니다.','말할 수 있다고 바로 잊는 건 아닙니다.'],
 ['이제 말할 수 있네요.','제 말만 막으면 끝인 건 아니죠.','다음엔 양쪽 얘길 들어주세요.','금지는 풀려도 기억은 남아요.','조용히 보고 있었어요.','다시 말한다고 바로 잊는 건 아니에요.'],
 ['이제 말 되네.','나만 막으면 끝임?','다음엔 양쪽 말 좀 들어.','금지는 풀려도 기억한다.','조용히 보고 있었다.','풀렸다고 잊는 건 아니다.']]}
for notice,styles in texts.items():
    for si,style in enumerate(('formal','casual','rough')):
        for intensity in (1,2,3):
            for i,text in enumerate(styles[si]):
                doc['templates'].append(dict(key=f'MODERATION.{notice}.{style}.{intensity}.{i}',act='REACT',style=style,intensity=intensity,variant='plain',text=text,requires=['notice:'+notice]))
path.write_text(json.dumps(doc,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
