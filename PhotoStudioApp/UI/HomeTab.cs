using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using PhotoStudioApp.Data;
using PhotoStudioApp.Services;
using PhotoStudioApp.UI.Dialogs;

namespace PhotoStudioApp.UI
{
    /// <summary>Accueil / Aujourd'hui : nombre de photos en direct par catégorie, prix
    /// unitaire, charges additionnelles du jour, et le bouton pour clôturer la journée.</summary>
    public class HomeTab : UserControl
    {
        public event EventHandler DayClosed;

        private readonly CategoryRepository _categoryRepo = new CategoryRepository();
        private readonly CustomChargeRepository _chargeRepo = new CustomChargeRepository();
        private readonly StockRepository _stockRepo = new StockRepository();
        private readonly SettingsRepository _settingsRepo = new SettingsRepository();
        private readonly PendingBalanceRepository _pendingRepo = new PendingBalanceRepository();
        private readonly ExpenseRepository _expenseRepo = new ExpenseRepository();
        private readonly FolderCountService _folders = new FolderCountService();
        private readonly ClosingService _closingService = new ClosingService();

        private DataGridView _categoryGrid;
        private DataGridView _chargesGrid;
        private DataGridView _pendingGrid;
        private DataGridView _expensesGrid;
        private Label _dateLabel;
        private Timer _autoRefreshTimer;

        // Real-time folder verification: a FileSystemWatcher per category folder fires the
        // instant a file is added/removed/renamed, instead of waiting for the next poll tick.
        // _watchDebounce coalesces bursts of events (e.g. copying 40 photos at once) into a
        // single UI refresh a few hundred ms later, rather than refreshing 40 times in a row.
        private readonly List<FileSystemWatcher> _folderWatchers = new List<FileSystemWatcher>();
        private Timer _watchDebounce;

        public HomeTab()
        {
            BackColor = Theme.Background;
            BuildLayout();
            RefreshAll();

            _watchDebounce = new Timer { Interval = 400 };
            _watchDebounce.Tick += (s, e) =>
            {
                _watchDebounce.Stop();
                RefreshCategoryCountsOnly();
            };

            // Kept as a safety-net poll (slower now that real-time watchers do the heavy
            // lifting) - covers network-drive folders where FileSystemWatcher can be unreliable.
            _autoRefreshTimer = new Timer { Interval = 15_000 };
            _autoRefreshTimer.Tick += (s, e) => RefreshCategoryCountsOnly();
            _autoRefreshTimer.Start();

            SetupFolderWatchers();
        }

