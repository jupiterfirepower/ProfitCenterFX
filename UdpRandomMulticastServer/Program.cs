using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XmlConfigHelper;
using static System.Console;
using static System.String;

namespace UdpRandomMulticastServer
{
    class Program
    {
        private static Thread _thread;
        private static int _seqid;

        private static async void Send(int random, string mltcastAddress, int port)
        {
            byte[] bufferSend = new byte[8];
            try
            {
                _seqid++;
                byte[] byteArray = BitConverter.GetBytes(_seqid);
                Buffer.BlockCopy(byteArray, 0, bufferSend, 0, byteArray.Length);
                byte[] byteArrayRandom = BitConverter.GetBytes(random);
                Buffer.BlockCopy(byteArrayRandom, 0, bufferSend, byteArray.Length, byteArrayRandom.Length);

                // Создаем UdpClient
                using (var sender = new UdpClient())
                {
                    var multicastAddress = IPAddress.Parse(mltcastAddress);
                    sender.JoinMulticastGroup(multicastAddress, 50);
                    // Создаем endPoint по информации об удаленном хосте
                    var remoteEndPoint = new IPEndPoint(multicastAddress, port);
                    // Отправляем данные
                    await sender.SendAsync(bufferSend, bufferSend.Length, remoteEndPoint);
                    //WriteLine($"Sent: {_seqid} | {random} ");
                }
            }
            catch (SocketException ex) when (ex.ErrorCode == 10022)
            {
            }
            catch (Exception ex)
            {
                WriteLine($"Error: {ex.Message}");
            }
        }

        static void Main()
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomainUnhandledException;
            //AppDomain.CurrentDomain.FirstChanceException += (s, arg) => WriteLine($"FirstChanceException: Exception type: {arg.Exception.GetType()} Message - {arg.Exception.Message}, StackTrace - {arg.Exception.StackTrace}");
            TaskScheduler.UnobservedTaskException += TaskSchedulerOnUnobservedTaskException;

            WriteLine("Attached to domain unhandled exception event.");
            

            var multicastAddress = XmlHelper.GetValueFromConfigByXPath(Path.Combine(
                Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location) ?? throw new InvalidOperationException(),
                "config.xml"));

            if (IsNullOrEmpty(multicastAddress) || IsNullOrWhiteSpace(multicastAddress))
            {
                WriteLine("Incorrent Config parameter [multicastaddress] can't be null or empty.");
                WriteLine("Udp random server not started. Press Enter for exit.");
                ReadLine();
                return;
            }

            var tokenSource = new CancellationTokenSource();
            var token = tokenSource.Token;

            var random = new Random(new Random().Next());

            // Start the ClientTarget thread so it is ready to receive.
            _thread = new Thread(() => {
                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        Send(random.Next(), multicastAddress, 2222);
                    }
                }
                catch (OperationCanceledException ex)
                {
                    WriteLine($"Error in thread: {ex.Message}{Environment.NewLine}");
                }
                catch (Exception ex)
                {
                    WriteLine($"Error in thread: {ex.Message}{Environment.NewLine}");
                }
            });
            
            _thread.Start();
            WriteLine("Udp random server started.");
            WriteLine("Press Enter for exit.");
            WriteLine($"The thread's state is: {_thread.ThreadState}");
            ReadLine();
            tokenSource.Cancel();
        }

        private static void TaskSchedulerOnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs eventArgs)
        {
            eventArgs.SetObserved();
            eventArgs.Exception.Handle(ex =>
            {
                WriteLine($"Unobserved Unhandled Exception => Error: {ex.Message}{Environment.NewLine}");
                return true;
            });
        }

        private static void CurrentDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception exception)
            {
                WriteLine($"Unexpected error encountered. => Error: {exception}{Environment.NewLine}");
            }
            else
            {
                WriteLine($"Unexpected error encountered: => Error: {e.ExceptionObject}{Environment.NewLine}");
            }
        }
    }
}
