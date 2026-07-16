using RomM.Client.Csdb;

namespace RomM.Client.Tests;

/// <summary>TEST-CSDB policy constants on options (Iteration 0 scaffold).</summary>
public sealed class CsdbLibraryOptionsTests
{
    [Fact]
    public void Validate_throws_when_LibraryRomsRoot_missing()
    {
        var options = new CsdbLibraryOptions { LibraryRomsRoot = "" };
        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.Contains("LibraryRomsRoot", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_enforces_MaxSearchLimit_ceiling_of_50()
    {
        var options = new CsdbLibraryOptions
        {
            LibraryRomsRoot = Path.GetTempPath(),
            MaxSearchLimit = 100,
        };
        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.Contains("50", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_succeeds_with_defaults()
    {
        var options = new CsdbLibraryOptions
        {
            LibraryRomsRoot = Path.GetTempPath(),
        };
        options.Validate();
        Assert.Equal(25, options.DefaultSearchLimit);
        Assert.Equal(50, options.MaxSearchLimit);
        Assert.Equal(20, options.MaxIngestBatch);
    }

    [Fact]
    public void ClampSearchLimit_refuses_over_max()
    {
        var options = new CsdbLibraryOptions { LibraryRomsRoot = Path.GetTempPath() };
        options.Validate();
        Assert.Throws<CsdbPolitenessException>(() => options.ClampSearchLimit(51));
    }

    [Fact]
    public void ClampSearchLimit_accepts_within_max()
    {
        var options = new CsdbLibraryOptions { LibraryRomsRoot = Path.GetTempPath() };
        options.Validate();
        Assert.Equal(20, options.ClampSearchLimit(20));
        Assert.Equal(25, options.ClampSearchLimit(null));
    }
}
