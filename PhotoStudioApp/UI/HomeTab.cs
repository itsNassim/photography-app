using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using PhotoStudioApp.Data;
using PhotoStudioApp.Models;
using PhotoStudioApp.Services;
using PhotoStudioApp.UI.Dialogs;

namespace PhotoStudioApp.UI
{
    /// <summary>Home / Today dashboard: live per-category counts, subtotals, grand total,
    /// today's custom charges, and the Close Day action.</summary>
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
        private Label _grandTotalLabel;
        private Label _dateLabel;
        private Timer _autoRefreshTimer;

        public HomeTab()
        {
            BackColor = Theme.Background;
            BuildLayout();
            RefreshAll();

            _autoRefreshTimer = new Timer { Interval = 10_000 }; // auto-refresh counts every 10s
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
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 30));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // --- Header row: date + refresh button ---
            var headerPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
            _dateLabel = new Label { Font = Theme.FontHeading, ForeColor = Theme.TextDark, AutoSize = true, Margin = new Padding(0, 0, 20, 10) };
            var refreshBtn = Theme.MakeSecondaryButton("⟳ Refresh");
            refreshBtn.Width = 110;
            refreshBtn.Click += (s, e) => RefreshAll();
            headerPanel.Controls.Add(_dateLabel);
            headerPanel.Controls.Add(refreshBtn);
            root.Controls.Add(headerPanel, 0, 0);

            // --- Category grid ---
            var categoryPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.CardBackground, Padding = new Padding(1) };
            _categoryGrid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true };
            Theme.StyleGrid(_categoryGrid);
            _categoryGrid.Columns.Add("Category", "Category");
            _categoryGrid.Columns.Add("Count", "Photos");
            _categoryGrid.Columns.Add("Price", "Unit Price");
            _categoryGrid.Columns.Add("Subtotal", "Subtotal");
            categoryPanel.Controls.Add(_categoryGrid);
            root.Controls.Add(categoryPanel, 0, 1);

            // --- Custom charges section ---
            var chargesOuter = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
            chargesOuter.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            chargesOuter.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var chargesHeader = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
            var chargesTitle = new Label { Text = "Today's custom / extra charges", Font = Theme.FontSubheading, AutoSize = true, Margin = new Padding(0, 6, 20, 6) };
            var addChargeBtn = Theme.MakePrimaryButton("+ Add charge");
            addChargeBtn.Width = 140;
            addChargeBtn.Click += AddChargeBtn_Click;
            var editChargeBtn = Theme.MakeSecondaryButton("Edit");
            editChargeBtn.Width = 90;
            editChargeBtn.Click += EditChargeBtn_Click;
            var deleteChargeBtn = Theme.MakeDangerButton("Delete");
            deleteChargeBtn.Width = 90;
            deleteChargeBtn.Click += DeleteChargeBtn_Click;
            chargesHeader.Controls.Add(chargesTitle);
            chargesHeader.Controls.Add(addChargeBtn);
            chargesHeader.Controls.Add(editChargeBtn);
            chargesHeader.Controls.Add(deleteChargeBtn);

            var chargesPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.CardBackground };
            _chargesGrid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true };
            Theme.StyleGrid(_chargesGrid);
            _chargesGrid.Columns.Add("Description", "Description");
            _chargesGrid.Columns.Add("Linked", "Linked stock item");
            _chargesGrid.Columns.Add("Qty", "Qty");
            _chargesGrid.Columns.Add("Amount", "Amount");
            chargesPanel.Controls.Add(_chargesGrid);

            chargesOuter.Controls.Add(chargesHeader, 0, 0);
            chargesOuter.Controls.Add(chargesPanel, 0, 1);
            root.Controls.Add(chargesOuter, 0, 2);

            // --- Footer: grand total + close day ---
            var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true, Padding = new Padding(0, 12, 0, 0) };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            _grandTotalLabel = new Label { Font = Theme.FontTotal, ForeColor = Theme.Success, AutoSize = true, TextAlign = System.Drawing.ContentAlignment.MiddleLeft };
            var closeDayBtn = Theme.MakePrimaryButton("🔒 Close Day");
            closeDayBtn.Width = 220;
            closeDayBtn.Height = 46;
            closeDayBtn.Anchor = AnchorStyles.Right;
            closeDayBtn.Click += CloseDayBtn_Click;

            var footerLeft = new Panel { Dock = DockStyle.Fill };
            footerLeft.Controls.Add(_grandTotalLabel);
            var footerRight = new FlowLayoutPanel { Dock = DockStyle.Right, FlowDirection = FlowDirection.RightToLeft, AutoSize = true };
            footerRight.Controls.Add(closeDayBtn);

            footer.Controls.Add(footerLeft, 0, 0);
            footer.Controls.Add(footerRight, 1, 0);
            root.Controls.Add(footer, 0, 3);

            Controls.Add(root);
        }

        public void RefreshAll()
        {
            var settings = _settingsRepo.LoadAll();
            string workDateStr = settings.CurrentWorkDate ?? DateTime.Today.ToString("yyyy-MM-dd");
            DateTime workDate = DateTime.TryParseExact(workDateStr, "yyyy-MM-dd", null,
                System.Globalization.DateTimeStyles.None, out var d) ? d : DateTime.Today;
            _dateLabel.Text = $"Today: {workDate:dddd, dd MMMM yyyy}";

            RefreshCategoryCountsOnly();
            RefreshCharges();
        }

        public void RefreshCategoryCountsOnly()
        {
            var settings = _settingsRepo.LoadAll();
            var categories = _categoryRepo.GetActiveCategories();
            _categoryGrid.Rows.Clear();

            decimal categoryTotal = 0;
            foreach (var cat in categories)
            {
                string abs = string.IsNullOrWhiteSpace(settings.StudioRootPath)
                    ? null
                    : Path.Combine(settings.StudioRootPath, cat.FolderRelativePath);
                int count = abs == null ? 0 : _folders.CountPhotos(abs);
                cat.LiveCount = count;
                categoryTotal += cat.Subtotal;

                int rowIdx = _categoryGrid.Rows.Add(cat.Name, count, cat.CurrentPrice.ToString("0.00"), cat.Subtotal.ToString("0.00"));
                if (abs != null && !Directory.Exists(abs))
                {
                    _categoryGrid.Rows[rowIdx].DefaultCellStyle.ForeColor = Theme.Warning;
                    _categoryGrid.Rows[rowIdx].Cells[0].ToolTipText = "Folder not found on disk";
                }
            }

            RecomputeGrandTotal();
        }

        private void RefreshCharges()
        {
            var settings = _settingsRepo.LoadAll();
            var charges = _chargeRepo.GetForWorkDate(settings.CurrentWorkDate);
            _chargesGrid.Rows.Clear();
            foreach (var c in charges)
            {
                int rowIdx = _chargesGrid.Rows.Add(c.Description, c.LinkedStockItemName ?? "-", c.Quantity?.ToString() ?? "-", c.Amount.ToString("0.00"));
                _chargesGrid.Rows[rowIdx].Tag = c.Id;
            }
            RecomputeGrandTotal();
        }

        private void RecomputeGrandTotal()
        {
            decimal categoryTotal = 0;
            foreach (DataGridViewRow row in _categoryGrid.Rows)
                categoryTotal += decimal.Parse(row.Cells["Subtotal"].Value.ToString());

            decimal chargeTotal = 0;
            foreach (DataGridViewRow row in _chargesGrid.Rows)
                chargeTotal += decimal.Parse(row.Cells["Amount"].Value.ToString());

            _grandTotalLabel.Text = $"GRAND TOTAL: {categoryTotal + chargeTotal:0.00}";
        }

        public void RefreshCustomChargeStockOptions()
        {
            // Placeholder hook: the Add Charge dialog re-queries stock live each time it opens,
            // so nothing to pre-cache here - kept for interface symmetry with other tabs.
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
                        int newQty = _stockRepo.AdjustQuantity(charge.LinkedStockItemId.Value, -charge.Quantity.Value);
                        var item = _stockRepo.GetById(charge.LinkedStockItemId.Value);
                        if (item != null && item.IsOutOfStock)
                            MessageBox.Show($"'{item.Name}' is now out of stock.", "Stock alert", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                    // Restock any previously-consumed quantity before applying the new one.
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

            if (MessageBox.Show("Delete this charge? If it consumed stock, that stock will be returned.",
                "Confirm delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
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
                MessageBox.Show("No closing password has been set yet. Set one in the Settings tab first.",
                    "Password required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var pwd = new PasswordPromptDialog(settings.MaxPasswordAttempts))
            {
                if (pwd.ShowDialog() != DialogResult.OK)
                    return; // cancelled or too many wrong attempts
            }

            if (MessageBox.Show(
                "This will archive today's photos, close the books for today, and start a new working day. Continue?",
                "Confirm close day", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            try
            {
                DailyClose close;
                try
                {
                    close = _closingService.CloseDay();
                }
                catch (EmptyDayException ex)
                {
                    if (MessageBox.Show(ex.Message, "Empty day", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
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
                MessageBox.Show(ex.Message, "Could not close the day", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show("An unexpected error occurred while closing the day:\n" + ex.Message,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
