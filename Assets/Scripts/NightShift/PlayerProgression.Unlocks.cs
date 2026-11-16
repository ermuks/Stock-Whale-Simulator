using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어가 돈과 시간을 들여 여는 정보. 비용은 실제 계좌 현금에서 한 번 빠지고, 게임시간이 지나야 열린다.
/// 상태 필드는 PlayerProgression의 일부로 세션 저장에 함께 들어간다.
/// </summary>
public sealed partial class PlayerProgression
{
    public const string CrowdValueUnlock = "chart.crowdValue", RealValueUnlock = "chart.realValue";

    [Serializable]
    public sealed class UnlockItem
    {
        public string id, title, description, requires;
        public long cost;
        public int minutes;
    }

    [Serializable]
    public sealed class UnlockCatalog
    {
        public int schemaVersion = 1;
        public UnlockItem[] items;
    }

    public enum UnlockState
    {
        Locked,
        Available,
        InProgress,
        Unlocked
    }

    // 목록은 설정 파일에서 읽는 정적 자료다. 저장하지 않는다.
    private static UnlockItem[] unlockItems;
    private readonly List<string> unlocked = new();
    private string pendingUnlock;
    private double pendingReadyAt;
    public event Action UnlocksChanged;
    public string UnlockResult { get; private set; } = "";

    public IReadOnlyList<UnlockItem> UnlockItems
    {
        get
        {
            if (unlockItems == null)
            {
                var data = Resources.Load<TextAsset>("Progression/PlayerUnlocks");
                var catalog = data == null ? null : JsonUtility.FromJson<UnlockCatalog>(data.text);
                var valid = new List<UnlockItem>();
                if (catalog != null && catalog.schemaVersion == 1 && catalog.items != null)
                {
                    foreach (var item in catalog.items)
                    {
                        if (item != null && !string.IsNullOrEmpty(item.id) && item.cost >= 0 && item.minutes >= 0)
                        {
                            valid.Add(item);
                        }
                    }
                }

                unlockItems = valid.ToArray();
            }

            return unlockItems;
        }
    }

    public static bool Has(string id)
    { return Instance != null && Instance.IsUnlocked(id); }

    public bool IsUnlocked(string id)
    { return !string.IsNullOrEmpty(id) && unlocked.Contains(id); }

    public UnlockItem FindUnlock(string id)
    {
        foreach (var item in UnlockItems)
        {
            if (item.id == id)
            { return item; }
        }

        return null;
    }

    public UnlockState StateOf(UnlockItem item)
    {
        if (item == null)
        { return UnlockState.Locked; }

        if (IsUnlocked(item.id))
        { return UnlockState.Unlocked; }

        if (pendingUnlock == item.id)
        { return UnlockState.InProgress; }

        return string.IsNullOrEmpty(item.requires) || IsUnlocked(item.requires) ? UnlockState.Available : UnlockState.Locked;
    }

    public string PendingUnlock => pendingUnlock;

    /// <summary>진행 중인 해금이 끝날 때까지 남은 게임 분.</summary>
    public double UnlockRemainingMinutes
    {
        get
        {
            if (string.IsNullOrEmpty(pendingUnlock) || MarketDayController.Instance == null)
            { return 0; }

            return Math.Max(0, pendingReadyAt - MarketDayController.Instance.AbsoluteGameMinute);
        }
    }

    public bool CanStartUnlock(UnlockItem item, out string reason)
    {
        reason = "";
        if (item == null)
        {
            reason = "항목 없음";
            return false;
        }

        var state = StateOf(item);
        if (state == UnlockState.Unlocked)
        {
            reason = "해금 완료";
            return false;
        }

        if (state == UnlockState.InProgress)
        {
            reason = "해금 진행 중";
            return false;
        }

        if (state == UnlockState.Locked)
        {
            reason = "선행 해금 필요";
            return false;
        }

        if (!string.IsNullOrEmpty(pendingUnlock))
        {
            reason = "다른 해금 진행 중";
            return false;
        }

        var payer = StockManager.Instance == null ? null : StockManager.Instance.playerAccount;
        if (payer == null || MarketDayController.Instance == null || !MarketDayController.Instance.Ready)
        {
            reason = "준비 중";
            return false;
        }

        if (PlayerAccounts.Available(payer) < item.cost)
        {
            reason = "가용 현금 부족";
            return false;
        }

        return true;
    }

    public bool TryStartUnlock(string id)
    {
        var item = FindUnlock(id);
        if (!CanStartUnlock(item, out string reason))
        {
            UnlockResult = reason;
            return false;
        }

        var payer = StockManager.Instance.playerAccount;
        // 정보 비용은 시장 밖으로 나가는 지출이다. 일일 손익에는 매매 손실이 아닌 외부 지출로 잡힌다.
        payer.cash -= item.cost;
        pendingUnlock = item.id;
        pendingReadyAt = MarketDayController.Instance.AbsoluteGameMinute + item.minutes;
        UnlockResult = "해금 시작";
        if (item.minutes <= 0)
        {
            CompleteUnlock();
        }
        else
        {
            UnlocksChanged?.Invoke();
        }

        return true;
    }

    private void CompleteUnlock()
    {
        string id = pendingUnlock;
        pendingUnlock = null;
        pendingReadyAt = 0;
        if (!string.IsNullOrEmpty(id) && !unlocked.Contains(id))
        {
            unlocked.Add(id);
            var item = FindUnlock(id);
            MarketEventFeed.Instance?.Notify("해금 완료 · " + (item == null ? id : item.title));
        }

        UnlockResult = "해금 완료";
        UnlocksChanged?.Invoke();
    }

    void Update()
    {
        if (string.IsNullOrEmpty(pendingUnlock) || MarketDayController.Instance == null || !MarketDayController.Instance.Ready)
        { return; }

        if (FindUnlock(pendingUnlock) == null)
        {
            // 목록에서 사라진 항목은 조용히 정리한다.
            pendingUnlock = null;
            return;
        }

        if (MarketDayController.Instance.AbsoluteGameMinute >= pendingReadyAt)
        {
            CompleteUnlock();
        }
    }
}
