using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Seizure.Net.Server.Connections;

namespace Seizure
{
    static class Program
    {
        static IServiceProvider _services;
        static Logger _log = LogManager.GetLogger("Main");
        static CancellationTokenSource _cts = new CancellationTokenSource();

        public static X509Certificate2 GetTlsCertificate()
        {
            X509Certificate2 temp = null;

            lock (_services)
                temp = _services.GetService<X509Certificate2>();

            return temp;
        }

        static void Main(string[] args)
        {
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;

                if (!_cts.IsCancellationRequested)
                    _cts.Cancel();
            };

            var error = false;

            try
            {
                var temp = new ServiceCollection()
                .AddSingleton<IXmppListener, TcpXmppListener>();

                var path = Path.Combine(Directory.GetCurrentDirectory(), "Seizure.pfx");

                if (!File.Exists(path))
                {
                    throw new FileNotFoundException("SSL certificate was not found.", path);
                }

                var certificate = new X509Certificate2(path, "urn:cryonline:k01");
                temp.AddSingleton(certificate);

                _services = temp.BuildServiceProvider();

                var listener = _services.GetService<IXmppListener>();
                listener.StartListen();

                while (!_cts.IsCancellationRequested)
                    Thread.Sleep(1);

                listener.StopListen();
            }
            catch(Exception ex)
            {
                error = true;
                _log.Fatal(ex, "Main(): seizure initialization failure.\n");
            }
            finally
            {
                if (error)
                    Console.ReadLine();
            }
        }
    }
}