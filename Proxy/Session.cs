using System;
using System.Net.Sockets;
using System.Threading;

namespace Proxy
{
    internal sealed class Session : IDisposable
    {
        private readonly Socket _socket;
        private readonly byte[] _buffer;
        private int _connected;

        public string RemoteEndPoint { get; }

        public bool Connected => _connected == 0;

        public Action<byte[], int> OnDataReceived { get; set; }
        public Action OnDisconnected { get; set; }

        public Session(Socket socket)
        {
            RemoteEndPoint = SetSockOpt(socket);
            _socket = socket;
            _buffer = BufferPool.Get();
            _connected = 0;
        }

        public void Receive()
        {
            if (!Connected) {
                return;
            }

            _socket.BeginReceive(_buffer, 0, _buffer.Length, SocketFlags.None, out var outBeginError, iar => {
                if (!Connected) {
                    return;
                }

                int size = _socket.EndReceive(iar, out var outEndError);

                if (size == 0 || outEndError != SocketError.Success) {
                    Dispose();
                }
                else {
                    OnDataReceived?.Invoke(_buffer, size);
                }
            }, null);

            if (outBeginError != SocketError.Success)
                Dispose();
        }

        public void Send(byte[] data, int start, int length)
        {
            if (!Connected) {
                return;
            }

            var buffer = BufferPool.Get();
            Buffer.BlockCopy(data, start, buffer, 0, length);

            _socket.BeginSend(buffer, 0, length, SocketFlags.None, out var outBeginError, iar => {
                if (!Connected) {
                    return;
                }

                int size = _socket.EndSend(iar, out var outEndError);

                if (size == 0 || outEndError != SocketError.Success) {
                    Dispose();
                }

                BufferPool.Put(buffer);
            }, null);

            if (outBeginError != SocketError.Success)
                Dispose();
        }

        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref _connected, 1, 0) == 0) {
                _socket.Shutdown(SocketShutdown.Both);
                _socket.Close();

                BufferPool.Put(_buffer);

                OnDisconnected?.Invoke();

                OnDataReceived = null;
                OnDisconnected = null;
            }
        }

        private static string SetSockOpt(Socket socket)
        {
            if (socket != null) {
                try {
                    var temp = socket.RemoteEndPoint.ToString();

                    socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                    socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);

                    return temp;
                }
                catch (SocketException) { /*Socket No Longer Connected*/
                }
            }

            return "Error";
        }
    }
}