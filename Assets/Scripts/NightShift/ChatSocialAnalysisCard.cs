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
    // ── 분석이 아닌 보고서 카드(세력 탐지, 비교, 기사 판정, 방 분위기, 계좌 진단, 성적, 브리핑) ──
    /// <summary>보고서의 갈래. 비어 있으면 종목 분석 카드다(예전 저장의 카드는 비어 있다).</summary>
    public string kind;
    /// <summary>제목, 고리 안에 적는 글(비면 점수), 고리 아래에 적는 글.</summary>
    public string title, scoreText, badge;
    /// <summary>오른쪽 네 줄의 이름과 값. tones는 줄마다의 색: 1 오름 색, -1 내림 색, 4 경고, 0 보통.</summary>
    public string[] captions, values;
    public int[] tones;
    /// <summary>고리의 색: 1 오름 색, -1 내림 색, 0 노랑, 2 점수대로(분석 카드와 같다), 3 흐린 글자색, 4 경고.</summary>
    public int tone;
    /// <summary>줄 이름 칸의 너비(픽셀, 0이면 분석 카드와 같은 90).</summary>
    public int captionWidth;
}
