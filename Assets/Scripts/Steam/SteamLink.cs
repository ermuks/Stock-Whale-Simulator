using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

/// <summary>
/// Steam 클라이언트와의 연결. Steamworks SDK의 steam_api64.dll(Assets/Plugins/x86_64)을 직접 부른다.
/// Steam이 없거나 연결하지 못하면 조용히 꺼진 채로 있고, 게임은 그대로 돌아간다.
/// 어느 앱인지는 Steam이 알려 준다(Steam에서 실행했을 때). Steam 밖에서 실행 파일을 직접 띄워 시험하려면
/// 실행 파일 옆에 앱 ID만 적은 steam_appid.txt를 둔다(그 파일은 Steam에 올리지 않는다).
/// </summary>
public static class SteamLink
{
    private const string Library = "steam_api64";
    public static bool Ready { get; private set; }
    /// <summary>연결하지 못한 까닭(연결됐으면 빈 글).</summary>
    public static string Failure { get; private set; } = "";
    private static IntPtr stats;
    private static bool tried;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int SteamAPI_InitFlat(byte[] error);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern void SteamAPI_Shutdown();
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern void SteamAPI_RunCallbacks();
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr SteamAPI_SteamUserStats_v013();
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool SteamAPI_ISteamUserStats_SetAchievement(IntPtr self, [MarshalAs(UnmanagedType.LPStr)] string name);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool SteamAPI_ISteamUserStats_GetAchievement(IntPtr self, [MarshalAs(UnmanagedType.LPStr)] string name, [MarshalAs(UnmanagedType.I1)] out bool achieved);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool SteamAPI_ISteamUserStats_StoreStats(IntPtr self);
#endif

    /// <summary>Steam에 연결한다. 한 번만 시도한다.</summary>
    public static bool Connect()
    {
        if (tried) { return Ready; }
        tried = true;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        // 편집기에서는 켜지 않는다(시험 삼아 돌리다 내 계정의 도전과제가 풀리지 않도록). 편집기에서 시험하려면 환경 변수 SWS_STEAM_IN_EDITOR=1.
        if (Application.isBatchMode) { Failure = "batch mode"; return false; }
        if (Application.isEditor && Environment.GetEnvironmentVariable("SWS_STEAM_IN_EDITOR") != "1") { Failure = "editor"; return false; }
        try
        {
            var error = new byte[1024];
            int result = SteamAPI_InitFlat(error);
            if (result != 0)
            {
                int length = Array.IndexOf(error, (byte)0);
                Failure = "init " + result + ": " + Encoding.UTF8.GetString(error, 0, length < 0 ? error.Length : length);
                Debug.Log("[Steam] 연결하지 못함 (" + Failure + "). 도전과제는 기록만 해 두고 다음에 올린다.");
                return false;
            }
            stats = SteamAPI_SteamUserStats_v013();
            if (stats == IntPtr.Zero) { Failure = "no user stats"; SteamAPI_Shutdown(); return false; }
            Ready = true; Failure = "";
            Debug.Log("[Steam] 연결됨");
        }
        catch (DllNotFoundException) { Failure = "steam_api64.dll not found"; }
        catch (EntryPointNotFoundException error) { Failure = "steam_api64.dll mismatch: " + error.Message; }
        catch (BadImageFormatException) { Failure = "steam_api64.dll wrong architecture"; }
        if (!Ready) { Debug.Log("[Steam] 꺼짐 (" + Failure + ")"); }
        return Ready;
#else
        Failure = "unsupported platform";
        return false;
#endif
    }

    /// <summary>매 프레임 한 번. Steam이 보낸 소식을 받는다.</summary>
    public static void Pump()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        if (Ready) { SteamAPI_RunCallbacks(); }
#endif
    }

    /// <summary>도전과제를 달성으로 표시한다. Steam에 실제로 올리는 것은 Store()다.</summary>
    public static bool SetAchievement(string id)
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        if (!Ready) { return false; }
        return SteamAPI_ISteamUserStats_SetAchievement(stats, id);
#else
        return false;
#endif
    }

    public static bool HasAchievement(string id)
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        if (!Ready) { return false; }
        return SteamAPI_ISteamUserStats_GetAchievement(stats, id, out bool achieved) && achieved;
#else
        return false;
#endif
    }

    public static bool Store()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        if (!Ready) { return false; }
        return SteamAPI_ISteamUserStats_StoreStats(stats);
#else
        return false;
#endif
    }

    public static void Disconnect()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        if (Ready) { SteamAPI_Shutdown(); }
#endif
        Ready = false; stats = IntPtr.Zero;
    }
}
