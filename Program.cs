using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
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
    // WINAPI
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

    // ---------------------------------------------------------
    // LM VIEWER
    // ---------------------------------------------------------

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(
        EnumWindowsProc lpEnumFunc,
        IntPtr lParam);

    private delegate bool EnumWindowsProc(
        IntPtr hWnd,
        IntPtr lParam);

    [DllImport(
        "user32.dll",
        CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(
        IntPtr hWnd,
        StringBuilder lpString,
        int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(
        IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(
        IntPtr hWnd,
        out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(
        IntPtr hWnd,
        int nCmdShow);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    // =========================================================
    // MOUSE
    // =========================================================

    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;

    private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;

    // =========================================================
    // SHOW WINDOW
    // =========================================================

    private const int SW_MAXIMIZE = 3;

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
    // КООРДИНАТЫ
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
    private readonly Button cancelButton = new();

    // НОВАЯ КНОПКА LM VIEWER
    private readonly Button lmViewerTestButton = new();

    // =========================================================
    // СОСТОЯНИЕ
    // =========================================================

    private int setupIndex = -1;

    private CancellationTokenSource? exportCancellation;

    private bool exportRunning;

    private string CurrentSetupScreen =>
        screenSelector.SelectedItem?.ToString()
        ?? screenNames[0];

    // =========================================================
    // ФАЙЛ НАСТРОЕК
    // =========================================================

    private string SettingsFile
    {
        get
        {
            string folder =
                Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.ApplicationData),
                    "BFN_Exporter");

            Directory.CreateDirectory(folder);

            return Path.Combine(
                folder,
                "coordinates.json");
        }
    }

    // =========================================================
    // CONSTRUCTOR
    // =========================================================

    public MainForm()
    {
        Text = "BFN Exporter — 4 экрана";

        Width = 820;
        Height = 650;

        StartPosition =
            FormStartPosition.CenterScreen;

        KeyPreview = true;

        KeyDown += MainForm_KeyDown;

        // -----------------------------------------------------
        // Инструкция
        // -----------------------------------------------------

        instruction.Dock =
            DockStyle.Top;

        instruction.Height = 65;

        instruction.Font =
            new Font(
                "Segoe UI",
                12);

        instruction.TextAlign =
            ContentAlignment.MiddleCenter;

        // -----------------------------------------------------
        // Положение мыши
        // -----------------------------------------------------

        mousePosition.Dock =
            DockStyle.Top;

        mousePosition.Height = 35;

        mousePosition.Font =
            new Font(
                "Segoe UI",
                10);

        mousePosition.TextAlign =
            ContentAlignment.MiddleCenter;

        // -----------------------------------------------------
        // Текущий экран
        // -----------------------------------------------------

        currentScreenLabel.Dock =
            DockStyle.Top;

        currentScreenLabel.Height = 35;

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

        screenSelector.Height = 35;

        screenSelector.DropDownStyle =
            ComboBoxStyle.DropDownList;

        foreach (string screen in screenNames)
        {
            screenSelector.Items.Add(screen);
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
        log.Dock = DockStyle.Fill;
        log.ScrollBars =
            ScrollBars.Vertical;

        // -----------------------------------------------------
        // Кнопка выгрузки
        // -----------------------------------------------------

        exportButton.Text =
            "▶ ВЫГРУЗИТЬ ВСЕ 4 ЭКРАНА";

        exportButton.Dock =
            DockStyle.Bottom;

        exportButton.Height = 55;

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

        // -----------------------------------------------------
        // Кнопка отмены
        // -----------------------------------------------------

        cancelButton.Text =
            "■ ОТМЕНА  (ESC)";

        cancelButton.Dock =
            DockStyle.Bottom;

        cancelButton.Height = 50;

        cancelButton.Enabled = false;

        cancelButton.Font =
            new Font(
                "Segoe UI",
                10,
                FontStyle.Bold);

        cancelButton.Click +=
            (_, _) =>
            {
                CancelExport();
            };

        // -----------------------------------------------------
        // Настройка координат
        // -----------------------------------------------------

        setupButton.Text =
            "⚙ Настроить координаты выбранного экрана";

        setupButton.Dock =
            DockStyle.Bottom;

        setupButton.Height = 50;

        setupButton.Click +=
            (_, _) =>
            {
                StartSetup();
            };

        // -----------------------------------------------------
        // НОВАЯ КНОПКА LM VIEWER
        // -----------------------------------------------------

        lmViewerTestButton.Text =
            "🖥 ТЕСТ LM VIEWER";

        lmViewerTestButton.Dock =
            DockStyle.Bottom;

        lmViewerTestButton.Height = 50;

        lmViewerTestButton.Font =
            new Font(
                "Segoe UI",
                10,
                FontStyle.Bold);

        lmViewerTestButton.Click +=
            async (_, _) =>
            {
                await TestLmViewer();
            };

        // -----------------------------------------------------
        // Добавление элементов
        // -----------------------------------------------------

        Controls.Add(log);

        Controls.Add(exportButton);

        Controls.Add(cancelButton);

        Controls.Add(lmViewerTestButton);

        Controls.Add(setupButton);

        Controls.Add(screenSelector);

        Controls.Add(mousePosition);

        Controls.Add(currentScreenLabel);

        Controls.Add(instruction);

        // -----------------------------------------------------
        // Таймер положения мыши
        // -----------------------------------------------------

        System.Windows.Forms.Timer timer =
            new System.Windows.Forms.Timer();

        timer.Interval = 100;

        timer.Tick +=
            (_, _) =>
            {
                if (GetCursorPos(out POINT p))
                {
                    mousePosition.Text =
                        $"Положение мыши: X={p.X}   Y={p.Y}";
                }
            };

        timer.Start();

        // -----------------------------------------------------
        // Загрузка координат
        // -----------------------------------------------------

        LoadCoordinates();

        UpdateCurrentScreenLabel();

        Log(
            "BFN Exporter запущен.");

        Log(
            "Настройка выполняется отдельно для каждого экрана.");

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
    // ПОИСК ОКНА LM VIEWER
    // =========================================================

    private IntPtr FindLmViewerWindow()
    {
        IntPtr result =
            IntPtr.Zero;

        EnumWindows(
            (hWnd, _) =>
            {
                if (!IsWindowVisible(hWnd))
                {
                    return true;
                }

                StringBuilder title =
                    new StringBuilder(512);

                GetWindowText(
                    hWnd,
                    title,
                    title.Capacity);

                string text =
                    title.ToString();

                if (text.Contains(
                        "LM Viewer",
                        StringComparison.OrdinalIgnoreCase))
                {
                    result = hWnd;

                    return false;
                }

                return true;

            },
            IntPtr.Zero);

        return result;
    }

    // =========================================================
    // ПОЛУЧИТЬ ВСЕ ВИДИМЫЕ ОКНА LM VIEWER
    // =========================================================

    private HashSet<IntPtr>
        GetVisibleLmViewerWindows()
    {
        HashSet<IntPtr> windows =
            new();

        EnumWindows(
            (hWnd, _) =>
            {
                if (!IsWindowVisible(hWnd))
                {
                    return true;
                }

                StringBuilder title =
                    new StringBuilder(512);

                GetWindowText(
                    hWnd,
                    title,
                    title.Capacity);

                string text =
                    title.ToString();

                if (text.Contains(
                        "LM Viewer",
                        StringComparison.OrdinalIgnoreCase))
                {
                    windows.Add(hWnd);
                }

                return true;

            },
            IntPtr.Zero);

        return windows;
    }

    // =========================================================
    // ТЕСТ LM VIEWER
    // =========================================================

    private async Task TestLmViewer()
    {
        if (exportRunning ||
            setupIndex >= 0)
        {
            MessageBox.Show(
                "Сначала дождись окончания другой операции.",
                "BFN Exporter",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            return;
        }

        lmViewerTestButton.Enabled = false;

        try
        {
            Log("");

            Log(
                "========================================");

            Log(
                "ТЕСТ LM VIEWER");

            Log(
                "========================================");

            // -------------------------------------------------
            // 1. Ищем исходное окно
            // -------------------------------------------------

            Log(
                "Ищем уже открытое окно LM Viewer...");

            IntPtr originalWindow =
                FindLmViewerWindow();

            if (originalWindow ==
                IntPtr.Zero)
            {
                Log(
                    "ОШИБКА: окно LM Viewer не найдено.");

                MessageBox.Show(
                    "Не удалось найти открытое окно LM Viewer.",
                    "BFN Exporter",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            Log(
                $"Исходное окно LM Viewer найдено: {originalWindow}");

            // -------------------------------------------------
            // 2. Запоминаем существующие окна
            // -------------------------------------------------

            HashSet<IntPtr> before =
                GetVisibleLmViewerWindows();

            // -------------------------------------------------
            // 3. Получаем центр исходного окна
            // -------------------------------------------------

            if (!GetWindowRect(
                    originalWindow,
                    out RECT rect))
            {
                throw new Exception(
                    "Не удалось получить положение окна LM Viewer.");
            }

            int centerX =
                rect.Left +
                ((rect.Right - rect.Left) / 2);

            int centerY =
                rect.Top +
                ((rect.Bottom - rect.Top) / 2);

            // -------------------------------------------------
            // 4. Наводим мышь
            // -------------------------------------------------

            Log(
                $"Наводим мышь на LM Viewer: X={centerX}, Y={centerY}");

            Cursor.Position =
                new Point(
                    centerX,
                    centerY);

            await Task.Delay(300);

            // -------------------------------------------------
            // 5. Нажимаем колесо
            // -------------------------------------------------

            Log(
                "Нажимаем колесо мыши...");

            mouse_event(
                MOUSEEVENTF_MIDDLEDOWN,
                0,
                0,
                0,
                UIntPtr.Zero);

            await Task.Delay(100);

            mouse_event(
                MOUSEEVENTF_MIDDLEUP,
                0,
                0,
                0,
                UIntPtr.Zero);

            Log(
                "Колесо нажато.");

            // -------------------------------------------------
            // 6. Ждём новое окно
            // -------------------------------------------------

            Log(
                "Ждём появления нового окна LM Viewer...");

            IntPtr newWindow =
                IntPtr.Zero;

            for (int i = 0; i < 30; i++)
            {
                await Task.Delay(200);

                HashSet<IntPtr> after =
                    GetVisibleLmViewerWindows();

                foreach (IntPtr hWnd in after)
                {
                    if (!before.Contains(hWnd) &&
                        hWnd != originalWindow)
                    {
                        newWindow = hWnd;

                        break;
                    }
                }

                if (newWindow !=
                    IntPtr.Zero)
                {
                    break;
                }
            }

            // -------------------------------------------------
            // 7. Проверяем новое окно
            // -------------------------------------------------

            if (newWindow ==
                IntPtr.Zero)
            {
                Log(
                    "ОШИБКА: новое окно LM Viewer не появилось.");

                MessageBox.Show(
                    "Новое окно LM Viewer не обнаружено.",
                    "BFN Exporter",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            StringBuilder newTitle =
                new StringBuilder(512);

            GetWindowText(
                newWindow,
                newTitle,
                newTitle.Capacity);

            Log(
                $"Новое окно найдено: {newWindow}");

            Log(
                $"Заголовок нового окна: {newTitle}");

            // -------------------------------------------------
            // 8. Разворачиваем НОВОЕ окно
            // -------------------------------------------------

            Log(
                "Разворачиваем именно новое окно LM Viewer...");

            ShowWindow(
                newWindow,
                SW_MAXIMIZE);

            await Task.Delay(1000);

            Log(
                "Новое окно LM Viewer развернуто.");

            Log(
                "Исходное окно LM Viewer не трогалось.");

            Log(
                "ТЕСТ ЗАВЕРШЁН.");

            MessageBox.Show(
                "Тест выполнен.\n\n" +
                "Новое окно LM Viewer найдено и развернуто.\n\n" +
                "Исходное окно не трогалось.\n\n" +
                "Новое окно пока НЕ закрывается.",
                "BFN Exporter",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            Log(
                "Ошибка теста LM Viewer: " +
                ex.Message);

            MessageBox.Show(
                ex.Message,
                "Ошибка LM Viewer",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            lmViewerTestButton.Enabled = true;
        }
    }

    // =========================================================
    // WNDPROC
    // =========================================================

    protected override void WndProc(
        ref Message m)
    {
        const int WM_HOTKEY = 0x0312;

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

        base.WndProc(ref m);
    }

    // =========================================================
    // ЗАКРЫТИЕ ФОРМЫ
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

        base.OnFormClosed(e);
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
    // НАСТРОЙКА
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
            GetOrCreateScreenPoints(screen);

        points.Clear();

        setupIndex = 0;

        setupButton.Enabled = false;
        exportButton.Enabled = false;
        cancelButton.Enabled = true;
        screenSelector.Enabled = false;
        lmViewerTestButton.Enabled = false;

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

        GetOrCreateScreenPoints(screen)
            .Clear();

        setupButton.Enabled = true;
        exportButton.Enabled = true;
        cancelButton.Enabled = false;
        screenSelector.Enabled = true;
        lmViewerTestButton.Enabled = true;

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
    // СОХРАНЕНИЕ ТОЧКИ
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
            GetOrCreateScreenPoints(screen);

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
            cancelButton.Enabled = false;
            screenSelector.Enabled = true;
            lmViewerTestButton.Enabled = true;

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
                if (HasAllCoordinates(screen))
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
    // CANCEL CHECK
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
        CheckCancellation(token);

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
        CheckCancellation(token);

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

        Cursor.Position = p;

        Wait(
            500,
            token);

        CheckCancellation(token);

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
        CheckCancellation(token);

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

        CheckCancellation(token);

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
        CheckCancellation(token);

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
        CheckCancellation(token);

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
            if (!HasAllCoordinates(screen))
            {
                throw new Exception(
                    $"Для экрана «{screen}» не настроены все 9 координат.");
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

        Log(title);

        Log(
            "========================================");
    }

    // =========================================================
    // EXPORT ALL
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
                "\n\nСначала выбери нужный экран и настрой 9 координат.",
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

        SetRunningState(true);

        DateTime today =
            DateTime.Today;

        DateTime yesterday =
            today.AddDays(-1);

        try
        {
            Log("");

            Log(
                "========================================");

            Log(
                "НАЧАЛО КОНВЕЙЕРНОЙ ВЫГРУЗКИ");

            Log(
                "4 ЭКРАНА × 3 ОТЧЁТА");

            Log(
                "========================================");

            // -------------------------------------------------
            // КРУГ 1
            // -------------------------------------------------

            LogRound(
                "КРУГ 1 — ЗАПУСК ПРОИЗВОДСТВЕННОГО ОТЧЁТА");

            foreach (string screen in screenNames)
            {
                CheckCancellation(token);

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
                CheckCancellation(token);

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
                CheckCancellation(token);

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
                CheckCancellation(token);

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
                CheckCancellation(token);

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
                CheckCancellation(token);

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
                CheckCancellation(token);

                ReturnToStart(
                    screen,
                    token);
            }

            Log("");

            Log(
                "========================================");

            Log(
                "ВСЕ 4 ЭКРАНА УСПЕШНО ВЫГРУЖЕНЫ");

            Log(
                "ВСЕ ЭКРАНЫ ВОЗВРАЩЕНЫ В ИСХОДНОЕ СОСТОЯНИЕ");

            Log(
                "========================================");

            MessageBox.Show(
                "Все 4 экрана успешно выгружены.\n\n" +
                "Все экраны возвращены в исходное состояние.",
                "BFN Exporter",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (OperationCanceledException)
        {
            Log("");

            Log(
                "========================================");

            Log(
                "ОПЕРАЦИЯ ОТМЕНЕНА ПОЛЬЗОВАТЕЛЕМ");

            Log(
                "========================================");

            MessageBox.Show(
                "Выгрузка отменена.",
                "BFN Exporter",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            Log("");

            Log(
                "========================================");

            Log(
                "ОШИБКА ПРИ ВЫГРУЗКЕ");

            Log(
                "========================================");

            Log(
                ex.Message);

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
    // CANCEL EXPORT
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
    // RUNNING STATE
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

        setupButton.Enabled =
            !running;

        cancelButton.Enabled =
            running;

        screenSelector.Enabled =
            !running;

        lmViewerTestButton.Enabled =
            !running;

        if (running)
        {
            instruction.Text =
                "Выполняется конвейерная выгрузка. Для отмены нажми ESC.";
        }
        else
        {
            UpdateInstructionForCurrentScreen();
        }
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
