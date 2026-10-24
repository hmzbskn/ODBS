using System.Text;

namespace Advisor.Application.Surveys;

/// <summary>Küçük RFC 4180 uyumlu CSV okuyucu/yazıcı. Türkçe Excel ';' ayırıcı kullandığı için
/// ayırıcı başlık satırından otomatik bulunur (',' / ';' / sekme).</summary>
public static class Csv
{
    public static List<string[]> Parse(string text, out char delimiter)
    {
        text = text.TrimStart('﻿');
        delimiter = DetectDelimiter(text);

        var rows = new List<string[]>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var inQuotes = false;

        void EndCell()
        {
            row.Add(cell.ToString());
            cell.Clear();
        }

        void EndRow()
        {
            EndCell();
            // Tamamen boş satırlar atlanır.
            if (row.Any(c => !string.IsNullOrWhiteSpace(c)))
            {
                rows.Add(row.ToArray());
            }

            row = new List<string>();
        }

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        cell.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    cell.Append(c);
                }

                continue;
            }

            if (c == '"' && cell.Length == 0)
            {
                inQuotes = true;
            }
            else if (c == delimiter)
            {
                EndCell();
            }
            else if (c == '\n')
            {
                EndRow();
            }
            else if (c != '\r')
            {
                cell.Append(c);
            }
        }

        if (cell.Length > 0 || row.Count > 0)
        {
            EndRow();
        }

        return rows;
    }

    private static char DetectDelimiter(string text)
    {
        var firstLine = text.Split('\n', 2)[0];
        var candidates = new[] { ';', ',', '\t' };
        return candidates.OrderByDescending(c => firstLine.Count(ch => ch == c)).First();
    }

    /// <summary>Hücreyi CSV için kaçışlar. '=', '+', '-', '@' ile başlayan değerler Excel'de formül
    /// olarak çalışabileceğinden (CSV enjeksiyonu) başına kesme işareti eklenir.</summary>
    public static string Escape(string? value, char delimiter = ';')
    {
        var v = value ?? string.Empty;
        var isNumber = double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _)
            || double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.GetCultureInfo("tr-TR"), out _);
        if (v.Length > 0 && "=+-@\t\r".Contains(v[0]) && !isNumber)
        {
            v = "'" + v;
        }

        return v.Contains(delimiter) || v.Contains('"') || v.Contains('\n') || v.Contains('\r')
            ? "\"" + v.Replace("\"", "\"\"") + "\""
            : v;
    }

    public static string Line(IEnumerable<string?> cells, char delimiter = ';') =>
        string.Join(delimiter, cells.Select(c => Escape(c, delimiter)));
}
