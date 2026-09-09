using System.Drawing;
using System.Windows.Forms;

namespace PhotoStudioApp.UI
{
    public class MainForm : Form
    {
        private TabControl _tabs;
        private HomeTab _homeTab;
        private StockTab _stockTab;
        private CategoriesTab _categoriesTab;
        private HistoryTab _historyTab;
        private SettingsTab _settingsTab;

        public MainForm()
        {
            Text = "Studio Photo - Production et revenus du jour";
            Width = 1180;
            Height = 760;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1000, 640);
            BackColor = Theme.Background;
            Font = Theme.FontBase;
            Icon = TryLoadIcon();

            BuildLayout();
        }

        private Icon TryLoadIcon()
        {
            try { return new Icon(System.IO.Path.Combine(Application.StartupPath, "Properties", "app.ico")); }
            catch { return null; }
        }

        private void BuildLayout()
        {
            _tabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = Theme.FontSubheading,
                Padding = new Point(18, 8)
            };

            var homeTabPage = new TabPage("Accueil") { BackColor = Theme.Background };
            var stockTabPage = new TabPage("Stock") { BackColor = Theme.Background };
            var categoriesTabPage = new TabPage("Categories / Tarifs") { BackColor = Theme.Background };
            var historyTabPage = new TabPage("Historique") { BackColor = Theme.Background };
            var settingsTabPage = new TabPage("Parametres") { BackColor = Theme.Background };

            _homeTab = new HomeTab { Dock = DockStyle.Fill };
            _stockTab = new StockTab { Dock = DockStyle.Fill };
            _categoriesTab = new CategoriesTab { Dock = DockStyle.Fill };
            _historyTab = new HistoryTab { Dock = DockStyle.Fill };
            _settingsTab = new SettingsTab { Dock = DockStyle.Fill };

            // Cross-tab refresh wiring: changing prices/categories or settings should refresh Home.
            _categoriesTab.CategoriesChanged += (s, e) => _homeTab.RefreshAll();
            _stockTab.StockChanged += (s, e) => _homeTab.RefreshCustomChargeStockOptions();
            _settingsTab.SettingsSaved += (s, e) => _homeTab.RefreshAll();
            _homeTab.DayClosed += (s, e) => _historyTab.RefreshAll();

            homeTabPage.Controls.Add(_homeTab);
            stockTabPage.Controls.Add(_stockTab);
            categoriesTabPage.Controls.Add(_categoriesTab);
            historyTabPage.Controls.Add(_historyTab);
            settingsTabPage.Controls.Add(_settingsTab);

            _tabs.TabPages.Add(homeTabPage);
            _tabs.TabPages.Add(stockTabPage);
            _tabs.TabPages.Add(categoriesTabPage);
            _tabs.TabPages.Add(historyTabPage);
            _tabs.TabPages.Add(settingsTabPage);

            _tabs.SelectedIndexChanged += (s, e) =>
            {
                if (_tabs.SelectedTab == homeTabPage) _homeTab.RefreshAll();
                else if (_tabs.SelectedTab == stockTabPage) _stockTab.RefreshAll();
                else if (_tabs.SelectedTab == categoriesTabPage) _categoriesTab.RefreshAll();
                else if (_tabs.SelectedTab == historyTabPage) _historyTab.RefreshAll();
                else if (_tabs.SelectedTab == settingsTabPage) _settingsTab.RefreshAll();
            };

            Controls.Add(_tabs);
        }
    }
}
