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

        private readonly Socket m_listener;
        private readonly ManualResetEvent m_allDone;

        private readonly int m_localPort;
        private readonly string m_remoteHost;
        private readonly int m_remotePort;

        public Program(string[] args)
        {
            m_localPort = Convert.ToInt32(args[0]);
            m_remoteHost = args[1];
            m_remotePort = Convert.ToInt32(args[2]);

            m_listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            m_listener.Bind(new IPEndPoint(IPAddress.Any, m_localPort));
            m_listener.Listen(100);

            m_allDone = new ManualResetEvent(false);

            Console.Title = string.Concat("TcpProxy : ", m_remoteHost, ':', m_remotePort);
            Program.Log($"Listening on port {m_localPort} for connections");
        }

        public void Loop()
        {
            while (true)
            {
                m_allDone.Reset();

                m_listener.BeginAccept(iar =>
                {
                    try
                    {
                        var socket = m_listener.EndAccept(iar);
                        var redirector = new Redirector(socket, m_remoteHost, m_remotePort);
                    }
                    catch (SocketException se)
                    {
                        Program.Log($"Accept failed with {se.ErrorCode}", ConsoleColor.Red);
                    }
                    finally
                    {
                        m_allDone.Set();
                    }
                }, null);

                m_allDone.WaitOne();
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
