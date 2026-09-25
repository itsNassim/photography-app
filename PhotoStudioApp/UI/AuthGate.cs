using System.Windows.Forms;
using PhotoStudioApp.Data;
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
            var settings = new SettingsRepository().LoadAll();
            if (string.IsNullOrEmpty(settings.PasswordHash))
            {
                MessageBox.Show(owner,
                    "Aucun mot de passe administrateur n'est defini. Definissez-en un dans l'onglet Paramètres.",
                    "Mot de passe requis", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            using (var dlg = new PasswordPromptDialog(settings.MaxPasswordAttempts, actionName, buttonLabel))
            {
                return dlg.ShowDialog(owner) == DialogResult.OK;
            }
        }
    }
}
