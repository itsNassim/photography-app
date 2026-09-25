using System;
using System.Windows.Forms;
using PhotoStudioApp.Data;
using PhotoStudioApp.Services;

namespace PhotoStudioApp.UI.Dialogs
{
    /// <summary>Prompts for the admin password before a protected action proceeds (closing the
    /// day, Stock/Categories CRUD, opening History, etc). Locks out after the configured number
    /// of wrong attempts (DialogResult.Cancel either way).
    ///
    /// This is also the single place every one of those prompts gets recorded into the login
    /// log (see LoginLogService): actionName/buttonLabel describe what was being protected, and
    /// FormClosed - which fires no matter how the dialog closes (OK, Cancel button, lockout, or
    /// the window's own X/Alt+F4) - logs exactly once with however many wrong attempts were made
    /// and the final outcome. Centralizing it here (rather than at each of AuthGate's many call
    /// sites) guarantees no password prompt can ever go unlogged.</summary>
    public class PasswordPromptDialog : Form
    {
        private readonly TextBox _pwdBox;
        private readonly Label _errorLabel;
        private readonly int _maxAttempts;
        private int _attempts;
        private readonly SettingsRepository _settingsRepo = new SettingsRepository();
        private readonly string _actionName;
        private readonly string _buttonLabel;
        private bool _logged;

        public PasswordPromptDialog(int maxAttempts, string actionName = null, string buttonLabel = null)
        {
            _maxAttempts = maxAttempts;
            _actionName = string.IsNullOrWhiteSpace(actionName) ? "Action protégée" : actionName;
            _buttonLabel = buttonLabel;

            Text = "Mot de passe requis";
            Width = 400;
            Height = 220;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            Font = Theme.FontBase;
            BackColor = Theme.CardBackground;

            var label = new Label { Text = "Cette action est protégée.\nEntrez le mot de passe administrateur pour continuer :", AutoSize = true, Left = 20, Top = 16 };
            _pwdBox = new TextBox { Left = 20, Top = 70, Width = 340, UseSystemPasswordChar = true, Font = Theme.FontBase };
            _errorLabel = new Label { Left = 20, Top = 98, Width = 340, ForeColor = Theme.Warning, AutoSize = true };

            var okBtn = Theme.MakePrimaryButton("Confirmer");
            okBtn.Left = 160; okBtn.Top = 130; okBtn.Width = 100;
            okBtn.Click += OkBtn_Click;
            var cancelBtn = Theme.MakeSecondaryButton("Annuler");
            cancelBtn.Left = 260; cancelBtn.Top = 130; cancelBtn.Width = 100;
            cancelBtn.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            Controls.Add(label);
            Controls.Add(_pwdBox);
            Controls.Add(_errorLabel);
            Controls.Add(okBtn);
            Controls.Add(cancelBtn);

            AcceptButton = okBtn;
            CancelButton = cancelBtn;

            FormClosed += (s, e) => LogIfNeeded();
        }

        private void OkBtn_Click(object sender, EventArgs e)
        {
            var settings = _settingsRepo.LoadAll();
            if (PasswordService.Verify(_pwdBox.Text, settings.PasswordHash, settings.PasswordSalt))
            {
                DialogResult = DialogResult.OK;
                Close();
                return;
            }

            _attempts++;
            int remaining = _maxAttempts - _attempts;
            if (remaining <= 0)
            {
                MessageBox.Show("Trop de tentatives incorrectes. Action annulee.",
                    "Acces bloque", MessageBoxButtons.OK, MessageBoxIcon.Error);
                DialogResult = DialogResult.Cancel;
                Close();
                return;
            }

            _errorLabel.Text = $"Mot de passe incorrect. {remaining} tentative(s) restante(s).";
            _pwdBox.SelectAll();
            _pwdBox.Focus();
        }

        private void LogIfNeeded()
        {
            if (_logged) return;
            _logged = true;

            bool success = DialogResult == DialogResult.OK;
            string details;
            if (success)
                details = null;
            else if (_attempts >= _maxAttempts)
                details = "Verrouille - trop de tentatives incorrectes";
            else
                details = "Annule par l'utilisateur";

            LoginLogService.Record(_actionName, _buttonLabel, _attempts, success, details);
        }
    }
}
