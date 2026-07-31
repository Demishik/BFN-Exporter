using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
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

    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;

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
            "▶ Запустить экспорт 3 отчётов";

        exportButton.Dock =
            DockStyle.Bottom;

        exportButton.Height = 50;

        exportButton.Click +=
            (_, _) => ExportAllReports();

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

        if (points.Count == pointNames.Length)
        {
            instruction.Text =
                "Координаты загружены. Можно запускать экспорт.";
        }
        else
        {
            instruction.Text =
                "Нажми «Настроить координаты».";
        }

        Log("BFN Exporter ПК №1 запущен.");
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
        {
            return;
        }

        if (!GetCursorPos(out POINT p))
        {
            return;
        }

        string name =
            pointNames[setupIndex];

        points[name] =
            new Point(p.X, p.Y);

        Log(
            $"{name}: X={p.X}, Y={p.Y}");

        setupIndex++;

        if (setupIndex >= pointNames.Length)
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
            {
                return;
            }

            string json =
                File.ReadAllText(
                    SettingsFile);

            Dictionary<string, Point>? loaded =
                JsonSerializer.Deserialize<
                    Dictionary<string, Point>>(
                        json);

            if (loaded == null)
            {
                return;
            }

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

        Thread.Sleep(500);

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

        Thread.Sleep(1000);
    }

    // -------------------------------------------------
    // ВВОД НОВОГО ИМЕНИ
    // -------------------------------------------------
    //
    // Старое имя удаляем проверенным способом:
    //
    // HOME -> SHIFT+END -> BACKSPACE
    //
    // Новое имя вводим по частям.
    // Это позволяет корректно обработать
    // английское "Damate qlik".
    //
    private void ReplaceFileName(string fileName)
    {
        Log("Очищаем старое имя файла.");

        // В начало поля имени.
        SendKeys.SendWait("{HOME}");

        Thread.Sleep(300);

        // Выделить всё старое имя.
        SendKeys.SendWait("+{END}");

        Thread.Sleep(300);

        // Удалить старое имя.
        SendKeys.SendWait("{BACKSPACE}");

        Thread.Sleep(500);

        Log("Старое имя удалено.");

        // Если это файл Damate qlik,
        // разбиваем имя на русскую
        // и английскую части.
        if (fileName.Contains("Damate qlik"))
        {
            string englishPart =
                "Damate qlik";

            int englishIndex =
                fileName.IndexOf(
                    englishPart,
                    StringComparison.Ordinal);

            string russianPart =
                fileName.Substring(
                    0,
                    englishIndex);

            // Русская часть.
            SendKeys.SendWait(
                russianPart);

            Thread.Sleep(300);

            // Переключаемся на английскую
            // раскладку.
            SwitchKeyboardLayout();

            Thread.Sleep(300);

            // Английская часть.
            SendKeys.SendWait(
                englishPart);

            Thread.Sleep(300);

            // Возвращаем русскую раскладку.
            SwitchKeyboardLayout();

            Thread.Sleep(500);
        }
        else
        {
            // Для Производства и Бункера
            // оставляем обычный рабочий
            // способ ввода.
            SendKeys.SendWait(
                fileName);
        }

        Thread.Sleep(1000);

        Log(
            $"Новое имя введено: {fileName}");
    }

    // Переключение раскладки:
    // Alt + Shift
    private void SwitchKeyboardLayout()
    {
        SendKeys.SendWait("%+");
    }

    // -------------------------------------------------
    // ЭКСПОРТ ОДНОГО ОТЧЁТА
    // -------------------------------------------------

    private void ExportSingleReport(
        string reportPoint,
        string fileName)
    {
        Log("");
        Log(
            $"--- Начинаем: {reportPoint} ---");

        Log(
            $"Имя файла: {fileName}");

        // Выбираем нужную вкладку.
        ClickPoint(
            reportPoint);

        Thread.Sleep(1000);

        // Файл.
        ClickPoint(
            "Файл");

        Thread.Sleep(500);

        // Экспорт.
        ClickPoint(
            "Экспорт");

        // Ждём открытия окна сохранения.
        Thread.Sleep(2000);

        // Поле имени файла.
        ClickPoint(
            "Поле имени файла");

        Thread.Sleep(500);

        // Удаление старого имени
        // и ввод нового.
        ReplaceFileName(
            fileName);

        Thread.Sleep(500);

        // Сохранить.
        ClickPoint(
            "Сохранить");

        // Ждём окно
        // "посмотреть файл?"
        Thread.Sleep(2000);

        // Нажимаем НЕТ.
        ClickPoint(
            "Нет");

        Thread.Sleep(1500);

        Log(
            $"Готово: {fileName}");
    }

    // -------------------------------------------------
    // ВСЕ ТРИ ОТЧЁТА
    // -------------------------------------------------

    private void ExportAllReports()
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
                "================================");

            Log(
                "НАЧАЛО ВЫГРУЗКИ 3 ОТЧЁТОВ");

            Log(
                "ПК №1 — Катковка Р-3");

            Log(
                "================================");

            DateTime today =
                DateTime.Today;

            DateTime yesterday =
                today.AddDays(-1);

            string folder =
                Path.Combine(
                    @"C:\Отчеты",
                    today.ToString(
                        "yyyy_MM"));

            Log(
                $"Папка: {folder}");

            // =========================================
            // 1. ОТЧЁТ ПРОИЗВОДСТВО
            // ДАТА — ПРЕДЫДУЩИЙ ДЕНЬ
            // =========================================

            string productionFile =
                $"{yesterday:yyyy_MM_dd} " +
                "Катковка Р-3 " +
                "Производственный отчет.xlsx";

            ExportSingleReport(
                "Отчет Производство",
                productionFile);

            Thread.Sleep(2000);

            // =========================================
            // 2. DAMATE QLIK
            // ДАТА — ПРЕДЫДУЩИЙ ДЕНЬ
            // =========================================

            string damateFile =
                $"{yesterday:yyyy_MM_dd} " +
                "Катковка Р-3 " +
                "Damate qlik.xlsx";

            ExportSingleReport(
                "Damate Qlik",
                damateFile);

            Thread.Sleep(2000);

            // =========================================
            // 3. ОТЧЁТ БУНКЕР
            // ДАТА — СЕГОДНЯ
            // =========================================

            string bunkerFile =
                $"{today:yyyy_MM_dd} " +
                "Катковка Р-3 " +
                "Остаток корма на 00-00.xlsx";

            ExportSingleReport(
                "Отчет Бункер",
                bunkerFile);

            Log("");
            Log(
                "================================");

            Log(
                "ВСЕ 3 ОТЧЁТА УСПЕШНО ВЫГРУЖЕНЫ");

            Log(
                "================================");

            MessageBox.Show(
                "Все 3 отчёта выгружены.",
                "BFN Exporter",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            Log("");
            Log(
                "ОШИБКА ПРИ ВЫГРУЗКЕ:");

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
