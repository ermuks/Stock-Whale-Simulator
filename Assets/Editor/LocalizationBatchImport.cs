using System;
using UnityEditor;
using UnityEngine;

/// <summary>배치모드에서 번역 가져오기 메뉴를 실행하고 끝낸다. 메뉴와 같은 함수를 부른다.</summary>
public static class LocalizationBatchImport
{
    public static void Run()
    {
        try
        {
            DonDonLocalizationSetup.Import();
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogError("Localization import failed: " + exception);
            EditorApplication.Exit(1);
        }
    }
}
