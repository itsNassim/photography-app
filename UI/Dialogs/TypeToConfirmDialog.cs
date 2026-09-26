using System;
using System.Windows.Forms;

namespace PhotoStudioApp.UI.Dialogs
{
    /// <summary>Second layer of confirmation for very destructive actions: after an initial
    /// Yes/No warning, this makes the user type an exact phrase before proceeding - much harder
    /// to trigger by an accidental double-click than a second Yes/No dialog would be.</summary>
    public class TypeToConfirmDialog : Form
    {
        private readonly string _requiredPhrase;
        private readonly TextBox _inputBox;

        public TypeToConfirmDialog(string message, string requiredPhrase)
        {
            _requiredPhrase = requiredPhrase;
            Text = "Confirmation requise";
            Width = 460;
            Height = 240;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            Font = Theme.FontBase;
            BackColor = Theme.CardBackground;

            var label = new Label
            {
                Text = message + $"\n\nPour confirmer, tapez exactement : {requiredPhrase}",
                Left = 20, Top = 16, Width = 400, AutoSize = true
            };
            _inputBox = new TextBox { Left = 20, Top = 100, Width = 400, Font = Theme.FontBase };

            var okBtn = Theme.MakeDangerButton("Confirmer la suppression");
            okBtn.Left = 190; okBtn.Top = 140; okBtn.Width = 230;
            okBtn.Click += OkBtn_Click;
            var cancelBtn = Theme.MakeSecondaryButton("Annuler");
            cancelBtn.Left = 20; cancelBtn.Top = 140; cancelBtn.Width = 100;
            cancelBtn.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            Controls.Add(label);
            Controls.Add(_inputBox);
            Controls.Add(okBtn);
            Controls.Add(cancelBtn);

            AcceptButton = okBtn;
            CancelButton = cancelBtn;
        }

        private void OkBtn_Click(object sender, EventArgs e)
        {
            if (!string.Equals(_inputBox.Text.Trim(), _requiredPhrase, StringComparison.Ordinal))
            {
                MessageBox.Show("Le texte saisi ne correspond pas exactement. Rien n'a ete supprime.",
                    "Texte incorrect", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
