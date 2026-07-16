using CsdbBridge.Services;
using Xunit;

namespace CsdbBridge.Tests;

public class ClassifierAndRssTests
{
    [Theory]
    [InlineData("C64 Crack", "crack")]
    [InlineData("C64 One-File Demo", "demo")]
    [InlineData("C64 Demo", "demo")]
    [InlineData("C64 Graphics", "other")]
    [InlineData("C64 Music", "sid")]
    public void ClassifyReleaseType(string input, string expected)
        => Assert.Equal(expected, CsdbClassifier.ClassifyReleaseType(input));

    [Fact]
    public void PlatformFolderForKind_is_always_c64()
    {
        Assert.Equal("c64", CsdbClassifier.PlatformFolderForKind("demo"));
        Assert.Equal("c64", CsdbClassifier.PlatformFolderForKind("crack"));
        Assert.Equal("c64", CsdbClassifier.PlatformFolderForKind("sid"));
        Assert.Equal("c64", CsdbClassifier.PlatformFolderForKind("other"));
    }

    [Fact]
    public void PackageBaseName_includes_type_and_csdb_id()
    {
        Assert.Equal("Foo (Demo) (csdb-123)", CsdbClassifier.PackageBaseName("Foo", "demo", 123));
        Assert.Equal("Bar (Crack) (csdb-9)", CsdbClassifier.PackageBaseName("Bar", "crack", 9));
        Assert.Equal("Tune (SID) (csdb-1)", CsdbClassifier.PackageBaseName("Tune", "sid", 1));
    }

    [Fact]
    public void SanitizeName_strips_invalid()
    {
        var s = CsdbClassifier.SanitizeName("Foo:Bar/Baz");
        Assert.DoesNotContain(":", s);
        Assert.DoesNotContain("/", s);
        Assert.Equal("untitled", CsdbClassifier.SanitizeName("   "));
    }

    [Fact]
    public void ParseRssItems_extracts_release_and_download()
    {
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <rss version="2.0"><channel><title>Test</title>
            <item>
              <title>Burglarized</title>
              <link>https://csdb.dk/release/?id=263020&amp;rss</link>
              <description><![CDATA[Type: <a href="x">C64 One-File Demo</a><br /><a href="https://csdb.dk/release/download.php?id=323665" title="https://csdb.dk/getinternalfile.php/281608/Burglarized.prg">Download</a>]]></description>
              <guid>https://csdb.dk/release/?id=263020</guid>
              <pubDate>Tue, 14 Jul 2026 00:00:00 +0200</pubDate>
            </item>
            </channel></rss>
            """;
        var items = RssIndexService.ParseRssItems(xml, "latest_releases");
        Assert.Single(items);
        Assert.Equal("Burglarized", items[0]["title"]);
        Assert.Equal("release", items[0]["entry_type"]);
        Assert.Equal(263020, Convert.ToInt32(items[0]["entry_id"]));
        Assert.Equal("demo", items[0]["kind"]);
        Assert.NotNull(items[0]["download_url"]);
    }
}
