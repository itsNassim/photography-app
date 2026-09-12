using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PhotoStudioApp.UI.Controls
{
    /// <summary>A combo-box-like control whose OPEN DROPDOWN has its own search box built into
    /// it, at the top of the list - typing there filters the items shown below it live. This is
    /// different from a plain editable ComboBox (which only offers OS-level autocomplete in the
    /// main text field): here, the search box is physically part of the dropdown itself. Click
    /// the control (or its arrow) to open it; click an item (or press Enter) to select it and
    /// close the dropdown. Works with any list of objects - filtering and display both use each
    /// item's ToString().</summary>
    public class SearchableComboBox : UserControl
    {
        public event EventHandler SelectedItemChanged;

        private readonly TextBox _displayBox;
        private readonly Button _arrowBtn;
        private List<object> _allItems = new List<object>();
        private object _selectedItem;

        private ToolStripDropDown _popup;
        private TextBox _popupSearchBox;
        private ListBox _popupListBox;

        public object SelectedItem
        {
            get => _selectedItem;
            set
            {
                _selectedItem = value;
                _displayBox.Text = value?.ToString() ?? "";
                SelectedItemChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public SearchableComboBox()
        {
            Height = 23;

            _arrowBtn = new Button { Text = "\u25BC", Width = 24, Dock = DockStyle.Right, FlatStyle = FlatStyle.Flat };
            _arrowBtn.FlatAppearance.BorderSize = 1;
            _arrowBtn.Click += (s, e) => OpenPopup();

            _displayBox = new TextBox { ReadOnly = true, Dock = DockStyle.Fill, Cursor = Cursors.Hand, BackColor = Color.White };
            _displayBox.Click += (s, e) => OpenPopup();

            // Dock=Right must be added before Dock=Fill so the fill control correctly claims
            // only the remaining space instead of the arrow button's area.
            Controls.Add(_displayBox);
            Controls.Add(_arrowBtn);
        }

        /// <summary>Replaces the full list of selectable items (shown, unfiltered, every time
        /// the dropdown is freshly opened).</summary>
        public void SetItems(IEnumerable<object> items)
        {
            _allItems = items.ToList();
        }

        private void OpenPopup()
        {
            _popup = new ToolStripDropDown { AutoClose = true, DropShadowEnabled = true, Padding = new Padding(2) };

            var panel = new TableLayoutPanel
            {
                Width = Math.Max(Width, 280),
                Height = 260,
                ColumnCount = 1,
                RowCount = 2
            };
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _popupSearchBox = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(2, 2, 2, 4) };
            _popupListBox = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false, Margin = new Padding(2, 0, 2, 2) };

            foreach (var item in _allItems)
                _popupListBox.Items.Add(item);
            if (_selectedItem != null && _popupListBox.Items.Contains(_selectedItem))
                _popupListBox.SelectedItem = _selectedItem;

            _popupSearchBox.TextChanged += (s, e) => FilterList();
            _popupListBox.Click += (s, e) => CommitSelectionAndClose();
            _popupListBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    CommitSelectionAndClose();
                    e.Handled = true;
                }
            };
            _popupSearchBox.KeyDown += (s, e) =>
            {
                // Lets the user type to filter, then immediately arrow down into the results
                // and press Enter, without needing to click into the list first.
                if (e.KeyCode == Keys.Down && _popupListBox.Items.Count > 0)
                {
                    _popupListBox.Focus();
                    _popupListBox.SelectedIndex = 0;
                    e.Handled = true;
                }
                else if (e.KeyCode == Keys.Enter && _popupListBox.Items.Count > 0)
                {
                    _popupListBox.SelectedIndex = 0;
                    CommitSelectionAndClose();
                    e.Handled = true;
                }
            };

            panel.Controls.Add(_popupSearchBox, 0, 0);
            panel.Controls.Add(_popupListBox, 0, 1);

            var host = new ToolStripControlHost(panel) { AutoSize = false, Margin = Padding.Empty };
            _popup.Items.Add(host);
            _popup.Show(this, new Point(0, Height));
            _popupSearchBox.Focus();
        }

        private void CommitSelectionAndClose()
        {
            if (_popupListBox.SelectedItem != null)
                SelectedItem = _popupListBox.SelectedItem;
            _popup.Close();
        }

        private void FilterList()
        {
            string term = _popupSearchBox.Text ?? "";
            _popupListBox.Items.Clear();
            foreach (var item in _allItems)
            {
                if (term.Length == 0 || item.ToString().IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                    _popupListBox.Items.Add(item);
            }
        }
    }
}
