using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Proxy
{
    public sealed class Program
    {
        private const string NologArgumentString = "nolog";
        public static readonly object SyncLock = new object();

        private readonly Socket _listener;
        private readonly ManualResetEvent _allDone;

        private readonly int _localPort;
        private readonly string _remoteHost;
        private readonly int _remotePort;

        private readonly bool _noLogOutput;

        public Program(string[] args)
        {
            _localPort = Convert.ToInt32(args[0]);
            _remoteHost = args[1];
            _remotePort = Convert.ToInt32(args[2]);
            _noLogOutput = args[3].ToLowerInvariant() == NologArgumentString;

            _listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _listener.Bind(new IPEndPoint(IPAddress.Any, _localPort));
            _listener.Listen(100);

            _allDone = new ManualResetEvent(false);

            Console.Title = string.Concat("TcpProxy : ", _remoteHost, ':', _remotePort);
            Program.Log($"Listening on port {_localPort} for connections");
        }

        public void Loop()
        {
            while (true) {
                _allDone.Reset();

                _listener.BeginAccept(iar => {
                    try {
                        var socket = _listener.EndAccept(iar);
                        var redirector = new Redirector(socket, _remoteHost, _remotePort, _noLogOutput);
                    }
                    catch (SocketException se) {
                        Program.Log($"Accept failed with {se.ErrorCode}", ConsoleColor.Red);
                    }
                    finally {
                        _allDone.Set();
                    }
                }, null);

                _allDone.WaitOne();
            }
        }

        public static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException +=
                (s, e) => File.WriteAllText("Exceptions.txt", e.ExceptionObject.ToString());

            if (args.Length < 3)
                Program.Log("Usage : TcpProxy <local port> <remote host> <remote port>");
            else
            {
                if (args.Length == 3) {
                    args = args.Concat(new[] {NologArgumentString}).ToArray();
                }
                new Program(args).Loop();
            }
        }

        public static void Log(string message, ConsoleColor color = ConsoleColor.White)
        {
            lock (SyncLock) {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.Write("[{0}] ", DateTime.Now);

                Console.ForegroundColor = color;
                Console.WriteLine(message);
            }
        }
    }
}