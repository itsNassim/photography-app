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
        private ListBox _prefixesList;
        private TextBox _newPrefixBox;
        private NumericUpDown _retentionDaysBox;

        /// <summary>Plain struct instead of a C# tuple - named-tuple syntax needs the
        /// ValueTuple types, which only became part of mscorlib in .NET Framework 4.7+; on
        /// 4.6 they're simply not there (and this project intentionally avoids adding the
        /// separate System.ValueTuple NuGet package just for this one spot). Field names match
        /// what the tuple's named elements were, so nothing below needed to change.</summary>
        private struct WeekdayOption
        {
            public string Fr;
            public DayOfWeek Value;
            public WeekdayOption(string fr, DayOfWeek value) { Fr = fr; Value = value; }
        }

        private static readonly WeekdayOption[] FrenchWeekdays =
        {
            new WeekdayOption("Lundi", DayOfWeek.Monday),
            new WeekdayOption("Mardi", DayOfWeek.Tuesday),
            new WeekdayOption("Mercredi", DayOfWeek.Wednesday),
            new WeekdayOption("Jeudi", DayOfWeek.Thursday),
            new WeekdayOption("Vendredi", DayOfWeek.Friday),
            new WeekdayOption("Samedi", DayOfWeek.Saturday),
            new WeekdayOption("Dimanche", DayOfWeek.Sunday),
        };

        public SettingsTab()
        {
            BackColor = Theme.Background;
            BuildLayout();
            RefreshAll();
        }

        private void BuildLayout()
        {
            // Scrolling fix: a TableLayoutPanel that is BOTH Dock=Fill AND AutoScroll=true can't
            // compute a scrollable content size correctly - Dock=Fill clamps it to the visible
            // client area, so content taller than the window gets clipped instead of scrolled
            // (this is what cut off the bottom of the Parametres page). The fix is to separate
            // the two roles: an outer Panel does the scrolling (Dock=Fill, AutoScroll=true), and
            // the actual content is a TableLayoutPanel that AutoSizes to its real full height and
            // docks to Top (so it only takes the width of the scroll panel, not a fixed fill).
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

            var title = new Label { Text = "Parametres", Font = Theme.FontHeading, AutoSize = true, Margin = new Padding(0, 0, 0, 14) };
            root.Controls.Add(title);

            var card = new TableLayoutPanel
            {
                ColumnCount = 3,
                AutoSize = true,
                BackColor = Theme.CardBackground,
                Padding = new Padding(20),
                Margin = new Padding(0)
            };
            card.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
            card.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 400));
            card.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            int row = 0;

            AddLabel(card, "Dossier racine du studio :", row);
            _studioPathBox = new TextBox { Width = 380, Font = Theme.FontBase };
            card.Controls.Add(_studioPathBox, 1, row);
            var browseStudio = Theme.MakeSecondaryButton("Parcourir...");
            browseStudio.Width = 100;
            browseStudio.Click += (s, e) => BrowseFolder(_studioPathBox);
            card.Controls.Add(browseStudio, 2, row);
            row++;

            AddLabel(card, "Dossier d'archive\n(contient les dossiers d'annees) :", row);
            _archivePathBox = new TextBox { Width = 380, Font = Theme.FontBase };
            card.Controls.Add(_archivePathBox, 1, row);
            var browseArchive = Theme.MakeSecondaryButton("Parcourir...");
            browseArchive.Width = 100;
            browseArchive.Click += (s, e) => BrowseFolder(_archivePathBox);
            card.Controls.Add(browseArchive, 2, row);
            row++;

            AddLabel(card, "Format du nom de dossier\n(jour de travail) :", row);
            _dateFormatBox = new TextBox { Width = 200, Font = Theme.FontBase, Text = "dd MMMM" };
            card.Controls.Add(_dateFormatBox, 1, row);
            card.Controls.Add(new Label { Text = "ex. dd MMMM -> 07 septembre", AutoSize = true, ForeColor = System.Drawing.Color.Gray, Margin = new Padding(3, 8, 0, 0) }, 2, row);
            row++;

            AddLabel(card, "Jours de fermeture\n(ignores pour le jour suivant) :", row);
            _closedDaysList = new CheckedListBox { Width = 200, Height = 150, Font = Theme.FontBase, CheckOnClick = true };
            foreach (var d in FrenchWeekdays)
                _closedDaysList.Items.Add(d.Fr);
            card.Controls.Add(_closedDaysList, 1, row);
            row++;

            AddLabel(card, "Prefixes de fichiers a supprimer\na la cloture (ex. \"re\") :", row);
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
                Text = "Tout fichier dont le nom commence par l'un\nde ces prefixes est supprime avant le\ncomptage, dans chaque dossier de categorie.",
                AutoSize = true, ForeColor = System.Drawing.Color.Gray, Margin = new Padding(3, 8, 0, 0)
            }, 2, row);
            row++;

            AddLabel(card, "Conservation des archives\n(0 = jamais nettoyer automatiquement) :", row);
            var retentionPanel = new FlowLayoutPanel { AutoSize = true };
            _retentionDaysBox = new NumericUpDown { Minimum = 0, Maximum = 3650, Width = 90, Font = Theme.FontBase };
            retentionPanel.Controls.Add(_retentionDaysBox);
            retentionPanel.Controls.Add(new Label { Text = "jours", AutoSize = true, Margin = new Padding(8, 8, 0, 0) });
            card.Controls.Add(retentionPanel, 1, row);
            card.Controls.Add(new Label
            {
                Text = "Utilise par le bouton \"Nettoyer les anciennes\narchives\" dans l'onglet Historique.",
                AutoSize = true, ForeColor = System.Drawing.Color.Gray, Margin = new Padding(3, 8, 0, 0)
            }, 2, row);
            row++;

            AddLabel(card, "Tentatives de mot de passe\nautorisees avant blocage :", row);
            _maxAttemptsBox = new NumericUpDown { Minimum = 1, Maximum = 20, Width = 80, Font = Theme.FontBase };
            card.Controls.Add(_maxAttemptsBox, 1, row);
            row++;

            AddLabel(card, "Mot de passe administrateur\n(cloture, Stock/Categories, Historique) :", row);
            var pwdPanel = new FlowLayoutPanel { AutoSize = true };
            _passwordStatusLabel = new Label { AutoSize = true, Margin = new Padding(0, 8, 14, 0) };
            var setPwdBtn = Theme.MakeSecondaryButton("Definir / Changer");
            setPwdBtn.Width = 170;
            setPwdBtn.Click += SetPwdBtn_Click;
            pwdPanel.Controls.Add(_passwordStatusLabel);
            pwdPanel.Controls.Add(setPwdBtn);
            card.Controls.Add(pwdPanel, 1, row);
            row++;

            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(card);

            var saveBtn = Theme.MakePrimaryButton("Enregistrer les parametres");
            saveBtn.Width = 220;
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

        private void AddPrefixBtn_Click(object sender, EventArgs e)
        {
            string prefix = _newPrefixBox.Text.Trim();
            if (string.IsNullOrEmpty(prefix))
                return;

            foreach (var existing in _prefixesList.Items)
            {
                if (string.Equals(existing.ToString(), prefix, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show("Ce prefixe est deja dans la liste.", "Deja present", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                var dow = FrenchWeekdays[i].Value;
                _closedDaysList.SetItemChecked(i, closed.Contains(dow));
            }

            _prefixesList.Items.Clear();
            foreach (var p in (s.PurgedFilePrefixesCsv ?? "").Split(',').Select(x => x.Trim()).Where(x => x.Length > 0))
                _prefixesList.Items.Add(p);

            _retentionDaysBox.Value = Math.Max(0, Math.Min(3650, s.ArchiveRetentionDays));

            _passwordStatusLabel.Text = string.IsNullOrEmpty(s.PasswordHash) ? "Non defini" : "Mot de passe defini";
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
                        MessageBox.Show("Le mot de passe actuel est incorrect.", "Non modifie", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    PasswordService.Hash(dlg.NewPassword, out string hash, out string salt);
                    current.PasswordHash = hash;
                    current.PasswordSalt = salt;
                    _repo.SaveAll(current);
                    RefreshAll();
                    MessageBox.Show("Mot de passe mis a jour.", "Termine", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void SaveBtn_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_studioPathBox.Text) || string.IsNullOrWhiteSpace(_archivePathBox.Text))
            {
                MessageBox.Show("Le dossier du studio et le dossier d'archive sont obligatoires.", "Information manquante", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(_dateFormatBox.Text))
            {
                MessageBox.Show("Le format du nom de dossier ne peut pas etre vide.", "Information manquante", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var s = _repo.LoadAll();
            s.StudioRootPath = _studioPathBox.Text.Trim();
            s.ArchiveRootPath = _archivePathBox.Text.Trim();
            s.DateFolderFormat = _dateFormatBox.Text.Trim();
            s.MaxPasswordAttempts = (int)_maxAttemptsBox.Value;

            var checkedDays = _closedDaysList.CheckedIndices.Cast<int>().Select(i => FrenchWeekdays[i].Value);
            s.ClosedWeekdaysCsv = WorkdayCalculator.ToCsv(checkedDays);

            s.PurgedFilePrefixesCsv = string.Join(",", _prefixesList.Items.Cast<object>().Select(o => o.ToString()));
            s.ArchiveRetentionDays = (int)_retentionDaysBox.Value;

            try
            {
                System.IO.Directory.CreateDirectory(s.StudioRootPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Impossible de creer/acceder au dossier du studio :\n" + ex.Message, "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            _repo.SaveAll(s);
            MessageBox.Show("Parametres enregistres.", "Termine", MessageBoxButtons.OK, MessageBoxIcon.Information);
            SettingsSaved?.Invoke(this, EventArgs.Empty);
        }
    }
}
