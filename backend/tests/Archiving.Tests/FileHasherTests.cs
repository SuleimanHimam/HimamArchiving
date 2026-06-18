using System.Text;
using Archiving.Infrastructure.Services;
using Xunit;

namespace Archiving.Tests;

public class FileHasherTests
{
    [Fact]
    public async Task Sha256_matches_known_vector_uppercase_hex()
    {
        using var s = new MemoryStream(Encoding.UTF8.GetBytes("hello"));
        var hex = await FileHasher.Sha256HexAsync(s);
        // SHA-256("hello"), uppercase hex (matches the storage layer's Convert.ToHexString).
        Assert.Equal("2CF24DBA5FB0A30E26E83B2AC5B9E29E1B161E5C1FA7425E73043362938B9824", hex);
    }

    [Fact]
    public async Task Different_bytes_produce_different_digest()
    {
        using var a = new MemoryStream(Encoding.UTF8.GetBytes("document-v1"));
        using var b = new MemoryStream(Encoding.UTF8.GetBytes("document-v2"));
        Assert.NotEqual(await FileHasher.Sha256HexAsync(a), await FileHasher.Sha256HexAsync(b));
    }
}
