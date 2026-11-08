using Senso.Mcp.Input;
using Xunit;

namespace Senso.Mcp.Tests;

public class MediaInputResolverTests
{
    private static MediaInputResolver Make(Action<SensoOptions>? c = null) => new(Samples.Options(c));

    [Fact]
    public async Task Reads_png_from_file_and_reports_sniffed_format()
    {
        var path = Samples.TempFile("scan.png", Samples.Png);
        var m = await Make().ResolveAsync(path, null, MediaKind.Image, default);
        Assert.Equal("png", m.Format);
        Assert.Equal("image/png", m.MimeType);
    }

    [Fact]
    public async Task Content_signature_wins_over_extension_for_naming_but_extension_must_be_allowed()
    {
        var jpgNamedPng = Samples.TempFile("a.png", Samples.Jpeg);
        var m = await Make().ResolveAsync(jpgNamedPng, null, MediaKind.Image, default);
        Assert.Equal("jpeg", m.Format);

        var txt = Samples.TempFile("a.txt", Samples.Png);
        await Assert.ThrowsAsync<SensoException>(() => Make().ResolveAsync(txt, null, MediaKind.Image, default));
    }

    [Fact]
    public async Task Rejects_files_whose_content_is_not_media()
    {
        var path = Samples.TempFile("fake.png", Samples.Garbage);
        var ex = await Assert.ThrowsAsync<SensoException>(() => Make().ResolveAsync(path, null, MediaKind.Image, default));
        Assert.Equal(SensoErrorCode.InvalidInput, ex.Code);
    }

    [Fact]
    public async Task Accepts_base64_and_data_urls()
    {
        var b64 = Convert.ToBase64String(Samples.Wav);
        Assert.Equal("wav", (await Make().ResolveAsync(null, b64, MediaKind.Audio, default)).Format);
        Assert.Equal("wav", (await Make().ResolveAsync(null, "data:audio/wav;base64," + b64, MediaKind.Audio, default)).Format);
    }

    [Fact]
    public async Task Rejects_invalid_base64()
    {
        var ex = await Assert.ThrowsAsync<SensoException>(() => Make().ResolveAsync(null, "***not base64***", MediaKind.Image, default));
        Assert.Contains("base64", ex.Message);
    }

    [Theory]
    [InlineData("http://169.254.169.254/latest/meta-data/")]
    [InlineData("https://example.com/a.png")]
    [InlineData("file:///etc/passwd")]
    [InlineData("ftp://host/a.png")]
    public async Task Never_fetches_urls(string url)
    {
        var ex = await Assert.ThrowsAsync<SensoException>(() => Make().ResolveAsync(url, null, MediaKind.Image, default));
        Assert.Contains("URLs are not supported", ex.Message);
    }

    [Fact]
    public async Task Requires_exactly_one_input()
    {
        await Assert.ThrowsAsync<SensoException>(() => Make().ResolveAsync(null, null, MediaKind.Image, default));
        await Assert.ThrowsAsync<SensoException>(() => Make().ResolveAsync("a.png", "AAAA", MediaKind.Image, default));
    }

    [Fact]
    public async Task Enforces_size_limit_for_files_before_reading()
    {
        var big = new byte[2 * 1024 * 1024];
        Array.Copy(Samples.Png, big, Samples.Png.Length);
        var path = Samples.TempFile("big.png", big);
        var ex = await Assert.ThrowsAsync<SensoException>(() =>
            Make(o => o.Limits.MaxImageSizeMb = 1).ResolveAsync(path, null, MediaKind.Image, default));
        Assert.Contains("limit", ex.Message);
    }

    [Fact]
    public async Task Enforces_size_limit_for_base64()
    {
        var big = new byte[2 * 1024 * 1024];
        Array.Copy(Samples.Png, big, Samples.Png.Length);
        var ex = await Assert.ThrowsAsync<SensoException>(() =>
            Make(o => o.Limits.MaxImageSizeMb = 1).ResolveAsync(null, Convert.ToBase64String(big), MediaKind.Image, default));
        Assert.Contains("exceed", ex.Message);
    }

    [Fact]
    public async Task Honors_cancellation()
    {
        var path = Samples.TempFile("c.png", Samples.Png);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Make().ResolveAsync(path, null, MediaKind.Image, cts.Token));
    }

    [Fact]
    public async Task AllowedDirectories_permits_inside_and_blocks_outside_including_traversal()
    {
        var allowed = Path.Combine(Path.GetTempPath(), "senso-allowed-" + Guid.NewGuid().ToString("N"));
        var inside = Samples.TempFile("ok.png", Samples.Png, allowed);
        var outside = Samples.TempFile("no.png", Samples.Png);
        var resolver = Make(o => o.AllowedDirectories.Add(allowed));

        Assert.Equal("png", (await resolver.ResolveAsync(inside, null, MediaKind.Image, default)).Format);
        await Assert.ThrowsAsync<SensoException>(() => resolver.ResolveAsync(outside, null, MediaKind.Image, default));
        var traversal = Path.Combine(allowed, "..", Path.GetFileName(Path.GetDirectoryName(outside)!), "no.png");
        await Assert.ThrowsAsync<SensoException>(() => resolver.ResolveAsync(traversal, null, MediaKind.Image, default));
    }

    [Fact]
    public async Task AllowedDirectories_is_not_fooled_by_sibling_prefix_or_symlink()
    {
        var root = Path.Combine(Path.GetTempPath(), "senso-root-" + Guid.NewGuid().ToString("N"));
        var allowed = Path.Combine(root, "media");
        var sibling = Path.Combine(root, "media-evil");
        Samples.TempFile("ok.png", Samples.Png, allowed);
        var evil = Samples.TempFile("x.png", Samples.Png, sibling);
        var resolver = Make(o => o.AllowedDirectories.Add(allowed));
        await Assert.ThrowsAsync<SensoException>(() => resolver.ResolveAsync(evil, null, MediaKind.Image, default));

        if (!OperatingSystem.IsWindows())
        {
            var link = Path.Combine(allowed, "link.png");
            File.CreateSymbolicLink(link, evil);
            await Assert.ThrowsAsync<SensoException>(() => resolver.ResolveAsync(link, null, MediaKind.Image, default));
        }
    }
}
