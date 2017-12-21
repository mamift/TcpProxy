using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Proxy
{
    public sealed class Program
    {
        public static readonly object SyncLock = new object();

        private readonly Socket listener;
        private readonly ManualResetEvent allDone;

        private int localPort;
        private string remoteHost;
        private int remotePort;

        public Program(string[] args)
        {
            localPort = Convert.ToInt32(args[0]);
            remoteHost = args[1];
            remotePort = Convert.ToInt32(args[2]);

            listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(new IPEndPoint(IPAddress.Any, localPort));
            listener.Listen(100);

            allDone = new ManualResetEvent(false);

            Console.Title = string.Concat("TcpProxy : ", remoteHost, ':', remotePort);
            Program.Log($"Listening on port {localPort} for connections");
        }

        public void Loop()
        {
            while (true)
            {
                allDone.Reset();
                listener.BeginAccept(iar =>
                {
                    allDone.Set();

                    try
                    {
                        var socket = listener.EndAccept(iar);
                        var redirector = new Redirector(socket, remoteHost, remotePort);
                    }
                    catch (SocketException se)
                    {
                        Program.Log($"Accept failed with {se.ErrorCode}", ConsoleColor.Red);
                    }
                }, null);
                allDone.WaitOne();
            }
        }

        public static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) => File.WriteAllText("Exceptions.txt", e.ExceptionObject.ToString());

            if (args.Length < 3)
                Program.Log("Usage : TcpProxy <local port> <remote host> <remote port>");
            else
                new Program(args).Loop();
        }

        public static void Log(string message,ConsoleColor color = ConsoleColor.White)
        {
            lock (SyncLock)
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.Write("[{0}] ", DateTime.Now);

                Console.ForegroundColor = color;
                Console.WriteLine(message);
            }
        }
    }
}
