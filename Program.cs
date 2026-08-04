using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Tesseract;

namespace ScreenReader
{
    public static class Program
    {
        private static readonly string AreasFile =
            Path.Combine(AppContext.BaseDirectory, "areas.txt");

        private static readonly string[] AreaNames =
        {
            "Название корпуса",
            "Корм за час",
            "Вода за час",
            "Корм с 00:00",
            "Вода с 00:00"
        };

        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();

            Form form = new Form
            {
                Text = "ScreenReader — чтение данных",
                StartPosition = FormStartPosition.CenterScreen,
                Width = 1200,
                Height = 800
            };

            Label title = new Label
            {
                Text = "Чтение данных корпуса",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(20, 20)
            };

            Label instruction = new Label
            {
                Text = "Настрой пять областей один раз. Строки и площадки программа ищет сама.",
                Font = new Font("Segoe UI", 10),
                AutoSize = true,
                Location = new Point(20, 50)
            };

            Button setupButton = new Button
            {
                Text = "Настроить области",
                Font = new Font("Segoe UI", 11),
                Width = 220,
                Height = 45,
                Location = new Point(20, 80)
            };

            Button detectButton = new Button
            {
                Text = "Найти корпуса автоматически",
                Font = new Font("Segoe UI", 11),
                Width = 260,
                Height = 45,
                Location = new Point(255, 80)
            };

            Button showRowsButton = new Button
            {
                Text = "Показать найденные строки",
                Font = new Font("Segoe UI", 11),
                Width = 250,
                Height = 45,
                Location = new Point(530, 80)
            };

            Button readButton = new Button
            {
                Text = "Считать найденные корпуса",
                Font = new Font("Segoe UI", 11),
                Width = 250,
                Height = 45,
                Location = new Point(795, 80)
            };

