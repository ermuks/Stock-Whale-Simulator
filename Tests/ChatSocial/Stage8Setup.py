from pathlib import Path
import json, uuid
root = Path(__file__).resolve().parents[2]
path = root/'Assets/Resources/Chat/chat_social_config.json'
doc = json.loads(path.read_text(encoding='utf-8'))
existing = {v['key'] for v in doc['values']}
changes = []
for key, value in dict(suffixChance=.25, suffixEmotionMin=60, cacheTrimSeconds=60, cacheGraceSeconds=180, closedThreadCache=512).items():
    if key not in existing:
        doc['values'].append(dict(key=key, value=value))
        changes.append(dict(key=key, before=None, after=value))
path.write_text(json.dumps(doc, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
folder = root/'docs/chat-social-results/stage-8'
folder.mkdir(parents=True, exist_ok=True)
if changes:
    with (folder/'tuning.jsonl').open('a', encoding='utf-8') as out:
        out.write(json.dumps(changes)+'\n')
for name in ['ChatSocialLanguage','ChatSocialMaintenance','ChatSocialLog','ChatSocialDiagnostics','ChatSocialDebugUnity','ChatSocialPromises']:
    source = root/f'Assets/Scripts/NightShift/{name}.cs'
    meta = Path(str(source)+'.meta')
    if source.exists() and not meta.exists():
        meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n', encoding='utf-8')
print('New settings:', changes)
