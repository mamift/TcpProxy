using System;
using System.Net.Sockets;
using System.Threading;

namespace Proxy
{
    internal sealed class Session : IDisposable
    {
        private readonly string m_name;
        private readonly Socket m_socket;
        private readonly byte[] m_buffer;
        private int m_connected;

        public string RemoteEndPoint => m_name;
        public bool Connected => m_connected == 0;

        public Action<byte[], int> OnDataReceived { get; set; }
        public Action OnDisconnected { get; set; }

        public Session(Socket socket)
        {
            m_name = SetSockOpt(socket);
            m_socket = socket;
            m_buffer = BufferPool.Get();
            m_connected = 0;
        }
        
        public void Receive()
        {
            if (!Connected) { return; }

            m_socket.BeginReceive(m_buffer, 0, m_buffer.Length, SocketFlags.None, out var outBeginError, iar =>
            {
                if (!Connected) { return; }

                int size = m_socket.EndReceive(iar, out var outEndError);

                if (size == 0 || outEndError != SocketError.Success)
                {
                    Dispose();
                }
                else
                {
                    OnDataReceived?.Invoke(m_buffer, size);
                }
            }, null);

            if (outBeginError != SocketError.Success)
                Dispose();
        }   
        public void Send(byte[] data,int start, int length)
        {
            if (!Connected) { return; }

            var buffer = BufferPool.Get();
            Buffer.BlockCopy(data, start, buffer, 0, length);

            m_socket.BeginSend(buffer, 0, length, SocketFlags.None,out var outBeginError, iar =>
            {
                if (!Connected) { return; }

                int size = m_socket.EndSend(iar, out var outEndError);

                if (size == 0 || outEndError != SocketError.Success)
                {
                    Dispose();
                }

                BufferPool.Put(buffer);

            }, null);

            if (outBeginError != SocketError.Success)
                Dispose();
        }
        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref m_connected, 1, 0) == 0)
            {
                m_socket.Shutdown(SocketShutdown.Both);
                m_socket.Close();

                BufferPool.Put(m_buffer);

                OnDisconnected?.Invoke();

                OnDataReceived = null;
                OnDisconnected = null;
            }
        }
        
        private static string SetSockOpt(Socket socket)
        {
            if (socket != null)
            {
                try
                {
                    var temp = socket.RemoteEndPoint.ToString();

                    socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                    socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);

                    return temp;
                }
                catch (SocketException) { /*Socket No Longer Connected*/ }
            }

            return "Error";
        }
    }
}
