using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace PmTracker.Web.Services.Export;

/// <summary>
/// Statický helper pro vytváření OpenXML elementů (paragraphs, runs, cells, spacers).
/// Sdílený mezi všemi partial soubory <see cref="OpenXmlWordExportService"/>.
/// </summary>
public static class OpenXmlWordElements
{
    internal const int BaseFontHalfPoints = 20;
    internal const int RecordTitleHalfPoints = 24;

    /// <summary>Segment textu s volitelným formátováním pro <see cref="CreateRichParagraph"/>.</summary>
    internal readonly record struct TextSegment(
        string Text,
        bool Bold = false,
        bool Italic = false,
        bool Underline = false,
        bool Strike = false,
        bool PreserveLineBreaks = false);

    internal static TableCell CreateCell(string text, bool bold = false, string? fillColor = null)
    {
        var properties = new TableCellProperties();
        if (!string.IsNullOrWhiteSpace(fillColor))
        {
            properties.Append(new Shading { Val = ShadingPatternValues.Clear, Fill = fillColor, Color = "auto" });
        }

        var cell = new TableCell(properties);
        cell.Append(CreateParagraph(text, bold: bold, sizeHalfPoints: BaseFontHalfPoints, before: 30, after: 30));
        return cell;
    }

    internal static Paragraph CreateParagraph(
        string text,
        bool bold = false,
        bool italic = false,
        bool strike = false,
        int sizeHalfPoints = BaseFontHalfPoints,
        JustificationValues? justification = null,
        string? shadingHex = null,
        string? colorHex = null,
        int before = 0,
        int after = 0,
        bool preserveLineBreaks = false)
    {
        var paragraphProperties = new ParagraphProperties(
            new SpacingBetweenLines
            {
                Before = before == 0 ? null : before.ToString(CultureInfo.InvariantCulture),
                After = after == 0 ? null : after.ToString(CultureInfo.InvariantCulture)
            });

        if (justification.HasValue)
        {
            paragraphProperties.Append(new Justification { Val = justification.Value });
        }

        if (!string.IsNullOrWhiteSpace(shadingHex))
        {
            paragraphProperties.Append(new Shading
            {
                Val = ShadingPatternValues.Clear,
                Fill = shadingHex,
                Color = "auto"
            });
        }

        var paragraph = new Paragraph(paragraphProperties);
        var runProperties = CreateRunProperties(sizeHalfPoints, bold, italic, strike, underline: false, colorHex);
        var normalizedText = (text ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);

        if (!preserveLineBreaks || normalizedText.Length == 0)
        {
            paragraph.Append(new Run(runProperties, new Text(normalizedText) { Space = SpaceProcessingModeValues.Preserve }));
            return paragraph;
        }

        var lines = normalizedText.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var lineRun = new Run(runProperties.CloneNode(true), new Text(lines[i]) { Space = SpaceProcessingModeValues.Preserve });
            paragraph.Append(lineRun);
            if (i < lines.Length - 1)
            {
                paragraph.Append(new Run(runProperties.CloneNode(true), new Break()));
            }
        }

        return paragraph;
    }

    internal static Paragraph CreateRichParagraph(
        IReadOnlyList<TextSegment> segments,
        int sizeHalfPoints = BaseFontHalfPoints,
        JustificationValues? justification = null,
        string? shadingHex = null,
        string? colorHex = null,
        int before = 0,
        int after = 0)
    {
        var paragraphProperties = new ParagraphProperties(
            new SpacingBetweenLines
            {
                Before = before == 0 ? null : before.ToString(CultureInfo.InvariantCulture),
                After = after == 0 ? null : after.ToString(CultureInfo.InvariantCulture)
            });

        if (justification.HasValue)
        {
            paragraphProperties.Append(new Justification { Val = justification.Value });
        }

        if (!string.IsNullOrWhiteSpace(shadingHex))
        {
            paragraphProperties.Append(new Shading
            {
                Val = ShadingPatternValues.Clear,
                Fill = shadingHex,
                Color = "auto"
            });
        }

        var paragraph = new Paragraph(paragraphProperties);
        foreach (var segment in segments)
        {
            var runProperties = CreateRunProperties(sizeHalfPoints, segment.Bold, segment.Italic, segment.Strike, segment.Underline, colorHex);
            AppendSegmentText(
                paragraph,
                runProperties,
                segment.Text ?? string.Empty,
                segment.PreserveLineBreaks);
        }

        return paragraph;
    }

    internal static RunProperties CreateRunProperties(int sizeHalfPoints, bool bold, bool italic, bool strike, bool underline, string? colorHex = null)
    {
        var runProperties = new RunProperties(
            new RunFonts
            {
                Ascii = "Times New Roman",
                HighAnsi = "Times New Roman",
                EastAsia = "Times New Roman",
                ComplexScript = "Times New Roman"
            },
            new FontSize { Val = sizeHalfPoints.ToString(CultureInfo.InvariantCulture) });

        if (bold)
        {
            runProperties.Append(new Bold());
        }

        if (italic)
        {
            runProperties.Append(new Italic());
        }

        if (strike)
        {
            runProperties.Append(new Strike());
        }

        if (underline)
        {
            runProperties.Append(new Underline { Val = UnderlineValues.Single });
        }

        if (!string.IsNullOrWhiteSpace(colorHex))
        {
            runProperties.Append(new Color { Val = colorHex });
        }

        return runProperties;
    }

    internal static void AppendSegmentText(
        Paragraph paragraph,
        RunProperties runProperties,
        string text,
        bool preserveLineBreaks)
    {
        var normalizedText = (text ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);

        if (!preserveLineBreaks || normalizedText.Length == 0)
        {
            paragraph.Append(new Run(runProperties.CloneNode(true), new Text(normalizedText) { Space = SpaceProcessingModeValues.Preserve }));
            return;
        }

        var lines = normalizedText.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            paragraph.Append(new Run(runProperties.CloneNode(true), new Text(lines[i]) { Space = SpaceProcessingModeValues.Preserve }));
            if (i < lines.Length - 1)
            {
                paragraph.Append(new Run(runProperties.CloneNode(true), new Break()));
            }
        }
    }

    internal static Paragraph CreateSpacerParagraph(int after)
    {
        return CreateParagraph(string.Empty, sizeHalfPoints: BaseFontHalfPoints, before: 0, after: after);
    }
}
