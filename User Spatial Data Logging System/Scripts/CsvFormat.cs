using System;
using System.Globalization;

/// <summary>Escapes variable text and formats numeric values for machine-readable CSV.</summary>
public static class CsvFormat
{
    public static string Field(string value)
    {
        if (value == null) return string.Empty;
        return value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0
            ? value : "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    public static string Number(float value, string format = "F4") =>
        value.ToString(format, CultureInfo.InvariantCulture);

    public static string Timestamp(DateTime utc) =>
        utc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    public static string Row(params string[] fields) => string.Join(",", Array.ConvertAll(fields, Field));
}
