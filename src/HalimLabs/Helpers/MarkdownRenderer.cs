using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace HalimLabs.Helpers;

public static class MarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    private static readonly Brush CodeBackground = Freeze("#1E1E22");
    private static readonly Brush CodeHeaderBackground = Freeze("#25252A");
    private static readonly Brush InlineCodeBackground = Freeze("#3A3A40");
    private static readonly Brush ForegroundBrush = Freeze("#E8E8EC");
    private static readonly Brush MutedBrush = Freeze("#A0A0A8");
    private static readonly Brush AccentBrush = Freeze("#6CB6FF");

    public static FlowDocument ToFlowDocument(string? markdown)
    {
        var document = CreateEmptyDocument();

        if (string.IsNullOrWhiteSpace(markdown))
        {
            document.Blocks.Add(new Paragraph(new Run(string.Empty)));
            return document;
        }

        try
        {
            var parsed = Markdown.Parse(markdown, Pipeline);
            foreach (var block in parsed)
                AddBlock(document.Blocks, block);

            if (document.Blocks.Count == 0)
                document.Blocks.Add(new Paragraph(new Run(markdown)));
        }
        catch
        {
            document.Blocks.Clear();
            document.Blocks.Add(new Paragraph(new Run(markdown))
            {
                FontFamily = new FontFamily("Cascadia Code, Consolas, Courier New"),
                Foreground = ForegroundBrush
            });
        }

        return document;
    }

    public static FlowDocument ToPlainDocument(string? text)
    {
        var document = CreateEmptyDocument();
        document.Blocks.Add(new Paragraph(new Run(text ?? string.Empty))
        {
            Margin = new Thickness(0)
        });
        return document;
    }

    private static FlowDocument CreateEmptyDocument() => new()
    {
        PagePadding = new Thickness(0),
        FontFamily = new FontFamily("Segoe UI"),
        FontSize = 14,
        Foreground = ForegroundBrush,
        TextAlignment = TextAlignment.Left
    };

    private static void AddBlock(BlockCollection blocks, Markdig.Syntax.Block block)
    {
        switch (block)
        {
            case HeadingBlock heading:
                blocks.Add(CreateHeading(heading));
                break;
            case ParagraphBlock paragraph:
                blocks.Add(CreateParagraph(paragraph));
                break;
            case FencedCodeBlock code:
                blocks.Add(CreateCodeBlock(code));
                break;
            case CodeBlock plainCode:
                blocks.Add(CreatePlainCodeBlock(plainCode));
                break;
            case QuoteBlock quote:
                blocks.Add(CreateQuote(quote));
                break;
            case ListBlock list:
                blocks.Add(CreateList(list));
                break;
            case ThematicBreakBlock:
                blocks.Add(new BlockUIContainer(new Border
                {
                    Height = 1,
                    Background = Freeze("#3A3A40"),
                    Margin = new Thickness(0, 10, 0, 10)
                }));
                break;
        }
    }

    private static Paragraph CreateHeading(HeadingBlock heading)
    {
        var paragraph = new Paragraph
        {
            Margin = new Thickness(0, heading.Level == 1 ? 12 : 8, 0, 6),
            FontWeight = FontWeights.SemiBold,
            FontSize = heading.Level switch
            {
                1 => 20,
                2 => 18,
                3 => 16,
                _ => 14
            }
        };
        AppendInlines(paragraph.Inlines, heading.Inline);
        return paragraph;
    }

    private static Paragraph CreateParagraph(ParagraphBlock paragraphBlock)
    {
        var paragraph = new Paragraph { Margin = new Thickness(0, 0, 0, 8) };
        AppendInlines(paragraph.Inlines, paragraphBlock.Inline);
        return paragraph;
    }

    private static BlockUIContainer CreateCodeBlock(FencedCodeBlock code)
    {
        var language = code.Info?.Trim() ?? string.Empty;
        return new BlockUIContainer(BuildCodePanel(language, GetCodeText(code)));
    }

    private static BlockUIContainer CreatePlainCodeBlock(CodeBlock code) =>
        new(BuildCodePanel(string.Empty, GetCodeText(code)));

    private static Border BuildCodePanel(string language, string code)
    {
        var root = new Border
        {
            Background = CodeBackground,
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(0, 4, 0, 10),
            BorderBrush = Freeze("#3A3A40"),
            BorderThickness = new Thickness(1)
        };

        var stack = new StackPanel();
        var header = new Grid { Background = CodeHeaderBackground };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var langLabel = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(language) ? "code" : language,
            Foreground = MutedBrush,
            FontSize = 11,
            Margin = new Thickness(12, 8, 12, 8),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(langLabel, 0);

        var copyButton = new Button
        {
            Content = "Copy",
            Width = 64,
            Padding = new Thickness(8, 4, 8, 4),
            Margin = new Thickness(0, 4, 8, 4),
            Cursor = System.Windows.Input.Cursors.Hand,
            Background = Freeze("#3A3A40"),
            Foreground = ForegroundBrush,
            BorderThickness = new Thickness(0),
            FontSize = 11,
            Focusable = false,
            IsTabStop = false
        };
        copyButton.Click += (_, e) =>
        {
            e.Handled = true;
            try
            {
                Clipboard.SetText(code);
                copyButton.Content = "Copied";
                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(1.2)
                };
                timer.Tick += (_, _) =>
                {
                    copyButton.Content = "Copy";
                    timer.Stop();
                };
                timer.Start();
            }
            catch
            {
                // ignore clipboard failures
            }
        };
        Grid.SetColumn(copyButton, 1);
        header.Children.Add(langLabel);
        header.Children.Add(copyButton);

        var codeBox = new TextBlock
        {
            FontFamily = new FontFamily("Cascadia Code, Consolas, Courier New"),
            FontSize = 12.5,
            Margin = new Thickness(12, 8, 12, 12),
            TextWrapping = TextWrapping.Wrap
        };

        foreach (var run in SimpleSyntaxHighlighter.Highlight(code, language))
            codeBox.Inlines.Add(run);

        stack.Children.Add(header);
        stack.Children.Add(new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = codeBox
        });
        root.Child = stack;
        return root;
    }

    private static Section CreateQuote(QuoteBlock quote)
    {
        var section = new Section
        {
            BorderBrush = AccentBrush,
            BorderThickness = new Thickness(3, 0, 0, 0),
            Padding = new Thickness(10, 0, 0, 0),
            Margin = new Thickness(0, 4, 0, 8)
        };

        foreach (var child in quote)
            AddBlock(section.Blocks, child);

        return section;
    }

    private static List CreateList(ListBlock listBlock)
    {
        var list = new List
        {
            MarkerStyle = listBlock.IsOrdered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc,
            Margin = new Thickness(0, 0, 0, 8)
        };

        foreach (var item in listBlock)
        {
            if (item is not ListItemBlock listItemBlock)
                continue;

            var listItem = new ListItem();
            foreach (var child in listItemBlock)
                AddBlock(listItem.Blocks, child);
            list.ListItems.Add(listItem);
        }

        return list;
    }

    private static void AppendInlines(InlineCollection inlines, ContainerInline? container)
    {
        if (container is null)
            return;

        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    inlines.Add(new Run(literal.Content.ToString()));
                    break;
                case CodeInline code:
                    inlines.Add(new Run(code.Content ?? string.Empty)
                    {
                        FontFamily = new FontFamily("Cascadia Code, Consolas, Courier New"),
                        Background = InlineCodeBackground,
                        Foreground = Freeze("#E0E0E6")
                    });
                    break;
                case EmphasisInline emphasis:
                    var span = new Span
                    {
                        FontWeight = emphasis.DelimiterCount >= 2 ? FontWeights.Bold : FontWeights.Normal,
                        FontStyle = emphasis.DelimiterCount == 1 ? FontStyles.Italic : FontStyles.Normal
                    };
                    AppendInlines(span.Inlines, emphasis);
                    inlines.Add(span);
                    break;
                case LinkInline link:
                {
                    var label = link.FirstChild is LiteralInline lit
                        ? lit.Content.ToString()
                        : (link.Url ?? "link");
                    var hyper = new Hyperlink(new Run(label))
                    {
                        NavigateUri = Uri.TryCreate(link.Url, UriKind.Absolute, out var uri) ? uri : null,
                        Foreground = AccentBrush
                    };
                    hyper.RequestNavigate += (_, e) =>
                    {
                        try
                        {
                            if (e.Uri is null)
                                return;
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = e.Uri.AbsoluteUri,
                                UseShellExecute = true
                            });
                            e.Handled = true;
                        }
                        catch
                        {
                            // ignore
                        }
                    };
                    inlines.Add(hyper);
                    break;
                }
                case LineBreakInline:
                    inlines.Add(new LineBreak());
                    break;
                case ContainerInline nested:
                    AppendInlines(inlines, nested);
                    break;
            }
        }
    }

    private static string GetCodeText(LeafBlock block)
    {
        try
        {
            var lines = block.Lines.Lines;
            if (lines is null || lines.Length == 0)
                return string.Empty;

            var sb = new StringBuilder(Math.Min(lines.Length * 64, 256_000));
            var count = 0;
            foreach (var line in lines)
            {
                if (line.Slice.Text is null)
                    continue;
                sb.Append(line.Slice.ToString());
                sb.Append('\n');
                count++;
                // Guard against pathological blocks freezing the UI.
                if (count > 4000)
                {
                    sb.Append("\n…");
                    break;
                }
            }

            return sb.ToString().TrimEnd('\n');
        }
        catch
        {
            return string.Empty;
        }
    }

    private static SolidColorBrush Freeze(string hex)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFrom(hex)!;
        brush.Freeze();
        return brush;
    }
}

