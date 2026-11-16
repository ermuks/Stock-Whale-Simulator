from pathlib import Path
import csv,json,hashlib,uuid
from PIL import Image
root=Path('Assets/Resources/UI/TabletKit')
src=Path('Assets/Resources/UI/TradingTerminal/Icon_Send.png.meta').read_text()
src=src.replace(next(line for line in src.splitlines() if line.startswith('guid:')),'guid: '+uuid.uuid4().hex)
src=src.replace('spriteMeshType: 0','spriteMeshType: 1')
if not (root/'Icons.png.meta').exists(): (root/'Icons.png.meta').write_text(src)
im=Image.open(root/'Icons.png').convert('RGBA')
assert im.size==(1024,768)
assert im.getpixel((0,0))[3]==0
for i in range(48):
    cell=im.crop((i%8*128,i//8*128,i%8*128+128,i//8*128+128))
    assert cell.getchannel('A').getbbox(),i
    assert cell.getpixel((0,0))[3]==0,i
    assert all(r==g==b==255 for r,g,b,a in cell.getdata() if a>0),i
print('PASS 48 atlas cells: nonempty, white pixels, transparent corners, 1024x768')
rows=[
('{ticks} 틱','{ticks} ticks','{ticks}ティック'),
('갱신 · {time}','Updated · {time}','更新 · {time}'),
('{ticks}틱','{ticks} ticks','{ticks}ティック'),('± {ticks}틱','± {ticks} ticks','± {ticks}ティック'),
('기본 계좌','Main account','基本口座'),('작업 계좌','Work account','運用口座'),('자산','Assets','資産'),('주문','Order','注文'),('자동 매매','Auto trade','自動売買'),('특수 기능','Special','特殊機能'),('기록','History','履歴'),
('종목 · 수량','Stock / Qty','銘柄・株数'),('현재가 · 평균가','Price / Avg','現在値・平均'),('손익 · 수익률','P&L / Return','損益・損益率'),
('선택 종목 상세','Position details','保有詳細'),('상장 준비','IPO readiness','上場準備'),('일매출','Daily sales','日次売上'),('투자','Invest','投資'),('발행 가능','Ready to publish','発行可能'),
('예상 매수가','Bid estimate','予想買値'),('예상 매도가','Ask estimate','予想売値'),('매수 잔량','Buy remaining','買残数'),('매도 잔량','Sell remaining','売残数'),('최근 체결','Recent fills','直近約定'),
('실행 경과 · {time}','Elapsed · {time}','稼働時間 · {time}'),('재사용 대기 · {time}','Cooldown · {time}','再使用まで · {time}'),('발행 완료 · 재사용 대기 {time}','Published · Cooldown {time}','発行済み · 再使用まで {time}'),
('대표 · {name}   ·   지분 {ratio}%','CEO · {name}   ·   Stake {ratio}%','代表 · {name}   ·   持分 {ratio}%'),
('배당 {a}%  ·  재투자 {b}%  ·  비축 {c}%','Dividend {a}% · Reinvest {b}% · Reserve {c}%','配当 {a}% · 再投資 {b}% · 留保 {c}%'),
('양방향 대기','Both quotes live','両建注文待ち'),('매수 대기','Buy pending','買注文待ち'),('매도 대기','Sell pending','売注文待ち'),('자산 · 호가 대기','Awaiting funds / quotes','資金・気配待ち'),('매매 취소됨','Stopped','停止済み'),
]
ip=Path('Assets/Resources/Localization/SourceIndex.json'); index=json.loads(ip.read_text(encoding='utf-8-sig'))
known={r['source'] for r in index['entries'] if not r.get('templateOnly',False)}
with Path('Assets/Localization/Import/DonDon.csv').open('a',encoding='utf-8',newline='') as out:
    writer=csv.writer(out)
    for ko,en,ja in rows:
        if ko in known: continue
        key='text.'+hashlib.sha256(ko.encode()).hexdigest()[:16]; writer.writerow([key,'Tablet kit',ko,en,ja]); index['entries'].append(dict(key=key,source=ko,templateOnly=False)); known.add(ko)
ip.write_text(json.dumps(index,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
for p in [Path('Assets/Scripts/NightShift/TabletKitStyle.cs'),Path('Assets/Scripts/NightShift/TabletKitLayout.cs')]:
    meta=Path(str(p)+'.meta')
    if not meta.exists(): meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n')
