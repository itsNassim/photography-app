using System;
using System.Windows.Forms;
using PhotoStudioApp.Data;
using PhotoStudioApp.UI.Dialogs;

namespace PhotoStudioApp.UI
{
    public class HistoryTab : UserControl
    {
        private readonly DailyCloseRepository _repo = new DailyCloseRepository();
        private DataGridView _grid;
        private Panel _lockedPanel;
        private Panel _contentPanel;
        private bool _unlocked;

        public HistoryTab()
        {
            BackColor = Theme.Background;
            BuildLayout();
            ShowLocked();
        }

        private void BuildLayout()
        {
            // --- Vue verrouillee ---
            _lockedPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background };
            var lockLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            lockLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            lockLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            lockLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var centerPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, Anchor = AnchorStyles.None, AutoSize = true };
            var lockLabel = new Label
            {
                Text = "🔒 L'historique est protege par mot de passe.",
                Font = Theme.FontSubheading,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 12),
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter
            };
            var unlockBtn = Theme.MakePrimaryButton("Deverrouiller");
            unlockBtn.Width = 160;
            unlockBtn.Click += (s, e) => TryUnlock();
            centerPanel.Controls.Add(lockLabel);
            centerPanel.Controls.Add(unlockBtn);

            lockLayout.Controls.Add(new Panel(), 0, 0);
            lockLayout.Controls.Add(centerPanel, 0, 1);
            lockLayout.Controls.Add(new Panel(), 0, 2);
            _lockedPanel.Controls.Add(lockLayout);

            // --- Vue deverrouillee (historique) ---
            _contentPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background };
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(16) };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var title = new Label { Text = "Historique des journees cloturees", Font = Theme.FontHeading, AutoSize = true, Margin = new Padding(0, 0, 0, 10) };
            root.Controls.Add(title, 0, 0);

            var toolbar = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 8) };
            var viewBtn = Theme.MakeSecondaryButton("Voir / Reimprimer");
            viewBtn.Width = 160;
            viewBtn.Click += ViewBtn_Click;
            var refreshBtn = Theme.MakeSecondaryButton("⟳ Actualiser");
            refreshBtn.Width = 130;
            refreshBtn.Click += (s, e) => LoadHistory();
            toolbar.Controls.Add(viewBtn);
            toolbar.Controls.Add(refreshBtn);
            root.Controls.Add(toolbar, 0, 1);

            var gridPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.CardBackground };
            _grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true };
            Theme.StyleGrid(_grid);
            _grid.Columns.Add("Date", "Date de cloture");
            _grid.Columns.Add("ClosedAt", "Cloturee le");
            _grid.Columns.Add("Total", "Total");
            _grid.Columns.Add("Archive", "Dossier d'archive");
            _grid.CellDoubleClick += (s, e) => OpenSelected();
            gridPanel.Controls.Add(_grid);
            root.Controls.Add(gridPanel, 0, 2);

            _contentPanel.Controls.Add(root);

            Controls.Add(_contentPanel);
            Controls.Add(_lockedPanel);
        }

        private void ShowLocked()
        {
            _lockedPanel.Visible = true;
            _lockedPanel.BringToFront();
            _contentPanel.Visible = false;
        }

        private void ShowContent()
        {
            _contentPanel.Visible = true;
            _contentPanel.BringToFront();
            _lockedPanel.Visible = false;
        }

        private void TryUnlock()
        {
            if (AuthGate.RequireAdminPassword(this))
            {
                _unlocked = true;
                ShowContent();
                LoadHistory();
            }
        }

        /// <summary>Called by MainForm whenever this tab is selected. Does nothing if the
        /// tab hasn't been unlocked yet - the locked panel stays showing.</summary>
        public void RefreshAll()
        {
            if (_unlocked)
                LoadHistory();
        }

        private void LoadHistory()
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
