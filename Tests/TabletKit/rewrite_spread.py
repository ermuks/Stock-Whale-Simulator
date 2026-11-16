from pathlib import Path
p=Path('Assets/Scripts/NightShift/SpreadTradingUI.cs')
s=p.read_text(encoding='utf-8-sig')
s=s.replace('private Button start, cancel;', 'private Button start, cancel, previous, following;\n    private TextMeshProUGUI elapsed, updated, tape;')
a=s.index('        Label("Title","스프레드"')
b=s.index('\n    private void BuildSlider()',a)
s=s[:a]+'''        Label("Title","자동 매매",32,8,512,56,40);
        state = Label("State","",960,8,608,56,28); state.color = TabletKitStyle.Accent;
        stockName = Label("Stock","",88,88,720,64,38);
        previous = Action("Previous Stock","‹",32,88,48,() => Select(-1));
        following = Action("Next Stock","›",832,88,48,() => Select(1));
        currentPrice = Number("Current Price",32,168,800,56,40);
        Label("Current Price Unit","원",848,168,32,56,25).color=Muted;
        Label("Range Label","스프레드 범위",32,264,480,48,29);
        range = Label("Range","",544,264,336,48,30); range.alignment=TextAlignmentOptions.MidlineRight;
        BuildSlider();
        Label("Bid Label","예상 매수가",32,432,400,40,27).color=Muted;
        Label("Ask Label","예상 매도가",480,432,400,40,27).color=Muted;
        bid = Number("Bid",32,480,400,56,40); ask = Number("Ask",480,480,400,56,40);
        cash = Metric("주문 가능 현금",32,584,848,"원");
        shares = Metric("매도 가능 수량",32,640,848,"주");
        elapsed = Label("Elapsed","",32,720,848,40,27);
        start = Action("Start Spread","매매 시작",32,792,408,StartTrading,true);
        cancel = Action("Cancel Spread","매매 취소",472,792,408,CancelTrading);
        var divider=Graphic("Auto Divider",transform,TabletKitStyle.Line);
        TradingOrderView.Place(divider.rectTransform,transform,920,88,1,752,1600,880);
        Label("Pending Title","미체결",960,88,608,56,34);
        buyRemaining=Metric("매수 잔량",960,168,608,"주");
        sellRemaining=Metric("매도 잔량",960,224,608,"주");
        bought=Metric("매수 체결",960,304,608,"주");
        sold=Metric("매도 체결",960,360,608,"주");
        realized=Metric("실현손익",960,416,608,"원");
        Label("Recent Fills","최근 체결",960,504,608,48,34);
        tape=Label("Fill Tape","—",960,568,608,176,27); tape.textWrappingMode=TextWrappingModes.Normal;
        updated=Label("Updated","",960,784,608,56,25); updated.color=Muted;
    }
    private void Select(int delta)
    {
        var manager=StockManager.Instance;
        if (operation.Active || manager==null || manager.stocks.Count==0)
        { return; }
        int index=manager.stocks.IndexOf(manager.CurrentStock);
        manager.SelectStock((index+delta+manager.stocks.Count)%manager.stocks.Count); next=0;
    }
''' + s[b:]
s=s.replace('48,564,1504,56','48,328,816,56').replace('32,616,160,32','32,392,160,32').replace('1408,616,160,32','720,392,160,32')
s=s.replace('start.interactable = !operation.Active', '''previous.interactable=following.interactable=!operation.Active;
        DeviceTerminalUI.Set(elapsed,operation.SpreadStartedGameMinute>0 ? "실행 경과 · "+DeviceMarketJournal.Duration(operation.SpreadElapsedGameMinutes) : "");
        DeviceTerminalUI.Set(updated,operation.SpreadUpdatedStamp ?? "");
        var buffer=new System.Text.StringBuilder();
        int shown=0;
        foreach (var fill in operation.SpreadFills)
        {
            if (++shown>3) { break; }
            buffer.Append(fill.Time).Append("  ").Append(fill.Buy ? "매수" : "매도").AppendLine();
            buffer.Append(TradingOrderView.Number(fill.Price)).Append("원 · ").Append(TradingOrderView.Number(fill.Quantity)).Append("주").AppendLine();
        }
        DeviceTerminalUI.Set(tape,buffer.Length>0 ? buffer.ToString().TrimEnd() : "—");
        start.interactable = !operation.Active''')
p.write_text(s,encoding='utf-8')
