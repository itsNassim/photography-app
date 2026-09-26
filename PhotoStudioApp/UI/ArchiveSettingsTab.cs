using System;
using System.Linq;
using System.Windows.Forms;
using PhotoStudioApp.Data;
using PhotoStudioApp.Services;
using PhotoStudioApp.UI.Dialogs;

namespace PhotoStudioApp.UI
{
    /// <summary>Sub-tab of Historique/Archive: admin-only settings that don't need their own
    /// per-field password check because simply reaching this tab already required the admin
    /// password (see HistoryTab's lock). Holds: the admin password itself, how many wrong
    /// attempts are allowed and how long a lockout lasts afterwards, how many days of archives/
    /// logs to keep, which filename prefixes get purged at close time, and the master on/off for
    /// the no-password "Archiver la journée" worker feature (moved here from the main Paramètres
    /// tab, which keeps only settings that don't need a password - studio/archive paths, date
    /// format, closed days, changing the work date, and the worker-archive button itself).</summary>
    public class ArchiveSettingsTab : UserControl
    {
        private readonly SettingsRepository _repo = new SettingsRepository();

        private Label _passwordStatusLabel;
        private NumericUpDown _maxAttemptsBox;
        private NumericUpDown _lockoutMinutesBox;
        private NumericUpDown _retentionDaysBox;
        private ListBox _prefixesList;
        private TextBox _newPrefixBox;
        private CheckBox _workerArchiveFeatureCheck;

        public ArchiveSettingsTab()
        {
            BackColor = Theme.Background;
            BuildLayout();
        }

        private void BuildLayout()
        {
            // Same scrolling-fix pattern as the main Paramètres tab - see its own comment for why
            // a Dock=Fill + AutoScroll panel is needed instead of a single AutoSize table.
            var scrollPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Theme.Background };

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 1,
                Padding = new Padding(16),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var title = new Label { Text = "Paramètres administrateur", Font = Theme.FontHeading, AutoSize = true, Margin = new Padding(0, 0, 0, 4) };
            root.Controls.Add(title);
            var subtitle = new Label
            {
                Text = "Protégés par le mot de passe administrateur (vous avez déjà dû le saisir pour ouvrir cet onglet).",
                AutoSize = true, ForeColor = System.Drawing.Color.Gray, Margin = new Padding(0, 0, 0, 14)
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(subtitle);

            var card = new TableLayoutPanel
            {
                ColumnCount = 3,
                AutoSize = true,
                BackColor = Theme.CardBackground,
                Padding = new Padding(20),
                Margin = new Padding(0)
            };
            card.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
            card.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 380));
            card.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            int row = 0;

            AddLabel(card, "Mot de passe administrateur\n(clôture, Stock/Catégories, Historique) :", row);
            var pwdPanel = new FlowLayoutPanel { AutoSize = true };
            _passwordStatusLabel = new Label { AutoSize = true, Margin = new Padding(0, 8, 14, 0) };
            var setPwdBtn = Theme.MakeSecondaryButton("Definir / Changer");
            setPwdBtn.Width = 170;
            setPwdBtn.Click += SetPwdBtn_Click;
            pwdPanel.Controls.Add(_passwordStatusLabel);
            pwdPanel.Controls.Add(setPwdBtn);
            card.Controls.Add(pwdPanel, 1, row);
            row++;

            AddLabel(card, "Tentatives de mot de passe\nautorisees avant blocage :", row);
            _maxAttemptsBox = new NumericUpDown { Minimum = 1, Maximum = 20, Width = 80, Font = Theme.FontBase };
            card.Controls.Add(_maxAttemptsBox, 1, row);
            row++;

            AddLabel(card, "Bloquer l'acces pendant\n(minutes) apres trop de tentatives :", row);
            var lockoutPanel = new FlowLayoutPanel { AutoSize = true };
            _lockoutMinutesBox = new NumericUpDown { Minimum = 1, Maximum = 1440, Width = 80, Font = Theme.FontBase };
            lockoutPanel.Controls.Add(_lockoutMinutesBox);
            lockoutPanel.Controls.Add(new Label { Text = "minute(s)", AutoSize = true, Margin = new Padding(8, 8, 0, 0) });
            card.Controls.Add(lockoutPanel, 1, row);
            row++;

