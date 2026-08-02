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

    private readonly string[] screenNames =
    {
        "Н33/Н34",
        "Р18",
        "Р20",
        "Катковка"
    };

    private readonly string[] pointNames =
    {
        "Отчет Производство",
        "Damate Qlik",
        "Отчет Бункер",
        "Файл",
        "Экспорт",
        "Поле имени файла",
        "Сохранить",
        "Нет"
    };

    private readonly Dictionary<string, Dictionary<string, Point>> allPoints = new();

    private readonly Label instruction = new();
    private readonly Label mousePosition = new();
    private readonly Label currentScreenLabel = new();
    private readonly TextBox log = new();

    private readonly ComboBox screenSelector = new();

    private readonly Button setupButton = new();
    private readonly Button exportButton = new();
    private readonly Button cancelButton = new();

    private int setupIndex = -1;

    private CancellationTokenSource? exportCancellation;
    private bool exportRunning;

    private string CurrentSetupScreen =>
        screenSelector.SelectedItem?.ToString()
        ?? screenNames[0];

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

    public MainForm()
    {
        Text = "BFN Exporter — 4 экрана";
        Width = 820;
        Height = 650;
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;

        instruction.Dock = DockStyle.Top;
        instruction.Height = 65;
        instruction.Font = new Font("Segoe UI", 12);
        instruction.TextAlign = ContentAlignment.MiddleCenter;

        mousePosition.Dock = DockStyle.Top;
        mousePosition.Height = 35;
        mousePosition.Font = new Font("Segoe UI", 10);
        mousePosition.TextAlign = ContentAlignment.MiddleCenter;

        currentScreenLabel.Dock = DockStyle.Top;
        currentScreenLabel.Height = 35;
        currentScreenLabel.Font = new Font("Segoe UI", 11, FontStyle.Bold);
        currentScreenLabel.TextAlign = ContentAlignment.MiddleCenter;

        screenSelector.Dock = DockStyle.Top;
        screenSelector.Height = 35;
        screenSelector.DropDownStyle = ComboBoxStyle.DropDownList;

        foreach (string screen in screenNames)
        {
            screenSelector.Items.Add(screen);
        }

        screenSelector.SelectedIndex = 0;

        screenSelector.SelectedIndexChanged += (_, _) =>
        {
            UpdateCurrentScreenLabel();

            if (setupIndex < 0 && !exportRunning)
            {
                UpdateInstructionForCurrentScreen();
            }
        };

        log.Multiline = true;
        log.ReadOnly = true;
        log.Dock = DockStyle.Fill;
        log.ScrollBars = ScrollBars.Vertical;

        exportButton.Text =
            "▶ ВЫГРУЗИТЬ ВСЕ 4 ЭКРАНА";

        exportButton.Dock = DockStyle.Bottom;
        exportButton.Height = 55;
        exportButton.Font = new Font(
            "Segoe UI",
            11,
            FontStyle.Bold);

        exportButton.Click += async (_, _) =>
        {
            await ExportAllScreens();
        };

        cancelButton.Text =
            "■ ОТМЕНА  (ESC)";

        cancelButton.Dock = DockStyle.Bottom;
        cancelButton.Height = 50;
        cancelButton.Enabled = false;
        cancelButton.Font = new Font(
            "Segoe UI",
            10,
            FontStyle.Bold);

        cancelButton.Click += (_, _) =>
        {
            CancelExport();
        };

        setupButton.Text =
            "⚙ Настроить координаты выбранного экрана";

        setupButton.Dock = DockStyle.Bottom;
        setupButton.Height = 50;

        setupButton.Click += (_, _) =>
        {
            StartSetup();
        };

        Controls.Add(log);
        Controls.Add(exportButton);
        Controls.Add(cancelButton);
        Controls.Add(setupButton);
        Controls.Add(screenSelector);
        Controls.Add(mousePosition);
        Controls.Add(currentScreenLabel);
        Controls.Add(instruction);

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

        LoadCoordinates();

        UpdateCurrentScreenLabel();

        Log(
            "BFN Exporter запущен.");

        Log(
            "Настройка выполняется отдельно для каждого экрана.");

        UpdateInstructionForCurrentScreen();

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

    protected override void WndProc(ref Message m)
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

    protected override void OnFormClosed(FormClosedEventArgs e)
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

    private void UpdateCurrentScreenLabel()
    {
        currentScreenLabel.Text =
            $"Выбранный экран: {CurrentSetupScreen}";
    }

    private void UpdateInstructionForCurrentScreen()
    {
        if (HasAllCoordinates(CurrentSetupScreen))
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

    private Dictionary<string, Point> GetOrCreateScreenPoints(
        string screen)
    {
        if (!allPoints.TryGetValue(
            screen,
            out Dictionary<string, Point>? points))
        {
            points = new Dictionary<string, Point>();

            allPoints[screen] = points;
        }

        return points;
    }

    private bool HasAllCoordinates(string screen)
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

    private void StartSetup()
    {
        if (exportRunning)
        {
            return;
        }

        string screen = CurrentSetupScreen;

        Dictionary<string, Point> points =
            GetOrCreateScreenPoints(screen);

        points.Clear();

        setupIndex = 0;

        setupButton.Enabled = false;
        exportButton.Enabled = false;
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

        string screen = CurrentSetupScreen;

        setupIndex = -1;

        GetOrCreateScreenPoints(screen).Clear();

        setupButton.Enabled = true;
        exportButton.Enabled = true;
        cancelButton.Enabled = false;
        screenSelector.Enabled = true;

        instruction.Text =
            "Настройка отменена.";

        Log(
            $"Настройка координат «{screen}» отменена.");
    }

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

        string screen = CurrentSetupScreen;

        Dictionary<string, Point> points =
            GetOrCreateScreenPoints(screen);

        string name =
            pointNames[setupIndex];

        points[name] =
            new Point(p.X, p.Y);

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

            instruction.Text =
                $"Готово! 8 координат для «{screen}» сохранены.";

            Log(
                $"Все 8 координат для «{screen}» сохранены.");

            return;
        }

        instruction.Text =
            $"«{screen}»: наведи мышь на «{pointNames[setupIndex]}» и нажми F8.";
    }

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

            Dictionary<string, Dictionary<string, Point>>? loaded =
                JsonSerializer.Deserialize<
                    Dictionary<string, Dictionary<string, Point>>>(
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
                        $"{screen}: 8 координат загружено.");
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

        if (token.WaitHandle.WaitOne(milliseconds))
        {
            throw new OperationCanceledException(
                token);
        }
    }

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

        Wait(500, token);

        CheckCancellation(token);

        mouse_event(
            MOUSEEVENTF_LEFTDOWN,
            0,
            0,
            0,
            UIntPtr.Zero);

        Wait(100, token);

        mouse_event(
            MOUSEEVENTF_LEFTUP,
            0,
            0,
            0,
            UIntPtr.Zero);

        Wait(1000, token);
    }

    private void ReplaceFileName(
        string fileName,
        CancellationToken token)
    {
        CheckCancellation(token);

        Log(
            "Очищаем старое имя файла.");

        SendKeys.SendWait(
            "{HOME}");

        Wait(300, token);

        SendKeys.SendWait(
            "+{END}");

        Wait(300, token);

        SendKeys.SendWait(
            "{BACKSPACE}");

        Wait(500, token);

        Log(
            "Старое имя удалено.");

        CheckCancellation(token);

        Log(
            "Вводим имя файла.");

        SendKeys.SendWait(
            fileName);

        Wait(1000, token);

        Log(
            $"Новое имя введено: {fileName}");
    }

    private void ExportSingleReport(
        string screen,
        string reportPoint,
        string fileName,
        CancellationToken token)
    {
        CheckCancellation(token);

        Log("");
        Log(
            $"--- {screen}: начинаем {reportPoint} ---");

        Log(
            $"Имя файла: {fileName}");

        ClickPoint(
            screen,
            reportPoint,
            token);

        Wait(1000, token);

        ClickPoint(
            screen,
            "Файл",
            token);

        Wait(500, token);

        ClickPoint(
            screen,
            "Экспорт",
            token);

        Wait(2000, token);

        ClickPoint(
            screen,
            "Поле имени файла",
            token);

        Wait(500, token);

        ReplaceFileName(
            fileName,
            token);

        Wait(500, token);

        ClickPoint(
            screen,
            "Сохранить",
            token);

        Wait(2000, token);

        ClickPoint(
            screen,
            "Нет",
            token);

        Wait(1500, token);

        Log(
            $"Готово: {fileName}");
    }

    private void ExportScreen(
        string screen,
        CancellationToken token)
    {
        CheckCancellation(token);

        if (!HasAllCoordinates(screen))
        {
            throw new Exception(
                $"Для экрана «{screen}» не настроены все 8 координат.");
        }

        Log("");
        Log(
            "================================");
        Log(
            $"НАЧАЛО ВЫГРУЗКИ: {screen}");
        Log(
            "================================");

        DateTime today =
            DateTime.Today;

        DateTime yesterday =
            today.AddDays(-1);

        string productionFile =
            $"{yesterday:yyyy_MM_dd} " +
            $"{screen} " +
            "Производственный отчет.xlsx";

        ExportSingleReport(
            screen,
            "Отчет Производство",
            productionFile,
            token);

        Wait(2000, token);

        string damateFile =
            $"{yesterday:yyyy_MM_dd} " +
            $"{screen} " +
            "Дамате клик.xlsx";

        ExportSingleReport(
            screen,
            "Damate Qlik",
            damateFile,
            token);

        Wait(2000, token);

        string bunkerFile =
            $"{today:yyyy_MM_dd} " +
            $"{screen} " +
            "Остаток корма на 00-00.xlsx";

        ExportSingleReport(
            screen,
            "Отчет Бункер",
            bunkerFile,
            token);

        Log("");
        Log(
            $"=== {screen}: ВСЕ 3 ОТЧЁТА ВЫГРУЖЕНЫ ===");
    }

    private async Task ExportAllScreens()
    {
        if (exportRunning)
        {
            return;
        }

        foreach (string screen in screenNames)
        {
            if (!HasAllCoordinates(screen))
            {
                MessageBox.Show(
                    $"Для экрана «{screen}» ещё не настроены все 8 координат.\n\n" +
                    "Сначала выбери этот экран в списке и нажми «Настроить координаты».",
                    "BFN Exporter",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }
        }

        exportRunning = true;

        exportCancellation =
            new CancellationTokenSource();

        CancellationToken token =
            exportCancellation.Token;

        SetRunningState(true);

        try
        {
            Log("");
            Log(
                "========================================");
            Log(
                "НАЧАЛО ВЫГРУЗКИ ВСЕХ 4 ЭКРАНОВ");
            Log(
                "========================================");

            foreach (string screen in screenNames)
            {
                CheckCancellation(token);

                Log("");
                Log(
                    $"ПЕРЕХОД К ЭКРАНУ: {screen}");

                ExportScreen(
                    screen,
                    token);
            }

            Log("");
            Log(
                "========================================");
            Log(
                "ВСЕ 4 ЭКРАНА УСПЕШНО ВЫГРУЖЕНЫ");
            Log(
                "========================================");

            MessageBox.Show(
                "Все 4 экрана успешно выгружены.",
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

    private void SetRunningState(bool running)
    {
        if (InvokeRequired)
        {
            BeginInvoke(
                new Action<bool>(
                    SetRunningState),
                running);

            return;
        }

        exportButton.Enabled = !running;
        setupButton.Enabled = !running;
        cancelButton.Enabled = running;
        screenSelector.Enabled = !running;

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

    private void Log(string text)
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
