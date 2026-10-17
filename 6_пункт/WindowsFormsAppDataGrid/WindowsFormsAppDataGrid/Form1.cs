using System.Drawing;
using System.Drawing.Drawing2D;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace WindowsFormsAppDataGrid
{
    public partial class Form1 : Form
    {
        private readonly List<PointTable> tables = new List<PointTable>();

        private PointTable CurrentTable => filesListBox.SelectedItem as PointTable;

        private PlotPanel plot;

        private float scale = 40f;

        private PointF offset = new PointF(0, 0);

        private Point lastMouse;

        private bool dragging;

        private readonly Color[] colors =
        {
            Color.Red, Color.Blue, Color.Green,
            Color.DarkOrange, Color.Purple, Color.Brown
        };

        public Form1()
        {
            InitializeComponent();

            CreatePlot();

            comboBox1.Items.AddRange(new object[] { "Линии", "Плавная кривая" });
            comboBox1.DropDownStyle = ComboBoxStyle.DropDownList;

            filesListBox.DisplayMember = nameof(PointTable.Name);

            filesListBox.SelectedIndexChanged += (s, e) =>
            {
                dataGridView1.DataSource = CurrentTable?.Points;
            };

            comboBox1.SelectedIndexChanged += (s, e) => UpdateChart();
            addButton.Click += AddButton_Click;
            saveButton.Click += SaveButton_Click;
            loadButton.Click += LoadButton_Click;

            var initial = new PointTable("Новая таблица");
            initial.Points.Add(new Data { X = 0, Y = 0 });
            initial.Points.Add(new Data { X = 1, Y = 1 });
            initial.Points.Add(new Data { X = 2, Y = 4 });
            AddTable(initial);

            comboBox1.SelectedIndex = 0;
        }

        private void CreatePlot()
        {
            plot = new PlotPanel
            {
                Bounds = chart1.Bounds,
                Anchor = chart1.Anchor,
                BackColor = Color.White,
                TabStop = true
            };

            chart1.Visible = false;
            Controls.Add(plot);
            plot.BringToFront();

            plot.Paint += Plot_Paint;
            plot.Resize += (s, e) => plot.Invalidate();

            plot.MouseEnter += (s, e) => plot.Focus();

            plot.MouseDown += (s, e) =>
            {
                if (e.Button != MouseButtons.Left) return;

                dragging = true;
                lastMouse = e.Location;
                plot.Capture = true;
                plot.Cursor = Cursors.Hand;
            };

            plot.MouseMove += (s, e) =>
            {
                if (!dragging) return;

                offset.X += e.X - lastMouse.X;
                offset.Y += e.Y - lastMouse.Y;
                lastMouse = e.Location;
                plot.Invalidate();
            };

            plot.MouseUp += (s, e) =>
            {
                dragging = false;
                plot.Capture = false;
                plot.Cursor = Cursors.Default;
            };

            plot.MouseWheel += Plot_MouseWheel;
        }

        private PointF ToScreen(double x, double y)
        {
            return new PointF(
                (float)(plot.Width / 2.0 + offset.X + x * scale),
                (float)(plot.Height / 2.0 + offset.Y - y * scale));
        }

        private void Plot_MouseWheel(object sender, MouseEventArgs e)
        {
            double x = (e.X - plot.Width / 2.0 - offset.X) / scale;
            double y = -(e.Y - plot.Height / 2.0 - offset.Y) / scale;

            float factor = (float)Math.Pow(1.2, e.Delta / 120.0);
            scale = Math.Max(0.1f, Math.Min(10000f, scale * factor));

            offset.X = (float)(e.X - plot.Width / 2.0 - x * scale);
            offset.Y = (float)(e.Y - plot.Height / 2.0 + y * scale);

            plot.Invalidate();
        }

        private void Plot_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Color.White);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int width = plot.Width;
            int height = plot.Height;

            if (width < 180 || height < 180)
            {
                g.DrawString("Увеличьте область графика",
                    Font, Brushes.Black, 5, 5);
                return;
            }

            float rowHeight = Font.Height + 5;
            int legendRows = Math.Min(tables.Count, 3);

            float legendHeight = 10 + legendRows * rowHeight;
            if (tables.Count > legendRows)
                legendHeight += rowHeight;

            float top = Math.Max(80, legendHeight);

            RectangleF area = new RectangleF(
                75, top,
                width - 100,
                height - top - 2 * rowHeight - 12);

            if (area.Width < 60 || area.Height < 30)
                return;

            PointF origin = ToScreen(0, 0);

            double minX = (area.Left - origin.X) / scale;
            double maxX = (area.Right - origin.X) / scale;
            double minY = (origin.Y - area.Bottom) / scale;
            double maxY = (origin.Y - area.Top) / scale;

            Func<double, double, double, double> chooseStep =
                (min, max, desiredPixels) =>
                {
                    double step = Math.Pow(
                        10, Math.Ceiling(Math.Log10(desiredPixels / scale)));

                    for (int attempt = 0; attempt < 20; attempt++)
                    {
                        double first = Math.Ceiling(min / step - 1e-9);
                        double last = Math.Floor(max / step + 1e-9);

                        if (last - first >= 1)
                            return step;

                        step /= 10;
                    }

                    return step;
                };

            double stepX = chooseStep(minX, maxX, 85);
            double stepY = chooseStep(minY, maxY, rowHeight * 2);

            Func<double, double, string> formatValue = (value, step) =>
            {
                if (Math.Abs(value) < step * 0.000001)
                    value = 0;

                int decimals = Math.Max(
                    0, (int)Math.Round(-Math.Log10(step)));

                return value.ToString(
                    "F" + Math.Min(decimals, 15),
                    CultureInfo.InvariantCulture);
            };

            var xValues = new List<double>();
            var yValues = new List<double>();

            double firstX = Math.Ceiling(minX / stepX - 1e-9);
            double lastX = Math.Floor(maxX / stepX + 1e-9);

            for (int i = 0; i < 1000 && firstX + i <= lastX; i++)
                xValues.Add((firstX + i) * stepX);

            double firstY = Math.Ceiling(minY / stepY - 1e-9);
            double lastY = Math.Floor(maxY / stepY + 1e-9);

            for (int i = 0; i < 1000 && firstY + i <= lastY; i++)
                yValues.Add((firstY + i) * stepY);

            GraphicsState state = g.Save();
            g.SetClip(area);

            using (var gridPen = new Pen(Color.LightGray))
            using (var axisPen = new Pen(Color.Black, 2))
            {
                foreach (double x in xValues)
                {
                    float sx = ToScreen(x, 0).X;
                    g.DrawLine(gridPen, sx, area.Top, sx, area.Bottom);
                }

                foreach (double y in yValues)
                {
                    float sy = ToScreen(0, y).Y;
                    g.DrawLine(gridPen, area.Left, sy, area.Right, sy);
                }

                if (origin.Y >= area.Top && origin.Y <= area.Bottom)
                    g.DrawLine(axisPen,
                        area.Left, origin.Y, area.Right, origin.Y);

                if (origin.X >= area.Left && origin.X <= area.Right)
                    g.DrawLine(axisPen,
                        origin.X, area.Top, origin.X, area.Bottom);
            }

            for (int i = 0; i < tables.Count; i++)
            {
                Color color = colors[i % colors.Length];

                PointF[] points = tables[i].Points
                    .Select(p => ToScreen(p.X, p.Y))
                    .ToArray();

                using (var pen = new Pen(color, 2))
                using (var brush = new SolidBrush(color))
                {
                    if (points.Length >= 2)
                    {
                        if (comboBox1.SelectedIndex == 1 &&
                            points.Length >= 3)
                            g.DrawCurve(pen, points);
                        else
                            g.DrawLines(pen, points);
                    }

                    foreach (PointF point in points)
                        g.FillEllipse(brush,
                            point.X - 3, point.Y - 3, 6, 6);
                }
            }

            g.Restore(state);

            g.DrawRectangle(Pens.Gray,
                area.X, area.Y, area.Width, area.Height);


            using (var labelFont = new Font(
                Font.FontFamily, Math.Min(Font.Size, 9f)))
            using (var centered = new StringFormat())
            using (var rightAligned = new StringFormat())
            {
                centered.Alignment = StringAlignment.Center;
                centered.LineAlignment = StringAlignment.Center;
                centered.FormatFlags = StringFormatFlags.NoWrap;

                rightAligned.Alignment = StringAlignment.Far;
                rightAligned.LineAlignment = StringAlignment.Center;
                rightAligned.FormatFlags = StringFormatFlags.NoWrap;

                var occupiedX = new List<RectangleF>();

                Action<int> drawX = index =>
                {
                    double value = xValues[index];
                    float sx = ToScreen(value, 0).X;
                    string text = formatValue(value, stepX);
                    float textWidth = g.MeasureString(text, labelFont).Width + 4;

                    RectangleF rect = new RectangleF(
                        sx - textWidth / 2,
                        area.Bottom + 5,
                        textWidth,
                        rowHeight);

                    rect.X = Math.Max(
                        2, Math.Min(width - rect.Width - 2, rect.X));

                    RectangleF padded = rect;
                    padded.Inflate(4, 0);

                    if (occupiedX.Any(r => r.IntersectsWith(padded)))
                        return;

                    g.DrawString(text, labelFont,
                        Brushes.Black, rect, centered);

                    occupiedX.Add(padded);
                };

                foreach (double x in xValues)
                {
                    float sx = ToScreen(x, 0).X;
                    g.DrawLine(Pens.Black,
                        sx, area.Bottom, sx, area.Bottom + 4);
                }

                if (xValues.Count > 0)
                {
                    drawX(0);

                    if (xValues.Count > 1)
                        drawX(xValues.Count - 1);

                    for (int i = 1; i < xValues.Count - 1; i++)
                        drawX(i);
                }

                var occupiedY = new List<RectangleF>();

                Action<int> drawY = index =>
                {
                    double value = yValues[index];
                    float sy = ToScreen(0, value).Y;
                    string text = formatValue(value, stepY);

                    RectangleF rect = new RectangleF(
                        3, sy - rowHeight / 2,
                        area.Left - 11, rowHeight);

                    RectangleF padded = rect;
                    padded.Inflate(0, 2);

                    if (occupiedY.Any(r => r.IntersectsWith(padded)))
                        return;

                    g.DrawString(text, labelFont,
                        Brushes.Black, rect, rightAligned);

                    occupiedY.Add(padded);
                };

                foreach (double y in yValues)
                {
                    float sy = ToScreen(0, y).Y;
                    g.DrawLine(Pens.Black,
                        area.Left - 4, sy, area.Left, sy);
                }

                if (yValues.Count > 0)
                {
                    drawY(0);

                    if (yValues.Count > 1)
                        drawY(yValues.Count - 1);

                    for (int i = 1; i < yValues.Count - 1; i++)
                        drawY(i);
                }

            }

            using (var format = new StringFormat())
            {
                format.FormatFlags = StringFormatFlags.NoWrap;
                format.Trimming = StringTrimming.EllipsisCharacter;

                for (int i = 0; i < legendRows; i++)
                {
                    float y = 5 + i * rowHeight;

                    using (var pen = new Pen(
                        colors[i % colors.Length], 3))
                    {
                        g.DrawLine(pen,
                            10, y + Font.Height / 2,
                            30, y + Font.Height / 2);
                    }

                    g.DrawString(
                        tables[i].Name, Font, Brushes.Black,
                        new RectangleF(
                            36, y, Math.Max(1, width - 124), rowHeight),
                        format);
                }

                if (tables.Count > legendRows)
                {
                    g.DrawString(
                        "Ещё таблиц: " + (tables.Count - legendRows),
                        Font, Brushes.Black,
                        new RectangleF(
                            10, 5 + legendRows * rowHeight,
                            Math.Max(1, width - 98), rowHeight),
                        format);
                }
            }

            float cx = width - 48;
            float cy = 40;

            using (var arrow = new AdjustableArrowCap(4, 5))
            using (var pen = new Pen(Color.Black, 2))
            using (var axisFont = new Font(Font, FontStyle.Bold))
            {
                pen.CustomEndCap = arrow;

                g.DrawLine(pen, cx - 28, cy, cx + 28, cy);
                g.DrawLine(pen, cx, cy + 28, cx, cy - 28);

                g.DrawString("X", axisFont, Brushes.Black,
                    cx + 29, cy + 3);

                g.DrawString("Y", axisFont, Brushes.Black,
                    cx + 5, cy - 39);
            }
        }

        private void AddTable(PointTable table)
        {
            tables.Add(table);

            table.Points.ListChanged += (s, e) => UpdateChart();

            filesListBox.Items.Add(table);
            filesListBox.SelectedItem = table;
            UpdateChart();
        }

        private void UpdateChart()
        {
            if (plot != null)
                plot.Invalidate();
        }

        private void AddButton_Click(object sender, EventArgs e)
        {
            if (CurrentTable == null) return;

            int nextX = CurrentTable.Points.Count == 0
                ? 0
                : (int)CurrentTable.Points.Max(p => p.X) + 1;

            CurrentTable.Points.Add(new Data
            {
                X = nextX,
                Y = nextX * nextX
            });
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            if (CurrentTable == null) return;

            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "CSV-файлы (*.csv)|*.csv";
                dialog.FileName = CurrentTable.Name + ".csv";

                if (dialog.ShowDialog() != DialogResult.OK) return;

                var lines = CurrentTable.Points.Select(p =>
                    p.X.ToString(CultureInfo.InvariantCulture) + ";" +
                    p.Y.ToString(CultureInfo.InvariantCulture));

                File.WriteAllLines(dialog.FileName, lines);
            }
        }

        private void LoadButton_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "CSV-файлы (*.csv)|*.csv";
                dialog.Multiselect = true;

                if (dialog.ShowDialog() != DialogResult.OK) return;

                try
                {
                    var loaded = new List<PointTable>();

                    foreach (string path in dialog.FileNames)
                    {
                        var table = new PointTable(
                            Path.GetFileNameWithoutExtension(path));

                        foreach (string line in File.ReadAllLines(path))
                        {
                            if (string.IsNullOrWhiteSpace(line)) continue;

                            string[] parts = line.Split(';');
                            if (parts.Length != 2)
                                throw new FormatException("Неверная строка в " + path);

                            table.Points.Add(new Data
                            {
                                X = double.Parse(parts[0],
                                    CultureInfo.InvariantCulture),
                                Y = double.Parse(parts[1],
                                    CultureInfo.InvariantCulture)
                            });
                        }

                        loaded.Add(table);
                    }

                    foreach (var table in loaded)
                        AddTable(table);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Не удалось загрузить файлы: " + ex.Message);
                }
            }
        }
    }

    public class PointTable
    {
        public string Name { get; }
        public BindingList<Data> Points { get; } = new BindingList<Data>();

        public PointTable(string name)
        {
            Name = name;
        }
    }

    public class PlotPanel : Panel
    {
        public PlotPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }
    }

    public class Data : INotifyPropertyChanged
    {
        private double x;
        private double y;

        public double X
        {
            get => x;
            set
            {
                x = value;
                PropertyChanged?.Invoke(this,
                    new PropertyChangedEventArgs(nameof(X)));
            }
        }

        public double Y
        {
            get => y;
            set
            {
                y = value;
                PropertyChanged?.Invoke(this,
                    new PropertyChangedEventArgs(nameof(Y)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}