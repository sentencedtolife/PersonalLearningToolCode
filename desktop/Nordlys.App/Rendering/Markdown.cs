// Markdown (Markdig syntax tree) -> WPF FlowDocument blocks. No HTML, no browser: inline HTML is shown as text,
// except the question placeholders (<!--Q:id-->), which become slots for native question cards.
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Markdig;
using Markdig.Extensions.Mathematics;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Nordlys.App.UI;
using Nordlys.Core.Lesson;
using MdBlock = Markdig.Syntax.Block;
using MdInline = Markdig.Syntax.Inlines.Inline;
using MdTable = Markdig.Extensions.Tables.Table;
using MdRow = Markdig.Extensions.Tables.TableRow;
using MdCell = Markdig.Extensions.Tables.TableCell;
using WpfRow = System.Windows.Documents.TableRow;
using WpfCell = System.Windows.Documents.TableCell;
using WpfTable = System.Windows.Documents.Table;

namespace Nordlys.App.Rendering;

public sealed class RenderOptions
{
    /// <summary>Number the ## sections ("1. Heading") and hide the # title (the lesson header shows it).</summary>
    public bool Paper { get; init; }
    /// <summary>Called for each &lt;!--Q:id--&gt; placeholder. Return null to drop it.</summary>
    public Func<string, UIElement?>? Slot { get; init; }
    public IReadOnlyList<GlossaryEntry> Glossary { get; init; } = [];
}

