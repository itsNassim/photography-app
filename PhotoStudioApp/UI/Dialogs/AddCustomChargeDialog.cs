using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using PhotoStudioApp.Data;
using PhotoStudioApp.Models;

namespace PhotoStudioApp.UI.Dialogs
{
    public class AddCustomChargeDialog : Form
    {
        public CustomCharge ResultCharge { get; private set; }

        /// <summary>One dropdown entry - either a stock item, a fixed-price no-folder
        /// category, or the "none / manual" placeholder.</summary>
        private class LinkOption
        {
            public string Label;
            public StockItem Stock;
            public Category ServiceCategory;
            public override string ToString() => Label;
        }

        private readonly TextBox _descBox;
        private readonly NumericUpDown _amountBox;
        private readonly ComboBox _linkCombo;
        private readonly NumericUpDown _qtyBox;
        private readonly CheckBox _autoAmountCheck;
        private readonly StockRepository _stockRepo = new StockRepository();
        private readonly CategoryRepository _categoryRepo = new CategoryRepository();
        private bool _restoringExisting;

        public AddCustomChargeDialog(CustomCharge existing = null)
        {
            Text = existing == null ? "Ajouter une charge additionnelle" : "Modifier la charge additionnelle";
            Width = 480;
            Height = 400;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            Font = Theme.FontBase;
            BackColor = Theme.CardBackground;

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(20), RowCount = 7 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            layout.Controls.Add(new Label { Text = "Categorie ou article de stock\n(optionnel) :", AutoSize = true, Margin = new Padding(0, 8, 0, 0) }, 0, 0);
            _linkCombo = new ComboBox { Width = 270, DropDownStyle = ComboBoxStyle.DropDownList };
            BuildLinkOptions();
            _linkCombo.SelectedIndex = 0;
            _linkCombo.SelectedIndexChanged += LinkCombo_SelectedIndexChanged;
            layout.Controls.Add(_linkCombo, 1, 0);

            layout.Controls.Add(new Label { Text = "Description :", AutoSize = true, Margin = new Padding(0, 8, 0, 0) }, 0, 1);
            _descBox = new TextBox { Width = 270, Text = existing?.Description ?? "" };
            layout.Controls.Add(_descBox, 1, 1);

            layout.Controls.Add(new Label { Text = "Quantite :", AutoSize = true, Margin = new Padding(0, 8, 0, 0) }, 0, 2);
            _qtyBox = new NumericUpDown { Width = 100, Minimum = 0, Maximum = 100000, Enabled = false, Value = existing?.Quantity ?? 0 };
            _qtyBox.ValueChanged += (s, e) => RecomputeAutoAmount();
            layout.Controls.Add(_qtyBox, 1, 2);

            layout.Controls.Add(new Label { Text = "Utiliser le prix du lien\npour le montant :", AutoSize = true, Margin = new Padding(0, 8, 0, 0) }, 0, 3);
            _autoAmountCheck = new CheckBox { Checked = existing == null, Enabled = false };
            _autoAmountCheck.CheckedChanged += (s, e) => RecomputeAutoAmount();
            layout.Controls.Add(_autoAmountCheck, 1, 3);

            layout.Controls.Add(new Label { Text = "Montant :", AutoSize = true, Margin = new Padding(0, 8, 0, 0) }, 0, 4);
            _amountBox = new NumericUpDown { Width = 120, Minimum = 0, Maximum = 1000000, DecimalPlaces = 2, Value = existing?.Amount ?? 0 };
            layout.Controls.Add(_amountBox, 1, 4);

            var btnPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill };
            var okBtn = Theme.MakePrimaryButton("Enregistrer");
            okBtn.Width = 110;
            okBtn.Click += OkBtn_Click;
            var cancelBtn = Theme.MakeSecondaryButton("Annuler");
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
                int idx = FindOptionIndex(o => o.Stock != null && o.Stock.Id == existing.LinkedStockItemId.Value);
                if (idx >= 0)
                {
                    _restoringExisting = true;
                    _linkCombo.SelectedIndex = idx;
                    _restoringExisting = false;
                }
            }
            else if (existing?.LinkedCategoryId != null)
            {
                int idx = FindOptionIndex(o => o.ServiceCategory != null && o.ServiceCategory.Id == existing.LinkedCategoryId.Value);
                if (idx >= 0)
                {
                    _restoringExisting = true;
                    _linkCombo.SelectedIndex = idx;
                    _restoringExisting = false;
                }
            }

