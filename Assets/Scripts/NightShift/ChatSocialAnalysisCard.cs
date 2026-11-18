using System;

/// <summary>
/// 종목 분석 AI가 채팅에 붙이는 분석 카드. 올리는 순간의 공개 시세(당일 시가·현재가·고가·저가)와 매수 추천 점수를 찍어 고정한다.
/// 점수는 0(강한 매도)~100(강한 매수)이다. 누구나 보는 시세·호가·거래량으로만 계산하고 진짜 가치는 읽지 않는다. 참고용이며 틀릴 수 있다.
/// </summary>
[Serializable]
public sealed class ChatAnalysisCard
{
    public string symbol, stockName;
    public decimal open, price, high, low;
    public int score;
    public double capturedAt;
}
