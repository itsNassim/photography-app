using System;
using System.Windows.Forms;
using PhotoStudioApp.Data;
using PhotoStudioApp.Services;
using PhotoStudioApp.UI.Dialogs;

namespace PhotoStudioApp.UI
{
    /// <summary>Shared "enter the admin password to continue" check, used before any
    /// Stock/Categories/History create-edit-delete-unlock action. actionName/buttonLabel
    /// describe what triggered the prompt (e.g. "Supprimer une journee de l'historique" /
    /// "Supprimer") purely so the login log (see PasswordPromptDialog/LoginLogService) records
    /// what was actually happening - they have no effect on the password check itself.</summary>
    public static class AuthGate
    {
        public static bool RequireAdminPassword(IWin32Window owner, string actionName, string buttonLabel = null)
        {
            var settingsRepo = new SettingsRepository();
            var settings = settingsRepo.LoadAll();
            if (string.IsNullOrEmpty(settings.PasswordHash))
            {
                MessageBox.Show(owner,
                    "Aucun mot de passe administrateur n'est defini. Definissez-en un dans l'onglet Historique > Paramètres.",
                    "Mot de passe requis", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            // Refuse to even open the prompt while a lockout from a previous run of wrong
            // attempts is still active (see PasswordPromptDialog, which sets this the moment
            // MaxPasswordAttempts is reached) - this is the "stop for N minutes" half of the
            // lockout feature; the "N wrong attempts" half is MaxPasswordAttempts itself.
            if (DateTime.TryParse(settings.PasswordLockoutUntilUtc, null,
                    System.Globalization.DateTimeStyles.RoundtripKind | System.Globalization.DateTimeStyles.AssumeUniversal,
                    out DateTime lockoutUntilUtc) && lockoutUntilUtc > DateTime.UtcNow)
            {
                string remaining = FormatRemaining(lockoutUntilUtc - DateTime.UtcNow);
                MessageBox.Show(owner,
                    $"Trop de tentatives incorrectes recemment. Reessayez dans {remaining}.",
                    "Acces temporairement bloque", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                LoginLogService.Record(actionName, buttonLabel, 0, false, $"Bloque - verrouillage actif ({remaining} restantes)");
                return false;
            }

            using (var dlg = new PasswordPromptDialog(settings.MaxPasswordAttempts, actionName, buttonLabel))
            {
                return dlg.ShowDialog(owner) == DialogResult.OK;
            }
        }

        /// <summary>Rounds up to the next whole minute (a lockout of "23 seconds remaining" would
        /// be confusing to show verbatim - the person just needs a rough sense of how long to
        /// wait) so the message always reads as at least "1 minute".</summary>
        internal static string FormatRemaining(TimeSpan remaining)
        {
            int minutes = Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes));
            return minutes == 1 ? "1 minute" : $"{minutes} minutes";
        }
    }
}
