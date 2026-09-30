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
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int width = plot.Width;
            int height = plot.Height;
            if (width < 1 || height < 1) return;

            PointF origin = ToScreen(0, 0);

            double step = Math.Pow(10, Math.Floor(Math.Log10(70.0 / scale)));
            if (step * scale < 35) step *= 5;
            else if (step * scale < 70) step *= 2;

            double minX = -origin.X / scale;
            double maxX = (width - origin.X) / scale;
            double minY = (origin.Y - height) / scale;
            double maxY = origin.Y / scale;

            using (var gridPen = new Pen(Color.LightGray))
            using (var axisPen = new Pen(Color.Black, 2))
            {
                float labelY = Math.Max(0, Math.Min(height - 18, origin.Y + 3));
                float labelX = Math.Max(0, Math.Min(width - 65, origin.X + 3));

                for (double x = Math.Ceiling(minX / step) * step;
                     x <= maxX; x += step)
                {
                    float screenX = ToScreen(x, 0).X;
                    g.DrawLine(gridPen, screenX, 0, screenX, height);
                    g.DrawString(x.ToString("G4"), Font, Brushes.Black,
                        screenX + 2, labelY);
                }

                for (double y = Math.Ceiling(minY / step) * step;
                     y <= maxY; y += step)
                {
                    float screenY = ToScreen(0, y).Y;
                    g.DrawLine(gridPen, 0, screenY, width, screenY);
                    g.DrawString(y.ToString("G4"), Font, Brushes.Black,
                        labelX, screenY + 2);
                }

                if (origin.Y >= 0 && origin.Y <= height)
                    g.DrawLine(axisPen, 0, origin.Y, width, origin.Y);

                if (origin.X >= 0 && origin.X <= width)
                    g.DrawLine(axisPen, origin.X, 0, origin.X, height);

                g.DrawString("X", Font, Brushes.Black, width - 18, labelY);
                g.DrawString("Y", Font, Brushes.Black, labelX, 0);
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
                        if (comboBox1.SelectedIndex == 1 && points.Length >= 3)
                            g.DrawCurve(pen, points);
                        else
                            g.DrawLines(pen, points);
                    }

                    foreach (PointF point in points)
                        g.FillEllipse(brush, point.X - 3, point.Y - 3, 6, 6);
                }
            }

            for (int i = 0; i < tables.Count; i++)
            {
                float y = 8 + i * 20;
                string name = tables[i].Name;
                SizeF size = g.MeasureString(name, Font);

                g.FillRectangle(Brushes.White, 6, y, size.Width + 42, 20);

                using (var pen = new Pen(colors[i % colors.Length], 3))
                    g.DrawLine(pen, 10, y + 8, 30, y + 8);

                g.DrawString(name, Font, Brushes.Black, 36, y);
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