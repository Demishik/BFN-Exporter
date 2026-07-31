using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;
using System.Threading;
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
    private static extern uint SendInput(
        uint nInputs,
        INPUT[] pInputs,
        int cbSize);

    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;

    private const uint INPUT_KEYBOARD = 1;

    private const uint KEYEVENTF_UNICODE = 0x0004;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    private const ushort VK_SHIFT = 0x10;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

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

    private readonly Dictionary<string, Point> points = new();

    private readonly Label instruction = new();
    private readonly Label mousePosition = new();
    private readonly TextBox log = new();
    private readonly Button setupButton = new();
    private readonly Button exportButton = new();

    private int setupIndex = -1;

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
        Text = "BFN Exporter — ПК №1";
        Width = 760;
        Height = 520;

        StartPosition =
            FormStartPosition.CenterScreen;

        KeyPreview = true;

        instruction.Dock = DockStyle.Top;
        instruction.Height = 75;
        instruction.Font =
            new Font("Segoe UI", 12);
        instruction.TextAlign =
            ContentAlignment.MiddleCenter;

        mousePosition.Dock =
            DockStyle.Top;
        mousePosition.Height = 40;
        mousePosition.Font =
            new Font("Segoe UI", 11);
        mousePosition.TextAlign =
            ContentAlignment.MiddleCenter;

        log.Multiline = true;
        log.ReadOnly = true;
        log.Dock = DockStyle.Fill;
        log.ScrollBars =
            ScrollBars.Vertical;

        exportButton.Text =
            "▶ Запустить экспорт";
        exportButton.Dock =
            DockStyle.Bottom;
        exportButton.Height = 50;
        exportButton.Click +=
            (_, _) => ProductionExport();

        setupButton.Text =
            "⚙ Настроить координаты";
        setupButton.Dock =
            DockStyle.Bottom;
        setupButton.Height = 50;
        setupButton.Click +=
            (_, _) => StartSetup();

        Controls.Add(log);
        Controls.Add(exportButton);
        Controls.Add(setupButton);
        Controls.Add(mousePosition);
        Controls.Add(instruction);

        KeyDown += MainForm_KeyDown;

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

        if (points.Count ==
            pointNames.Length)
        {
            instruction.Text =
                "Координаты загружены. Можно запускать экспорт.";
        }
        else
        {
            instruction.Text =
                "Нажми «Настроить координаты».";
        }

        Log("BFN Exporter запущен.");
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

        if (e.KeyCode == Keys.Escape &&
            setupIndex >= 0)
        {
            setupIndex = -1;

            setupButton.Enabled = true;
            exportButton.Enabled = true;

            instruction.Text =
                "Настройка отменена.";

            Log("Настройка отменена.");
        }
    }

    private void StartSetup()
    {
        points.Clear();

        setupIndex = 0;

        setupButton.Enabled = false;
        exportButton.Enabled = false;

        instruction.Text =
            $"Наведи мышь на «{pointNames[0]}» и нажми F8.";

        Log("Настройка координат начата.");
        Log("F8 — сохранить текущую позицию мыши.");
        Log("ESC — отменить.");
    }

    private void CapturePoint()
    {
        if (setupIndex < 0 ||
            setupIndex >= pointNames.Length)
            return;

        if (!GetCursorPos(out POINT p))
            return;

        string name =
            pointNames[setupIndex];

        points[name] =
            new Point(p.X, p.Y);

        Log(
            $"{name}: X={p.X}, Y={p.Y}");

        setupIndex++;

        if (setupIndex >=
            pointNames.Length)
        {
            SaveCoordinates();

            setupIndex = -1;

            setupButton.Enabled = true;
            exportButton.Enabled = true;

            instruction.Text =
                "Готово! 8 координат сохранены.";

            Log(
                "Все 8 координат сохранены.");

            return;
        }

        instruction.Text =
            $"Наведи мышь на «{pointNames[setupIndex]}» и нажми F8.";
    }

    private void SaveCoordinates()
    {
        string json =
            JsonSerializer.Serialize(
                points,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });

        File.WriteAllText(
            SettingsFile,
            json);
    }

    private void LoadCoordinates()
    {
        try
        {
            if (!File.Exists(SettingsFile))
                return;

            string json =
                File.ReadAllText(SettingsFile);

            Dictionary<string, Point>? loaded =
                JsonSerializer.Deserialize<
                    Dictionary<string, Point>>(
                        json);

            if (loaded == null)
                return;

            foreach (var item in loaded)
            {
                points[item.Key] =
                    item.Value;
            }

            Log("Координаты загружены.");
        }
        catch (Exception ex)
        {
            Log(
                "Ошибка загрузки координат: " +
                ex.Message);
        }
    }

    private void ClickPoint(string name)
    {
        if (!points.TryGetValue(
            name,
            out Point p))
        {
            throw new Exception(
                $"Нет координаты: {name}");
        }

        Log(
            $"Клик: {name} ({p.X},{p.Y})");

        Cursor.Position = p;

        Thread.Sleep(400);

        mouse_event(
            MOUSEEVENTF_LEFTDOWN,
            0,
            0,
            0,
            UIntPtr.Zero);

        Thread.Sleep(100);

        mouse_event(
            MOUSEEVENTF_LEFTUP,
            0,
            0,
            0,
            UIntPtr.Zero);

        Thread.Sleep(800);
    }

    private void SendUnicodeText(string text)
    {
        foreach (char c in text)
        {
            ushort code = c;

            INPUT down = new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = 0,
                        wScan = code,
                        dwFlags = KEYEVENTF_UNICODE,
                        time = 0,
                        dwExtraInfo = UIntPtr.Zero
                    }
                }
            };

            INPUT up = new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = 0,
                        wScan = code,
                        dwFlags =
                            KEYEVENTF_UNICODE |
                            KEYEVENTF_KEYUP,
                        time = 0,
                        dwExtraInfo = UIntPtr.Zero
                    }
                }
            };

            SendInput(
                1,
                new[] { down },
                Marshal.SizeOf<INPUT>());

            SendInput(
                1,
                new[] { up },
                Marshal.SizeOf<INPUT>());

            Thread.Sleep(10);
        }
    }

    private void ReplaceFileName(string fileName)
    {
        Log("Очищаем старое имя файла.");

        // Переходим в начало поля.
        SendKeys.SendWait("{HOME}");

        Thread.Sleep(300);

        // Выделяем всё содержимое.
        SendKeys.SendWait("+{END}");

        Thread.Sleep(300);

        // Удаляем старое имя.
        SendKeys.SendWait("{BACKSPACE}");

        Thread.Sleep(500);

        Log("Старое имя удалено.");

        // Вводим новое имя напрямую
        // через Windows Unicode input.
        SendUnicodeText(fileName);

        Thread.Sleep(1000);

        Log("Новое имя введено.");
    }

    private void ProductionExport()
    {
        if (points.Count !=
            pointNames.Length)
        {
            MessageBox.Show(
                "Нужно настроить все 8 координат.",
                "BFN Exporter",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            return;
        }

        exportButton.Enabled = false;
        setupButton.Enabled = false;

        try
        {
            Log("");
            Log(
                "=== НАЧАЛО ЭКСПОРТА ПРОИЗВОДСТВА ===");

            DateTime today =
                DateTime.Today;

            DateTime yesterday =
                today.AddDays(-1);

            string folder =
                Path.Combine(
                    @"C:\Отчеты",
                    today.ToString("yyyy_MM"));

            string fileName =
                $"{yesterday:yyyy_MM_dd} " +
                "Катковка Р-3 " +
                "Производственный отчет.xlsx";

            Log(
                $"Папка: {folder}");

            Log(
                $"Имя файла: {fileName}");

            // Отчет Производство
            ClickPoint(
                "Отчет Производство");

            // Файл
            ClickPoint("Файл");

            // Экспорт
            ClickPoint("Экспорт");

            // Ждём окно сохранения.
            Thread.Sleep(2000);

            // Поле имени файла.
            ClickPoint(
                "Поле имени файла");

            Thread.Sleep(500);

            // Заменяем старое имя.
            ReplaceFileName(
                fileName);

            // Сохранить.
            ClickPoint(
                "Сохранить");

            // Ждём окно после сохранения.
            Thread.Sleep(1800);

            // Нет.
            ClickPoint("Нет");

            Thread.Sleep(1000);

            Log(
                "Производственный отчёт сохранён.");

            Log(
                "Окно просмотра закрыто кнопкой «Нет».");

            Log(
                "=== ЭКСПОРТ ЗАВЕРШЁН ===");

            MessageBox.Show(
                "Производственный отчёт сохранён.",
                "BFN Exporter",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            Log(
                "ОШИБКА: " +
                ex.Message);

            MessageBox.Show(
                ex.Message,
                "Ошибка BFN Exporter",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            exportButton.Enabled = true;
            setupButton.Enabled = true;
        }
    }

    private void Log(string text)
    {
        log.AppendText(
            $"{DateTime.Now:HH:mm:ss}  " +
            $"{text}" +
            Environment.NewLine);
    }
}
