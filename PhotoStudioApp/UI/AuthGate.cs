using System.Windows.Forms;
using PhotoStudioApp.Data;
using PhotoStudioApp.UI.Dialogs;

namespace PhotoStudioApp.UI
{
    /// <summary>Shared "enter the admin password to continue" check, used before any
    /// Stock/Categories create-edit-delete action and before showing the History tab.</summary>
    public static class AuthGate
    {
        public static bool RequireAdminPassword(IWin32Window owner)
        {
            var settings = new SettingsRepository().LoadAll();
            if (string.IsNullOrEmpty(settings.PasswordHash))
            {
                MessageBox.Show(owner,
                    "Aucun mot de passe administrateur n'est defini. Definissez-en un dans l'onglet Parametres.",
                    "Mot de passe requis", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            using (var dlg = new PasswordPromptDialog(settings.MaxPasswordAttempts))
            {
                return dlg.ShowDialog(owner) == DialogResult.OK;
            }
        }
    }
}
