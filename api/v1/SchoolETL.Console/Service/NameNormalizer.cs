using System.Text;
using System.Text.RegularExpressions;


namespace SchoolETL.ConsoleApp.Service;


public static class NameNormalizer
{
    public static string? Clean(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim();
        s = Regex.Replace(s, "\\s+", " ");
        s = s.Replace("\u00A0", " "); // NBSP
        return s;
    }


    public static string Key(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;
        s = s.ToUpperInvariant();
        var normalized = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalized)
        {
            var uc = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (uc != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return Regex.Replace(sb.ToString(), "[^A-Z0-9 ]", "").Trim();
    }
}