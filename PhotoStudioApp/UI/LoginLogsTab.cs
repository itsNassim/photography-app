using System;
using System.Linq;
using System.Windows.Forms;
using PhotoStudioApp.Data;
using PhotoStudioApp.Models;
using PhotoStudioApp.Services;
using PhotoStudioApp.UI.Dialogs;

namespace PhotoStudioApp.UI
{
    /// <summary>Sub-tab of Historique/Archive: every admin-password prompt shown anywhere in the
    /// app (see PasswordPromptDialog/LoginLogService) - when, what action it was protecting,
    /// which button triggered it, how many wrong attempts were made, and the outcome. Filterable
    /// by period/action/result/minimum attempts. Sits behind the same lock as the rest of the
    /// Archive tab - HistoryTab only ever shows this sub-tab's content once already unlocked.</summary>
    public class LoginLogsTab : UserControl
    {
        private readonly SettingsRepository _settingsRepo = new SettingsRepository();
        private System.Collections.Generic.List<LoginLogEntry> _allEntries = new System.Collections.Generic.List<LoginLogEntry>();

        private DataGridView _grid;
        private ComboBox _periodCombo;
        private ComboBox _actionCombo;
        private ComboBox _resultCombo;
        private NumericUpDown _minAttemptsInput;
        private NumericUpDown _retentionInput;

        public LoginLogsTab()
        {
            BackColor = Theme.Background;
            BuildLayout();
        }

        private void BuildLayout()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1, Padding = new Padding(16) };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var title = new Label { Text = "Journal de connexion (tentatives de mot de passe)", Font = Theme.FontHeading, AutoSize = true, Margin = new Padding(0, 0, 0, 10) };
            root.Controls.Add(title, 0, 0);