            AddLabel(card, "Conservation des archives\n(0 = jamais nettoyer automatiquement) :", row);
            var retentionPanel = new FlowLayoutPanel { AutoSize = true };
            _retentionDaysBox = new NumericUpDown { Minimum = 0, Maximum = 3650, Width = 90, Font = Theme.FontBase };
            retentionPanel.Controls.Add(_retentionDaysBox);
            retentionPanel.Controls.Add(new Label { Text = "jours", AutoSize = true, Margin = new Padding(8, 8, 0, 0) });
            card.Controls.Add(retentionPanel, 1, row);
            card.Controls.Add(new Label
            {
                Text = "Utilise par le bouton \"Nettoyer les anciennes\narchives\" dans l'onglet Journées.",
                AutoSize = true, ForeColor = System.Drawing.Color.Gray, Margin = new Padding(3, 8, 0, 0)
            }, 2, row);
            row++;

            AddLabel(card, "Prefixes de fichiers a supprimer\na la clôture (ex. \"re\") :", row);
            var prefixPanel = new TableLayoutPanel { ColumnCount = 1, AutoSize = true };
            prefixPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            prefixPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _prefixesList = new ListBox { Width = 200, Height = 90, Font = Theme.FontBase };
            var prefixInputRow = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
            _newPrefixBox = new TextBox { Width = 110, Font = Theme.FontBase };
            var addPrefixBtn = Theme.MakeSecondaryButton("+ Ajouter");
            addPrefixBtn.Width = 90;
            addPrefixBtn.Click += AddPrefixBtn_Click;
            var removePrefixBtn = Theme.MakeDangerButton("Retirer");
            removePrefixBtn.Width = 80;
            removePrefixBtn.Click += RemovePrefixBtn_Click;
            prefixInputRow.Controls.Add(_newPrefixBox);
            prefixInputRow.Controls.Add(addPrefixBtn);
            prefixInputRow.Controls.Add(removePrefixBtn);
            prefixPanel.Controls.Add(_prefixesList, 0, 0);
            prefixPanel.Controls.Add(prefixInputRow, 0, 1);
            card.Controls.Add(prefixPanel, 1, row);
            card.Controls.Add(new Label
            {
                Text = "Tout fichier dont le nom commence par l'un\nde ces prefixes est envoye a la corbeille\navant le comptage, dans chaque dossier de\ncatégorie.",
                AutoSize = true, ForeColor = System.Drawing.Color.Gray, Margin = new Padding(3, 8, 0, 0)
            }, 2, row);
            row++;

            AddLabel(card, "Archivage sans mot de passe\npour les employés :", row);
            var workerArchivePanel = new TableLayoutPanel { AutoSize = true, ColumnCount = 1 };
            _workerArchiveFeatureCheck = new CheckBox
            {
                Text = "Autoriser l'archivage journalier sans revenu pour les employés\n(le bouton apparait ensuite dans l'onglet Paramètres principal)",
                AutoSize = true,
                Margin = new Padding(0, 6, 0, 0)
            };
            _workerArchiveFeatureCheck.Click += WorkerArchiveFeatureCheck_Click;
            workerArchivePanel.Controls.Add(_workerArchiveFeatureCheck);
            card.Controls.Add(workerArchivePanel, 1, row);
            row++;

            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(card);

            var saveBtn = Theme.MakePrimaryButton("Enregistrer");
            saveBtn.Width = 160;
            saveBtn.Margin = new Padding(0, 16, 0, 24);
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            saveBtn.Click += SaveBtn_Click;
            root.Controls.Add(saveBtn);

