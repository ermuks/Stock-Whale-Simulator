using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// 플레이용 빌드. 명령줄에서 부른다:
//   Unity.exe -batchmode -quit -projectPath . -executeMethod PlaytestBuild.Build -logFile Logs/playtest-build.log
// 번역 표(Addressables)를 먼저 굽고, 빌드 설정에 켜져 있는 장면으로 Playtest 폴더에 출시용(개발 빌드 아님) 실행 파일을 만든다.
// 번역 표를 CSV에서 다시 들여오지는 않는다. 지금 표 그대로 굽는다.
public static class PlaytestBuild
{
    private const string Folder = "Playtest";
    public static void Build() { Run(Folder, Folder + "/build-results.txt", false); }
    // Steam에 올릴 빌드(SteamBuild/steam_upload.bat가 부른다). 대상 폴더를 비우고 새로 만들며,
    // 결과 파일은 Steam에 올라가지 않도록 폴더 밖(SteamBuild/이름-results.txt)에 쓴다.
    public static void SteamPlaytest() { Run("SteamBuild/playtest", "SteamBuild/playtest-results.txt", true); }
    public static void SteamRelease() { Run("SteamBuild/Release", "SteamBuild/Release-results.txt", true); }

    private static void Run(string folder, string results, bool clean)
    {
        try
        {
            if (clean && Directory.Exists(folder)) { Directory.Delete(folder, true); }
            Directory.CreateDirectory(folder);
            UnityEditor.AddressableAssets.Settings.AddressableAssetSettings.BuildPlayerContent(out var content);
            if (!string.IsNullOrEmpty(content.Error)) { throw new InvalidOperationException("Addressables: " + content.Error); }
            var scenes = new System.Collections.Generic.List<string>();
            foreach (var scene in EditorBuildSettings.scenes) { if (scene.enabled) { scenes.Add(scene.path); } }
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = folder + "/" + PlayerSettings.productName + ".exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            var result = report.summary;
            File.WriteAllText(results, result.result + "\nErrors: " + result.totalErrors + "\nWarnings: " + result.totalWarnings + "\nBytes: " + result.totalSize + "\nSeconds: " + result.totalTime.TotalSeconds.ToString("0") + "\n");
            EditorApplication.Exit(result.result == BuildResult.Succeeded ? 0 : 1);
        }
        catch (Exception error)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(results));
            File.WriteAllText(results, "FAILED\n" + error);
            Debug.LogException(error); EditorApplication.Exit(1);
        }
    }
}
