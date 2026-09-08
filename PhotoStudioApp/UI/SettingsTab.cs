using System;
using System.Linq;
using System.Windows.Forms;
using PhotoStudioApp.Data;
using PhotoStudioApp.Models;
using PhotoStudioApp.Services;
using PhotoStudioApp.UI.Dialogs;

namespace PhotoStudioApp.UI
{
    public class SettingsTab : UserControl
    {
        public event EventHandler SettingsSaved;

        private readonly SettingsRepository _repo = new SettingsRepository();

        private TextBox _studioPathBox;
        private TextBox _archivePathBox;
        private TextBox _dateFormatBox;
        private CheckedListBox _closedDaysList;
        private NumericUpDown _maxAttemptsBox;
        private Label _passwordStatusLabel;

        public SettingsTab()
        {
            BackColor = Theme.Background;
            BuildLayout();
            RefreshAll();
        }

        private void BuildLayout()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(16), AutoScroll = true };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var title = new Label { Text = "Settings", Font = Theme.FontHeading, AutoSize = true, Margin = new Padding(0, 0, 0, 14) };
            root.Controls.Add(title);

            var card = new TableLayoutPanel
            {
                ColumnCount = 3,
                AutoSize = true,
                BackColor = Theme.CardBackground,
                Padding = new Padding(20),
                Margin = new Padding(0)
            };
            card.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            card.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 420));
            card.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            int row = 0;

            AddLabel(card, "Studio root folder:", row);
            _studioPathBox = new TextBox { Width = 400, Font = Theme.FontBase };
            card.Controls.Add(_studioPathBox, 1, row);
            var browseStudio = Theme.MakeSecondaryButton("Browse...");
            browseStudio.Width = 90;
            browseStudio.Click += (s, e) => BrowseFolder(_studioPathBox);
            card.Controls.Add(browseStudio, 2, row);
            row++;

            AddLabel(card, "Archive/destination folder:", row);
            _archivePathBox = new TextBox { Width = 400, Font = Theme.FontBase };
            card.Controls.Add(_archivePathBox, 1, row);
            var browseArchive = Theme.MakeSecondaryButton("Browse...");
            browseArchive.Width = 90;
            browseArchive.Click += (s, e) => BrowseFolder(_archivePathBox);
            card.Controls.Add(browseArchive, 2, row);
            row++;

            AddLabel(card, "Date folder format:", row);
            _dateFormatBox = new TextBox { Width = 200, Font = Theme.FontBase, Text = "dd-MMMM" };
            card.Controls.Add(_dateFormatBox, 1, row);
            card.Controls.Add(new Label { Text = "e.g. dd-MMMM -> 07-September, or yyyy-MM-dd -> 2026-09-07", AutoSize = true, ForeColor = System.Drawing.Color.Gray, Margin = new Padding(3, 8, 0, 0) }, 2, row);
            row++;

            AddLabel(card, "Closed weekdays\n(skip when computing next day):", row);
            _closedDaysList = new CheckedListBox { Width = 200, Height = 140, Font = Theme.FontBase, CheckOnClick = true };
            foreach (var d in AppSettings.AllWeekdayNames)
                _closedDaysList.Items.Add(d);
            card.Controls.Add(_closedDaysList, 1, row);
            row++;

            AddLabel(card, "Max wrong-password attempts\nbefore lockout:", row);
            _maxAttemptsBox = new NumericUpDown { Minimum = 1, Maximum = 20, Width = 80, Font = Theme.FontBase };
            card.Controls.Add(_maxAttemptsBox, 1, row);
            row++;

            AddLabel(card, "Closing password:", row);
            var pwdPanel = new FlowLayoutPanel { AutoSize = true };
            _passwordStatusLabel = new Label { AutoSize = true, Margin = new Padding(0, 8, 14, 0) };
            var setPwdBtn = Theme.MakeSecondaryButton("Set / Change password");
            setPwdBtn.Width = 190;
            setPwdBtn.Click += SetPwdBtn_Click;
            pwdPanel.Controls.Add(_passwordStatusLabel);
            pwdPanel.Controls.Add(setPwdBtn);
            card.Controls.Add(pwdPanel, 1, row);
            row++;

            root.Controls.Add(card);

            var saveBtn = Theme.MakePrimaryButton("Save settings");
            saveBtn.Width = 160;
            saveBtn.Margin = new Padding(0, 16, 0, 0);
            saveBtn.Click += SaveBtn_Click;
            root.Controls.Add(saveBtn);

            Controls.Add(root);
        }

        private void AddLabel(TableLayoutPanel panel, string text, int row)
        {
            panel.Controls.Add(new Label { Text = text, AutoSize = true, Font = Theme.FontSubheading, Margin = new Padding(0, 8, 10, 0) }, 0, row);
        }

        private void BrowseFolder(TextBox target)
        {
            using (var dlg = new FolderBrowserDialog())
            {
                if (!string.IsNullOrWhiteSpace(target.Text) && System.IO.Directory.Exists(target.Text))
                    dlg.SelectedPath = target.Text;
                if (dlg.ShowDialog() == DialogResult.OK)
                    target.Text = dlg.SelectedPath;
            }
        }

        public void RefreshAll()
        {
            var s = _repo.LoadAll();
            _studioPathBox.Text = s.StudioRootPath;
            _archivePathBox.Text = s.ArchiveRootPath;
            _dateFormatBox.Text = s.DateFolderFormat;
            _maxAttemptsBox.Value = s.MaxPasswordAttempts;

            var closed = WorkdayCalculator.ParseClosedWeekdays(s.ClosedWeekdaysCsv);
            for (int i = 0; i < _closedDaysList.Items.Count; i++)
            {
                var dow = (DayOfWeek)Enum.Parse(typeof(DayOfWeek), (string)_closedDaysList.Items[i]);
                _closedDaysList.SetItemChecked(i, closed.Contains(dow));
            }

            _passwordStatusLabel.Text = string.IsNullOrEmpty(s.PasswordHash) ? "Not set" : "Password is set";
            _passwordStatusLabel.ForeColor = string.IsNullOrEmpty(s.PasswordHash) ? Theme.Warning : Theme.Success;
        }

        private void SetPwdBtn_Click(object sender, EventArgs e)
        {
            var current = _repo.LoadAll();
            using (var dlg = new SetPasswordDialog(hasExistingPassword: !string.IsNullOrEmpty(current.PasswordHash)))
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    if (!string.IsNullOrEmpty(current.PasswordHash) &&
                        !PasswordService.Verify(dlg.CurrentPassword, current.PasswordHash, current.PasswordSalt))
                    {
                        MessageBox.Show("Current password is incorrect.", "Not changed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    PasswordService.Hash(dlg.NewPassword, out string hash, out string salt);
                    current.PasswordHash = hash;
                    current.PasswordSalt = salt;
                    _repo.SaveAll(current);
                    RefreshAll();
                    MessageBox.Show("Password updated.", "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void SaveBtn_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_studioPathBox.Text) || string.IsNullOrWhiteSpace(_archivePathBox.Text))
            {
                MessageBox.Show("Studio root and archive folders are required.", "Missing info", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(_dateFormatBox.Text))
            {
                MessageBox.Show("Date folder format cannot be empty.", "Missing info", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var s = _repo.LoadAll();
            s.StudioRootPath = _studioPathBox.Text.Trim();
            s.ArchiveRootPath = _archivePathBox.Text.Trim();
            s.DateFolderFormat = _dateFormatBox.Text.Trim();
            s.MaxPasswordAttempts = (int)_maxAttemptsBox.Value;

            var checkedDays = _closedDaysList.CheckedItems.Cast<string>()
                .Select(n => (DayOfWeek)Enum.Parse(typeof(DayOfWeek), n));
            s.ClosedWeekdaysCsv = WorkdayCalculator.ToCsv(checkedDays);

            try
            {
                System.IO.Directory.CreateDirectory(s.StudioRootPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not create/access the studio root folder:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            _repo.SaveAll(s);
            MessageBox.Show("Settings saved.", "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
            SettingsSaved?.Invoke(this, EventArgs.Empty);
        }
    }
}
