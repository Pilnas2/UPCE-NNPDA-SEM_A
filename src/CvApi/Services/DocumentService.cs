using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace CvApi;

public sealed class DocumentService
{
    public const int MaxBytes = 10 * 1024 * 1024; //10MB
    private const int MaxText = 500000;
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    public async Task<ExtractResult> Read(IFormFile file, CancellationToken ct)
    {
        if (file.Length == 0) throw new ApiException(400, "Soubor je prázdný.");
        if (file.Length > MaxBytes) throw new ApiException(413, "Maximální velikost souboru je 10 MB.");
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not (".pdf" or ".docx")) throw new ApiException(415, "Podporovány jsou PDF a DOCX.");
        using var stream = new MemoryStream();
        await file.CopyToAsync(stream, ct);
        var bytes = stream.ToArray();
        try
        {
            ct.ThrowIfCancellationRequested();
            return extension switch { ".pdf" => ReadPdf(bytes, ct), _ => ReadDocx(bytes, ct) };
        }
        catch (ApiException) { throw; }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        { throw new ApiException(422, "Dokument nelze přečíst."); }
    }

    private static ExtractResult ReadPdf(byte[] bytes, CancellationToken ct)
    {
        using var pdf = PdfDocument.Open(bytes);
        if (pdf.NumberOfPages > 10) throw new ApiException(413, "Dokument smí mít nejvýše 10 stran.");
        var all = new StringBuilder();
        var safe = new StringBuilder();
        var issues = new List<Issue>();
        foreach (var page in pdf.GetPages())
        {
            ct.ThrowIfCancellationRequested();
            all.AppendLine(ContentOrderTextExtractor.GetText(page));
            var segments = new List<(string Text, string[] Reasons, double Size)>();
            foreach (var letter in page.Letters)
            {
                var reasons = new List<string>();
                if ((int)letter.RenderingMode is 3 or 7) reasons.Add("HIDDEN_TEXT");
                if (letter.PointSize < 3) reasons.Add("TINY_FONT");
                var color = letter.Color.ToRGBValues();
                if (color.r >= .98 && color.g >= .98 && color.b >= .98) reasons.Add("WHITE_TEXT");
                var box = letter.BoundingBox;
                var crop = page.CropBox.Bounds;
                if (box.Right < crop.Left || box.Left > crop.Right || box.Top < crop.Bottom || box.Bottom > crop.Top)
                    reasons.Add("OFF_PAGE_TEXT");
                if (segments.Count > 0 && segments[^1].Reasons.SequenceEqual(reasons) && Math.Abs(segments[^1].Size - letter.PointSize) < .01)
                    segments[^1] = (segments[^1].Text + letter.Value, segments[^1].Reasons, letter.PointSize);
                else segments.Add((letter.Value, reasons.ToArray(), letter.PointSize));
            }
            var pageHasIssues = false;
            foreach (var segment in segments)
            {
                foreach (var reason in segment.Reasons)
                {
                    issues.Add(new(reason, segment.Text, reason == "TINY_FONT" ? segment.Size : null, $"page:{page.Number}"));
                    pageHasIssues = true;
                }
            }
            safe.AppendLine(pageHasIssues ? string.Join(" ", segments.Where(s => s.Reasons.Length == 0).Select(s => s.Text)) : ContentOrderTextExtractor.GetText(page));
            Limit(all);
        }
        return Finish(all, safe, issues);
    }

    private static ExtractResult ReadDocx(byte[] bytes, CancellationToken ct)
    {
        using var input = new MemoryStream(bytes);
        using var zip = new ZipArchive(input, ZipArchiveMode.Read);
        if (zip.Entries.Count > 2000 || zip.Entries.Sum(x => x.Length) > 50 * 1024 * 1024)
            throw new ApiException(413, "Dokument je příliš velký.");
        if (zip.GetEntry("word/document.xml") is null) throw new ApiException(422, "Neplatný DOCX dokument.");
        XDocument? styles = zip.GetEntry("word/styles.xml") is { } s ? ReadXml(s) : null;
        var styleMap = styles?.Descendants(W + "style").Where(x => x.Attribute(W + "styleId") != null)
            .ToDictionary(x => (string)x.Attribute(W + "styleId")!, x => x) ?? [];
        var defaults = styles?.Descendants(W + "rPrDefault").FirstOrDefault()?.Element(W + "rPr");
        var defaultParagraphStyle = styleMap.Values.FirstOrDefault(x => (string?)x.Attribute(W + "type") == "paragraph" && (string?)x.Attribute(W + "default") == "1");
        var all = new StringBuilder();
        var safe = new StringBuilder();
        var issues = new List<Issue>();
        foreach (var entry in zip.Entries.Where(x => x.FullName == "word/document.xml" ||
                     x.FullName.StartsWith("word/header") && x.FullName.EndsWith(".xml") ||
                     x.FullName.StartsWith("word/footer") && x.FullName.EndsWith(".xml") ||
                     x.FullName is "word/footnotes.xml" or "word/endnotes.xml"))
        {
            var xml = ReadXml(entry);
            var index = 0;
            foreach (var paragraph in xml.Descendants(W + "p"))
            {
                foreach (var run in paragraph.Descendants(W + "r").Where(r => r.Ancestors(W + "p").First() == paragraph))
                {
                    ct.ThrowIfCancellationRequested();
                    index++;
                    var text = string.Concat(run.Descendants().Where(x => x.Name == W + "t" || x.Name == W + "tab" || x.Name == W + "br")
                        .Select(x => x.Name == W + "tab" ? "\t" : x.Name == W + "br" ? "\n" : x.Value));
                    if (string.IsNullOrWhiteSpace(text)) continue;
                    var props = new Dictionary<string, string>();
                    Apply(defaults, props, false);
                    var pstyle = (string?)paragraph.Element(W + "pPr")?.Element(W + "pStyle")?.Attribute(W + "val");
                    ApplyStyle(pstyle ?? (string?)defaultParagraphStyle?.Attribute(W + "styleId"), styleMap, props, []);
                    var rpr = run.Element(W + "rPr");
                    ApplyStyle((string?)rpr?.Element(W + "rStyle")?.Attribute(W + "val"), styleMap, props, []);
                    Apply(rpr, props, false);
                    var reasons = new List<string>();
                    if (IsOn(props.GetValueOrDefault("vanish", "0")) || IsOn(props.GetValueOrDefault("webHidden", "0"))) reasons.Add("HIDDEN_TEXT");
                    var size = double.TryParse(props.GetValueOrDefault("sz"), CultureInfo.InvariantCulture, out var halfPoints) ? halfPoints / 2 : 11;
                    if (size < 3) reasons.Add("TINY_FONT");
                    if (props.GetValueOrDefault("color")?.Equals("FFFFFF", StringComparison.OrdinalIgnoreCase) == true) reasons.Add("WHITE_TEXT");
                    AddRun(text, size, reasons, $"{entry.FullName}:run:{index}", all, safe, issues);
                }
                all.AppendLine(); safe.AppendLine();
            }
        }
        return Finish(all, safe, issues);
    }
    private static XDocument ReadXml(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 50 * 1024 * 1024 });
        return XDocument.Load(reader);
    }
    private static bool IsOn(string value) => value is not ("false" or "0" or "off");
    private static void Apply(XElement? rpr, Dictionary<string, string> props, bool style)
    {
        if (rpr == null) return;
        foreach (var p in rpr.Elements())
        {
            var value = (string?)p.Attribute(W + "val") ?? "1";
            // OOXML toggle properties invert the inherited value in styles; direct formatting sets it.
            if (style && p.Name.LocalName is "vanish" or "webHidden")
            {
                if (IsOn(value)) props[p.Name.LocalName] = IsOn(props.GetValueOrDefault(p.Name.LocalName, "0")) ? "0" : "1";
            }
            else props[p.Name.LocalName] = value;
        }
    }
    private static void ApplyStyle(string? id, Dictionary<string, XElement> styles, Dictionary<string, string> props, HashSet<string> visited)
    {
        if (id is null || !visited.Add(id) || !styles.TryGetValue(id, out var style)) return;
        ApplyStyle((string?)style.Element(W + "basedOn")?.Attribute(W + "val"), styles, props, visited);
        Apply(style.Element(W + "rPr"), props, true);
    }
    private static void AddRun(string text, double size, List<string> reasons, string location, StringBuilder all, StringBuilder safe, List<Issue> issues)
    {
        all.Append(text);
        if (reasons.Count == 0) safe.Append(text); else safe.Append(' ');
        foreach (var reason in reasons) issues.Add(new(reason, text, reason == "TINY_FONT" ? size : null, location));
        Limit(all);
    }
    private static void Limit(StringBuilder text)
    {
        if (text.Length > MaxText) throw new ApiException(413, "Dokument obsahuje příliš mnoho textu.");
    }
    private static ExtractResult Finish(StringBuilder all, StringBuilder safe, List<Issue> issues)
    {
        var text = all.ToString().Trim();
        if (text.Length == 0) throw new ApiException(422, "Dokument neobsahuje čitelnou textovou vrstvu. OCR není součástí API.");
        foreach (var line in text.Split(['\r', '\n']).Where(x => x.Length > 0 && InputCheck.IsInstruction(x)))
            issues.Add(new("PROMPT_INJECTION", line));
        return new(text, InputCheck.Clean(safe.ToString()).Trim(), new(issues.Count > 0, issues));
    }
}
