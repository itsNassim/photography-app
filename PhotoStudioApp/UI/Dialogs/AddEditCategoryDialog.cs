using System;
using System.Windows.Forms;
using PhotoStudioApp.Models;

namespace PhotoStudioApp.UI.Dialogs
{
    public class AddEditCategoryDialog : Form
    {
        public string CategoryName { get; private set; }
        public string FolderRelativePath { get; private set; }
        public decimal Price { get; private set; }

        private readonly TextBox _nameBox;
        private readonly TextBox _folderBox;
        private readonly NumericUpDown _priceBox;
        private readonly bool _isEdit;

        public AddEditCategoryDialog(Category existing = null)
        {
            _isEdit = existing != null;
            Text = _isEdit ? "Edit category" : "Add category";
            Width = 460;
            Height = 300;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            Font = Theme.FontBase;
            BackColor = Theme.CardBackground;

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(20), RowCount = 5 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            layout.Controls.Add(new Label { Text = "Category name:", AutoSize = true, Margin = new Padding(0, 8, 0, 0) }, 0, 0);
            _nameBox = new TextBox { Width = 240, Text = existing?.Name ?? "" };
            layout.Controls.Add(_nameBox, 1, 0);

            layout.Controls.Add(new Label { Text = "Folder (relative to\nstudio root):", AutoSize = true, Margin = new Padding(0, 8, 0, 0) }, 0, 1);
            _folderBox = new TextBox { Width = 240, Text = existing?.FolderRelativePath ?? "" };
            layout.Controls.Add(_folderBox, 1, 1);

            layout.Controls.Add(new Label { Text = "Note:", AutoSize = true, ForeColor = System.Drawing.Color.Gray }, 0, 2);
            layout.Controls.Add(new Label
            {
                Text = "Use a subfolder like \"amature\\12 identite\" for nested categories.",
                AutoSize = true, ForeColor = System.Drawing.Color.Gray, MaximumSize = new System.Drawing.Size(240, 0)
            }, 1, 2);

            layout.Controls.Add(new Label { Text = "Price:", AutoSize = true, Margin = new Padding(0, 8, 0, 0) }, 0, 3);
            _priceBox = new NumericUpDown { Width = 120, Minimum = 0, Maximum = 1000000, DecimalPlaces = 2, Value = existing?.CurrentPrice ?? 0 };
            layout.Controls.Add(_priceBox, 1, 3);

            var btnPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill };
            var okBtn = Theme.MakePrimaryButton("Save");
            okBtn.Width = 100;
            okBtn.Click += OkBtn_Click;
            var cancelBtn = Theme.MakeSecondaryButton("Cancel");
            cancelBtn.Width = 100;
            cancelBtn.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            btnPanel.Controls.Add(okBtn);
            btnPanel.Controls.Add(cancelBtn);
            layout.Controls.Add(btnPanel, 1, 4);

            Controls.Add(layout);
            AcceptButton = okBtn;
            CancelButton = cancelBtn;
        }

        private void OkBtn_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_nameBox.Text))
            {
                MessageBox.Show("Category name is required.", "Missing info", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(_folderBox.Text))
            {
                MessageBox.Show("Folder path is required.", "Missing info", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            CategoryName = _nameBox.Text.Trim();
            FolderRelativePath = _folderBox.Text.Trim();
            Price = _priceBox.Value;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
