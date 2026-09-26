using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using PhotoStudioApp.Data;
using PhotoStudioApp.Models;
using PhotoStudioApp.Services;
using PhotoStudioApp.UI.Dialogs;

namespace PhotoStudioApp.UI
{
    /// <summary>Sub-tab of Historique/Archive: every file-level event in the studio's category
    /// folders (a photo entering a folder, being removed, renamed, purged, or archived at close
    /// time - see StudioActionLogService / HomeTab's FileSystemWatcher / FolderCountService).
    /// Filterable by period and action; the search field searches file names WITHIN whatever
    /// period/action filter is currently applied, not the whole unfiltered log. Sits behind the
    /// same lock as the rest of the Archive tab.</summary>
    public class StudioLogsTab : UserControl
    {
        private readonly SettingsRepository _settingsRepo = new SettingsRepository();
        private List<StudioActionLogEntry> _allEntries = new List<StudioActionLogEntry>();

        private DataGridView _grid;
        private ComboBox _periodCombo;
        private ComboBox _actionCombo;
        private TextBox _searchBox;
        private NumericUpDown _retentionInput;

        public StudioLogsTab()
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

            var title = new Label { Text = "Journal des actions studio (fichiers)", Font = Theme.FontHeading, AutoSize = true, Margin = new Padding(0, 0, 0, 10) };
            root.Controls.Add(title, 0, 0);

