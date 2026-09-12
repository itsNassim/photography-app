using System;
using System.Windows.Forms;
using PhotoStudioApp.Models;

namespace PhotoStudioApp.UI.Dialogs
{
    /// <summary>Add or edit one business expense (money spent that day - e.g. a worker buying
    /// supplies for the studio). Deliberately simple: just a description and an amount.</summary>
    public class AddExpenseDialog : Form
    {
        public Expense ResultExpense { get; private set; }

        private readonly TextBox _descBox;
        private readonly NumericUpDown _amountBox;

        public AddExpenseDialog(Expense existing = null)
        {
            Text = existing == null ? "Ajouter une dépense" : "Modifier la dépense";
            Width = 420;
            Height = 240;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            Font = Theme.FontBase;
            BackColor = Theme.CardBackground;

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(20), RowCount = 3 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            layout.Controls.Add(new Label { Text = "Description\n(ce qui a ete achete) :", AutoSize = true, Margin = new Padding(0, 8, 0, 0) }, 0, 0);
            _descBox = new TextBox { Width = 220, Text = existing?.Description ?? "" };
            layout.Controls.Add(_descBox, 1, 0);

            layout.Controls.Add(new Label { Text = "Montant :", AutoSize = true, Margin = new Padding(0, 8, 0, 0) }, 0, 1);
            _amountBox = new NumericUpDown { Width = 120, Minimum = 0, Maximum = 1000000, DecimalPlaces = 2, Value = existing?.Amount ?? 0 };
            layout.Controls.Add(_amountBox, 1, 1);

            var btnPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill };
            var okBtn = Theme.MakePrimaryButton("Enregistrer");
            okBtn.Width = 110;
            okBtn.Click += OkBtn_Click;
            var cancelBtn = Theme.MakeSecondaryButton("Annuler");
            cancelBtn.Width = 100;
            cancelBtn.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            btnPanel.Controls.Add(okBtn);
            btnPanel.Controls.Add(cancelBtn);
            layout.Controls.Add(btnPanel, 1, 2);

            Controls.Add(layout);
            AcceptButton = okBtn;
            CancelButton = cancelBtn;
        }

        private void OkBtn_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_descBox.Text))
            {
                MessageBox.Show("La description est obligatoire.", "Information manquante", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ResultExpense = new Expense
            {
                Description = _descBox.Text.Trim(),
                Amount = _amountBox.Value
            };
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
