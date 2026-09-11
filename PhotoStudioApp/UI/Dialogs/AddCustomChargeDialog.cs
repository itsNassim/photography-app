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

        /// <summary>One dropdown entry - a stock item, a fixed-price no-folder category, an
        /// outstanding pending balance being paid off, or the "none / manual" placeholder.</summary>
        private class LinkOption
        {
            public string Label;
            public StockItem Stock;
            public Category ServiceCategory;
            public PendingBalance Balance;
            public override string ToString() => Label;
        }

        private readonly TextBox _descBox;
        private readonly NumericUpDown _amountBox;
        private readonly NumericUpDown _paidAmountBox;
        private readonly TextBox _searchBox;
        private readonly ComboBox _linkCombo;
        private readonly NumericUpDown _qtyBox;
        private readonly CheckBox _autoAmountCheck;
        private readonly StockRepository _stockRepo = new StockRepository();
        private readonly CategoryRepository _categoryRepo = new CategoryRepository();
        private readonly PendingBalanceRepository _pendingRepo = new PendingBalanceRepository();
        private List<LinkOption> _allOptions = new List<LinkOption>();
        private bool _restoringExisting;
        private decimal _lastSyncedPaidValue;

        public AddCustomChargeDialog(CustomCharge existing = null, PendingBalance preselectedBalance = null)
        {
            Text = existing == null ? "Ajouter une charge additionnelle" : "Modifier la charge additionnelle";
            Width = 480;
            Height = 480;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            Font = Theme.FontBase;
            BackColor = Theme.CardBackground;

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(20), RowCount = 8 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            layout.Controls.Add(new Label { Text = "Rechercher (nom ou\ncategorie) :", AutoSize = true, Margin = new Padding(0, 8, 0, 0) }, 0, 0);
            _searchBox = new TextBox { Width = 270 };
            _searchBox.TextChanged += (s, e) => ApplySearchFilter();
            layout.Controls.Add(_searchBox, 1, 0);

            layout.Controls.Add(new Label { Text = "Categorie, article de stock,\nou solde en attente (optionnel) :", AutoSize = true, Margin = new Padding(0, 8, 0, 0) }, 0, 1);
            _linkCombo = new ComboBox { Width = 270, DropDownStyle = ComboBoxStyle.DropDownList };
            layout.Controls.Add(_linkCombo, 1, 1);

            layout.Controls.Add(new Label { Text = "Description :", AutoSize = true, Margin = new Padding(0, 8, 0, 0) }, 0, 2);
            _descBox = new TextBox { Width = 270, Text = existing?.Description ?? "" };
            layout.Controls.Add(_descBox, 1, 2);

            layout.Controls.Add(new Label { Text = "Quantite :", AutoSize = true, Margin = new Padding(0, 8, 0, 0) }, 0, 3);
            _qtyBox = new NumericUpDown { Width = 100, Minimum = 0, Maximum = 100000, Enabled = false, Value = existing?.Quantity ?? 0 };
            _qtyBox.ValueChanged += (s, e) => RecomputeAutoAmount();
            layout.Controls.Add(_qtyBox, 1, 3);

            layout.Controls.Add(new Label { Text = "Utiliser le prix du lien\npour le montant :", AutoSize = true, Margin = new Padding(0, 8, 0, 0) }, 0, 4);
            _autoAmountCheck = new CheckBox { Checked = existing == null, Enabled = false };
            _autoAmountCheck.CheckedChanged += (s, e) => RecomputeAutoAmount();
            layout.Controls.Add(_autoAmountCheck, 1, 4);

            layout.Controls.Add(new Label { Text = "Montant total :", AutoSize = true, Margin = new Padding(0, 8, 0, 0) }, 0, 5);
            _amountBox = new NumericUpDown { Width = 120, Minimum = 0, Maximum = 1000000, DecimalPlaces = 2, Value = existing?.Amount ?? 0 };
            _amountBox.ValueChanged += (s, e) => SyncPaidToAmountIfUnlinkedDefault();
            layout.Controls.Add(_amountBox, 1, 5);

            // "Montant paye maintenant" comes LAST (after Montant/quantite/etc. are settled) so
            // filling in the rest of the form never overwrites a partial-payment amount the user
            // already typed here - see SyncPaidToAmountIfUnlinkedDefault.
            layout.Controls.Add(new Label { Text = "Montant paye maintenant\n(si different, le reste devient\nun solde en attente) :", AutoSize = true, Margin = new Padding(0, 8, 0, 0) }, 0, 6);
            _paidAmountBox = new NumericUpDown { Width = 120, Minimum = 0, Maximum = 1000000, DecimalPlaces = 2, Value = existing?.Amount ?? 0 };
            layout.Controls.Add(_paidAmountBox, 1, 6);

            var btnPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill };
            var okBtn = Theme.MakePrimaryButton("Enregistrer");
            okBtn.Width = 110;
            okBtn.Click += OkBtn_Click;
            var cancelBtn = Theme.MakeSecondaryButton("Annuler");
            cancelBtn.Width = 100;
            cancelBtn.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            btnPanel.Controls.Add(okBtn);
            btnPanel.Controls.Add(cancelBtn);
            layout.Controls.Add(btnPanel, 1, 7);

            Controls.Add(layout);
            AcceptButton = okBtn;
            CancelButton = cancelBtn;

            // Populate the dropdown (and wire its change handler) only now that every field it
            // touches (_qtyBox, _autoAmountCheck, _descBox, _amountBox, _paidAmountBox) actually
            // exists - this used to run right after _linkCombo was created, before those fields
            // existed yet, which threw a NullReferenceException the instant the dialog opened.
            BuildLinkOptions();

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
            else if (preselectedBalance != null)
            {
                // Pre-select a pending balance so this dialog opens ready to record a payment
                // toward it - used by the Home tab's "Soldes en attente" section's "Payer"
                // button. Not wrapped in _restoringExisting: we WANT the normal auto-fill
                // (description + amount = remaining balance) that a fresh selection triggers.
                int idx = FindOptionIndex(o => o.Balance != null && o.Balance.Id == preselectedBalance.Id);
                if (idx >= 0)
                    _linkCombo.SelectedIndex = idx;
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
            _allOptions = new List<LinkOption> { new LinkOption { Label = "(aucun - saisie manuelle)" } };

            var pendingBalances = _pendingRepo.GetAll();
            foreach (var bal in pendingBalances)
            {
                string label = $"[Solde en attente] {bal.Description}  (reste : {bal.RemainingAmount:0.00})";
                _allOptions.Add(new LinkOption { Label = label, Balance = bal });
            }

            var stockItems = _stockRepo.GetAll(activeOnly: true);
            foreach (var item in stockItems)
            {
                string statusSuffix = item.IsOutOfStock
                    ? "RUPTURE DE STOCK"
                    : item.IsLowStock
                        ? "STOCK FAIBLE"
                        : $"qte : {item.Quantity}";
                string categoryPart = string.IsNullOrWhiteSpace(item.Category) ? "" : $"  [{item.Category}]";
                string label = $"[Stock] {item.Name}{categoryPart}  ({statusSuffix})";
                _allOptions.Add(new LinkOption { Label = label, Stock = item });
            }

            var serviceCategories = _categoryRepo.GetActiveServiceCategories();
            foreach (var cat in serviceCategories)
            {
                string label = $"[Categorie] {cat.Name}  ({cat.CurrentPrice:0.00} / unite)";
                _allOptions.Add(new LinkOption { Label = label, ServiceCategory = cat });
            }

            RepopulateCombo(_allOptions);
        }

        /// <summary>Filters the dropdown live as the user types in the search box, by item/stock
        /// name, stock category, service-category name, or pending-balance description - makes
        /// finding the right entry quick even when there are many to scroll through.</summary>
        private void ApplySearchFilter()
        {
            string term = _searchBox.Text?.Trim() ?? "";
            if (term.Length == 0)
            {
                RepopulateCombo(_allOptions);
                return;
            }

            var filtered = new List<LinkOption> { _allOptions[0] }; // always keep "(aucun...)"
            foreach (var o in _allOptions.Skip(1))
            {
                bool matches =
                    (o.Stock != null && (
                        o.Stock.Name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        (o.Stock.Category ?? "").IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)) ||
                    (o.ServiceCategory != null &&
                        o.ServiceCategory.Name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (o.Balance != null &&
                        o.Balance.Description.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
                if (matches)
                    filtered.Add(o);
            }
            RepopulateCombo(filtered);
        }

        private void RepopulateCombo(List<LinkOption> options)
        {
            var previouslySelected = SelectedOption();
            _linkCombo.SelectedIndexChanged -= LinkCombo_SelectedIndexChanged;
            _linkCombo.Items.Clear();
            foreach (var o in options)
                _linkCombo.Items.Add(o);

            int idx = 0;
            if (previouslySelected != null)
            {
                for (int i = 0; i < options.Count; i++)
                {
                    if (ReferenceEquals(options[i], previouslySelected)) { idx = i; break; }
                }
            }
            _linkCombo.SelectedIndexChanged += LinkCombo_SelectedIndexChanged;
            if (_linkCombo.Items.Count > 0)
                _linkCombo.SelectedIndex = idx;
        }

        private LinkOption SelectedOption() => _linkCombo.SelectedItem as LinkOption;

        private void LinkCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            var option = SelectedOption();
            bool linkedToPriced = option?.Stock != null || option?.ServiceCategory != null;
            bool linkedToBalance = option?.Balance != null;

            _qtyBox.Enabled = linkedToPriced;
            _autoAmountCheck.Enabled = linkedToPriced;

            if (linkedToBalance)
            {
                // Paying off (or partially paying) an existing balance: the total is fixed at
                // whatever's still owed - not editable here, since that total was already set
                // when the balance was first created.
                _amountBox.Value = Math.Min(_amountBox.Maximum, option.Balance.RemainingAmount);
                _amountBox.Enabled = false;
                _paidAmountBox.Value = _amountBox.Value;

                if (!_restoringExisting)
                {
                    _descBox.Text = option.Balance.Description;
                    _descBox.SelectionStart = _descBox.Text.Length;
                    _descBox.Focus();
                }
                return;
            }

            if (linkedToPriced)
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
            else
            {
                _amountBox.Enabled = true;
            }
        }

        private void RecomputeAutoAmount()
        {
            var option = SelectedOption();
            if (option?.Balance != null) return; // amount is fixed to the remaining balance instead

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

            SyncPaidToAmountIfUnlinkedDefault();
        }

        /// <summary>Keeps "Montant paye maintenant" defaulted to the full Montant as the user
        /// fills in the rest of the form (so the common case - fully paid - needs no extra
        /// step), WITHOUT clobbering a partial amount the user may have already typed there:
        /// this only re-syncs the value while the two boxes still match (i.e. nothing has
        /// diverged them yet). Once the user edits "Montant paye maintenant" to anything
        /// different from Montant, this stops touching it.</summary>
        private void SyncPaidToAmountIfUnlinkedDefault()
        {
            if (_paidAmountBox.Value == _lastSyncedPaidValue)
                _paidAmountBox.Value = Math.Min(_paidAmountBox.Maximum, _amountBox.Value);
            _lastSyncedPaidValue = _paidAmountBox.Value;
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

            decimal total = _amountBox.Value;
            decimal paidNow = Math.Min(_paidAmountBox.Value, total);
            decimal shortfall = total - paidNow;

            if (shortfall > 0.005m && !string.IsNullOrWhiteSpace(_descBox.Text))
            {
                string msg = option?.Balance != null
                    ? $"{paidNow:0.00} paye maintenant sur ce solde - il restera {shortfall:0.00} en attente. Continuer ?"
                    : $"Le client paye {paidNow:0.00} sur un total de {total:0.00} - un solde en attente de {shortfall:0.00} sera cree pour \"{_descBox.Text.Trim()}\". Continuer ?";
                if (MessageBox.Show(msg, "Paiement partiel", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;
            }

            // Only the amount actually collected today counts as this charge's amount - it's
            // what gets added to TODAY's revenue total, per the "paid on the day it's paid"
            // rule. The unpaid shortfall (if any) is tracked separately as a pending balance,
            // never as part of today's total.
            ResultCharge = new CustomCharge
            {
                Description = _descBox.Text.Trim(),
                Amount = paidNow,
                LinkedStockItemId = option?.Stock?.Id,
                LinkedCategoryId = option?.ServiceCategory?.Id,
                Quantity = (option?.Stock != null || option?.ServiceCategory != null) ? (int)_qtyBox.Value : (int?)null
            };

            if (option?.Balance != null)
            {
                _pendingRepo.ReduceOrDelete(option.Balance.Id, paidNow);
            }
            else if (shortfall > 0.005m)
            {
                _pendingRepo.Add(_descBox.Text.Trim(), shortfall, DateTime.Now.ToString("yyyy-MM-dd"));
            }

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
