using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Windows.Forms;
using PhotoStudioApp.Models;

namespace PhotoStudioApp.Services
{
    /// <summary>Renders a DailyClose as a simple receipt-style report, for both printing
    /// (PrintDocument/PrintDialog) and PDF export via the Microsoft Print to PDF driver.</summary>
    public class ReportPrintService
    {
        private DailyClose _close;
        private readonly Font _titleFont = new Font("Consolas", 13, FontStyle.Bold);
        private readonly Font _bodyFont = new Font("Consolas", 10);
        private readonly Font _totalFont = new Font("Consolas", 12, FontStyle.Bold);

        public PrintDocument BuildDocument(DailyClose close)
        {
            _close = close;
            var doc = new PrintDocument();
            doc.DocumentName = $"Daily Report - {close.CloseDate}";
            doc.PrintPage += Doc_PrintPage;
            return doc;
        }

        /// <summary>Shows the standard Windows print dialog then prints if confirmed.</summary>
        public bool ShowPrintDialogAndPrint(DailyClose close)
        {
            var doc = BuildDocument(close);
            using (var dlg = new PrintDialog { Document = doc, AllowSomePages = false, AllowSelection = false })
            {
                if (dlg.ShowDialog() != DialogResult.OK)
                    return false;
                doc.Print();
                return true;
            }
        }

        /// <summary>Exports to PDF by printing to the built-in "Microsoft Print to PDF" driver,
        /// if present on the machine (standard on Windows 8.1+; on Windows 7 it may be absent,
        /// in which case this throws and the caller should fall back to Print only).</summary>
        public void ExportToPdf(DailyClose close, string outputPdfPath)
        {
            _close = close;
            const string pdfPrinterName = "Microsoft Print to PDF";

            bool found = false;
            foreach (string name in PrinterSettings.InstalledPrinters)
            {
                if (name == pdfPrinterName) { found = true; break; }
            }
            if (!found)
                throw new System.IO.FileNotFoundException(
                    "\"Microsoft Print to PDF\" is not installed on this machine. " +
                    "PDF export needs it (or a third-party PDF printer driver such as CutePDF/PDFCreator on Windows 7). " +
                    "Use Print instead, or install a PDF printer driver.");

            var doc = new PrintDocument
            {
                PrinterSettings = new PrinterSettings
                {
                    PrinterName = pdfPrinterName,
                    PrintToFile = true,
                    PrintFileName = outputPdfPath
                }
            };
            doc.PrintPage += Doc_PrintPage;
            doc.Print();
        }

        private void Doc_PrintPage(object sender, PrintPageEventArgs e)
        {
            var g = e.Graphics;
            float y = 40;
            float x = 40;
            float lineHeight = 22;

            g.DrawString("PHOTO STUDIO - DAILY REPORT", _titleFont, Brushes.Black, x, y);
            y += lineHeight + 6;
            g.DrawString($"Date: {_close.CloseDate}", _bodyFont, Brushes.Black, x, y);
            y += lineHeight;
            g.DrawString($"Closed at: {_close.ClosedAt:g}", _bodyFont, Brushes.Black, x, y);
            y += lineHeight + 10;

            g.DrawLine(Pens.Black, x, y, e.PageBounds.Width - x, y);
            y += 10;

            foreach (var li in _close.LineItems)
            {
                string line = $"{li.CategoryName,-28} {li.Count,4} x {li.UnitPrice,8:0.00} = {li.Subtotal,10:0.00}";
                g.DrawString(line, _bodyFont, Brushes.Black, x, y);
                y += lineHeight;
            }

            if (_close.CustomCharges.Count > 0)
            {
                y += 6;
                g.DrawString("Custom charges:", _bodyFont, Brushes.Black, x, y);
                y += lineHeight;
                foreach (var cc in _close.CustomCharges)
                {
                    string line = $"  {cc.Description,-34} = {cc.Amount,10:0.00}";
                    g.DrawString(line, _bodyFont, Brushes.Black, x, y);
                    y += lineHeight;
                }
            }

            y += 10;
            g.DrawLine(Pens.Black, x, y, e.PageBounds.Width - x, y);
            y += 14;
            g.DrawString($"TOTAL: {_close.GrandTotal:0.00}", _totalFont, Brushes.Black, x, y);

            e.HasMorePages = false;
        }
    }
}
