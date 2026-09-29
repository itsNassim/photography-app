using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using PhotoStudioApp.Data;
using PhotoStudioApp.Models;
using PhotoStudioApp.UI.Controls;

namespace PhotoStudioApp.UI.Dialogs
{
    public class AddCustomChargeDialog : Form
    {
        /// <summary>The main charge (always present once the dialog returns OK). Kept for the
        /// callers that only ever deal with a single charge (editing one, paying a balance) - when
        /// optional extras were ticked, this is just the first entry of ResultCharges.</summary>
        public CustomCharge ResultCharge { get; private set; }

        /// <summary>Every charge to record: the main one first, then one per ticked extra (see
        /// CategoryAddOn). Always at least one entry after an OK result.</summary>
        public List<CustomCharge> ResultCharges { get; private set; } = new List<CustomCharge>();

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

        /// <summary>One optional extra offered for the chosen category: a tick-box, a quantity
        /// (e.g. number of people for a per-person price) and its computed line total.</summary>
        private class ExtraRow
        {
            public string Name;
            public Category Category;
            public StockItem Stock;
            public CheckBox Check;
            public NumericUpDown Qty;
            public Label LineLabel;

            public decimal UnitPrice => Stock != null ? Stock.UnitPrice : Category.CurrentPrice;
            public decimal Nominal => UnitPrice * Qty.Value;
        }

        private const int BaseHeight = 440;

        private readonly TextBox _descBox;
        private readonly NumericUpDown _amountBox;
        private readonly Label _amountLabel;
        private readonly NumericUpDown _paidAmountBox;
        private readonly SearchableComboBox _linkCombo;
        private readonly NumericUpDown _qtyBox;
        private readonly CheckBox _autoAmountCheck;
        private readonly Panel _extrasHost;
        private readonly Panel _extrasScroll;
        private readonly TableLayoutPanel _extrasTable;
        private readonly Label _extrasSummary;
        private readonly StockRepository _stockRepo = new StockRepository();
        private readonly CategoryRepository _categoryRepo = new CategoryRepository();
        private readonly PendingBalanceRepository _pendingRepo = new PendingBalanceRepository();
        private readonly CategoryAddOnRepository _addOnRepo = new CategoryAddOnRepository();
        private readonly List<ExtraRow> _extraRows = new List<ExtraRow>();
        private readonly bool _isEditing;
        private List<LinkOption> _allOptions = new List<LinkOption>();
        private bool _restoringExisting;
        private decimal _lastSyncedPaidValue;