public static class SimpleSyntaxHighlighter
{
    private static readonly Regex KeywordRegex = new(
        @"\b(abstract|as|async|await|base|break|case|catch|class|const|continue|default|delegate|do|else|enum|event|explicit|extern|false|finally|fixed|for|foreach|goto|if|implicit|in|interface|internal|is|lock|namespace|new|null|operator|out|override|params|private|protected|public|readonly|ref|return|sealed|sizeof|stackalloc|static|struct|switch|this|throw|true|try|typeof|unchecked|unsafe|using|virtual|void|volatile|while|var|let|function|import|from|export|package|type|extends|implements|final|def|fn|match|with|then|elif|lambda|yield|select|where|join|into|orderby|group|by|on|equals|and|or|not)\b",
        RegexOptions.Compiled);

    private static readonly Regex StringRegex = new(
        @"(""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\])*'|`(?:\\.|[^`\\])*`)",
        RegexOptions.Compiled);

    private static readonly Regex CommentRegex = new(
        @"(//.*?$|/\*[\s\S]*?\*/|#.*?$)",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex TypeRegex = new(
        @"\b([A-Z][A-Za-z0-9_]+)\b",
        RegexOptions.Compiled);

    private static readonly Brush DefaultBrush = Freeze("#D4D4D4");
    private static readonly Brush KeywordBrush = Freeze("#C586C0");
    private static readonly Brush StringBrush = Freeze("#CE9178");
    private static readonly Brush CommentBrush = Freeze("#6A9955");
    private static readonly Brush TypeBrush = Freeze("#4EC9B0");

    public static IEnumerable<Run> Highlight(string? code, string? language)
    {
        code ??= string.Empty;
        if (code.Length == 0)
        {
            yield return new Run(string.Empty) { Foreground = DefaultBrush };
            yield break;
        }

        // Large code: skip heavy regex highlighting to keep UI responsive.
        if (code.Length > 12000)
        {
            yield return new Run(code) { Foreground = DefaultBrush };
            yield break;
        }

        var tokens = new List<(int Start, int Length, Brush Brush)>();
        AddMatches(tokens, CommentRegex, code, CommentBrush);
        AddMatches(tokens, StringRegex, code, StringBrush);
        AddMatches(tokens, KeywordRegex, code, KeywordBrush);
        AddMatches(tokens, TypeRegex, code, TypeBrush);

        tokens = tokens
            .OrderBy(t => t.Start)
            .ThenByDescending(t => t.Length)
            .ToList();

        var occupied = new bool[code.Length];
        var accepted = new List<(int Start, int Length, Brush Brush)>();
        foreach (var token in tokens)
        {
            if (token.Start < 0 || token.Length <= 0 || token.Start >= code.Length)
                continue;

            var end = Math.Min(token.Start + token.Length, code.Length);
            var overlaps = false;
            for (var i = token.Start; i < end; i++)
            {
                if (occupied[i])
                {
                    overlaps = true;
                    break;
                }
            }

            if (overlaps)
                continue;

            for (var i = token.Start; i < end; i++)
                occupied[i] = true;
            accepted.Add((token.Start, end - token.Start, token.Brush));
        }

        var index = 0;
        foreach (var token in accepted.OrderBy(t => t.Start))
        {
            if (token.Start > index)
                yield return new Run(code[index..token.Start]) { Foreground = DefaultBrush };

            yield return new Run(code.Substring(token.Start, token.Length)) { Foreground = token.Brush };
            index = token.Start + token.Length;
        }

        if (index < code.Length)
            yield return new Run(code[index..]) { Foreground = DefaultBrush };
    }

    private static void AddMatches(List<(int Start, int Length, Brush Brush)> tokens, Regex regex, string code, Brush brush)
    {
        foreach (Match match in regex.Matches(code))
        {
            if (match.Success)
                tokens.Add((match.Index, match.Length, brush));
        }
    }

    private static SolidColorBrush Freeze(string hex)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFrom(hex)!;
        brush.Freeze();
        return brush;
    }
}
