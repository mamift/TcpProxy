using System;
using System.Collections.Concurrent;

namespace Proxy
{
    internal sealed class BufferPool
    {
        public const int BufferSize = 8192;

        private static ConcurrentBag<byte[]> ObjectPool = new ConcurrentBag<byte[]>();

        public static byte[] Get() => ObjectPool.TryTake(out var buffer) ? buffer : new byte[BufferSize];

        public static void Put(byte[] buffer)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if (buffer.Length != BufferSize)
                throw new ArgumentOutOfRangeException(nameof(buffer));

            ObjectPool.Add(buffer);
        }
    }
}
