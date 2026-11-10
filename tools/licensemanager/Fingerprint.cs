using System.Text;

namespace Archiving.LicenseManager;

/// <summary>
/// Validation and repair for a machine fingerprint the customer typed or pasted.
///
/// This exists because of what a wrong character costs. A licence is signed over the fingerprint, so
/// a single mistyped letter produces a perfectly valid, perfectly useless licence: it verifies, it
/// says the right customer name, and the customer's server rejects it — usually discovered by them,
/// on a Sunday, after the vendor has gone home. Catching it here, before signing, is worth the
/// hundred lines.
///
/// The format is fixed by MachineFingerprint: twenty characters of Crockford-style Base32 in four
/// groups of five. That alphabet deliberately omits I, L, O and U, which is what makes repair
/// possible — a person reading a screen aloud says "oh" for zero and "eye" for one, and those
/// substitutions are unambiguous precisely because the real letters are not in the alphabet.
/// </summary>
internal static class Fingerprint
{
    /// <summary>The alphabet MachineFingerprint.Base32 emits. Kept in sync by the test below.</summary>
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    private const int Groups = 4;
    private const int GroupSize = 5;
    public const int Length = Groups * GroupSize;

    /// <summary>A licence for our own demo and development boxes, bound to no machine.</summary>
    public const string AnyMachine = "*";

    public enum Verdict { Valid, Repaired, Invalid }

    public readonly record struct Result(Verdict Verdict, string Value, string? Message)
    {
        public bool Usable => Verdict is Verdict.Valid or Verdict.Repaired;
    }

    /// <summary>
    /// Normalises what the user gave us, repairing the confusions a human transcription introduces,
    /// and reports honestly which of the three things happened. A repair is surfaced rather than
    /// applied silently: the operator should see that the string they pasted was not quite the
    /// string being signed.
    /// </summary>
    public static Result Check(string? raw)
    {
        var input = (raw ?? "").Trim();
        if (input.Length == 0)
            return new Result(Verdict.Invalid, "", "أدخل بصمة الجهاز.");

        if (input == AnyMachine)
            return new Result(Verdict.Valid, AnyMachine, "ترخيص غير مرتبط بجهاز — للعروض والتطوير فقط.");

        // Strip everything that is only presentation: dashes, spaces, and the line breaks that come
        // with a fingerprint pasted out of an email.
        var stripped = new StringBuilder(Length);
        foreach (var c in input.ToUpperInvariant())
        {
            if (c is '-' or ' ' or '\t' or '\r' or '\n') continue;
            stripped.Append(c);
        }

        var chars = stripped.ToString();
        if (chars.Length != Length)
            return new Result(Verdict.Invalid, "",
                $"الطول غير صحيح: {chars.Length} حرفًا بدل {Length}. البصمة أربع مجموعات من خمسة أحرف.");

        var repaired = false;
        var fixedChars = new StringBuilder(Length);
        foreach (var c in chars)
        {
            var mapped = c switch
            {
                // Safe because none of these four are in the alphabet — the mapping cannot destroy a
                // legitimate character, it can only recover one a person mis-transcribed.
                'I' or 'L' => '1',
                'O' => '0',
                'U' => 'V',
                _ => c,
            };
            if (mapped != c) repaired = true;

            if (Alphabet.IndexOf(mapped) < 0)
                return new Result(Verdict.Invalid, "",
                    $"الحرف '{c}' لا يظهر في بصمات هذا النظام. تحقّق من النسخ.");

            fixedChars.Append(mapped);
        }

        var value = Group(fixedChars.ToString());
        return repaired
            ? new Result(Verdict.Repaired, value,
                "صُحّحت أحرف متشابهة (O→0 · I/L→1 · U→V). تأكّد أن النتيجة تطابق ما يظهر على شاشة العميل.")
            : new Result(Verdict.Valid, value, null);
    }

    private static string Group(string s) =>
        string.Join('-', Enumerable.Range(0, s.Length / GroupSize)
                                   .Select(i => s.Substring(i * GroupSize, GroupSize)));
}
