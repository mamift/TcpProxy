using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Proxy
{
    internal sealed class Redirector : IDisposable
    {
        private readonly string m_name;
        private readonly Session m_client;
        private Session m_server;
        private int m_alive;

        public bool Alive => m_alive == 0;

        public Redirector(Socket client, string ip, int port)
        {
            m_alive = 0;

            m_client = new Session(client)
            {
                OnDataReceived = (buffer, length) =>
                {
                    if (Alive)
                        m_server.Send(buffer,0, length);

                    Program.Log("[Send] " + Encoding.ASCII.GetString(buffer, 0, length));

                    m_client.Receive();
                },
                OnDisconnected = Dispose
            };

            m_name = m_client.RemoteEndPoint;

            Program.Log($"({m_name}) Proxy session created ", ConsoleColor.Green);

            var outSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            outSocket.BeginConnect(ip, port, iar =>
            {
                try
                {
                    outSocket.EndConnect(iar);

                    m_server = new Session(outSocket)
                    {
                        OnDataReceived = (buffer, length) =>
                        {
                            if (Alive)
                                m_client.Send(buffer,0, length);

                            Program.Log("[Recv] " + Encoding.ASCII.GetString(buffer,0,length));

                            m_server.Receive();
                        },
                        OnDisconnected = Dispose
                    };

                    m_server.Receive();
                    m_client.Receive();
                }
                catch (SocketException se)
                {
                    Program.Log($"({m_name}) Connection bridge failed with {se.ErrorCode}", ConsoleColor.Red);
                    Dispose();
                }
            }, outSocket);
        }

        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref m_alive, 1, 0) == 0)
            {
                m_client?.Dispose();
                m_server?.Dispose();
                Program.Log($"({m_name}) Proxy session ended", ConsoleColor.Cyan);
            }
        }
    }
}
