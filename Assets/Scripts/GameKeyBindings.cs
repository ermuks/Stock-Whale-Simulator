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
        get => (KeyCode)PlayerPrefs.GetInt(PrefsPrefix + "Interact", (int)KeyCode.E);
        set => PlayerPrefs.SetInt(PrefsPrefix + "Interact", (int)value);
    }

    public static void Save() => PlayerPrefs.Save();
}
