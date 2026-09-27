using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Windows.Forms;
using PhotoStudioApp.Models;

namespace PhotoStudioApp.Services
{
    /// <summary>Renders a DailyClose as a simple receipt-style report, for both printing
    /// (PrintDocument/PrintDialog) and PDF export via the Microsoft Print to PDF driver.
    /// Also renders a MonthSummary (with its day-by-day breakdown) the same way.</summary>
    public class ReportPrintService
    {
        private DailyClose _close;
        private MonthSummary _monthSummary;
        private List<DailyClose> _monthDays;
        private List<MonthlyExpense> _monthExpenses;
        private readonly Font _titleFont = new Font("Consolas", 13, FontStyle.Bold);
        private readonly Font _bodyFont = new Font("Consolas", 10);
        private readonly Font _totalFont = new Font("Consolas", 12, FontStyle.Bold);

        public PrintDocument BuildDocument(DailyClose close)
        {
            _close = close;
            _monthSummary = null;
            _monthDays = null;
            _monthExpenses = null;
            var doc = new PrintDocument();
            doc.DocumentName = $"Rapport du jour - {close.CloseDate}";
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

        /// <summary>Builds a printable document for a month's summary - total revenue plus the
        /// day-by-day breakdown that makes up that total, and the manual month-level expenses
        /// (rent, subscriptions, etc. - see MonthlyExpenseRepository) entered against this
        /// month.</summary>
        public PrintDocument BuildMonthDocument(MonthSummary summary, List<DailyClose> daysInMonth, List<MonthlyExpense> monthlyExpenses)
        {
            _monthSummary = summary;
            _monthDays = daysInMonth;
            _monthExpenses = monthlyExpenses;
            _close = null;
            var doc = new PrintDocument();
            doc.DocumentName = $"Rapport mensuel - {summary.MonthLabel}";
            doc.PrintPage += DocMonth_PrintPage;
            return doc;
        }

        /// <summary>Shows the standard Windows print dialog then prints the month summary if confirmed.</summary>
        public bool ShowPrintDialogAndPrintMonth(MonthSummary summary, List<DailyClose> daysInMonth, List<MonthlyExpense> monthlyExpenses)
        {
            var doc = BuildMonthDocument(summary, daysInMonth, monthlyExpenses);
            using (var dlg = new PrintDialog { Document = doc, AllowSomePages = false, AllowSelection = false })
            {
                if (dlg.ShowDialog() != DialogResult.OK)
                    return false;
                doc.Print();
                return true;
            }
        }

        /// <summary>Same PDF-export path as ExportToPdf, but for a month summary.</summary>
        public void ExportMonthToPdf(MonthSummary summary, List<DailyClose> daysInMonth, List<MonthlyExpense> monthlyExpenses, string outputPdfPath)
        {
            _monthSummary = summary;
            _monthDays = daysInMonth;
            _monthExpenses = monthlyExpenses;
            _close = null;
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
            doc.PrintPage += DocMonth_PrintPage;
            doc.Print();
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

            g.DrawString("STUDIO PHOTO - RAPPORT DU JOUR", _titleFont, Brushes.Black, x, y);
            y += lineHeight + 6;
            g.DrawString($"Date : {_close.CloseDate}", _bodyFont, Brushes.Black, x, y);
            y += lineHeight;
            g.DrawString($"Clôture le : {_close.ClosedAt:g}", _bodyFont, Brushes.Black, x, y);
            y += lineHeight + 10;

            g.DrawLine(Pens.Black, x, y, e.PageBounds.Width - x, y);
            y += 10;

            foreach (var li in _close.LineItems)
            {
                string line = $"{li.CategoryName,-28} {li.Count,4} x {li.UnitPrice,8:0.00} = {li.Subtotal,10:0.00}";
                g.DrawString(line, _bodyFont, Brushes.Black, x, y);
                y += lineHeight;
                if (li.PurgedCount > 0)
                {
                    g.DrawString($"  (dont {li.PurgedCount} rejete(s)/duplique(s) calcule(s) puis supprime(s))", _bodyFont, Brushes.Gray, x, y);
                    y += lineHeight;
                }
            }

            if (_close.CustomCharges.Count > 0)
            {
                y += 6;
                g.DrawString("Charges additionnelles :", _bodyFont, Brushes.Black, x, y);
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
            g.DrawString($"TOTAL (revenu) : {_close.GrandTotal:0.00}", _totalFont, Brushes.Black, x, y);
            y += lineHeight + 10;

            if (_close.Expenses.Count > 0)
            {
                g.DrawString("Dépenses (achats pour le magasin) :", _bodyFont, Brushes.Black, x, y);
                y += lineHeight;
                foreach (var ex in _close.Expenses)
                {
                    string line = $"  {ex.Description,-32} = -{ex.Amount,9:0.00}";
                    g.DrawString(line, _bodyFont, Brushes.Black, x, y);
                    y += lineHeight;
                }
                g.DrawString($"Total des dépenses : -{_close.TotalExpenses:0.00}", _bodyFont, Brushes.Black, x, y);
                y += lineHeight + 6;
                g.DrawLine(Pens.Black, x, y, e.PageBounds.Width - x, y);
                y += 14;
            }

            g.DrawString($"TOTAL NET : {_close.NetTotal:0.00}", _totalFont, Brushes.Black, x, y);

            e.HasMorePages = false;
        }

        private void DocMonth_PrintPage(object sender, PrintPageEventArgs e)
        {
            var g = e.Graphics;
            float y = 40;
            float x = 40;
            float lineHeight = 22;

            g.DrawString("STUDIO PHOTO - RAPPORT MENSUEL", _titleFont, Brushes.Black, x, y);
            y += lineHeight + 6;
            g.DrawString($"Mois : {_monthSummary.MonthLabel}", _bodyFont, Brushes.Black, x, y);
            y += lineHeight;
            g.DrawString($"Journées clôturées : {_monthSummary.DaysClosed}", _bodyFont, Brushes.Black, x, y);
            y += lineHeight + 10;

            g.DrawLine(Pens.Black, x, y, e.PageBounds.Width - x, y);
            y += 10;

            foreach (var day in _monthDays)
            {
                string line = $"{day.CloseDate,-16} revenu {day.GrandTotal,10:0.00}   dépenses -{day.TotalExpenses,8:0.00}   net {day.NetTotal,10:0.00}";
                g.DrawString(line, _bodyFont, Brushes.Black, x, y);
                y += lineHeight;
            }

            if (_monthExpenses != null && _monthExpenses.Count > 0)
            {
                y += 6;
                g.DrawString("Dépenses du mois (loyer, abonnements, etc.) :", _bodyFont, Brushes.Black, x, y);
                y += lineHeight;
                foreach (var me in _monthExpenses)
                {
                    string line = $"  {me.DateAdded:dd/MM/yyyy}  {me.Description,-28} = -{me.Amount,9:0.00}";
                    g.DrawString(line, _bodyFont, Brushes.Black, x, y);
                    y += lineHeight;
                }
            }

            y += 10;
            g.DrawLine(Pens.Black, x, y, e.PageBounds.Width - x, y);
            y += 14;
            g.DrawString($"TOTAL DU MOIS (revenu) : {_monthSummary.TotalRevenue:0.00}", _totalFont, Brushes.Black, x, y);
            y += lineHeight;
            g.DrawString($"Total des dépenses du mois : -{_monthSummary.TotalExpenses:0.00}", _bodyFont, Brushes.Black, x, y);
            y += lineHeight;
            g.DrawString($"TOTAL NET DU MOIS : {_monthSummary.NetRevenue:0.00}", _totalFont, Brushes.Black, x, y);

            e.HasMorePages = false;
        }
    }
}
