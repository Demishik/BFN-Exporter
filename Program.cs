using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Collections.Generic;
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

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    private readonly string[] pointNames =
    {
        "Отчет Производство",
        "Damate Qlik",
        "Отчет Бункер",
        "Файл",
        "Экспорт",
        "Поле имени файла",
        "Сохранить"
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

            return Path.Combine(folder, "coordinates.json");
        }
    }

    public MainForm()
    {
        Text = "BFN Exporter — ПК №1";
        Width = 760;
        Height = 520;
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;

        instruction.Dock = DockStyle.Top;
        instruction.Height = 75;
        instruction.Font = new Font("Segoe UI", 12);
        instruction.TextAlign = ContentAlignment.MiddleCenter;

        mousePosition.Dock = DockStyle.Top;
        mousePosition.Height = 40;
        mousePosition.Font = new Font("Segoe UI", 11);
        mousePosition.TextAlign = ContentAlignment.MiddleCenter;

        log.Multiline = true;
        log.ReadOnly = true;
        log.Dock = DockStyle.Fill;
        log.ScrollBars = ScrollBars.Vertical;

        exportButton.Text = "▶ Запустить экспорт";
        exportButton.Dock = DockStyle.Bottom;
        exportButton.Height = 50;
        exportButton.Click += (_, _) => TestExport();

        setupButton.Text = "⚙ Настроить координаты";
        setupButton.Dock = DockStyle.Bottom;
        setupButton.Height = 50;
        setupButton.Click += (_, _) => StartSetup();

        Controls.Add(log);
        Controls.Add(exportButton);
        Controls.Add(setupButton);
        Controls.Add(mousePosition);
        Controls.Add(instruction);

        KeyDown += MainForm_KeyDown;

        Timer timer = new Timer();
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

        if (points.Count == pointNames.Length)
        {
            instruction.Text =
                "Координаты уже настроены. Можно запускать тест.";
        }
        else
        {
            instruction.Text =
                "Нажми «Настроить координаты».";
        }

        Log("BFN Exporter запущен.");
    }

    private void MainForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.F8 && setupIndex >= 0)
        {
            e.SuppressKeyPress = true;
            CapturePoint();
        }

        if (e.KeyCode == Keys.Escape && setupIndex >= 0)
        {
            setupIndex = -1;

            setupButton.Enabled = true;
            exportButton.Enabled = true;

            instruction.Text = "Настройка отменена.";

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

        Log("Начинаем настройку координат.");
        Log("F8 — зафиксировать точку.");
        Log("ESC — отменить настройку.");
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

        string name = pointNames[setupIndex];

        points[name] = new Point(p.X, p.Y);

        Log($"{name}: X={p.X}, Y={p.Y}");

        setupIndex++;

        if (setupIndex >= pointNames.Length)
        {
            SaveCoordinates();

            setupIndex = -1;

            setupButton.Enabled = true;
            exportButton.Enabled = true;

            instruction.Text =
                "Готово! Все координаты сохранены.";

            Log("Все 7 координат сохранены.");
            Log("Теперь можно нажать «Запустить экспорт».");

            return;
        }

        instruction.Text =
            $"Наведи мышь на «{pointNames[setupIndex]}» и нажми F8.";
    }

    private void SaveCoordinates()
    {
        string json = JsonSerializer.Serialize(
            points,
            new JsonSerializerOptions
            {
                WriteIndented = true
            });

        File.WriteAllText(SettingsFile, json);
    }

    private void LoadCoordinates()
    {
        try
        {
            if (!File.Exists(SettingsFile))
            {
                return;
            }

            string json = File.ReadAllText(SettingsFile);

            Dictionary<string, Point>? loaded =
                JsonSerializer.Deserialize<
                    Dictionary<string, Point>>(json);

            if (loaded == null)
            {
                return;
            }

            foreach (var item in loaded)
            {
                points[item.Key] = item.Value;
            }

            Log("Сохранённые координаты загружены.");
        }
        catch (Exception ex)
        {
            Log("Не удалось загрузить координаты: " +
                ex.Message);
        }
    }

    private void TestExport()
    {
        if (points.Count != pointNames.Length)
        {
            MessageBox.Show(
                "Сначала настрой координаты.",
                "BFN Exporter",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            return;
        }

        DateTime today = DateTime.Today;
        DateTime yesterday = today.AddDays(-1);

        string folder = Path.Combine(
            @"C:\Отчеты",
            today.ToString("yyyy_MM"));

        string production =
            $"{yesterday:yyyy_MM_dd} " +
            $"Катковка Р-3 Производственный отчет.xlsx";

        string damate =
            $"{yesterday:yyyy_MM_dd} " +
            $"Катковка Р-3 Damate qlik.xlsx";

        string bunker =
            $"{today:yyyy_MM_dd} " +
            $"Катковка Р-3 Остаток корма на 00-00.xlsx";

        Log("");
        Log("=== ПРОВЕРКА ===");
        Log($"Папка: {folder}");
        Log($"Производство: {production}");
        Log($"Damate Qlik: {damate}");
        Log($"Бункер: {bunker}");
        Log("");
        Log("Координаты:");

        foreach (string name in pointNames)
        {
            if (points.TryGetValue(name, out Point p))
            {
                Log($"{name}: X={p.X}, Y={p.Y}");
            }
        }

        Log("");
        Log("Проверка завершена.");
        Log("Реальные клики BigFarmNet пока НЕ выполняются.");
    }

    private void Log(string text)
    {
        log.AppendText(
            $"{DateTime.Now:HH:mm:ss}  {text}" +
            Environment.NewLine);
    }
}
