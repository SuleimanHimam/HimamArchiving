using System.Runtime.Versioning;
using System.Windows.Forms;

namespace Archiving.LicenseManager;

// مراسيل — licence manager (vendor side).
//
// Issues signed licences, and — the part the CLI never had — keeps the record of what was issued to
// whom, on which machine, and until when. Runs on our machine only: it holds the private half of
// the signing key, and a copy on a customer's server would let them mint their own licences.
//
// The CLI in tools/licensegen still works and is unchanged; this is the same format and the same
// two shared source files, with a window and a ledger around them.

[SupportedOSPlatform("windows")]
internal static class Program
{
    [STAThread]
    private static int Main()
    {
        ApplicationConfiguration.Initialize();

        var vault = Vault.Load();

        Ledger ledger;
        try
        {
            ledger = Ledger.Load(vault.LedgerPath ?? vault.DefaultLedgerPath);
        }
        catch (Exception ex)
        {
            // A corrupt ledger is the one failure worth refusing to start over: carrying on with an
            // empty list would overwrite the entire issuing history on the next licence.
            MessageBox.Show(ex.Message, "تعذّرت قراءة سجل التراخيص", MessageBoxButtons.OK, MessageBoxIcon.Error,
                MessageBoxDefaultButton.Button1, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
            return 1;
        }

        Application.Run(new MainForm(vault, ledger));
        return 0;
    }
}
