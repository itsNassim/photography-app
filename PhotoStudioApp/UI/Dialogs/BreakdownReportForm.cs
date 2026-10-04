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
        private readonly bool _isPreview;
        private readonly ReportPrintService _printService = new ReportPrintService();

        /// <summary>isPreview=true is for ClosingService.PreviewCurrentState's result: a
        /// DailyClose computed live for "Voir l'état actuel" that was never actually closed/
        /// archived/saved - the title and report header say so plainly, so it's never mistaken
        /// for an actual closed-day record.</summary>
        public BreakdownReportForm(DailyClose close, bool isPreview = false)
        {
            _close = close;
            _isPreview = isPreview;
            Text = isPreview
                ? $"Aperçu de l'état actuel (journée non clôturée) - {close.CloseDate}"
                : $"Rapport du jour - {close.CloseDate}";
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

            // Title (+ an extra notice line when this is a live preview, not an actual closed
            // day) stacked in a small top-down panel, so both sit in row 0 and the textbox/
            // buttons rows below never need to shift.
            var header = new FlowLayoutPanel { Dock = DockStyle.Top, FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false };
            header.Controls.Add(new Label
            {
                Text = _isPreview
                    ? $"Aperçu de l'état actuel (non clôturé) - {_close.CloseDate}"
                    : $"Rapport du jour - {_close.CloseDate}",
                Font = Theme.FontHeading, AutoSize = true, Margin = new Padding(0, 0, 0, _isPreview ? 4 : 10)
            });
            if (_isPreview)
            {
                header.Controls.Add(new Label
                {
                    Text = "Ceci est un aperçu des montants actuels - rien n'a été archivé ni enregistré. Fermez simplement cette fenêtre pour continuer la journée normalement.",
                    AutoSize = true, ForeColor = System.Drawing.Color.Gray, Margin = new Padding(0, 0, 0, 10), MaximumSize = new System.Drawing.Size(500, 0)
                });
            }
            root.Controls.Add(header, 0, 0);

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
            if (_isPreview)
            {
                sb.AppendLine("STUDIO PHOTO - APERÇU DE L'ÉTAT ACTUEL (JOURNÉE NON CLÔTURÉE)");
                sb.AppendLine($"Date (journée en cours) : {_close.CloseDate}");
                sb.AppendLine($"Aperçu généré le : {_close.ClosedAt:g}");
            }
            else
            {
                sb.AppendLine("STUDIO PHOTO - RAPPORT DU JOUR");
                sb.AppendLine($"Date : {_close.CloseDate}");
                sb.AppendLine($"Clôture le : {_close.ClosedAt:g}");
            }
            sb.AppendLine(new string('-', 46));

            foreach (var li in _close.LineItems)
            {
                sb.AppendLine($"{li.CategoryName,-24} {li.Count,4} x {li.UnitPrice,8:0.00} = {li.Subtotal,10:0.00}");
                if (li.PurgedCount > 0)
                    sb.AppendLine($"  (dont {li.PurgedCount} fichier(s) rejete(s)/dupliques calcule(s) puis supprime(s))");
            }

            if (_close.CustomCharges.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Charges additionnelles :");
                foreach (var cc in _close.CustomCharges)
                    sb.AppendLine($"  {cc.Description,-30} = {cc.Amount,10:0.00}");
            }

            sb.AppendLine(new string('-', 46));
            sb.AppendLine($"TOTAL (revenu) : {_close.GrandTotal:0.00}");

            if (_close.Expenses.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Dépenses (achats pour le magasin) :");
                foreach (var ex in _close.Expenses)
                    sb.AppendLine($"  {ex.Description,-30} = -{ex.Amount,9:0.00}");
                sb.AppendLine($"Total des dépenses : -{_close.TotalExpenses:0.00}");
                sb.AppendLine(new string('-', 46));
            }

            sb.AppendLine($"TOTAL NET : {_close.NetTotal:0.00}");
            return sb.ToString();
        }

        private void PrintBtn_Click(object sender, EventArgs e)
        {
            try
            {
                _printService.ShowPrintDialogAndPrint(_close, _isPreview);
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
                FileName = _isPreview ? $"ApercuEtatActuel_{_close.CloseDate}.pdf" : $"RapportDuJour_{_close.CloseDate}.pdf"
            })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;

                try
                {
                    _printService.ExportToPdf(_close, dlg.FileName, _isPreview);
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
