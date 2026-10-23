using RedeScan.Core.Services;

namespace RedeScan.Core.Tests;

public class ScanTargetParserTests
{
    [Fact]
    public void Parse_UserExample_MixesCidrPrefixAndRange()
    {
        var r = ScanTargetParser.Parse("192.168.0.0/24, 192.168.0 10.0.0 10.0.1.1/23 10.1.0.1-10.1.1.10");

        Assert.Empty(r.Errors);
        Assert.Equal(new[] { "192.168.0.0/24", "192.168.0.0/24", "10.0.0.0/24", "10.0.0.0/23", "10.1.0.1-10.1.1.10" }, r.Ranges);

        // 192.168.0.0/24 aparece 2x mas conta uma vez (254); 10.0.0.0/24 está
        // dentro de 10.0.0.0/23 (510); a faixa tem 256 + 10 = 266.
        Assert.Equal(254 + 510 + 266, r.Addresses.Count);
        Assert.Equal("10.0.0.1", r.Addresses[0].ToString());
        Assert.Contains(r.Addresses, a => a.ToString() == "10.0.1.254");
        Assert.DoesNotContain(r.Addresses, a => a.ToString() == "192.168.0.0");
        Assert.DoesNotContain(r.Addresses, a => a.ToString() == "192.168.0.255");
    }

    [Theory]
    [InlineData("192.168.0.", 254, "192.168.0.0/24")]
    [InlineData("10.0", 65534, "10.0.0.0/16")]
    [InlineData("192.168.1.10-20", 11, "192.168.1.10-192.168.1.20")]
    [InlineData("192.168.1.7", 1, "192.168.1.7")]
    [InlineData("192.168.1.8/31", 2, "192.168.1.8/31")]
    [InlineData("192.168.1.5/32", 1, "192.168.1.5/32")]
    public void Parse_SingleToken(string input, int count, string label)
    {
        var r = ScanTargetParser.Parse(input);
        Assert.True(r.IsValid);
        Assert.Equal(count, r.Addresses.Count);
        Assert.Equal(label, r.Ranges.Single());
    }

    [Theory]
    [InlineData("192.168.0.0/33")]
    [InlineData("300.1.1.1")]
    [InlineData("10.0.0.50-10.0.0.10")]
    [InlineData("roteador")]
    [InlineData("10")]
    public void Parse_InvalidTokens_AreReported(string input)
    {
        var r = ScanTargetParser.Parse(input);
        Assert.False(r.IsValid);
        Assert.Single(r.Errors);
    }

    [Fact]
    public void Parse_SeparatorsAndEmptyItems_AreTolerated()
    {
        var r = ScanTargetParser.Parse(" 192.168.0.1 ;\n192.168.0.2,,  192.168.0.3\t");
        Assert.True(r.IsValid);
        Assert.Equal(3, r.Addresses.Count);
    }

    [Fact]
    public void Parse_RespectsHostLimit()
    {
        var r = ScanTargetParser.Parse("10.0.0.0/8", maxHosts: 1000);
        Assert.True(r.Truncated);
        Assert.Equal(1000, r.Addresses.Count);
    }
}
