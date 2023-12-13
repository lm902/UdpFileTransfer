using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;
using OxyPlot.Wpf;
using System.Configuration;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace UdpFileTransfer.Grapher
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private List<string[]> lines;

        public MainWindow()
        {
            var startInfo = new ProcessStartInfo(ConfigurationManager.AppSettings["proxyApplicationFileName"]!, ConfigurationManager.AppSettings["proxyApplicationArguments"]!);
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.CreateNoWindow = true;
            Console.WriteLine("Press any key to terminate proxy");
            var process = Process.Start(startInfo)!;
            lines = new List<string[]>();
            Task.Run(() =>
            {
                while (!process.StandardOutput.EndOfStream)
                {
                    var line = process.StandardOutput.ReadLine();
                    if (line == null)
                    {
                        continue;
                    }
                    Console.WriteLine(line);
                    lines.Add(line.Split('\t'));
                }
            });
            Console.ReadKey();
            process.Kill();
            InitializeComponent();
        }

        private void PacketSizesPlotView_Loaded(object sender, RoutedEventArgs e)
        {
            var view = sender as PlotView;
            var model = new PlotModel();
            model.Title = "Packet Sizes";
            model.Axes.Add(new DateTimeAxis
            {
                Title = "Time",
                Key = "x"
            });
            model.Axes.Add(new LinearAxis
            {
                Title = "Packet Size (byte)",
                Key = "y",
                Minimum = 0
            });
            var sources = lines.Skip(1).GroupBy(line => line[0]);
            var legends = from source in sources select new Legend { Key = source.Key, LegendTitle = source.Key };
            foreach (var legend in legends)
            {
                model.Legends.Add(legend);
            }
            foreach (var source in sources)
            {
                var series = new LineSeries()
                {
                    LegendKey = source.Key
                };
                series.XAxisKey = "x";
                series.YAxisKey = "y";
                series.Points.AddRange(from line in source select new DataPoint(Axis.ToDouble(DateTime.FromBinary(long.Parse(line[2]))), double.Parse(line[1])));
                model.Series.Add(series);
            }
            view!.Model = model;
        }

        private void PacketRatesPlotView_Loaded(object sender, RoutedEventArgs e)
        {
            var view = sender as PlotView;
            var model = new PlotModel();
            model.Title = "Packet Rates";
            model.Axes.Add(new DateTimeAxis
            {
                Title = "Time",
                Key = "x"
            });
            model.Axes.Add(new LinearAxis
            {
                Title = "Packets Per Second",
                Key = "y",
                Minimum = 0
            });
            var sources = lines.Skip(1).GroupBy(line => line[0]);
            var legends = from source in sources select new Legend { Key = source.Key, LegendTitle = source.Key };
            foreach (var legend in legends)
            {
                model.Legends.Add(legend);
            }
            foreach (var source in sources)
            {
                var seconds = source.GroupBy(line => long.Parse(line[2]) / 10000000);
                var series = new LineSeries()
                {
                    LegendKey = source.Key
                };
                series.XAxisKey = "x";
                series.YAxisKey = "y";
                series.Points.AddRange(from second in seconds select new DataPoint(Axis.ToDouble(DateTime.FromBinary(second.Key * 10000000)), second.Count()));
                model.Series.Add(series);
            }
            view!.Model = model;
        }
    }
}