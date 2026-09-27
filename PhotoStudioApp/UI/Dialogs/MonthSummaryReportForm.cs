using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using PhotoStudioApp.Data;
using PhotoStudioApp.Models;
using PhotoStudioApp.Services;

namespace PhotoStudioApp.UI.Dialogs
{
    /// <summary>Shows the printable "Résumé mensuel" (month summary) report: total revenue for
    /// the month, the day-by-day breakdown that makes it up, and the manual month-level expenses
    /// (rent, subscriptions, etc.) entered against this month. Mirrors BreakdownReportForm's
    /// layout/behavior (view/print/export) for the daily report.</summary>
    public class MonthSummaryReportForm : Form
    {
        private readonly MonthSummary _summary;
        private readonly List<DailyClose> _days;
        private readonly List<MonthlyExpense> _monthExpenses;
        private readonly ReportPrintService _printService = new ReportPrintService();

        public MonthSummaryReportForm(MonthSummary summary, List<DailyClose> days)
        {
            _summary = summary;
            _days = days;
            _monthExpenses = new MonthlyExpenseRepository().GetForMonth(summary.YearMonth);
            Text = $"Rapport mensuel - {summary.MonthLabel}";
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

            var title = new Label { Text = $"Rapport mensuel - {_summary.MonthLabel}", Font = Theme.FontHeading, AutoSize = true, Margin = new Padding(0, 0, 0, 10) };
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
            var closeBtn = Theme.MakeSecondaryButton("Fermer");
            closeBtn.Width = 100;
            closeBtn.Click += (s, e) => Close();
            var exportPdfBtn = Theme.MakeSecondaryButton("Exporter en PDF");
            exportPdfBtn.Width = 150;
            exportPdfBtn.Click += ExportPdfBtn_Click;
            var printBtn = Theme.MakePrimaryButton("Imprimer");
            printBtn.Image = IconGlyphs.Printer(16);
            printBtn.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            printBtn.TextImageRelation = TextImageRelation.ImageBeforeText;
            printBtn.Width = 130;
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
            sb.AppendLine("STUDIO PHOTO - RAPPORT MENSUEL");
            sb.AppendLine($"Mois : {_summary.MonthLabel}");
            sb.AppendLine($"Journées clôturées : {_summary.DaysClosed}");
            sb.AppendLine(new string('-', 46));

            foreach (var day in _days)
                sb.AppendLine($"{day.CloseDate,-14} revenu {day.GrandTotal,9:0.00}  dépenses -{day.TotalExpenses,8:0.00}  net {day.NetTotal,9:0.00}");

            if (_monthExpenses.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Dépenses du mois (loyer, abonnements, etc.) :");
                foreach (var me in _monthExpenses)
                    sb.AppendLine($"  {me.DateAdded:dd/MM/yyyy}  {me.Description,-28} -{me.Amount,9:0.00}");
            }

            sb.AppendLine(new string('-', 46));
            sb.AppendLine($"TOTAL DU MOIS (revenu) : {_summary.TotalRevenue:0.00}");
            sb.AppendLine($"Total des dépenses du mois : -{_summary.TotalExpenses:0.00}");
            sb.AppendLine($"TOTAL NET DU MOIS : {_summary.NetRevenue:0.00}");
            return sb.ToString();
        }

        private void PrintBtn_Click(object sender, EventArgs e)
        {
            try
            {
                _printService.ShowPrintDialogAndPrintMonth(_summary, _days, _monthExpenses);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Impossible d'imprimer :\n" + ex.Message, "Erreur d'impression", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ExportPdfBtn_Click(object sender, EventArgs e)
        {
            using (var dlg = new SaveFileDialog
            {
                Filter = "Fichier PDF (*.pdf)|*.pdf",
                FileName = $"RapportMensuel_{_summary.YearMonth}.pdf"
            })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;

                try
                {
                    _printService.ExportMonthToPdf(_summary, _days, _monthExpenses, dlg.FileName);
                    MessageBox.Show("PDF exporté avec succès.", "Termine", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Export PDF indisponible", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }
    }
}
