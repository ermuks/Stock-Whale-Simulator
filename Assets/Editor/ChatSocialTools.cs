using UnityEditor;
using UnityEngine;

public static class ChatSocialTools
{
    [MenuItem("DonDon/Chat/Reload social settings")]
    public static void Reload()
    {
        AssetDatabase.Refresh(); ChatSocialAssets.Reload();
        Debug.Log("Chat social catalogs reloaded, generation " + ChatSocialAssets.Generation);
    }
}
