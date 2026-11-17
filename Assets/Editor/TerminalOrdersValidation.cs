using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Bounded edit-mode fixture using the existing scene Canvas/camera and real order book. Never runs bots or saves the scene.</summary>
public static class TerminalOrdersValidation
{
    private const string Output = "Validation/TerminalOrders/";
    private static readonly StringBuilder report = new();
    private static Camera camera;
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        try
        {
            EditorSceneManager.OpenScene("Assets/Scenes 1/TradeScene.unity");
            Application.targetFrameRate = 15; QualitySettings.vSyncCount = 0;
            camera = Camera.main; camera.aspect = 16f / 9f; camera.fieldOfView = 45f;
            var first = Object.FindObjectsByType<TabletAccountUI>(FindObjectsInactive.Include).Single(ui => !ui.second);
            var ui = first.orders;
            var manager = Object.FindAnyObjectByType<StockManager>(); StockManager.Instance = manager;
            manager.useInitialEconomy = false;
            var a = new Stock("DSA","덕상가주에이아이",1000,1000000);
            var b = new Stock("DDP","대만디스플레이",2000,1000000);
            manager.stocks = new List<Stock> { a,b }; manager.CurrentStock = a;
            var account = new GameObject("Validation Account").AddComponent<Trader>(); account.cash = 1000000; account.GrantShares(a,200,750);
            manager.playerAccount = account; ui.session.account = account; first.account = account;
            Invoke(ui,"Start"); first.ShowPage(TabletAccountUI.Page.Orders);
            ui.thisTablet.viewCamera = camera; ui.thisTablet.moveSpeed = 0; ui.thisTablet.HoldForDeskView();
            Refresh(ui);
            Submit(ui,"900","100"); var oldest = ui.session.Latest;
            Submit(ui,"800","50");
            manager.CurrentStock = b; Refresh(ui); Submit(ui,"1200","20");
            manager.CurrentStock = a; Refresh(ui); Click(ui.sell); Submit(ui,"1400","100");
            var seller = new GameObject("Validation Counterparty").AddComponent<Trader>(); seller.cash = 1000000; seller.GrantShares(a,10,1000); seller.SellLimit(900,10,0,a);
            Invoke(ui.session,"Update"); Refresh(ui);
            Check(ui.session.ActiveOrders.Count == 4 && oldest.Filled == 10,"real UI submits four independent requests and tracks an older partial fill");
            var pending = ui.panel.GetComponentInChildren<PendingOrderListUI>(); pending.Refresh();
            Check(pending.Count == 4 && ui.prepare.interactable,"pending rows remain visible and composer stays usable");
            Inspect(pending.gameObject);
            Capture("pending-1600x900.png",1600,900); Capture("pending-1920x1080.png",1920,1080);
            var cancel = pending.GetComponentsInChildren<Button>().First(); Click(cancel); Refresh(ui);
            Check(pending.Count == 3 && account.GetOwnedVolume(a) == 210 && account.ReservedCash == 64000,"row cancellation returns only that remainder and preserves other buys and filled shares");
            Check(account.EscrowVolume == 100,"row cancellation leaves the separate sell order intact");
            var scroll = pending.GetComponentInChildren<ScrollRect>();
            Check(scroll.vertical && !scroll.horizontal && scroll.viewport.GetComponent<RectMask2D>() != null,"pending rows scroll inside a clipped viewport");
            Capture("after-row-cancel.png",1600,900);
            Click(ui.buy); Submit(ui,"700","1");
            Check(ui.session.ActiveOrders.Count == 4,"another order submits while older requests remain pending");
            ui.session.Cancel(); ui.thisTablet.ReturnForDeskView();
            report.AppendLine("No play mode, bots, scene saves or gameplay speed changes were used.");
            File.WriteAllText(Output+"ui-results.txt",report.ToString()); EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            report.AppendLine("FAIL "+ex); File.WriteAllText(Output+"ui-results.txt",report.ToString()); EditorApplication.Exit(1);
        }
    }
    private static void Submit(DeviceTerminalUI ui,string price,string quantity)
    { ui.price.SetTextWithoutNotify(price); ui.quantity.SetTextWithoutNotify(quantity); Refresh(ui); Click(ui.review); Refresh(ui); }
    private static void Refresh(DeviceTerminalUI ui)
    { typeof(DeviceTerminalUI).GetField("next",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(ui,0f); Invoke(ui,"Update"); Canvas.ForceUpdateCanvases(); }
    private static void Invoke(object target,string method)
    { target.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,null); }
    private static void Click(Button button)
    {
        Canvas.ForceUpdateCanvases();
        Check(button.gameObject.activeInHierarchy && button.interactable,"clickable "+button.name);
        var rect = (RectTransform)button.transform;
        var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(rect.rect.center)),button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer,hits);
        Check(hits.Count > 0 && (hits[0].gameObject == button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform)),"frontmost raycast reaches "+button.name);
        ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerClickHandler);
    }
    private static void Inspect(GameObject root)
    {
        Canvas.ForceUpdateCanvases();
        foreach (var text in root.GetComponentsInChildren<TextMeshProUGUI>())
        {
            text.ForceMeshUpdate();
            Check(!text.isTextOverflowing,"fits "+text.name);
        }
    }
    private static void Capture(string name,int width,int height)
    {
        Canvas.ForceUpdateCanvases();
        var target = new RenderTexture(width,height,24); var previous = RenderTexture.active; var old = camera.targetTexture;
        camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
        var pixels = new Texture2D(width,height,TextureFormat.RGB24,false);
        pixels.ReadPixels(new Rect(0,0,width,height),0,0); pixels.Apply(); File.WriteAllBytes(Output+name,pixels.EncodeToPNG());
        camera.targetTexture = old; RenderTexture.active = previous;
        Object.DestroyImmediate(pixels); target.Release(); Object.DestroyImmediate(target);
    }
    private static void Check(bool condition,string message)
    { if (!condition) { throw new Exception(message); } report.AppendLine("PASS "+message); }
}
