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

        public Form1()
        {
            InitializeComponent();

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
            chart1.Series.Clear();
            chart1.Legends.Clear();
            chart1.Legends.Add(new Legend("Таблицы"));

            if (chart1.ChartAreas.Count == 0)
                chart1.ChartAreas.Add(new ChartArea("ChartArea1"));

            var type = comboBox1.SelectedIndex == 1
                ? SeriesChartType.Spline
                : SeriesChartType.Line;

            foreach (var table in tables)
            {
                var series = new Series(table.Name);
                series.ChartType = type;
                series.ChartArea = chart1.ChartAreas[0].Name;
                series.Legend = "Таблицы";

                foreach (var point in table.Points)
                    series.Points.AddXY(point.X, point.Y);

                chart1.Series.Add(series);
            }
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