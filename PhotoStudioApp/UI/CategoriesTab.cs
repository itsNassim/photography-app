using System;
using System.Drawing;
using System.Windows.Forms;
using PhotoStudioApp.Data;
using PhotoStudioApp.Models;
using PhotoStudioApp.Services;
using PhotoStudioApp.UI.Dialogs;

namespace PhotoStudioApp.UI
{
    public class CategoriesTab : UserControl
    {
        public event EventHandler CategoriesChanged;

        private readonly CategoryRepository _repo = new CategoryRepository();
        private readonly ImportExportService _importExport = new ImportExportService();
        private DataGridView _grid;

        public CategoriesTab()
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

            var title = new Label { Text = "Categories / Tarifs", Font = Theme.FontHeading, AutoSize = true, Margin = new Padding(0, 0, 0, 10) };
            root.Controls.Add(title, 0, 0);

            var toolbar = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 8) };
            var addBtn = Theme.MakePrimaryButton("+ Ajouter");
            addBtn.Width = 120;
            addBtn.Click += AddBtn_Click;
            var editBtn = Theme.MakeSecondaryButton("Modifier");
            editBtn.Width = 110;
            editBtn.Click += EditBtn_Click;
            var toggleBtn = Theme.MakeSecondaryButton("Activer / Desactiver");
            toggleBtn.Width = 170;
            toggleBtn.Click += ToggleBtn_Click;
            var exportBtn = Theme.MakeSecondaryButton("Exporter");
            exportBtn.Width = 110;
            exportBtn.Click += ExportBtn_Click;
            var importBtn = Theme.MakeSecondaryButton("Importer");
            importBtn.Width = 110;
            importBtn.Click += ImportBtn_Click;
            toolbar.Controls.Add(addBtn);
            toolbar.Controls.Add(editBtn);
            toolbar.Controls.Add(toggleBtn);
            toolbar.Controls.Add(exportBtn);
            toolbar.Controls.Add(importBtn);
            root.Controls.Add(toolbar, 0, 1);

            var gridPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.CardBackground };
            _grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true };
            Theme.StyleGrid(_grid);
            _grid.Columns.Add("Name", "Nom de la categorie");
            _grid.Columns.Add("Folder", "Dossier (ou sans dossier)");
            _grid.Columns.Add("Type", "Type");
            _grid.Columns.Add("Price", "Prix actuel");
            _grid.Columns.Add("Status", "Statut");
            gridPanel.Controls.Add(_grid);
            root.Controls.Add(gridPanel, 0, 2);

            Controls.Add(root);
        }

        public void RefreshAll()
        {
            var all = _repo.GetAllCategories(includeInactive: true);
            _grid.Rows.Clear();
            foreach (var cat in all)
            {
                string folderDisplay = cat.HasFolder ? cat.FolderRelativePath : "(sans dossier - prix fixe)";
                int idx = _grid.Rows.Add(
                    cat.Name, folderDisplay, cat.IsCustom ? "Personnalisee" : "Fixe",
                    cat.CurrentPrice.ToString("0.00"), cat.IsActive ? "Active" : "Desactivee");
                _grid.Rows[idx].Tag = cat;
                if (!cat.IsActive)
                {
                    _grid.Rows[idx].DefaultCellStyle.ForeColor = Color.Gray;
                }
            }
        }

        private Category Selected() => _grid.CurrentRow?.Tag as Category;

        private void AddBtn_Click(object sender, EventArgs e)
        {
            if (!AuthGate.RequireAdminPassword(this)) return;

            using (var dlg = new AddEditCategoryDialog())
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    _repo.AddCategory(dlg.CategoryName, dlg.HasFolder, dlg.FolderRelativePath, isCustom: true, initialPrice: dlg.Price);
                    RefreshAll();
                    CategoriesChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private void EditBtn_Click(object sender, EventArgs e)
        {
            var cat = Selected();
            if (cat == null) return;
            if (!AuthGate.RequireAdminPassword(this)) return;

            using (var dlg = new AddEditCategoryDialog(cat))
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    if (dlg.CategoryName != cat.Name)
                        _repo.RenameCategory(cat.Id, dlg.CategoryName);
                    if (dlg.HasFolder != cat.HasFolder || dlg.FolderRelativePath != cat.FolderRelativePath)
                        _repo.UpdateFolderPath(cat.Id, dlg.HasFolder, dlg.FolderRelativePath);
                    if (dlg.Price != cat.CurrentPrice)
                        _repo.SetPrice(cat.Id, dlg.Price); // nouvelle ligne datee ; l'historique garde l'ancien prix

                    RefreshAll();
                    CategoriesChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private void ToggleBtn_Click(object sender, EventArgs e)
        {
            var cat = Selected();
            if (cat == null) return;
            if (!AuthGate.RequireAdminPassword(this)) return;

            if (cat.IsActive)
            {
                if (MessageBox.Show($"Desactiver '{cat.Name}' ? Elle disparaitra de l'accueil mais les rapports passes qui la mentionnent restent intacts.",
                    "Confirmer la desactivation", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;
                _repo.Deactivate(cat.Id);
            }
            else
            {
                _repo.Reactivate(cat.Id);
            }
            RefreshAll();
            CategoriesChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Exports every category (fixed and custom, active and inactive) to a CSV
        /// file. Password-protected since it's a bulk data-access action, matching Import.</summary>
        private void ExportBtn_Click(object sender, EventArgs e)
        {
            if (!AuthGate.RequireAdminPassword(this)) return;

            using (var dlg = new SaveFileDialog { Filter = "Fichier CSV (*.csv)|*.csv", FileName = "categories.csv" })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
                try
                {
                    _importExport.ExportCategories(dlg.FileName);
                    MessageBox.Show("Categories exportees avec succes.", "Termine", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Echec de l'export :\n" + ex.Message, "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        /// <summary>Imports categories from a CSV file (as produced by Export). Any category
        /// whose name already exists (case-insensitive) is skipped - never overwritten, never
        /// duplicated - so importing the same file twice is always safe.</summary>
        private void ImportBtn_Click(object sender, EventArgs e)
        {
            if (!AuthGate.RequireAdminPassword(this)) return;

            using (var dlg = new OpenFileDialog { Filter = "Fichier CSV (*.csv)|*.csv" })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
                try
                {
                    var result = _importExport.ImportCategories(dlg.FileName);
                    RefreshAll();
                    CategoriesChanged?.Invoke(this, EventArgs.Empty);

                    string msg = $"{result.Added} categorie(s) ajoutee(s), {result.SkippedDuplicates} deja existante(s) ignoree(s).";
                    if (result.Errors.Count > 0)
                        msg += $"\n\n{result.Errors.Count} ligne(s) ignoree(s) a cause d'un format invalide.";
                    MessageBox.Show(msg, "Import termine", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Echec de l'import :\n" + ex.Message, "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}