            // --- Filters ---
            var filterBar = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 6) };

            filterBar.Controls.Add(new Label { Text = "Période :", AutoSize = true, Margin = new Padding(0, 8, 4, 0) });
            _periodCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150, Margin = new Padding(0, 4, 16, 4) };
            _periodCombo.Items.AddRange(new object[] { "Tout", "Aujourd'hui", "7 derniers jours", "30 derniers jours", "90 derniers jours" });
            _periodCombo.SelectedIndex = 3;
            _periodCombo.SelectedIndexChanged += (s, e) => ApplyFilters();
            filterBar.Controls.Add(_periodCombo);

            filterBar.Controls.Add(new Label { Text = "Action :", AutoSize = true, Margin = new Padding(0, 8, 4, 0) });
            _actionCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260, Margin = new Padding(0, 4, 16, 4) };
            _actionCombo.SelectedIndexChanged += (s, e) => ApplyFilters();
            filterBar.Controls.Add(_actionCombo);

            filterBar.Controls.Add(new Label { Text = "Résultat :", AutoSize = true, Margin = new Padding(0, 8, 4, 0) });
            _resultCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110, Margin = new Padding(0, 4, 16, 4) };
            _resultCombo.Items.AddRange(new object[] { "Tous", "Réussi", "Échoué" });
            _resultCombo.SelectedIndex = 0;
            _resultCombo.SelectedIndexChanged += (s, e) => ApplyFilters();
            filterBar.Controls.Add(_resultCombo);

            filterBar.Controls.Add(new Label { Text = "Tentatives min. :", AutoSize = true, Margin = new Padding(0, 8, 4, 0) });
            _minAttemptsInput = new NumericUpDown { Minimum = 0, Maximum = 50, Width = 60, Margin = new Padding(0, 4, 16, 4) };
            _minAttemptsInput.ValueChanged += (s, e) => ApplyFilters();
            filterBar.Controls.Add(_minAttemptsInput);

            var resetBtn = Theme.MakeSecondaryButton("Réinitialiser les filtres");
            resetBtn.Width = 190;
            resetBtn.Margin = new Padding(0, 2, 0, 4);
            resetBtn.Click += (s, e) => ResetFilters();
            filterBar.Controls.Add(resetBtn);

            root.Controls.Add(filterBar, 0, 1);

            // --- Retention + clear ---
            var retentionBar = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 8) };
            retentionBar.Controls.Add(new Label { Text = "Conserver ce journal pendant (jours, 0 = illimité) :", AutoSize = true, Margin = new Padding(0, 8, 4, 0) });
            _retentionInput = new NumericUpDown { Minimum = 0, Maximum = 3650, Width = 70, Margin = new Padding(0, 4, 8, 4) };
            retentionBar.Controls.Add(_retentionInput);
            var saveRetentionBtn = Theme.MakeSecondaryButton("Enregistrer");
            saveRetentionBtn.Width = 120;
            saveRetentionBtn.Margin = new Padding(0, 2, 24, 4);
            saveRetentionBtn.Click += SaveRetentionBtn_Click;
            retentionBar.Controls.Add(saveRetentionBtn);

            var clearBtn = Theme.MakeDangerButton("Vider le journal de connexion");
            clearBtn.Width = 220;
            clearBtn.Margin = new Padding(0, 2, 0, 4);
            clearBtn.Click += ClearBtn_Click;
            retentionBar.Controls.Add(clearBtn);
            root.Controls.Add(retentionBar, 0, 2);

            // --- Grid ---
            var gridPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.CardBackground };
            _grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true };
            Theme.StyleGrid(_grid);
            _grid.Columns.Add("When", "Date / Heure");
            _grid.Columns.Add("Action", "Action");
            _grid.Columns.Add("Button", "Bouton");
            _grid.Columns.Add("Attempts", "Tentatives");
            _grid.Columns.Add("Result", "Résultat");
            _grid.Columns.Add("Details", "Détails");
            gridPanel.Controls.Add(_grid);
            root.Controls.Add(gridPanel, 0, 3);

            Controls.Add(root);
        }

        /// <summary>Reloads from the database (running the configured retention cleanup first),
        /// then re-applies whatever filters are currently selected. Called by HistoryTab when
        /// this sub-tab is (re)selected or the Archive tab as a whole refreshes.</summary>
        public void LoadData()
        {
            LoginLogService.CleanupOlderThanConfigured();
            _allEntries = LoginLogService.GetAll();

            var settings = _settingsRepo.LoadAll();
            _retentionInput.Value = Math.Max(0, Math.Min(3650, settings.LoginLogRetentionDays));

            string previouslySelected = _actionCombo.SelectedItem as string;
            _actionCombo.Items.Clear();
            _actionCombo.Items.Add("Toutes");
            foreach (var action in _allEntries.Select(x => x.ActionName).Distinct().OrderBy(x => x))
                _actionCombo.Items.Add(action);
            _actionCombo.SelectedItem = (previouslySelected != null && _actionCombo.Items.Contains(previouslySelected))
                ? previouslySelected : "Toutes";

            ApplyFilters();
        }

        private void ResetFilters()
        {
            _periodCombo.SelectedIndex = 3;
            _actionCombo.SelectedItem = "Toutes";
            _resultCombo.SelectedIndex = 0;
            _minAttemptsInput.Value = 0;
            ApplyFilters();
        }

        private DateTime? PeriodCutoff()
        {
            switch (_periodCombo.SelectedIndex)
            {
                case 1: return DateTime.Today;
                case 2: return DateTime.Now.AddDays(-7);
                case 3: return DateTime.Now.AddDays(-30);
                case 4: return DateTime.Now.AddDays(-90);
                default: return null; // "Tout"
            }
        }

        private void ApplyFilters()
        {
            if (_grid == null) return;

            var query = _allEntries.AsEnumerable();

            var cutoff = PeriodCutoff();
            if (cutoff.HasValue)
                query = query.Where(x => x.Timestamp >= cutoff.Value);

            string action = _actionCombo.SelectedItem as string;
            if (!string.IsNullOrEmpty(action) && action != "Toutes")
                query = query.Where(x => x.ActionName == action);

            if (_resultCombo.SelectedIndex == 1) query = query.Where(x => x.Success);
            else if (_resultCombo.SelectedIndex == 2) query = query.Where(x => !x.Success);

            int minAttempts = (int)_minAttemptsInput.Value;
            if (minAttempts > 0)
                query = query.Where(x => x.AttemptsMade >= minAttempts);

            _grid.Rows.Clear();
            foreach (var e in query.OrderByDescending(x => x.Timestamp))
            {
                _grid.Rows.Add(e.Timestamp.ToString("g"), e.ActionName, e.ButtonLabel,
                    e.AttemptsMade, e.Success ? "Réussi" : "Échoué", e.Details);
            }
        }

        private void SaveRetentionBtn_Click(object sender, EventArgs e)
        {
            var settings = _settingsRepo.LoadAll();
            settings.LoginLogRetentionDays = (int)_retentionInput.Value;
            _settingsRepo.SaveAll(settings);

            int removed = LoginLogService.CleanupOlderThanConfigured();
            LoadData();

            string msg = settings.LoginLogRetentionDays <= 0
                ? "Durée de conservation enregistrée (illimitée - rien n'est supprimé automatiquement)."
                : removed > 0
                    ? $"Durée de conservation enregistrée. {removed} entrée(s) de plus de {settings.LoginLogRetentionDays} jour(s) ont été supprimées."
                    : "Durée de conservation enregistrée.";
            MessageBox.Show(msg, "Terminé", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ClearBtn_Click(object sender, EventArgs e)
        {
            if (!AuthGate.RequireAdminPassword(this, "Vider le journal de connexion", "Vider le journal de connexion")) return;

            if (MessageBox.Show(
                "Ceci va supprimer DÉFINITIVEMENT tout le journal de connexion (toutes les tentatives de mot de passe enregistrées, quelle que soit leur date).\n\nContinuer ?",
                "Attention - vider le journal", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            using (var confirm = new TypeToConfirmDialog(
                "Vous êtes sur le point de vider DÉFINITIVEMENT le journal de connexion.", "SUPPRIMER"))
            {
                if (confirm.ShowDialog(this) != DialogResult.OK)
                    return;
            }

            LoginLogService.DeleteAll();
            // Leaves a single fresh entry marking the reset, rather than a totally silent log -
            // the clearing action itself is still admin-password-protected and double-confirmed
            // above, so this is just a clean starting point, not a gap in the audit trail.
            LoginLogService.Record("Vider le journal de connexion", "Vider le journal de connexion", 0, true,
                "Journal vidé - historique précédent effacé");

            LoadData();
            MessageBox.Show("Le journal de connexion a été vidé.", "Terminé", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
