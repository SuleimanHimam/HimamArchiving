using Archiving.Infrastructure.Storage;
using Xunit;

namespace Archiving.Tests;

/// <summary>Path-traversal and naming-scheme cover for <see cref="StorageKeys"/>. Every case here is a
/// way a client-supplied name has historically escaped a storage root.</summary>
public class StorageKeyTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "archiving-key-tests");

    [Fact]
    public void NewKey_UsesShardedUuidScheme()
    {
        var key = StorageKeys.NewKey("documents", new DateTime(2026, 9, 3, 0, 0, 0, DateTimeKind.Utc));

        Assert.StartsWith("documents/2026/09/", key);
        Assert.EndsWith(".enc", key);
        Assert.True(StorageKeys.IsWellFormed(key));

        // documents / yyyy / MM / ab / cd / <32 hex>.enc
        var parts = key.Split('/');
        Assert.Equal(6, parts.Length);
        Assert.Equal(2, parts[3].Length);
        Assert.Equal(2, parts[4].Length);
        Assert.Equal(36, parts[5].Length);           // 32 hex + ".enc"
        Assert.StartsWith(parts[3] + parts[4], parts[5]);   // shard mirrors the identifier
    }

    [Fact]
    public void NewKey_DiscardsCallerFileNameAndExtension()
    {
        var key = StorageKeys.NewKey("documents");

        Assert.DoesNotContain("payroll", key);
        Assert.EndsWith(".enc", key);   // nothing on the file server discloses the content type
    }

    [Fact]
    public void NewKey_IsUnpredictable()
    {
        var keys = Enumerable.Range(0, 500).Select(_ => StorageKeys.NewKey("documents")).ToHashSet();
        Assert.Equal(500, keys.Count);
    }

    [Theory]
    [InlineData("../etc")]
    [InlineData("documents/../../etc")]
    [InlineData("/etc/passwd")]
    [InlineData("C:/Windows")]
    [InlineData(@"..\..\windows")]
    [InlineData("documents/.")]
    [InlineData("doc uments")]
    [InlineData("documents\0evil")]
    public void NormaliseFolder_RejectsUnsafeInput(string folder)
    {
        Assert.ThrowsAny<ArgumentException>(() => StorageKeys.NormaliseFolder(folder));
    }

    [Theory]
    [InlineData("documents/2026/09/3f/a1/../../../../../../etc/passwd")]
    [InlineData("../../../windows/system32/config/sam")]
    [InlineData("documents/2026/09/3f/a1/deadbeef.pdf")]     // wrong extension
    [InlineData("documents/2026/09/3f/a1/NOTHEX.enc")]
    [InlineData("")]
    public void ResolvePath_RejectsMalformedKeys(string key)
    {
        Assert.ThrowsAny<Exception>(() => StorageKeys.ResolvePath(Root, key));
    }

    [Fact]
    public void ResolvePath_RejectsSiblingDirectoryWithSharedPrefix()
    {
        // The classic prefix bug: root "…/store" must not accept a path under "…/store-evil".
        var key = StorageKeys.NewKey("documents");
        var resolved = StorageKeys.ResolvePath(Root, key);

        Assert.StartsWith(Root + Path.DirectorySeparatorChar, resolved);
        Assert.False(StorageKeys.ResolvePath(Root, key).StartsWith(Root + "-evil", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ResolvePath_StaysUnderRoot()
    {
        var key = StorageKeys.NewKey("documents/preservation");
        var resolved = StorageKeys.ResolvePath(Root, key);

        Assert.StartsWith(Path.GetFullPath(Root), resolved);
    }

    [Theory]
    [InlineData("../../evil.pdf", "evil.pdf")]
    [InlineData(@"C:\secrets\report.pdf", "report.pdf")]
    [InlineData("head\r\ninjected: yes", "headinjected_ yes")]
    [InlineData("", "document")]
    [InlineData("   ", "document")]
    public void SafeDisplayName_StripsPathsAndHeaderInjection(string input, string expected)
    {
        Assert.Equal(expected, StorageKeys.SafeDisplayName(input));
    }

    [Fact]
    public void SafeDisplayName_KeepsArabicNames()
    {
        Assert.Equal("قرار إداري.pdf", StorageKeys.SafeDisplayName("قرار إداري.pdf"));
    }
}
