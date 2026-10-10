using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

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
