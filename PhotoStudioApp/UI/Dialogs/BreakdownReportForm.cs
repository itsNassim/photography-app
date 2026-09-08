using System;
using System.Text;
using System.Windows.Forms;
using PhotoStudioApp.Models;
using PhotoStudioApp.Services;

namespace PhotoStudioApp.UI.Dialogs
{
    public class BreakdownReportForm : Form
    {
        private readonly DailyClose _close;
        private readonly ReportPrintService _printService = new ReportPrintService();

        public BreakdownReportForm(DailyClose close)
        {
            _close = close;
            Text = $"Daily report - {close.CloseDate}";
            Width = 560;
            Height = 620;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Theme.CardBackground;
            Font = Theme.FontBase;

            BuildLayout();
        }

        private void BuildLayout()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(20) };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var title = new Label { Text = $"Daily report - {_close.CloseDate}", Font = Theme.FontHeading, AutoSize = true, Margin = new Padding(0, 0, 0, 10) };
            root.Controls.Add(title, 0, 0);

            var textBox = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                Font = new System.Drawing.Font("Consolas", 10.5f),
                Text = BuildReportText()
            };
            root.Controls.Add(textBox, 0, 1);

            var btnPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, AutoSize = true, Margin = new Padding(0, 12, 0, 0) };
            var closeBtn = Theme.MakeSecondaryButton("Close");
            closeBtn.Width = 100;
            closeBtn.Click += (s, e) => Close();
            var exportPdfBtn = Theme.MakeSecondaryButton("Export as PDF");
            exportPdfBtn.Width = 140;
            exportPdfBtn.Click += ExportPdfBtn_Click;
            var printBtn = Theme.MakePrimaryButton("🖨 Print");
            printBtn.Width = 120;
            printBtn.Click += PrintBtn_Click;

            btnPanel.Controls.Add(closeBtn);
            btnPanel.Controls.Add(exportPdfBtn);
            btnPanel.Controls.Add(printBtn);
            root.Controls.Add(btnPanel, 0, 2);

            Controls.Add(root);
        }

        private string BuildReportText()
        {
            var sb = new StringBuilder();
            sb.AppendLine("PHOTO STUDIO - DAILY REPORT");
            sb.AppendLine($"Date: {_close.CloseDate}");
            sb.AppendLine($"Closed at: {_close.ClosedAt:g}");
            sb.AppendLine(new string('-', 46));

            foreach (var li in _close.LineItems)
                sb.AppendLine($"{li.CategoryName,-24} {li.Count,4} x {li.UnitPrice,8:0.00} = {li.Subtotal,10:0.00}");

            if (_close.CustomCharges.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Custom charges:");
                foreach (var cc in _close.CustomCharges)
                    sb.AppendLine($"  {cc.Description,-30} = {cc.Amount,10:0.00}");
            }

            sb.AppendLine(new string('-', 46));
            sb.AppendLine($"TOTAL: {_close.GrandTotal:0.00}");
            return sb.ToString();
        }

        private void PrintBtn_Click(object sender, EventArgs e)
        {
            try
            {
                _printService.ShowPrintDialogAndPrint(_close);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not print:\n" + ex.Message, "Print error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ExportPdfBtn_Click(object sender, EventArgs e)
        {
            using (var dlg = new SaveFileDialog
            {
                Filter = "PDF file (*.pdf)|*.pdf",
                FileName = $"DailyReport_{_close.CloseDate}.pdf"
            })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;

                try
                {
                    _printService.ExportToPdf(_close, dlg.FileName);
                    MessageBox.Show("PDF exported successfully.", "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "PDF export unavailable", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }
    }
}
