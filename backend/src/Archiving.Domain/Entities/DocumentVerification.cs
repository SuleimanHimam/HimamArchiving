using System.Security.Cryptography;

namespace Archiving.Domain.Entities;

/// <summary>
/// The code printed on a document as a QR, and the only thing a stranger holding the paper needs
/// in order to ask the system whether it issued it.
///
/// Not the document's id: an id printed on paper is an invitation to try the next one, and it
/// would let anyone who receives a single letter walk the whole register. This is 10 characters
/// drawn from a cryptographic RNG over a 30-symbol alphabet — around 6×10^14 possibilities,
/// unrelated to anything else about the document, and therefore safe to print.
///
/// The alphabet leaves out the glyphs that get confused when the code is keyed in by hand rather
/// than scanned: 0/O, 1/I/L, and U (which reads as V in careless handwriting). Excluding them is
/// the repair — there is no honest way to guess which of two look-alikes was meant, so the code
/// never contains either.
/// </summary>
public static class DocumentVerification
{
    public const int Length = 10;

    private const string Alphabet = "ABCDEFGHJKMNPQRSTVWXYZ23456789";

    public static string NewCode()
    {
        Span<char> buffer = stackalloc char[Length];
        for (var i = 0; i < Length; i++)
            buffer[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        return new string(buffer);
    }

    /// <summary>
    /// Cleans up what arrived — from a scan, a URL, or someone typing what they read — and returns
    /// null when it cannot be a code at all. Case and the separators people insert while reading a
    /// long code aloud are forgiven; an out-of-alphabet character is not, because repairing it
    /// would mean inventing a different document's code.
    /// </summary>
    public static string? Normalize(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;

        Span<char> buffer = stackalloc char[Length];
        var n = 0;
        foreach (var raw in code)
        {
            if (raw is '-' or ' ' or '_') continue;
            if (n == Length) return null;                     // longer than a code: not one

            var c = char.ToUpperInvariant(raw);
            if (!Alphabet.Contains(c)) return null;
            buffer[n++] = c;
        }

        return n == Length ? new string(buffer) : null;
    }
}
