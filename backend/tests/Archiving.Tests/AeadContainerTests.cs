using System.Security.Cryptography;
using Archiving.Infrastructure.Security.Crypto;
using Xunit;

namespace Archiving.Tests;

/// <summary>
/// Covers the AENC3 container: that it round-trips exactly, and that each of the ways an attacker can
/// alter a file on the server is detected rather than silently served as a document.
/// </summary>
public class AeadContainerTests
{
    private const int SmallChunk = AeadFormat.MinChunkSize;   // 16 KiB, so multi-frame cases stay fast

    private static byte[] NewKey() => RandomNumberGenerator.GetBytes(32);

    private static async Task<byte[]> SealAsync(byte[] key, byte[] plaintext, int chunkSize = SmallChunk)
    {
        var ms = new MemoryStream();
        await using (var sealer = new AeadWritingStream(ms, key, chunkSize, leaveOpen: true))
        {
            await sealer.WriteAsync(plaintext);
            await sealer.FinishAsync();
        }
        return ms.ToArray();
    }

    private static async Task<byte[]> OpenAsync(byte[] key, byte[] container)
    {
        await using var reader = await AeadReadingStream.OpenAsync(new MemoryStream(container), key);
        var outp = new MemoryStream();
        await reader.CopyToAsync(outp);
        return outp.ToArray();
    }

    [Theory]
    [InlineData(0)]                       // empty document still costs one sealed frame
    [InlineData(1)]
    [InlineData(SmallChunk - 1)]
    [InlineData(SmallChunk)]              // exact multiple: final frame is zero-length
    [InlineData(SmallChunk + 1)]
    [InlineData(SmallChunk * 3 + 77)]     // several frames plus a remainder
    public async Task RoundTrips_Exactly(int size)
    {
        var key = NewKey();
        var plaintext = RandomNumberGenerator.GetBytes(size);

        var container = await SealAsync(key, plaintext);
        var recovered = await OpenAsync(key, container);

        Assert.Equal(plaintext, recovered);
    }

    [Fact]
    public async Task Container_IsNotPlaintext()
    {
        var key = NewKey();
        var plaintext = "سري للغاية — CONFIDENTIAL PAYROLL 2026"u8.ToArray();

        var container = await SealAsync(key, plaintext);

        // The marker bytes must not appear anywhere in the file written to the server.
        Assert.DoesNotContain(Convert.ToHexString(plaintext), Convert.ToHexString(container));
    }

    [Fact]
    public async Task WrongKey_IsRejected()
    {
        var container = await SealAsync(NewKey(), RandomNumberGenerator.GetBytes(4096));
        await Assert.ThrowsAsync<AuthenticationTagMismatchException>(() => OpenAsync(NewKey(), container));
    }

    [Fact]
    public async Task FlippedCiphertextBit_IsDetected()
    {
        var key = NewKey();
        var container = await SealAsync(key, RandomNumberGenerator.GetBytes(4096));

        container[AeadFormat.HeaderLen + 10] ^= 0x01;

        await Assert.ThrowsAsync<AuthenticationTagMismatchException>(() => OpenAsync(key, container));
    }

    [Fact]
    public async Task TamperedHeader_IsDetected()
    {
        var key = NewKey();
        var container = await SealAsync(key, RandomNumberGenerator.GetBytes(4096));

        container[12] ^= 0xFF;   // first byte of the nonce prefix — header is bound as AAD

        await Assert.ThrowsAsync<AuthenticationTagMismatchException>(() => OpenAsync(key, container));
    }

    [Fact]
    public async Task Truncation_IsDetected()
    {
        var key = NewKey();
        // Three full frames plus a remainder, so dropping the tail leaves a structurally valid file.
        var container = await SealAsync(key, RandomNumberGenerator.GetBytes(SmallChunk * 3 + 500));

        var truncated = container[..(AeadFormat.HeaderLen + (SmallChunk + AeadFormat.TagLen))];

        // The surviving frame was sealed as non-final; verifying it as final fails. Truncating an
        // encrypted archive must never look like a shorter document.
        await Assert.ThrowsAnyAsync<Exception>(() => OpenAsync(key, truncated));
    }

    [Fact]
    public async Task ReorderedFrames_AreDetected()
    {
        var key = NewKey();
        var container = await SealAsync(key, RandomNumberGenerator.GetBytes(SmallChunk * 2 + 100));

        // Swap frame 0 and frame 1 — each is individually valid, only the order is wrong.
        var frameLen = SmallChunk + AeadFormat.TagLen;
        var swapped = (byte[])container.Clone();
        Array.Copy(container, AeadFormat.HeaderLen + frameLen, swapped, AeadFormat.HeaderLen, frameLen);
        Array.Copy(container, AeadFormat.HeaderLen, swapped, AeadFormat.HeaderLen + frameLen, frameLen);

        await Assert.ThrowsAsync<AuthenticationTagMismatchException>(() => OpenAsync(key, swapped));
    }

    [Fact]
    public async Task FramesFromAnotherObject_CannotBeSpliced()
    {
        var key = NewKey();
        var a = await SealAsync(key, RandomNumberGenerator.GetBytes(SmallChunk * 2));
        var b = await SealAsync(key, RandomNumberGenerator.GetBytes(SmallChunk * 2));

        // Same key, but each object has its own random nonce prefix in its own authenticated header.
        var frameLen = SmallChunk + AeadFormat.TagLen;
        var spliced = (byte[])a.Clone();
        Array.Copy(b, AeadFormat.HeaderLen, spliced, AeadFormat.HeaderLen, frameLen);

        await Assert.ThrowsAsync<AuthenticationTagMismatchException>(() => OpenAsync(key, spliced));
    }

    [Fact]
    public async Task NoncePrefix_IsUniquePerObject()
    {
        var key = NewKey();
        var seen = new HashSet<string>();

        for (var i = 0; i < 200; i++)
        {
            var container = await SealAsync(key, [1, 2, 3]);
            Assert.True(seen.Add(Convert.ToHexString(container.AsSpan(12, AeadFormat.NoncePrefixLen))),
                "nonce prefix repeated — (key, nonce) reuse breaks AES-GCM completely");
        }
    }

    [Fact]
    public async Task PlaintextSize_IsReported()
    {
        var ms = new MemoryStream();
        await using var sealer = new AeadWritingStream(ms, NewKey(), SmallChunk, leaveOpen: true);
        await sealer.WriteAsync(RandomNumberGenerator.GetBytes(1234));
        await sealer.FinishAsync();

        Assert.Equal(1234, sealer.PlaintextLength);
        Assert.True(ms.Length > 1234, "container must carry header and tags on top of the payload");
    }
}
