using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Windows.Forms;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using System.Linq;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

public sealed class MainForm : Form
{
    // =========================================================
    // USER32
    // =========================================================

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT p);

    [DllImport("user32.dll")]
    private static extern void mouse_event(
        uint dwFlags,
        uint dx,
        uint dy,
        uint dwData,
        UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(
        IntPtr hWnd,
        int id,
        uint fsModifiers,
        uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(
        IntPtr hWnd,
        int id);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(
        EnumWindowsProc lpEnumFunc,
        IntPtr lParam);

    [DllImport(
        "user32.dll",
        CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(
        IntPtr hWnd,
        StringBuilder lpString,
        int nMaxCount);

    [DllImport(
        "user32.dll",
        CharSet = CharSet.Unicode)]
    private static extern int GetClassName(
        IntPtr hWnd,
        StringBuilder lpClassName,
        int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(
        IntPtr hWndParent,
        EnumWindowsProc lpEnumFunc,
        IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(
        string? lpClassName,
        string? lpWindowName);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(
        IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(
        IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(
        IntPtr hWnd,
        out RECT rect);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(
        IntPtr hWnd,
        int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(
        IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(
        IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(
        IntPtr hWnd,
        out uint processId);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(
        int X,
        int Y);

    // =========================================================
    // TYPES
    // =========================================================

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private delegate bool EnumWindowsProc(
        IntPtr hWnd,
        IntPtr lParam);

    private sealed class WindowInfo
    {
        public IntPtr Handle { get; set; }

        public string Title { get; set; } = "";

        public string ClassName { get; set; } = "";

        public uint ProcessId { get; set; }
    }

    // =========================================================
    // MOUSE
    // =========================================================

    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;

    private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;

    // =========================================================
    // WINDOW STATES
    // =========================================================

    private const int SW_MINIMIZE = 6;
    private const int SW_MAXIMIZE = 3;
    private const int SW_RESTORE = 9;

    // =========================================================
    // HOTKEY
    // =========================================================

    private const int HOTKEY_ESC = 1001;

    private const uint MOD_NOREPEAT = 0x4000;

    private const uint VK_ESCAPE = 0x1B;

    // =========================================================
    // ЭКРАНЫ
    // =========================================================

    private readonly string[] screenNames =
    {
        "Н-33, Н-34",
        "Р18",
        "Р20",
        "Катковка"
    };

    // =========================================================
    // КООРДИНАТЫ BFN
    // =========================================================

    private readonly string[] pointNames =
    {
        "Отчет Производство",
        "Damate Qlik",
        "Отчет Бункер"
    };

    private const string DischargeReportPoint =
        "Отчет Разряжение";

    // Фиксированные координаты стандартных кнопок BFNM.
    // Пользователь их больше не настраивает.
    private readonly Dictionary<string, Dictionary<string, Point>> fixedPoints =
        new(StringComparer.Ordinal)
        {
            ["Н-33, Н-34"] = new Dictionary<string, Point>(StringComparer.Ordinal)
            {
                ["Файл"] = new Point(-1800, -991),
                ["Экспорт"] = new Point(-1768, -972),
                ["Вернуться"] = new Point(-1568, -880)
            },
            ["Р20"] = new Dictionary<string, Point>(StringComparer.Ordinal)
            {
                ["Файл"] = new Point(90, -1022),
                ["Экспорт"] = new Point(117, -1004),
                ["Вернуться"] = new Point(359, -908)
            },
            ["Р18"] = new Dictionary<string, Point>(StringComparer.Ordinal)
            {
                ["Файл"] = new Point(-1807, 87),
                ["Экспорт"] = new Point(-1781, 109),
                ["Вернуться"] = new Point(-1547, 198)
            },
            ["Катковка"] = new Dictionary<string, Point>(StringComparer.Ordinal)
            {
                ["Файл"] = new Point(117, 86),
                ["Экспорт"] = new Point(142, 107),
                ["Вернуться"] = new Point(350, 202)
            }
        };

    // =========================================================
    // ДАННЫЕ
    // =========================================================

    private readonly Dictionary<
        string,
        Dictionary<string, Point>> allPoints = new();

    // =========================================================
    // UI
    // =========================================================

    private readonly Label instruction = new();

    private readonly Label mousePosition = new();

    private readonly Label currentScreenLabel = new();

    private readonly TextBox log = new();

    private readonly ComboBox screenSelector = new();

    private readonly Button setupButton = new();

    private readonly Button dischargeSetupButton = new();

    private readonly Button exportButton = new();

    private readonly Button dischargeExportButton = new();
    private readonly Button buildReportsButton = new();

    private readonly Button exportScreen1Button = new();

    private readonly Button exportScreen2Button = new();

    private readonly Button exportScreen3Button = new();

    private readonly Button exportScreen4Button = new();

    private readonly Button transferButton = new();

    private readonly Button foldersButton = new();

    private readonly Button cancelButton = new();

    private readonly Button settingsButton = new();

    private readonly Button journalButton = new();

    private Panel? journalPanel;

    // =========================================================
    // СОСТОЯНИЕ
    // =========================================================

    private int setupIndex = -1;

    private CancellationTokenSource? exportCancellation;

    private bool exportRunning;

    // =========================================================
    // НАСТРОЙКИ ПАПОК
    // =========================================================

    private string remoteReportsFolder = "";

    private string localReportsFolder = "";

    // =========================================================
    // ТЕКУЩИЙ ЭКРАН
    // =========================================================

    private string CurrentSetupScreen =>
        screenSelector.SelectedItem?.ToString()
        ?? screenNames[0];

    // =========================================================
    // ФАЙЛ НАСТРОЕК
    // =========================================================

    private string SettingsFolder
    {
        get
        {
            string folder =
                Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.ApplicationData),
                    "BFN_Exporter");

            Directory.CreateDirectory(folder);

            return folder;
        }
    }

    private string SettingsFile =>
        Path.Combine(
            SettingsFolder,
            "coordinates.json");

    private string TransferSettingsFile =>
        Path.Combine(
            SettingsFolder,
            "transfer-settings.json");

    // =========================================================
    // КОНСТРУКТОР
    // =========================================================

    public MainForm()
    {
        Text = "BFN Exporter — 4 экрана";
        Width = 760;
        Height = 560;
        MinimumSize = new Size(680, 500);
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;
        KeyDown += MainForm_KeyDown;
        BackColor = Color.FromArgb(30, 34, 40);
        ForeColor = Color.Gainsboro;
        Font = new Font("Segoe UI", 9.5f);

        // -----------------------------------------------------
        // СКРЫТЫЕ СЛУЖЕБНЫЕ ЭЛЕМЕНТЫ
        // -----------------------------------------------------
        screenSelector.Visible = false;
        setupButton.Visible = false;
        dischargeSetupButton.Visible = false;
        exportScreen1Button.Visible = false;
        exportScreen2Button.Visible = false;
        exportScreen3Button.Visible = false;
        exportScreen4Button.Visible = false;
        foldersButton.Visible = false;
        transferButton.Visible = false;

        foreach (string screen in screenNames)
            screenSelector.Items.Add(screen);
        screenSelector.SelectedIndex = 0;
        screenSelector.SelectedIndexChanged += (_, _) =>
        {
            UpdateCurrentScreenLabel();
            if (setupIndex < 0 && !exportRunning)
                UpdateInstructionForCurrentScreen();
        };

        // -----------------------------------------------------
        // ВЕРХНЯЯ ИНФОРМАЦИЯ
        // -----------------------------------------------------
        Panel header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 118,
            Padding = new Padding(18, 12, 18, 8),
            BackColor = Color.FromArgb(36, 41, 48)
        };

        Label title = new Label
        {
            Dock = DockStyle.Top,
            Height = 30,
            Text = "BFN EXPORTER",
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 13, FontStyle.Bold),
            ForeColor = Color.White
        };

        currentScreenLabel.Dock = DockStyle.Top;
        currentScreenLabel.Height = 27;
        currentScreenLabel.TextAlign = ContentAlignment.MiddleLeft;
        currentScreenLabel.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        currentScreenLabel.ForeColor = Color.Gainsboro;

        mousePosition.Dock = DockStyle.Top;
        mousePosition.Height = 25;
        mousePosition.TextAlign = ContentAlignment.MiddleLeft;
        mousePosition.Font = new Font("Segoe UI", 9);
        mousePosition.ForeColor = Color.FromArgb(170, 180, 190);

        instruction.Dock = DockStyle.Fill;
        instruction.TextAlign = ContentAlignment.MiddleLeft;
        instruction.Font = new Font("Segoe UI", 9);
        instruction.ForeColor = Color.FromArgb(150, 160, 170);

        header.Controls.Add(instruction);
        header.Controls.Add(mousePosition);
        header.Controls.Add(currentScreenLabel);
        header.Controls.Add(title);

        // -----------------------------------------------------
        // ОСНОВНЫЕ КНОПКИ
        // -----------------------------------------------------
        TableLayoutPanel actions = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 210,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(18, 12, 18, 8),
            BackColor = Color.FromArgb(30, 34, 40)
        };
        actions.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f));
        actions.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f));
        actions.RowStyles.Add(new RowStyle(SizeType.Percent, 33.34f));

        ConfigureMainButton(exportButton, "▶  ВЫГРУЗИТЬ 3 ОСНОВНЫХ ОТЧЁТА");
        exportButton.Click += async (_, _) => await ExportAllScreens();

        ConfigureMainButton(dischargeExportButton, "▶  ВЫГРУЗИТЬ РАЗРЯЖЕНИЕ");
        dischargeExportButton.Click += async (_, _) => await ExportDischargeAllScreens();

        ConfigureMainButton(buildReportsButton, "▶  СОБРАТЬ ИТОГОВЫЕ ОТЧЁТЫ");
        buildReportsButton.Click += async (_, _) => await BuildFinalReports();

        actions.Controls.Add(exportButton, 0, 0);
        actions.Controls.Add(dischargeExportButton, 0, 1);
        actions.Controls.Add(buildReportsButton, 0, 2);
        // -----------------------------------------------------
        // ЦЕНТРАЛЬНАЯ ОБЛАСТЬ
        // -----------------------------------------------------
        Panel center = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18, 4, 18, 8),
            BackColor = Color.FromArgb(30, 34, 40)
        };

        Label hint = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Выберите нужную операцию выше.\n\n"
                 + "Настройки координат и папок находятся в кнопке «⚙ Настройки».\n"
                 + "Журнал скрыт и открывается только при необходимости.",
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 10),
            ForeColor = Color.FromArgb(130, 140, 150)
        };
        center.Controls.Add(hint);

        // -----------------------------------------------------
        // ЖУРНАЛ (СКРЫТ ПО УМОЛЧАНИЮ)
        // -----------------------------------------------------
        journalPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 220,
            Padding = new Padding(18, 4, 18, 8),
            BackColor = Color.FromArgb(30, 34, 40),
            Visible = false
        };

        log.Multiline = true;
        log.ReadOnly = true;
        log.Dock = DockStyle.Fill;
        log.ScrollBars = ScrollBars.Vertical;
        log.Font = new Font("Consolas", 8.5f);
        log.BackColor = Color.FromArgb(22, 25, 30);
        log.ForeColor = Color.FromArgb(205, 210, 215);
        log.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
        journalPanel.Controls.Add(log);

        // -----------------------------------------------------
        // НИЖНЯЯ ПАНЕЛЬ
        // -----------------------------------------------------
        TableLayoutPanel bottom = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 52,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(12, 5, 12, 8),
            BackColor = Color.FromArgb(36, 41, 48)
        };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34f));

        ConfigureServiceButton(settingsButton, "⚙  Настройки");
        settingsButton.Click += (_, _) => ShowSettingsDialog();

        ConfigureServiceButton(journalButton, "▣  Журнал");
        journalButton.Click += (_, _) => ToggleJournal();

        ConfigureServiceButton(cancelButton, "■  Отмена (ESC)");
        cancelButton.Enabled = false;
        cancelButton.Click += (_, _) => CancelExport();

        bottom.Controls.Add(settingsButton, 0, 0);
        bottom.Controls.Add(journalButton, 1, 0);
        bottom.Controls.Add(cancelButton, 2, 0);

        Controls.Add(center);
        Controls.Add(actions);
        Controls.Add(header);
        Controls.Add(journalPanel);
        Controls.Add(bottom);

        // -----------------------------------------------------
        // ТАЙМЕР МЫШИ
        // -----------------------------------------------------
        System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer
        {
            Interval = 100
        };
        timer.Tick += (_, _) =>
        {
            if (GetCursorPos(out POINT p))
                mousePosition.Text = $"Мышь: X={p.X}   Y={p.Y}";
        };
        timer.Start();

        // -----------------------------------------------------
        // ЗАГРУЗКА
        // -----------------------------------------------------
        LoadCoordinates();
        ApplyFixedCoordinates();
        LoadTransferSettings();
        UpdateCurrentScreenLabel();

        Log("BFN Exporter запущен.");
        Log("Координаты существующей выгрузки сохранены.");
        Log("Доступна выгрузка 3 основных отчётов.");
        Log("Доступна отдельная выгрузка отчёта по разряжению.");
        UpdateInstructionForCurrentScreen();

        // -----------------------------------------------------
        // ESC
        // -----------------------------------------------------
        if (!RegisterHotKey(Handle, HOTKEY_ESC, MOD_NOREPEAT, VK_ESCAPE))
            Log("ВНИМАНИЕ: не удалось зарегистрировать глобальную клавишу ESC.");
        else
            Log("ESC зарегистрирован как глобальная отмена.");

        ApplyDarkTheme(this);
    }

    private static void ConfigureMainButton(Button button, string text)
    {
        button.Text = text;
        button.Dock = DockStyle.Fill;
        button.Margin = new Padding(4);
        button.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.BackColor = Color.FromArgb(48, 56, 66);
        button.ForeColor = Color.White;
        button.Cursor = Cursors.Hand;
    }

    private static void ConfigureServiceButton(Button button, string text)
    {
        button.Text = text;
        button.Dock = DockStyle.Fill;
        button.Margin = new Padding(3);
        button.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.BackColor = Color.FromArgb(43, 49, 57);
        button.ForeColor = Color.FromArgb(220, 225, 230);
        button.Cursor = Cursors.Hand;
    }

    private void ToggleJournal()
    {
        if (journalPanel == null)
            return;

        journalPanel.Visible = !journalPanel.Visible;
        journalButton.Text = journalPanel.Visible ? "▣  Скрыть журнал" : "▣  Журнал";
        if (journalPanel.Visible)
            log.SelectionStart = log.TextLength;
        log.ScrollToCaret();
    }

    private void ShowSettingsDialog()
    {
        if (exportRunning)
            return;

        Form dialog = new Form
        {
            Text = "Настройки BFN Exporter",
            Width = 560,
            Height = 500,
            StartPosition = FormStartPosition.CenterParent,
            BackColor = Color.FromArgb(30, 34, 40),
            ForeColor = Color.Gainsboro,
            Font = new Font("Segoe UI", 9.5f),
            MinimizeBox = false,
            MaximizeBox = false,
            FormBorderStyle = FormBorderStyle.FixedDialog
        };

        Label title = new Label
        {
            Dock = DockStyle.Top,
            Height = 48,
            Text = "НАСТРОЙКИ",
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            ForeColor = Color.White
        };

        ComboBox selector = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Dock = DockStyle.Top,
            Height = 34,
            BackColor = Color.FromArgb(43, 49, 57),
            ForeColor = Color.White,
            Margin = new Padding(12)
        };
        foreach (string screen in screenNames)
            selector.Items.Add(screen);
        selector.SelectedIndex = Math.Max(0, screenSelector.SelectedIndex);

        Label selected = new Label
        {
            Dock = DockStyle.Top,
            Height = 28,
            Text = "Экран для настройки",
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.FromArgb(160, 170, 180)
        };

        FlowLayoutPanel content = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(18, 12, 18, 12),
            AutoScroll = true,
            BackColor = Color.FromArgb(30, 34, 40)
        };

        Button mainCoords = CreateSettingsButton("⚙  Настроить основные координаты");
        Button dischargeCoords = CreateSettingsButton("⚙  Настроить координату разряжения");
        Button folders = CreateSettingsButton("📁  Настроить папки");
        Button transfer = CreateSettingsButton("⇄  Переслать отчёты");

        Label manualLabel = new Label
        {
            AutoSize = false,
            Width = 500,
            Height = 28,
            Text = "Ручная выгрузка выбранного экрана",
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            ForeColor = Color.FromArgb(170, 180, 190)
        };

        TableLayoutPanel manual = new TableLayoutPanel
        {
            Width = 500,
            Height = 92,
            ColumnCount = 4,
            RowCount = 1,
            BackColor = Color.FromArgb(30, 34, 40)
        };
        for (int i = 0; i < 4; i++)
            manual.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));

        Button[] manualButtons =
        {
            CreateSettingsButton("Н-33, Н-34"),
            CreateSettingsButton("Р-18"),
            CreateSettingsButton("Р-20"),
            CreateSettingsButton("Катковка")
        };
        for (int i = 0; i < manualButtons.Length; i++)
            manual.Controls.Add(manualButtons[i], i, 0);

        Button close = CreateSettingsButton("Готово");
        close.Width = 500;

        content.Controls.Add(mainCoords);
        content.Controls.Add(dischargeCoords);
        content.Controls.Add(folders);
        content.Controls.Add(transfer);
        content.Controls.Add(manualLabel);
        content.Controls.Add(manual);
        content.Controls.Add(close);

        dialog.Controls.Add(content);
        dialog.Controls.Add(selector);
        dialog.Controls.Add(selected);
        dialog.Controls.Add(title);

        selector.SelectedIndexChanged += (_, _) =>
        {
            if (selector.SelectedIndex >= 0)
                selected.Text = $"Экран: {selector.SelectedItem}";
        };
        selector.SelectedIndexChanged += (_, _) => { };
        selected.Text = $"Экран: {selector.SelectedItem}";

        void SyncScreen()
        {
            if (selector.SelectedIndex >= 0)
                screenSelector.SelectedIndex = selector.SelectedIndex;
        }

        mainCoords.Click += (_, _) =>
        {
            SyncScreen();
            dialog.Close();
            StartSetup();
        };

        dischargeCoords.Click += (_, _) =>
        {
            SyncScreen();
            dialog.Close();
            StartDischargeSetup();
        };

        folders.Click += (_, _) =>
        {
            dialog.Close();
            ConfigureTransferFolders();
        };

        transfer.Click += (_, _) =>
        {
            dialog.Close();
            StartTransferTest();
        };

        manualButtons[0].Click += async (_, _) => { dialog.Close(); await ExportSingleScreen("Н-33, Н-34"); };
        manualButtons[1].Click += async (_, _) => { dialog.Close(); await ExportSingleScreen("Р18"); };
        manualButtons[2].Click += async (_, _) => { dialog.Close(); await ExportSingleScreen("Р20"); };
        manualButtons[3].Click += async (_, _) => { dialog.Close(); await ExportSingleScreen("Катковка"); };
        close.Click += (_, _) => dialog.Close();

        ApplyDarkTheme(dialog);
        dialog.ShowDialog(this);
    }

    private static Button CreateSettingsButton(string text)
    {
        Button button = new Button
        {
            Text = text,
            Width = 500,
            Height = 48,
            Margin = new Padding(0, 4, 0, 4),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(43, 49, 57),
            ForeColor = Color.Gainsboro,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderSize = 1;
        return button;
    }

    private static void ApplyDarkTheme(Control root)
    {
        root.BackColor = Color.FromArgb(30, 34, 40);
        root.ForeColor = Color.Gainsboro;

        foreach (Control control in root.Controls)
        {
            if (control is Button button)
            {
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderColor = Color.FromArgb(78, 88, 100);
                button.BackColor = button.BackColor == SystemColors.Control
                    ? Color.FromArgb(43, 49, 57)
                    : button.BackColor;
                button.ForeColor = button.ForeColor == SystemColors.ControlText
                    ? Color.Gainsboro
                    : button.ForeColor;
            }
            else if (control is TextBox textBox)
            {
                textBox.BackColor = Color.FromArgb(22, 25, 30);
                textBox.ForeColor = Color.Gainsboro;
            }
            else if (control is ComboBox comboBox)
            {
                comboBox.BackColor = Color.FromArgb(43, 49, 57);
                comboBox.ForeColor = Color.White;
            }

            if (control.HasChildren)
                ApplyDarkTheme(control);
        }
    }

    // =========================================================
    // WNDPROC
    // =========================================================

    protected override void WndProc(
        ref Message m)
    {
        const int WM_HOTKEY =
            0x0312;

        if (m.Msg == WM_HOTKEY &&
            m.WParam.ToInt32() ==
            HOTKEY_ESC)
        {
            if (exportRunning)
            {
                CancelExport();
            }
            else if (setupIndex >= -2)
            {
                CancelSetup();
            }
        }

        base.WndProc(
            ref m);
    }

    // =========================================================
    // CLOSE
    // =========================================================

    protected override void OnFormClosed(
        FormClosedEventArgs e)
    {
        try
        {
            UnregisterHotKey(
                Handle,
                HOTKEY_ESC);
        }
        catch
        {
        }

        exportCancellation?.Cancel();

        exportCancellation?.Dispose();

        base.OnFormClosed(
            e);
    }

    // =========================================================
    // ЭКРАН
    // =========================================================

    private void UpdateCurrentScreenLabel()
    {
        currentScreenLabel.Text =
            $"Выбранный экран: {CurrentSetupScreen}";
    }

    private void UpdateInstructionForCurrentScreen()
    {
        if (HasAllCoordinates(
            CurrentSetupScreen))
        {
            instruction.Text =
                $"Координаты для «{CurrentSetupScreen}» загружены.";
        }
        else
        {
            instruction.Text =
                $"Для «{CurrentSetupScreen}» координаты ещё не настроены.";
        }
    }

    // =========================================================
    // КООРДИНАТЫ
    // =========================================================

    private Dictionary<string, Point>
        GetOrCreateScreenPoints(
            string screen)
    {
        if (!allPoints.TryGetValue(
            screen,
            out Dictionary<string, Point>? points))
        {
            points =
                new Dictionary<string, Point>();

            allPoints[screen] =
                points;
        }

        return points;
    }

    private bool HasAllCoordinates(
        string screen)
    {
        if (!allPoints.TryGetValue(
            screen,
            out Dictionary<string, Point>? points))
        {
            return false;
        }

        foreach (string pointName in pointNames)
        {
            if (!points.ContainsKey(
                pointName))
            {
                return false;
            }
        }

        return true;
    }

    // =========================================================
    // НАСТРОЙКА КООРДИНАТ
    // =========================================================

    private void StartSetup()
    {
        if (exportRunning)
        {
            return;
        }

        string screen =
            CurrentSetupScreen;

        Dictionary<string, Point> points =
            GetOrCreateScreenPoints(
                screen);

        Point? dischargePoint =
            points.TryGetValue(DischargeReportPoint, out Point savedDischarge)
                ? savedDischarge
                : null;

        points.Clear();

        if (dischargePoint.HasValue)
            points[DischargeReportPoint] = dischargePoint.Value;

        setupIndex = 0;

        setupButton.Enabled = false;
        dischargeSetupButton.Enabled = false;

        exportButton.Enabled = false;
        dischargeExportButton.Enabled = false;

        exportScreen1Button.Enabled = false;
        exportScreen2Button.Enabled = false;
        exportScreen3Button.Enabled = false;
        exportScreen4Button.Enabled = false;

        transferButton.Enabled = false;

        foldersButton.Enabled = false;

        cancelButton.Enabled = true;

        screenSelector.Enabled = false;

        instruction.Text =
            $"«{screen}»: наведи мышь на «{pointNames[0]}» и нажми F8.";

        Log("");

        Log(
            $"--- Начата настройка координат: {screen} ---");

        Log(
            "F8 — сохранить текущую позицию мыши.");

        Log(
            "ESC — отменить настройку.");

        Log(
            $"Нужно сохранить {pointNames.Length} координаты панели отчётов.");
    }

    private void CancelSetup()
    {
        if (setupIndex < -2)
        {
            return;
        }

        string screen =
            CurrentSetupScreen;

        bool dischargeOnly =
            setupIndex == -2;

        setupIndex = -1;

        if (!dischargeOnly)
        {
            Dictionary<string, Point> points = GetOrCreateScreenPoints(screen);
            Point? dischargePoint =
                points.TryGetValue(DischargeReportPoint, out Point savedDischarge)
                    ? savedDischarge
                    : null;
            points.Clear();
            if (dischargePoint.HasValue)
                points[DischargeReportPoint] = dischargePoint.Value;
        }

        setupButton.Enabled = true;
        dischargeSetupButton.Enabled = true;

        exportButton.Enabled = true;
        dischargeExportButton.Enabled = true;

        exportScreen1Button.Enabled = true;
        exportScreen2Button.Enabled = true;
        exportScreen3Button.Enabled = true;
        exportScreen4Button.Enabled = true;

        transferButton.Enabled = true;


        foldersButton.Enabled = true;

        cancelButton.Enabled = false;

        screenSelector.Enabled = true;

        instruction.Text =
            "Настройка отменена.";

        Log(
            dischargeOnly
                ? $"Настройка координаты разряжения для «{screen}» отменена."
                : $"Настройка координат «{screen}» отменена.");
    }

    // =========================================================
    // KEYDOWN
    // =========================================================

    private void MainForm_KeyDown(
        object? sender,
        KeyEventArgs e)
    {
        if (e.KeyCode == Keys.F8 &&
            setupIndex >= 0)
        {
            e.SuppressKeyPress = true;

            CapturePoint();
        }
        else if (e.KeyCode == Keys.F8 &&
                 setupIndex == -2)
        {
            e.SuppressKeyPress = true;
            CaptureDischargePoint();
        }

        if (e.KeyCode == Keys.Escape)
        {
            e.SuppressKeyPress = true;

            if (exportRunning)
            {
                CancelExport();
            }
            else if (setupIndex >= -2)
            {
                CancelSetup();
            }
        }
    }

    // =========================================================
    // CAPTURE POINT
    // =========================================================

    private void CapturePoint()
    {
        if (setupIndex < 0 ||
            setupIndex >= pointNames.Length)
        {
            return;
        }

        if (!GetCursorPos(
            out POINT p))
        {
            return;
        }

        string screen =
            CurrentSetupScreen;

        Dictionary<string, Point> points =
            GetOrCreateScreenPoints(
                screen);

        string name =
            pointNames[setupIndex];

        points[name] =
            new Point(
                p.X,
                p.Y);

        Log(
            $"{screen} — {name}: X={p.X}, Y={p.Y}");

        setupIndex++;

        if (setupIndex >= pointNames.Length)
        {
            SaveCoordinates();

            setupIndex = -1;

            setupButton.Enabled = true;
            dischargeSetupButton.Enabled = true;

            exportButton.Enabled = true;
            dischargeExportButton.Enabled = true;

            exportScreen1Button.Enabled = true;
            exportScreen2Button.Enabled = true;
            exportScreen3Button.Enabled = true;
            exportScreen4Button.Enabled = true;

            transferButton.Enabled = true;

    
            foldersButton.Enabled = true;

            cancelButton.Enabled = false;

            screenSelector.Enabled = true;

            instruction.Text =
                $"Готово! 3 координаты панелей для «{screen}» сохранены.";

            Log(
                $"Все 3 координаты панелей для «{screen}» сохранены.");

            return;
        }

        instruction.Text =
            $"«{screen}»: наведи мышь на «{pointNames[setupIndex]}» и нажми F8.";
    }

    // =========================================================
    // НАСТРОЙКА КООРДИНАТ РАЗРЯЖЕНИЯ
    // =========================================================

    private void StartDischargeSetup()
    {
        if (exportRunning || setupIndex >= 0)
        {
            return;
        }

        string screen = CurrentSetupScreen;
        setupIndex = -2;

        setupButton.Enabled = false;
        dischargeSetupButton.Enabled = false;
        exportButton.Enabled = false;
        dischargeExportButton.Enabled = false;
        exportScreen1Button.Enabled = false;
        exportScreen2Button.Enabled = false;
        exportScreen3Button.Enabled = false;
        exportScreen4Button.Enabled = false;
        transferButton.Enabled = false;
        foldersButton.Enabled = false;
        cancelButton.Enabled = true;
        screenSelector.Enabled = false;

        instruction.Text =
            $"«{screen}»: наведи мышь на «Отчет по разряжению» и нажми F8.";

        Log("");
        LogRound($"НАСТРОЙКА КООРДИНАТ РАЗРЯЖЕНИЯ — {screen}");
        Log("F8 — сохранить текущую позицию мыши.");
        Log("ESC — отменить настройку.");
    }

    private void CaptureDischargePoint()
    {
        if (setupIndex != -2)
        {
            return;
        }

        if (!GetCursorPos(out POINT p))
        {
            return;
        }

        string screen = CurrentSetupScreen;
        Dictionary<string, Point> points =
            GetOrCreateScreenPoints(screen);

        points[DischargeReportPoint] =
            new Point(p.X, p.Y);

        SaveCoordinates();

        setupIndex = -1;
        setupButton.Enabled = true;
        dischargeSetupButton.Enabled = true;
        exportButton.Enabled = true;
        dischargeExportButton.Enabled = true;
        exportScreen1Button.Enabled = true;
        exportScreen2Button.Enabled = true;
        exportScreen3Button.Enabled = true;
        exportScreen4Button.Enabled = true;
        transferButton.Enabled = true;
        foldersButton.Enabled = true;
        cancelButton.Enabled = false;
        screenSelector.Enabled = true;

        instruction.Text =
            $"Координата «Отчет по разряжению» для «{screen}» сохранена.";

        Log(
            $"{screen} — {DischargeReportPoint}: X={p.X}, Y={p.Y}");
        Log(
            $"Координата «{DischargeReportPoint}» сохранена.");
    }

    // =========================================================
    // SAVE COORDINATES
    // =========================================================

    private void SaveCoordinates()
    {
        try
        {
            string json =
                JsonSerializer.Serialize(
                    allPoints,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

            File.WriteAllText(
                SettingsFile,
                json);
        }
        catch (Exception ex)
        {
            Log(
                "Ошибка сохранения координат: " +
                ex.Message);
        }
    }

    // =========================================================
    // LOAD COORDINATES
    // =========================================================

    private void ApplyFixedCoordinates()
    {
        foreach (string screen in screenNames)
        {
            if (!fixedPoints.TryGetValue(screen, out Dictionary<string, Point>? actions))
                continue;

            Dictionary<string, Point> points =
                GetOrCreateScreenPoints(screen);

            foreach (var action in actions)
                points[action.Key] = action.Value;

            Log($"{screen}: фиксированные координаты Файл/Экспорт/Вернуться загружены.");
        }
    }

    private void LoadCoordinates()
    {
        try
        {
            if (!File.Exists(
                SettingsFile))
            {
                return;
            }

            string json =
                File.ReadAllText(
                    SettingsFile);

            Dictionary<
                string,
                Dictionary<string, Point>>? loaded =
                JsonSerializer.Deserialize<
                    Dictionary<
                        string,
                        Dictionary<string, Point>>>(
                            json);

            if (loaded == null)
            {
                return;
            }

            allPoints.Clear();

            foreach (var screen in loaded)
            {
                allPoints[screen.Key] =
                    screen.Value;
            }

            Log(
                "Координаты загружены.");

            foreach (string screen in screenNames)
            {
                if (HasAllCoordinates(
                    screen))
                {
                    Log(
                        $"{screen}: 3 координаты панелей загружены.");
                }
                else
                {
                    Log(
                        $"{screen}: координаты настроены не полностью.");
                }
            }
        }
        catch (Exception ex)
        {
            Log(
                "Ошибка загрузки координат: " +
                ex.Message);
        }
    }

    // =========================================================
    // CANCEL / WAIT
    // =========================================================

    private void CheckCancellation(
        CancellationToken token)
    {
        if (token.IsCancellationRequested)
        {
            throw new OperationCanceledException(
                token);
        }
    }

    private void Wait(
        int milliseconds,
        CancellationToken token)
    {
        CheckCancellation(
            token);

        if (token.WaitHandle.WaitOne(
            milliseconds))
        {
            throw new OperationCanceledException(
                token);
        }
    }

    // =========================================================
    // CLICK
    // =========================================================

    private void ClickPoint(
        string screen,
        string name,
        CancellationToken token)
    {
        CheckCancellation(
            token);

        Point p;

        if (fixedPoints.TryGetValue(screen, out Dictionary<string, Point>? fixedScreen) &&
            fixedScreen.TryGetValue(name, out Point fixedPoint))
        {
            p = fixedPoint;
        }
        else if (allPoints.TryGetValue(screen, out Dictionary<string, Point>? points) &&
                 points.TryGetValue(name, out Point configuredPoint))
        {
            p = configuredPoint;
        }
        else
        {
            throw new Exception(
                $"Нет координаты «{name}» для экрана «{screen}».");
        }

        Log(
            $"[{screen}] Клик: {name} ({p.X},{p.Y})");

        Cursor.Position =
            p;

        Wait(
            500,
            token);

        CheckCancellation(
            token);

        mouse_event(
            MOUSEEVENTF_LEFTDOWN,
            0,
            0,
            0,
            UIntPtr.Zero);

        Wait(
            100,
            token);

        mouse_event(
            MOUSEEVENTF_LEFTUP,
            0,
            0,
            0,
            UIntPtr.Zero);

        Wait(
            1000,
            token);
    }

    // =========================================================
    // ИМЯ ФАЙЛА
    // =========================================================

    // =========================================================
    // START REPORT
    // =========================================================

    private void StartReport(
        string screen,
        string reportPoint,
        CancellationToken token)
    {
        CheckCancellation(
            token);

        Log("");

        Log(
            $"[{screen}] Запускаем: {reportPoint}");

        ClickPoint(
            screen,
            reportPoint,
            token);

        Wait(
            500,
            token);

        Log(
            $"[{screen}] {reportPoint} запущен.");
    }

    // =========================================================
    // SAVE REPORT
    // =========================================================

    private void SaveReport(
        string screen,
        string fileName,
        CancellationToken token)
    {
        CheckCancellation(token);

        Log("");
        fileName = NormalizeXlsFileName(fileName);

        Log($"[{screen}] Сохраняем: {fileName}");

        // Стандартные кнопки «Файл» и «Экспорт» находятся
        // по фиксированным координатам конкретного экрана.
        ClickPoint(screen, "Файл", token);
        Wait(300, token);

        ClickPoint(screen, "Экспорт", token);
        Wait(1500, token);

        // После «Экспорт» поле имени уже активно.
        // Старое имя не удаляем: пользователь проверил, что
        // новое имя можно сразу вводить в это поле.
        Log("Вводим новое имя файла без отдельного клика по полю.");
        SendKeys.SendWait(fileName);
        Wait(500, token);

        // Сохранение: Enter.
        Log("Подтверждаем имя файла клавишей Enter.");
        SendKeys.SendWait("{ENTER}");
        Wait(1500, token);

        // В окне BFNM с предложением открыть файл:
        // один Tab переводит фокус на «Нет», второй Enter подтверждает.
        Log("Закрываем предложение открыть сохранённый файл: Tab + Enter.");
        SendKeys.SendWait("{TAB}");
        Wait(200, token);
        SendKeys.SendWait("{ENTER}");
        Wait(1000, token);

        Log($"[{screen}] Сохранено: {fileName}");
    }

    // =========================================================
    // RETURN
    // =========================================================

    private void ReturnToStart(
        string screen,
        CancellationToken token)
    {
        CheckCancellation(
            token);

        Log(
            $"[{screen}] Возврат в исходное состояние.");

        ClickPoint(
            screen,
            "Вернуться",
            token);

        Wait(
            1000,
            token);

        Log(
            $"[{screen}] Возврат выполнен.");
    }

    private static string NormalizeXlsFileName(string fileName)
    {
        // BFNM сам добавляет расширение .xls.
        // В поле имени передаём только имя файла без расширения.
        return Path.GetFileNameWithoutExtension(fileName).TrimEnd('.', ' ');
    }

    // =========================================================
    // FILE NAMES
    // =========================================================

    private string ProductionFile(
        string screen,
        DateTime yesterday)
    {
        return
            $"{yesterday:yyyy_MM_dd} " +
            $"{screen} " +
            "Производственный отчет";
    }

    private string DamateFile(
        string screen,
        DateTime yesterday)
    {
        return
            $"{yesterday:yyyy_MM_dd} " +
            $"{screen} " +
            "Дамате клик";
    }

    private string BunkerFile(
        string screen,
        DateTime today)
    {
        return
            $"{today:yyyy_MM_dd} " +
            $"{screen} " +
            "Остаток корма на 00-00";
    }

    private string DischargeFile(
        string screen,
        DateTime today)
    {
        return
            $"{today:yyyy_MM_dd} " +
            $"{screen} " +
            "Разряжение на 04-00";
    }

    // =========================================================
    // VALIDATE
    // =========================================================

    private void ValidateAllCoordinates()
    {
        foreach (string screen in screenNames)
        {
            if (!HasAllCoordinates(
                screen))
            {
                throw new Exception(
                    $"Для экрана «{screen}» не настроены 3 координаты панелей.");
            }
        }
    }

    private void ValidateScreenCoordinates(
        string screen)
    {
        if (!HasAllCoordinates(
            screen))
        {
            throw new Exception(
                $"Для экрана «{screen}» не настроены 3 координаты панелей.");
        }
    }

    private void ValidateDischargeCoordinates()
    {
        foreach (string screen in screenNames)
        {
            if (!allPoints.TryGetValue(screen, out Dictionary<string, Point>? points) ||
                !points.ContainsKey(DischargeReportPoint))
            {
                throw new Exception(
                    $"Для экрана «{screen}» не настроена координата «Отчет по разряжению».");
            }
        }
    }

    // =========================================================
    // LOG ROUND
    // =========================================================

    private void LogRound(
        string title)
    {
        Log("");

        Log(
            "========================================");

        Log(
            title);

        Log(
            "========================================");
    }

    // =========================================================
    // ОБЩАЯ ВЫГРУЗКА
    // =========================================================

    private async Task ExportAllScreens()
    {
        if (exportRunning)
        {
            return;
        }

        try
        {
            ValidateAllCoordinates();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message +
                "\n\nСначала настрой необходимые координаты.",
                "BFN Exporter",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            return;
        }

        exportRunning = true;

        exportCancellation =
            new CancellationTokenSource();

        CancellationToken token =
            exportCancellation.Token;

        SetRunningState(
            true);

        DateTime today =
            DateTime.Today;

        DateTime yesterday =
            today.AddDays(-1);

        try
        {
            LogRound(
                "НАЧАЛО КОНВЕЙЕРНОЙ ВЫГРУЗКИ — 4 ЭКРАНА");

            // -------------------------------------------------
            // КРУГ 1
            // -------------------------------------------------

            LogRound(
                "КРУГ 1 — ЗАПУСК ПРОИЗВОДСТВЕННОГО ОТЧЁТА");

            foreach (string screen in screenNames)
            {
                CheckCancellation(
                    token);

                StartReport(
                    screen,
                    "Отчет Производство",
                    token);
            }

            // -------------------------------------------------
            // КРУГ 2
            // -------------------------------------------------

            LogRound(
                "КРУГ 2 — СОХРАНЕНИЕ ПРОИЗВОДСТВЕННОГО ОТЧЁТА");

            foreach (string screen in screenNames)
            {
                CheckCancellation(
                    token);

                SaveReport(
                    screen,
                    ProductionFile(
                        screen,
                        yesterday),
                    token);
            }

            // -------------------------------------------------
            // КРУГ 3
            // -------------------------------------------------

            LogRound(
                "КРУГ 3 — ЗАПУСК DAMATE QLIK");

            foreach (string screen in screenNames)
            {
                CheckCancellation(
                    token);

                StartReport(
                    screen,
                    "Damate Qlik",
                    token);
            }

            // -------------------------------------------------
            // КРУГ 4
            // -------------------------------------------------

            LogRound(
                "КРУГ 4 — СОХРАНЕНИЕ DAMATE QLIK");

            foreach (string screen in screenNames)
            {
                CheckCancellation(
                    token);

                SaveReport(
                    screen,
                    DamateFile(
                        screen,
                        yesterday),
                    token);
            }

            // -------------------------------------------------
            // КРУГ 5
            // -------------------------------------------------

            LogRound(
                "КРУГ 5 — ЗАПУСК ОТЧЁТА БУНКЕР");

            foreach (string screen in screenNames)
            {
                CheckCancellation(
                    token);

                StartReport(
                    screen,
                    "Отчет Бункер",
                    token);
            }

            // -------------------------------------------------
            // КРУГ 6
            // -------------------------------------------------

            LogRound(
                "КРУГ 6 — СОХРАНЕНИЕ ОТЧЁТА БУНКЕР");

            foreach (string screen in screenNames)
            {
                CheckCancellation(
                    token);

                SaveReport(
                    screen,
                    BunkerFile(
                        screen,
                        today),
                    token);
            }

            // -------------------------------------------------
            // КРУГ 7
            // -------------------------------------------------

            LogRound(
                "КРУГ 7 — ВОЗВРАТ В ИСХОДНОЕ СОСТОЯНИЕ");

            foreach (string screen in screenNames)
            {
                CheckCancellation(
                    token);

                ReturnToStart(
                    screen,
                    token);
            }

            LogRound(
                "ВСЕ 4 ЭКРАНА УСПЕШНО ВЫГРУЖЕНЫ");

            MessageBox.Show(
                "Все 4 экрана успешно выгружены.\n\n" +
                "Все экраны возвращены в исходное состояние.",
                "BFN Exporter",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (OperationCanceledException)
        {
            Log(
                "ОПЕРАЦИЯ ОТМЕНЕНА ПОЛЬЗОВАТЕЛЕМ.");

            MessageBox.Show(
                "Выгрузка отменена.",
                "BFN Exporter",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            Log(
                "ОШИБКА ПРИ ВЫГРУЗКЕ:");

            Log(
                ex.ToString());

            MessageBox.Show(
                ex.Message,
                "Ошибка BFN Exporter",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            exportCancellation?.Dispose();

            exportCancellation = null;

            exportRunning = false;

            SetRunningState(
                false);
        }

        await Task.CompletedTask;
    }

    // =========================================================
    // ОТДЕЛЬНАЯ ВЫГРУЗКА РАЗРЯЖЕНИЯ — 4 ЭКРАНА
    // =========================================================

    private async Task ExportDischargeAllScreens()
    {
        if (exportRunning)
        {
            return;
        }

        try
        {
            ValidateDischargeCoordinates();
            ValidateAllCoordinates();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message +
                "\n\nСначала настрой необходимые координаты.",
                "BFN Exporter",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        exportRunning = true;
        exportCancellation = new CancellationTokenSource();
        CancellationToken token = exportCancellation.Token;
        SetRunningState(true);

        DateTime today = DateTime.Today;
        bool returnedScreens = false;

        try
        {
            LogRound("ОТДЕЛЬНАЯ ВЫГРУЗКА — ОТЧЁТ ПО РАЗРЯЖЕНИЮ");

            foreach (string screen in screenNames)
            {
                CheckCancellation(token);

                LogRound($"[{screen}] ОТЧЁТ ПО РАЗРЯЖЕНИЮ");

                StartReport(
                    screen,
                    DischargeReportPoint,
                    token);

                SaveReport(
                    screen,
                    DischargeFile(screen, today),
                    token);

                ReturnToStart(
                    screen,
                    token);

                Log(
                    $"[{screen}] Разряжение сохранено и экран возвращён.");
            }

            returnedScreens = true;

            LogRound("ОТЧЁТ ПО РАЗРЯЖЕНИЮ — ВСЕ 4 ЭКРАНА ГОТОВЫ");

            MessageBox.Show(
                "Отчёт по разряжению для всех 4 экранов успешно выгружен.\n\n" +
                "Все экраны возвращены в исходное состояние.",
                "BFN Exporter",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (OperationCanceledException)
        {
            Log("Выгрузка отчёта по разряжению отменена пользователем.");
            MessageBox.Show(
                "Выгрузка отчёта по разряжению отменена.",
                "BFN Exporter",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            Log("ОШИБКА ПРИ ВЫГРУЗКЕ ОТЧЁТА ПО РАЗРЯЖЕНИЮ:");
            Log(ex.ToString());
            MessageBox.Show(
                ex.Message,
                "Ошибка BFN Exporter",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            exportCancellation?.Dispose();
            exportCancellation = null;
            exportRunning = false;
            SetRunningState(false);
        }

        await Task.CompletedTask;
    }

    // =========================================================
    // ОТДЕЛЬНАЯ ВЫГРУЗКА ОДНОГО ЭКРАНА
    // =========================================================

    private async Task ExportSingleScreen(
        string screen)
    {
        if (exportRunning)
        {
            return;
        }

        try
        {
            ValidateScreenCoordinates(
                screen);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "BFN Exporter",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            return;
        }

        exportRunning = true;

        exportCancellation =
            new CancellationTokenSource();

        CancellationToken token =
            exportCancellation.Token;

        SetRunningState(
            true);

        DateTime today =
            DateTime.Today;

        DateTime yesterday =
            today.AddDays(-1);

        bool returnedToStart =
            false;

        try
        {
            LogRound(
                $"ОТДЕЛЬНАЯ ВЫГРУЗКА — {screen}");

            // -------------------------------------------------
            // ПРОИЗВОДСТВЕННЫЙ
            // -------------------------------------------------

            LogRound(
                $"[{screen}] ПРОИЗВОДСТВЕННЫЙ ОТЧЁТ");

            StartReport(
                screen,
                "Отчет Производство",
                token);

            SaveReport(
                screen,
                ProductionFile(
                    screen,
                    yesterday),
                token);

            // -------------------------------------------------
            // DAMATE
            // -------------------------------------------------

            LogRound(
                $"[{screen}] DAMATE QLIK");

            StartReport(
                screen,
                "Damate Qlik",
                token);

            SaveReport(
                screen,
                DamateFile(
                    screen,
                    yesterday),
                token);

            // -------------------------------------------------
            // БУНКЕР
            // -------------------------------------------------

            LogRound(
                $"[{screen}] ОТЧЁТ БУНКЕР");

            StartReport(
                screen,
                "Отчет Бункер",
                token);

            SaveReport(
                screen,
                BunkerFile(
                    screen,
                    today),
                token);

            // -------------------------------------------------
            // ВОЗВРАТ
            // -------------------------------------------------

            LogRound(
                $"[{screen}] ВОЗВРАТ");

            ReturnToStart(
                screen,
                token);

            returnedToStart = true;

            LogRound(
                $"ЭКРАН «{screen}» УСПЕШНО ВЫГРУЖЕН");

            MessageBox.Show(
                $"Экран «{screen}» успешно выгружен.\n\n" +
                "Все 3 отчёта сохранены.",
                "BFN Exporter",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (OperationCanceledException)
        {
            Log(
                $"Выгрузка «{screen}» отменена.");

            MessageBox.Show(
                $"Выгрузка «{screen}» отменена.",
                "BFN Exporter",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            Log(
                $"ОШИБКА ПРИ ВЫГРУЗКЕ «{screen}»");

            Log(
                ex.ToString());

            MessageBox.Show(
                ex.Message,
                "Ошибка BFN Exporter",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            // Если произошла ошибка после открытия отчёта,
            // стараемся вернуть экран.
            if (!returnedToStart)
            {
                try
                {
                    if (!token.IsCancellationRequested)
                    {
                        ReturnToStart(
                            screen,
                            token);
                    }
                }
                catch
                {
                }
            }

            exportCancellation?.Dispose();

            exportCancellation = null;

            exportRunning = false;

            SetRunningState(
                false);
        }

        await Task.CompletedTask;
    }

    // =========================================================
    // ОТМЕНА
    // =========================================================

    private void CancelExport()
    {
        if (!exportRunning)
        {
            return;
        }

        Log("");

        Log(
            "Получена команда ОТМЕНА.");

        exportCancellation?.Cancel();
    }

    // =========================================================
    // UI STATE
    // =========================================================

    private void SetRunningState(
        bool running)
    {
        if (InvokeRequired)
        {
            BeginInvoke(
                new Action<bool>(
                    SetRunningState),
                running);

            return;
        }

        exportButton.Enabled =
            !running;

        dischargeExportButton.Enabled =
            !running;

        buildReportsButton.Enabled =
            !running;

        exportScreen1Button.Enabled =
            !running;

        exportScreen2Button.Enabled =
            !running;

        exportScreen3Button.Enabled =
            !running;

        exportScreen4Button.Enabled =
            !running;

        setupButton.Enabled =
            !running;

        dischargeSetupButton.Enabled =
            !running;

        transferButton.Enabled =
            !running;

        foldersButton.Enabled =
            !running;

        cancelButton.Enabled =
            running;

        screenSelector.Enabled =
            !running;

        if (running)
        {
            instruction.Text =
                "Выполняется операция. Для отмены нажми ESC.";
        }
        else
        {
            UpdateInstructionForCurrentScreen();
        }
    }

    // =========================================================
    // LM VIEWER
    // =========================================================

    private static bool IsLmViewerWindow(
        WindowInfo window)
    {
        string text =
            (window.Title + " " +
             window.ClassName)
            .ToLowerInvariant();

        return
            text.Contains("litemanager") ||
            text.Contains("lite manager") ||
            text.Contains("lm viewer") ||
            text.Contains("lmviewer");
    }

    private List<WindowInfo> GetVisibleWindows()
    {
        List<WindowInfo> result =
            new();

        EnumWindows(
            (hWnd, _) =>
            {
                if (!IsWindowVisible(
                    hWnd))
                {
                    return true;
                }

                StringBuilder title =
                    new StringBuilder(512);

                StringBuilder className =
                    new StringBuilder(512);

                GetWindowText(
                    hWnd,
                    title,
                    title.Capacity);

                GetClassName(
                    hWnd,
                    className,
                    className.Capacity);

                string titleText =
                    title.ToString().Trim();

                string classText =
                    className.ToString().Trim();

                if (string.IsNullOrWhiteSpace(
                    titleText))
                {
                    return true;
                }

                GetWindowThreadProcessId(
                    hWnd,
                    out uint processId);

                result.Add(
                    new WindowInfo
                    {
                        Handle = hWnd,
                        Title = titleText,
                        ClassName = classText,
                        ProcessId = processId
                    });

                return true;
            },
            IntPtr.Zero);

        return result;
    }

    private void ConfigureTransferFolders()
    {
        if (exportRunning)
        {
            return;
        }

        using Form form =
            new Form();

        form.Text =
            "Папки для передачи отчётов";

        form.Width =
            720;

        form.Height =
            260;

        form.StartPosition =
            FormStartPosition.CenterParent;

        Label remoteLabel =
            new Label
            {
                Text =
                    "Папка с отчётами на удалённых ПК:",
                Left = 20,
                Top = 20,
                Width = 650
            };

        TextBox remoteBox =
            new TextBox
            {
                Left = 20,
                Top = 45,
                Width = 540,
                Text =
                    remoteReportsFolder
            };

        Button remoteBrowse =
            new Button
            {
                Text = "...",
                Left = 570,
                Top = 43,
                Width = 70
            };

        remoteBrowse.Click +=
            (_, _) =>
            {
                using FolderBrowserDialog dialog =
                    new FolderBrowserDialog();

                dialog.Description =
                    "Выбери папку с отчётами на удалённом ПК.";

                if (!string.IsNullOrWhiteSpace(
                    remoteBox.Text) &&
                    Directory.Exists(
                        remoteBox.Text))
                {
                    dialog.SelectedPath =
                        remoteBox.Text;
                }

                if (dialog.ShowDialog(
                    form) ==
                    DialogResult.OK)
                {
                    remoteBox.Text =
                        dialog.SelectedPath;
                }
            };

        Label localLabel =
            new Label
            {
                Text =
                    "Папка на твоём ПК, куда складывать отчёты:",
                Left = 20,
                Top = 85,
                Width = 650
            };

        TextBox localBox =
            new TextBox
            {
                Left = 20,
                Top = 110,
                Width = 540,
                Text =
                    localReportsFolder
            };

        Button localBrowse =
            new Button
            {
                Text = "...",
                Left = 570,
                Top = 108,
                Width = 70
            };

        localBrowse.Click +=
            (_, _) =>
            {
                using FolderBrowserDialog dialog =
                    new FolderBrowserDialog();

                dialog.Description =
                    "Выбери папку на своём ПК.";

                if (!string.IsNullOrWhiteSpace(
                    localBox.Text) &&
                    Directory.Exists(
                        localBox.Text))
                {
                    dialog.SelectedPath =
                        localBox.Text;
                }

                if (dialog.ShowDialog(
                    form) ==
                    DialogResult.OK)
                {
                    localBox.Text =
                        dialog.SelectedPath;
                }
            };

        Button save =
            new Button
            {
                Text =
                    "Сохранить",
                Left = 530,
                Top = 165,
                Width = 110,
                Height = 35
            };

        Button cancel =
            new Button
            {
                Text =
                    "Отмена",
                Left = 410,
                Top = 165,
                Width = 110,
                Height = 35
            };

        save.Click +=
            (_, _) =>
            {
                remoteReportsFolder =
                    remoteBox.Text.Trim();

                localReportsFolder =
                    localBox.Text.Trim();

                SaveTransferSettings();

                Log("");

                Log(
                    "Настройки папок сохранены.");

                Log(
                    $"Удалённая папка: {remoteReportsFolder}");

                Log(
                    $"Локальная папка: {localReportsFolder}");

                form.DialogResult =
                    DialogResult.OK;

                form.Close();
            };

        cancel.Click +=
            (_, _) =>
            {
                form.Close();
            };

        form.Controls.Add(
            remoteLabel);

        form.Controls.Add(
            remoteBox);

        form.Controls.Add(
            remoteBrowse);

        form.Controls.Add(
            localLabel);

        form.Controls.Add(
            localBox);

        form.Controls.Add(
            localBrowse);

        form.Controls.Add(
            save);

        form.Controls.Add(
            cancel);

        form.ShowDialog(
            this);
    }

    // =========================================================
    // SAVE TRANSFER SETTINGS
    // =========================================================

    private void SaveTransferSettings()
    {
        try
        {
            TransferSettings settings =
                new TransferSettings
                {
                    RemoteReportsFolder =
                        remoteReportsFolder,

                    LocalReportsFolder =
                        localReportsFolder
                };

            string json =
                JsonSerializer.Serialize(
                    settings,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

            File.WriteAllText(
                TransferSettingsFile,
                json);
        }
        catch (Exception ex)
        {
            Log(
                "Ошибка сохранения настроек папок: " +
                ex.Message);
        }
    }

    // =========================================================
    // LOAD TRANSFER SETTINGS
    // =========================================================

    private void LoadTransferSettings()
    {
        try
        {
            if (!File.Exists(
                TransferSettingsFile))
            {
                return;
            }

            string json =
                File.ReadAllText(
                    TransferSettingsFile);

            TransferSettings? settings =
                JsonSerializer.Deserialize<
                    TransferSettings>(
                        json);

            if (settings == null)
            {
                return;
            }

            remoteReportsFolder =
                settings.RemoteReportsFolder ??
                "";

            localReportsFolder =
                settings.LocalReportsFolder ??
                "";

            Log(
                "Настройки папок загружены.");
        }
        catch (Exception ex)
        {
            Log(
                "Ошибка загрузки настроек папок: " +
                ex.Message);
        }
    }

    // =========================================================
    // TRANSFER SETTINGS CLASS
    // =========================================================

    private sealed class TransferSettings
    {
        public string RemoteReportsFolder { get; set; } = "";

        public string LocalReportsFolder { get; set; } = "";
    }

    // =========================================================
    // СБОРКА ИТОГОВЫХ EXCEL-ОТЧЁТОВ (.XLS)
    // =========================================================

    private const string BuilderSourceFolder =
        @"T:\PMI\PMI\Ситуационный центр ПМИ\Разное\Димаш";

    private const string ProductionRootFolder =
        @"T:\PMI\PMI\Ситуационный центр ПМИ\=ОТЧЕТЫ=\Производственный отчет";

    private const string DamateRootFolder =
        @"T:\PMI\PMI\Ситуационный центр ПМИ\=ОТЧЕТЫ=\Damate Qlik";

    private const string BunkerRootFolder =
        @"T:\PMI\PMI\Ситуационный центр ПМИ\=ОТЧЕТЫ=\Остаток корма на 00ч.00м";

    private sealed class ReportTransfer
    {
        public string Screen { get; init; } = "";
        public string SourceStem { get; init; } = "";
        public string SourceRange { get; init; } = "";
        public string DestinationRange { get; init; } = "";
    }

    private async Task BuildFinalReports()
    {
        if (exportRunning)
        {
            Log("Сборка отчётов не запущена: уже выполняется другая операция.");
            return;
        }

        DateTime today = DateTime.Today;
        DateTime yesterday = today.AddDays(-1);
        string year = today.ToString("yyyy");
        string monthFolder = today.ToString("yyyy_MM");

        string productionFolder = Path.Combine(ProductionRootFolder, year, monthFolder);
        string damateFolder = Path.Combine(DamateRootFolder, year, monthFolder);
        string bunkerFolder = Path.Combine(BunkerRootFolder, year, monthFolder);

        string productionDestinationStem =
            $"{yesterday:yyyy_MM_dd}_Катковка Каргалей и Ольгино. Производственный_";
        string damateDestinationStem =
            $"{yesterday:yyyy_MM_dd}_Катковка Каргалей и Ольгино Damate Qlik";
        string bunkerDestinationStem =
            $"{today:yyyy_MM_dd}_Катковка Каргалей Ольгино. Остаток корма на 00-00";

        exportRunning = true;
        exportCancellation = new CancellationTokenSource();
        CancellationToken token = exportCancellation.Token;
        SetRunningState(true);

        try
        {
            LogRound("СБОРКА ИТОГОВЫХ ОТЧЁТОВ");
            Log($"Источник выгруженных файлов: {BuilderSourceFolder}");
            Log($"Производственный отчёт: {productionFolder}");
            Log($"Дамате клик: {damateFolder}");
            Log($"Остаток корма: {bunkerFolder}");

            RequireDirectory(BuilderSourceFolder);
            RequireDirectory(productionFolder);
            RequireDirectory(damateFolder);
            RequireDirectory(bunkerFolder);

            string productionDestination = FindXlsFileByStem(
                productionFolder,
                productionDestinationStem);

            string damateDestination = FindXlsFileByStem(
                damateFolder,
                damateDestinationStem);

            string bunkerDestination = FindXlsFileByStem(
                bunkerFolder,
                bunkerDestinationStem);

            Log($"Производственный файл назначения: {Path.GetFileName(productionDestination)}");
            Log($"Damate Qlik файл назначения: {Path.GetFileName(damateDestination)}");
            Log($"Остаток корма файл назначения: {Path.GetFileName(bunkerDestination)}");

            await Task.Run(() =>
            {
                BuildProductionReport(
                    productionDestination,
                    yesterday,
                    token);

                CheckCancellation(token);

                BuildDamateReport(
                    damateDestination,
                    yesterday,
                    token);

                CheckCancellation(token);

                BuildBunkerReport(
                    bunkerDestination,
                    today,
                    token);
            }, token);

            LogRound("ИТОГОВЫЕ ОТЧЁТЫ УСПЕШНО СОБРАНЫ");

            MessageBox.Show(
                "Три итоговых отчёта успешно собраны.",
                "BFN Exporter",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (OperationCanceledException)
        {
            Log("Сборка итоговых отчётов отменена.");
        }
        catch (Exception ex)
        {
            Log("ОШИБКА ПРИ СБОРКЕ ИТОГОВЫХ ОТЧЁТОВ:");
            Log(ex.ToString());

            MessageBox.Show(
                ex.Message,
                "Ошибка сборки отчётов",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            exportCancellation?.Dispose();
            exportCancellation = null;
            exportRunning = false;
            SetRunningState(false);
        }
    }

    private static void RequireDirectory(string folder)
    {
        if (!Directory.Exists(folder))
        {
            throw new DirectoryNotFoundException(
                $"Не найдена папка:\n{folder}");
        }
    }

    private void BuildProductionReport(
        string destinationFile,
        DateTime reportDate,
        CancellationToken token)
    {
        LogRound("ПРОИЗВОДСТВЕННЫЙ ОТЧЁТ");

        ReportTransfer[] transfers =
        {
            new ReportTransfer
            {
                Screen = "Катковка",
                SourceStem = $"{reportDate:yyyy_MM_dd} Катковка Производственный отчет",
                SourceRange = "D2:I37",
                DestinationRange = "D6:I41"
            },
            new ReportTransfer
            {
                Screen = "Р18",
                SourceStem = $"{reportDate:yyyy_MM_dd} Р18 Производственный отчет",
                SourceRange = "D2:I33",
                DestinationRange = "D42:I73"
            },
            new ReportTransfer
            {
                Screen = "Н-33, Н-34",
                SourceStem = $"{reportDate:yyyy_MM_dd} Н-33, Н-34 Производственный отчет",
                SourceRange = "D2:I33",
                DestinationRange = "D74:I105"
            },
            new ReportTransfer
            {
                Screen = "Р20",
                SourceStem = $"{reportDate:yyyy_MM_dd} Р20 Производственный отчет",
                SourceRange = "D2:I33",
                DestinationRange = "D106:I137"
            }
        };

        HSSFWorkbook destinationBook = LoadXls(destinationFile);

        try
        {
            ISheet destinationSheet = GetFirstSheet(destinationBook);

            foreach (ReportTransfer transfer in transfers)
            {
                CheckCancellation(token);

                string sourceFile = FindXlsFileByStem(
                    BuilderSourceFolder,
                    transfer.SourceStem);

                Log($"[{transfer.Screen}] Производственный: {Path.GetFileName(sourceFile)}");
                Log($"[{transfer.Screen}] {transfer.SourceRange} → {transfer.DestinationRange}");

                HSSFWorkbook sourceBook = LoadXls(sourceFile);

                try
                {
                    ISheet sourceSheet = GetFirstSheet(sourceBook);
                    CopyValues(
                        sourceBook,
                        sourceSheet,
                        transfer.SourceRange,
                        destinationSheet,
                        transfer.DestinationRange);
                }
                finally
                {
                    sourceBook.Close();
                }
            }

            CheckCancellation(token);
            SaveXls(destinationBook, destinationFile);
            Log($"Сохранён: {Path.GetFileName(destinationFile)}");
        }
        finally
        {
            destinationBook.Close();
        }
    }

    private void BuildDamateReport(
        string destinationFile,
        DateTime reportDate,
        CancellationToken token)
    {
        LogRound("ДАМАТЕ КЛИК");

        ReportTransfer[] transfers =
        {
            new ReportTransfer
            {
                Screen = "Катковка",
                SourceStem = $"{reportDate:yyyy_MM_dd} Катковка Дамате клик",
                SourceRange = "D2:N37",
                DestinationRange = "D6:N41"
            },
            new ReportTransfer
            {
                Screen = "Р18",
                SourceStem = $"{reportDate:yyyy_MM_dd} Р18 Дамате клик",
                SourceRange = "D2:N33",
                DestinationRange = "D42:N73"
            },
            new ReportTransfer
            {
                Screen = "Н-33, Н-34",
                SourceStem = $"{reportDate:yyyy_MM_dd} Н-33, Н-34 Дамате клик",
                SourceRange = "D2:N33",
                DestinationRange = "D74:N105"
            },
            new ReportTransfer
            {
                Screen = "Р20",
                SourceStem = $"{reportDate:yyyy_MM_dd} Р20 Дамате клик",
                SourceRange = "D2:N33",
                DestinationRange = "D106:N137"
            }
        };

        HSSFWorkbook destinationBook = LoadXls(destinationFile);

        try
        {
            ISheet destinationSheet = GetFirstSheet(destinationBook);

            foreach (ReportTransfer transfer in transfers)
            {
                CheckCancellation(token);

                string sourceFile = FindXlsFileByStem(
                    BuilderSourceFolder,
                    transfer.SourceStem);

                Log($"[{transfer.Screen}] Дамате: {Path.GetFileName(sourceFile)}");
                Log($"[{transfer.Screen}] {transfer.SourceRange} → {transfer.DestinationRange}");

                HSSFWorkbook sourceBook = LoadXls(sourceFile);

                try
                {
                    ISheet sourceSheet = GetFirstSheet(sourceBook);
                    CopyValues(
                        sourceBook,
                        sourceSheet,
                        transfer.SourceRange,
                        destinationSheet,
                        transfer.DestinationRange);
                }
                finally
                {
                    sourceBook.Close();
                }
            }

            CheckCancellation(token);
            SaveXls(destinationBook, destinationFile);
            Log($"Сохранён: {Path.GetFileName(destinationFile)}");
        }
        finally
        {
            destinationBook.Close();
        }
    }

    private void BuildBunkerReport(
        string destinationFile,
        DateTime reportDate,
        CancellationToken token)
    {
        LogRound("ОСТАТОК КОРМА");

        ReportTransfer[] transfers =
        {
            new ReportTransfer
            {
                Screen = "Катковка",
                SourceStem = $"{reportDate:yyyy_MM_dd} Катковка Остаток корма на 00-00",
                SourceRange = "D2:F37",
                DestinationRange = "D6:G41"
            },
            new ReportTransfer
            {
                Screen = "Р18",
                SourceStem = $"{reportDate:yyyy_MM_dd} Р18 Остаток корма на 00-00",
                SourceRange = "D2:F33",
                DestinationRange = "D42:G73"
            },
            new ReportTransfer
            {
                Screen = "Н-33, Н-34",
                SourceStem = $"{reportDate:yyyy_MM_dd} Н-33, Н-34 Остаток корма на 00-00",
                SourceRange = "D2:F33",
                DestinationRange = "D74:G105"
            },
            new ReportTransfer
            {
                Screen = "Р20",
                SourceStem = $"{reportDate:yyyy_MM_dd} Р20 Остаток корма на 00-00",
                SourceRange = "D2:F33",
                DestinationRange = "D106:G137"
            }
        };

        HSSFWorkbook destinationBook = LoadXls(destinationFile);

        try
        {
            ISheet destinationSheet = GetFirstSheet(destinationBook);

            foreach (ReportTransfer transfer in transfers)
            {
                CheckCancellation(token);

                string sourceFile = FindXlsFileByStem(
                    BuilderSourceFolder,
                    transfer.SourceStem);

                Log($"[{transfer.Screen}] Остаток корма: {Path.GetFileName(sourceFile)}");
                Log($"[{transfer.Screen}] {transfer.SourceRange} → {transfer.DestinationRange}");

                HSSFWorkbook sourceBook = LoadXls(sourceFile);

                try
                {
                    ISheet sourceSheet = GetFirstSheet(sourceBook);
                    CopyBunkerValues(
                        sourceBook,
                        sourceSheet,
                        destinationSheet,
                        transfer.SourceRange,
                        transfer.DestinationRange);
                }
                finally
                {
                    sourceBook.Close();
                }
            }

            CheckCancellation(token);
            SaveXls(destinationBook, destinationFile);
            Log($"Сохранён: {Path.GetFileName(destinationFile)}");
        }
        finally
        {
            destinationBook.Close();
        }
    }

    private static HSSFWorkbook LoadXls(string file)
    {
        using FileStream stream = new FileStream(
            file,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite);

        return new HSSFWorkbook(stream);
    }

    private static void SaveXls(
        HSSFWorkbook workbook,
        string file)
    {
        string temporaryFile = file + ".tmp";

        try
        {
            using (FileStream stream = new FileStream(
                temporaryFile,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None))
            {
                workbook.Write(stream);
            }

            File.Copy(
                temporaryFile,
                file,
                true);
        }
        finally
        {
            if (File.Exists(temporaryFile))
            {
                File.Delete(temporaryFile);
            }
        }
    }

    private static ISheet GetFirstSheet(HSSFWorkbook workbook)
    {
        if (workbook.NumberOfSheets == 0)
        {
            throw new InvalidOperationException("В Excel-файле нет листов.");
        }

        return workbook.GetSheetAt(0);
    }

    private static string FindXlsFileByStem(
        string folder,
        string stem)
    {
        string[] files = Directory.GetFiles(
            folder,
            "*.xls",
            SearchOption.TopDirectoryOnly);

        string? match = files.FirstOrDefault(file =>
            string.Equals(
                Path.GetFileNameWithoutExtension(file),
                stem,
                StringComparison.OrdinalIgnoreCase));

        if (match == null)
        {
            throw new FileNotFoundException(
                $"Не найден Excel-файл:\n{stem}.xls\n\nПапка:\n{folder}");
        }

        return match;
    }

    private static void CopyValues(
        HSSFWorkbook sourceBook,
        ISheet sourceSheet,
        string sourceRangeAddress,
        ISheet destinationSheet,
        string destinationRangeAddress)
    {
        CellRange sourceRange = ParseRange(sourceRangeAddress);
        CellRange destinationRange = ParseRange(destinationRangeAddress);

        int sourceRows = sourceRange.LastRow - sourceRange.FirstRow + 1;
        int sourceColumns = sourceRange.LastColumn - sourceRange.FirstColumn + 1;
        int destinationRows = destinationRange.LastRow - destinationRange.FirstRow + 1;
        int destinationColumns = destinationRange.LastColumn - destinationRange.FirstColumn + 1;

        if (sourceRows != destinationRows || sourceColumns != destinationColumns)
        {
            throw new InvalidOperationException(
                $"Размеры диапазонов не совпадают: {sourceRangeAddress} → {destinationRangeAddress}.");
        }

        HSSFFormulaEvaluator evaluator = new HSSFFormulaEvaluator(sourceBook);

        for (int rowOffset = 0; rowOffset < sourceRows; rowOffset++)
        {
            for (int columnOffset = 0; columnOffset < sourceColumns; columnOffset++)
            {
                ICell sourceCell = GetCell(
                    sourceSheet,
                    sourceRange.FirstRow + rowOffset,
                    sourceRange.FirstColumn + columnOffset);

                ICell destinationCell = GetOrCreateCell(
                    destinationSheet,
                    destinationRange.FirstRow + rowOffset,
                    destinationRange.FirstColumn + columnOffset);

                CopyCellValue(sourceCell, destinationCell, evaluator);
            }
        }
    }

    private static void CopyBunkerValues(
        HSSFWorkbook sourceBook,
        ISheet sourceSheet,
        ISheet destinationSheet,
        string sourceRangeAddress,
        string destinationRangeAddress)
    {
        CellRange sourceRange = ParseRange(sourceRangeAddress);
        CellRange destinationRange = ParseRange(destinationRangeAddress);

        int sourceRows = sourceRange.LastRow - sourceRange.FirstRow + 1;
        int sourceColumns = sourceRange.LastColumn - sourceRange.FirstColumn + 1;
        int destinationRows = destinationRange.LastRow - destinationRange.FirstRow + 1;
        int destinationColumns = destinationRange.LastColumn - destinationRange.FirstColumn + 1;

        if (sourceRows != destinationRows ||
            sourceColumns != 3 ||
            destinationRows != sourceRows ||
            destinationColumns != 4)
        {
            throw new InvalidOperationException(
                $"Неверные размеры диапазонов остатка корма: {sourceRangeAddress} → {destinationRangeAddress}.");
        }

        HSSFFormulaEvaluator evaluator = new HSSFFormulaEvaluator(sourceBook);

        for (int rowOffset = 0; rowOffset < sourceRows; rowOffset++)
        {
            ICell sourceD = GetCell(
                sourceSheet,
                sourceRange.FirstRow + rowOffset,
                sourceRange.FirstColumn);

            ICell sourceE = GetCell(
                sourceSheet,
                sourceRange.FirstRow + rowOffset,
                sourceRange.FirstColumn + 1);

            ICell sourceF = GetCell(
                sourceSheet,
                sourceRange.FirstRow + rowOffset,
                sourceRange.FirstColumn + 2);

            ICell destinationD = GetOrCreateCell(
                destinationSheet,
                destinationRange.FirstRow + rowOffset,
                destinationRange.FirstColumn);

            ICell destinationE = GetOrCreateCell(
                destinationSheet,
                destinationRange.FirstRow + rowOffset,
                destinationRange.FirstColumn + 1);

            ICell destinationG = GetOrCreateCell(
                destinationSheet,
                destinationRange.FirstRow + rowOffset,
                destinationRange.FirstColumn + 3);

            // D → D, E → E, F → G.
            // Столбец F назначения намеренно не изменяем.
            CopyCellValue(sourceD, destinationD, evaluator);
            CopyCellValue(sourceE, destinationE, evaluator);
            CopyNumericValueOnly(sourceF, destinationG, evaluator);
        }
    }

    private static ICell? GetCell(
        ISheet sheet,
        int rowIndex,
        int columnIndex)
    {
        IRow? row = sheet.GetRow(rowIndex);

        if (row == null)
        {
            return null;
        }

        return row.GetCell(columnIndex);
    }

    private static ICell GetOrCreateCell(
        ISheet sheet,
        int rowIndex,
        int columnIndex)
    {
        IRow row = sheet.GetRow(rowIndex) ?? sheet.CreateRow(rowIndex);
        return row.GetCell(columnIndex) ?? row.CreateCell(columnIndex);
    }

    private static void CopyCellValue(
        ICell? source,
        ICell destination,
        HSSFFormulaEvaluator evaluator)
    {
        if (source == null || source.CellType == CellType.Blank)
        {
            destination.SetCellType(CellType.Blank);
            return;
        }

        switch (source.CellType)
        {
            case CellType.Boolean:
                destination.SetCellValue(source.BooleanCellValue);
                break;

            case CellType.Numeric:
                destination.SetCellValue(source.NumericCellValue);
                break;

            case CellType.String:
                destination.SetCellValue(source.StringCellValue);
                break;

            case CellType.Error:
                destination.SetCellErrorValue(source.ErrorCellValue);
                break;

            case CellType.Formula:
                try
                {
                    CellValue value = evaluator.Evaluate(source);

                    if (value == null)
                    {
                        destination.SetCellType(CellType.Blank);
                        break;
                    }

                    switch (value.CellType)
                    {
                        case CellType.Boolean:
                            destination.SetCellValue(value.BooleanValue);
                            break;

                        case CellType.Numeric:
                            destination.SetCellValue(value.NumberValue);
                            break;

                        case CellType.String:
                            destination.SetCellValue(value.StringValue ?? "");
                            break;

                        case CellType.Error:
                            destination.SetCellErrorValue(value.ErrorValue);
                            break;

                        default:
                            destination.SetCellType(CellType.Blank);
                            break;
                    }
                }
                catch
                {
                    // Если NPOI не смог вычислить формулу, используем
                    // сохранённый Excel-результат как запасной вариант.
                    switch (source.CachedFormulaResultType)
                    {
                        case CellType.Boolean:
                            destination.SetCellValue(source.BooleanCellValue);
                            break;

                        case CellType.Numeric:
                            destination.SetCellValue(source.NumericCellValue);
                            break;

                        case CellType.String:
                            destination.SetCellValue(source.StringCellValue);
                            break;

                        case CellType.Error:
                            destination.SetCellErrorValue(source.ErrorCellValue);
                            break;

                        default:
                            destination.SetCellType(CellType.Blank);
                            break;
                    }
                }
                break;

            default:
                destination.SetCellType(CellType.Blank);
                break;
        }
    }

    private static void CopyNumericValueOnly(
        ICell? source,
        ICell destination,
        HSSFFormulaEvaluator evaluator)
    {
        if (source == null || source.CellType == CellType.Blank)
        {
            destination.SetCellType(CellType.Blank);
            return;
        }

        double value;

        if (source.CellType == CellType.Formula)
        {
            try
            {
                CellValue evaluated = evaluator.Evaluate(source);
                if (evaluated.CellType != CellType.Numeric)
                {
                    destination.SetCellType(CellType.Blank);
                    return;
                }
                value = evaluated.NumberValue;
            }
            catch
            {
                if (source.CachedFormulaResultType != CellType.Numeric)
                {
                    destination.SetCellType(CellType.Blank);
                    return;
                }
                value = source.NumericCellValue;
            }
        }
        else if (source.CellType == CellType.Numeric)
        {
            value = source.NumericCellValue;
        }
        else
        {
            string text = source.CellType == CellType.String
                ? source.StringCellValue?.Trim() ?? ""
                : "";

            if (!double.TryParse(
                    text,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out value) &&
                !double.TryParse(
                    text,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.CurrentCulture,
                    out value))
            {
                destination.SetCellType(CellType.Blank);
                return;
            }
        }

        destination.SetCellValue(value);
    }

    private readonly struct CellRange
    {
        public int FirstRow { get; }
        public int LastRow { get; }
        public int FirstColumn { get; }
        public int LastColumn { get; }

        public CellRange(
            int firstRow,
            int lastRow,
            int firstColumn,
            int lastColumn)
        {
            FirstRow = firstRow;
            LastRow = lastRow;
            FirstColumn = firstColumn;
            LastColumn = lastColumn;
        }
    }

    private static CellRange ParseRange(string address)
    {
        string[] parts = address.Split(':');

        if (parts.Length != 2)
        {
            throw new ArgumentException(
                $"Неверный диапазон Excel: {address}");
        }

        (int firstColumn, int firstRow) = ParseCellAddress(parts[0]);
        (int lastColumn, int lastRow) = ParseCellAddress(parts[1]);

        return new CellRange(
            firstRow - 1,
            lastRow - 1,
            firstColumn - 1,
            lastColumn - 1);
    }

    private static (int Column, int Row) ParseCellAddress(string address)
    {
        int split = 0;

        while (split < address.Length &&
               char.IsLetter(address[split]))
        {
            split++;
        }

        if (split == 0 || split == address.Length)
        {
            throw new ArgumentException(
                $"Неверный адрес ячейки: {address}");
        }

        string letters = address[..split].ToUpperInvariant();
        string rowText = address[split..];

        if (!int.TryParse(rowText, out int row) || row <= 0)
        {
            throw new ArgumentException(
                $"Неверный номер строки: {address}");
        }

        int column = 0;

        foreach (char letter in letters)
        {
            column = column * 26 + (letter - 'A' + 1);
        }

        return (column, row);
    }

    // =========================================================
    // ПЕРЕСЛАТЬ ОТЧЁТЫ
    // =========================================================

    private void StartTransferTest()
    {
        if (exportRunning)
        {
            return;
        }

        Log("");

        LogRound(
            "ПЕРЕСЛАТЬ ОТЧЁТЫ");

        if (string.IsNullOrWhiteSpace(
            remoteReportsFolder) ||
            string.IsNullOrWhiteSpace(
                localReportsFolder))
        {
            Log(
                "Папки ещё не настроены.");

            MessageBox.Show(
                "Сначала нажми «Настроить папки отчётов» и укажи:\n\n" +
                "1. папку с отчётами на удалённых ПК;\n" +
                "2. папку на своём ПК.",
                "Переслать отчёты",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            return;
        }

        Log(
            $"Источник: {remoteReportsFolder}");

        Log(
            $"Назначение: {localReportsFolder}");

        Log(
            "Перенос файлов пока работает в тестовом режиме.");

        Log(
            "Файлы пока НЕ переносятся.");

        MessageBox.Show(
            "Папки сохранены.\n\n" +
            "Перенос файлов пока работает в тестовом режиме.\n\n" +
            "Сейчас файлы специально не трогаются.",
            "Переслать отчёты",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    // =========================================================
    // LOG
    // =========================================================

    private void Log(
        string text)
    {
        if (log.InvokeRequired)
        {
            log.BeginInvoke(
                new Action<string>(
                    Log),
                text);

            return;
        }

        log.AppendText(
            $"{DateTime.Now:HH:mm:ss}  " +
            $"{text}" +
            Environment.NewLine);
    }
}
