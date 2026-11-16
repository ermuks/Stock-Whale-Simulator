"""Reviewed compact Korean/English/Japanese company-growth strings."""
import csv, hashlib, json
from pathlib import Path
rows=[
('내 기업','My company','自社'),('기업 평가액','Business value','企業評価額'),('상장 상태','Listing status','上場状況'),
('비상장','Private','未上場'),('상장','Listed','上場済み'),('상장 심사 대기','IPO pending','上場審査待ち'),
('성장·상장','Grow / List','成長・上場'),('설비 확장','Expand capacity','設備拡張'),('영업 확대','Expand sales','販路拡大'),('기술 연구','Research','技術研究'),
('출자','Fund','出資'),('상장 신청','Apply for IPO','上場申請'),('대표 · {name}','CEO · {name}','代表 · {name}'),
('일매출 {revenue}   순이익 {profit}','Daily sales {revenue}   Net profit {profit}','日次売上 {revenue}   純利益 {profit}'),
('실적 —','Results —','業績 —'),('최대 단계','Max level','最大レベル'),('{cost} · {days}일','{cost} · {days} days','{cost} · {days}日'),
('{project} · DAY {day} 완료','{project} · Ready DAY {day}','{project} · DAY {day} 完了'),
('진행 중인 투자 없음','No active project','進行中の投資なし'),('상장 평가액','IPO value target','上場評価額'),('흑자 결산','Profitable closes','黒字決算'),('사업 확장','Expansions','事業拡張'),
('투자 착수','Project started','投資開始'),('진행 중·자금·단계 확인','Check project, cash or level','進行状況・資金・段階を確認'),
('비상장 완전 소유 기업만','Wholly owned private firms only','完全所有の未上場企業のみ'),('가용 현금 부족','Insufficient cash','資金不足'),
('출자 완료','Capital added','出資完了'),('상장 신청 불가','IPO unavailable','上場申請不可'),('다음 개장 심사','Review next opening','次の寄付きに審査'),('상장 요건 확인','IPO criteria unmet','上場条件を確認'),
('확장 완료','Expansion complete','拡張完了'),('사업 확장 완료','Business expansion complete','事業拡張完了'),
('투자가 완료되어 다음 실적에 반영됩니다.','Investment is complete and will feed into the next results.','投資が完了し、次回の業績に反映されます。'),
('신규 상장','New listing','新規上場'),
('상장 심사를 통과했습니다. 공모 자금은 기업에 유입되며 실제 호가에서 거래가 시작됩니다.','The listing has been approved. Offering proceeds fund the company, and trading begins on the order book.','上場審査を通過しました。公募資金は企業に入り、板での取引が始まります。'),
('내 기업 지분 100% · 비상장','My company: 100% · Private','自社持分100% · 未上場'),
('기업 성장 · 주식 투자 · 리딩방','Grow · Invest · Lead','企業育成 · 株式投資 · 投資部屋'),
('평가 {amount}','Value {amount}','評価 {amount}'),
('생산 ×{factor}','Output ×{factor}','生産 ×{factor}'),('수요 +{value}%p','Demand +{value}pp','需要 +{value}%p'),('변동비 −{value}%','Var. costs −{value}%','変動費 −{value}%'),
]
index_path=Path('Assets/Resources/Localization/SourceIndex.json')
index=json.loads(index_path.read_text(encoding='utf-8-sig'))
known={r['source'] for r in index['entries'] if not r.get('templateOnly',False)}
with Path('Assets/Localization/Import/DonDon.csv').open('a',encoding='utf-8',newline='') as out:
    writer=csv.writer(out)
    for ko,en,ja in rows:
        if ko in known: continue
        key='text.'+hashlib.sha256(ko.encode()).hexdigest()[:16]
        writer.writerow([key,'Company growth / IPO',ko,en,ja])
        index['entries'].append(dict(key=key,source=ko,templateOnly=False)); known.add(ko)
index_path.write_text(json.dumps(index,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
