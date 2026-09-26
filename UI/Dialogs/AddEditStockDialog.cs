using System;
using System.Windows.Forms;
using PhotoStudioApp.Models;

namespace PhotoStudioApp.UI.Dialogs
{
    public class AddEditStockDialog : Form
    {
        public StockItem ResultItem { get; private set; }

        private readonly TextBox _nameBox;
        private readonly TextBox _categoryBox;
        private readonly NumericUpDown _qtyBox;
        private readonly NumericUpDown _priceBox;
        private readonly NumericUpDown _lowStockBox;

        public AddEditStockDialog(StockItem existing = null)
        {
            Text = existing == null ? "Ajouter un article de stock" : "Modifier l'article de stock";
            Width = 440;
            Height = 340;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            Font = Theme.FontBase;
            BackColor = Theme.CardBackground;

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(20), RowCount = 6 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            layout.Controls.Add(new Label { Text = "Nom :", AutoSize = true, Margin = new Padding(0, 8, 0, 0) }, 0, 0);
            _nameBox = new TextBox { Width = 220, Text = existing?.Name ?? "" };
            layout.Controls.Add(_nameBox, 1, 0);

            layout.Controls.Add(new Label { Text = "Catégorie (optionnel) :", AutoSize = true, Margin = new Padding(0, 8, 0, 0) }, 0, 1);
            _categoryBox = new TextBox { Width = 220, Text = existing?.Category ?? "" };
            layout.Controls.Add(_categoryBox, 1, 1);

            layout.Controls.Add(new Label { Text = "Quantité :", AutoSize = true, Margin = new Padding(0, 8, 0, 0) }, 0, 2);
            _qtyBox = new NumericUpDown { Width = 120, Minimum = 0, Maximum = 1000000, Value = existing?.Quantity ?? 0 };
            layout.Controls.Add(_qtyBox, 1, 2);

            layout.Controls.Add(new Label { Text = "Prix unitaire :", AutoSize = true, Margin = new Padding(0, 8, 0, 0) }, 0, 3);
            _priceBox = new NumericUpDown { Width = 120, Minimum = 0, Maximum = 1000000, DecimalPlaces = 2, Value = existing?.UnitPrice ?? 0 };
            layout.Controls.Add(_priceBox, 1, 3);

            layout.Controls.Add(new Label { Text = "Seuil de stock bas :", AutoSize = true, Margin = new Padding(0, 8, 0, 0) }, 0, 4);
            _lowStockBox = new NumericUpDown { Width = 120, Minimum = 0, Maximum = 1000000, Value = existing?.LowStockThreshold ?? 0 };
            layout.Controls.Add(_lowStockBox, 1, 4);

            var btnPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill };
            var okBtn = Theme.MakePrimaryButton("Enregistrer");
            okBtn.Width = 110;
            okBtn.Click += OkBtn_Click;
            var cancelBtn = Theme.MakeSecondaryButton("Annuler");
            cancelBtn.Width = 100;
            cancelBtn.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            btnPanel.Controls.Add(okBtn);
            btnPanel.Controls.Add(cancelBtn);
            layout.Controls.Add(btnPanel, 1, 5);

            Controls.Add(layout);
            AcceptButton = okBtn;
            CancelButton = cancelBtn;
        }

        private void OkBtn_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_nameBox.Text))
            {
                MessageBox.Show("Le nom de l'article est obligatoire.", "Information manquante", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ResultItem = new StockItem
            {
                Name = _nameBox.Text.Trim(),
                Category = _categoryBox.Text.Trim(),
                Quantity = (int)_qtyBox.Value,
                UnitPrice = _priceBox.Value,
                LowStockThreshold = (int)_lowStockBox.Value
            };
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
