from pathlib import Path
import json
root=Path(__file__).resolve().parents[2]
path=root/'Assets/Resources/Chat/chat_templates.json'
doc=json.loads(path.read_text(encoding='utf-8'))
doc['templates']=[x for x in doc['templates'] if not x['key'].startswith('NOTICE.')]
notices={
 'EXIT_WARNING':('REACT','이대로라면 방을 나가겠습니다. 더는 참기 어렵습니다.','이대로면 나갈게요. 더는 참기 힘드네요.','이대로면 나감. 더는 못 참겠다.'),
 'SYMPATHY_EXIT':('FAREWELL','{departedName}님이 나가셨군요. 저도 함께 나가겠습니다.','{departedName}님 나가셨네요. 저도 따라 나갈게요.','{departedName} 나갔네. 나도 따라 나감.'),
 'VOLUNTARY_EXIT':('FAREWELL','아무도 말리지 않는군요. 이 싸움을 더 견디지 않겠습니다. 나갑니다.','아무도 안 말리네요. 더는 싸우기 싫어서 나갈게요.','아무도 안 말리네. 더는 싸우기 싫다. 나감.'),
 'KICK_WARNING':('WARN_PLAYER','그 방에서 추방당했습니다. 제 경험은 좋지 않았습니다.','그 방에서 쫓겨났어요. 제 경험은 별로였네요.','그 방에서 쫓겨남. 내 경험은 별로임.'),
 'LEFT_WARNING':('WARN_PLAYER','싸움을 말리지 않아 그 방에서 나왔습니다.','싸워도 아무도 안 말려서 그 방 나왔어요.','싸워도 안 말리길래 그 방 나옴.')}
prefixes=[('','',''),('덧붙이겠습니다. ','덧붙이자면요. ','덧붙이면. '),('제 입장입니다. ','제 입장은 그래요. ','내 입장은 그럼. '),('말씀드려야겠습니다. ','얘기는 해야겠네요. ','말은 해야겠다. '),('참고로 남깁니다. ','참고로 남겨요. ','참고로 남김. '),('지금 느낀 그대로입니다. ','지금 느낀 그대로예요. ','지금 느낀 그대로임. ')]
for notice,(act,*phrases) in notices.items():
    for index,style in enumerate(('formal','casual','rough')):
        for intensity in (1,2,3):
            for n,prefix in enumerate(prefixes):
                doc['templates'].append(dict(key=f'NOTICE.{notice}.{style}.{intensity}.{n}',act=act,style=style,intensity=intensity,variant='plain',text=prefix[index]+phrases[index],requires=['notice:'+notice]))
path.write_text(json.dumps(doc,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
config=root/'Assets/Resources/Chat/chat_social_config.json'
settings=json.loads(config.read_text(encoding='utf-8'))
if not any(x['key']=='sympathyAffinityOffset' for x in settings['values']): settings['values'].append(dict(key='sympathyAffinityOffset',value=50))
if not any(x['key']=='recentReadCount' for x in settings['values']): settings['values'].append(dict(key='recentReadCount',value=10))
if not any(x['key']=='emotionHighThreshold' for x in settings['values']): settings['values'].append(dict(key='emotionHighThreshold',value=80))
config.write_text(json.dumps(settings,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
