using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
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

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;

    private const int HOTKEY_ESC = 1001;
    private const uint MOD_NOREPEAT = 0x4000;
    private const uint VK_ESCAPE = 0x1B;

    // ---------------------------------------------------------
    // 4 удалённых ПК
    // ---------------------------------------------------------

    private readonly string[] screenNames =
    {
        "Н-33, Н-34",
        "Р18",
        "Р20",
        "Катковка"
    };

    // ---------------------------------------------------------
    // 9 координат для каждого удалённого ПК
    // ---------------------------------------------------------

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

    // ---------------------------------------------------------
    // Координаты всех 4 экранов
    // ---------------------------------------------------------

    private readonly Dictionary<string, Dictionary<string, Point>> allPoints =
        new();

    // ---------------------------------------------------------
    // Элементы интерфейса
    // ---------------------------------------------------------

    private readonly Label instruction = new();
    private readonly Label mousePosition = new();
    private readonly Label currentScreenLabel = new();

    private readonly TextBox log = new();

    private readonly ComboBox screenSelector = new();

    private readonly Button setupButton = new();
    private readonly Button exportButton = new();
    private readonly Button cancelButton = new();

    private readonly FlowLayoutPanel singleExportPanel =
        new();

    private readonly Button[] singleExportButtons =
    {
        new Button(),
        new Button(),
        new Button(),
        new Button()
    };

    // ---------------------------------------------------------
    // Состояние
    // ---------------------------------------------------------

    private int setupIndex = -1;

    private CancellationTokenSource? exportCancellation;

    private bool exportRunning;

    private string CurrentSetupScreen =>
        screenSelector.SelectedItem?.ToString()
        ?? screenNames[0];

    // ---------------------------------------------------------
    // Файл координат
    // ---------------------------------------------------------

    private string SettingsFile
    {
        get
        {
            string folder = Path.Combine(
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
    // СОЗДАНИЕ ОКНА
    // =========================================================

    public MainForm()
    {
        Text = "BFN Exporter — 4 экрана";

        Width = 900;
        Height = 700;

        StartPosition =
            FormStartPosition.CenterScreen;

        KeyPreview = true;

        KeyDown += MainForm_KeyDown;

        // -----------------------------------------------------
        // Инструкция
        // -----------------------------------------------------

        instruction.Dock = DockStyle.Top;

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
        // Выбранный экран
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
        // Выбор экрана для настройки
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

        screenSelector.SelectedIndexChanged += (_, _) =>
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
        // Кнопка полной выгрузки
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

        exportButton.Click += async (_, _) =>
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

        cancelButton.Click += (_, _) =>
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

        setupButton.Click += (_, _) =>
        {
            StartSetup();
        };

        // -----------------------------------------------------
        // Панель отдельных выгрузок
        // -----------------------------------------------------

        singleExportPanel.Dock =
            DockStyle.Bottom;

        singleExportPanel.Height = 65;

        singleExportPanel.FlowDirection =
            FlowDirection.LeftToRight;

        singleExportPanel.WrapContents =
            false;

        singleExportPanel.Padding =
            new Padding(5);

        singleExportPanel.AutoScroll =
            true;

        for (int i = 0;
             i < singleExportButtons.Length;
             i++)
        {
            int screenIndex = i;

            Button button =
                singleExportButtons[i];

            button.Text =
                $"▶ {screenNames[i]}";

            button.Width = 200;

            button.Height = 50;

            button.Font =
                new Font(
                    "Segoe UI",
                    9,
                    FontStyle.Bold);

            button.Margin =
                new Padding(3);

            button.Click += async (_, _) =>
            {
                await ExportSingleScreen(
                    screenIndex);
            };

            singleExportPanel.Controls.Add(
                button);
        }

        // -----------------------------------------------------
        // Добавляем элементы
        // -----------------------------------------------------

        Controls.Add(log);

        Controls.Add(singleExportPanel);

        Controls.Add(exportButton);

        Controls.Add(cancelButton);

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

        timer.Tick += (_, _) =>
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

        Log(
            "Доступна отдельная выгрузка каждого из 4 экранов.");

        UpdateInstructionForCurrentScreen();

        // -----------------------------------------------------
        // Глобальный ESC
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
    // GLOBAL HOTKEY
    // =========================================================

    protected override void WndProc(
        ref Message m)
    {
        const int WM_HOTKEY = 0x0312;

        if (m.Msg == WM_HOTKEY &&
            m.WParam.ToInt32() == HOTKEY_ESC)
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
    // ЗАКРЫТИЕ ПРОГРАММЫ
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
    // ТЕКУЩИЙ ЭКРАН
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
    // ПОЛУЧИТЬ КООРДИНАТЫ ЭКРАНА
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

    // =========================================================
    // ПРОВЕРКА КООРДИНАТ
    // =========================================================

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
            if (!points.ContainsKey(pointName))
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
            GetOrCreateScreenPoints(screen);

        points.Clear();

        setupIndex = 0;

        setupButton.Enabled = false;

        exportButton.Enabled = false;

        cancelButton.Enabled = true;

        screenSelector.Enabled = false;

        SetSingleExportButtonsEnabled(
            false);

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

    // =========================================================
    // ОТМЕНА НАСТРОЙКИ
    // =========================================================

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

        cancelButton.Enabled = false;

        screenSelector.Enabled = true;

        SetSingleExportButtonsEnabled(
            true);

        instruction.Text =
            "Настройка отменена.";

        Log(
            $"Настройка координат «{screen}» отменена.");
    }

    // =========================================================
    // КЛАВИАТУРА
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
    // СОХРАНЕНИЕ КООРДИНАТЫ
    // =========================================================

    private void CapturePoint()
    {
        if (setupIndex < 0 ||
            setupIndex >= pointNames.Length)
        {
            return;
        }

        if (!GetCursorPos(out POINT p))
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

            SetSingleExportButtonsEnabled(
                true);

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
    // СОХРАНЕНИЕ КООРДИНАТ
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
    // ЗАГРУЗКА КООРДИНАТ
    // =========================================================

    private void LoadCoordinates()
    {
        try
        {
            if (!File.Exists(SettingsFile))
            {
                return;
            }

            string json =
                File.ReadAllText(
                    SettingsFile);

            Dictionary<string,
                Dictionary<string, Point>>? loaded =
                JsonSerializer.Deserialize<
                    Dictionary<string,
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
    // ПРОВЕРКА ОТМЕНЫ
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

    // =========================================================
    // ОЖИДАНИЕ
    // =========================================================

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
    // КЛИК ПО КООРДИНАТЕ
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
    // ЗАМЕНА ИМЕНИ ФАЙЛА
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
    // ЗАПУСК ОТЧЁТА
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
    // СОХРАНЕНИЕ ОТЧЁТА
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
    // ВОЗВРАТ В ИСХОДНОЕ СОСТОЯНИЕ
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
    // ИМЕНА ФАЙЛОВ
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
    // ПРОВЕРКА ВСЕХ КООРДИНАТ
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
    // ПРОВЕРКА КООРДИНАТ ОДНОГО ЭКРАНА
    // =========================================================

    private void ValidateScreenCoordinates(
        string screen)
    {
        if (!HasAllCoordinates(screen))
        {
            throw new Exception(
                $"Для экрана «{screen}» не настроены все 9 координат.");
        }
    }

    // =========================================================
    // ЗАГОЛОВОК КРУГА
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
    // НОВОЕ:
    // ОТДЕЛЬНАЯ ВЫГРУЗКА ОДНОГО УДАЛЁННОГО ПК
    // =========================================================

    private async Task ExportSingleScreen(
        int screenIndex)
    {
        if (exportRunning)
        {
            return;
        }

        if (screenIndex < 0 ||
            screenIndex >= screenNames.Length)
        {
            return;
        }

        string screen =
            screenNames[screenIndex];

        try
        {
            ValidateScreenCoordinates(
                screen);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message +
                "\n\nСначала выбери этот экран и настрой 9 координат.",
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
                $"ОТДЕЛЬНАЯ ВЫГРУЗКА: {screen}");

            Log(
                "========================================");

            // -------------------------------------------------
            // 1. Производственный отчёт
            // -------------------------------------------------

            CheckCancellation(token);

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
            // 2. Дамате клик
            // -------------------------------------------------

            CheckCancellation(token);

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
            // 3. Бункер
            // -------------------------------------------------

            CheckCancellation(token);

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
            // 4. Возврат
            // -------------------------------------------------

            CheckCancellation(token);

            ReturnToStart(
                screen,
                token);

            Log("");

            Log(
                "========================================");

            Log(
                $"ЭКРАН «{screen}» УСПЕШНО ВЫГРУЖЕН");

            Log(
                "========================================");

            MessageBox.Show(
                $"Экран «{screen}» успешно выгружен.\n\n" +
                "Все 3 отчёта сохранены.\n" +
                "Экран возвращён в исходное состояние.",
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
                $"ВЫГРУЗКА «{screen}» ОТМЕНЕНА");

            Log(
                "========================================");

            MessageBox.Show(
                $"Выгрузка экрана «{screen}» отменена.",
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
                $"ОШИБКА ПРИ ВЫГРУЗКЕ «{screen}»");

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
    // ПОЛНАЯ ВЫГРУЗКА ВСЕХ 4 ЭКРАНОВ
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
            // Производственный отчёт
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
            // Сохранение Производственного отчёта
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
            // Запуск Damate Qlik
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
            // Сохранение Damate Qlik
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
            // Запуск Бункера
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
            // Сохранение Бункера
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
            // Возврат всех экранов
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
    // ОТМЕНА ВЫГРУЗКИ
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
    // СОСТОЯНИЕ КНОПОК ОТДЕЛЬНОЙ ВЫГРУЗКИ
    // =========================================================

    private void SetSingleExportButtonsEnabled(
        bool enabled)
    {
        foreach (Button button in singleExportButtons)
        {
            button.Enabled =
                enabled;
        }
    }

    // =========================================================
    // СОСТОЯНИЕ ИНТЕРФЕЙСА
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

        SetSingleExportButtonsEnabled(
            !running);

        if (running)
        {
            instruction.Text =
                "Выполняется выгрузка. Для отмены нажми ESC.";
        }
        else
        {
            UpdateInstructionForCurrentScreen();
        }
    }

    // =========================================================
    // ЖУРНАЛ
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
