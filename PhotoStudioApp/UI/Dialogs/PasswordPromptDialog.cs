using System;
using System.Windows.Forms;
using PhotoStudioApp.Data;
using PhotoStudioApp.Services;

namespace PhotoStudioApp.UI.Dialogs
{
    /// <summary>Prompts for the closing password before Close Day proceeds. Locks out after
    /// the configured number of wrong attempts (DialogResult.Cancel either way).</summary>
    public class PasswordPromptDialog : Form
    {
        private readonly TextBox _pwdBox;
        private readonly Label _errorLabel;
        private readonly int _maxAttempts;
        private int _attempts;
        private readonly SettingsRepository _settingsRepo = new SettingsRepository();

        public PasswordPromptDialog(int maxAttempts)
        {
            _maxAttempts = maxAttempts;
            Text = "Enter closing password";
            Width = 400;
            Height = 220;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            Font = Theme.FontBase;
            BackColor = Theme.CardBackground;

            var label = new Label { Text = "This action will close and archive today's work.\nEnter the closing password to continue:", AutoSize = true, Left = 20, Top = 16 };
            _pwdBox = new TextBox { Left = 20, Top = 70, Width = 340, UseSystemPasswordChar = true, Font = Theme.FontBase };
            _errorLabel = new Label { Left = 20, Top = 98, Width = 340, ForeColor = Theme.Warning, AutoSize = true };

            var okBtn = Theme.MakePrimaryButton("Confirm");
            okBtn.Left = 160; okBtn.Top = 130; okBtn.Width = 100;
            okBtn.Click += OkBtn_Click;
            var cancelBtn = Theme.MakeSecondaryButton("Cancel");
            cancelBtn.Left = 260; cancelBtn.Top = 130; cancelBtn.Width = 100;
            cancelBtn.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            Controls.Add(label);
            Controls.Add(_pwdBox);
            Controls.Add(_errorLabel);
            Controls.Add(okBtn);
            Controls.Add(cancelBtn);

            AcceptButton = okBtn;
            CancelButton = cancelBtn;
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
                MessageBox.Show("Too many incorrect attempts. Closing day was cancelled.",
                    "Locked out", MessageBoxButtons.OK, MessageBoxIcon.Error);
                DialogResult = DialogResult.Cancel;
                Close();
                return;
            }

            _errorLabel.Text = $"Incorrect password. {remaining} attempt(s) remaining.";
            _pwdBox.SelectAll();
            _pwdBox.Focus();
        }
    }
}
