using System;
using System.IO;
using System.Windows.Forms;
using PhotoStudioApp.Data;
using PhotoStudioApp.Services;
using PhotoStudioApp.UI.Dialogs;

namespace PhotoStudioApp.UI
{
    /// <summary>Accueil / Aujourd'hui : nombre de photos en direct par categorie, prix
    /// unitaire, charges additionnelles du jour, et le bouton pour cloturer la journee.</summary>
    public class HomeTab : UserControl
    {
        public event EventHandler DayClosed;

        private readonly CategoryRepository _categoryRepo = new CategoryRepository();
        private readonly CustomChargeRepository _chargeRepo = new CustomChargeRepository();
        private readonly StockRepository _stockRepo = new StockRepository();
        private readonly SettingsRepository _settingsRepo = new SettingsRepository();
        private readonly FolderCountService _folders = new FolderCountService();
        private readonly ClosingService _closingService = new ClosingService();

        private DataGridView _categoryGrid;
        private DataGridView _chargesGrid;
        private Label _dateLabel;
        private Timer _autoRefreshTimer;

        public HomeTab()
        {
            BackColor = Theme.Background;
            BuildLayout();
            RefreshAll();

            _autoRefreshTimer = new Timer { Interval = 10_000 }; // actualisation auto toutes les 10s
            _autoRefreshTimer.Tick += (s, e) => RefreshCategoryCountsOnly();
            _autoRefreshTimer.Start();
        }

        private void BuildLayout()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(16)
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 35));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // --- En-tete : date + bouton actualiser ---
            var headerPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
            _dateLabel = new Label { Font = Theme.FontHeading, ForeColor = Theme.TextDark, AutoSize = true, Margin = new Padding(0, 0, 20, 10) };
            var refreshBtn = Theme.MakeSecondaryButton("⟳ Actualiser");
            refreshBtn.Width = 130;
            refreshBtn.Click += (s, e) => RefreshAll();
            headerPanel.Controls.Add(_dateLabel);
            headerPanel.Controls.Add(refreshBtn);
            root.Controls.Add(headerPanel, 0, 0);

            // --- Tableau des categories ---
            var categoryPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.CardBackground, Padding = new Padding(1) };
            _categoryGrid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true };
            Theme.StyleGrid(_categoryGrid);
            _categoryGrid.Columns.Add("Category", "Categorie");
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

            // --- Pied de page : bouton Cloturer la journee (pas d'apercu de total, sur demande) ---
            var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Margin = new Padding(0, 12, 0, 0) };
            var closeDayBtn = Theme.MakePrimaryButton("🔒 Cloturer la journee");
            closeDayBtn.Width = 240;
            closeDayBtn.Height = 46;
            closeDayBtn.Click += CloseDayBtn_Click;
            footer.Controls.Add(closeDayBtn);
            root.Controls.Add(footer, 0, 3);

            Controls.Add(root);
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

        public void RefreshCustomChargeStockOptions()
        {
            // Point d'extension : le dialogue "Ajouter une charge" recharge le stock et les
            // categories a chaque ouverture, donc rien a mettre en cache ici.
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
                    // Restitue toute quantite de stock precedemment consommee avant d'appliquer la nouvelle.
                    if (existing.LinkedStockItemId.HasValue && existing.Quantity.HasValue)
                        _stockRepo.AdjustQuantity(existing.LinkedStockItemId.Value, existing.Quantity.Value);

                    var updated = dlg.ResultCharge;
                    updated.Id = id;
                    updated.WorkDate = existing.WorkDate;
                    _chargeRepo.Update(updated);

                    if (updated.LinkedStockItemId.HasValue && updated.Quantity.HasValue && updated.Quantity.Value > 0)
                        _stockRepo.AdjustQuantity(updated.LinkedStockItemId.Value, -updated.Quantity.Value);

                    RefreshCharges();
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
                MessageBox.Show("Aucun mot de passe de cloture n'est defini. Definissez-en un dans l'onglet Parametres.",
                    "Mot de passe requis", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var pwd = new PasswordPromptDialog(settings.MaxPasswordAttempts))
            {
                if (pwd.ShowDialog() != DialogResult.OK)
                    return; // annule ou trop de tentatives
            }

            if (MessageBox.Show(
                "Cette action va archiver les photos du jour, cloturer la comptabilite et demarrer une nouvelle journee. Continuer ?",
                "Confirmer la cloture", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
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
                    if (MessageBox.Show(ex.Message, "Journee vide", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
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
                MessageBox.Show(ex.Message, "Impossible de cloturer la journee", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Une erreur inattendue est survenue lors de la cloture :\n" + ex.Message,
                    "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
