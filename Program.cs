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

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
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
        ClientSize = new Size(980, 760);
        MinimumSize = new Size(900, 700);
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;
        BackColor = Color.FromArgb(18, 24, 32);
        ForeColor = Color.FromArgb(235, 240, 245);

        KeyDown += MainForm_KeyDown;

        // -----------------------------------------------------
        // Общий стиль
        // -----------------------------------------------------
        Font normalFont = new Font("Segoe UI", 10);
        Font boldFont = new Font("Segoe UI", 10, FontStyle.Bold);

        instruction.Dock = DockStyle.Top;
        instruction.Height = 52;
        instruction.Font = new Font("Segoe UI", 12, FontStyle.Bold);
        instruction.ForeColor = Color.FromArgb(235, 240, 245);
        instruction.BackColor = Color.FromArgb(18, 24, 32);
        instruction.TextAlign = ContentAlignment.MiddleCenter;

        currentScreenLabel.Dock = DockStyle.Top;
        currentScreenLabel.Height = 34;
        currentScreenLabel.Font = boldFont;
        currentScreenLabel.ForeColor = Color.FromArgb(96, 165, 250);
        currentScreenLabel.BackColor = Color.FromArgb(18, 24, 32);
        currentScreenLabel.TextAlign = ContentAlignment.MiddleCenter;

        mousePosition.Dock = DockStyle.Top;
        mousePosition.Height = 30;
        mousePosition.Font = normalFont;
        mousePosition.ForeColor = Color.FromArgb(180, 190, 200);
        mousePosition.BackColor = Color.FromArgb(18, 24, 32);
        mousePosition.TextAlign = ContentAlignment.MiddleCenter;

        screenSelector.Dock = DockStyle.Top;
        screenSelector.Height = 32;
        screenSelector.DropDownStyle = ComboBoxStyle.DropDownList;
        screenSelector.BackColor = Color.FromArgb(31, 41, 55);
        screenSelector.ForeColor = Color.White;

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
        // Журнал
        // -----------------------------------------------------
        log.Multiline = true;
        log.ReadOnly = true;
        log.Dock = DockStyle.Fill;
        log.ScrollBars = ScrollBars.Vertical;
        log.BackColor = Color.FromArgb(10, 15, 22);
        log.ForeColor = Color.FromArgb(220, 230, 240);
        log.BorderStyle = BorderStyle.FixedSingle;
        log.Font = new Font("Consolas", 9);

        // -----------------------------------------------------
        // Основная кнопка
        // -----------------------------------------------------
        StyleButton(
            exportButton,
            "▶  ВЫГРУЗИТЬ ВСЕ 4 ЭКРАНА",
            Color.FromArgb(37, 99, 235),
            Color.White,
            12);

        exportButton.Click += async (_, _) =>
        {
            await ExportAllScreens();
        };

        // -----------------------------------------------------
        // Отдельный отчёт по разряжению
        // -----------------------------------------------------
        StyleButton(
            dischargeExportButton,
            "▶  ВЫГРУЗИТЬ РАЗРЯЖЕНИЕ — 4 ЭКРАНА",
            Color.FromArgb(124, 58, 237),
            Color.White,
            11);

        dischargeExportButton.Click += async (_, _) =>
        {
            await ExportDischargeAllScreens();
        };

        // -----------------------------------------------------
        // Четыре отдельных экрана
        // -----------------------------------------------------
        exportScreen1Button.Text = "Н-33, Н-34";
        exportScreen2Button.Text = "Р-18";
        exportScreen3Button.Text = "Р-20";
        exportScreen4Button.Text = "Катковка";

        Button[] screenButtons =
        {
            exportScreen1Button,
            exportScreen2Button,
            exportScreen3Button,
            exportScreen4Button
        };

        foreach (Button button in screenButtons)
        {
            StyleButton(
                button,
                button.Text,
                Color.FromArgb(31, 41, 55),
                Color.FromArgb(235, 240, 245),
                9);
        }

        exportScreen1Button.Click += async (_, _) =>
            await ExportSingleScreen("Н-33, Н-34");

        exportScreen2Button.Click += async (_, _) =>
            await ExportSingleScreen("Р18");

        exportScreen3Button.Click += async (_, _) =>
            await ExportSingleScreen("Р20");

        exportScreen4Button.Click += async (_, _) =>
            await ExportSingleScreen("Катковка");

        // -----------------------------------------------------
        // Служебные кнопки — возвращаем их в интерфейс
        // -----------------------------------------------------
        StyleButton(
            setupButton,
            "⚙  КООРДИНАТЫ",
            Color.FromArgb(55, 65, 81),
            Color.White,
            9);

        setupButton.Click += (_, _) => StartSetup();

        StyleButton(
            dischargeSetupButton,
            "⚙  РАЗРЯЖЕНИЕ",
            Color.FromArgb(76, 29, 149),
            Color.White,
            9);

        dischargeSetupButton.Click += (_, _) => StartDischargeSetup();

        StyleButton(
            foldersButton,
            "📁  ПАПКИ",
            Color.FromArgb(55, 65, 81),
            Color.White,
            9);

        foldersButton.Click += (_, _) => ConfigureTransferFolders();

        StyleButton(
            transferButton,
            "⇄  ПЕРЕСЛАТЬ ОТЧЁТЫ",
            Color.FromArgb(55, 65, 81),
            Color.White,
            9);

        transferButton.Click += (_, _) => StartTransferTest();

        StyleButton(
            cancelButton,
            "■  ОТМЕНА (ESC)",
            Color.FromArgb(127, 29, 29),
            Color.White,
            9);

        cancelButton.Enabled = false;
        cancelButton.Click += (_, _) => CancelExport();

        // -----------------------------------------------------
        // Нижняя панель
        // -----------------------------------------------------
        TableLayoutPanel bottomPanel =
            new TableLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 235,
                ColumnCount = 4,
                RowCount = 4,
                Padding = new Padding(6),
                BackColor = Color.FromArgb(18, 24, 32)
            };

        for (int i = 0; i < 4; i++)
        {
            bottomPanel.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Percent,
                    25));
        }

        bottomPanel.RowStyles.Add(
            new RowStyle(
                SizeType.Absolute,
                48));

        bottomPanel.RowStyles.Add(
            new RowStyle(
                SizeType.Absolute,
                48));

        bottomPanel.RowStyles.Add(
            new RowStyle(
                SizeType.Absolute,
                48));

        bottomPanel.RowStyles.Add(
            new RowStyle(
                SizeType.Absolute,
                70));

        bottomPanel.Controls.Add(
            exportButton,
            0,
            0);

        bottomPanel.SetColumnSpan(
            exportButton,
            4);

        bottomPanel.Controls.Add(
            dischargeExportButton,
            0,
            1);

        bottomPanel.SetColumnSpan(
            dischargeExportButton,
            4);

        // Все четыре кнопки должны быть в одной строке.
        bottomPanel.Controls.Add(
            exportScreen1Button,
            0,
            2);

        bottomPanel.Controls.Add(
            exportScreen2Button,
            1,
            2);

        bottomPanel.Controls.Add(
            exportScreen3Button,
            2,
            2);

        bottomPanel.Controls.Add(
            exportScreen4Button,
            3,
            2);

        TableLayoutPanel servicePanel =
            new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 5,
                RowCount = 1,
                BackColor = Color.FromArgb(18, 24, 32)
            };

        for (int i = 0; i < 5; i++)
        {
            servicePanel.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Percent,
                    20));
        }

        servicePanel.Controls.Add(setupButton, 0, 0);
        servicePanel.Controls.Add(dischargeSetupButton, 1, 0);
        servicePanel.Controls.Add(foldersButton, 2, 0);
        servicePanel.Controls.Add(transferButton, 3, 0);
        servicePanel.Controls.Add(cancelButton, 4, 0);

        bottomPanel.Controls.Add(
            servicePanel,
            0,
            3);

        bottomPanel.SetColumnSpan(
            servicePanel,
            4);

        // -----------------------------------------------------
        // Порядок размещения
        // -----------------------------------------------------
        Controls.Add(log);
        Controls.Add(bottomPanel);
        Controls.Add(screenSelector);
        Controls.Add(mousePosition);
        Controls.Add(currentScreenLabel);
        Controls.Add(instruction);

        // Загружаем настройки уже после создания всех элементов.
        LoadCoordinates();
        LoadTransferSettings();
        ApplyFixedKatkovkaCoordinates();

        UpdateCurrentScreenLabel();
        UpdateInstructionForCurrentScreen();
    }


    private static void StyleButton(
        Button button,
        string text,
        Color backColor,
        Color foreColor,
        float fontSize)
    {
        button.Text = text;
        button.Dock = DockStyle.Fill;
        button.Margin = new Padding(3);
        button.Font = new Font(
            "Segoe UI",
            fontSize,
            FontStyle.Bold);
        button.BackColor = backColor;
        button.ForeColor = foreColor;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor =
            ControlPaint.Light(backColor, 0.15f);
        button.FlatAppearance.MouseDownBackColor =
            ControlPaint.Dark(backColor, 0.10f);
        button.UseVisualStyleBackColor = false;
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

        points.Clear();

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
            $"Нужно сохранить {pointNames.Length} координат.");
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
            GetOrCreateScreenPoints(
                screen).Clear();
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
                $"Готово! 9 координат для «{screen}» сохранены.";

            Log(
                $"Все 9 координат для «{screen}» сохранены.");

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

    private void ApplyFixedKatkovkaCoordinates()
    {
        Dictionary<string, Point> points =
            GetOrCreateScreenPoints("Катковка");

        points["Файл"] = new Point(117, 86);
        points["Экспорт"] = new Point(142, 107);
        points["Вернуться"] = new Point(350, 202);

        Log("Катковка: зафиксированы координаты Файл=117,86; Экспорт=142,107; Вернуться=350,202.");
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

    private string DischargeFile(
        string screen,
        DateTime today)
    {
        return
            $"{today:yyyy_MM_dd} " +
            $"{screen} " +
            "Разряжение на 04-00.xlsx";
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