        private void BuildLayout()
        {
            // Scrolling fix: a TableLayoutPanel that is Dock=Fill with Percent rows can't
            // scroll - on a small window each section (categories/charges/soldes/dépenses)
            // just gets squeezed into whatever tiny percentage of space remains, with no way to
            // see the rest. Fix: give each section a fixed, comfortable minimum height instead
            // of a percentage, let the root panel AutoSize to its true total height, and put it
            // inside an outer Panel that actually scrolls when that total exceeds the window -
            // the same pattern used for the Paramètres tab's scroll fix.
            var scrollPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Theme.Background };

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 1,
                RowCount = 6,
                Padding = new Padding(16),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 300));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 210));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 170));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 170));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // --- En-tete : date + bouton actualiser ---
            var headerPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
            _dateLabel = new Label { Font = Theme.FontHeading, ForeColor = Theme.TextDark, AutoSize = true, Margin = new Padding(0, 0, 20, 10) };
            var refreshBtn = Theme.MakeSecondaryButton("Actualiser");
            refreshBtn.Image = IconGlyphs.Refresh(16);
            refreshBtn.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            refreshBtn.TextImageRelation = TextImageRelation.ImageBeforeText;
            refreshBtn.Width = 130;
            refreshBtn.Click += (s, e) => RefreshAll();
            headerPanel.Controls.Add(_dateLabel);
            headerPanel.Controls.Add(refreshBtn);
            root.Controls.Add(headerPanel, 0, 0);

            // --- Tableau des categories ---
            var categoryPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.CardBackground, Padding = new Padding(1) };
            _categoryGrid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true };
            Theme.StyleGrid(_categoryGrid);
            _categoryGrid.Columns.Add("Category", "Catégorie");
            _categoryGrid.Columns.Add("Count", "Photos");
            _categoryGrid.Columns.Add("Price", "Prix unitaire");
            categoryPanel.Controls.Add(_categoryGrid);
            root.Controls.Add(categoryPanel, 0, 1);

            // --- Charges additionnelles ---
            var chargesOuter = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
            chargesOuter.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            chargesOuter.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var chargesHeader = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
            var chargesTitle = new Label { Text = "Charges additionnelles du jour", Font = Theme.FontSubheading, AutoSize = true, Margin = new Padding(0, 6, 20, 6) };
            var addChargeBtn = Theme.MakePrimaryButton("+ Ajouter");
            addChargeBtn.Width = 120;
            addChargeBtn.Click += AddChargeBtn_Click;
            var editChargeBtn = Theme.MakeSecondaryButton("Modifier");
            editChargeBtn.Width = 100;
            editChargeBtn.Click += EditChargeBtn_Click;
            var deleteChargeBtn = Theme.MakeDangerButton("Supprimer");
            deleteChargeBtn.Width = 100;
            deleteChargeBtn.Click += DeleteChargeBtn_Click;
            chargesHeader.Controls.Add(chargesTitle);
            chargesHeader.Controls.Add(addChargeBtn);
            chargesHeader.Controls.Add(editChargeBtn);
            chargesHeader.Controls.Add(deleteChargeBtn);

            var chargesPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.CardBackground };
            _chargesGrid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true };
            Theme.StyleGrid(_chargesGrid);
            _chargesGrid.Columns.Add("Description", "Description");
            _chargesGrid.Columns.Add("Linked", "Lien");
            _chargesGrid.Columns.Add("Qty", "Qte");
            _chargesGrid.Columns.Add("Amount", "Montant");
            chargesPanel.Controls.Add(_chargesGrid);

            chargesOuter.Controls.Add(chargesHeader, 0, 0);
            chargesOuter.Controls.Add(chargesPanel, 0, 1);
            root.Controls.Add(chargesOuter, 0, 2);

            // --- Soldes en attente (paiements partiels non termines) ---
            var pendingOuter = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
            pendingOuter.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            pendingOuter.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var pendingHeader = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
            var pendingTitle = new Label { Text = "Soldes en attente (paiements partiels)", Font = Theme.FontSubheading, AutoSize = true, Margin = new Padding(0, 6, 20, 6) };
            var payBalanceBtn = Theme.MakePrimaryButton("Payer");
            payBalanceBtn.Width = 110;
            payBalanceBtn.Click += PayBalanceBtn_Click;
            var refreshPendingBtn = Theme.MakeSecondaryButton("Actualiser");
            refreshPendingBtn.Image = IconGlyphs.Refresh(16);
            refreshPendingBtn.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            refreshPendingBtn.TextImageRelation = TextImageRelation.ImageBeforeText;
            refreshPendingBtn.Width = 130;
            refreshPendingBtn.Click += (s, e) => RefreshPendingBalances();
            pendingHeader.Controls.Add(pendingTitle);
            pendingHeader.Controls.Add(payBalanceBtn);
            pendingHeader.Controls.Add(refreshPendingBtn);

            var pendingPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.CardBackground };
            _pendingGrid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true };
            Theme.StyleGrid(_pendingGrid);
            _pendingGrid.Columns.Add("Description", "Description");
            _pendingGrid.Columns.Add("Remaining", "Reste a payer");
            _pendingGrid.CellDoubleClick += (s, e) => PayBalanceBtn_Click(s, e);
            pendingPanel.Controls.Add(_pendingGrid);

            pendingOuter.Controls.Add(pendingHeader, 0, 0);
            pendingOuter.Controls.Add(pendingPanel, 0, 1);
            root.Controls.Add(pendingOuter, 0, 3);

            // --- Dépenses du jour (achats pour le magasin) ---
            var expensesOuter = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
            expensesOuter.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            expensesOuter.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var expensesHeader = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
            var expensesTitle = new Label { Text = "Dépenses du jour (achats pour le magasin)", Font = Theme.FontSubheading, AutoSize = true, Margin = new Padding(0, 6, 20, 6) };
            var addExpenseBtn = Theme.MakePrimaryButton("Ajouter");
            addExpenseBtn.Width = 110;
            addExpenseBtn.Click += AddExpenseBtn_Click;
            var editExpenseBtn = Theme.MakeSecondaryButton("Modifier");
            editExpenseBtn.Width = 110;
            editExpenseBtn.Click += EditExpenseBtn_Click;
            var deleteExpenseBtn = Theme.MakeDangerButton("Supprimer");
            deleteExpenseBtn.Width = 110;
            deleteExpenseBtn.Click += DeleteExpenseBtn_Click;
            expensesHeader.Controls.Add(expensesTitle);
            expensesHeader.Controls.Add(addExpenseBtn);
            expensesHeader.Controls.Add(editExpenseBtn);
            expensesHeader.Controls.Add(deleteExpenseBtn);

            var expensesPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.CardBackground };
            _expensesGrid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true };
            Theme.StyleGrid(_expensesGrid);
            _expensesGrid.Columns.Add("Description", "Description");
            _expensesGrid.Columns.Add("Amount", "Montant");
            _expensesGrid.CellDoubleClick += (s, e) => EditExpenseBtn_Click(s, e);
            expensesPanel.Controls.Add(_expensesGrid);

            expensesOuter.Controls.Add(expensesHeader, 0, 0);
            expensesOuter.Controls.Add(expensesPanel, 0, 1);
            root.Controls.Add(expensesOuter, 0, 4);

            // --- Pied de page : bouton Clôturer la journée (pas d'apercu de total, sur demande) ---
            var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Margin = new Padding(0, 12, 0, 0) };
            var closeDayBtn = Theme.MakePrimaryButton("Clôturer la journée");
            closeDayBtn.Image = IconGlyphs.Lock(18);
            closeDayBtn.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            closeDayBtn.TextImageRelation = TextImageRelation.ImageBeforeText;
            closeDayBtn.Width = 240;
            closeDayBtn.Height = 46;
            closeDayBtn.Click += CloseDayBtn_Click;
            footer.Controls.Add(closeDayBtn);
            root.Controls.Add(footer, 0, 5);

            scrollPanel.Controls.Add(root);
            Controls.Add(scrollPanel);
        }

        public void RefreshAll()
        {
            var settings = _settingsRepo.LoadAll();
            string workDateStr = settings.CurrentWorkDate ?? DateTime.Today.ToString("yyyy-MM-dd");
            DateTime workDate = DateTime.TryParseExact(workDateStr, "yyyy-MM-dd", null,
                System.Globalization.DateTimeStyles.None, out var d) ? d : DateTime.Today;
            _dateLabel.Text = $"Aujourd'hui : {workDate.ToString("dddd dd MMMM yyyy", AppCulture.French)}";

            RefreshCategoryCountsOnly();
            RefreshCharges();
            RefreshPendingBalances();
            RefreshExpenses();
        }

        public void RefreshCategoryCountsOnly()
        {
            var settings = _settingsRepo.LoadAll();
            var categories = _categoryRepo.GetActiveFolderCategories();
            _categoryGrid.Rows.Clear();

            foreach (var cat in categories)
            {
                string abs = string.IsNullOrWhiteSpace(settings.StudioRootPath)
                    ? null
                    : Path.Combine(settings.StudioRootPath, cat.FolderRelativePath);
                int count = abs == null ? 0 : _folders.CountPhotos(abs);

                int rowIdx = _categoryGrid.Rows.Add(cat.Name, count, cat.CurrentPrice.ToString("0.00"));
                if (abs != null && !Directory.Exists(abs))
                {
                    _categoryGrid.Rows[rowIdx].DefaultCellStyle.ForeColor = Theme.Warning;
                    _categoryGrid.Rows[rowIdx].DefaultCellStyle.SelectionForeColor = Theme.Warning;
                    _categoryGrid.Rows[rowIdx].DefaultCellStyle.SelectionBackColor = Theme.WarningBackground;
                    _categoryGrid.Rows[rowIdx].Cells[0].ToolTipText = "Dossier introuvable sur le disque";
                }
            }
        }

        private void RefreshCharges()
        {
            var settings = _settingsRepo.LoadAll();
            var charges = _chargeRepo.GetForWorkDate(settings.CurrentWorkDate);
            _chargesGrid.Rows.Clear();
            foreach (var c in charges)
            {
                string linked = c.LinkedStockItemName ?? c.LinkedCategoryName ?? "-";
                int rowIdx = _chargesGrid.Rows.Add(c.Description, linked, c.Quantity?.ToString() ?? "-", c.Amount.ToString("0.00"));
                _chargesGrid.Rows[rowIdx].Tag = c.Id;
            }
        }

        /// <summary>Refreshes the "Soldes en attente" grid - clients who only paid part of a
        /// charge. A balance disappears from here automatically once fully paid off (see
        /// PendingBalanceRepository.ReduceOrDelete), and can be paid down further (or fully
        /// settled) via the "Payer" button here or by picking it again from "Ajouter une
        /// charge"'s dropdown.</summary>
        private void RefreshPendingBalances()
        {
            if (_pendingGrid == null) return;
            var balances = _pendingRepo.GetAll();
            _pendingGrid.Rows.Clear();
            foreach (var b in balances)
            {
                int rowIdx = _pendingGrid.Rows.Add(b.Description, b.RemainingAmount.ToString("0.00"));
                _pendingGrid.Rows[rowIdx].Tag = b.Id;
            }
        }

        private void PayBalanceBtn_Click(object sender, EventArgs e)
        {
            if (_pendingGrid.CurrentRow?.Tag == null)
            {
                MessageBox.Show("Selectionnez d'abord un solde dans la liste.", "Aucune selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            int id = (int)_pendingGrid.CurrentRow.Tag;
            var balance = _pendingRepo.GetById(id);
            if (balance == null) return;

            using (var dlg = new AddCustomChargeDialog(preselectedBalance: balance))
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    var settings = _settingsRepo.LoadAll();
                    var charge = dlg.ResultCharge;
                    charge.WorkDate = settings.CurrentWorkDate;
                    _chargeRepo.Add(charge);
                    RefreshCharges();
                    RefreshPendingBalances();
                }
            }
        }

        /// <summary>Refreshes the "Dépenses du jour" grid - money spent (e.g. a worker buying
        /// supplies for the studio), tracked separately from revenue and subtracted at close
        /// time to produce the day's net total.</summary>
        private void RefreshExpenses()
        {
            if (_expensesGrid == null) return;
            var settings = _settingsRepo.LoadAll();
            var expenses = _expenseRepo.GetForWorkDate(settings.CurrentWorkDate);
            _expensesGrid.Rows.Clear();
            foreach (var ex in expenses)
            {
                int rowIdx = _expensesGrid.Rows.Add(ex.Description, ex.Amount.ToString("0.00"));
                _expensesGrid.Rows[rowIdx].Tag = ex.Id;
            }
        }

        private void AddExpenseBtn_Click(object sender, EventArgs e)
        {
            using (var dlg = new AddExpenseDialog())
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    var settings = _settingsRepo.LoadAll();
                    var expense = dlg.ResultExpense;
                    expense.WorkDate = settings.CurrentWorkDate;
                    _expenseRepo.Add(expense);
                    RefreshExpenses();
                }
            }
        }

        private void EditExpenseBtn_Click(object sender, EventArgs e)
        {
            if (_expensesGrid.CurrentRow?.Tag == null) return;
            int id = (int)_expensesGrid.CurrentRow.Tag;
            var existing = _expenseRepo.GetById(id);
            if (existing == null) return;

            using (var dlg = new AddExpenseDialog(existing))
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    var updated = dlg.ResultExpense;
                    updated.Id = id;
                    updated.WorkDate = existing.WorkDate;
                    _expenseRepo.Update(updated);
                    RefreshExpenses();
                }
            }
        }

        private void DeleteExpenseBtn_Click(object sender, EventArgs e)
        {
            if (_expensesGrid.CurrentRow?.Tag == null) return;
            int id = (int)_expensesGrid.CurrentRow.Tag;

            if (MessageBox.Show("Supprimer cette dépense ?", "Confirmer", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            _expenseRepo.Delete(id);
            RefreshExpenses();
        }

        public void RefreshCustomChargeStockOptions()
        {
            // Point d'extension : le dialogue "Ajouter une charge" recharge le stock et les
            // categories a chaque ouverture, donc rien a mettre en cache ici.
        }

        /// <summary>(Re)creates one FileSystemWatcher per active folder-backed category so photo
        /// counts update in real time (the instant a file is added/removed) instead of only on
        /// the next timer tick. Called at startup and whenever categories or the studio root
        /// path might have changed (see MainForm wiring).</summary>
        public void SetupFolderWatchers()
        {
            foreach (var w in _folderWatchers)
            {
                w.EnableRaisingEvents = false;
                w.Dispose();
            }
            _folderWatchers.Clear();

            var settings = _settingsRepo.LoadAll();
            if (string.IsNullOrWhiteSpace(settings.StudioRootPath))
                return;

            foreach (var cat in _categoryRepo.GetActiveFolderCategories())
            {
                string abs;
                try { abs = Path.Combine(settings.StudioRootPath, cat.FolderRelativePath); }
                catch { continue; }

                if (!Directory.Exists(abs))
                    continue;

                try
                {
                    var watcher = new FileSystemWatcher(abs)
                    {
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.DirectoryName,
                        IncludeSubdirectories = false
                    };
                    watcher.Created += FolderWatcher_Changed;
                    watcher.Deleted += FolderWatcher_Changed;
                    watcher.Renamed += FolderWatcher_Changed;
                    watcher.Changed += FolderWatcher_Changed;
                    watcher.EnableRaisingEvents = true;
                    _folderWatchers.Add(watcher);
                }
                catch
                {
                    // Best-effort - if a watcher can't be created for this folder (e.g. a
                    // network path that doesn't support it), the 15s safety-net poll still
                    // catches it eventually.
                }
            }
        }

        private void FolderWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            // FileSystemWatcher events arrive on a background thread - marshal back to the UI
            // thread and (re)start the debounce timer so a burst of events collapses into one
            // refresh shortly after activity settles down.
            if (IsDisposed) return;
            try
            {
                BeginInvoke((Action)(() =>
                {
                    if (_watchDebounce == null) return;
                    _watchDebounce.Stop();
                    _watchDebounce.Start();
                }));
            }
            catch (ObjectDisposedException) { /* form closing - ignore */ }
            catch (InvalidOperationException) { /* handle not yet created - ignore */ }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var w in _folderWatchers)
                {
                    w.EnableRaisingEvents = false;
                    w.Dispose();
                }
                _folderWatchers.Clear();
                _watchDebounce?.Dispose();
                _autoRefreshTimer?.Dispose();
            }
            base.Dispose(disposing);
        }

        private void AddChargeBtn_Click(object sender, EventArgs e)
        {
            using (var dlg = new AddCustomChargeDialog())
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    var settings = _settingsRepo.LoadAll();
                    var charge = dlg.ResultCharge;
                    charge.WorkDate = settings.CurrentWorkDate;
                    _chargeRepo.Add(charge);

                    if (charge.LinkedStockItemId.HasValue && charge.Quantity.HasValue && charge.Quantity.Value > 0)
                    {
                        _stockRepo.AdjustQuantity(charge.LinkedStockItemId.Value, -charge.Quantity.Value);
                        var item = _stockRepo.GetById(charge.LinkedStockItemId.Value);
                        if (item != null && item.IsOutOfStock)
                            MessageBox.Show($"'{item.Name}' est maintenant en rupture de stock.", "Alerte stock", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    RefreshCharges();
                    RefreshPendingBalances();
                }
            }
        }

        private void EditChargeBtn_Click(object sender, EventArgs e)
        {
            if (_chargesGrid.CurrentRow == null) return;
            int id = (int)_chargesGrid.CurrentRow.Tag;
            var existing = _chargeRepo.GetById(id);
            if (existing == null) return;

            using (var dlg = new AddCustomChargeDialog(existing))
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    // Restitue toute quantité de stock precedemment consommee avant d'appliquer la nouvelle.
                    if (existing.LinkedStockItemId.HasValue && existing.Quantity.HasValue)
                        _stockRepo.AdjustQuantity(existing.LinkedStockItemId.Value, existing.Quantity.Value);

                    var updated = dlg.ResultCharge;
                    updated.Id = id;
                    updated.WorkDate = existing.WorkDate;
                    _chargeRepo.Update(updated);

                    if (updated.LinkedStockItemId.HasValue && updated.Quantity.HasValue && updated.Quantity.Value > 0)
                        _stockRepo.AdjustQuantity(updated.LinkedStockItemId.Value, -updated.Quantity.Value);

                    RefreshCharges();
                    RefreshPendingBalances();
                }
            }
        }

        private void DeleteChargeBtn_Click(object sender, EventArgs e)
        {
            if (_chargesGrid.CurrentRow == null) return;
            int id = (int)_chargesGrid.CurrentRow.Tag;

            if (MessageBox.Show("Supprimer cette charge ? Si elle avait consomme du stock, celui-ci sera restitue.",
                "Confirmer la suppression", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            var existing = _chargeRepo.GetById(id);
            if (existing?.LinkedStockItemId != null && existing.Quantity.HasValue)
                _stockRepo.AdjustQuantity(existing.LinkedStockItemId.Value, existing.Quantity.Value);

            _chargeRepo.Delete(id);
            RefreshCharges();
        }

        private void CloseDayBtn_Click(object sender, EventArgs e)
        {
            var settings = _settingsRepo.LoadAll();
            if (string.IsNullOrEmpty(settings.PasswordHash))
            {
                MessageBox.Show("Aucun mot de passe de clôture n'est defini. Definissez-en un dans l'onglet Paramètres.",
                    "Mot de passe requis", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var pwd = new PasswordPromptDialog(settings.MaxPasswordAttempts))
            {
                if (pwd.ShowDialog() != DialogResult.OK)
                    return; // annule ou trop de tentatives
            }

            if (MessageBox.Show(
                "Cette action va archiver les photos du jour, clôturer la comptabilite et demarrer une nouvelle journée. Continuer ?",
                "Confirmer la clôture", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            try
            {
                Models.DailyClose close;
                try
                {
                    close = _closingService.CloseDay();
                }
                catch (EmptyDayException ex)
                {
                    if (MessageBox.Show(ex.Message, "Journée vide", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                        return;
                    close = _closingService.CloseDay(confirmedEmptyDay: true);
                }

                RefreshAll();
                DayClosed?.Invoke(this, EventArgs.Empty);

                using (var breakdown = new BreakdownReportForm(close))
                    breakdown.ShowDialog();
            }
            catch (CloseDayException ex)
            {
                MessageBox.Show(ex.Message, "Impossible de clôturer la journée", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Une erreur inattendue est survenue lors de la clôture :\n" + ex.Message,
                    "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
