using System;
using System.IO;
using System.Windows.Forms;
using PhotoStudioApp.Data;
using PhotoStudioApp.Models;

namespace PhotoStudioApp.UI.Dialogs
{
    public class AddEditCategoryDialog : Form
    {
        public string CategoryName { get; private set; }
        public bool HasFolder { get; private set; }
        public string FolderRelativePath { get; private set; }
        public decimal Price { get; private set; }

        private readonly TextBox _nameBox;
        private readonly CheckBox _noFolderCheck;
        private readonly TextBox _folderBox;
        private readonly Button _browseBtn;
        private readonly Label _folderLabel;
        private readonly Label _hintLabel;
        private readonly NumericUpDown _priceBox;

        public AddEditCategoryDialog(Category existing = null)
        {
            Text = existing == null ? "Ajouter une catégorie" : "Modifier la catégorie";
            Width = 480;
            Height = 380;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            Font = Theme.FontBase;
            BackColor = Theme.CardBackground;

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Padding = new Padding(20), RowCount = 7 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            layout.Controls.Add(new Label { Text = "Nom de la catégorie :", AutoSize = true, Margin = new Padding(0, 8, 0, 0) }, 0, 0);
            _nameBox = new TextBox { Width = 260, Text = existing?.Name ?? "" };
            layout.Controls.Add(_nameBox, 1, 0);

            _noFolderCheck = new CheckBox
            {
                Text = "Article sans dossier (prix fixe, quantité saisie manuellement)",
                AutoSize = true,
                Checked = existing != null && !existing.HasFolder,
                Margin = new Padding(0, 10, 0, 0)
            };
            layout.Controls.Add(_noFolderCheck, 0, 1);
            layout.SetColumnSpan(_noFolderCheck, 3);

            _hintLabel = new Label
            {
                Text = "Exemple : impression de photos depuis le telephone d'un client - pas de dossier a compter,\nvous saisissez juste la quantité dans \"Ajouter une charge\" et le prix se calcule automatiquement.",
                AutoSize = true, ForeColor = System.Drawing.Color.Gray, Margin = new Padding(0, 2, 0, 8)
            };
            layout.Controls.Add(_hintLabel, 0, 2);
            layout.SetColumnSpan(_hintLabel, 3);

            _folderLabel = new Label { Text = "Dossier (dans le\ndossier du studio) :", AutoSize = true, Margin = new Padding(0, 8, 0, 0) };
            layout.Controls.Add(_folderLabel, 0, 3);
            _folderBox = new TextBox { Width = 260, ReadOnly = true, Text = existing?.FolderRelativePath ?? "" };
            layout.Controls.Add(_folderBox, 1, 3);
            _browseBtn = Theme.MakeSecondaryButton("Parcourir...");
            _browseBtn.Width = 100;
            _browseBtn.Click += BrowseBtn_Click;
            layout.Controls.Add(_browseBtn, 2, 3);

            layout.Controls.Add(new Label { Text = "Prix :", AutoSize = true, Margin = new Padding(0, 8, 0, 0) }, 0, 4);
            _priceBox = new NumericUpDown { Width = 120, Minimum = 0, Maximum = 1000000, DecimalPlaces = 2, Value = existing?.CurrentPrice ?? 0 };
            layout.Controls.Add(_priceBox, 1, 4);

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
            layout.SetColumnSpan(btnPanel, 2);

            Controls.Add(layout);
            AcceptButton = okBtn;
            CancelButton = cancelBtn;

            _noFolderCheck.CheckedChanged += (s, e) => UpdateFolderControlsEnabled();
            UpdateFolderControlsEnabled();
        }

        private void UpdateFolderControlsEnabled()
        {
            bool hasFolder = !_noFolderCheck.Checked;
            _folderLabel.Enabled = hasFolder;
            _folderBox.Enabled = hasFolder;
            _browseBtn.Enabled = hasFolder;
            if (!hasFolder)
                _folderBox.Text = "";
        }

        private void BrowseBtn_Click(object sender, EventArgs e)
        {
            var settings = new SettingsRepository().LoadAll();
            if (string.IsNullOrWhiteSpace(settings.StudioRootPath))
            {
                MessageBox.Show("Configurez d'abord le dossier racine du studio dans l'onglet Paramètres.",
                    "Dossier du studio non configure", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Directory.CreateDirectory(settings.StudioRootPath);

            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "Choisissez ou creez le dossier de cette catégorie, a l'interieur du dossier du studio.";
                dlg.SelectedPath = settings.StudioRootPath;

                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;

                string root = Path.GetFullPath(settings.StudioRootPath)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string chosen = Path.GetFullPath(dlg.SelectedPath)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                if (!chosen.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show("Le dossier choisi doit se trouver a l'interieur du dossier du studio :\n" + settings.StudioRootPath,
                        "Dossier invalide", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (chosen.Length == root.Length)
                {
                    MessageBox.Show("Choisissez un sous-dossier a l'interieur du dossier du studio, pas le dossier racine lui-meme.",
                        "Dossier invalide", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string relative = chosen.Substring(root.Length)
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                _folderBox.Text = relative;
            }
        }

        private void OkBtn_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_nameBox.Text))
            {
                MessageBox.Show("Le nom de la catégorie est obligatoire.", "Information manquante", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            bool hasFolder = !_noFolderCheck.Checked;
            if (hasFolder && string.IsNullOrWhiteSpace(_folderBox.Text))
            {
                MessageBox.Show("Choisissez un dossier avec le bouton \"Parcourir...\", ou cochez \"Article sans dossier\".",
                    "Information manquante", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            CategoryName = _nameBox.Text.Trim();
            HasFolder = hasFolder;
            FolderRelativePath = hasFolder ? _folderBox.Text.Trim() : null;
            Price = _priceBox.Value;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
