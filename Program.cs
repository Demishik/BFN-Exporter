```csharp
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
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
    // =========================================================
    // WINDOWS API
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
    private static extern void keybd_event(
        byte bVk,
        byte bScan,
        uint dwFlags,
        UIntPtr dwExtraInfo);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;

    private const byte VK_LWIN = 0x5B;
    private const byte VK_SPACE = 0x20;
    private const uint KEYEVENTF_KEYUP = 0x0002;

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
        "Нет"
    };

    private readonly Dictionary<string, Point> points = new();

    // =========================================================
    // ЭЛЕМЕНТЫ ОКНА
    // =========================================================

    private readonly Label instruction = new();
    private readonly Label mousePosition = new();
    private readonly TextBox log = new();
    private readonly Button setupButton = new();
    private readonly Button exportButton = new();

    private int setupIndex = -1;

    // =========================================================
    // ФАЙЛ НАСТРОЕК
    // =========================================================

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
    // КОНСТРУКТОР
    // =========================================================

    public MainForm()
    {
        Text = "BFN Exporter — ПК №1";

        Width = 760;
        Height = 520;

        StartPosition =
            FormStartPosition.CenterScreen;

        KeyPreview = true;

        // -----------------------------------------------------
        // Инструкция
        // -----------------------------------------------------

        instruction.Dock = DockStyle.Top;
        instruction.Height = 75;

        instruction.Font =
            new Font("Segoe UI", 12);

        instruction.TextAlign =
            ContentAlignment.MiddleCenter;

        // -----------------------------------------------------
        // Координаты мыши
        // -----------------------------------------------------

        mousePosition.Dock =
            DockStyle.Top;

        mousePosition.Height = 40;

        mousePosition.Font =
            new Font("Segoe UI", 11);

        mousePosition.TextAlign =
            ContentAlignment.MiddleCenter;

        // -----------------------------------------------------
        // Журнал
        // -----------------------------------------------------

        log.Multiline = true;
        log.ReadOnly = true;
        log.Dock = DockStyle.Fill;
        log.ScrollBars =
            ScrollBars.Vertical;

        // -----------------------------------------------------
        // Кнопка экспорта
        // -----------------------------------------------------

        exportButton.Text =
            "▶ Запустить экспорт 3 отчётов";

        exportButton.Dock =
            DockStyle.Bottom;

        exportButton.Height = 50;

        exportButton.Click +=
            (_, _) => ExportAllReports();

        // -----------------------------------------------------
        // Кнопка настройки
        // -----------------------------------------------------

        setupButton.Text =
            "⚙ Настроить координаты";

        setupButton.Dock =
            DockStyle.Bottom;

        setupButton.Height = 50;

        setupButton.Click +=
            (_, _) => StartSetup();

        // -----------------------------------------------------
        // Элементы окна
        // -----------------------------------------------------

        Controls.Add(log);
        Controls.Add(exportButton);
        Controls.Add(setupButton);
        Controls.Add(mousePosition);
        Controls.Add(instruction);

        KeyDown += MainForm_KeyDown;

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
        // Загружаем координаты
        // -----------------------------------------------------

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

        Log(
            "BFN Exporter ПК №1 запущен.");
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

        if (e.KeyCode == Keys.Escape &&
            setupIndex >= 0)
        {
            setupIndex = -1;

            setupButton.Enabled = true;
            exportButton.Enabled = true;

            instruction.Text =
                "Настройка отменена.";

            Log(
                "Настройка отменена.");
        }
    }

    // =========================================================
    // НАСТРОЙКА КООРДИНАТ
    // =========================================================

    private void StartSetup()
    {
        points.Clear();

        setupIndex = 0;

        setupButton.Enabled = false;
        exportButton.Enabled = false;

        instruction.Text =
            $"Наведи мышь на «{pointNames[0]}» и нажми F8.";

        Log(
            "Настройка координат начата.");

        Log(
            "F8 — сохранить текущую позицию мыши.");

        Log(
            "ESC — отменить.");
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

    // =========================================================
    // СОХРАНЕНИЕ КООРДИНАТ
    // =========================================================

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

            Log(
                "Координаты загружены.");
        }
        catch (Exception ex)
        {
            Log(
                "Ошибка загрузки координат: " +
                ex.Message);
        }
    }

    // =========================================================
    // КЛИК ПО КООРДИНАТЕ
    // =========================================================

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

    // =========================================================
    // ПЕРЕКЛЮЧЕНИЕ РАСКЛАДКИ
    // Win + Space
    // =========================================================

    private void SwitchKeyboardLayout()
    {
        Log(
            "Переключаем раскладку: Win + Space.");

        keybd_event(
            VK_LWIN,
            0,
            0,
            UIntPtr.Zero);

        Thread.Sleep(100);

        keybd_event(
            VK_SPACE,
            0,
            0,
            UIntPtr.Zero);

        Thread.Sleep(100);

        keybd_event(
            VK_SPACE,
            0,
            KEYEVENTF_KEYUP,
            UIntPtr.Zero);

        Thread.Sleep(100);

        keybd_event(
            VK_LWIN,
            0,
            KEYEVENTF_KEYUP,
            UIntPtr.Zero);

        // Даём Windows время реально сменить раскладку.
        Thread.Sleep(1000);
    }

    // =========================================================
    // ПОВТОРНАЯ АКТИВАЦИЯ ПОЛЯ ИМЕНИ
    // =========================================================

    private void ActivateFileNameField()
    {
        Log(
            "Повторно активируем поле имени файла.");

        ClickPoint(
            "Поле имени файла");

        Thread.Sleep(700);

        // Курсор обязательно ставим в конец.
        SendKeys.SendWait(
            "{END}");

        Thread.Sleep(300);
    }

    // =========================================================
    // УДАЛЕНИЕ СТАРОГО ИМЕНИ + ВВОД НОВОГО
    // =========================================================

    private void ReplaceFileName(
        string fileName)
    {
        Log(
            "Очищаем старое имя файла.");

        // -----------------------------------------------------
        // УДАЛЯЕМ СТАРОЕ ИМЯ
        // -----------------------------------------------------

        SendKeys.SendWait(
            "{HOME}");

        Thread.Sleep(300);

        SendKeys.SendWait(
            "+{END}");

        Thread.Sleep(300);

        SendKeys.SendWait(
            "{BACKSPACE}");

        Thread.Sleep(500);

        Log(
            "Старое имя удалено.");

        // =====================================================
        // ЕСЛИ ЕСТЬ DAMATE QLIK
        // =====================================================

        if (fileName.Contains(
            "Damate qlik",
            StringComparison.Ordinal))
        {
            const string englishPart =
                "Damate qlik";

            int englishIndex =
                fileName.IndexOf(
                    englishPart,
                    StringComparison.Ordinal);

            if (englishIndex < 0)
            {
                throw new Exception(
                    "Не удалось найти 'Damate qlik' в имени файла.");
            }

            string russianPart =
                fileName.Substring(
                    0,
                    englishIndex);

            // -------------------------------------------------
            // 1. РУССКАЯ ЧАСТЬ
            // -------------------------------------------------

            Log(
                "Вводим русскую часть имени.");

            SendKeys.SendWait(
                russianPart);

            Thread.Sleep(700);

            Log(
                "Русская часть имени введена.");

            // -------------------------------------------------
            // 2. RU -> EN
            // -------------------------------------------------

            Log(
                "Переключаемся с RU на EN.");

            SwitchKeyboardLayout();

            // -------------------------------------------------
            // 3. ПОСЛЕ ПЕРЕКЛЮЧЕНИЯ ФОКУС МОЖЕТ ПРОПАСТЬ
            // -------------------------------------------------

            ActivateFileNameField();

            Log(
                "Поле имени снова активно.");

            // -------------------------------------------------
            // 4. АНГЛИЙСКАЯ ЧАСТЬ
            // -------------------------------------------------

            Log(
                "Вводим: Damate qlik");

            SendKeys.SendWait(
                englishPart);

            Thread.Sleep(1000);

            Log(
                "Damate qlik введено.");

            // -------------------------------------------------
            // 5. EN -> RU
            // -------------------------------------------------

            Log(
                "Возвращаем русскую раскладку.");

            SwitchKeyboardLayout();

            // -------------------------------------------------
            // ВАЖНО:
            // ЗДЕСЬ БОЛЬШЕ НЕТ ВТОРОГО
            // SwitchKeyboardLayout().
            // -------------------------------------------------

            Log(
                $"Новое имя введено: {fileName}");

            return;
        }

        // =====================================================
        // ПРОИЗВОДСТВО И БУНКЕР
        // =====================================================

        Log(
            "Вводим имя файла.");

        SendKeys.SendWait(
            fileName);

        Thread.Sleep(1000);

        Log(
            $"Новое имя введено: {fileName}");
    }

    // =========================================================
    // ЭКСПОРТ ОДНОГО ОТЧЁТА
    // =========================================================

    private void ExportSingleReport(
        string reportPoint,
        string fileName)
    {
        Log("");

        Log(
            $"--- Начинаем: {reportPoint} ---");

        Log(
            $"Имя файла: {fileName}");

        // -----------------------------------------------------
        // ВКЛАДКА
        // -----------------------------------------------------

        ClickPoint(
            reportPoint);

        Thread.Sleep(1000);

        // -----------------------------------------------------
        // ФАЙЛ
        // -----------------------------------------------------

        ClickPoint(
            "Файл");

        Thread.Sleep(500);

        // -----------------------------------------------------
        // ЭКСПОРТ
        // -----------------------------------------------------

        ClickPoint(
            "Экспорт");

        Thread.Sleep(2000);

        // -----------------------------------------------------
        // ПОЛЕ ИМЕНИ ФАЙЛА
        // -----------------------------------------------------

        ClickPoint(
            "Поле имени файла");

        Thread.Sleep(500);

        // -----------------------------------------------------
        // УДАЛЯЕМ СТАРОЕ ИМЯ
        // И ВВОДИМ НОВОЕ
        // -----------------------------------------------------

        ReplaceFileName(
            fileName);

        Thread.Sleep(500);

        // -----------------------------------------------------
        // СОХРАНИТЬ
        // -----------------------------------------------------

        ClickPoint(
            "Сохранить");

        Thread.Sleep(2000);

        // -----------------------------------------------------
        // ОКНО "ПОСМОТРЕТЬ ФАЙЛ"
        // НАЖИМАЕМ НЕТ
        // -----------------------------------------------------

        ClickPoint(
            "Нет");

        Thread.Sleep(1500);

        Log(
            $"Готово: {fileName}");
    }

    // =========================================================
    // ЭКСПОРТ ВСЕХ ТРЁХ ФАЙЛОВ
    // =========================================================

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

            // =================================================
            // 1. ПРОИЗВОДСТВО
            // =================================================

            string productionFile =
                $"{yesterday:yyyy_MM_dd} " +
                "Катковка Р-3 " +
                "Производственный отчет.xlsx";

            ExportSingleReport(
                "Отчет Производство",
                productionFile);

            Thread.Sleep(2000);

            // =================================================
            // 2. DAMATE QLIK
            // =================================================

            string damateFile =
                $"{yesterday:yyyy_MM_dd} " +
                "Катковка Р-3 " +
                "Damate qlik.xlsx";

            ExportSingleReport(
                "Damate Qlik",
                damateFile);

            Thread.Sleep(2000);

            // =================================================
            // 3. БУНКЕР
            // =================================================

            string bunkerFile =
                $"{today:yyyy_MM_dd} " +
                "Катковка Р-3 " +
                "Остаток корма на 00-00.xlsx";

            ExportSingleReport(
                "Отчет Бункер",
                bunkerFile);

            // =================================================
            // ГОТОВО
            // =================================================

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

    // =========================================================
    // ЖУРНАЛ
    // =========================================================

    private void Log(string text)
    {
        log.AppendText(
            $"{DateTime.Now:HH:mm:ss}  " +
            $"{text}" +
            Environment.NewLine);
    }
}
```