public static partial class Markdown
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UsePipeTables().UseMathematics().UseEmphasisExtras().Build();

    /// <summary>Marks code runs, so the glossary pass and context menus can recognise them.</summary>
    public const string CodeTag = "code";

    public static FlowDocument Document(string markdown, RenderOptions? options = null)
    {
        var doc = NewDocument();
        doc.Blocks.AddRange(Blocks(markdown, options ?? new RenderOptions()));
        return doc;
    }

    public static FlowDocument NewDocument()
    {
        var doc = new FlowDocument { PagePadding = new Thickness(0), TextAlignment = TextAlignment.Left, LineHeight = double.NaN };
        doc.SetResourceReference(FlowDocument.FontFamilyProperty, "PaperFont");
        doc.SetResourceReference(FlowDocument.FontSizeProperty, "FontBase");
        doc.SetResourceReference(FlowDocument.ForegroundProperty, "Text");
        return doc;
    }

    public static List<System.Windows.Documents.Block> Blocks(string markdown, RenderOptions o)
    {
        var ast = Markdig.Markdown.Parse(markdown, Pipeline);
        var ctx = new Ctx(o);
        var blocks = new List<System.Windows.Documents.Block>();
        foreach (var b in ast) { var r = ctx.Block(b); if (r != null) blocks.Add(r); }
        if (o.Glossary.Count > 0) Glossary.Apply(blocks, o.Glossary);
        return blocks;
    }

    /// <summary>One line of Markdown as inlines (choices, short labels). A single paragraph is unwrapped.</summary>
    public static void InlinesInto(InlineCollection target, string markdown)
    {
        var ast = Markdig.Markdown.Parse(markdown, Pipeline);
        var ctx = new Ctx(new RenderOptions());
        var first = true;
        foreach (var b in ast)
        {
            if (b is not LeafBlock { Inline: { } inl }) continue;
            if (!first) target.Add(new LineBreak());
            first = false;
            foreach (var i in ctx.Inlines(inl)) target.Add(i);
        }
    }

    private sealed class Ctx(RenderOptions o)
    {
        private int section;

        public System.Windows.Documents.Block? Block(MdBlock b)
        {
            switch (b)
            {
                case HeadingBlock h:
                {
                    if (o.Paper && h.Level == 1) return null;
                    var p = new Paragraph { Margin = new Thickness(0, h.Level <= 2 ? 26 : 18, 0, 8), FontWeight = FontWeights.SemiBold };
                    p.ResC(TextElement.FontFamilyProperty, "UiFont");
                    p.ResC(TextElement.FontSizeProperty, h.Level switch { 1 => "FontH1", 2 => "FontH2", _ => "FontH3" });
                    if (o.Paper && h.Level == 2)
                        p.Inlines.Add(new Run($"{++section}. ").ResC(TextElement.ForegroundProperty, "TextFaint"));
                    if (h.Inline != null) p.Inlines.AddRange(Inlines(h.Inline));
                    return p;
                }
                case ParagraphBlock pb:
                {
                    var p = new Paragraph { Margin = new Thickness(0, 0, 0, 12), LineHeight = double.NaN };
                    if (pb.Inline != null) p.Inlines.AddRange(Inlines(pb.Inline));
                    return p;
                }
                case ListBlock lb:
                {
                    var list = new List { MarkerStyle = lb.IsOrdered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc, Margin = new Thickness(0, 0, 0, 12), Padding = new Thickness(24, 0, 0, 0) };
                    if (lb.IsOrdered && int.TryParse(lb.OrderedStart, out var start)) list.StartIndex = start;
                    foreach (var item in lb.OfType<ListItemBlock>())
                    {
                        var li = new ListItem();
                        foreach (var child in item) if (Block(child) is { } cb) { if (cb is Paragraph cp) cp.Margin = new Thickness(0, 0, 0, 4); li.Blocks.Add(cb); }
                        list.ListItems.Add(li);
                    }
                    return list;
                }
                case QuoteBlock qb:
                {
                    var s = new Section { BorderThickness = new Thickness(3, 0, 0, 0), Padding = new Thickness(14, 2, 0, 2), Margin = new Thickness(0, 4, 0, 12) };
                    s.ResC(Section.BorderBrushProperty, "Accent");
                    s.ResC(TextElement.ForegroundProperty, "TextMuted");
                    foreach (var child in qb) if (Block(child) is { } cb) s.Blocks.Add(cb);
                    return s;
                }
                case MathBlock mb:
                    return new BlockUIContainer(Math(mb.Lines.ToString(), display: true)) { Margin = new Thickness(0, 4, 0, 14) };
                case FencedCodeBlock fc when (fc.Info ?? "").Trim().Equals("svg", StringComparison.OrdinalIgnoreCase):
                    return Svg(fc.Lines.ToString());
                case CodeBlock cb:
                    return Code(cb.Lines.ToString(), (cb as FencedCodeBlock)?.Info?.Trim().Split(' ')[0] ?? "");
                case MdTable t:
                    return Table(t);
                case HtmlBlock hb:
                {
                    var html = hb.Lines.ToString().Trim();
                    var m = Parser.Placeholder.Match(html);
                    if (m.Success)
                        return o.Slot?.Invoke(m.Groups[1].Value) is { } slot ? new BlockUIContainer(slot) { Margin = new Thickness(0, 8, 0, 16) } : null;
                    return new Paragraph(new Run(StripTags(html))) { Margin = new Thickness(0, 0, 0, 12) };
                }
                case ThematicBreakBlock:
                {
                    var line = new Border { Height = 1, Margin = new Thickness(0, 8, 0, 8) };
                    line.Res(Border.BackgroundProperty, "Border");
                    return new BlockUIContainer(line);
                }
                case LinkReferenceDefinitionGroup:
                    return null;
                default:
                    return b is LeafBlock { Inline: { } li2 } ? new Paragraph(WithChildren(new Span(), Inlines(li2))) : null;
            }
        }

        public List<System.Windows.Documents.Inline> Inlines(ContainerInline container)
        {
            var list = new List<System.Windows.Documents.Inline>();
            foreach (var i in container) if (Inline(i) is { } r) list.Add(r);
            return list;
        }

        private System.Windows.Documents.Inline? Inline(MdInline i)
        {
            switch (i)
            {
                case LiteralInline lit:
                    return new Run(lit.Content.ToString());
                case EmphasisInline em:
                {
                    Span s = em.DelimiterChar == '~' ? new Span() : em.DelimiterCount >= 2 ? new Bold() : new Italic();
                    if (em.DelimiterChar == '~') s.TextDecorations = TextDecorations.Strikethrough;
                    s.Inlines.AddRange(Inlines(em));
                    return s;
                }
                case CodeInline code:
                {
                    var r = new Run(code.Content) { Tag = CodeTag };
                    r.ResC(TextElement.FontFamilyProperty, "MonoFont").ResC(TextElement.BackgroundProperty, "CodeBg");
                    r.FontSize = 0.92 * (Application.Current.TryFindResource("FontBase") as double? ?? 15);
                    return r;
                }
                case MathInline mi:
                    return new InlineUIContainer(Math(mi.Content.ToString(), display: false)) { BaselineAlignment = BaselineAlignment.Center };
                case LinkInline link when link.IsImage:
                    return new Run(string.Concat(link.Descendants<LiteralInline>().Select(l => l.Content.ToString())));
                case LinkInline link:
                    return Link(link.Url ?? "", Inlines(link));
                case AutolinkInline al:
                    return Link(al.Url, [new Run(al.Url)]);
                case LineBreakInline lb:
                    return lb.IsHard ? new LineBreak() : new Run(" ");
                case HtmlEntityInline ent:
                    return new Run(ent.Transcoded.ToString());
                case HtmlInline:
                    return null;
                case ContainerInline ci:
                    return WithChildren(new Span(), Inlines(ci));
                default:
                    return null;
            }
        }

        private static Span WithChildren(Span s, List<System.Windows.Documents.Inline> children) { s.Inlines.AddRange(children); return s; }

        private static System.Windows.Documents.Inline Link(string url, List<System.Windows.Documents.Inline> content)
        {
            // Only web links open (in the default browser). Anything else is shown as plain text.
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
                return WithChildren(new Span(), content);
            var h = new Hyperlink { NavigateUri = uri, ToolTip = uri.ToString() };
            h.ResC(TextElement.ForegroundProperty, "Accent");
            h.Inlines.AddRange(content);
            h.RequestNavigate += (_, e) => { OpenInBrowser(e.Uri); e.Handled = true; };
            return h;
        }

        private static System.Windows.Documents.Block Code(string code, string lang)
        {
            var p = new Paragraph { Tag = CodeTag, Padding = new Thickness(14, 12, 14, 12), Margin = new Thickness(0, 4, 0, 14), BorderThickness = new Thickness(1), LineHeight = double.NaN };
            p.ResC(TextElement.FontFamilyProperty, "MonoFont").ResC(Paragraph.BackgroundProperty, "CodeBg").ResC(Paragraph.BorderBrushProperty, "Border");
            p.FontSize = 0.88 * (Application.Current.TryFindResource("FontBase") as double? ?? 15);
            p.Inlines.AddRange(Highlighter.Runs(code.TrimEnd('\n'), lang));
            foreach (var r in p.Inlines.OfType<Run>()) r.Tag = CodeTag;
            return p;
        }

        private System.Windows.Documents.Block Table(MdTable t)
        {
            var table = new WpfTable { CellSpacing = 0, Margin = new Thickness(0, 4, 0, 16), BorderThickness = new Thickness(1, 1, 0, 0) };
            table.ResC(WpfTable.BorderBrushProperty, "Border");
            table.ResC(TextElement.FontFamilyProperty, "UiFont");
            var cols = t.OfType<MdRow>().Select(r => r.Count).DefaultIfEmpty(0).Max();
            for (var c = 0; c < cols; c++) table.Columns.Add(new TableColumn { Width = GridLength.Auto });
            var group = new TableRowGroup();
            foreach (var row in t.OfType<MdRow>())
            {
                var tr = new WpfRow();
                if (row.IsHeader) { tr.ResC(WpfRow.BackgroundProperty, "Surface"); tr.FontWeight = FontWeights.SemiBold; }
                foreach (var cell in row.OfType<MdCell>())
                {
                    var tc = new WpfCell { Padding = new Thickness(10, 6, 10, 6), BorderThickness = new Thickness(0, 0, 1, 1) };
                    tc.ResC(WpfCell.BorderBrushProperty, "Border");
                    foreach (var child in cell) if (Block(child) is { } cb) { if (cb is Paragraph cp) cp.Margin = new Thickness(0); tc.Blocks.Add(cb); }
                    tr.Cells.Add(tc);
                }
                group.Rows.Add(tr);
            }
            table.RowGroups.Add(group);
            return table;
        }

        private static System.Windows.Documents.Block Svg(string svg)
        {
            try
            {
                var settings = new SharpVectors.Renderers.Wpf.WpfDrawingSettings { IncludeRuntime = true, TextAsGeometry = false };
                using var reader = new SharpVectors.Converters.FileSvgReader(settings);
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(svg));
                var drawing = reader.Read(stream);
                var img = new Image { Source = new DrawingImage(drawing), Stretch = Stretch.Uniform, MaxHeight = 360, StretchDirection = StretchDirection.Both };
                var frame = new Border { Child = img, Padding = new Thickness(12), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8) };
                frame.Res(Border.BackgroundProperty, "Surface").Res(Border.BorderBrushProperty, "Border");
                return new BlockUIContainer(frame) { Margin = new Thickness(0, 8, 0, 16) };
            }
            catch (Exception e)
            {
                return new Paragraph(new Run($"[This diagram could not be drawn: {e.Message}]")).ResC(TextElement.ForegroundProperty, "TextFaint");
            }
        }
    }

    /// <summary>LaTeX via WpfMath. If the formula can't be parsed, the source is shown in monospace.</summary>
    public static FrameworkElement Math(string latex, bool display)
    {
        var size = (Application.Current.TryFindResource("FontBase") as double? ?? 15) * (display ? 1.25 : 1.05);
        try
        {
            var f = new WpfMath.Controls.FormulaControl { Formula = latex.Trim(), Scale = size, SnapsToDevicePixels = true };
            f.SetResourceReference(Control.ForegroundProperty, "Text");
            if (f.HasError) throw new FormatException();
            if (display) f.HorizontalAlignment = HorizontalAlignment.Center;
            return f;
        }
        catch (Exception)
        {
            var t = new TextBlock { Text = latex };
            return t.Res(TextBlock.FontFamilyProperty, "MonoFont");
        }
    }

    public static void OpenInBrowser(Uri uri)
    {
        if (uri.Scheme is "http" or "https") Process.Start(new ProcessStartInfo(uri.ToString()) { UseShellExecute = true });
    }

    public static string StripTags(string html) => TagRe().Replace(html, "");

    /// <summary>Plain text of a document range (for copying a code block, a question, …).</summary>
    public static string PlainText(TextElement e) => new TextRange(e.ContentStart, e.ContentEnd).Text;

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex TagRe();
}

