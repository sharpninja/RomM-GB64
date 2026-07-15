using System.IO.Compression;
using System.Text;
using CsdbBridge.Services;
using Xunit;

namespace CsdbBridge.Tests;

public class ArchiveExtractorTests
{
    [Theory]
    [InlineData("game.zip", true)]
    [InlineData("game.7z", true)]
    [InlineData("game.rar", true)]
    [InlineData("game.d64", false)]
    [InlineData("game.prg", false)]
    public void IsArchiveFileName(string name, bool expected)
        => Assert.Equal(expected, ArchiveExtractor.IsArchiveFileName(name));

    [Fact]
    public void SanitizeEntryPath_rejects_traversal()
    {
        Assert.Null(ArchiveExtractor.SanitizeEntryPath("../evil.prg"));
        Assert.Null(ArchiveExtractor.SanitizeEntryPath("foo/../../evil.prg"));
        Assert.Null(ArchiveExtractor.SanitizeEntryPath(@"C:\abs\path.prg"));
        Assert.Equal(Path.Combine("disk", "game.d64"), ArchiveExtractor.SanitizeEntryPath("disk/game.d64"));
        // Leading slash is treated as relative archive path (common in zip entries)
        Assert.Equal(Path.Combine("abs", "path.prg"), ArchiveExtractor.SanitizeEntryPath("/abs/path.prg"));
    }

    [Fact]
    public void ExtractToDirectory_zip_writes_files_under_folder()
    {
        var zipBytes = BuildZip(new Dictionary<string, byte[]>
        {
            ["readme.txt"] = Encoding.UTF8.GetBytes("hello"),
            ["disk/game.d64"] = new byte[] { 1, 2, 3, 4 },
        });

        var dest = Path.Combine(Path.GetTempPath(), "csdb-zip-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            Assert.True(ArchiveExtractor.IsArchive(zipBytes, "pack.zip"));
            var files = ArchiveExtractor.ExtractToDirectory(zipBytes, "pack.zip", dest);
            Assert.True(files.Count >= 2);
            Assert.All(files, f => Assert.StartsWith(dest, f, StringComparison.OrdinalIgnoreCase));
            Assert.Contains(files, f => f.EndsWith("readme.txt", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(files, f => f.EndsWith("game.d64", StringComparison.OrdinalIgnoreCase));
            // Archive itself must not remain
            Assert.Empty(Directory.GetFiles(dest, "*.zip", SearchOption.AllDirectories));
        }
        finally
        {
            if (Directory.Exists(dest))
                Directory.Delete(dest, true);
        }
    }

    [Fact]
    public void ExtractToDirectory_single_root_folder_is_hoisted()
    {
        var zipBytes = BuildZip(new Dictionary<string, byte[]>
        {
            ["Bundle/game.prg"] = new byte[] { 9, 9, 9 },
        });

        var dest = Path.Combine(Path.GetTempPath(), "csdb-zip-hoist-" + Guid.NewGuid().ToString("N"));
        try
        {
            var files = ArchiveExtractor.ExtractToDirectory(zipBytes, "bundle.zip", dest);
            Assert.Single(files);
            Assert.Equal(Path.Combine(dest, "game.prg"), files[0]);
            Assert.False(Directory.Exists(Path.Combine(dest, "Bundle")));
        }
        finally
        {
            if (Directory.Exists(dest))
                Directory.Delete(dest, true);
        }
    }

    private static byte[] BuildZip(Dictionary<string, byte[]> entries)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, data) in entries)
            {
                var entry = zip.CreateEntry(name, CompressionLevel.Fastest);
                using var s = entry.Open();
                s.Write(data, 0, data.Length);
            }
        }

        return ms.ToArray();
    }
}
