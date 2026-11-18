using UnityEngine;

/// <summary>
/// 게임 단축키. 나중에 설정 화면에서 변경 시 PlayerPrefs 등으로 저장/로드 가능.
/// </summary>
public static class GameKeyBindings
{
    const string PrefsPrefix = "KeyBind_";

    public static KeyCode Pause
    {
        get
        {
            KeyCode saved = (KeyCode)PlayerPrefs.GetInt(PrefsPrefix + "Pause", (int)KeyCode.P);
            return saved == KeyCode.Space || saved == KeyCode.Tab ? KeyCode.P : saved;
        }
        set => PlayerPrefs.SetInt(PrefsPrefix + "Pause", (int)value);
    }

    public static KeyCode Interact
    {
        // E는 채팅 화면 단축키가 됐다. 예전에 E로 저장돼 있었어도 F로 읽는다.
        get { var saved = (KeyCode)PlayerPrefs.GetInt(PrefsPrefix + "Interact", (int)KeyCode.F); return saved == KeyCode.E ? KeyCode.F : saved; }
        set => PlayerPrefs.SetInt(PrefsPrefix + "Interact", (int)value);
    }

    public static void Save() => PlayerPrefs.Save();
}