            scrollPanel.Controls.Add(root);
            Controls.Add(scrollPanel);
        }

        private void AddLabel(TableLayoutPanel panel, string text, int row)
        {
            panel.Controls.Add(new Label { Text = text, AutoSize = true, Font = Theme.FontSubheading, Margin = new Padding(0, 8, 10, 0) }, 0, row);
        }

        /// <summary>Reloads every field from the database. Called by HistoryTab when this sub-tab
        /// is (re)selected or the Archive tab as a whole refreshes.</summary>
        public void LoadData()
        {
            var s = _repo.LoadAll();

            _passwordStatusLabel.Text = string.IsNullOrEmpty(s.PasswordHash) ? "Non defini" : "Mot de passe defini";
            _passwordStatusLabel.ForeColor = string.IsNullOrEmpty(s.PasswordHash) ? Theme.Warning : Theme.Success;

            _maxAttemptsBox.Value = s.MaxPasswordAttempts;
            _lockoutMinutesBox.Value = Math.Max(1, Math.Min(1440, s.PasswordLockoutMinutes));
            _retentionDaysBox.Value = Math.Max(0, Math.Min(3650, s.ArchiveRetentionDays));

            _prefixesList.Items.Clear();
            foreach (var p in (s.PurgedFilePrefixesCsv ?? "").Split(',').Select(x => x.Trim()).Where(x => x.Length > 0))
                _prefixesList.Items.Add(p);

            _workerArchiveFeatureCheck.Checked = s.WorkerArchiveFeatureEnabled;
        }

        private void AddPrefixBtn_Click(object sender, EventArgs e)
        {
            string prefix = _newPrefixBox.Text.Trim();
            if (string.IsNullOrEmpty(prefix))
                return;

            foreach (var existing in _prefixesList.Items)
            {
                if (string.Equals(existing.ToString(), prefix, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show("Ce prefixe est déjà dans la liste.", "Deja present", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
            }

            _prefixesList.Items.Add(prefix);
            _newPrefixBox.Clear();
            _newPrefixBox.Focus();
        }

        private void RemovePrefixBtn_Click(object sender, EventArgs e)
        {
            if (_prefixesList.SelectedIndex >= 0)
                _prefixesList.Items.RemoveAt(_prefixesList.SelectedIndex);
        }

        private void SetPwdBtn_Click(object sender, EventArgs e)
        {
            var current = _repo.LoadAll();
            using (var dlg = new SetPasswordDialog(hasExistingPassword: !string.IsNullOrEmpty(current.PasswordHash)))
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    bool hadExistingPassword = !string.IsNullOrEmpty(current.PasswordHash);
                    if (hadExistingPassword &&
                        !PasswordService.Verify(dlg.CurrentPassword, current.PasswordHash, current.PasswordSalt))
                    {
                        LoginLogService.Record("Changer le mot de passe administrateur", "Definir / Changer", 1, false, "Mot de passe actuel incorrect");
                        MessageBox.Show("Le mot de passe actuel est incorrect.", "Non modifie", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    if (hadExistingPassword)
                        LoginLogService.Record("Changer le mot de passe administrateur", "Definir / Changer", 0, true);

                    PasswordService.Hash(dlg.NewPassword, out string hash, out string salt);
                    current.PasswordHash = hash;
                    current.PasswordSalt = salt;
                    _repo.SaveAll(current);
                    LoadData();
                    MessageBox.Show("Mot de passe mis a jour.", "Termine", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        /// <summary>Master on/off for the whole "Archiver la journée (sans revenu)" worker
        /// feature - password-protected in both directions (an extra AuthGate check on top of
        /// this tab's own lock, since it's a security-relevant toggle) since it controls whether
        /// a no-password-needed close-day button becomes available at all in the main Paramètres
        /// tab.</summary>
        private void WorkerArchiveFeatureCheck_Click(object sender, EventArgs e)
        {
            bool wantsEnabled = _workerArchiveFeatureCheck.Checked;
            if (!AuthGate.RequireAdminPassword(this, "Activer / désactiver l'archivage sans mot de passe", "Case a cocher - archivage employé"))
            {
                _workerArchiveFeatureCheck.Checked = !wantsEnabled;
                return;
            }

            var settings = _repo.LoadAll();
            settings.WorkerArchiveFeatureEnabled = wantsEnabled;
            if (!wantsEnabled)
                settings.WorkerArchiveArmed = false; // never leave it armed once the feature itself is off
            _repo.SaveAll(settings);
        }

        private void SaveBtn_Click(object sender, EventArgs e)
        {
            var s = _repo.LoadAll();
            s.MaxPasswordAttempts = (int)_maxAttemptsBox.Value;
            s.PasswordLockoutMinutes = (int)_lockoutMinutesBox.Value;
            s.ArchiveRetentionDays = (int)_retentionDaysBox.Value;
            s.PurgedFilePrefixesCsv = string.Join(",", _prefixesList.Items.Cast<object>().Select(o => o.ToString()));
            _repo.SaveAll(s);
            MessageBox.Show("Paramètres administrateur enregistres.", "Termine", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
