using UnityEditor;
using UnityEngine;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor.SceneManagement;

public static class ChatSocialTools
{
    [MenuItem("DonDon/Chat/Reload social settings")]
    public static void Reload()
    {
        AssetDatabase.Refresh(); ChatSocialAssets.Reload();
        Debug.Log("Chat social catalogs reloaded, generation " + ChatSocialAssets.Generation);
    }
    // Batch validation of the serialized UI and its real binding methods. Does not save the scene or run the market.
    public static void ValidateProfile()
    {
        const string output = "docs/chat-social-results/stage-6/ui-binding.txt";
        try
        {
            EditorSceneManager.OpenScene("Assets/Scenes 1/TradeScene.unity");
            var profiles = UnityEngine.Object.FindObjectsByType<InvestorProfileUI>(FindObjectsInactive.Include);
            if (profiles.Length == 0) { throw new Exception("Serialized investor profile is missing."); }
            var state = new BotSocialState(900001, 73129, "RandomBot");
            var saved = state.Psychology.CaptureSocial(); saved.trust = 180; saved.betrayal = 130;
            if (!state.Psychology.RestoreSocial(saved)) { throw new Exception("Profile fixture restore failed."); }
            ChatSocialAudit.GameMinute = 100;
            state.Psychology.TryFomo("TEST", 100, 0);
            foreach (var profile in profiles)
            {
                typeof(InvestorProfileUI).GetMethod("Start", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(profile, null);
                profile.Bind(state);
                if (profile.metricButtons[4].gameObject.activeSelf || !profile.heading.text.Contains("FOMO") || !profile.heading.text.Contains("애증"))
                { throw new Exception("FOMO gauge/tag or actual-value quadrant binding failed."); }
                if (!profile.gauges[0].Overheated || !profile.gauges[1].Overheated || profile.gauges[0].Value != 180 || profile.gauges[1].Value != 130)
                { throw new Exception("D01 overheat values were lost in UI binding."); }
                if (profile.metricButtons.Where((button, index) => index != 4).Count(button => button.gameObject.activeSelf) != 5)
                { throw new Exception("Expected five visible metric buttons."); }
                ChatSocialAudit.GameMinute = 121; profile.Bind(state);
                if (profile.heading.text.Contains("FOMO")) { throw new Exception("Expired FOMO tag stayed visible."); }
                ChatSocialAudit.GameMinute = 100;
            }
            File.WriteAllText(output, $"PASS {profiles.Length} serialized profile(s): five gauges, FOMO gauge hidden, timed tag appears/expires, actual-value quadrant, independent 180/130 overheat preserved.\nEdit-mode binding test only; no game-camera render or market execution. Scene was not saved.\n");
        }
        catch (Exception error)
        { File.WriteAllText(output, "FAIL " + error); Debug.LogException(error); EditorApplication.Exit(1); }
    }
}
