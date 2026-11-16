from pathlib import Path
import shutil, re, csv, hashlib, json, uuid

files=['Settings/GameSettings.cs','Settings/SystemMenuUI.cs','CandleDrawManager.cs','ChartManager.cs','ChartCandleSelectData.cs','ChartUI/ChartGuideOverlay.cs','ChartUI/TradeHistoryItem.cs','OrderBookUI/DrawRawOrder.cs','OrderBookUI/OrderBookUIManager.cs','StockListUI/StockListItem.cs','StockListUI/StockListUIManager.cs','Hold/HoldListItem.cs','NightShift/InvestmentDashboard.cs','NightShift/DeviceTerminalUI.cs','NightShift/PendingOrderListUI.cs','NightShift/SpreadTradingUI.cs','NightShift/TabletKitStyle.cs','NightShift/TradingOrderView.cs']
for name in files:
    path=Path('Assets/Scripts')/name
    backup=Path('Validation/MarketColors/before')/path
    if not backup.exists():
        backup.parent.mkdir(parents=True,exist_ok=True); shutil.copy2(path,backup)

# Only monetary direction colors; risk, heat, warnings and selection accents remain unchanged.
for name in ['ChartManager.cs','ChartUI/ChartGuideOverlay.cs','NightShift/InvestmentDashboard.cs','NightShift/PendingOrderListUI.cs','NightShift/SpreadTradingUI.cs']:
    path=Path('Assets/Scripts')/name; text=path.read_text(encoding='utf-8-sig')
    text=re.sub(r'new Color32\(239,\s*89,\s*100,\s*255\)', 'MarketColors.Up', text)
    text=re.sub(r'new Color32\(76,\s*150,\s*232,\s*255\)', 'MarketColors.Down', text)
    path.write_text(text,encoding='utf-8')
for name in ['StockListUI/StockListItem.cs','ChartCandleSelectData.cs']:
    path=Path('Assets/Scripts')/name; text=path.read_text(encoding='utf-8-sig')
    text=re.sub(r'(?:private )?static readonly Color Up = .*?;', 'private static Color Up => MarketColors.Up;',text)
    text=re.sub(r'(?:private )?static readonly Color Down = .*?;', 'private static Color Down => MarketColors.Down;',text)
    path.write_text(text,encoding='utf-8')
path=Path('Assets/Scripts/NightShift/DeviceTerminalUI.cs'); text=path.read_text(encoding='utf-8-sig')
text=text.replace(', Up = new Color32(239, 89, 100, 255), Down = new Color32(76, 150, 232, 255)', '')
text=text.replace('change.color = delta >= 0m ? Up : Down;', 'change.color = MarketColors.ForChange(delta,Ink);')
path.write_text(text,encoding='utf-8')
path=Path('Assets/Scripts/Hold/HoldListItem.cs'); text=path.read_text(encoding='utf-8-sig')
text=text.replace('private static readonly Color Up = new Color32(239,89,100,255), Down = new Color32(76,150,232,255), Neutral', 'private static readonly Color Neutral')
text=text.replace('evaluationProfitLoss > 0m ? Up : evaluationProfitLoss < 0m ? Down : Neutral','MarketColors.ForChange(evaluationProfitLoss,Neutral)')
path.write_text(text,encoding='utf-8')
path=Path('Assets/Scripts/NightShift/TradingOrderView.cs'); text=path.read_text(encoding='utf-8-sig')
text=text.replace('private static readonly Color Up = new Color32(239,89,100,255), Down = new Color32(76,150,232,255);','private static readonly Color Error = new Color32(239,89,100,255);')
text=text.replace('ui.validation.color = Up;', 'ui.validation.color = Error;').replace('ui.confirmation.color=Up;', 'ui.confirmation.color=Error;')
text=text.replace('value>0?Up:value<0?Down:Ink','MarketColors.ForChange(value,Ink)')
path.write_text(text,encoding='utf-8')
path=Path('Assets/Scripts/NightShift/TabletKitStyle.cs'); text=path.read_text(encoding='utf-8-sig')
text=text.replace('Up => Parse(Palette.profitKR)', 'Up => MarketColors.Up').replace('Down => Parse(Palette.lossKR)', 'Down => MarketColors.Down'); path.write_text(text,encoding='utf-8')

rows=[('시세 색상','Market colors','株価の色'),('상승 / 하락','Up / Down','上昇 / 下落'),('빨강 / 파랑','Red / Blue','赤 / 青'),('초록 / 빨강','Green / Red','緑 / 赤'),('▲ 상승','▲ Up','▲ 上昇'),('▼ 하락','▼ Down','▼ 下落')]
ip=Path('Assets/Resources/Localization/SourceIndex.json'); catalog=Path('Assets/Localization/Import/DonDon.csv')
for path in [ip,catalog]:
    backup=Path('Validation/MarketColors/before')/path
    if not backup.exists():
        backup.parent.mkdir(parents=True,exist_ok=True); shutil.copy2(path,backup)
index=json.loads(ip.read_text(encoding='utf-8-sig')); known={r['source'] for r in index['entries'] if not r.get('templateOnly',False)}
with catalog.open('a',encoding='utf-8',newline='') as out:
    writer=csv.writer(out)
    for ko,en,ja in rows:
        if ko in known: continue
        key='text.'+hashlib.sha256(ko.encode()).hexdigest()[:16]
        writer.writerow([key,'Market color preferences',ko,en,ja]); index['entries'].append(dict(key=key,source=ko,templateOnly=False)); known.add(ko)
ip.write_text(json.dumps(index,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
for path in ['Assets/Scripts/Settings/MarketColors.cs','Assets/Editor/MarketColorsValidation.cs']:
    meta=Path(path+'.meta')
    if not meta.exists(): meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n',encoding='utf-8')
print('Saved scoped baseline, connected direction colors, and registered six localized labels.')
