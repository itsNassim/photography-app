using System;
using System.Windows.Forms;
using PhotoStudioApp.Models;

namespace PhotoStudioApp.UI.Dialogs
{
    /// <summary>Add or edit one manual month-level expense (rent, a subscription, etc. - not
    /// tied to any single closed day). Deliberately simple, same shape as AddExpenseDialog: just
    /// a description and an amount - which month it belongs to (YearMonth) and when it was added
    /// (DateAdded) are both set by the caller (HistoryTab), not chosen here: an expense always
    /// counts toward whichever month row was selected when Ajouter was clicked, and DateAdded is
    /// simply the actual date it was added on (kept unchanged when later editing description/
    /// amount, since it's a historical fact, not a "last modified" stamp).</summary>
    public class AddEditMonthlyExpenseDialog : Form
    {
        public MonthlyExpense ResultExpense { get; private set; }

        private readonly TextBox _descBox;
        private readonly NumericUpDown _amountBox;
        private readonly int _existingId;

        public AddEditMonthlyExpenseDialog(MonthlyExpense existing = null)
        {
            Text = existing == null ? "Ajouter une dépense du mois" : "Modifier la dépense";
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

            layout.Controls.Add(new Label { Text = "Description\n(ex. Loyer, Electricité) :", AutoSize = true, Margin = new Padding(0, 8, 0, 0) }, 0, 0);
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

            _existingId = existing?.Id ?? 0;
        }

        private void OkBtn_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_descBox.Text))
            {
                MessageBox.Show("La description est obligatoire.", "Information manquante", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ResultExpense = new MonthlyExpense
            {
                Id = _existingId,
                Description = _descBox.Text.Trim(),
                Amount = _amountBox.Value
            };
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
