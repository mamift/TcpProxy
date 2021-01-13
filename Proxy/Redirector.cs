using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Proxy
{
    internal sealed class Redirector : IDisposable
    {
        private readonly bool _noLogOutput;
        private readonly string _name;
        private readonly Session _client;
        private Session _server;
        private int _alive;

        public bool Alive => _alive == 0;

        public Redirector(Socket client, string ip, int port, bool noLogOutput)
        {
            _noLogOutput = noLogOutput;
            _alive = 0;

            _client = new Session(client) {
                OnDataReceived = (buffer, length) => {
                    if (Alive)
                        _server.Send(buffer, 0, length);
                    
                    if (!_noLogOutput)
                        Program.Log("[Send] " + Encoding.ASCII.GetString(buffer, 0, length));

                    _client.Receive();
                },
                OnDisconnected = Dispose
            };

            _name = _client.RemoteEndPoint;

            Program.Log($"({_name}) Proxy session created ", ConsoleColor.Green);

            var outSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            outSocket.BeginConnect(ip, port, iar => {
                try {
                    outSocket.EndConnect(iar);

                    _server = new Session(outSocket) {
                        OnDataReceived = (buffer, length) => {
                            if (Alive)
                                _client.Send(buffer, 0, length);

                            if (!_noLogOutput)
                                Program.Log("[Recv] " + Encoding.ASCII.GetString(buffer, 0, length));

                            _server.Receive();
                        },
                        OnDisconnected = Dispose
                    };

                    _server.Receive();
                    _client.Receive();
                }
                catch (SocketException se) {
                    Program.Log($"({_name}) Connection bridge failed with {se.ErrorCode}", ConsoleColor.Red);
                    Dispose();
                }
            }, outSocket);
        }

        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref _alive, 1, 0) == 0) {
                _client?.Dispose();
                _server?.Dispose();
                Program.Log($"({_name}) Proxy session ended", ConsoleColor.Cyan);
            }
        }
    }
}