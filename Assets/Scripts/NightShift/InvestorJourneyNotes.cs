using System;
using TMPro;
using UnityEngine;

public enum StickyNoteLanguage { English, Korean, Japanese }

/// <summary>Passive handwritten goals, localized only for the game's three supported languages.</summary>
public sealed class InvestorJourneyNotes : MonoBehaviour
{
    [Serializable]
    public sealed class Note
    {
        public StickyNotePaper paper;
        public TextMeshProUGUI number, title, detail, footer, status;
    }
    [Serializable]
    public sealed class Copy
    {
        public string[] titles;
        public string observing, waiting, firstSignal, turnover, profit, preparing, eligible, operate, complete, current;
        public string tenThousand, hundredMillion, trillion, won;
    }
    public Note[] notes = new Note[3];
    public Transform framingBottom;
    public StickyNoteLanguage language = StickyNoteLanguage.Korean;
    public int CurrentStage { get; private set; } = -1;
    private static readonly Color Ink = new Color32(46, 43, 35, 255);
    private static readonly Color SecondaryInk = new Color32(88, 80, 65, 255);
    private static readonly Color ActiveInk = new Color32(122, 45, 30, 255);
    private int completedMask;
    private bool initialized;
    private Copy copy;
    private StickyNoteLanguage loadedLanguage = (StickyNoteLanguage)(-1);
    private int localeRevision = -1;

    public void InitializeMilestones(int savedMask)
    {
        if (initialized)
        { return; }
        initialized = true; completedMask = savedMask & 7;
        for (int i = 0; i < notes.Length; i++)
        {
            if ((completedMask & (1 << i)) != 0)
            { notes[i].paper?.HideCompleted(); }
        }
    }

    public void SetLanguage(StickyNoteLanguage value)
    {
        if (value != StickyNoteLanguage.English && value != StickyNoteLanguage.Korean && value != StickyNoteLanguage.Japanese)
        { throw new ArgumentOutOfRangeException(nameof(value)); }
        language = value; EnsureCopy(); CurrentStage = -1;
    }

    private void EnsureCopy()
    {
        if (GameLocale.Ready && localeRevision != GameLocale.Revision)
        {
            localeRevision = GameLocale.Revision;
            language = GameLocale.Code == "en" ? StickyNoteLanguage.English : GameLocale.Code == "ja" ? StickyNoteLanguage.Japanese : StickyNoteLanguage.Korean;
            copy = null;
        }
        if (copy != null && loadedLanguage == language)
        { return; }
        string code;
        switch (language)
        {
            case StickyNoteLanguage.English: code = "en"; break;
            case StickyNoteLanguage.Korean: code = "ko"; break;
            case StickyNoteLanguage.Japanese: code = "ja"; break;
            default: throw new ArgumentOutOfRangeException(nameof(language));
        }
        var data = Resources.Load<TextAsset>("Localization/StickyNotes/" + code);
        if (data == null)
        { throw new InvalidOperationException("Missing sticky note language: " + code); }
        copy = JsonUtility.FromJson<Copy>(data.text); loadedLanguage = language; CurrentStage = -1;
        if (GameLocale.Ready)
        {
            for (int i = 0; i < copy.titles.Length; i++) { copy.titles[i] = GameLocale.Get("notes.title." + i,copy.titles[i]); }
            foreach (var field in typeof(Copy).GetFields())
            { if (field.FieldType == typeof(string)) { field.SetValue(copy,GameLocale.Get("notes." + field.Name,(string)field.GetValue(copy))); } }
        }
        for (int i = 0; i < notes.Length; i++)
        {
            if ((completedMask & (1 << i)) == 0) { continue; }
            Set(notes[i].title,copy.titles[i]); Set(notes[i].detail,copy.complete); Set(notes[i].footer,""); Set(notes[i].status,copy.complete);
        }
    }

    public void Refresh(NpcLeadingRoom npc, PlayerProgression career, int step)
    {
        if (npc == null || career == null || notes.Length != 3)
        { return; }
        EnsureCopy();
        Set(notes[1].number, "02 / " + copy.won);
        if ((completedMask & 1) == 0)
        {
            Set(notes[0].title, copy.titles[0]);
            Set(notes[0].detail, npc.JoinRequested ? copy.waiting : copy.observing);
            Set(notes[0].footer, copy.firstSignal);
        }
        if ((completedMask & 2) == 0)
        {
            Set(notes[1].title, copy.titles[1]);
            Set(notes[1].detail, copy.turnover + " " + Amount(career.Turnover) + " / " + Amount(career.rules.leadingTurnover));
            Set(notes[1].footer, copy.profit + " " + Amount(career.RealizedProfit) + " / " + Amount(career.rules.leadingRealizedProfit));
        }
        if ((completedMask & 4) == 0)
        {
            Set(notes[2].title, copy.titles[2]);
            Set(notes[2].detail, career.Eligible ? copy.eligible : copy.preparing);
            Set(notes[2].footer, copy.operate);
        }
        int changed = career.JourneyMilestones & ~completedMask;
        float delay = .45f;
        for (int i = 0; i < notes.Length; i++)
        {
            if ((changed & (1 << i)) == 0)
            { continue; }
            Set(notes[i].status, copy.complete); notes[i].paper?.Release(delay); delay += .65f;
        }
        completedMask |= changed;
        int current = (completedMask & 1) == 0 ? 0 : (completedMask & 2) == 0 ? 1 : (completedMask & 4) == 0 ? 2 : -1;
        if (current == CurrentStage)
        { return; }
        CurrentStage = current;
        for (int i = 0; i < notes.Length; i++)
        {
            Set(notes[i].status, (completedMask & (1 << i)) != 0 ? copy.complete : i == current ? copy.current : "");
            notes[i].title.color = i == current ? ActiveInk : Ink;
            notes[i].status.color = i == current ? ActiveInk : SecondaryInk;
        }
    }

    private string Amount(decimal value)
    {
        decimal magnitude = Math.Abs(value);
        if (language == StickyNoteLanguage.English)
        {
            decimal divisor = magnitude >= 1000000000000m ? 1000000000000m : magnitude >= 1000000000m ? 1000000000m : magnitude >= 1000000m ? 1000000m : magnitude >= 1000m ? 1000m : 1m;
            string suffix = divisor == 1000000000000m ? "T" : divisor == 1000000000m ? "B" : divisor == 1000000m ? "M" : divisor == 1000m ? "K" : "";
            return (value / divisor).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + suffix;
        }
        if (magnitude < 10000m)
        { return value.ToString("#,##0;-#,##0;0", System.Globalization.CultureInfo.InvariantCulture); }
        decimal unit = magnitude >= 1000000000000m ? 1000000000000m : magnitude >= 100000000m ? 100000000m : 10000m;
        string ending = unit == 1000000000000m ? copy.trillion : unit == 100000000m ? copy.hundredMillion : copy.tenThousand;
        return (value / unit).ToString("#,##0.#;-#,##0.#;0", System.Globalization.CultureInfo.InvariantCulture) + ending;
    }
    private static void Set(TMP_Text label, string value)
    {
        if (label != null && !GameLocalizedText.Matches(label,value))
        { GameLocalizedText.Set(label,value,true); }
    }
}
