using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
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
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_RESTORE = 9;

    private readonly TextBox log = new();
    private readonly Button start = new();

    public MainForm()
    {
        Text = "BFN Exporter — Катковка Р-3";
        Width = 700;
        Height = 420;
        StartPosition = FormStartPosition.CenterScreen;

        log.Multiline = true;
        log.ReadOnly = true;
        log.ScrollBars = ScrollBars.Vertical;
        log.Dock = DockStyle.Fill;

        start.Text = "Запустить экспорт";
        start.Dock = DockStyle.Bottom;
        start.Height = 50;
        start.Click += (_, _) => Run();

        Controls.Add(log);
        Controls.Add(start);

        Log("BFN Exporter готов.");
        Log("ПК №1 — Катковка Р-3");
    }

    private void Log(string text)
    {
        log.AppendText(
            $"{DateTime.Now:HH:mm:ss}  {text}{Environment.NewLine}");
    }

    private void Run()
    {
        start.Enabled = false;

        try
        {
            DateTime today = DateTime.Today;
            DateTime yesterday = today.AddDays(-1);

            string folder = Path.Combine(
                @"C:\Отчеты",
                today.ToString("yyyy_MM"));

            string production =
                $"{yesterday:yyyy_MM_dd} Катковка Р-3 Производственный отчет.xlsx";

            string damate =
                $"{yesterday:yyyy_MM_dd} Катковка Р-3 Damate qlik.xlsx";

            string bunker =
                $"{today:yyyy_MM_dd} Катковка Р-3 Остаток корма на 00-00.xlsx";

            Log($"Папка: {folder}");
            Log($"Производство: {production}");
            Log($"Damate Qlik: {damate}");
            Log($"Бункер: {bunker}");

            Log("");
            Log("Параметры проверены.");
            Log("Следующий этап — подключение реальных кликов BigFarmNet.");
        }
        catch (Exception ex)
        {
            Log("ОШИБКА: " + ex.Message);
        }
        finally
        {
            start.Enabled = true;
        }
    }
}
