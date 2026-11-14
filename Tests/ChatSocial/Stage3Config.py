from pathlib import Path
import json
root=Path(__file__).resolve().parents[2]
path=root/'Assets/Resources/Chat/chat_social_config.json'
data=json.loads(path.read_text(encoding='utf-8'))
values=dict(initiateTargetChance=.35,relationWeightFloor=.2,argumentAttackCount=2,witnessProbability=.35,witnessActivityBase=.35,witnessActivityScale=.65)
replies=dict(INSULT=.9,REBUT=.8,MOCK=.75,ASK=.7,DOUBT=.7,APOLOGIZE=.65,COMFORT=.5,AGREE=.5,BRAG=.5,ANSWER=.4,MEDIATE=.4,SMALLTALK=.35,LAMENT=.5,PRAISE_PLAYER=.5,WARN_PLAYER=.5,CALL_PLAYER=0,REACT=.4,FAREWELL=.35)
values.update({'replyBase.'+k:v for k,v in replies.items()})
for key,value in values.items():
    if not any(x['key']==key for x in data['values']): data['values'].append(dict(key=key,value=value))
path.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
