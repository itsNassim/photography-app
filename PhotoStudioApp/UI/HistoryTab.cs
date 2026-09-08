using System;
using System.Windows.Forms;
using PhotoStudioApp.Data;
using PhotoStudioApp.Models;
using PhotoStudioApp.UI.Dialogs;

namespace PhotoStudioApp.UI
{
    public class HistoryTab : UserControl
    {
        private readonly DailyCloseRepository _repo = new DailyCloseRepository();
        private DataGridView _grid;

        public HistoryTab()
        {
            BackColor = Theme.Background;
            BuildLayout();
            RefreshAll();
        }

        private void BuildLayout()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(16) };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var title = new Label { Text = "History of closed days", Font = Theme.FontHeading, AutoSize = true, Margin = new Padding(0, 0, 0, 10) };
            root.Controls.Add(title, 0, 0);

            var toolbar = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 8) };
            var viewBtn = Theme.MakeSecondaryButton("View / Reprint");
            viewBtn.Width = 150;
            viewBtn.Click += ViewBtn_Click;
            var refreshBtn = Theme.MakeSecondaryButton("⟳ Refresh");
            refreshBtn.Width = 110;
            refreshBtn.Click += (s, e) => RefreshAll();
            toolbar.Controls.Add(viewBtn);
            toolbar.Controls.Add(refreshBtn);
            root.Controls.Add(toolbar, 0, 1);

            var gridPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.CardBackground };
            _grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true };
            Theme.StyleGrid(_grid);
            _grid.Columns.Add("Date", "Date closed");
            _grid.Columns.Add("ClosedAt", "Closed at");
            _grid.Columns.Add("Total", "Grand total");
            _grid.Columns.Add("Archive", "Archive folder");
            _grid.CellDoubleClick += (s, e) => OpenSelected();
            gridPanel.Controls.Add(_grid);
            root.Controls.Add(gridPanel, 0, 2);

            Controls.Add(root);
        }

        public void RefreshAll()
        {
            var history = _repo.GetHistory();
            _grid.Rows.Clear();
            foreach (var h in history)
            {
                int idx = _grid.Rows.Add(h.CloseDate, h.ClosedAt.ToString("g"), h.GrandTotal.ToString("0.00"), h.ArchiveFolderPath);
                _grid.Rows[idx].Tag = h.Id;
            }
        }

        private void ViewBtn_Click(object sender, EventArgs e) => OpenSelected();

        private void OpenSelected()
        {
            if (_grid.CurrentRow?.Tag == null) return;
            int id = (int)_grid.CurrentRow.Tag;
            var full = _repo.GetFullById(id);
            if (full == null) return;

            using (var breakdown = new BreakdownReportForm(full))
                breakdown.ShowDialog();
        }
    }
}
