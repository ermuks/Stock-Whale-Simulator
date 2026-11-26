using TMPro;
using UnityEngine;

/// <summary>
/// 번역 표를 거치지 않고 언어마다 직접 적은 글자(한국어·영어·일본어). 언어를 바꾸면 그 언어의 글로 다시 적는다.
/// 화면을 만들 때 한 번만 적는 이름표에 붙인다(그냥 적으면 화면을 만들 때의 언어로 굳는다).
/// </summary>
public sealed class LiteralText : MonoBehaviour
{
    private TMP_Text label;
    private string ko, en, ja;
    private int revision = -1;

    /// <summary>label에 세 언어의 글을 걸고 지금 언어로 적는다. 다시 부르면 글을 바꾼다.</summary>
    public static TMP_Text Apply(TMP_Text label, string ko, string en, string ja)
    {
        if (label == null) { return null; }
        if (!label.TryGetComponent<LiteralText>(out var literal)) { literal = label.gameObject.AddComponent<LiteralText>(); }
        literal.label = label; literal.ko = ko; literal.en = en; literal.ja = ja; literal.revision = -1; literal.Refresh();
        return label;
    }
    public static string Pick(string ko, string en, string ja) => GameLocale.Code == "ja" ? ja : GameLocale.Code == "en" ? en : ko;
    private void Refresh()
    {
        if (label == null || revision == GameLocale.Revision) { return; }
        revision = GameLocale.Revision;
        string value = Pick(ko, en, ja);
        if (!GameLocalizedText.Matches(label, value)) { GameLocalizedText.Set(label, value, true); }
    }
    void LateUpdate() { Refresh(); }
}
