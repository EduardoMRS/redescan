using RedeScan.Core.Services.Media;

namespace RedeScan.Core.Tests;

public class FfmpegArgsTests
{
    [Fact]
    public void MaskCredentials_HidesUserAndPassword()
    {
        var masked = FfmpegArgs.MaskCredentials("falha em rtsp://admin:s3nh4@10.0.0.5:554/live");
        Assert.DoesNotContain("s3nh4", masked);
        Assert.DoesNotContain("admin", masked);
        Assert.Contains("10.0.0.5:554/live", masked);
    }

    [Theory]
    [InlineData("method DESCRIBE failed: 401 Unauthorized", "401")]
    [InlineData("Server returned 403 Forbidden (access denied)", "403")]
    [InlineData("method DESCRIBE failed: 404 Not Found", "404")]
    [InlineData("Connection to tcp://10.0.0.5:554 failed: Connection refused", "recusada")]
    public void Explain_TranslatesCommonErrors(string stderr, string expected)
    {
        Assert.Contains(expected, FfmpegArgs.Explain(stderr), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TransportsFor_RtspAutoTriesTcpThenUdp()
    {
        Assert.Equal(new string?[] { "tcp", "udp" }, FfmpegArgs.TransportsFor("rtsp://x/y", "auto"));
        Assert.Equal(new string?[] { null }, FfmpegArgs.TransportsFor("http://x/y.mjpg", "auto"));
    }

    [Fact]
    public void Input_PassesUrlAsSingleArgument()
    {
        // URL com espaço e aspas não pode virar vários argumentos (sem shell).
        var args = FfmpegArgs.Input("rtsp://h/a b\"c", "tcp").ToList();
        var i = args.IndexOf("-i");
        Assert.Equal("rtsp://h/a b\"c", args[i + 1]);
    }
}