            // Restore the saved description LAST, after any selection-restore above may have
            // touched the textbox, so an edited charge keeps exactly what was saved.
            if (existing != null)
                _descBox.Text = existing.Description ?? "";
        }

        private int FindOptionIndex(Func<LinkOption, bool> predicate)
        {
            for (int i = 0; i < _linkCombo.Items.Count; i++)
                if (_linkCombo.Items[i] is LinkOption o && predicate(o))
                    return i;
            return -1;
        }

        private void BuildLinkOptions()
        {
            _linkCombo.Items.Add(new LinkOption { Label = "(aucun - saisie manuelle)" });

            var stockItems = _stockRepo.GetAll(activeOnly: true);
            foreach (var item in stockItems)
            {
                string label = "[Stock] " + item.Name + (item.IsOutOfStock ? "  (RUPTURE DE STOCK)" : $"  (qte : {item.Quantity})");
                _linkCombo.Items.Add(new LinkOption { Label = label, Stock = item });
            }

            var serviceCategories = _categoryRepo.GetActiveServiceCategories();
            foreach (var cat in serviceCategories)
            {
                string label = $"[Categorie] {cat.Name}  ({cat.CurrentPrice:0.00} / unite)";
                _linkCombo.Items.Add(new LinkOption { Label = label, ServiceCategory = cat });
            }
        }

        private LinkOption SelectedOption() => _linkCombo.SelectedItem as LinkOption;

        private void LinkCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            var option = SelectedOption();
            bool linked = option?.Stock != null || option?.ServiceCategory != null;

            _qtyBox.Enabled = linked;
            _autoAmountCheck.Enabled = linked;

            if (linked)
            {
                _qtyBox.Value = Math.Max(1, _qtyBox.Value);

                if (!_restoringExisting)
                {
                    // Fill the description with the linked item's name - the user can keep typing
                    // after it (e.g. to add "- urgent" or similar) without it being cleared.
                    string name = option.Stock?.Name ?? option.ServiceCategory?.Name;
                    _descBox.Text = name;
                    _descBox.SelectionStart = _descBox.Text.Length;
                    _descBox.Focus();
                }

                RecomputeAutoAmount();
            }
        }

        private void RecomputeAutoAmount()
        {
            var option = SelectedOption();
            decimal? unitPrice = option?.Stock?.UnitPrice ?? option?.ServiceCategory?.CurrentPrice;

            if (unitPrice.HasValue && _autoAmountCheck.Checked)
            {
                _amountBox.Value = Math.Min(_amountBox.Maximum, unitPrice.Value * _qtyBox.Value);
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
                MessageBox.Show("La description est obligatoire.", "Information manquante", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var option = SelectedOption();

            if (option?.Stock != null)
            {
                var item = option.Stock;
                if (item.IsOutOfStock)
                {
                    if (MessageBox.Show($"'{item.Name}' est en rupture de stock. Ajouter cette charge quand meme ?",
                        "Rupture de stock", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                        return;
                }
                else if (_qtyBox.Value > item.Quantity)
                {
                    if (MessageBox.Show($"Il ne reste que {item.Quantity} de '{item.Name}', mais {_qtyBox.Value} sont demandes. Continuer quand meme ?",
                        "Stock insuffisant", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                        return;
                }
            }

            ResultCharge = new CustomCharge
            {
                Description = _descBox.Text.Trim(),
                Amount = _amountBox.Value,
                LinkedStockItemId = option?.Stock?.Id,
                LinkedCategoryId = option?.ServiceCategory?.Id,
                Quantity = (option?.Stock != null || option?.ServiceCategory != null) ? (int)_qtyBox.Value : (int?)null
            };
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
