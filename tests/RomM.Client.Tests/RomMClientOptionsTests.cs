namespace RomM.Client.Tests;

/// <summary>TEST-DX-001: options validation.</summary>
public sealed class RomMClientOptionsTests
{
    [Fact]
    public void Validate_throws_when_BaseAddress_missing()
    {
        var options = new RomMClientOptions();
        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.Contains("BaseAddress", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_succeeds_when_BaseAddress_set()
    {
        var options = new RomMClientOptions
        {
            BaseAddress = new Uri("http://192.168.1.77:8080/"),
        };
        options.Validate();
    }

    [Fact]
    public void Validate_normalizes_BaseAddress_trailing_slash()
    {
        var options = new RomMClientOptions
        {
            BaseAddress = new Uri("http://example.com"),
        };
        options.Validate();
        Assert.Equal("http://example.com/", options.BaseAddress!.AbsoluteUri);
    }
}
