using System;
using System.Windows.Forms;
using PhotoStudioApp.Data;
using PhotoStudioApp.Models;
using PhotoStudioApp.Services;
using PhotoStudioApp.UI.Dialogs;

namespace PhotoStudioApp.UI
{
    public class HistoryTab : UserControl
    {
        private readonly DailyCloseRepository _repo = new DailyCloseRepository();
        private readonly ArchiveCleanupService _cleanupService = new ArchiveCleanupService();
        private readonly SettingsRepository _settingsRepo = new SettingsRepository();
        private DataGridView _grid;
        private DataGridView _monthGrid;
        private Panel _lockedPanel;
        private Panel _contentPanel;
        private bool _unlocked;

        public HistoryTab()
        {
            BackColor = Theme.Background;
            BuildLayout();
            ShowLocked();
        }

        private void BuildLayout()
        {
            // --- Vue verrouillee ---
            _lockedPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background };
            var lockLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            lockLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            lockLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            lockLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var centerPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, Anchor = AnchorStyles.None, AutoSize = true };
            var lockIcon = new PictureBox
            {
                Image = IconGlyphs.Lock(28, Theme.Primary),
                SizeMode = PictureBoxSizeMode.AutoSize,
                Margin = new Padding(0, 0, 0, 6),
                Anchor = AnchorStyles.None
            };
            var lockLabel = new Label
            {
                Text = "L'historique est protégé par mot de passe.",
                Font = Theme.FontSubheading,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 12),
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter
            };
            var unlockBtn = Theme.MakePrimaryButton("Déverrouiller");
            unlockBtn.Width = 160;
            unlockBtn.Click += (s, e) => TryUnlock();
            centerPanel.Controls.Add(lockIcon);
            centerPanel.Controls.Add(lockLabel);
            centerPanel.Controls.Add(unlockBtn);

            lockLayout.Controls.Add(new Panel(), 0, 0);
            lockLayout.Controls.Add(centerPanel, 0, 1);
            lockLayout.Controls.Add(new Panel(), 0, 2);
            _lockedPanel.Controls.Add(lockLayout);

            // --- Vue déverrouillée (historique / archive) ---
            _contentPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background };

            var innerTabs = new TabControl { Dock = DockStyle.Fill, Font = Theme.FontBase };
            var daysPage = new TabPage("Journées") { BackColor = Theme.Background };
            var monthPage = new TabPage("Résumé mensuel") { BackColor = Theme.Background };
            innerTabs.TabPages.Add(daysPage);
            innerTabs.TabPages.Add(monthPage);
            innerTabs.SelectedIndexChanged += (s, e) => { if (innerTabs.SelectedTab == monthPage) LoadMonthSummaries(); };

            // --- Sub-tab: Journées (list of closed days) ---
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(16) };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var title = new Label { Text = "Historique des journées clôturées", Font = Theme.FontHeading, AutoSize = true, Margin = new Padding(0, 0, 0, 10) };
            root.Controls.Add(title, 0, 0);

            var toolbar = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 8) };
            var viewBtn = Theme.MakeSecondaryButton("Voir / Reimprimer");
            viewBtn.Width = 160;
            viewBtn.Click += ViewBtn_Click;
            var refreshBtn = Theme.MakeSecondaryButton("Actualiser");
            refreshBtn.Image = IconGlyphs.Refresh(16);
            refreshBtn.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            refreshBtn.TextImageRelation = TextImageRelation.ImageBeforeText;
            refreshBtn.Width = 130;
            refreshBtn.Click += (s, e) => LoadHistory();
            var deleteBtn = Theme.MakeDangerButton("Supprimer");
            deleteBtn.Width = 110;
            deleteBtn.Click += DeleteBtn_Click;
            var cleanupBtn = Theme.MakeSecondaryButton("Nettoyer les anciennes archives");
            cleanupBtn.Width = 260;
            cleanupBtn.Click += CleanupBtn_Click;
            var keepCurrentMonthBtn = Theme.MakeSecondaryButton("Garder ce mois, supprimer le reste");
            keepCurrentMonthBtn.Width = 280;
            keepCurrentMonthBtn.Click += KeepCurrentMonthBtn_Click;
            var clearAllBtn = Theme.MakeDangerButton("Tout supprimer");
            clearAllBtn.Width = 140;
            clearAllBtn.Click += ClearAllBtn_Click;
            toolbar.Controls.Add(viewBtn);
            toolbar.Controls.Add(refreshBtn);
            toolbar.Controls.Add(deleteBtn);
            toolbar.Controls.Add(cleanupBtn);
            toolbar.Controls.Add(keepCurrentMonthBtn);
            toolbar.Controls.Add(clearAllBtn);
            root.Controls.Add(toolbar, 0, 1);

            var gridPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.CardBackground };
            _grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true };
            Theme.StyleGrid(_grid);
            _grid.Columns.Add("Date", "Date de clôture");
            _grid.Columns.Add("ClosedAt", "Clôturée le");
            _grid.Columns.Add("Total", "Revenu");
            _grid.Columns.Add("Expenses", "Dépenses");
            _grid.Columns.Add("Net", "Net");
            _grid.Columns.Add("Archive", "Dossier d'archive");
            _grid.CellDoubleClick += (s, e) => OpenSelected();
            gridPanel.Controls.Add(_grid);
            root.Controls.Add(gridPanel, 0, 2);

            daysPage.Controls.Add(root);

            // --- Sub-tab: Résumé mensuel (month summary) ---
            var monthRoot = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(16) };
            monthRoot.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            monthRoot.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            monthRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var monthTitle = new Label { Text = "Résumé des revenus par mois", Font = Theme.FontHeading, AutoSize = true, Margin = new Padding(0, 0, 0, 10) };
            monthRoot.Controls.Add(monthTitle, 0, 0);

            var monthToolbar = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 8) };
            var monthViewBtn = Theme.MakeSecondaryButton("Voir / Imprimer le mois");
            monthViewBtn.Width = 200;
            monthViewBtn.Click += MonthViewBtn_Click;
            var monthRefreshBtn = Theme.MakeSecondaryButton("Actualiser");
            monthRefreshBtn.Image = IconGlyphs.Refresh(16);
            monthRefreshBtn.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            monthRefreshBtn.TextImageRelation = TextImageRelation.ImageBeforeText;
            monthRefreshBtn.Width = 130;
            monthRefreshBtn.Click += (s, e) => LoadMonthSummaries();
            monthToolbar.Controls.Add(monthViewBtn);
            monthToolbar.Controls.Add(monthRefreshBtn);
            monthRoot.Controls.Add(monthToolbar, 0, 1);

            var monthGridPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.CardBackground };
            _monthGrid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true };
            Theme.StyleGrid(_monthGrid);
            _monthGrid.Columns.Add("Month", "Mois");
            _monthGrid.Columns.Add("Days", "Journées clôturées");
            _monthGrid.Columns.Add("Total", "Revenu total du mois");
            _monthGrid.Columns.Add("Expenses", "Dépenses du mois");
            _monthGrid.Columns.Add("Net", "Net du mois");
            _monthGrid.CellDoubleClick += (s, e) => OpenSelectedMonth();
            monthGridPanel.Controls.Add(_monthGrid);
            monthRoot.Controls.Add(monthGridPanel, 0, 2);

            monthPage.Controls.Add(monthRoot);

            _contentPanel.Controls.Add(innerTabs);

            Controls.Add(_contentPanel);
            Controls.Add(_lockedPanel);
        }

        private void ShowLocked()
        {
            _lockedPanel.Visible = true;
            _lockedPanel.BringToFront();
            _contentPanel.Visible = false;
        }

        private void ShowContent()
        {
            _contentPanel.Visible = true;
            _contentPanel.BringToFront();
            _lockedPanel.Visible = false;
        }

        private void TryUnlock()
        {
            if (AuthGate.RequireAdminPassword(this))
            {
                _unlocked = true;
                ShowContent();
                LoadHistory();
            }
        }

        /// <summary>Called by MainForm whenever this tab is selected. Does nothing if the
        /// tab hasn't been unlocked yet - the locked panel stays showing.</summary>
        public void RefreshAll()
        {
            if (_unlocked)
                LoadHistory();
        }

        /// <summary>Called by MainForm whenever the user switches AWAY from this tab, so the
        /// history stays protected: coming back always requires the password again instead of
        /// staying unlocked for the rest of the session.</summary>
        public void Lock()
        {
            if (!_unlocked) return;
            _unlocked = false;
            ShowLocked();
        }

        private void LoadHistory()
        {
            var history = _repo.GetHistory();
            _grid.Rows.Clear();
            foreach (var h in history)
            {
                int idx = _grid.Rows.Add(h.CloseDate, h.ClosedAt.ToString("g"), h.GrandTotal.ToString("0.00"),
                    h.TotalExpenses.ToString("0.00"), h.NetTotal.ToString("0.00"), h.ArchiveFolderPath);
                _grid.Rows[idx].Tag = h.Id;
            }
            LoadMonthSummaries();
        }

        private void LoadMonthSummaries()
        {
            if (_monthGrid == null) return;
            var summaries = _repo.GetMonthlySummaries();
            _monthGrid.Rows.Clear();
            foreach (var m in summaries)
            {
                int idx = _monthGrid.Rows.Add(m.MonthLabel, m.DaysClosed, m.TotalRevenue.ToString("0.00"),
                    m.TotalExpenses.ToString("0.00"), m.NetRevenue.ToString("0.00"));
                _monthGrid.Rows[idx].Tag = m.YearMonth;
            }
        }

        private void ViewBtn_Click(object sender, EventArgs e) => OpenSelected();

        private void OpenSelected()
        {
            if (_grid.CurrentRow?.Tag == null) return;
            int id = (int)_grid.CurrentRow.Tag;
            var full = _repo.GetFullById(id);
            if (full == null) return;

            using (var breakdown = new BreakdownReportForm(full))
                breakdown.ShowDialog();
        }

        private void DeleteBtn_Click(object sender, EventArgs e)
        {
            if (_grid.CurrentRow?.Tag == null) return;
            int id = (int)_grid.CurrentRow.Tag;
            var close = _repo.GetFullById(id);
            if (close == null) return;

            if (!AuthGate.RequireAdminPassword(this)) return;

            if (MessageBox.Show(
                $"Retirer la journée du {close.CloseDate} de l'historique ?\n\n" +
                "Cela supprime uniquement l'entrée/le total de l'historique - le dossier " +
                $"archive sur le disque est CONSERVE :\n{close.ArchiveFolderPath}",
                "Confirmer la suppression", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            _cleanupService.DeleteSingle(close);
            LoadHistory();
        }

        private void MonthViewBtn_Click(object sender, EventArgs e) => OpenSelectedMonth();

        private void OpenSelectedMonth()
        {
            if (_monthGrid.CurrentRow?.Tag == null) return;
            string yearMonth = (string)_monthGrid.CurrentRow.Tag;

            var summaries = _repo.GetMonthlySummaries();
            var summary = summaries.Find(m => m.YearMonth == yearMonth);
            if (summary == null) return;

            var days = _repo.GetClosesForMonth(yearMonth);
            using (var form = new MonthSummaryReportForm(summary, days))
                form.ShowDialog();
        }

        private void KeepCurrentMonthBtn_Click(object sender, EventArgs e)
        {
            var candidates = _cleanupService.FindClosesOutsideCurrentMonth();
            if (candidates.Count == 0)
            {
                MessageBox.Show(
                    "Toutes les archives existantes sont déjà dans le mois en cours - rien a supprimer.",
                    "Rien a nettoyer", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!AuthGate.RequireAdminPassword(this)) return;

            if (MessageBox.Show(
                $"{candidates.Count} journée(s) en dehors du mois en cours vont etre retirees " +
                "de l'historique (le compte/total seulement - les dossiers archives sur le disque " +
                "ne sont JAMAIS supprimes, quel que soit le bouton utilise). Seul le mois en cours " +
                "restera dans l'historique. Continuer ?",
                "Confirmer le nettoyage", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            int deleted = _cleanupService.DeleteAllExceptCurrentMonth();
            LoadHistory();
            MessageBox.Show($"{deleted} journée(s) retiree(s) de l'historique (dossiers archives conserves). Seul le mois en cours reste dans l'historique.", "Nettoyage termine", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void CleanupBtn_Click(object sender, EventArgs e)
        {
            var settings = _settingsRepo.LoadAll();
            if (settings.ArchiveRetentionDays <= 0)
            {
                MessageBox.Show(
                    "La duree de conservation n'est pas configuree (0 = desactive).\n" +
                    "Definissez-la dans l'onglet Paramètres pour utiliser ce bouton.",
                    "Nettoyage non configure", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var candidates = _cleanupService.FindClosesOlderThan(settings.ArchiveRetentionDays);
            if (candidates.Count == 0)
            {
                MessageBox.Show(
                    $"Aucune archive de plus de {settings.ArchiveRetentionDays} jour(s) a nettoyer.",
                    "Rien a nettoyer", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!AuthGate.RequireAdminPassword(this)) return;

            if (MessageBox.Show(
                $"{candidates.Count} journée(s) de plus de {settings.ArchiveRetentionDays} jour(s) vont etre " +
                "retirees de l'historique (le compte/total seulement - les dossiers archives sur le disque " +
                "ne sont JAMAIS supprimes). Continuer ?",
                "Confirmer le nettoyage", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            int deleted = _cleanupService.CleanupOlderThanConfiguredRetention();
            LoadHistory();
            MessageBox.Show($"{deleted} journée(s) retiree(s) de l'historique (dossiers archives conserves).", "Nettoyage termine", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ClearAllBtn_Click(object sender, EventArgs e)
        {
            if (!AuthGate.RequireAdminPassword(this)) return;

            if (MessageBox.Show(
                "Ceci va vider DEFINITIVEMENT tout l'historique (tous les comptes/totaux de journées).\n\n" +
                "Les dossiers archives sur le disque (les photos elles-memes) ne sont JAMAIS supprimes " +
                "par cette action, ni par aucune autre action de nettoyage dans l'application - ils " +
                "resteront intacts sous le dossier d'archive.\n\n" +
                "Voulez-vous continuer ?",
                "Attention - vider l'historique", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            using (var confirm = new TypeToConfirmDialog(
                "Vous etes sur le point de vider TOUT l'historique (les dossiers archives seront conserves).",
                "SUPPRIMER"))
            {
                if (confirm.ShowDialog(this) != DialogResult.OK)
                    return;
            }

            _cleanupService.DeleteAll();
            LoadHistory();
            MessageBox.Show("L'historique a ete vide. Les dossiers archives sur le disque ont ete conserves.", "Termine", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
