using TMPro;
using UnityEngine;

/// <summary>
/// 실행할 때마다 동적 글꼴의 글리프 표와 아틀라스를 비우고 처음부터 다시 채운다.
/// 동적 글꼴은 쓰이는 글자를 그때그때 아틀라스에 그려 넣는다. 에디터에서 플레이를 되풀이하거나(도메인 다시 읽기를 끈 상태),
/// 빌드가 디스크의 아틀라스를 비운 뒤에는 메모리에 남은 글리프 표와 아틀라스 그림이 어긋나서
/// 글자 일부가 빠지거나 엉뚱한 조각이 보일 수 있다. 비우고 시작하면 둘이 늘 맞는다.
/// </summary>
public static class FontAtlasReset
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Clear()
    {
        foreach (var font in Resources.LoadAll<TMP_FontAsset>(""))
        {
            if (font == null || font.atlasPopulationMode != AtlasPopulationMode.Dynamic) { continue; }
            font.ClearFontAssetData(true);
        }
    }
}