/// <summary>Underlines glossary terms (dotted) with a tooltip definition. Skips code, links and headings.</summary>
public static partial class Glossary
{
    public const string TermTag = "term";

    public static void Apply(IEnumerable<System.Windows.Documents.Block> blocks, IReadOnlyList<GlossaryEntry> glossary)
    {
        if (glossary.Count == 0) return;
        var defs = glossary.GroupBy(g => g.Term.ToLowerInvariant()).ToDictionary(g => g.Key, g => g.First().Definition);
        var pattern = string.Join('|', defs.Keys.OrderByDescending(k => k.Length).Select(Regex.Escape));
        var re = new Regex($@"\b({pattern})(?:s|es)?\b", RegexOptions.IgnoreCase);
        foreach (var b in blocks) Walk(b, re, defs);
    }

    // Snapshots (ToList) everywhere: all collections in a FlowDocument share one text tree, so changing any
    // paragraph's text invalidates every live enumerator in the document.
    private static void Walk(System.Windows.Documents.Block b, Regex re, Dictionary<string, string> defs)
    {
        switch (b)
        {
            case Paragraph p when (p.Tag as string) != Markdown.CodeTag && p.FontWeight != FontWeights.SemiBold:
                WalkInlines(p.Inlines, re, defs);
                break;
            case List l:
                foreach (var cb in l.ListItems.ToList().SelectMany(li => li.Blocks.ToList())) Walk(cb, re, defs);
                break;
            case Section s:
                foreach (var cb in s.Blocks.ToList()) Walk(cb, re, defs);
                break;
            case System.Windows.Documents.Table t:
                foreach (var cb in t.RowGroups.SelectMany(g => g.Rows).SelectMany(r => r.Cells).SelectMany(c => c.Blocks).ToList()) Walk(cb, re, defs);
                break;
        }
    }

