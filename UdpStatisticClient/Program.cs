using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MathNet.Numerics.Statistics;
using XmlConfigHelper;

namespace UdpStatisticClient
{
    class Program
    {
        private static readonly List<(int, double)> RandomValues = new List<(int, double)>();
  
        public static async void Receiver(CancellationToken token, string multicastAddress, int localPort, int timespanMiliseconds = 500)
        {
            // Создаем UdpClient для чтения входящих данных
            UdpClient receivingUdpClient = new UdpClient(localPort);

            try
            {
                IPEndPoint remoteIpEndPoint = new IPEndPoint(IPAddress.Any, 0);
                receivingUdpClient.JoinMulticastGroup(IPAddress.Parse(multicastAddress), 50);
                receivingUdpClient.Client.ReceiveTimeout = 1000;
                receivingUdpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer, 8); // for simulation package lost
                //receivingUdpClient.Client.ReceiveBufferSize = 8; // for simulation package lost

                while (!token.IsCancellationRequested)
                {
                    // Ожидание дейтаграммы
                    var result = await receivingUdpClient.ReceiveAsync().ConfigureAwait(false);
                    byte[] receiveBytes = result.Buffer;
                    var seqid = BitConverter.ToInt32(receiveBytes, 0);
                    var random = BitConverter.ToInt32(receiveBytes, 4);

                    Console.WriteLine($" --> {seqid} | {random}");

                    try
                    {
                        RandomValues.Add((seqid, Convert.ToDouble(random)));
                    }
                    catch (OutOfMemoryException)
                    {
                        RandomValues.Clear();
                        GC.Collect();
                        // Waiting till finilizer thread will call all finalizers
                        GC.WaitForPendingFinalizers();
                        RandomValues.Add((seqid, Convert.ToDouble(random)));
                    }

                    Thread.Sleep(new TimeSpan(0, 0, 0, 0, timespanMiliseconds)); // for simulation package lost
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}{Environment.NewLine}");
            }
            finally
            {
                receivingUdpClient.Close();
            }
        }

        private static long GetLostPackages()
        {
            var data = RandomValues.AsParallel().OrderBy(x => x.Item1);
            long count = 0;
            (int, double)? prev = null;

            foreach (var tpl in data)
            {
                if (prev != null && tpl.Item1 - prev?.Item1 > 1)
                {
                    count += (long)(tpl.Item1 - prev?.Item1 - 1);
                }

                prev = tpl;
            }

            return count;
        }

        public static void ShowStatistics()
        {
            Task.Factory.StartNew(() =>
            {
                try
                {
                    var data = RandomValues.AsParallel().Select(x=>x.Item2);
                    double avg = data.Average();
                    var median = data.Median();
                    var deviation = data.StandardDeviation();

                    var mode = data.AsParallel().
                        GroupBy(n => n).
                        OrderByDescending(g => g.Count()).
                        Select(g => g.Key).
                        FirstOrDefault();

                    var pkgCount = RandomValues.AsParallel().Max(x=>x.Item1);
                    var lostPackages = GetLostPackages();

                    Console.WriteLine($"Average: {avg}, Standard Deviation: {deviation}, Mode: {mode}, Median: {median}");
                    Console.WriteLine($"Packages: {pkgCount}, Lost Packages: {lostPackages} ( {((double)lostPackages / pkgCount * 100):F2}% )");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}{Environment.NewLine}");
                }
            });
        }

        public static void Start(CancellationToken token)
        {
            bool correct = true;
            var multicastAddress = XmlHelper.GetValueFromConfigByXPath(Path.Combine(
                Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location) ?? throw new InvalidOperationException(),
                "config.xml"));

            if (string.IsNullOrEmpty(multicastAddress) || string.IsNullOrWhiteSpace(multicastAddress))
            {
                Console.WriteLine("Incorrent Config parameter [multicastaddress] can't be null or empty.");
                correct = false;
            }

            var timespanMiliseconds = XmlHelper.GetTimeSpanMilisecondsByXPath(Path.Combine(
                Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location) ?? throw new InvalidOperationException(),
                "config.xml"));

            if (string.IsNullOrEmpty(timespanMiliseconds) || string.IsNullOrWhiteSpace(timespanMiliseconds))
                Console.WriteLine("Incorrent Config parameter [timespanmiliseconds] can't be null or empty.");

            // int miliseconds;
            if (correct && int.TryParse(timespanMiliseconds, out int miliseconds))
            {
                Task.Factory.StartNew(() => Receiver(token, multicastAddress, 2222, miliseconds), token);
            }
            else if(correct)
            {
                Console.WriteLine("Can't parse config parameter(from config.xml) [timespanmiliseconds] in int. Run with default(500).");
                Task.Factory.StartNew(() => Receiver(token, multicastAddress, 2222), token);
            }

            /*const int upperBoundary = (int)(Int32.MaxValue * 0.96);

            if (correct)
            {
                Action<Task> repeatAction = null;

                repeatAction = _ignored1 =>
                {
                    try
                    {
                        if (RandomValues.Count >= upperBoundary)
                            RandomValues.Clear();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error: {ex.Message}");
                    }

                    Task.Delay(new TimeSpan(0, 10, 0), token).ContinueWith(_ignored2 => repeatAction(_ignored2), token); // Repeat every 10 min
                };

                Task.Delay(5000, token).ContinueWith(repeatAction, token); // Launch with 5 sec delay
            }*/
        }

        static void Main()
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomainUnhandledException;
            TaskScheduler.UnobservedTaskException += TaskSchedulerOnUnobservedTaskException;

            Console.WriteLine("Attached to domain unhandled exception event.");
            Console.WriteLine("Udp random statistics client started.");

            var tokenSource = new CancellationTokenSource();
            var token = tokenSource.Token;

            Start(token);

            ConsoleKeyInfo cki;
            Console.TreatControlCAsInput = true;

            Console.WriteLine($"Press the Escape (Esc) key to quit: {Environment.NewLine}");
            Console.WriteLine("Press Enter to see statistics.");
            do
            {
                cki = Console.ReadKey();
                if (cki.Key == ConsoleKey.Enter)
                {
                    ShowStatistics();
                }
            } while (cki.Key != ConsoleKey.Escape);
            tokenSource.Cancel();
        }

        private static void TaskSchedulerOnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs eventArgs)
        {
            eventArgs.SetObserved();
            eventArgs.Exception.Handle(ex =>
            {
                Console.WriteLine($"Unobserved Unhandled Exception => Error: {ex.Message}{Environment.NewLine}");
                return true;
            });
        }

        private static void CurrentDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception exception)
            {
                Console.WriteLine($"Unexpected error encountered. => Error: {exception}{Environment.NewLine}");
            }
            else
            {
                Console.WriteLine($"Unexpected error encountered: => Error: {e.ExceptionObject}{Environment.NewLine}");
            }
        }
    }
}
