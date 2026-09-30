using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Globalization;
using System.IO;
using System.Linq;

namespace WindowsFormsAppDataGrid
{
    public partial class Form1 : Form
    {
        private readonly BindingSource data = new BindingSource();

        public Form1()
        {
            InitializeComponent();

            data.Add(new Data { X = 0, Y = 0 });
            data.Add(new Data { X = 1, Y = 1 });
            data.Add(new Data { X = 2, Y = 4 });
            dataGridView1.DataSource = data;

            chart1.Series.Clear();
            var series = chart1.Series.Add("Series1");
            series.XValueMember = "X";
            series.YValueMembers = "Y";

            addButton.Click += AddButton_Click;

            comboBox1.Items.AddRange(new object[] { "Линии", "Плавная кривая" });
            comboBox1.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBox1.SelectedIndexChanged += ComboBox1_SelectedIndexChanged;
            comboBox1.SelectedIndex = 0;

            data.ListChanged += (sender, e) => UpdateChart();
            UpdateChart();

            saveButton.Click += SaveButton_Click;
            loadButton.Click += LoadButton_Click;
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "CSV-файлы (*.csv)|*.csv";
                dialog.FileName = "points.csv";

                if (dialog.ShowDialog() != DialogResult.OK)
                    return;

                var lines = data.Cast<Data>()
                    .Select(point =>
                        point.X.ToString(CultureInfo.InvariantCulture) + ";" +
                        point.Y.ToString(CultureInfo.InvariantCulture));

                File.WriteAllLines(dialog.FileName, lines);
            }
        }

        private void LoadButton_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "CSV-файлы (*.csv)|*.csv";

                if (dialog.ShowDialog() != DialogResult.OK)
                    return;

                try
                {
                    var points = File.ReadAllLines(dialog.FileName)
                        .Where(line => !string.IsNullOrWhiteSpace(line))
                        .Select(line =>
                        {
                            string[] parts = line.Split(';');
                            if (parts.Length != 2)
                                throw new FormatException("В строке должно быть два числа: X;Y");

                            return new Data
                            {
                                X = double.Parse(parts[0], CultureInfo.InvariantCulture),
                                Y = double.Parse(parts[1], CultureInfo.InvariantCulture)
                            };
                        })
                        .ToList();

                    data.Clear();
                    foreach (var point in points)
                        data.Add(point);

                    UpdateChart();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Не удалось загрузить файл: " + ex.Message);
                }
            }
        }

        private void UpdateChart()
        {
            chart1.DataSource = null;
            chart1.DataSource = data;
            chart1.DataBind();
        }

        private void ComboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBox1.SelectedIndex == 0)
                DrawAsLines_Click(sender, e);
            else
                DrawAsSpline_Click(sender, e);
        }

        private void AddButton_Click(object sender, EventArgs e)
        {
            data.Add(new Data { X = 4, Y = 16 });
        }

        private void DrawAsLines_Click(object sender, EventArgs e)
        {
            chart1.DataSource = null;
            chart1.Series[0].ChartType =
                System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line;
            chart1.DataSource = data;
            chart1.DataBind();
        }

        private void DrawAsSpline_Click(object sender, EventArgs e)
        {
            chart1.DataSource = null;
            chart1.Series[0].ChartType =
                System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Spline;
            chart1.DataSource = data;
            chart1.DataBind();
        }
    }

    public class Data : System.ComponentModel.INotifyPropertyChanged
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
                    new System.ComponentModel.PropertyChangedEventArgs(nameof(X)));
            }
        }

        public double Y
        {
            get => y;
            set
            {
                y = value;
                PropertyChanged?.Invoke(this,
                    new System.ComponentModel.PropertyChangedEventArgs(nameof(Y)));
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    }
}
