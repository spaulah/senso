using System.Text.RegularExpressions;

namespace Senso.Mcp.Ocr;

/// <summary>
/// BEST-EFFORT pattern masking of email addresses, card-like numbers, US SSN-like numbers and phone-like numbers.
/// It will miss many forms of personal data (names, addresses, non-US formats, OCR errors) and can mask
/// harmless numbers. It is NOT a privacy or compliance guarantee.
/// </summary>
public static class PiiRedactor
{
    public const string Placeholder = "[REDACTED]";

    private static readonly Regex Email = new(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b", RegexOptions.Compiled);
    private static readonly Regex Card = new(@"\b(?:\d{4}[- ]?){3}\d{4}\b", RegexOptions.Compiled);
    private static readonly Regex Ssn = new(@"\b(?!000|666|9\d{2})\d{3}-(?!00)\d{2}-(?!0{4})\d{4}\b", RegexOptions.Compiled);
    private static readonly Regex Phone = new(@"(?<!\d)(?:\+?1[-. ]?)?(?:\(\d{3}\)\s?|\d{3}[-. ])\d{3}[-. ]\d{4}(?!\d)", RegexOptions.Compiled);

    // Order matters: longer/more specific patterns first so they are not partially consumed.
    public static string Redact(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        text = Email.Replace(text, Placeholder);
        text = Card.Replace(text, Placeholder);
        text = Ssn.Replace(text, Placeholder);
        text = Phone.Replace(text, Placeholder);
        return text;
    }
}