        public AddCustomChargeDialog(CustomCharge existing = null, PendingBalance preselectedBalance = null)
        {
            _isEditing = existing != null;

            Text = existing == null ? "Ajouter une charge additionnelle" : "Modifier la charge additionnelle";
            Width = 660;
            Height = BaseHeight;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            Font = Theme.FontBase;
            BackColor = Theme.CardBackground;

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(20), RowCount = 8 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            // SearchableComboBox instead of a plain ComboBox: its dropdown has its own search
            // box built into it (at the top of the popup list), rather than relying on a
            // separate field or on OS-level autocomplete in the main text area.
            layout.Controls.Add(new Label { Text = "Catégorie, article de stock,\nou solde en attente :", AutoSize = true, Margin = new Padding(0, 8, 0, 0) }, 0, 0);
            _linkCombo = new SearchableComboBox { Width = 390 };
            layout.Controls.Add(_linkCombo, 1, 0);

            layout.Controls.Add(new Label { Text = "Description :", AutoSize = true, Margin = new Padding(0, 8, 0, 0) }, 0, 1);
            _descBox = new TextBox { Width = 390, Text = existing?.Description ?? "" };
            layout.Controls.Add(_descBox, 1, 1);

            layout.Controls.Add(new Label { Text = "Quantité :", AutoSize = true, Margin = new Padding(0, 8, 0, 0) }, 0, 2);
            _qtyBox = new NumericUpDown { Width = 100, Minimum = 0, Maximum = 100000, Enabled = false, Value = existing?.Quantity ?? 0 };
            _qtyBox.ValueChanged += (s, e) => RecomputeAutoAmount();
            layout.Controls.Add(_qtyBox, 1, 2);

            layout.Controls.Add(new Label { Text = "Utiliser le prix du lien\npour le montant :", AutoSize = true, Margin = new Padding(0, 8, 0, 0) }, 0, 3);
            _autoAmountCheck = new CheckBox { Checked = existing == null, Enabled = false };
            _autoAmountCheck.CheckedChanged += (s, e) => RecomputeAutoAmount();
            layout.Controls.Add(_autoAmountCheck, 1, 3);

            // Optional extras for the chosen category (see CategoryAddOn) - hidden unless the
            // selected category actually offers some; filled in by RebuildExtras. Built entirely
            // from docked panels and a table (no hand-placed pixel coordinates or anchors): an
            // earlier version anchored children to the host before it had its real width, which
            // pushed the quantity boxes, totals and summary past the right edge of the window.
            _extrasHost = new Panel { Dock = DockStyle.Fill, Visible = false, Height = 0, Margin = new Padding(0, 6, 0, 6) };

            _extrasTable = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 3
            };
            _extrasTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _extrasTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
            _extrasTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));

            _extrasScroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            _extrasScroll.Controls.Add(_extrasTable);

            var extrasHeader = new Label
            {
                Text = "Extras optionnels (cochez ce que le client prend) :",
                Dock = DockStyle.Top, AutoSize = false, Height = 26,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Font = new System.Drawing.Font(Theme.FontBase, System.Drawing.FontStyle.Bold)
            };
            _extrasSummary = new Label
            {
                Dock = DockStyle.Bottom, AutoSize = false, Height = 28,
                TextAlign = System.Drawing.ContentAlignment.MiddleRight,
                Font = new System.Drawing.Font(Theme.FontBase, System.Drawing.FontStyle.Bold)
            };

            // Docking is resolved back-to-front: the Fill panel is added first so the Top header
            // and Bottom summary claim their strips and the scroll area gets what's left.
            _extrasHost.Controls.Add(_extrasScroll);
            _extrasHost.Controls.Add(extrasHeader);
            _extrasHost.Controls.Add(_extrasSummary);
            layout.Controls.Add(_extrasHost, 0, 4);
            layout.SetColumnSpan(_extrasHost, 2);

            _amountLabel = new Label { Text = "Montant total :", AutoSize = true, Margin = new Padding(0, 8, 0, 0) };
            layout.Controls.Add(_amountLabel, 0, 5);
            _amountBox = new NumericUpDown { Width = 120, Minimum = 0, Maximum = 1000000, DecimalPlaces = 2, Value = existing?.Amount ?? 0 };
            _amountBox.ValueChanged += (s, e) => SyncPaidToAmountIfUnlinkedDefault();
            layout.Controls.Add(_amountBox, 1, 5);

            // "Montant paye maintenant" comes LAST (after Montant/quantité/etc. are settled) so
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
            // touches (_qtyBox, _autoAmountCheck, _descBox, _amountBox, _paidAmountBox, and the
            // extras panel) actually exists - this used to run right after _linkCombo was created,
            // before those fields existed yet, which threw a NullReferenceException the instant
            // the dialog opened.
            BuildLinkOptions();
            _linkCombo.SelectedItemChanged += LinkCombo_SelectedIndexChanged;
            _linkCombo.SelectedItem = _allOptions[0];

            if (existing?.LinkedStockItemId != null)
            {
                var opt = _allOptions.FirstOrDefault(o => o.Stock != null && o.Stock.Id == existing.LinkedStockItemId.Value);
                if (opt != null)
                {
                    _restoringExisting = true;
                    _linkCombo.SelectedItem = opt;
                    _restoringExisting = false;
                }
            }
            else if (existing?.LinkedCategoryId != null)
            {
                var opt = _allOptions.FirstOrDefault(o => o.ServiceCategory != null && o.ServiceCategory.Id == existing.LinkedCategoryId.Value);
                if (opt != null)
                {
                    _restoringExisting = true;
                    _linkCombo.SelectedItem = opt;
                    _restoringExisting = false;
                }
            }
            else if (preselectedBalance != null)
            {
                // Pre-select a pending balance so this dialog opens ready to record a payment
                // toward it - used by the Home tab's "Soldes en attente" section's "Payer"
                // button. Not wrapped in _restoringExisting: we WANT the normal auto-fill
                // (description + amount = remaining balance) that a fresh selection triggers.
                var opt = _allOptions.FirstOrDefault(o => o.Balance != null && o.Balance.Id == preselectedBalance.Id);
                if (opt != null)
                    _linkCombo.SelectedItem = opt;
            }

            // Restore the saved description LAST, after any selection-restore above may have
            // touched the textbox, so an edited charge keeps exactly what was saved.
            if (existing != null)
                _descBox.Text = existing.Description ?? "";
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

            var extrasCounts = _addOnRepo.GetCountsByParent();
            var serviceCategories = _categoryRepo.GetActiveServiceCategories();
            foreach (var cat in serviceCategories)
            {
                // A small hint on categories that offer optional extras, so the cashier knows
                // picking it will show a tick-list of extras.
                string extrasHint = extrasCounts.ContainsKey(cat.Id) ? "  + extras" : "";
                string label = $"[Catégorie] {cat.Name}  ({cat.CurrentPrice:0.00} / unité){extrasHint}";
                _allOptions.Add(new LinkOption { Label = label, ServiceCategory = cat });
            }

            // The dropdown's own embedded search box (see SearchableComboBox) filters this list
            // live by each option's Label - which already includes the stock category name in
            // brackets, so searching by category works without any extra plumbing here.
            _linkCombo.SetItems(_allOptions.Cast<object>());
        }

        private LinkOption SelectedOption() => _linkCombo.SelectedItem as LinkOption;

        private void LinkCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            var option = SelectedOption();
            bool linkedToPriced = option?.Stock != null || option?.ServiceCategory != null;
            bool linkedToBalance = option?.Balance != null;

            // Only when ADDING a charge for a service category can extras be offered - editing an
            // existing single charge line never shows them.
            RebuildExtras(_isEditing ? null : option?.ServiceCategory);

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

        // ---------------------------------------------------------------------------------
        // Optional extras
        // ---------------------------------------------------------------------------------

        /// <summary>Clears and rebuilds the extras tick-list for the given parent category (null =
        /// no extras: hides the panel). Only extras that can actually be billed right now are
        /// offered - an extra whose category/stock item has since been deactivated is skipped.</summary>
        private void RebuildExtras(Category parent)
        {
            foreach (var ctl in _extrasTable.Controls.Cast<Control>().ToList())
            {
                _extrasTable.Controls.Remove(ctl);
                ctl.Dispose();
            }
            _extrasTable.RowStyles.Clear();
            _extrasTable.RowCount = 0;
            _extraRows.Clear();

            if (parent != null)
            {
                foreach (var link in _addOnRepo.GetForParent(parent.Id))
                {
                    var row = new ExtraRow();
                    string label;
                    if (link.AddOnCategoryId.HasValue)
                    {
                        var cat = _categoryRepo.GetById(link.AddOnCategoryId.Value);
                        if (cat == null || !cat.IsActive || cat.HasFolder) continue;
                        row.Category = cat;
                        row.Name = cat.Name;
                        label = $"{cat.Name}  ({cat.CurrentPrice:0.00} / unité)";
                    }
                    else if (link.AddOnStockItemId.HasValue)
                    {
                        var item = _stockRepo.GetById(link.AddOnStockItemId.Value);
                        if (item == null || !item.IsActive) continue;
                        row.Stock = item;
                        row.Name = item.Name;
                        string stockInfo = item.IsOutOfStock ? "RUPTURE" : $"stock : {item.Quantity}";
                        label = $"{item.Name}  ({item.UnitPrice:0.00})  [{stockInfo}]";
                    }
                    else
                    {
                        continue;
                    }

                    int rowIndex = _extraRows.Count;
                    _extrasTable.RowCount = rowIndex + 1;
                    _extrasTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));

                    // AutoEllipsis: a very long stock/category name is shortened with "..." rather
                    // than stretching the row past the edge of the window.
                    row.Check = new CheckBox
                    {
                        Text = label, Dock = DockStyle.Fill, AutoSize = false, AutoEllipsis = true,
                        TextAlign = System.Drawing.ContentAlignment.MiddleLeft, Margin = new Padding(4, 2, 4, 2)
                    };
                    row.Qty = new NumericUpDown
                    {
                        Width = 66, Minimum = 1, Maximum = 10000, Value = 1, Enabled = false,
                        Anchor = AnchorStyles.Left, Margin = new Padding(4, 3, 4, 3)
                    };
                    row.LineLabel = new Label
                    {
                        Dock = DockStyle.Fill, AutoSize = false, Margin = new Padding(0, 0, 8, 0),
                        TextAlign = System.Drawing.ContentAlignment.MiddleRight
                    };

                    var captured = row;
                    row.Check.CheckedChanged += (s, e) =>
                    {
                        captured.Qty.Enabled = captured.Check.Checked;
                        OnExtrasChanged();
                    };
                    row.Qty.ValueChanged += (s, e) => OnExtrasChanged();

                    _extrasTable.Controls.Add(row.Check, 0, rowIndex);
                    _extrasTable.Controls.Add(row.Qty, 1, rowIndex);
                    _extrasTable.Controls.Add(row.LineLabel, 2, rowIndex);
                    _extraRows.Add(row);
                }
            }

            if (_extraRows.Count == 0)
            {
                _extrasHost.Visible = false;
                _extrasHost.Height = 0;
                _amountLabel.Text = "Montant total :";
                ResizeForExtras(0);
                SyncPaidToAmountIfUnlinkedDefault();
                return;
            }

            // Header (26) + the tick-list (up to ~5 rows before it scrolls) + summary (28).
            int scrollHeight = Math.Min(170, _extraRows.Count * 34 + 4);
            _extrasHost.Height = 26 + scrollHeight + 28;
            _extrasHost.Visible = true;
            _amountLabel.Text = "Montant (sans extras) :";
            ResizeForExtras(_extrasHost.Height + 12);
            OnExtrasChanged();
        }

        /// <summary>Grows/shrinks the dialog to fit the extras panel, keeping it fully on screen
        /// (a fixed-size dialog that just gets taller can otherwise run off the bottom of a small
        /// screen).</summary>
        private void ResizeForExtras(int extraHeight)
        {
            Height = BaseHeight + extraHeight;
            if (Visible)
            {
                var wa = Screen.FromControl(this).WorkingArea;
                if (Bottom > wa.Bottom)
                    Top = Math.Max(wa.Top, wa.Bottom - Height);
            }
        }

        private decimal ExtrasTotal() => _extraRows.Where(r => r.Check.Checked).Sum(r => r.Nominal);

        /// <summary>Main amount + every ticked extra - what the customer owes for this whole
        /// purchase, and what "Montant paye maintenant" defaults to.</summary>
        private decimal GrandTotal() => _amountBox.Value + ExtrasTotal();

        private void OnExtrasChanged()
        {
            foreach (var r in _extraRows)
                r.LineLabel.Text = r.Check.Checked ? $"= {r.Nominal:0.00}" : "";

            _extrasSummary.Text = $"Extras : +{ExtrasTotal():0.00}      Total avec extras : {GrandTotal():0.00}";
            SyncPaidToAmountIfUnlinkedDefault();
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

            if (_extraRows.Count > 0)
                OnExtrasChanged(); // refreshes the "Total avec extras" summary too
            else
                SyncPaidToAmountIfUnlinkedDefault();
        }

        /// <summary>Keeps "Montant paye maintenant" defaulted to the full total (main amount plus
        /// any ticked extras) as the user fills in the rest of the form (so the common case -
        /// fully paid - needs no extra step), WITHOUT clobbering a partial amount the user may
        /// have already typed there: this only re-syncs the value while the two boxes still match
        /// (i.e. nothing has diverged them yet). Once the user edits "Montant paye maintenant" to
        /// anything different from the total, this stops touching it.</summary>
        private void SyncPaidToAmountIfUnlinkedDefault()
        {
            if (_paidAmountBox.Value == _lastSyncedPaidValue)
                _paidAmountBox.Value = Math.Min(_paidAmountBox.Maximum, GrandTotal());
            _lastSyncedPaidValue = _paidAmountBox.Value;
        }

        private bool ConfirmStock(StockItem item, decimal wantedQty)
        {
            if (item.IsOutOfStock)
            {
                return MessageBox.Show($"'{item.Name}' est en rupture de stock. Ajouter cette charge quand meme ?",
                    "Rupture de stock", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;
            }
            if (wantedQty > item.Quantity)
            {
                return MessageBox.Show($"Il ne reste que {item.Quantity} de '{item.Name}', mais {wantedQty} sont demandes. Continuer quand meme ?",
                    "Stock insuffisant", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;
            }
            return true;
        }

        private void OkBtn_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_descBox.Text))
            {
                MessageBox.Show("La description est obligatoire.", "Information manquante", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var option = SelectedOption();
            var chosenExtras = _extraRows.Where(r => r.Check.Checked).ToList();

            if (option?.Stock != null && !ConfirmStock(option.Stock, _qtyBox.Value))
                return;
            foreach (var x in chosenExtras.Where(r => r.Stock != null))
            {
                if (!ConfirmStock(x.Stock, x.Qty.Value))
                    return;
            }

            decimal baseNominal = _amountBox.Value;
            decimal total = baseNominal + chosenExtras.Sum(x => x.Nominal);
            decimal paidNow = Math.Min(_paidAmountBox.Value, total);
            decimal shortfall = total - paidNow;

            string desc = _descBox.Text.Trim();
            string bundleDesc = chosenExtras.Count > 0 ? $"{desc} (+ {chosenExtras.Count} extra(s))" : desc;

            if (shortfall > 0.005m && !string.IsNullOrWhiteSpace(_descBox.Text))
            {
                string msg = option?.Balance != null
                    ? $"{paidNow:0.00} paye maintenant sur ce solde - il restera {shortfall:0.00} en attente. Continuer ?"
                    : $"Le client paye {paidNow:0.00} sur un total de {total:0.00} - un solde en attente de {shortfall:0.00} sera créé pour \"{bundleDesc}\". Continuer ?";
                if (MessageBox.Show(msg, "Paiement partiel", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;
            }

            // Only the amount actually collected today counts as a charge's amount - it's what
            // gets added to TODAY's revenue total, per the "paid on the day it's paid" rule. The
            // unpaid shortfall (if any) is tracked separately as a pending balance, never as part
            // of today's total. With extras ticked, the money actually paid is spread over the
            // lines in order (main item first, then extras) - each line takes what it's worth
            // until the paid amount runs out - so a fully-paid purchase records every line at its
            // full price, and a part-paid one fills lines from the top.
            var nominals = new List<decimal> { baseNominal };
            nominals.AddRange(chosenExtras.Select(x => x.Nominal));

            var allocated = new List<decimal>();
            decimal remainingPaid = paidNow;
            foreach (var nominal in nominals)
            {
                decimal part = Math.Min(nominal, remainingPaid);
                allocated.Add(part);
                remainingPaid -= part;
            }

            var charges = new List<CustomCharge>
            {
                new CustomCharge
                {
                    Description = desc,
                    Amount = allocated[0],
                    LinkedStockItemId = option?.Stock?.Id,
                    LinkedCategoryId = option?.ServiceCategory?.Id,
                    Quantity = (option?.Stock != null || option?.ServiceCategory != null) ? (int)_qtyBox.Value : (int?)null
                }
            };
            for (int i = 0; i < chosenExtras.Count; i++)
            {
                var x = chosenExtras[i];
                charges.Add(new CustomCharge
                {
                    Description = x.Name,
                    Amount = allocated[i + 1],
                    LinkedStockItemId = x.Stock?.Id,
                    LinkedCategoryId = x.Category?.Id,
                    Quantity = (int)x.Qty.Value
                });
            }

            ResultCharges = charges;
            ResultCharge = charges[0];

            if (option?.Balance != null)
            {
                _pendingRepo.ReduceOrDelete(option.Balance.Id, paidNow);
            }
            else if (shortfall > 0.005m)
            {
                _pendingRepo.Add(bundleDesc, total, shortfall, DateTime.Now.ToString("yyyy-MM-dd"));
            }

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
