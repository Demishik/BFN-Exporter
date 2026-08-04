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
using System.Windows.Automation;
using System.Windows.Forms;

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
        "Отчет Бункер",
        "Файл",
        "Экспорт",
        "Поле имени файла",
        "Сохранить",
        "Нет",
        "Вернуться"
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

    private readonly Button exportButton = new();

    private readonly Button exportScreen1Button = new();

    private readonly Button exportScreen2Button = new();

    private readonly Button exportScreen3Button = new();

    private readonly Button exportScreen4Button = new();

    private readonly Button transferButton = new();

    private readonly Button lmViewerTestButton = new();

    private readonly Button foldersButton = new();

    private readonly Button cancelButton = new();

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
        Text =
            "BFN Exporter — 4 экрана";

        Width = 900;

        Height = 850;

        StartPosition =
            FormStartPosition.CenterScreen;

        KeyPreview = true;

        KeyDown += MainForm_KeyDown;

        // -----------------------------------------------------
        // Инструкция
        // -----------------------------------------------------

        instruction.Dock =
            DockStyle.Top;

        instruction.Height =
            65;

        instruction.Font =
            new Font(
                "Segoe UI",
                12);

        instruction.TextAlign =
            ContentAlignment.MiddleCenter;

        // -----------------------------------------------------
        // Мышь
        // -----------------------------------------------------

        mousePosition.Dock =
            DockStyle.Top;

        mousePosition.Height =
            35;

        mousePosition.Font =
            new Font(
                "Segoe UI",
                10);

        mousePosition.TextAlign =
            ContentAlignment.MiddleCenter;

        // -----------------------------------------------------
        // Экран
        // -----------------------------------------------------

        currentScreenLabel.Dock =
            DockStyle.Top;

        currentScreenLabel.Height =
            35;

        currentScreenLabel.Font =
            new Font(
                "Segoe UI",
                11,
                FontStyle.Bold);

        currentScreenLabel.TextAlign =
            ContentAlignment.MiddleCenter;

        // -----------------------------------------------------
        // Выбор экрана
        // -----------------------------------------------------

        screenSelector.Dock =
            DockStyle.Top;

        screenSelector.Height =
            35;

        screenSelector.DropDownStyle =
            ComboBoxStyle.DropDownList;

        foreach (string screen in screenNames)
        {
            screenSelector.Items.Add(
                screen);
        }

        screenSelector.SelectedIndex = 0;

        screenSelector.SelectedIndexChanged +=
            (_, _) =>
            {
                UpdateCurrentScreenLabel();

                if (setupIndex < 0 &&
                    !exportRunning)
                {
                    UpdateInstructionForCurrentScreen();
                }
            };

        // -----------------------------------------------------
        // Журнал
        // -----------------------------------------------------

        log.Multiline = true;

        log.ReadOnly = true;

        log.Dock =
            DockStyle.Fill;

        log.ScrollBars =
            ScrollBars.Vertical;

        // -----------------------------------------------------
        // КОМПАКТНЫЙ ИНТЕРФЕЙС — ВАРИАНТ №2
        // -----------------------------------------------------

        exportButton.Text =
            "▶ ВЫГРУЗИТЬ ВСЕ 4 ЭКРАНА";

        exportButton.Dock =
            DockStyle.Fill;

        exportButton.Height =
            52;

        exportButton.Font =
            new Font(
                "Segoe UI",
                11,
                FontStyle.Bold);

        exportButton.Click +=
            async (_, _) =>
            {
                await ExportAllScreens();
            };

        exportScreen1Button.Text =
            "Н-33, Н-34";

        exportScreen2Button.Text =
            "Р-18";

        exportScreen3Button.Text =
            "Р-20";

        exportScreen4Button.Text =
            "Катковка";

        Button[] screenButtons =
        {
            exportScreen1Button,
            exportScreen2Button,
            exportScreen3Button,
            exportScreen4Button
        };

        foreach (Button button in screenButtons)
        {
            button.Dock =
                DockStyle.Fill;

            button.Margin =
                new Padding(3);

            button.Font =
                new Font(
                    "Segoe UI",
                    9,
                    FontStyle.Bold);
        }

        exportScreen1Button.Click +=
            async (_, _) =>
            {
                await ExportSingleScreen(
                    "Н-33, Н-34");
            };

        exportScreen2Button.Click +=
            async (_, _) =>
            {
                await ExportSingleScreen(
                    "Р18");
            };

        exportScreen3Button.Click +=
            async (_, _) =>
            {
                await ExportSingleScreen(
                    "Р20");
            };

        exportScreen4Button.Click +=
            async (_, _) =>
            {
                await ExportSingleScreen(
                    "Катковка");
            };

        lmViewerTestButton.Text =
            "🔍 LM Viewer";

        lmViewerTestButton.Dock =
            DockStyle.Fill;

        lmViewerTestButton.Margin =
            new Padding(3);

        lmViewerTestButton.Click +=
            async (_, _) =>
            {
                await TestLmViewer();
            };

        foldersButton.Text =
            "📁 Папки";

        foldersButton.Dock =
            DockStyle.Fill;

        foldersButton.Margin =
            new Padding(3);

        foldersButton.Click +=
            (_, _) =>
            {
                ConfigureTransferFolders();
            };

        transferButton.Text =
            "⇄ Переслать отчёты";

        transferButton.Dock =
            DockStyle.Fill;

        transferButton.Margin =
            new Padding(3);

        transferButton.Click +=
            (_, _) =>
            {
                StartTransferTest();
            };

        cancelButton.Text =
            "■ Отмена (ESC)";

        cancelButton.Dock =
            DockStyle.Fill;

        cancelButton.Margin =
            new Padding(3);

        cancelButton.Enabled =
            false;

        cancelButton.Click +=
            (_, _) =>
            {
                CancelExport();
            };

        setupButton.Text =
            "⚙ Координаты";

        setupButton.Dock =
            DockStyle.Fill;

        setupButton.Margin =
            new Padding(3);

        setupButton.Click +=
            (_, _) =>
            {
                StartSetup();
            };

        TableLayoutPanel bottomPanel =
            new TableLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 205,
                ColumnCount = 4,
                RowCount = 3,
                Padding = new Padding(5)
            };

        bottomPanel.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                25));

        bottomPanel.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                25));

        bottomPanel.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                25));

        bottomPanel.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                25));

        bottomPanel.RowStyles.Add(
            new RowStyle(
                SizeType.Absolute,
                52));

        bottomPanel.RowStyles.Add(
            new RowStyle(
                SizeType.Absolute,
                58));

        bottomPanel.RowStyles.Add(
            new RowStyle(
                SizeType.Percent,
                100));

        bottomPanel.Controls.Add(
            exportButton,
            0,
            0);

        bottomPanel.SetColumnSpan(
            exportButton,
            4);

        bottomPanel.Controls.Add(
            exportScreen1Button,
            0,
            1);

        bottomPanel.Controls.Add(
            exportScreen2Button,
            1,
            1);

        bottomPanel.Controls.Add(
            exportScreen3Button,
            2,
            1);

        bottomPanel.Controls.Add(
            exportScreen4Button,
            3,
            1);

        TableLayoutPanel servicePanel =
            new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 5,
                RowCount = 1
            };

        for (int i = 0; i < 5; i++)
        {
            servicePanel.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Percent,
                    20));
        }

        servicePanel.Controls.Add(
            setupButton,
            0,
            0);

        servicePanel.Controls.Add(
            foldersButton,
            1,
            0);

        servicePanel.Controls.Add(
            transferButton,
            2,
            0);

        servicePanel.Controls.Add(
            lmViewerTestButton,
            3,
            0);

        servicePanel.Controls.Add(
            cancelButton,
            4,
            0);

        bottomPanel.Controls.Add(
            servicePanel,
            0,
            2);

        bottomPanel.SetColumnSpan(
            servicePanel,
            4);

        // -----------------------------------------------------
        // CONTROLS
        // -----------------------------------------------------

        Controls.Add(
            log);

        Controls.Add(
            bottomPanel);

        Controls.Add(
            screenSelector);

        Controls.Add(
            mousePosition);

        Controls.Add(
            currentScreenLabel);

        Controls.Add(
            instruction);

        // -----------------------------------------------------
        // TIMER
        // -----------------------------------------------------

        System.Windows.Forms.Timer timer =
            new System.Windows.Forms.Timer();

        timer.Interval =
            100;

        timer.Tick +=
            (_, _) =>
            {
                if (GetCursorPos(
                    out POINT p))
                {
                    mousePosition.Text =
                        $"Положение мыши: X={p.X}   Y={p.Y}";
                }
            };

        timer.Start();

        // -----------------------------------------------------
        // ЗАГРУЗКА
        // -----------------------------------------------------

        LoadCoordinates();

        LoadTransferSettings();

        UpdateCurrentScreenLabel();

        Log(
            "BFN Exporter запущен.");

        Log(
            "Координаты существующей выгрузки сохранены.");

        Log(
            "Доступна выгрузка всех 4 экранов.");

        Log(
            "Доступна отдельная выгрузка каждого экрана.");

        Log(
            "Добавлен безопасный тест LM Viewer.");

        UpdateInstructionForCurrentScreen();

        // -----------------------------------------------------
        // ESC
        // -----------------------------------------------------

        if (!RegisterHotKey(
            Handle,
            HOTKEY_ESC,
            MOD_NOREPEAT,
            VK_ESCAPE))
        {
            Log(
                "ВНИМАНИЕ: не удалось зарегистрировать глобальную клавишу ESC.");
        }
        else
        {
            Log(
                "ESC зарегистрирован как глобальная отмена.");
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
            else if (setupIndex >= 0)
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

        points.Clear();

        setupIndex = 0;

        setupButton.Enabled = false;

        exportButton.Enabled = false;

        exportScreen1Button.Enabled = false;
        exportScreen2Button.Enabled = false;
        exportScreen3Button.Enabled = false;
        exportScreen4Button.Enabled = false;

        transferButton.Enabled = false;

        lmViewerTestButton.Enabled = false;

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
            $"Нужно сохранить {pointNames.Length} координат.");
    }

    private void CancelSetup()
    {
        if (setupIndex < 0)
        {
            return;
        }

        string screen =
            CurrentSetupScreen;

        setupIndex = -1;

        GetOrCreateScreenPoints(
            screen).Clear();

        setupButton.Enabled = true;

        exportButton.Enabled = true;

        exportScreen1Button.Enabled = true;
        exportScreen2Button.Enabled = true;
        exportScreen3Button.Enabled = true;
        exportScreen4Button.Enabled = true;

        transferButton.Enabled = true;

        lmViewerTestButton.Enabled = true;

        foldersButton.Enabled = true;

        cancelButton.Enabled = false;

        screenSelector.Enabled = true;

        instruction.Text =
            "Настройка отменена.";

        Log(
            $"Настройка координат «{screen}» отменена.");
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

        if (e.KeyCode == Keys.Escape)
        {
            e.SuppressKeyPress = true;

            if (exportRunning)
            {
                CancelExport();
            }
            else if (setupIndex >= 0)
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

            exportButton.Enabled = true;

            exportScreen1Button.Enabled = true;
            exportScreen2Button.Enabled = true;
            exportScreen3Button.Enabled = true;
            exportScreen4Button.Enabled = true;

            transferButton.Enabled = true;

            lmViewerTestButton.Enabled = true;

            foldersButton.Enabled = true;

            cancelButton.Enabled = false;

            screenSelector.Enabled = true;

            instruction.Text =
                $"Готово! 9 координат для «{screen}» сохранены.";

            Log(
                $"Все 9 координат для «{screen}» сохранены.");

            return;
        }

        instruction.Text =
            $"«{screen}»: наведи мышь на «{pointNames[setupIndex]}» и нажми F8.";
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
                        $"{screen}: 9 координат загружено.");
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

        if (!allPoints.TryGetValue(
            screen,
            out Dictionary<string, Point>? points))
        {
            throw new Exception(
                $"Нет координат экрана: {screen}");
        }

        if (!points.TryGetValue(
            name,
            out Point p))
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

    private void ReplaceFileName(
        string fileName,
        CancellationToken token)
    {
        CheckCancellation(
            token);

        Log(
            "Очищаем старое имя файла.");

        SendKeys.SendWait(
            "{HOME}");

        Wait(
            300,
            token);

        SendKeys.SendWait(
            "+{END}");

        Wait(
            300,
            token);

        SendKeys.SendWait(
            "{BACKSPACE}");

        Wait(
            500,
            token);

        Log(
            "Старое имя удалено.");

        CheckCancellation(
            token);

        Log(
            "Вводим имя файла.");

        SendKeys.SendWait(
            fileName);

        Wait(
            1000,
            token);

        Log(
            $"Новое имя введено: {fileName}");
    }

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
        CheckCancellation(
            token);

        Log("");

        Log(
            $"[{screen}] Сохраняем: {fileName}");

        ClickPoint(
            screen,
            "Файл",
            token);

        Wait(
            300,
            token);

        ClickPoint(
            screen,
            "Экспорт",
            token);

        Wait(
            1500,
            token);

        ClickPoint(
            screen,
            "Поле имени файла",
            token);

        Wait(
            300,
            token);

        ReplaceFileName(
            fileName,
            token);

        Wait(
            300,
            token);

        ClickPoint(
            screen,
            "Сохранить",
            token);

        Wait(
            1500,
            token);

        ClickPoint(
            screen,
            "Нет",
            token);

        Wait(
            1000,
            token);

        Log(
            $"[{screen}] Сохранено: {fileName}");
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
            "Производственный отчет.xlsx";
    }

    private string DamateFile(
        string screen,
        DateTime yesterday)
    {
        return
            $"{yesterday:yyyy_MM_dd} " +
            $"{screen} " +
            "Дамате клик.xlsx";
    }

    private string BunkerFile(
        string screen,
        DateTime today)
    {
        return
            $"{today:yyyy_MM_dd} " +
            $"{screen} " +
            "Остаток корма на 00-00.xlsx";
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
                    $"Для экрана «{screen}» не настроены все 9 координат.");
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
                $"Для экрана «{screen}» не настроены все 9 координат.");
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

        transferButton.Enabled =
            !running;

        lmViewerTestButton.Enabled =
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

    // =========================================================
    // UI AUTOMATION — ОСНОВНАЯ ПАНЕЛЬ ЗАДАЧ
    // =========================================================

    private sealed class UiTaskbarElementInfo
    {
        public string Name = "";
        public string ClassName = "";
        public string ControlType = "";
        public string AutomationId = "";
        public string ProcessName = "";
        public int NativeWindowHandle;
        public Rectangle Bounds;
    }

    private List<UiTaskbarElementInfo> GetTaskbarUiAutomationElements()
    {
        List<UiTaskbarElementInfo> result =
            new List<UiTaskbarElementInfo>();

        IntPtr taskbarHandle =
            FindWindow(
                "Shell_TrayWnd",
                null);

        if (taskbarHandle == IntPtr.Zero)
        {
            Log("UI Automation: Shell_TrayWnd не найден.");
            return result;
        }

        AutomationElement taskbar;

        try
        {
            taskbar =
                AutomationElement.FromHandle(
                    taskbarHandle);
        }
        catch (Exception ex)
        {
            Log("UI Automation: не удалось получить AutomationElement панели задач.");
            Log(ex.Message);
            return result;
        }

        AutomationElement searchRoot =
            taskbar;

        // Пытаемся найти именно основной список работающих приложений.
        // Второй монитор здесь вообще не используется.
        try
        {
            Condition classCondition =
                new PropertyCondition(
                    AutomationElement.ClassNameProperty,
                    "MSTaskListWClass",
                    PropertyConditionFlags.IgnoreCase);

            AutomationElement taskList =
                taskbar.FindFirst(
                    TreeScope.Descendants,
                    classCondition);

            if (taskList != null)
            {
                searchRoot = taskList;
                Log("UI Automation: найден MSTaskListWClass.");
            }
            else
            {
                Log("UI Automation: MSTaskListWClass не найден. Проверяем всё дерево основной панели задач.");
            }
        }
        catch (Exception ex)
        {
            Log("UI Automation: ошибка поиска MSTaskListWClass:");
            Log(ex.Message);
        }

        AutomationElementCollection elements;

        try
        {
            elements =
                searchRoot.FindAll(
                    TreeScope.Descendants,
                    Condition.TrueCondition);
        }
        catch (Exception ex)
        {
            Log("UI Automation: ошибка чтения дерева элементов:");
            Log(ex.Message);
            return result;
        }

        for (int i = 0; i < elements.Count; i++)
        {
            AutomationElement element =
                elements[i];

            try
            {
                AutomationElement.AutomationElementInformation info =
                    element.Current;

                string name =
                    info.Name ?? "";

                string className =
                    info.ClassName ?? "";

                string controlType =
                    info.ControlType != null
                        ? info.ControlType.ProgrammaticName
                        : "";

                string automationId =
                    info.AutomationId ?? "";

                Rect rect =
                    info.BoundingRectangle;

                Rectangle bounds =
                    new Rectangle(
                        (int)Math.Round(rect.Left),
                        (int)Math.Round(rect.Top),
                        Math.Max(0, (int)Math.Round(rect.Width)),
                        Math.Max(0, (int)Math.Round(rect.Height)));

                int nativeHandle =
                    info.NativeWindowHandle;

                string processName =
                    "";

                if (info.ProcessId > 0)
                {
                    try
                    {
                        using Process process =
                            Process.GetProcessById(
                                info.ProcessId);

                        processName =
                            process.ProcessName;
                    }
                    catch
                    {
                    }
                }

                // Сохраняем элементы, которые имеют имя, кнопку,
                // собственный HWND или заметную область на панели.
                if (!string.IsNullOrWhiteSpace(name) ||
                    controlType.Contains("Button", StringComparison.OrdinalIgnoreCase) ||
                    nativeHandle != 0 ||
                    (bounds.Width > 0 && bounds.Height > 0))
                {
                    result.Add(
                        new UiTaskbarElementInfo
                        {
                            Name = name,
                            ClassName = className,
                            ControlType = controlType,
                            AutomationId = automationId,
                            ProcessName = processName,
                            NativeWindowHandle = nativeHandle,
                            Bounds = bounds
                        });
                }
            }
            catch
            {
                // Элемент мог исчезнуть во время чтения дерева.
            }
        }

        return result;
    }

    private static bool IsLmViewerUiElement(
        UiTaskbarElementInfo element)
    {
        string text =
            (
                element.Name + " " +
                element.ClassName + " " +
                element.AutomationId + " " +
                element.ProcessName
            ).ToLowerInvariant();

        return
            text.Contains("lm viewer") ||
            text.Contains("lmviewer") ||
            text.Contains("litemanager") ||
            text.Contains("lite manager") ||
            text.Contains("liteclient") ||
            text.Contains("litemanager.exe");
    }

    private static HashSet<IntPtr>
        GetWindowHandles(
            List<WindowInfo> windows)
    {
        HashSet<IntPtr> result =
            new();

        foreach (WindowInfo window in windows)
        {
            result.Add(
                window.Handle);
        }

        return result;
    }

    private static WindowInfo?
        FindNewWindow(
            List<WindowInfo> before,
            IntPtr originalHandle)
    {
        HashSet<IntPtr> oldHandles =
            GetWindowHandles(
                before);

        List<WindowInfo> after =
            new();

        EnumWindows(
            (hWnd, _) =>
            {
                if (!IsWindowVisible(
                    hWnd))
                {
                    return true;
                }

                if (hWnd ==
                    originalHandle)
                {
                    return true;
                }

                if (oldHandles.Contains(
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

                after.Add(
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

        if (after.Count == 0)
        {
            return null;
        }

        // Сначала ищем новое окно,
        // которое принадлежит тому же процессу.
        WindowInfo? original =
            before.Find(
                x => x.Handle ==
                     originalHandle);

        if (original != null)
        {
            WindowInfo? sameProcess =
                after.Find(
                    x => x.ProcessId ==
                         original.ProcessId);

            if (sameProcess != null)
            {
                return sameProcess;
            }
        }

        return after[0];
    }

    // =========================================================
    // MIDDLE CLICK
    // =========================================================

    private void MiddleClickWindow(
        IntPtr hWnd)
    {
        if (!GetWindowRect(
            hWnd,
            out RECT rect))
        {
            throw new Exception(
                "Не удалось получить положение окна LM Viewer.");
        }

        int x =
            rect.Left +
            ((rect.Right - rect.Left) / 2);

        int y =
            rect.Top +
            ((rect.Bottom - rect.Top) / 2);

        Log(
            $"LM Viewer: средний клик X={x}, Y={y}");

        SetCursorPos(
            x,
            y);

        Thread.Sleep(
            300);

        mouse_event(
            MOUSEEVENTF_MIDDLEDOWN,
            0,
            0,
            0,
            UIntPtr.Zero);

        Thread.Sleep(
            100);

        mouse_event(
            MOUSEEVENTF_MIDDLEUP,
            0,
            0,
            0,
            UIntPtr.Zero);
    }

    // =========================================================
    // ТЕСТ LM VIEWER
    // =========================================================

    private async Task TestLmViewer()
    {
        if (exportRunning)
        {
            return;
        }

        lmViewerTestButton.Enabled = false;

        try
        {
            Log("");
            LogRound("UI AUTOMATION — ПОИСК LM VIEWER НА ОСНОВНОЙ ПАНЕЛИ ЗАДАЧ");

            List<UiTaskbarElementInfo> elements =
                GetTaskbarUiAutomationElements();

            Log(
                $"UI Automation: найдено элементов для диагностики: {elements.Count}");

            if (elements.Count == 0)
            {
                Log("UI Automation не вернул ни одного элемента.");
                Log("Средний клик НЕ выполнялся.");

                MessageBox.Show(
                    "UI Automation не вернул элементы панели задач.\n\n" +
                    "Средний клик не выполнялся.",
                    "LM Viewer",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            int index = 0;
            int lmCount = 0;

            foreach (UiTaskbarElementInfo element in elements)
            {
                index++;

                string name =
                    string.IsNullOrWhiteSpace(element.Name)
                        ? "<без имени>"
                        : element.Name;

                string className =
                    string.IsNullOrWhiteSpace(element.ClassName)
                        ? "<без класса>"
                        : element.ClassName;

                string controlType =
                    string.IsNullOrWhiteSpace(element.ControlType)
                        ? "<без типа>"
                        : element.ControlType;

                string automationId =
                    string.IsNullOrWhiteSpace(element.AutomationId)
                        ? "<без AutomationId>"
                        : element.AutomationId;

                string processName =
                    string.IsNullOrWhiteSpace(element.ProcessName)
                        ? "<неизвестно>"
                        : element.ProcessName;

                Rectangle r =
                    element.Bounds;

                string hwnd =
                    element.NativeWindowHandle == 0
                        ? "<нет>"
                        : element.NativeWindowHandle.ToString();

                string marker =
                    IsLmViewerUiElement(element)
                        ? "  <<< ВОЗМОЖНЫЙ LM VIEWER"
                        : "";

                if (marker.Length > 0)
                {
                    lmCount++;
                }

                Log(
                    $"[{index}] Name=\"{name}\" | " +
                    $"ControlType=\"{controlType}\" | " +
                    $"Class=\"{className}\" | " +
                    $"AutomationId=\"{automationId}\" | " +
                    $"Process=\"{processName}\" | " +
                    $"HWND={hwnd} | " +
                    $"X={r.X}, Y={r.Y}, W={r.Width}, H={r.Height}" +
                    marker);
            }

            Log("");
            Log(
                $"Совпадений по LM Viewer: {lmCount}");
            Log("Диагностика завершена. Средний клик НЕ выполнялся.");

            MessageBox.Show(
                "UI Automation завершил поиск.\n\n" +
                $"Элементов: {elements.Count}\n" +
                $"Совпадений по LM Viewer: {lmCount}\n\n" +
                "Средний клик НЕ выполнялся.",
                "LM Viewer",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            Log("Ошибка UI Automation LM Viewer:");
            Log(ex.ToString());

            MessageBox.Show(
                "Ошибка UI Automation:\n\n" + ex.Message,
                "LM Viewer",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            lmViewerTestButton.Enabled =
                !exportRunning;
        }

        await Task.CompletedTask;
    }

    // =========================================================

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
            "LM Viewer пока запускается только в тестовом режиме.");

        Log(
            "Файлы пока НЕ переносятся.");

        MessageBox.Show(
            "Папки сохранены.\n\n" +
            "Следующим этапом подключаем реальный перенос файлов через файловое окно LM Viewer.\n\n" +
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
