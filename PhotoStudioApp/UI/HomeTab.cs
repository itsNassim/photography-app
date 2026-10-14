using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        private readonly Dictionary<FileSystemWatcher, string> _watcherCategoryNames = new Dictionary<FileSystemWatcher, string>();
        /// <summary>Guards every read/write of _folderWatchers, _watcherCategoryNames and
        /// _pendingDeletes below - FileSystemWatcher events arrive on background threads (one
        /// per watcher, potentially concurrently), and a dynamically-discovered new sub-folder
        /// (see TryAddWatcher) can mutate these same collections from within an event callback
        /// while SetupFolderWatchers might be rebuilding them from the UI thread.</summary>
        private readonly object _watcherLock = new object();
        /// <summary>Category label used in the studio action log for events that happen directly
        /// in the studio root folder itself (see SetupFolderWatchers' extra root-level watcher),
        /// rather than inside any one category's own folder.</summary>
        private const string StudioRootFolderLabel = "(dossier racine du studio)";

        /// <summary>A file (or folder) deleted from one watched location within the last
        /// MoveCorrelationWindow, held back from being logged as a plain deletion in case a
        /// matching Created event (same name) shows up in a DIFFERENT watched folder shortly
        /// after - which would mean this was really a move between two folders, each with their
        /// own separate FileSystemWatcher (a rename/move within a SINGLE watcher's tree already
        /// arrives as one Renamed event with both paths, so doesn't need this correlation - see
        /// LogStudioFileEvent's Renamed handling instead). See HandleCreated/HandleDeleted/
        /// FlushPendingDelete.</summary>
        private class PendingDelete
        {
            public string CategoryName;
            public string SubPath;
            public bool IsDirectory;
            public System.Threading.Timer FlushTimer;
        }
        private readonly Dictionary<string, PendingDelete> _pendingDeletes = new Dictionary<string, PendingDelete>(StringComparer.OrdinalIgnoreCase);
        private static readonly TimeSpan MoveCorrelationWindow = TimeSpan.FromSeconds(3);

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
            // Charges additionnelles gets the most room of the lower sections (was 210): with
            // optional extras, one sale can now add several lines at once, so a short list
            // meant constant scrolling inside the grid.
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 400));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 170));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 170));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // --- En-tete : date (temps reel - pas de bouton actualiser, voir RefreshAll) ---
            var headerPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
            _dateLabel = new Label { Font = Theme.FontHeading, ForeColor = Theme.TextDark, AutoSize = true, Margin = new Padding(0, 0, 20, 10) };
            headerPanel.Controls.Add(_dateLabel);
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
            var deleteBalanceBtn = Theme.MakeDangerButton("Supprimer");
            deleteBalanceBtn.Width = 110;
            deleteBalanceBtn.Click += DeleteBalanceBtn_Click;
            pendingHeader.Controls.Add(pendingTitle);
            pendingHeader.Controls.Add(payBalanceBtn);
            pendingHeader.Controls.Add(deleteBalanceBtn);

            var pendingPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.CardBackground };
            _pendingGrid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true };
            Theme.StyleGrid(_pendingGrid);
            _pendingGrid.Columns.Add("Description", "Description");
            _pendingGrid.Columns.Add("Total", "Total achete");
            _pendingGrid.Columns.Add("Remaining", "Reste a payer");
            _pendingGrid.Columns.Add("Date", "Date d'achat");
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

            // --- Pied de page : bouton Clôturer la journée (pas d'apercu de total par defaut -
            //     voir le bouton "Voir l'état actuel" juste a cote pour ca, sur demande) ---
            // FlowLayoutPanel + RightToLeft lays children out starting from the right: the
            // FIRST-added control (closeDayBtn) ends up rightmost, with each later-added one
            // placed to its left - so closeDayBtn stays the rightmost/most prominent action and
            // viewStateBtn appears right next to it, to its left.
            var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Margin = new Padding(0, 12, 0, 0) };
            var closeDayBtn = Theme.MakePrimaryButton("Clôturer la journée");
            closeDayBtn.Image = IconGlyphs.Lock(18);
            closeDayBtn.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            closeDayBtn.TextImageRelation = TextImageRelation.ImageBeforeText;
            closeDayBtn.Width = 240;
            closeDayBtn.Height = 46;
            closeDayBtn.Click += CloseDayBtn_Click;
            var viewStateBtn = Theme.MakeSecondaryButton("Voir l'état actuel");
            viewStateBtn.Image = IconGlyphs.Lock(18);
            viewStateBtn.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            viewStateBtn.TextImageRelation = TextImageRelation.ImageBeforeText;
            viewStateBtn.Width = 220;
            viewStateBtn.Height = 46;
            viewStateBtn.Margin = new Padding(0, 0, 10, 0);
            viewStateBtn.Click += ViewCurrentStateBtn_Click;
            footer.Controls.Add(closeDayBtn);
            footer.Controls.Add(viewStateBtn);
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
                int rowIdx = _pendingGrid.Rows.Add(b.Description, b.TotalAmount.ToString("0.00"), b.RemainingAmount.ToString("0.00"), b.CreatedDate);
                _pendingGrid.Rows[rowIdx].Tag = b.Id;
            }
        }

        /// <summary>Manually removes a pending balance without it ever being paid off - e.g. the
        /// studio decides to write it off, or it was created by mistake. Unlike paying it down,
        /// this never creates a CustomCharge/revenue entry - the balance just disappears.</summary>
        private void DeleteBalanceBtn_Click(object sender, EventArgs e)
        {
            if (_pendingGrid.CurrentRow?.Tag == null)
            {
                MessageBox.Show("Selectionnez d'abord un solde dans la liste.", "Aucune selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            int id = (int)_pendingGrid.CurrentRow.Tag;
            var balance = _pendingRepo.GetById(id);
            if (balance == null) return;

            if (MessageBox.Show(
                $"Supprimer definitivement le solde en attente \"{balance.Description}\" (reste : {balance.RemainingAmount:0.00}) ?\n\n" +
                "Cette action ne cree aucune charge/revenu - le solde disparait simplement sans avoir ete paye.",
                "Confirmer la suppression", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            _pendingRepo.Delete(id);
            RefreshPendingBalances();
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

        /// <summary>(Re)creates one FileSystemWatcher per folder-backed category - active or not,
        /// see GetAllFolderCategories - so photo counts update in real time (the instant a file
        /// is added/removed) instead of only on the next timer tick, AND so every category's
        /// folder gets logged to the studio action log even if the category itself was later
        /// deactivated. ALSO covers every other folder anywhere under the studio root that isn't
        /// itself a registered category - the day's "today" working folder, an orphaned/renamed
        /// category folder, or anything else - via ScanForOrphanWatchers below, so genuinely
        /// nothing that happens anywhere inside the studio root goes unwatched. Called at startup
        /// and whenever categories or the studio root path might have changed (see MainForm
        /// wiring).</summary>
        public void SetupFolderWatchers()
        {
            lock (_watcherLock)
            {
                foreach (var w in _folderWatchers)
                {
                    w.EnableRaisingEvents = false;
                    w.Dispose();
                }
                _folderWatchers.Clear();
                _watcherCategoryNames.Clear();
            }
            ClearPendingDeletes();

            var settings = _settingsRepo.LoadAll();
            if (string.IsNullOrWhiteSpace(settings.StudioRootPath))
                return;

            var categoryPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var cat in _categoryRepo.GetAllFolderCategories())
            {
                string abs;
                try { abs = Path.Combine(settings.StudioRootPath, cat.FolderRelativePath); }
                catch { continue; }

                if (!Directory.Exists(abs))
                    continue;

                // Recursive - a category folder's own subfolders (a photographer organizing
                // shots into a client-name subfolder, for instance) need to be watched and
                // logged too, not just the category folder's own top level.
                TryAddWatcher(abs, cat.Name, recursive: true);
                categoryPaths.Add(NormalizePath(abs));
            }

            if (Directory.Exists(settings.StudioRootPath))
            {
                // NOT recursive - every category folder already has its own recursive watcher
                // above, so this one only needs to see entries directly inside the studio root
                // itself; making it recursive too would log every category's activity a second
                // time. A brand-new sub-folder that appears directly in the root (not yet a
                // registered category) gets its OWN dynamic recursive watcher the instant it's
                // created instead - see HandleCreated.
                TryAddWatcher(settings.StudioRootPath, StudioRootFolderLabel, recursive: false);

                try
                {
                    ScanForOrphanWatchers(settings.StudioRootPath, categoryPaths);
                }
                catch
                {
                    // Best-effort - a permissions/I-O issue partway through the scan shouldn't
                    // crash startup; whatever was already added above still works.
                }
            }
        }

        /// <summary>Recursively walks every folder under dir that ISN'T itself a registered
        /// category (those already got their own recursive watcher above) and gives each one
        /// exactly the coverage it needs, with no overlap: a folder with NO registered category
        /// anywhere below it (the day's "today" working folder, a leftover/renamed category
        /// folder, or anything else) gets ONE recursive watcher covering its whole subtree and
        /// scanning stops there - nothing further to add. A folder that IS an ANCESTOR of a
        /// registered category (e.g. "amateur", the parent of the "amateur\12 identite" and
        /// "amateur\16 identite" category folders) gets its own NON-recursive watcher instead -
        /// so a file dropped directly inside "amateur" itself (alongside its category
        /// subfolders) still gets logged - and scanning continues one level deeper to find that
        /// nested category (already covered, skipped) and any other orphan siblings next to it.
        /// This is exactly what closes the original gap: "amateur" itself, and folders like a
        /// "today" working folder with no category inside it at all, previously had no watcher
        /// covering their own direct contents.</summary>
        private void ScanForOrphanWatchers(string dir, HashSet<string> categoryPaths)
        {
            foreach (var subDir in Directory.GetDirectories(dir))
            {
                string normalized;
                try { normalized = NormalizePath(subDir); }
                catch { continue; }

                if (categoryPaths.Contains(normalized))
                    continue; // already has its own recursive watcher from the category loop

                bool hasCategoryBelow = categoryPaths.Any(p =>
                    p.StartsWith(normalized + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));

                if (hasCategoryBelow)
                {
                    TryAddWatcher(subDir, Path.GetFileName(subDir), recursive: false);
                    try { ScanForOrphanWatchers(subDir, categoryPaths); }
                    catch { /* best-effort - see SetupFolderWatchers' own catch */ }
                }
                else
                {
                    TryAddWatcher(subDir, Path.GetFileName(subDir), recursive: true);
                }
            }
        }

        /// <summary>Full path, trailing separator trimmed, for reliable case-insensitive
        /// comparison/prefix-matching between paths built in different ways (Path.Combine vs.
        /// Directory.GetDirectories, for instance).</summary>
        private static string NormalizePath(string path)
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        /// <summary>Creates, wires up, starts and registers one FileSystemWatcher for path,
        /// labelled label in the studio action log - the single place a watcher gets created,
        /// used for every category folder, the studio root itself, every "orphan" folder found
        /// by ScanForOrphanWatchers (both from SetupFolderWatchers), and any brand-new sub-folder
        /// discovered live inside a non-recursive watcher after startup (see HandleCreated), so
        /// all of these always behave identically. Best-effort: swallows failures (e.g. a network
        /// path that doesn't support watching) since the 15s safety-net poll still catches those
        /// folders eventually.</summary>
        private void TryAddWatcher(string path, string label, bool recursive)
        {
            try
            {
                var watcher = new FileSystemWatcher(path)
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.DirectoryName,
                    IncludeSubdirectories = recursive
                };
                watcher.Created += FolderWatcher_Changed;
                watcher.Deleted += FolderWatcher_Changed;
                watcher.Renamed += FolderWatcher_Changed;
                watcher.Changed += FolderWatcher_Changed;
                watcher.EnableRaisingEvents = true;

                lock (_watcherLock)
                {
                    _folderWatchers.Add(watcher);
                    _watcherCategoryNames[watcher] = label;
                }
            }
            catch
            {
                // Best-effort - see summary above.
            }
        }

        private void FolderWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            LogStudioFileEvent(sender, e);

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

        /// <summary>Records a file (or subfolder) entering/leaving/being renamed anywhere inside
        /// a category folder or the studio root - including inside subfolders someone creates
        /// within it, now that watchers are recursive (see SetupFolderWatchers) - into the
        /// studio action log (Archive tab). "Changed" (a plain write, e.g. mid-copy) is not
        /// logged - too noisy and not a meaningful action on its own; only a file/folder's
        /// arrival, removal, rename, or move is. Skipped entirely while StudioLogSuppressor is
        /// active, i.e. during the app's own routine close-day purge/archive - those are logged
        /// explicitly and more informatively by FolderCountService instead (see its comments),
        /// so logging them again here would both duplicate and mislabel them (a photo safely
        /// moved to the archive would otherwise show up here as a plain "Suppression").</summary>
        private void LogStudioFileEvent(object sender, FileSystemEventArgs e)
        {
            try
            {
                if (e.ChangeType == WatcherChangeTypes.Changed) return;
                if (StudioLogSuppressor.IsSuppressed) return;

                var watcher = sender as FileSystemWatcher;
                string categoryName;
                lock (_watcherLock)
                    categoryName = (watcher != null && _watcherCategoryNames.TryGetValue(watcher, out var name)) ? name : null;

                string fileName = Path.GetFileName(e.FullPath);
                string subPath = ComputeSubPath(watcher, e.FullPath);

                switch (e.ChangeType)
                {
                    case WatcherChangeTypes.Created:
                        HandleCreated(watcher, categoryName, fileName, subPath, e.FullPath);
                        break;
                    case WatcherChangeTypes.Deleted:
                        HandleDeleted(categoryName, fileName, subPath, e.FullPath);
                        break;
                    case WatcherChangeTypes.Renamed:
                        HandleRenamed(watcher, categoryName, fileName, subPath, e as RenamedEventArgs);
                        break;
                }
            }
            catch
            {
                // Logging must never break the live folder-watching feature.
            }
        }

        /// <summary>A file/folder just appeared. Two things can be going on: (1) it's a brand-new
        /// sub-folder appearing directly inside a NON-recursive watcher (the studio root itself,
        /// or an "ancestor of a category" folder like "amateur" - see ScanForOrphanWatchers) -
        /// give it its own live recursive watcher right away, so nothing created inside IT is
        /// ever missed either, whatever else this turns out to be; (2) it matches something
        /// deleted from a DIFFERENT watched folder moments ago (see HandleDeleted/PendingDelete) -
        /// if so, this is really a move between two separately-watched folders, logged as ONE
        /// "Déplacement" entry instead of a misleading "Suppression" + "Entrée" pair. Otherwise,
        /// it's logged as a genuine new arrival.</summary>
        private void HandleCreated(FileSystemWatcher watcher, string categoryName, string fileName, string subPath, string fullPath)
        {
            bool isDirectory = Directory.Exists(fullPath);

            // A non-recursive watcher, by definition, doesn't automatically extend to a
            // sub-folder created directly inside it - give that new sub-folder its own live
            // recursive watcher immediately, whether it's the studio root or an "amateur"-style
            // orphan parent folder (see ScanForOrphanWatchers for why those are non-recursive).
            if (isDirectory && watcher != null && !watcher.IncludeSubdirectories)
            {
                bool alreadyWatched;
                lock (_watcherLock)
                    alreadyWatched = _folderWatchers.Any(w => string.Equals(w.Path, fullPath, StringComparison.OrdinalIgnoreCase));
                if (!alreadyWatched)
                    TryAddWatcher(fullPath, fileName, recursive: true);
            }

            var pending = TakePendingDelete(fileName);
            if (pending != null)
            {
                string fromLabel = pending.SubPath == null ? pending.CategoryName : $"{pending.CategoryName} / {pending.SubPath}";
                string toLabel = subPath == null ? categoryName : $"{categoryName} / {subPath}";
                string moveAction = isDirectory ? "Déplacement de sous-dossier" : "Déplacement";
                StudioActionLogService.Record(fileName, moveAction, categoryName, $"De {fromLabel} vers {toLabel}");
                return;
            }

            string action = isDirectory ? "Création de sous-dossier" : "Entrée (nouveau fichier)";
            string details = subPath == null ? null : $"Dans le sous-dossier : {subPath}";
            StudioActionLogService.Record(fileName, action, categoryName, details);
        }

        /// <summary>A file/folder just disappeared. Rather than logging a deletion immediately,
        /// this is held for MoveCorrelationWindow in case a matching Created event (same file
        /// name) shows up in a different watched folder shortly after - see HandleCreated. If
        /// nothing matches in time, FlushPendingDelete logs it as a genuine deletion.</summary>
        private void HandleDeleted(string categoryName, string fileName, string subPath, string fullPath)
        {
            // Can't check Directory.Exists any more - it's already gone. A missing extension is
            // a best-effort hint that this was a folder, not a file (a real photo file always
            // has one; a folder named e.g. "IMG_2026" without a dot would be the rare miss here).
            bool isDirectory = string.IsNullOrEmpty(Path.GetExtension(fullPath));

            var pending = new PendingDelete { CategoryName = categoryName, SubPath = subPath, IsDirectory = isDirectory };
            pending.FlushTimer = new System.Threading.Timer(
                _ => FlushPendingDelete(fileName), null, MoveCorrelationWindow, System.Threading.Timeout.InfiniteTimeSpan);

            lock (_watcherLock)
            {
                if (_pendingDeletes.TryGetValue(fileName, out var existing))
                    existing.FlushTimer.Dispose();
                _pendingDeletes[fileName] = pending;
            }
        }

        /// <summary>A rename/move WITHIN a single watcher's own tree (unlike HandleCreated's
        /// cross-watcher correlation, this arrives as one Renamed event with both the old and new
        /// full paths already known, since both are under the same watched root). Distinguishes a
        /// genuine same-folder rename from a move to a different sub-folder by comparing the two
        /// paths' containing folders.</summary>
        private void HandleRenamed(FileSystemWatcher watcher, string categoryName, string fileName, string subPath, RenamedEventArgs renamed)
        {
            if (renamed == null) return;

            bool isDirectory = Directory.Exists(renamed.FullPath);
            string oldName = Path.GetFileName(renamed.OldFullPath);
            string oldSubPath = ComputeSubPath(watcher, renamed.OldFullPath);
            bool sameName = string.Equals(oldName, fileName, StringComparison.OrdinalIgnoreCase);
            bool movedFolder = !string.Equals(oldSubPath ?? "", subPath ?? "", StringComparison.OrdinalIgnoreCase);

            string action;
            string details;
            if (movedFolder)
            {
                action = isDirectory ? "Déplacement de sous-dossier" : "Déplacement";
                string fromLabel = oldSubPath ?? "(racine de la catégorie)";
                string toLabel = subPath ?? "(racine de la catégorie)";
                details = $"De {fromLabel} vers {toLabel}" + (sameName ? "" : $" - Ancien nom : {oldName}");
            }
            else
            {
                action = isDirectory ? "Renommage de sous-dossier" : "Renommage";
                details = subPath == null ? $"Ancien nom : {oldName}" : $"Dans le sous-dossier : {subPath} - Ancien nom : {oldName}";
            }

            StudioActionLogService.Record(fileName, action, categoryName, details);
        }

        /// <summary>Removes and returns a pending deletion for fileName if one is still waiting
        /// (i.e. a matching Created event arrived within MoveCorrelationWindow), disposing its
        /// flush timer so it never fires. Returns null if there's no match.</summary>
        private PendingDelete TakePendingDelete(string fileName)
        {
            lock (_watcherLock)
            {
                if (_pendingDeletes.TryGetValue(fileName, out var pending))
                {
                    _pendingDeletes.Remove(fileName);
                    pending.FlushTimer.Dispose();
                    return pending;
                }
            }
            return null;
        }

        /// <summary>Called once MoveCorrelationWindow has passed with no matching Created event -
        /// logs the held-back deletion as a genuine "Suppression", since it turned out not to be
        /// half of a move after all.</summary>
        private void FlushPendingDelete(string fileName)
        {
            PendingDelete pending;
            lock (_watcherLock)
            {
                if (!_pendingDeletes.TryGetValue(fileName, out pending)) return;
                _pendingDeletes.Remove(fileName);
            }

            try
            {
                pending.FlushTimer.Dispose();
                string action = pending.IsDirectory ? "Suppression de sous-dossier" : "Suppression";
                string details = pending.SubPath == null ? null : $"Dans le sous-dossier : {pending.SubPath}";
                StudioActionLogService.Record(fileName, action, pending.CategoryName, details);
            }
            catch
            {
                // Logging must never break the live folder-watching feature.
            }
        }

        private void ClearPendingDeletes()
        {
            lock (_watcherLock)
            {
                foreach (var pending in _pendingDeletes.Values)
                    pending.FlushTimer.Dispose();
                _pendingDeletes.Clear();
            }
        }

        /// <summary>Path of fullPath relative to watcher's own root (e.g.
        /// "C:\Studio\Portrait\ClientX\a.jpg" relative to a watcher rooted at "C:\Studio\Portrait"
        /// becomes the containing folder "ClientX"), or null if fullPath sits directly at the
        /// watcher's own root with no sub-folder. Null watcher (shouldn't normally happen) also
        /// yields null.</summary>
        private static string ComputeSubPath(FileSystemWatcher watcher, string fullPath)
        {
            if (watcher == null) return null;
            string relativeDir = Path.GetDirectoryName(GetRelativePath(watcher.Path, fullPath));
            return string.IsNullOrEmpty(relativeDir) ? null : relativeDir;
        }

        /// <summary>.NET Framework 4.6 has no built-in Path.GetRelativePath (that's a .NET Core /
        /// 4.8+ API) - this is the minimal equivalent needed above: "C:\Studio\Portrait\ClientX\a.jpg"
        /// relative to "C:\Studio\Portrait" becomes "ClientX\a.jpg". Falls back to just the file
        /// name if fullPath isn't actually under basePath for some reason.</summary>
        private static string GetRelativePath(string basePath, string fullPath)
        {
            try
            {
                string baseFull = Path.GetFullPath(basePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;
                string target = Path.GetFullPath(fullPath);
                if (target.StartsWith(baseFull, StringComparison.OrdinalIgnoreCase))
                    return target.Substring(baseFull.Length);
                return Path.GetFileName(fullPath);
            }
            catch
            {
                return Path.GetFileName(fullPath);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                lock (_watcherLock)
                {
                    foreach (var w in _folderWatchers)
                    {
                        w.EnableRaisingEvents = false;
                        w.Dispose();
                    }
                    _folderWatchers.Clear();
                    foreach (var pending in _pendingDeletes.Values)
                        pending.FlushTimer.Dispose();
                    _pendingDeletes.Clear();
                }
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

                    // ResultCharges holds the main charge plus one entry per optional extra the
                    // cashier ticked (see CategoryAddOn) - each is recorded as its own line, and
                    // any stock item among them is consumed exactly like a standalone charge, so
                    // deleting one line later restores just that line's stock.
                    foreach (var charge in dlg.ResultCharges)
                    {
                        charge.WorkDate = settings.CurrentWorkDate;
                        _chargeRepo.Add(charge);

                        if (charge.LinkedStockItemId.HasValue && charge.Quantity.HasValue && charge.Quantity.Value > 0)
                        {
                            _stockRepo.AdjustQuantity(charge.LinkedStockItemId.Value, -charge.Quantity.Value);
                            var item = _stockRepo.GetById(charge.LinkedStockItemId.Value);
                            if (item != null && item.IsOutOfStock)
                                MessageBox.Show($"'{item.Name}' est maintenant en rupture de stock.", "Alerte stock", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
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

        /// <summary>Shows what today's totals currently look like - exactly the same breakdown
        /// screen as an actual close, computed live - WITHOUT closing the day: nothing is
        /// purged, moved, archived, or saved, and the work date doesn't advance. Password-
        /// protected like Clôturer la journée, so the check itself is recorded in the login log
        /// even though the action it protects has no side effect of its own to log.</summary>
        private void ViewCurrentStateBtn_Click(object sender, EventArgs e)
        {
            if (!AuthGate.RequireAdminPassword(this, "Voir l'état actuel (sans clôturer)", "Voir l'état actuel"))
                return;

            try
            {
                var preview = _closingService.PreviewCurrentState();
                using (var breakdown = new BreakdownReportForm(preview, isPreview: true))
                    breakdown.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Une erreur inattendue est survenue lors du calcul de l'état actuel :\n" + ex.Message,
                    "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CloseDayBtn_Click(object sender, EventArgs e)
        {
            if (!AuthGate.RequireAdminPassword(this, "Clôturer la journée", "Clôturer la journée"))
                return; // annule, trop de tentatives, ou pas de mot de passe defini

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
