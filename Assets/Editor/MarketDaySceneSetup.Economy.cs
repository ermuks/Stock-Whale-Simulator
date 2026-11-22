using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static partial class MarketDaySceneSetup
{
    [MenuItem("DonDon/Connect Initial Economy and Owner Bots")]
    public static void ConnectEconomy()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes 1/TradeScene.unity");
        var manager = Object.FindFirstObjectByType<StockManager>();
        if (manager == null)
        {
            throw new System.InvalidOperationException("StockManager is required.");
        }

        if (!manager.useInitialEconomy)
        {
            manager.economy = new EconomySettings();
            manager.useInitialEconomy = true;
        }

        if (manager.playerAccount == null)
        {
            foreach (var trader in Object.FindObjectsByType<Trader>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (trader.GetType() == typeof(Trader))
                {
                    manager.playerAccount = trader;
                    break;
                }
            }

            if (manager.playerAccount == null)
            {
                manager.playerAccount = new GameObject("Player Account").AddComponent<Trader>();
            }
        }

        foreach (var holdings in Object.FindObjectsByType<HoldUIManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (holdings.account == null)
            {
                holdings.account = holdings.whale != null ? holdings.whale : manager.playerAccount;
                EditorUtility.SetDirty(holdings);
            }
        }

        var candles = Object.FindFirstObjectByType<CandleHistoryManager>();
        if (candles != null)
        {
            candles.candleDuration = 5f;
            EditorUtility.SetDirty(candles);
        }

        EditorUtility.SetDirty(manager);
        ConnectRiskFontFallbacks();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Initial economy connected: one player account, mandatory setup, 8 listed / 3 unlisted definitions, existing unified exchange.");
    }

    private static void ConnectRiskFontFallbacks()
    {
        var fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Chat/Chat Korean.asset");
        if (fallback == null)
        { return; }

        fallback.TryAddCharacters("안전경고위험거래중지판정대기플레이어기업초발행량공개비율현금조달본인확설시작단가총자산최종보유수량");
        EditorUtility.SetDirty(fallback);
        foreach (var text in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            AddRiskFallback(text.font, fallback);
        }

        var stockListPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/StockListItem.prefab");
        if (stockListPrefab != null)
        {
            foreach (var text in stockListPrefab.GetComponentsInChildren<TMP_Text>(true))
            {
                AddRiskFallback(text.font, fallback);
            }
        }
    }

    private static void AddRiskFallback(TMP_FontAsset asset, TMP_FontAsset fallback)
    {
        if (asset == null || asset == fallback || !AssetDatabase.GetAssetPath(asset).StartsWith("Assets/") || asset.fallbackFontAssetTable.Contains(fallback))
        { return; }

        asset.fallbackFontAssetTable.Add(fallback);
        EditorUtility.SetDirty(asset);
    }
}
