using Senso.Mcp.Ocr;
using Xunit;

namespace Senso.Mcp.Tests;

public class PiiRedactorTests
{
    [Theory]
    [InlineData("write to jane.doe+x@example.co.uk now", "write to [REDACTED] now")]
    [InlineData("card 4111 1111 1111 1111 ok", "card [REDACTED] ok")]
    [InlineData("card 4111-1111-1111-1111", "card [REDACTED]")]
    [InlineData("ssn 123-45-6789.", "ssn [REDACTED].")]
    [InlineData("call (555) 123-4567 or +1 555-123-4567", "call [REDACTED] or [REDACTED]")]
    public void Masks_known_patterns(string input, string expected) =>
        Assert.Equal(expected, PiiRedactor.Redact(input));

    [Theory]
    [InlineData("Invoice 1234567 total 99.50")]
    [InlineData("Order #20240101")]
    [InlineData("")]
    public void Leaves_ordinary_numbers_alone(string input) =>
        Assert.Equal(input, PiiRedactor.Redact(input));

    [Fact]
    public void Does_not_catch_names_documenting_the_limitation() =>
        Assert.Equal("Contact Jane Smith, 12 Oak Street", PiiRedactor.Redact("Contact Jane Smith, 12 Oak Street"));
}
