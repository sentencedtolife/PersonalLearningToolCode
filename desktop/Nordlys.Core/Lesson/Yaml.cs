// YAML text -> plain values, resolved like js-yaml's default ("core") schema so the format behaves the same
// as before: plain 5 is a number, "5" is text, true/false are booleans, ~ / null / empty are null.
// Values: null | string | long | double | bool | List<object?> | Dictionary<string, object?>.
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Nordlys.Core.Lesson;

public sealed class YamlError(string reason, int line) : Exception(reason)
{
    public string Reason { get; } = reason;
    /// <summary>0-based line inside the YAML snippet.</summary>
    public int Line { get; } = line;
}

public static partial class Yaml
{
    public static object? Load(string text)
    {
        var stream = new YamlStream();
        try
        {
            stream.Load(new StringReader(text));
        }
        catch (YamlException e)
        {
            var reason = (e.InnerException as YamlException)?.Message ?? e.Message;
            var line = (int)Math.Max(0, (e.InnerException as YamlException ?? e).Start.Line - 1);
            throw new YamlError(Clean(reason), line);
        }
        return stream.Documents.Count == 0 ? null : Convert(stream.Documents[0].RootNode);
    }

    // YamlDotNet messages start with "(Line: 3, Col: 5, Idx: 20) - (Line: …): " — the line is reported separately.
    private static string Clean(string message) => PositionPrefix().Replace(message, "").Trim().TrimEnd('.');

    private static object? Convert(YamlNode node) => node switch
    {
        YamlScalarNode s => Scalar(s),
        YamlSequenceNode seq => seq.Children.Select(Convert).ToList(),
        YamlMappingNode map => map.Children.ToDictionary(kv => KeyText(kv.Key), kv => Convert(kv.Value)),
        _ => null,
    };

    private static string KeyText(YamlNode key) => key is YamlScalarNode s ? s.Value ?? "" : key.ToString();

    private static object? Scalar(YamlScalarNode s)
    {
        var v = s.Value ?? "";
        if (s.Style != YamlDotNet.Core.ScalarStyle.Plain) return v; // quoted or block scalar: always text
        if (v is "" or "~" or "null" or "Null" or "NULL") return null;
        if (v is "true" or "True" or "TRUE") return true;
        if (v is "false" or "False" or "FALSE") return false;
        if (IntRe().IsMatch(v) && long.TryParse(v, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var i)) return i;
        if (v.StartsWith("0x") && long.TryParse(v[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex)) return hex;
        if (FloatRe().IsMatch(v) && double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return d;
        if (v is ".inf" or ".Inf" or ".INF" or "+.inf") return double.PositiveInfinity;
        if (v is "-.inf" or "-.Inf" or "-.INF") return double.NegativeInfinity;
        if (v is ".nan" or ".NaN" or ".NAN") return double.NaN;
        return v;
    }

    public static bool IsScalar(object? v) => v is string or long or double or bool;
    public static bool IsNumber(object? v) => v is long or double;
    public static bool IsText(object? v) => v is string s && s.Trim() != "";
    public static bool IsList(object? v) => v is List<object?>;
    public static bool IsMap(object? v) => v is Dictionary<string, object?>;

    public static bool IsInteger(object? v) => v is long || (v is double d && Math.Floor(d) == d && !double.IsInfinity(d));
    public static double ToDouble(object? v) => v switch { long l => l, double d => d, _ => double.NaN };

    /// <summary>A scalar as text, the way JavaScript's String() prints it.</summary>
    public static string Str(object? v) => v switch
    {
        null => "",
        string s => s,
        bool b => b ? "true" : "false",
        long l => l.ToString(CultureInfo.InvariantCulture),
        double d => NumberText(d),
        _ => "",
    };

    public static string NumberText(double d) =>
        Math.Floor(d) == d && Math.Abs(d) < 1e21 ? ((long)d).ToString(CultureInfo.InvariantCulture) : d.ToString("R", CultureInfo.InvariantCulture);

    /// <summary>Like JSON.stringify in messages: "text", 5, [1,"a"], {"a":1}.</summary>
    public static string Show(object? v) => v switch
    {
        string s => $"\"{s}\"",
        long or double => Str(v),
        bool b => b ? "true" : "false",
        null => "null",
        _ => JsonSerializer.Serialize(ToJsonValue(v)),
    };

    private static object? ToJsonValue(object? v) => v switch
    {
        List<object?> l => l.Select(ToJsonValue).ToList(),
        Dictionary<string, object?> m => m.ToDictionary(kv => kv.Key, kv => ToJsonValue(kv.Value)),
        double d when Math.Floor(d) == d => (long)d,
        _ => v,
    };

    [GeneratedRegex(@"^[-+]?[0-9]+$")]
    private static partial Regex IntRe();

    [GeneratedRegex(@"^[-+]?(\.[0-9]+|[0-9]+(\.[0-9]*)?)([eE][-+]?[0-9]+)?$")]
    private static partial Regex FloatRe();

    [GeneratedRegex(@"\(Line: \d+, Col: \d+, Idx: \d+\) - \(Line: \d+, Col: \d+, Idx: \d+\):\s*")]
    private static partial Regex PositionPrefix();
}