            // --- Filters + search ---
            var filterBar = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 6) };

            filterBar.Controls.Add(new Label { Text = "Période :", AutoSize = true, Margin = new Padding(0, 8, 4, 0) });
            _periodCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150, Margin = new Padding(0, 4, 16, 4) };
            _periodCombo.Items.AddRange(new object[] { "Tout", "Aujourd'hui", "7 derniers jours", "30 derniers jours", "90 derniers jours" });
            _periodCombo.SelectedIndex = 3;
            _periodCombo.SelectedIndexChanged += (s, e) => ApplyFilters();
            filterBar.Controls.Add(_periodCombo);

            filterBar.Controls.Add(new Label { Text = "Action :", AutoSize = true, Margin = new Padding(0, 8, 4, 0) });
            _actionCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220, Margin = new Padding(0, 4, 16, 4) };
            _actionCombo.SelectedIndexChanged += (s, e) => ApplyFilters();
            filterBar.Controls.Add(_actionCombo);

            filterBar.Controls.Add(new Label { Text = "Rechercher un fichier :", AutoSize = true, Margin = new Padding(0, 8, 4, 0) });
            _searchBox = new TextBox { Width = 200, Margin = new Padding(0, 4, 16, 4), Font = Theme.FontBase };
            _searchBox.TextChanged += (s, e) => ApplyFilters();
            filterBar.Controls.Add(_searchBox);

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

            var clearBtn = Theme.MakeDangerButton("Vider le journal des actions studio");
            clearBtn.Width = 250;
            clearBtn.Margin = new Padding(0, 2, 0, 4);
            clearBtn.Click += ClearBtn_Click;
            retentionBar.Controls.Add(clearBtn);
            root.Controls.Add(retentionBar, 0, 2);

            // --- Grid ---
            var gridPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.CardBackground };
            _grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true };
            Theme.StyleGrid(_grid);
            _grid.Columns.Add("When", "Date / Heure");
            _grid.Columns.Add("File", "Fichier");
            _grid.Columns.Add("Action", "Action");
            _grid.Columns.Add("Category", "Catégorie");
            _grid.Columns.Add("Details", "Détails");
            gridPanel.Controls.Add(_grid);
            root.Controls.Add(gridPanel, 0, 3);

            Controls.Add(root);
        }

        /// <summary>Reloads from the database (running the configured retention cleanup first),
        /// then re-applies whatever filters/search are currently selected. Called by HistoryTab
        /// when this sub-tab is (re)selected or the Archive tab as a whole refreshes.</summary>
        public void LoadData()
        {
            StudioActionLogService.CleanupOlderThanConfigured();
            _allEntries = StudioActionLogService.GetAll();

            var settings = _settingsRepo.LoadAll();
            _retentionInput.Value = Math.Max(0, Math.Min(3650, settings.StudioLogRetentionDays));

            string previouslySelected = _actionCombo.SelectedItem as string;
            _actionCombo.Items.Clear();
            _actionCombo.Items.Add("Toutes");
            foreach (var action in _allEntries.Select(x => x.Action).Distinct().OrderBy(x => x))
                _actionCombo.Items.Add(action);
            _actionCombo.SelectedItem = (previouslySelected != null && _actionCombo.Items.Contains(previouslySelected))
                ? previouslySelected : "Toutes";

            ApplyFilters();
        }

        private void ResetFilters()
        {
            _periodCombo.SelectedIndex = 3;
            _actionCombo.SelectedItem = "Toutes";
            _searchBox.Text = "";
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

            // Period + action filters are applied first ...
            var query = _allEntries.AsEnumerable();

            var cutoff = PeriodCutoff();
            if (cutoff.HasValue)
                query = query.Where(x => x.Timestamp >= cutoff.Value);

            string action = _actionCombo.SelectedItem as string;
            if (!string.IsNullOrEmpty(action) && action != "Toutes")
                query = query.Where(x => x.Action == action);

            // ... and only THEN does the file-name search run, on whatever that filtering left -
            // so a search while an action/period filter is active stays scoped to that filter,
            // as requested, instead of always searching the entire unfiltered log.
            string search = _searchBox.Text?.Trim();
            if (!string.IsNullOrEmpty(search))
                query = query.Where(x => x.FileName != null && x.FileName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);

            _grid.Rows.Clear();
            foreach (var e in query.OrderByDescending(x => x.Timestamp))
            {
                _grid.Rows.Add(e.Timestamp.ToString("g"), e.FileName, e.Action,
                    string.IsNullOrEmpty(e.CategoryName) ? "—" : e.CategoryName, e.Details);
            }
        }

        private void SaveRetentionBtn_Click(object sender, EventArgs e)
        {
            var settings = _settingsRepo.LoadAll();
            settings.StudioLogRetentionDays = (int)_retentionInput.Value;
            _settingsRepo.SaveAll(settings);

            int removed = StudioActionLogService.CleanupOlderThanConfigured();
            LoadData();

            string msg = settings.StudioLogRetentionDays <= 0
                ? "Durée de conservation enregistrée (illimitée - rien n'est supprimé automatiquement)."
                : removed > 0
                    ? $"Durée de conservation enregistrée. {removed} entrée(s) de plus de {settings.StudioLogRetentionDays} jour(s) ont été supprimées."
                    : "Durée de conservation enregistrée.";
            MessageBox.Show(msg, "Terminé", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ClearBtn_Click(object sender, EventArgs e)
        {
            if (!AuthGate.RequireAdminPassword(this, "Vider le journal des actions studio", "Vider le journal des actions studio")) return;

            if (MessageBox.Show(
                "Ceci va supprimer DÉFINITIVEMENT tout le journal des actions studio (tous les évènements de fichiers enregistrés, quelle que soit leur date).\n\nContinuer ?",
                "Attention - vider le journal", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            using (var confirm = new TypeToConfirmDialog(
                "Vous êtes sur le point de vider DÉFINITIVEMENT le journal des actions studio.", "SUPPRIMER"))
            {
                if (confirm.ShowDialog(this) != DialogResult.OK)
                    return;
            }

            StudioActionLogService.DeleteAll();
            StudioActionLogService.Record("(journal)", "Vider le journal des actions studio", null,
                "Journal vidé - historique précédent effacé");

            LoadData();
            MessageBox.Show("Le journal des actions studio a été vidé.", "Terminé", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
