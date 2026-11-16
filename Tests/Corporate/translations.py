"""Append reviewed company UI/news strings without changing existing translations."""
import csv, hashlib, json
from pathlib import Path

rows = [
('거래','Trade','取引'),
('첫 기업 결산 전','Awaiting first results','初回企業決算前'),
('발행 주식 수','Shares issued','発行株数'),('마감 종가','Close price','終値'),('마감 시총','Closing cap','終値時価総額'),
('내 지분','My stake','持株比率'),('매출','Revenue','売上'),('운영비','Costs','運営費'),('순이익','Net profit','純利益'),
('내 배당금','My dividend','受取配当'),('기업 현금','Company cash','企業資金'),('미지급 비용','Unpaid costs','未払費用'),
('재투자','Reinvest','再投資'),('배당 비율','Payout ratio','配当比率'),('배당','Dividends','配当'),('비축','Reserves','内部留保'),
('지분 매입','Buy stake','株式取得'),('관련 뉴스','Company news','関連ニュース'),('완전 소유','Wholly owned','完全子会社'),
('경영권 확보','Controlled','経営権取得'),('경영권 50% 초과','Control: >50%','経営権：過半数'),('첫 마감 전','Awaiting first close','初回決算前'),
('DAY {day} · 17:00 결산','DAY {day} · 17:00 close','DAY {day} · 17:00 決算'),
('경영 정책 저장','Policy saved','方針を保存'),('경영권 필요','Control required','経営権が必要'),('정산 중','Settling','決算中'),
('기업 배당','Dividends','企業配当'),('기업 결산','Company results','企業決算'),('정산으로','Back to close','精算に戻る'),
('가용 현금 또는 추가 매입 수량이 없습니다.','No cash or shares left to buy.','購入余力または必要株数がありません。'),
('{company} 경영권 인수 확정','{company}: takeover confirmed','{company}の経営権取得が確定'),
('기업 인수','Takeover','企業買収'),('기업 공시','Company filing','企業開示'),('지분 확인','Stake verified','持株確認済み'),
('플레이어가 과반 지분을 확보했습니다. 새 경영 전략과 주주환원 확대에 대한 기대가 커지고 있습니다.',
 'The player has secured a majority stake. Expectations are rising for a new strategy and stronger shareholder returns.',
 'プレイヤーが過半数の株式を取得。新たな経営戦略と株主還元の拡大に期待が高まっています。'),
('주주 원장에서 플레이어의 지분 {stake}%가 확인됐습니다. 실제 체결이 완료된 지분을 기준으로 경영권이 이전됐습니다.',
 'The shareholder ledger confirms a {stake}% player stake. Control has changed hands following completed trades.',
 '株主台帳でプレイヤーの保有比率{stake}%が確認されました。約定済みの株式に基づき経営権が移りました。'),
('새 지배주주는 배당·재투자·현금 비축 정책을 결정할 수 있습니다. 시장에서는 자본 배분 개선과 성장 투자 가능성을 주요 호재로 평가하고 있습니다.',
 'The new controlling shareholder can set dividend, reinvestment and reserve policies. Investors see improved capital allocation and growth investment as strong positive catalysts.',
 '新たな支配株主は配当・再投資・内部留保の方針を決定できます。市場は資本配分の改善や成長投資への期待を強い好材料と受け止めています。'),
('기업의 일일 배당은 장 마감 실적과 보유 현금에 따라 결정됩니다. 인수 자체가 이익이나 주가 상승을 보장하지는 않습니다.',
 'Daily dividends depend on closing earnings and available company cash. A takeover alone does not guarantee profits or a higher share price.',
 '日次配当は引け時の業績と企業資金に応じて決まります。買収だけで利益や株価上昇が保証されるわけではありません。'),
]
index_path=Path('Assets/Resources/Localization/SourceIndex.json')
index=json.loads(index_path.read_text(encoding='utf-8-sig'))
known={row['source'] for row in index['entries'] if not row.get('templateOnly',False)}
with Path('Assets/Localization/Import/DonDon.csv').open('a',encoding='utf-8',newline='') as out:
    writer=csv.writer(out)
    for ko,en,ja in rows:
        if ko in known:
            continue
        key='text.'+hashlib.sha256(ko.encode()).hexdigest()[:16]
        writer.writerow([key,'Corporate UI / daily business / acquisition',ko,en,ja])
        index['entries'].append(dict(key=key,source=ko,templateOnly=False))
        known.add(ko)
index_path.write_text(json.dumps(index,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