    private static void WalkInlines(InlineCollection inlines, Regex re, Dictionary<string, string> defs)
    {
        foreach (var inline in inlines.ToList())
        {
            if (inline is Hyperlink) continue;
            if (inline is Span s) { WalkInlines(s.Inlines, re, defs); continue; }
            if (inline is not Run run || (run.Tag as string) is Markdown.CodeTag or TermTag) continue;
            var text = run.Text;
            var matches = re.Matches(text);
            if (matches.Count == 0) continue;
            var last = 0;
            foreach (Match m in matches)
            {
                if (m.Index > last) inlines.InsertBefore(run, new Run(text[last..m.Index]));
                var term = new Run(m.Value) { Tag = TermTag, ToolTip = defs[m.Groups[1].Value.ToLowerInvariant()] };
                var dots = new TextDecoration { Location = TextDecorationLocation.Underline, PenOffset = 2 };
                dots.Pen = new Pen(Application.Current.TryFindResource("TextMuted") as Brush ?? Brushes.Gray, 1) { DashStyle = DashStyles.Dot };
                term.TextDecorations = [dots];
                term.Cursor = System.Windows.Input.Cursors.Help;
                ToolTipService.SetInitialShowDelay(term, 250);
                inlines.InsertBefore(run, term);
                last = m.Index + m.Length;
            }
            if (last < text.Length) inlines.InsertBefore(run, new Run(text[last..]));
            inlines.Remove(run);
        }
    }

    public static string? DefinitionOf(Run r) => (r.Tag as string) == TermTag ? r.ToolTip as string : null;
}
