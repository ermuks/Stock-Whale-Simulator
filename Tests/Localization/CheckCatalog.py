"""Fast, offline translation coverage/placeholder regression check (standard library only)."""
import csv
import json
import re
from collections import Counter
from pathlib import Path

root = Path(__file__).resolve().parents[2]
rows = list(csv.DictReader((root / 'Assets/Localization/Import/DonDon.csv').open(encoding='utf-8-sig')))
keys = {row['Key']: row for row in rows}
assert len(keys) == len(rows), 'Duplicate table keys'
tokens = lambda value: Counter(re.findall(r'\{([^{}:,]+)(?:[^{}]*)\}', value))
for row in rows:
    for locale in ('ko', 'en', 'ja'):
        assert row[locale].strip(), (row['Key'], locale, 'empty translation')
        assert tokens(row['ko']) == tokens(row[locale]), (row['Key'], locale, 'changed placeholders')
    for locale in ('en', 'ja'):
        assert not re.search('[가-힣ㄱ-ㅎㅏ-ㅣ]', row[locale]), (row['Key'], locale, 'untranslated Korean')
        assert not re.search(r'\{(?:amount|gain|loss|price|memAmount)\}円', row[locale]), 'KRW must not become JPY'
index = json.loads((root / 'Assets/Resources/Localization/SourceIndex.json').read_text(encoding='utf-8'))['entries']
for entry in index:
    assert entry['key'] in keys and keys[entry['key']]['ko'] == entry['source'], entry
sources = {entry['source'] for entry in index}
chat = json.loads((root / 'Assets/Resources/Chat/chat_templates.json').read_text(encoding='utf-8'))
phrases = set()
for section in ('templates', 'contexts', 'community', 'voices'):
    for entry in chat[section]:
        phrases.update([entry['text']] if 'text' in entry else entry['texts'])
assert not phrases - sources, ('Missing chat phrases', phrases - sources)
news_js = (root / 'Assets/itCompany.js').read_text(encoding='utf-8-sig')
company_news = json.JSONDecoder().raw_decode(news_js[news_js.index('{'):])[0]
headlines = {node['description'] for node in company_news['nodes']}
assert not headlines - sources, ('Missing company headlines', headlines - sources)
assert {p.stem for p in (root / 'Assets/Localization/Locales').glob('*.asset')} == {'ko', 'en', 'ja'}
assert keys[next(entry['key'] for entry in index if entry['source'] == '원')]['en'] == '₩'
print(f'PASS {len(rows)} entries in each of ko/en/ja; {len(phrases)} chat phrases; {len(headlines)} company headlines')
print('PASS unique keys, complete locales, unchanged placeholders, Korean residuals, KRW units and source index')
