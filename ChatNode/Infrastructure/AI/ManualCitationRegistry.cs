using System.Text;
using System.Text.RegularExpressions;
using Domain.Entities;

namespace ChatNode.Infrastructure.AI;

public sealed partial class ManualCitationRegistry
{
    private const int MaxQuoteChars = 320;

    private readonly Dictionary<string, ManualPart> _byLabel = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Guid, string> _labelByPart = [];
    private readonly HashSet<string> _used = new(StringComparer.OrdinalIgnoreCase);

    public string Register(ManualPart part)
    {
        if (_labelByPart.TryGetValue(part.Id, out var existing)) return existing;

        var label = $"М{_byLabel.Count + 1}";
        _byLabel[label] = part;
        _labelByPart[part.Id] = label;

        return label;
    }

    public string StripUnknown(string text)
    {
        return LabelRegex().Replace(text, match =>
        {
            var label = match.Groups[1].Value;
            if (!_byLabel.ContainsKey(label)) return string.Empty;

            _used.Add(label);
            return match.Value;
        });
    }

    public string BuildSources()
    {
        if (_used.Count == 0) return string.Empty;

        var builder = new StringBuilder();
        builder.AppendLine("## Источники в методичке");
        builder.AppendLine();

        foreach (var label in _used.OrderBy(Order))
        {
            var part = _byLabel[label];
            var where = string.IsNullOrWhiteSpace(part.Navigation) ? part.Title : part.Navigation;

            builder.AppendLine($"**[{label}]** {where}");
            builder.AppendLine();
            builder.AppendLine($"> {Quote(part.Content)}");
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private static int Order(string label) =>
        int.TryParse(label.AsSpan(1), out var number) ? number : int.MaxValue;

    private static string Quote(string content)
    {
        var text = content.Replace('\n', ' ').Replace("  ", " ").Trim();
        if (text.Length <= MaxQuoteChars) return text;

        var cut = text[..MaxQuoteChars];
        var lastStop = cut.LastIndexOfAny(['.', ';', '!', '?']);

        return lastStop > MaxQuoteChars / 2 ? cut[..(lastStop + 1)] : cut + "…";
    }

    [GeneratedRegex(@"\[(М\d{1,3})\]")]
    private static partial Regex LabelRegex();
}
