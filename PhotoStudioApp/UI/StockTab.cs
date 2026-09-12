using System;
using System.Linq;
using System.Windows.Forms;
using PhotoStudioApp.Data;
using PhotoStudioApp.Models;
using PhotoStudioApp.Services;
using PhotoStudioApp.UI.Dialogs;

namespace PhotoStudioApp.UI
{
    public class StockTab : UserControl
    {
        public event EventHandler StockChanged;

        private readonly StockRepository _repo = new StockRepository();
        private readonly ImportExportService _importExport = new ImportExportService();
        private DataGridView _grid;
        private TextBox _searchBox;
        private ComboBox _filterCombo;

        public StockTab()
        {
            BackColor = Theme.Background;
            BuildLayout();
            RefreshAll();
        }

        private void BuildLayout()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(16) };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var title = new Label { Text = "Stock / Consommables", Font = Theme.FontHeading, AutoSize = true, Margin = new Padding(0, 0, 0, 10) };
            root.Controls.Add(title, 0, 0);

            var toolbar = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 8) };
            _searchBox = new TextBox { Width = 220, Font = Theme.FontBase, Margin = new Padding(0, 4, 10, 4) };
            _searchBox.TextChanged += (s, e) => ApplyFilter();
            var searchLabel = new Label { Text = "Rechercher :", AutoSize = true, Margin = new Padding(0, 9, 4, 0) };

            _filterCombo = new ComboBox { Width = 170, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 4, 14, 4) };
            _filterCombo.Items.AddRange(new object[] { "Tous", "Stock bas", "Rupture de stock" });
            _filterCombo.SelectedIndex = 0;
            _filterCombo.SelectedIndexChanged += (s, e) => ApplyFilter();

            var addBtn = Theme.MakePrimaryButton("+ Ajouter");
            addBtn.Width = 120;
            addBtn.Click += AddBtn_Click;
            var editBtn = Theme.MakeSecondaryButton("Modifier");
            editBtn.Width = 100;
            editBtn.Click += EditBtn_Click;
            var restockBtn = Theme.MakeSecondaryButton("Ajuster qte");
            restockBtn.Width = 110;
            restockBtn.Click += RestockBtn_Click;
            var deleteBtn = Theme.MakeDangerButton("Supprimer");
            deleteBtn.Width = 100;
            deleteBtn.Click += DeleteBtn_Click;
            var exportBtn = Theme.MakeSecondaryButton("Exporter");
            exportBtn.Width = 110;
            exportBtn.Click += ExportBtn_Click;
            var importBtn = Theme.MakeSecondaryButton("Importer");
            importBtn.Width = 110;
            importBtn.Click += ImportBtn_Click;

            toolbar.Controls.Add(searchLabel);
            toolbar.Controls.Add(_searchBox);
            toolbar.Controls.Add(new Label { Text = "Filtre :", AutoSize = true, Margin = new Padding(0, 9, 4, 0) });
            toolbar.Controls.Add(_filterCombo);
            toolbar.Controls.Add(addBtn);
            toolbar.Controls.Add(editBtn);
            toolbar.Controls.Add(restockBtn);
            toolbar.Controls.Add(deleteBtn);
            toolbar.Controls.Add(exportBtn);
            toolbar.Controls.Add(importBtn);
            root.Controls.Add(toolbar, 0, 1);

            var gridPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.CardBackground };
            _grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true };
            Theme.StyleGrid(_grid);
            _grid.Columns.Add("Name", "Nom");
            _grid.Columns.Add("Category", "Catégorie");
            _grid.Columns.Add("Quantity", "Quantité");
            _grid.Columns.Add("UnitPrice", "Prix unitaire");
            _grid.Columns.Add("Status", "Statut");
            _grid.RowPrePaint += Grid_RowPrePaint;
            gridPanel.Controls.Add(_grid);
            root.Controls.Add(gridPanel, 0, 2);

            Controls.Add(root);
        }

        private void Grid_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            var row = _grid.Rows[e.RowIndex];
            if (row.Tag is StockItem item)
            {
                if (item.IsOutOfStock)
                {
                    row.DefaultCellStyle.BackColor = Theme.WarningBackground;
                    row.DefaultCellStyle.ForeColor = Theme.Warning;
                    row.DefaultCellStyle.SelectionBackColor = Theme.WarningSelectionBackColor;
                    row.DefaultCellStyle.SelectionForeColor = Theme.WarningSelectionForeColor;
                }
                else if (item.IsLowStock)
                {
                    row.DefaultCellStyle.BackColor = Theme.LowStockBackground;
                    row.DefaultCellStyle.ForeColor = Theme.LowStockColor;
                    row.DefaultCellStyle.SelectionBackColor = Theme.LowStockSelectionBackColor;
                    row.DefaultCellStyle.SelectionForeColor = Theme.LowStockSelectionForeColor;
                }
                else
                {
                    row.DefaultCellStyle.BackColor = Theme.CardBackground;
                    row.DefaultCellStyle.ForeColor = Theme.TextDark;
                    row.DefaultCellStyle.SelectionBackColor = Theme.SelectionBackColor;
                    row.DefaultCellStyle.SelectionForeColor = Theme.SelectionForeColor;
                }
            }
        }

        public void RefreshAll() => ApplyFilter();

        private void ApplyFilter()
        {
            var all = _repo.GetAll(activeOnly: true);
            string search = _searchBox.Text?.Trim().ToLowerInvariant() ?? "";
            string filter = _filterCombo.SelectedItem?.ToString() ?? "Tous";

            var filtered = all.Where(i => string.IsNullOrEmpty(search) || i.Name.ToLowerInvariant().Contains(search));
            if (filter == "Stock bas") filtered = filtered.Where(i => i.IsLowStock);
            else if (filter == "Rupture de stock") filtered = filtered.Where(i => i.IsOutOfStock);

            _grid.Rows.Clear();
            foreach (var item in filtered)
            {
                string status = item.IsOutOfStock ? "RUPTURE" : item.IsLowStock ? "Stock bas" : "OK";
                int idx = _grid.Rows.Add(item.Name, item.Category, item.Quantity, item.UnitPrice.ToString("0.00"), status);
                _grid.Rows[idx].Tag = item;
            }
        }

        private StockItem SelectedItem() => _grid.CurrentRow?.Tag as StockItem;

        private void AddBtn_Click(object sender, EventArgs e)
        {
            if (!AuthGate.RequireAdminPassword(this)) return;

            using (var dlg = new AddEditStockDialog())
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    _repo.Add(dlg.ResultItem);
                    RefreshAll();
                    StockChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private void EditBtn_Click(object sender, EventArgs e)
        {
            var item = SelectedItem();
            if (item == null) return;
            if (!AuthGate.RequireAdminPassword(this)) return;

            using (var dlg = new AddEditStockDialog(item))
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    var updated = dlg.ResultItem;
                    updated.Id = item.Id;
                    _repo.Update(updated);
                    RefreshAll();
                    StockChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private void RestockBtn_Click(object sender, EventArgs e)
        {
            var item = SelectedItem();
            if (item == null) return;
            if (!AuthGate.RequireAdminPassword(this)) return;

            using (var form = new Form
            {
                Text = $"Ajuster la quantité - {item.Name}",
                Width = 380,
                Height = 160,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false,
                Font = Theme.FontBase
            })
            {
                var label = new Label { Text = $"Quantité actuelle : {item.Quantity}\nSaisissez la variation (+ pour reapprovisionner, - pour retirer) :", AutoSize = true, Left = 16, Top = 14 };
                var input = new NumericUpDown { Minimum = -100000, Maximum = 100000, Left = 16, Top = 60, Width = 120 };
                var okBtn = Theme.MakePrimaryButton("Appliquer");
                okBtn.Left = 160; okBtn.Top = 58; okBtn.Width = 110;
                okBtn.Click += (s, e2) => { form.Tag = (int)input.Value; form.DialogResult = DialogResult.OK; };
                form.Controls.Add(label);
                form.Controls.Add(input);
                form.Controls.Add(okBtn);

                if (form.ShowDialog() == DialogResult.OK)
                {
                    int delta = (int)form.Tag;
                    _repo.AdjustQuantity(item.Id, delta);
                    RefreshAll();
                    StockChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private void DeleteBtn_Click(object sender, EventArgs e)
        {
            var item = SelectedItem();
            if (item == null) return;
            if (!AuthGate.RequireAdminPassword(this)) return;

            if (MessageBox.Show($"Supprimer l'article de stock '{item.Name}' ? L'historique existant qui le mentionne n'est pas affecte.",
                "Confirmer la suppression", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                _repo.Deactivate(item.Id);
                RefreshAll();
                StockChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>Exports every stock item (active and inactive) to a CSV file.
        /// Password-protected since it's a bulk data-access action, matching Import.</summary>
        private void ExportBtn_Click(object sender, EventArgs e)
        {
            if (!AuthGate.RequireAdminPassword(this)) return;

            using (var dlg = new SaveFileDialog { Filter = "Fichier CSV (*.csv)|*.csv", FileName = "stock.csv" })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
                try
                {
                    _importExport.ExportStock(dlg.FileName);
                    MessageBox.Show("Stock exporté avec succès.", "Termine", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Échec de l'export :\n" + ex.Message, "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        /// <summary>Imports stock items from a CSV file (as produced by Export). Any item whose
        /// name already exists (case-insensitive) is skipped - never overwritten, never
        /// duplicated, and its current quantity is left untouched - so importing the same file
        /// twice is always safe.</summary>
        private void ImportBtn_Click(object sender, EventArgs e)
        {
            if (!AuthGate.RequireAdminPassword(this)) return;

            using (var dlg = new OpenFileDialog { Filter = "Fichier CSV (*.csv)|*.csv" })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
                try
                {
                    var result = _importExport.ImportStock(dlg.FileName);
                    RefreshAll();
                    StockChanged?.Invoke(this, EventArgs.Empty);

                    string msg = $"{result.Added} article(s) ajouté(s), {result.SkippedDuplicates} déjà existant(s) ignoré(s).";
                    if (result.Errors.Count > 0)
                        msg += $"\n\n{result.Errors.Count} ligne(s) ignorée(s) a cause d'un format invalide.";
                    MessageBox.Show(msg, "Import termine", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Échec de l'import :\n" + ex.Message, "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}
