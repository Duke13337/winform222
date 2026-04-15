using Newtonsoft.Json;
using System.Text;

namespace RegionSnabApp;

public partial class Form1 : Form
{
    private List<Order> _allOrders = new(); 
    private DataGridView _grid = new();     
    private TextBox _txtSearch = new();
    private Label _lblLiveTotal = new Label(); // Для живого расчета

    public Form1()
    {
        InitializeComponent();
        SetupCustomUI();
        LoadData();
    }

    private void SetupCustomUI()
    {
        this.Text = "РегионСнаб: Учёт заявок";
        this.Size = new Size(1200, 750);
        this.StartPosition = FormStartPosition.CenterScreen;

        TableLayoutPanel mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3 };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 65F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 120F)); // Чуть выше для итогов
        this.Controls.Add(mainLayout);

        // --- 1. ВЕРХНЯЯ ПАНЕЛЬ ---
        Panel topPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(240, 240, 240) };
        _txtSearch = new TextBox { Left = 120, Top = 20, Width = 250 };
        _txtSearch.TextChanged += (s, e) => ApplyFilters();
        topPanel.Controls.Add(new Label { Text = "Поиск товара:", Left = 20, Top = 22, Width = 90, Font = new Font("Segoe UI", 9, FontStyle.Bold) });
        topPanel.Controls.Add(_txtSearch);
        
        Button btnExport = new Button { Text = "💾 CSV Экспорт", Left = 380, Top = 16, Width = 120, Height = 32, BackColor = Color.White };
        btnExport.Click += (s, e) => ExportToCsv();
        topPanel.Controls.Add(btnExport);
        mainLayout.Controls.Add(topPanel, 0, 0);

        // --- 2. ТАБЛИЦА С ПОДСВЕТКОЙ И УДАЛЕНИЕМ ---
        _grid.Dock = DockStyle.Fill;
        _grid.BackgroundColor = Color.White;
        _grid.AllowUserToAddRows = false;
        _grid.AutoGenerateColumns = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.RowTemplate.Height = 35;
        _grid.ColumnHeadersHeight = 45;
        _grid.EnableHeadersVisualStyles = false;
        _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(0, 120, 215);
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;

        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "№", DataPropertyName = "Id", Width = 50 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Дата", DataPropertyName = "CreatedDate", Width = 110 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "ФИО", DataPropertyName = "Initiator", Width = 140 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Отдел", DataPropertyName = "Department", Width = 120 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Товар", DataPropertyName = "ItemName", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Кол.", DataPropertyName = "Quantity", Width = 60 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Цена", DataPropertyName = "Price", Width = 90 });

        var statusCol = new DataGridViewComboBoxColumn { HeaderText = "Статус", DataPropertyName = "Status", Width = 120 };
        statusCol.Items.AddRange("Новая", "Согласована", "Заказана", "Получена", "Отменена");
        _grid.Columns.Add(statusCol);

        // ДОРАБОТКА 1: Подсветка строк
        _grid.CellFormatting += (s, e) => {
            if (_grid.Columns[e.ColumnIndex].DataPropertyName == "Status" && e.Value != null) {
                string status = e.Value.ToString();
                DataGridViewRow row = _grid.Rows[e.RowIndex];
                if (status == "Получена") row.DefaultCellStyle.BackColor = Color.LightGreen;
                else if (status == "Отменена") row.DefaultCellStyle.BackColor = Color.MistyRose;
                else if (status == "Заказана") row.DefaultCellStyle.BackColor = Color.LightYellow;
                else row.DefaultCellStyle.BackColor = Color.White;
            }
        };

        // ДОРАБОТКА 2: Удаление по кнопке Delete
        _grid.KeyDown += (s, e) => {
            if (e.KeyCode == Keys.Delete && _grid.CurrentRow != null) {
                var order = (Order)_grid.CurrentRow.DataBoundItem;
                var res = MessageBox.Show($"Удалить заявку №{order.Id}?", "Удаление", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (res == DialogResult.Yes) {
                    _allOrders.Remove(order);
                    SaveData(); ApplyFilters();
                }
            }
        };

        _grid.CellValueChanged += (s, e) => { SaveData(); ApplyFilters(); };
        mainLayout.Controls.Add(_grid, 0, 1);

        // --- 3. НИЖНЯЯ ПАНЕЛЬ С ЖИВЫМ РАСЧЕТОМ ---
        Panel bottomPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(230, 235, 240) };
        TextBox tName = new TextBox { Left = 20, Top = 40, Width = 200, PlaceholderText = "Товар" };
        TextBox tInit = new TextBox { Left = 230, Top = 40, Width = 140, PlaceholderText = "ФИО" };
        TextBox tDep = new TextBox { Left = 380, Top = 40, Width = 110, PlaceholderText = "Отдел" };
        NumericUpDown nQty = new NumericUpDown { Left = 500, Top = 40, Width = 60, Minimum = 1, Value = 1 };
        TextBox tPrice = new TextBox { Left = 570, Top = 40, Width = 90, PlaceholderText = "Цена" };
        
        _lblLiveTotal.Text = "Итого: 0.00 руб.";
        _lblLiveTotal.Left = 570; _lblLiveTotal.Top = 70; _lblLiveTotal.Width = 200;
        _lblLiveTotal.Font = new Font("Segoe UI", 9, FontStyle.Bold);

        // ДОРАБОТКА 3: Живой расчет суммы
        EventHandler calcAction = (s, e) => {
            decimal.TryParse(tPrice.Text.Replace(".", ","), out decimal p);
            _lblLiveTotal.Text = $"К оплате: {p * nQty.Value:N2} руб.";
        };
        tPrice.TextChanged += calcAction;
        nQty.ValueChanged += calcAction;

        // Валидация (запрет цифр и букв)
        KeyPressEventHandler noDigits = (s, e) => { if (char.IsDigit(e.KeyChar)) e.Handled = true; };
        tInit.KeyPress += noDigits; tDep.KeyPress += noDigits;
        tPrice.KeyPress += (s, e) => {
            if (!char.IsDigit(e.KeyChar) && e.KeyChar != ',' && e.KeyChar != (char)8) e.Handled = true;
            if (e.KeyChar == ',' && tPrice.Text.Contains(",")) e.Handled = true;
        };

        Button btnAdd = new Button { Text = "✚ Добавить", Left = 680, Top = 35, Width = 140, Height = 40, BackColor = Color.LightGreen, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
        btnAdd.Click += (s, e) => {
            if (string.IsNullOrWhiteSpace(tName.Text) || string.IsNullOrWhiteSpace(tInit.Text)) return;
            _allOrders.Add(new Order {
                Id = _allOrders.Count > 0 ? _allOrders.Max(x => x.Id) + 1 : 1,
                ItemName = tName.Text, Initiator = tInit.Text, Department = tDep.Text,
                Quantity = (int)nQty.Value, 
                Price = decimal.TryParse(tPrice.Text.Replace(".", ","), out var p) ? p : 0,
                Status = "Новая"
            });
            SaveData(); ApplyFilters();
            tName.Clear(); tPrice.Clear();
        };

        bottomPanel.Controls.AddRange(new Control[] { tName, tInit, tDep, nQty, tPrice, btnAdd, _lblLiveTotal });
        mainLayout.Controls.Add(bottomPanel, 0, 2);
    }

    private void LoadData() {
        if (File.Exists("orders.json"))
            _allOrders = JsonConvert.DeserializeObject<List<Order>>(File.ReadAllText("orders.json")) ?? new();
        ApplyFilters();
    }

    private void SaveData() => File.WriteAllText("orders.json", JsonConvert.SerializeObject(_allOrders, Formatting.Indented));

    private void ApplyFilters() {
        _grid.DataSource = null;
        _grid.DataSource = _allOrders.Where(x => x.ItemName.Contains(_txtSearch.Text, StringComparison.OrdinalIgnoreCase)).ToList();
        decimal total = _allOrders.Where(x => x.Status == "Заказана").Sum(x => x.Price * x.Quantity);
        this.Text = $"РегионСнаб | Общая сумма 'Заказана': {total:N2} руб.";
    }

    private void ExportToCsv() {
        StringBuilder csv = new StringBuilder("ID;Дата;ФИО;Отдел;Товар;Кол;Цена;Статус\n");
        foreach (var o in _allOrders) csv.AppendLine($"{o.Id};{o.CreatedDate:dd.MM.yyyy};{o.Initiator};{o.Department};{o.ItemName};{o.Quantity};{o.Price};{o.Status}");
        File.WriteAllText("export_orders.csv", csv.ToString(), Encoding.UTF8);
        MessageBox.Show("Экспорт выполнен!");
    }
}
