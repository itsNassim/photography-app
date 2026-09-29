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
        private readonly CategoryAddOnRepository _addOnRepo = new CategoryAddOnRepository();
        private readonly ImportExportService _importExport = new ImportExportService();
        private DataGridView _grid;
        private TextBox _searchBox;
        private System.Collections.Generic.List<Category> _allCategories = new System.Collections.Generic.List<Category>();
        private System.Collections.Generic.Dictionary<int, int> _addOnCounts = new System.Collections.Generic.Dictionary<int, int>();

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

            var title = new Label { Text = "Catégories / Tarifs", Font = Theme.FontHeading, AutoSize = true, Margin = new Padding(0, 0, 0, 10) };
            root.Controls.Add(title, 0, 0);

            var toolbar = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 8) };
            var searchLabel = new Label { Text = "Rechercher :", AutoSize = true, Margin = new Padding(0, 9, 4, 0) };
            _searchBox = new TextBox { Width = 220, Font = Theme.FontBase, Margin = new Padding(0, 4, 14, 4) };
            _searchBox.TextChanged += (s, e) => ApplyFilter();
            var addBtn = Theme.MakePrimaryButton("+ Ajouter");
            addBtn.Width = 120;
            addBtn.Click += AddBtn_Click;
            var editBtn = Theme.MakeSecondaryButton("Modifier");
            editBtn.Width = 110;
            editBtn.Click += EditBtn_Click;
            var toggleBtn = Theme.MakeSecondaryButton("Activer / Desactiver");
            toggleBtn.Width = 170;
            toggleBtn.Click += ToggleBtn_Click;
            var addOnsBtn = Theme.MakeSecondaryButton("Extras...");
            addOnsBtn.Width = 120;
            addOnsBtn.Click += AddOnsBtn_Click;
            var exportBtn = Theme.MakeSecondaryButton("Exporter");
            exportBtn.Width = 110;
            exportBtn.Click += ExportBtn_Click;
            var importBtn = Theme.MakeSecondaryButton("Importer");
            importBtn.Width = 110;
            importBtn.Click += ImportBtn_Click;
            toolbar.Controls.Add(searchLabel);
            toolbar.Controls.Add(_searchBox);
            toolbar.Controls.Add(addBtn);
            toolbar.Controls.Add(editBtn);
            toolbar.Controls.Add(toggleBtn);
            toolbar.Controls.Add(addOnsBtn);
            toolbar.Controls.Add(exportBtn);
            toolbar.Controls.Add(importBtn);
            root.Controls.Add(toolbar, 0, 1);

            var gridPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.CardBackground };
            _grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true };
            Theme.StyleGrid(_grid);
            _grid.Columns.Add("Name", "Nom de la catégorie");
            _grid.Columns.Add("Folder", "Dossier (ou sans dossier)");
            _grid.Columns.Add("Type", "Type");
            _grid.Columns.Add("Price", "Prix actuel");
            _grid.Columns.Add("Status", "Statut");
            gridPanel.Controls.Add(_grid);
            root.Controls.Add(gridPanel, 0, 2);

            Controls.Add(root);
        }

        /// <summary>Reloads categories from the database, then re-applies whatever is typed in the
        /// search box (so an add/edit/toggle never silently clears the current search).</summary>
        public void RefreshAll()
        {
            _allCategories = _repo.GetAllCategories(includeInactive: true);
            _addOnCounts = _addOnRepo.GetCountsByParent();
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            if (_grid == null) return;

            _grid.Rows.Clear();
            foreach (var cat in _allCategories)
            {
                string folderDisplay = cat.HasFolder ? cat.FolderRelativePath : "(sans dossier - prix fixe)";
                string typeDisplay = cat.IsCustom ? "Personnalisee" : "Fixe";
                // A category that offers optional extras is effectively its own kind of category -
                // say so right in the Type column so it's obvious at a glance which ones do.
                if (_addOnCounts.TryGetValue(cat.Id, out int extrasCount) && extrasCount > 0)
                    typeDisplay += $" - avec extras ({extrasCount})";

                string statusDisplay = cat.IsActive ? "Active" : "Desactivee";
                if (!TextSearch.MatchesAny(_searchBox?.Text, cat.Name, folderDisplay, typeDisplay, statusDisplay))
                    continue;

                int idx = _grid.Rows.Add(
                    cat.Name, folderDisplay, typeDisplay,
                    cat.CurrentPrice.ToString("0.00"), statusDisplay);
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
            if (!AuthGate.RequireAdminPassword(this, "Ajouter une catégorie", "+ Ajouter")) return;

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
            if (!AuthGate.RequireAdminPassword(this, "Modifier une catégorie", "Modifier")) return;

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

        /// <summary>Opens the manager for the selected category's optional extras (other categories
        /// or stock items offered as tick-boxes when this category is chosen in "Ajouter une
        /// charge"). Password-protected like every other change on this tab. Only categories
        /// without a folder can offer extras - folder-based ones are counted from disk at closing
        /// time and never picked in the charge screen, so extras could never be offered there.</summary>
        private void AddOnsBtn_Click(object sender, EventArgs e)
        {
            var cat = Selected();
            if (cat == null)
            {
                MessageBox.Show("Selectionnez d'abord une catégorie dans la liste.", "Aucune selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (cat.HasFolder)
            {
                MessageBox.Show(
                    $"'{cat.Name}' est comptée depuis un dossier a la clôture, elle n'est jamais choisie dans \"Ajouter une charge\" - " +
                    "les extras ne peuvent donc pas s'y appliquer.\n\nLes extras fonctionnent pour les catégories sans dossier (prix fixe).",
                    "Extras non disponibles", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!AuthGate.RequireAdminPassword(this, "Gérer les extras d'une catégorie", "Extras...")) return;

            using (var dlg = new ManageCategoryAddOnsDialog(cat))
                dlg.ShowDialog(this);
            RefreshAll();
        }

        private void ToggleBtn_Click(object sender, EventArgs e)
        {
            var cat = Selected();
            if (cat == null) return;
            if (!AuthGate.RequireAdminPassword(this, "Activer / désactiver une catégorie", "Activer / Desactiver")) return;

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
            if (!AuthGate.RequireAdminPassword(this, "Exporter les catégories", "Exporter")) return;

            using (var dlg = new SaveFileDialog { Filter = "Fichier CSV (*.csv)|*.csv", FileName = "categories.csv" })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
                try
                {
                    _importExport.ExportCategories(dlg.FileName);
                    MessageBox.Show("Catégories exportées avec succès.", "Termine", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Échec de l'export :\n" + ex.Message, "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        /// <summary>Imports categories from a CSV file (as produced by Export). Any category
        /// whose name already exists (case-insensitive) is skipped - never overwritten, never
        /// duplicated - so importing the same file twice is always safe.</summary>
        private void ImportBtn_Click(object sender, EventArgs e)
        {
            if (!AuthGate.RequireAdminPassword(this, "Importer des catégories", "Importer")) return;

            using (var dlg = new OpenFileDialog { Filter = "Fichier CSV (*.csv)|*.csv" })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
                try
                {
                    var result = _importExport.ImportCategories(dlg.FileName);
                    RefreshAll();
                    CategoriesChanged?.Invoke(this, EventArgs.Empty);

                    string msg = $"{result.Added} catégorie(s) ajoutée(s), {result.SkippedDuplicates} déjà existante(s) ignorée(s).";
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
