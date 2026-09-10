using System.Drawing;
using System.Windows.Forms;

namespace PhotoStudioApp.UI
{
    /// <summary>Central color/font palette so the app doesn't look like default WinForms gray.
    /// Teal/blue primary, light backgrounds, green for positive totals, red reserved for
    /// warnings/out-of-stock/errors - per the spec.</summary>
    public static class Theme
    {
        public static readonly Color Primary = ColorTranslator.FromHtml("#00707A");      // teal
        public static readonly Color PrimaryDark = ColorTranslator.FromHtml("#00505A");
        public static readonly Color Background = ColorTranslator.FromHtml("#F4F6F7");   // light gray
        public static readonly Color CardBackground = Color.White;
        public static readonly Color TextDark = ColorTranslator.FromHtml("#222831");
        public static readonly Color Success = ColorTranslator.FromHtml("#1E8E3E");      // green totals
        public static readonly Color Warning = ColorTranslator.FromHtml("#C62828");      // red - warnings only
        public static readonly Color WarningBackground = ColorTranslator.FromHtml("#FDEAEA");
        public static readonly Color LowStockBackground = ColorTranslator.FromHtml("#FFF6DC");
        public static readonly Color LowStockColor = ColorTranslator.FromHtml("#8A6D00");  // amber text for low-stock rows
        public static readonly Color BorderGray = ColorTranslator.FromHtml("#D7DDE0");

        // Normal-row selection color (used both by the grid default and explicitly re-applied
        // to non-warning rows, since setting a row's DefaultCellStyle at all overrides the
        // grid-level default for that row).
        public static readonly Color SelectionBackColor = ColorTranslator.FromHtml("#CFE8EA");
        public static readonly Color SelectionForeColor = TextDark;

        // Selected-state colors for warning/low-stock rows - a plain DataGridView paints every
        // selected row with the same selection color regardless of that row's own colors, which
        // would hide the red/yellow warning the moment the row is clicked. These are deliberately
        // a DARKER/stronger shade of the same red/yellow (not the neutral teal selection color)
        // so the row still reads as "out of stock" / "low stock" while still looking selected.
        public static readonly Color WarningSelectionBackColor = ColorTranslator.FromHtml("#C62828"); // solid red
        public static readonly Color WarningSelectionForeColor = Color.White;
        public static readonly Color LowStockSelectionBackColor = ColorTranslator.FromHtml("#FFC107"); // solid amber
        public static readonly Color LowStockSelectionForeColor = ColorTranslator.FromHtml("#3D2E00");

        public static readonly Font FontBase = new Font("Segoe UI", 9.5f);
        public static readonly Font FontHeading = new Font("Segoe UI Semibold", 15f, FontStyle.Bold);
        public static readonly Font FontSubheading = new Font("Segoe UI Semibold", 11f, FontStyle.Bold);
        public static readonly Font FontTotal = new Font("Segoe UI", 20f, FontStyle.Bold);
        public static readonly Font FontButton = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);

        public static Button MakePrimaryButton(string text)
        {
            var b = new Button
            {
                Text = text,
                Font = FontButton,
                BackColor = Primary,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Height = 38,
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = PrimaryDark;
            return b;
        }

        public static Button MakeSecondaryButton(string text)
        {
            var b = new Button
            {
                Text = text,
                Font = FontButton,
                BackColor = Color.White,
                ForeColor = Primary,
                FlatStyle = FlatStyle.Flat,
                Height = 34,
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = Primary;
            b.FlatAppearance.MouseOverBackColor = ColorTranslator.FromHtml("#E6F1F2");
            return b;
        }

        public static Button MakeDangerButton(string text)
        {
            var b = new Button
            {
                Text = text,
                Font = FontButton,
                BackColor = Warning,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Height = 34,
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 0;
            return b;
        }

        public static void StyleGrid(DataGridView grid)
        {
            grid.BackgroundColor = CardBackground;
            grid.BorderStyle = BorderStyle.None;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.GridColor = BorderGray;
            grid.RowHeadersVisible = false;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToResizeRows = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = false;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ColumnHeadersHeight = 34;
            grid.RowTemplate.Height = 30;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Primary;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            grid.ColumnHeadersDefaultCellStyle.Font = FontSubheading;
            grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            grid.DefaultCellStyle.Font = FontBase;
            grid.DefaultCellStyle.SelectionBackColor = SelectionBackColor;
            grid.DefaultCellStyle.SelectionForeColor = SelectionForeColor;
            grid.AlternatingRowsDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#FAFBFC");
        }
    }
}
