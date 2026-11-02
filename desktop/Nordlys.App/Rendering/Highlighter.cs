// Small syntax highlighter for GDScript/Python-like code: comments, strings, numbers, keywords, decorators, calls.
// Colours follow the Nord palette and the theme.
using System.Text.RegularExpressions;
using System.Windows.Documents;
using Nordlys.App.UI;

namespace Nordlys.App.Rendering;

public static partial class Highlighter
{
    private static readonly HashSet<string> Keywords =
    [
        "func", "var", "const", "extends", "class_name", "class", "if", "elif", "else", "for", "while", "in", "return", "signal", "match",
        "and", "or", "not", "true", "false", "null", "self", "pass", "break", "continue", "await", "static", "enum", "is", "as", "super",
        "def", "import", "from", "None", "True", "False", "lambda", "with", "yield", "try", "except", "finally", "raise",
    ];

    private static readonly HashSet<string> Languages = ["gdscript", "gd", "python", "py"];

    public static bool Supports(string lang) => Languages.Contains(lang.ToLowerInvariant());

    public static IEnumerable<Run> Runs(string code, string lang)
    {
        if (!Supports(lang))
        {
            yield return new Run(code);
            yield break;
        }
        foreach (Match m in Token().Matches(code))
        {
            var t = m.Value;
            string? key = null;
            if (m.Groups["comment"].Success) key = "HlComment";
            else if (m.Groups["string"].Success) key = "HlString";
            else if (m.Groups["number"].Success) key = "HlNumber";
            else if (m.Groups["deco"].Success) key = "HlMeta";
            else if (m.Groups["word"].Success)
            {
                if (Keywords.Contains(t)) key = "HlKeyword";
                else if (char.IsUpper(t[0])) key = "HlType";
                else if (m.Groups["call"].Success) key = "HlTitle";
            }
            var run = new Run(t);
            if (key != null) run.ResC(TextElement.ForegroundProperty, key);
            if (key == "HlComment") run.FontStyle = System.Windows.FontStyles.Italic;
            yield return run;
        }
    }

    [GeneratedRegex("""(?<comment>#[^\n]*)|(?<string>"(?:[^"\\\n]|\\.)*"|'(?:[^'\\\n]|\\.)*')|(?<deco>@[A-Za-z_]\w*)|(?<number>\b\d+(?:\.\d+)?\b)|(?<word>[A-Za-z_]\w*)(?<call>(?=\s*\())?|(?<other>[^A-Za-z_\d#"'@]+|.)""")]
    private static partial Regex Token();
}
