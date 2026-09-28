using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using PhotoStudioApp.Data;
using PhotoStudioApp.Models;
using PhotoStudioApp.UI.Controls;

namespace PhotoStudioApp.UI.Dialogs
{
    /// <summary>Lets an admin choose which optional extras (other no-folder categories, or stock
    /// items) are offered whenever the given category is picked in "Ajouter une charge" - e.g.
    /// "10x15 photo" offering a frame from stock, "Traitement" and "Assemblage". Every change is
    /// saved immediately, like the rest of the Categories tab. Only extras a cashier can actually
    /// bill from that screen are offered here: active service categories and active stock items.</summary>
    public class ManageCategoryAddOnsDialog : Form
    {
        private class PickOption
        {
            public string Label;
            public Category Category;
            public StockItem Stock;
            public override string ToString() => Label;
        }

        private readonly Category _parent;
        private readonly CategoryAddOnRepository _addOnRepo = new CategoryAddOnRepository();
        private readonly CategoryRepository _categoryRepo = new CategoryRepository();
        private readonly StockRepository _stockRepo = new StockRepository();

        private readonly SearchableComboBox _picker;
        private readonly DataGridView _grid;

        public ManageCategoryAddOnsDialog(Category parent)
        {
            _parent = parent;

            Text = $"Extras optionnels - {parent.Name}";
            Width = 620;
            Height = 500;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            Font = Theme.FontBase;
            BackColor = Theme.CardBackground;

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(16) };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            root.Controls.Add(new Label
            {
                Text = $"Extras proposés avec « {parent.Name} »",
                Font = Theme.FontSubheading, AutoSize = true, Margin = new Padding(0, 0, 0, 4)
            }, 0, 0);

            root.Controls.Add(new Label
            {
                Text = "Quand cette catégorie est choisie dans \"Ajouter une charge\", ces extras s'affichent avec une case a cocher\n" +
                       "et une quantité (ex. Assemblage x nombre de personnes). Rien n'est ajouté sans que vous le cochiez.",
                AutoSize = true, ForeColor = System.Drawing.Color.Gray, Margin = new Padding(0, 0, 0, 10)
            }, 0, 1);

            var addRow = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 0, 0, 8) };
            _picker = new SearchableComboBox { Width = 400, Margin = new Padding(0, 0, 8, 0) };
            var addBtn = Theme.MakePrimaryButton("+ Ajouter");
            addBtn.Width = 110;
            addBtn.Margin = new Padding(0, -2, 0, 0);
            addBtn.Click += AddBtn_Click;
            addRow.Controls.Add(_picker);
            addRow.Controls.Add(addBtn);
            root.Controls.Add(addRow, 0, 2);

            _grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true };
            Theme.StyleGrid(_grid);
            _grid.Columns.Add("Type", "Type");
            _grid.Columns.Add("Name", "Nom");
            _grid.Columns.Add("Price", "Prix unitaire");
            _grid.Columns.Add("Status", "Statut");
            root.Controls.Add(_grid, 0, 3);

            var btnPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 10, 0, 0) };
            var closeBtn = Theme.MakeSecondaryButton("Fermer");
            closeBtn.Width = 100;
            closeBtn.Click += (s, e) => { DialogResult = DialogResult.OK; Close(); };
            var removeBtn = Theme.MakeDangerButton("Retirer");
            removeBtn.Width = 110;
            removeBtn.Click += RemoveBtn_Click;
            btnPanel.Controls.Add(closeBtn);
            btnPanel.Controls.Add(removeBtn);
            root.Controls.Add(btnPanel, 0, 4);

            Controls.Add(root);
            CancelButton = closeBtn;

            Reload();
        }

        /// <summary>Reloads the assigned-extras grid and rebuilds the picker so it only offers
        /// things that aren't already assigned (and never the parent category itself).</summary>
        private void Reload()
        {
            var assigned = _addOnRepo.GetForParent(_parent.Id);

            _grid.Rows.Clear();
            foreach (var a in assigned)
            {
                string type, name, status;
                decimal price;
                if (a.AddOnCategoryId.HasValue)
                {
                    var cat = _categoryRepo.GetById(a.AddOnCategoryId.Value);
                    type = "Catégorie";
                    name = cat?.Name ?? "(introuvable)";
                    price = cat?.CurrentPrice ?? 0m;
                    status = cat == null ? "-" : cat.IsActive ? "Actif" : "Désactivé";
                }
                else
                {
                    var item = _stockRepo.GetById(a.AddOnStockItemId.Value);
                    type = "Stock";
                    name = item?.Name ?? "(introuvable)";
                    price = item?.UnitPrice ?? 0m;
                    status = item == null ? "-" : item.IsActive ? "Actif" : "Désactivé";
                }

                int idx = _grid.Rows.Add(type, name, price.ToString("0.00"), status);
                _grid.Rows[idx].Tag = a.Id;
                if (status == "Désactivé")
                    _grid.Rows[idx].DefaultCellStyle.ForeColor = System.Drawing.Color.Gray;
            }

            var takenCategoryIds = new HashSet<int>(assigned.Where(a => a.AddOnCategoryId.HasValue).Select(a => a.AddOnCategoryId.Value));
            var takenStockIds = new HashSet<int>(assigned.Where(a => a.AddOnStockItemId.HasValue).Select(a => a.AddOnStockItemId.Value));

            var options = new List<PickOption>();
            foreach (var cat in _categoryRepo.GetActiveServiceCategories())
            {
                if (cat.Id == _parent.Id || takenCategoryIds.Contains(cat.Id)) continue;
                options.Add(new PickOption { Label = $"[Catégorie] {cat.Name}  ({cat.CurrentPrice:0.00} / unité)", Category = cat });
            }
            foreach (var item in _stockRepo.GetAll(activeOnly: true))
            {
                if (takenStockIds.Contains(item.Id)) continue;
                string categoryPart = string.IsNullOrWhiteSpace(item.Category) ? "" : $"  [{item.Category}]";
                options.Add(new PickOption { Label = $"[Stock] {item.Name}{categoryPart}  ({item.UnitPrice:0.00})", Stock = item });
            }

            _picker.SetItems(options.Cast<object>());
            _picker.SelectedItem = null;
        }

        private void AddBtn_Click(object sender, EventArgs e)
        {
            var option = _picker.SelectedItem as PickOption;
            if (option == null)
            {
                MessageBox.Show("Choisissez d'abord un extra dans la liste.", "Aucune selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _addOnRepo.Add(_parent.Id, option.Category?.Id, option.Stock?.Id);
            Reload();
        }

        private void RemoveBtn_Click(object sender, EventArgs e)
        {
            if (_grid.CurrentRow?.Tag == null) return;
            _addOnRepo.Remove((int)_grid.CurrentRow.Tag);
            Reload();
        }
    }
}
