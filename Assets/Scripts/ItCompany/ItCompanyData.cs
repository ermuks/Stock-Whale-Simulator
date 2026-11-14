using System;
using UnityEngine;

/// <summary>
/// itCompany.js의 itCompanyData 구조. JSON 파싱용.
/// </summary>
[Serializable]
public class ItCompanyDataRoot
{
    public string companyType;
    public int startNodeId;
    public ItCompanyNode[] nodes;
}

public enum CompanyEventKind
{
    News,
    Delisting
}

[Serializable]
public class ItCompanyNode
{
    public int id;
    public CompanyEventKind EventKind => id == 1997 || id == 1998 || id == 1999 ? CompanyEventKind.Delisting : CompanyEventKind.News;

    public string description;
    public float criticalValueMin;
    public float criticalValueMax;
    public int[] next;
    public int[] nextWeight;
    public float stayTimeRangeMin;
    public float stayTimeRangeMax;
}

/// <summary>
/// itCompany.js 파일에서 JSON 블록을 추출해 파싱.
/// </summary>
public static class ItCompanyLoader
{
    const string Prefix = "const itCompanyData = ";
    /// <summary>
    /// JS 파일 내용에서 itCompanyData 객체만 추출해 파싱. 실패 시 null.
    /// </summary>
    public static ItCompanyDataRoot ParseFromJsContent(string jsContent)
    {
        if (string.IsNullOrEmpty(jsContent))
        { return null; }

        int start = jsContent.IndexOf(Prefix, StringComparison.Ordinal);
        if (start < 0)
        { return null; }

        start += Prefix.Length;
        int braceStart = jsContent.IndexOf('{', start);
        if (braceStart < 0)
        { return null; }

        int depth = 0;
        int end = -1;
        for (int i = braceStart; i < jsContent.Length; i++)
        {
            char c = jsContent[i];
            if (c == '{')
            {
                depth++;
            }
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                {
                    end = i;
                    break;
                }
            }
        }

        if (end < 0)
        { return null; }

        string json = jsContent.Substring(braceStart, end - braceStart + 1);
        return JsonUtility.FromJson<ItCompanyDataRoot>(json);
    }
}