            TextBox result = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 11),
                Location = new Point(20, 145),
                Width = 1140,
                Height = 560
            };

            form.Controls.Add(title);
            form.Controls.Add(instruction);
            form.Controls.Add(setupButton);
            form.Controls.Add(detectButton);
            form.Controls.Add(showRowsButton);
            form.Controls.Add(readButton);
            form.Controls.Add(result);

            Dictionary<string, Rectangle> areas = LoadAreas();
            List<DetectedRow> detectedRows = new List<DetectedRow>();

            result.Text = areas.Count == AreaNames.Length
                ? "Области загружены из areas.txt.\r\n\r\nНажми «Найти корпуса автоматически»."
                : "Области ещё не настроены.\r\n\r\nНажми «Настроить области».";

            setupButton.Click += (sender, e) =>
            {
                try
                {
                    Dictionary<string, Rectangle> newAreas = SelectAreas(form);

                    if (newAreas.Count != AreaNames.Length)
                        return;

                    areas = newAreas;
                    SaveAreas(areas);

                    result.Text =
                        "ОБЛАСТИ СОХРАНЕНЫ\r\n\r\n" +
                        "Теперь нажми «Найти корпуса автоматически».";
                }
                catch (Exception ex)
                {
                    form.Show();
                    form.Activate();
                    result.Text = "ОШИБКА НАСТРОЙКИ:\r\n\r\n" + ex.Message;
                }
            };

            detectButton.Click += (sender, e) =>
            {
                if (areas.Count != AreaNames.Length)
                {
                    result.Text = "Сначала настрой пять областей.";
                    return;
                }

                try
                {
                    form.Hide();
                    Application.DoEvents();
                    System.Threading.Thread.Sleep(300);

                    using Bitmap screenshot = CaptureScreen();

                    detectedRows = DetectRows(screenshot, areas);

                    form.Show();
                    form.Activate();

                    result.Text = BuildDetectionReport(detectedRows);

                    if (detectedRows.Count > 0)
                    {
                        using RowPreviewForm preview =
                            new RowPreviewForm(screenshot, detectedRows);
                        preview.ShowDialog();
                        form.Show();
                        form.Activate();
                    }
                }
                catch (Exception ex)
                {
                    form.Show();
                    form.Activate();
                    result.Text =
                        "ОШИБКА АВТОМАТИЧЕСКОГО ПОИСКА:\r\n\r\n" +
                        ex.Message + "\r\n\r\n" + ex.StackTrace;
                }
            };

            showRowsButton.Click += (sender, e) =>
            {
                if (detectedRows.Count == 0)
                {
                    result.Text = "Сначала нажми «Найти корпуса автоматически».";
                    return;
                }

                try
                {
                    form.Hide();
                    Application.DoEvents();
                    System.Threading.Thread.Sleep(200);

                    using Bitmap screenshot = CaptureScreen();
                    using RowPreviewForm preview =
                        new RowPreviewForm(screenshot, detectedRows);

                    preview.ShowDialog();

                    form.Show();
                    form.Activate();
                    result.Text =
                        BuildDetectionReport(detectedRows) +
                        "\r\n\r\nESC закрывает проверку.";
                }
                catch (Exception ex)
                {
                    form.Show();
                    form.Activate();
                    result.Text = "ОШИБКА:\r\n\r\n" + ex.Message;
                }
            };

            readButton.Click += (sender, e) =>
            {
                if (areas.Count != AreaNames.Length)
                {
                    result.Text = "Сначала настрой пять областей.";
                    return;
                }

                if (detectedRows.Count == 0)
                {
                    result.Text = "Сначала найди корпуса автоматически.";
                    return;
                }

                try
                {
                    form.Hide();
                    Application.DoEvents();
                    System.Threading.Thread.Sleep(300);

                    using Bitmap screenshot = CaptureScreen();

                    List<string> output = new List<string>();
                    int rowIndex = 0;

                    foreach (DetectedRow row in detectedRows)
                    {
                        rowIndex++;

                        string[] values = ReadRow(
                            screenshot,
                            areas,
                            row.Y);

                        output.Add(
                            $"Площадка {row.Platform}, корпус {row.NumberInPlatform}");
                        output.Add($"Название: {values[0]}");
                        output.Add($"Корм/час: {values[1]}");
                        output.Add($"Вода/час: {values[2]}");
                        output.Add($"Корм с 00:00: {values[3]}");
                        output.Add($"Вода с 00:00: {values[4]}");
                        output.Add("");

                        if (rowIndex >= 100)
                            break;
                    }

                    form.Show();
                    form.Activate();

                    result.Text =
                        "РЕЗУЛЬТАТ СЧИТЫВАНИЯ\r\n" +
                        "==============================\r\n\r\n" +
                        string.Join("\r\n", output);
                }
                catch (Exception ex)
                {
                    form.Show();
                    form.Activate();
                    result.Text =
                        "ОШИБКА OCR:\r\n\r\n" +
                        ex.Message + "\r\n\r\n" +
                        ex.StackTrace;
                }
            };

            Application.Run(form);
        }

        // =============================================================
        // АВТОМАТИЧЕСКОЕ ОБНАРУЖЕНИЕ СТРОК И ПЛОЩАДОК
        // =============================================================

        private static List<DetectedRow> DetectRows(
            Bitmap screenshot,
            Dictionary<string, Rectangle> areas)
        {
            List<int> centers = FindTextCenters(screenshot, areas);

            if (centers.Count < 2)
                return centers
                    .Select((y, i) => new DetectedRow
                    {
                        Y = y,
                        Platform = 1,
                        NumberInPlatform = i + 1,
                        IsReconstructed = false
                    })
                    .ToList();

            int step = FindDominantStep(centers);

            if (step < 5 || step > 100)
                return new List<DetectedRow>();

            List<List<int>> platforms =
                SplitPlatforms(centers, step);

            List<DetectedRow> result = new List<DetectedRow>();
            int platformNumber = 1;

            foreach (List<int> platform in platforms)
            {
                if (platform.Count == 0)
                    continue;

                List<int> rows = ReconstructRows(platform, step);

                int number = 1;

                foreach (int y in rows)
                {
                    result.Add(new DetectedRow
                    {
                        Y = y,
                        Platform = platformNumber,
                        NumberInPlatform = number++,
                        IsReconstructed = !platform.Contains(y)
                    });
                }

                platformNumber++;
            }

            return result;
        }

        private static List<int> FindTextCenters(
            Bitmap screenshot,
            Dictionary<string, Rectangle> areas)
        {
            int width = screenshot.Width;
            int height = screenshot.Height;

            List<(int left, int right)> xRanges =
                new List<(int left, int right)>();

            foreach (string name in AreaNames)
            {
                if (!areas.TryGetValue(name, out Rectangle r))
                    continue;

                int left = Math.Max(0, r.Left);
                int right = Math.Min(width - 1, r.Right - 1);

                if (right > left)
                    xRanges.Add((left, right));
            }

            if (xRanges.Count == 0)
                return new List<int>();

            int[] density = new int[height];

            for (int y = 0; y < height; y++)
            {
                int dark = 0;
                int total = 0;

                foreach ((int left, int right) range in xRanges)
                {
                    for (int x = range.left; x <= range.right; x += 2)
                    {
                        Color c = screenshot.GetPixel(x, y);
                        int brightness = (c.R + c.G + c.B) / 3;

                        if (brightness < 155)
                            dark++;

                        total++;
                    }
                }

                density[y] = total == 0
                    ? 0
                    : (dark * 1000) / total;
            }

            // Сглаживание.
            int[] smooth = new int[height];

            for (int y = 0; y < height; y++)
            {
                int sum = 0;
                int count = 0;

                for (int d = -2; d <= 2; d++)
                {
                    int yy = y + d;

                    if (yy < 0 || yy >= height)
                        continue;

                    sum += density[yy];
                    count++;
                }

                smooth[y] = count == 0 ? 0 : sum / count;
            }

            // Ищем полосы текста.
            const int threshold = 8;
            List<(int top, int bottom)> bands =
                new List<(int top, int bottom)>();

            bool active = false;
            int start = 0;

            for (int y = 0; y < height; y++)
            {
                bool isActive = smooth[y] >= threshold;

                if (isActive && !active)
                {
                    active = true;
                    start = y;
                }

                if (!isActive && active)
                {
                    int bottom = y - 1;

                    if (bottom - start + 1 >= 2)
                        bands.Add((start, bottom));

                    active = false;
                }
            }

            if (active)
            {
                int bottom = height - 1;

                if (bottom - start + 1 >= 2)
                    bands.Add((start, bottom));
            }

            List<int> centers = new List<int>();

            foreach ((int top, int bottom) band in bands)
            {
                int center = (band.top + band.bottom) / 2;

                if (centers.Count == 0 ||
                    center - centers[^1] >= 6)
                {
                    centers.Add(center);
                }
            }

            return centers;
        }

        private static int FindDominantStep(List<int> centers)
        {
            if (centers.Count < 2)
                return 0;

            Dictionary<int, int> votes =
                new Dictionary<int, int>();

            for (int i = 1; i < centers.Count; i++)
            {
                int difference =
                    centers[i] - centers[i - 1];

                if (difference < 8 || difference > 80)
                    continue;

                // Разрешаем ошибку измерения ±2 пикселя.
                int normalized =
                    Math.Max(
                        1,
                        (int)Math.Round(difference / 2.0) * 2);

                if (!votes.ContainsKey(normalized))
                    votes[normalized] = 0;

                votes[normalized]++;
            }

            if (votes.Count == 0)
                return 0;

            return votes
                .OrderByDescending(x => x.Value)
                .ThenBy(x => Math.Abs(x.Key - 20))
                .First()
                .Key;
        }

        private static List<List<int>> SplitPlatforms(
            List<int> centers,
            int step)
        {
            List<List<int>> platforms =
                new List<List<int>>();

            if (centers.Count == 0)
                return platforms;

            List<int> current = new List<int>
            {
                centers[0]
            };

            for (int i = 1; i < centers.Count; i++)
            {
                int difference =
                    centers[i] - centers[i - 1];

                // Обычный корпус: примерно один шаг.
                // Разрыв больше 1.8 шага считаем новой площадкой.
                if (difference > step * 1.8)
                {
                    platforms.Add(current);
                    current = new List<int>();
                }

                current.Add(centers[i]);
            }

            if (current.Count > 0)
                platforms.Add(current);

            return platforms;
        }

        private static List<int> ReconstructRows(
            List<int> rows,
            int step)
        {
            if (rows.Count <= 1 || step <= 0)
                return rows.OrderBy(x => x).ToList();

            rows = rows.OrderBy(x => x).ToList();

            List<int> result = new List<int>();

            for (int i = 0; i < rows.Count - 1; i++)
            {
                int current = rows[i];
                int next = rows[i + 1];

                result.Add(current);

                int difference = next - current;
                int count =
                    (int)Math.Round(
                        (double)difference / step);

                if (count > 1 && count <= 6)
                {
                    for (int n = 1; n < count; n++)
                    {
                        int y = current + step * n;

                        if (y < next)
                            result.Add(y);
                    }
                }
            }

            result.Add(rows[^1]);

            return result
                .Distinct()
                .OrderBy(x => x)
                .ToList();
        }

        // =============================================================
        // OCR
        // =============================================================

        private static string[] ReadRow(
            Bitmap screenshot,
            Dictionary<string, Rectangle> areas,
            int centerY)
        {
            string[] values = new string[AreaNames.Length];

            for (int i = 0; i < AreaNames.Length; i++)
            {
                Rectangle template = areas[AreaNames[i]];

                int y =
                    centerY -
                    template.Height / 2;

                Rectangle area = new Rectangle(
                    template.X,
                    y,
                    template.Width,
                    template.Height);

                area = ClampRectangle(
                    area,
                    screenshot.Width,
                    screenshot.Height);

                using Bitmap crop =
                    screenshot.Clone(
                        area,
                        PixelFormat.Format32bppArgb);

                values[i] =
                    RunSparseOcr(crop).Trim();
            }

            return values;
        }

        private static string RunSparseOcr(Bitmap source)
        {
            using Bitmap enlarged =
                ResizeImage(source, 3);

            string tessdataPath =
                Path.Combine(
                    AppContext.BaseDirectory,
                    "tessdata");

            using TesseractEngine engine =
                new TesseractEngine(
                    tessdataPath,
                    "rus",
                    EngineMode.Default);

            engine.SetVariable(
                "preserve_interword_spaces",
                "1");

            using MemoryStream stream =
                new MemoryStream();

            enlarged.Save(
                stream,
                System.Drawing.Imaging.ImageFormat.Png);

            using Pix pix =
                Pix.LoadFromMemory(stream.ToArray());

            using Page page =
                engine.Process(
                    pix,
                    PageSegMode.SparseText);

            return page.GetText();
        }

        private static Bitmap ResizeImage(
            Bitmap source,
            int scale)
        {
            Bitmap result =
                new Bitmap(
                    source.Width * scale,
                    source.Height * scale,
                    PixelFormat.Format24bppRgb);

            using Graphics graphics =
                Graphics.FromImage(result);

            graphics.InterpolationMode =
                InterpolationMode.HighQualityBicubic;

            graphics.SmoothingMode =
                SmoothingMode.HighQuality;

            graphics.PixelOffsetMode =
                PixelOffsetMode.HighQuality;

            graphics.DrawImage(
                source,
                new Rectangle(
                    0,
                    0,
                    result.Width,
                    result.Height));

            return result;
        }

        // =============================================================
        // ОБЛАСТИ
        // =============================================================

        private static Dictionary<string, Rectangle> SelectAreas(
            Form form)
        {
            Dictionary<string, Rectangle> areas =
                new Dictionary<string, Rectangle>();

            form.Hide();
            Application.DoEvents();
            System.Threading.Thread.Sleep(300);

            using Bitmap screenshot = CaptureScreen();

            for (int i = 0; i < AreaNames.Length; i++)
            {
                using SelectionForm selection =
                    new SelectionForm(
                        screenshot,
                        AreaNames[i]);

                if (selection.ShowDialog() != DialogResult.OK)
                {
                    form.Show();
                    form.Activate();
                    return areas;
                }

                Rectangle r =
                    selection.SelectedRectangle;

                if (r.Width <= 5 || r.Height <= 5)
                {
                    form.Show();
                    form.Activate();
                    return areas;
                }

                areas[AreaNames[i]] = r;
            }

            form.Show();
            form.Activate();

            return areas;
        }

        private static void SaveAreas(
            Dictionary<string, Rectangle> areas)
        {
            using StreamWriter writer =
                new StreamWriter(AreasFile, false);

            foreach (string name in AreaNames)
            {
                Rectangle r = areas[name];

                writer.WriteLine(
                    $"{name}|{r.X}|{r.Y}|{r.Width}|{r.Height}");
            }
        }

        private static Dictionary<string, Rectangle> LoadAreas()
        {
            Dictionary<string, Rectangle> areas =
                new Dictionary<string, Rectangle>();

            if (!File.Exists(AreasFile))
                return areas;

            try
            {
                foreach (string line in File.ReadAllLines(AreasFile))
                {
                    string[] parts = line.Split('|');

                    if (parts.Length != 5)
                        continue;

                    if (!int.TryParse(parts[1], out int x))
                        continue;

                    if (!int.TryParse(parts[2], out int y))
                        continue;

                    if (!int.TryParse(parts[3], out int width))
                        continue;

                    if (!int.TryParse(parts[4], out int height))
                        continue;

                    areas[parts[0]] =
                        new Rectangle(
                            x,
                            y,
                            width,
                            height);
                }
            }
            catch
            {
                areas.Clear();
            }

            return areas;
        }

        // =============================================================
        // ЭКРАН
        // =============================================================

        private static Bitmap CaptureScreen()
        {
            Rectangle bounds =
                Screen.PrimaryScreen.Bounds;

            Bitmap screenshot =
                new Bitmap(
                    bounds.Width,
                    bounds.Height,
                    PixelFormat.Format32bppArgb);

            using Graphics graphics =
                Graphics.FromImage(screenshot);

            graphics.CopyFromScreen(
                bounds.Left,
                bounds.Top,
                0,
                0,
                bounds.Size);

            return screenshot;
        }

        private static Rectangle ClampRectangle(
            Rectangle rectangle,
            int width,
            int height)
        {
            int left =
                Math.Max(0, rectangle.Left);

            int top =
                Math.Max(0, rectangle.Top);

            int right =
                Math.Min(width, rectangle.Right);

            int bottom =
                Math.Min(height, rectangle.Bottom);

            if (right <= left)
                right = Math.Min(width, left + 1);

            if (bottom <= top)
                bottom = Math.Min(height, top + 1);

            return new Rectangle(
                left,
                top,
                right - left,
                bottom - top);
        }

        private static string BuildDetectionReport(
            List<DetectedRow> rows)
        {
            if (rows.Count == 0)
                return "Корпуса не найдены.";

            int platformCount =
                rows.Select(x => x.Platform).Distinct().Count();

            string text =
                "АВТОМАТИЧЕСКОЕ ОБНАРУЖЕНИЕ\r\n" +
                "==============================\r\n\r\n" +
                $"Найдено строк: {rows.Count}\r\n" +
                $"Площадок: {platformCount}\r\n\r\n";

            int currentPlatform = -1;

            foreach (DetectedRow row in rows)
            {
                if (row.Platform != currentPlatform)
                {
                    currentPlatform = row.Platform;
                    text +=
                        $"ПЛОЩАДКА {currentPlatform}\r\n";
                }

                text +=
                    $"  Корпус {row.NumberInPlatform}" +
                    $"   Y={row.Y}";

                if (row.IsReconstructed)
                    text += "   [восстановлен по шагу]";

                text += "\r\n";
            }

            return text;
        }

        public class DetectedRow
        {
            public int Y { get; set; }
            public int Platform { get; set; }
            public int NumberInPlatform { get; set; }
            public bool IsReconstructed { get; set; }
        }
    }

    // =================================================================
    // ВЫБОР OCR-ОБЛАСТИ
    // =================================================================

    public class SelectionForm : Form
    {
        private readonly Bitmap screenshot;
        private readonly string areaName;

        private Point startPoint;
        private Point currentPoint;
        private bool selecting;

        public Rectangle SelectedRectangle { get; private set; }

        public SelectionForm(
            Bitmap screenshot,
            string areaName)
        {
            this.screenshot = screenshot;
            this.areaName = areaName;

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = Screen.PrimaryScreen.Bounds;
            TopMost = true;
            Cursor = Cursors.Cross;
            DoubleBuffered = true;
            KeyPreview = true;

            MouseDown += SelectionForm_MouseDown;
            MouseMove += SelectionForm_MouseMove;
            MouseUp += SelectionForm_MouseUp;
            KeyDown += SelectionForm_KeyDown;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            e.Graphics.DrawImage(
                screenshot,
                ClientRectangle);

            using SolidBrush overlay =
                new SolidBrush(
                    Color.FromArgb(100, Color.Black));

            e.Graphics.FillRectangle(
                overlay,
                ClientRectangle);

            using SolidBrush textBrush =
                new SolidBrush(Color.White);

            using Font font =
                new Font(
                    "Segoe UI",
                    18,
                    FontStyle.Bold);

            e.Graphics.DrawString(
                "Выберите: " + areaName,
                font,
                textBrush,
                20,
                20);

            if (!selecting)
                return;

            Rectangle rectangle =
                GetRectangle(
                    startPoint,
                    currentPoint);

            using SolidBrush selectedBrush =
                new SolidBrush(
                    Color.FromArgb(
                        80,
                        Color.White));

            using Pen pen =
                new Pen(
                    Color.Red,
                    3);

            e.Graphics.FillRectangle(
                selectedBrush,
                rectangle);

            e.Graphics.DrawRectangle(
                pen,
                rectangle);
        }

        private void SelectionForm_MouseDown(
            object? sender,
            MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;

            selecting = true;
            startPoint = e.Location;
            currentPoint = e.Location;
            Invalidate();
        }

        private void SelectionForm_MouseMove(
            object? sender,
            MouseEventArgs e)
        {
            if (!selecting)
                return;

            currentPoint = e.Location;
            Invalidate();
        }

        private void SelectionForm_MouseUp(
            object? sender,
            MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;

            selecting = false;
            currentPoint = e.Location;

            SelectedRectangle =
                GetRectangle(
                    startPoint,
                    currentPoint);

            if (SelectedRectangle.Width > 5 &&
                SelectedRectangle.Height > 5)
            {
                DialogResult = DialogResult.OK;
                Close();
            }

            Invalidate();
        }

        private void SelectionForm_KeyDown(
            object? sender,
            KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                Close();
            }
        }

        private static Rectangle GetRectangle(
            Point p1,
            Point p2)
        {
            return new Rectangle(
                Math.Min(p1.X, p2.X),
                Math.Min(p1.Y, p2.Y),
                Math.Abs(p1.X - p2.X),
                Math.Abs(p1.Y - p2.Y));
        }
    }

    // =================================================================
    // ПРЕДПРОСМОТР АВТОМАТИЧЕСКИХ СТРОК
    // =================================================================

    public class RowPreviewForm : Form
    {
        private readonly Bitmap screenshot;
        private readonly List<Program.DetectedRow> rows;

        public RowPreviewForm(
            Bitmap screenshot,
            List<Program.DetectedRow> rows)
        {
            this.screenshot = screenshot;
            this.rows = rows;

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = Screen.PrimaryScreen.Bounds;
            TopMost = true;
            DoubleBuffered = true;
            KeyPreview = true;

            KeyDown += RowPreviewForm_KeyDown;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            e.Graphics.DrawImage(
                screenshot,
                ClientRectangle);

            using SolidBrush background =
                new SolidBrush(
                    Color.FromArgb(
                        170,
                        Color.Black));

            e.Graphics.FillRectangle(
                background,
                0,
                0,
                700,
                125);

            using Font titleFont =
                new Font(
                    "Segoe UI",
                    18,
                    FontStyle.Bold);

            using Font infoFont =
                new Font(
                    "Segoe UI",
                    11);

            using SolidBrush white =
                new SolidBrush(Color.White);

            e.Graphics.DrawString(
                "Автоматически найденные строки",
                titleFont,
                white,
                15,
                10);

            e.Graphics.DrawString(
                $"Найдено строк: {rows.Count}",
                infoFont,
                white,
                15,
                48);

            e.Graphics.DrawString(
                "Зелёный — найденный текст; " +
                "жёлтый — восстановленный по шагу; ESC — выход",
                infoFont,
                white,
                15,
                75);

            foreach (Program.DetectedRow row in rows)
            {
                Color lineColor =
                    row.IsReconstructed
                        ? Color.Yellow
                        : Color.Lime;

                using Pen pen =
                    new Pen(lineColor, 2);

                e.Graphics.DrawLine(
                    pen,
                    0,
                    row.Y,
                    ClientSize.Width,
                    row.Y);

                using Font rowFont =
                    new Font(
                        "Segoe UI",
                        9,
                        FontStyle.Bold);

                string label =
                    $"П{row.Platform}  " +
                    $"Корпус {row.NumberInPlatform}  " +
                    $"Y={row.Y}";

                e.Graphics.DrawString(
                    label,
                    rowFont,
                    white,
                    8,
                    Math.Max(
                        0,
                        row.Y - 12));
            }
        }

        private void RowPreviewForm_KeyDown(
            object? sender,
            KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
                Close();
        }
    }
}
