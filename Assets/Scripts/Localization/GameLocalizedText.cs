using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Stores the original display value, refreshes only when it or the locale changes. Never binds input contents.</summary>
public sealed class GameLocalizedText : MonoBehaviour
{
    private static readonly Dictionary<Component,GameLocalizedText> bindings = new();
    private TMP_Text tmp;
    private Text legacy;
    private string source;
    private bool literal;
    private Font originalFont;
    private TMP_InputField tmpInput;
    private InputField legacyInput;
    private static Font japaneseLegacy;
    public string Source => source;
    public static bool Matches(TMP_Text label,string value)
    { return label != null && (bindings.TryGetValue(label,out var binding) ? binding.source == value : label.text == value); }
    public static bool Matches(Text label,string value)
    { return label != null && (bindings.TryGetValue(label,out var binding) ? binding.source == value : label.text == value); }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { bindings.Clear(); japaneseLegacy = null; }
    public static GameLocalizedText Attach(TMP_Text label)
    {
        if (label == null) { return null; }
        if (bindings.TryGetValue(label,out var existing)) { return existing; }
        var input = label.GetComponentInParent<TMP_InputField>();
        if (input != null && input.textComponent == label) { return null; }
        var binding = label.GetComponent<GameLocalizedText>();
        if (binding == null) { binding = label.gameObject.AddComponent<GameLocalizedText>(); }
        binding.tmp = label; binding.tmpInput = input; binding.source = label.text; bindings[label] = binding;
        binding.Refresh(); return binding;
    }
    public static GameLocalizedText Attach(Text label)
    {
        if (label == null) { return null; }
        if (bindings.TryGetValue(label,out var existing)) { return existing; }
        var input = label.GetComponentInParent<InputField>();
        if (input != null && input.textComponent == label) { return null; }
        var binding = label.GetComponent<GameLocalizedText>();
        if (binding == null) { binding = label.gameObject.AddComponent<GameLocalizedText>(); }
        binding.legacy = label; binding.legacyInput = input; binding.originalFont = label.font; binding.source = label.text; bindings[label] = binding;
        binding.Refresh(); return binding;
    }
    public static void Set(TMP_Text label,string value,bool literal=false)
    {
        var binding = Attach(label);
        if (binding == null) { if (label != null && label.text != value) { label.text = value; } return; }
        binding.Assign(value,literal);
    }
    public static void Set(Text label,string value,bool literal=false)
    {
        var binding = Attach(label);
        if (binding == null) { if (label != null && label.text != value) { label.text = value; } return; }
        binding.Assign(value,literal);
    }
    private void Assign(string value,bool isLiteral)
    {
        value ??= "";
        if (source == value && literal == isLiteral) { return; }
        source = value; literal = isLiteral; Refresh();
    }
    private void OnEnable() { GameLocale.Changed += Refresh; Refresh(); }
    private void OnDisable() { GameLocale.Changed -= Refresh; }
    private void Refresh()
    {
        if (source == null) { return; }
        // Runtime UI factories may assign textComponent after creating its label. Never translate/reset user input.
        if (tmpInput != null && tmpInput.textComponent == tmp || legacyInput != null && legacyInput.textComponent == legacy) { return; }
        string value = literal ? source : GameLocale.Text(source);
        if (tmp != null && tmp.text != value) { tmp.text = value; }
        if (legacy != null)
        {
            if (GameLocale.Code == "ja")
            {
                japaneseLegacy ??= GameLocaleFonts.Current?.japaneseLegacy;
                if (japaneseLegacy != null) { legacy.font = japaneseLegacy; }
            }
            else if (originalFont != null) { legacy.font = originalFont; }
            if (legacy.text != value) { legacy.text = value; }
        }
    }
    private void OnDestroy()
    {
        if (tmp != null) { bindings.Remove(tmp); }
        if (legacy != null) { bindings.Remove(legacy); }
    }
}
