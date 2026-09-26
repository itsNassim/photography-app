using System;
using System.Windows.Forms;

namespace PhotoStudioApp.UI.Dialogs
{
    public class SetPasswordDialog : Form
    {
        public string CurrentPassword { get; private set; }
        public string NewPassword { get; private set; }

        private readonly TextBox _currentBox;
        private readonly TextBox _newBox;
        private readonly TextBox _confirmBox;
        private readonly bool _hasExisting;

        public SetPasswordDialog(bool hasExistingPassword)
        {
            _hasExisting = hasExistingPassword;
            Text = hasExistingPassword ? "Changer le mot de passe administrateur" : "Definir le mot de passe administrateur";
            Width = 400;
            Height = hasExistingPassword ? 290 : 230;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            Font = Theme.FontBase;
            BackColor = Theme.CardBackground;

            int y = 16;
            if (hasExistingPassword)
            {
                Controls.Add(new Label { Text = "Mot de passe actuel :", Left = 20, Top = y, AutoSize = true });
                _currentBox = new TextBox { Left = 20, Top = y + 22, Width = 340, UseSystemPasswordChar = true };
                Controls.Add(_currentBox);
                y += 60;
            }

            Controls.Add(new Label { Text = "Nouveau mot de passe :", Left = 20, Top = y, AutoSize = true });
            _newBox = new TextBox { Left = 20, Top = y + 22, Width = 340, UseSystemPasswordChar = true };
            Controls.Add(_newBox);
            y += 60;

            Controls.Add(new Label { Text = "Confirmer le nouveau mot de passe :", Left = 20, Top = y, AutoSize = true });
            _confirmBox = new TextBox { Left = 20, Top = y + 22, Width = 340, UseSystemPasswordChar = true };
            Controls.Add(_confirmBox);
            y += 60;

            var okBtn = Theme.MakePrimaryButton("Enregistrer");
            okBtn.Left = 150; okBtn.Top = y; okBtn.Width = 110;
            okBtn.Click += OkBtn_Click;
            var cancelBtn = Theme.MakeSecondaryButton("Annuler");
            cancelBtn.Left = 270; cancelBtn.Top = y; cancelBtn.Width = 100;
            cancelBtn.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(okBtn);
            Controls.Add(cancelBtn);

            AcceptButton = okBtn;
            CancelButton = cancelBtn;
        }

        private void OkBtn_Click(object sender, EventArgs e)
        {
            if (_hasExisting && string.IsNullOrEmpty(_currentBox.Text))
            {
                MessageBox.Show("Entrez le mot de passe actuel.", "Information manquante", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (string.IsNullOrEmpty(_newBox.Text) || _newBox.Text.Length < 4)
            {
                MessageBox.Show("Le nouveau mot de passe doit contenir au moins 4 caracteres.", "Trop court", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (_newBox.Text != _confirmBox.Text)
            {
                MessageBox.Show("Les mots de passe ne correspondent pas.", "Non concordant", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            CurrentPassword = _hasExisting ? _currentBox.Text : null;
            NewPassword = _newBox.Text;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
