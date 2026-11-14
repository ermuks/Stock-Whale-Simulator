using UnityEngine;

[System.Serializable]
public struct CandleData
{
    public double completedTime;
    // 표시용 게임 시각 스냅샷. 초기 생성 시세는 0으로 남겨 실제 기록과 구분한다.
    public int marketDay, marketMinute;
    public bool historical;
    public decimal exactClose;
    public decimal nonExchangeVolume;
    public decimal ClosePrice => exactClose > 0m ? exactClose : (decimal)close;

    public float open;
    public float close;
    public float high;
    public float low;
    /// <summary>해당 캔들 구간 동안의 거래량</summary>
    public float volume;
}
