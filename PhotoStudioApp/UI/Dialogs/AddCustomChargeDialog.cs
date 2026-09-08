using System;
using System.Linq;
using System.Windows.Forms;
using PhotoStudioApp.Data;
using PhotoStudioApp.Models;

namespace PhotoStudioApp.UI.Dialogs
{
    public class AddCustomChargeDialog : Form
    {
        public CustomCharge ResultCharge { get; private set; }

        private readonly TextBox _descBox;
        private readonly NumericUpDown _amountBox;
        private readonly ComboBox _stockCombo;
        private readonly NumericUpDown _qtyBox;
        private readonly CheckBox _autoAmountCheck;
        private readonly StockRepository _stockRepo = new StockRepository();
        private System.Collections.Generic.List<StockItem> _stockItems;

        public AddCustomChargeDialog(CustomCharge existing = null)
        {
            Text = existing == null ? "Add custom / extra charge" : "Edit custom / extra charge";
            Width = 460;
            Height = 380;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            Font = Theme.FontBase;
            BackColor = Theme.CardBackground;

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(20), RowCount = 7 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            layout.Controls.Add(new Label { Text = "Description:", AutoSize = true, Margin = new Padding(0, 8, 0, 0) }, 0, 0);
            _descBox = new TextBox { Width = 230, Text = existing?.Description ?? "" };
            layout.Controls.Add(_descBox, 1, 0);

            layout.Controls.Add(new Label { Text = "Link to stock item\n(optional):", AutoSize = true, Margin = new Padding(0, 8, 0, 0) }, 0, 1);
            _stockCombo = new ComboBox { Width = 260, DropDownStyle = ComboBoxStyle.DropDownList };
            _stockItems = _stockRepo.GetAll(activeOnly: true);
            _stockCombo.Items.Add("(none - manual charge)");
            foreach (var item in _stockItems)
            {
                string label = item.Name + (item.IsOutOfStock ? "  [OUT OF STOCK]" : $"  (qty: {item.Quantity})");
                _stockCombo.Items.Add(label);
            }
            _stockCombo.SelectedIndex = 0;
            _stockCombo.SelectedIndexChanged += StockCombo_SelectedIndexChanged;
            layout.Controls.Add(_stockCombo, 1, 1);

            layout.Controls.Add(new Label { Text = "Quantity used:", AutoSize = true, Margin = new Padding(0, 8, 0, 0) }, 0, 2);
            _qtyBox = new NumericUpDown { Width = 100, Minimum = 0, Maximum = 100000, Enabled = false, Value = existing?.Quantity ?? 0 };
            _qtyBox.ValueChanged += (s, e) => RecomputeAutoAmount();
            layout.Controls.Add(_qtyBox, 1, 2);

            layout.Controls.Add(new Label { Text = "Use stock unit price\nfor amount:", AutoSize = true, Margin = new Padding(0, 8, 0, 0) }, 0, 3);
            _autoAmountCheck = new CheckBox { Checked = existing == null, Enabled = false };
            _autoAmountCheck.CheckedChanged += (s, e) => RecomputeAutoAmount();
            layout.Controls.Add(_autoAmountCheck, 1, 3);

            layout.Controls.Add(new Label { Text = "Amount:", AutoSize = true, Margin = new Padding(0, 8, 0, 0) }, 0, 4);
            _amountBox = new NumericUpDown { Width = 120, Minimum = 0, Maximum = 1000000, DecimalPlaces = 2, Value = existing?.Amount ?? 0 };
            layout.Controls.Add(_amountBox, 1, 4);

            var btnPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill };
            var okBtn = Theme.MakePrimaryButton("Save");
            okBtn.Width = 100;
            okBtn.Click += OkBtn_Click;
            var cancelBtn = Theme.MakeSecondaryButton("Cancel");
            cancelBtn.Width = 100;
            cancelBtn.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            btnPanel.Controls.Add(okBtn);
            btnPanel.Controls.Add(cancelBtn);
            layout.Controls.Add(btnPanel, 1, 6);

            Controls.Add(layout);
            AcceptButton = okBtn;
            CancelButton = cancelBtn;

            if (existing?.LinkedStockItemId != null)
            {
                int idx = _stockItems.FindIndex(i => i.Id == existing.LinkedStockItemId.Value);
                if (idx >= 0) _stockCombo.SelectedIndex = idx + 1;
            }
        }

        private StockItem SelectedStockItem() =>
            _stockCombo.SelectedIndex > 0 ? _stockItems[_stockCombo.SelectedIndex - 1] : null;

        private void StockCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            var item = SelectedStockItem();
            bool linked = item != null;
            _qtyBox.Enabled = linked;
            _autoAmountCheck.Enabled = linked;
            if (linked)
            {
                _qtyBox.Value = Math.Max(1, _qtyBox.Value);
                RecomputeAutoAmount();
            }
        }

        private void RecomputeAutoAmount()
        {
            var item = SelectedStockItem();
            if (item != null && _autoAmountCheck.Checked)
            {
                _amountBox.Value = Math.Min(_amountBox.Maximum, item.UnitPrice * _qtyBox.Value);
                _amountBox.Enabled = false;
            }
            else
            {
                _amountBox.Enabled = true;
            }
        }

        private void OkBtn_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_descBox.Text))
            {
                MessageBox.Show("Description is required.", "Missing info", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var item = SelectedStockItem();
            if (item != null && item.IsOutOfStock)
            {
                if (MessageBox.Show($"'{item.Name}' is out of stock. Add this charge anyway?",
                    "Out of stock", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;
            }
            if (item != null && _qtyBox.Value > item.Quantity)
            {
                if (MessageBox.Show($"Only {item.Quantity} of '{item.Name}' left, but {_qtyBox.Value} were requested. Continue anyway?",
                    "Not enough stock", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;
            }

            ResultCharge = new CustomCharge
            {
                Description = _descBox.Text.Trim(),
                Amount = _amountBox.Value,
                LinkedStockItemId = item?.Id,
                Quantity = item != null ? (int)_qtyBox.Value : (int?)null
            };
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
