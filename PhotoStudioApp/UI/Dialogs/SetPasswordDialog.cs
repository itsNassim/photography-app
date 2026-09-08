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
            Text = hasExistingPassword ? "Change closing password" : "Set closing password";
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
                Controls.Add(new Label { Text = "Current password:", Left = 20, Top = y, AutoSize = true });
                _currentBox = new TextBox { Left = 20, Top = y + 22, Width = 340, UseSystemPasswordChar = true };
                Controls.Add(_currentBox);
                y += 60;
            }

            Controls.Add(new Label { Text = "New password:", Left = 20, Top = y, AutoSize = true });
            _newBox = new TextBox { Left = 20, Top = y + 22, Width = 340, UseSystemPasswordChar = true };
            Controls.Add(_newBox);
            y += 60;

            Controls.Add(new Label { Text = "Confirm new password:", Left = 20, Top = y, AutoSize = true });
            _confirmBox = new TextBox { Left = 20, Top = y + 22, Width = 340, UseSystemPasswordChar = true };
            Controls.Add(_confirmBox);
            y += 60;

            var okBtn = Theme.MakePrimaryButton("Save");
            okBtn.Left = 160; okBtn.Top = y; okBtn.Width = 100;
            okBtn.Click += OkBtn_Click;
            var cancelBtn = Theme.MakeSecondaryButton("Cancel");
            cancelBtn.Left = 260; cancelBtn.Top = y; cancelBtn.Width = 100;
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
                MessageBox.Show("Enter the current password.", "Missing info", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (string.IsNullOrEmpty(_newBox.Text) || _newBox.Text.Length < 4)
            {
                MessageBox.Show("New password must be at least 4 characters.", "Too short", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (_newBox.Text != _confirmBox.Text)
            {
                MessageBox.Show("Passwords do not match.", "Mismatch", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            CurrentPassword = _hasExisting ? _currentBox.Text : null;
            NewPassword = _newBox.Text;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
