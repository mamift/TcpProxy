using System.Collections.Concurrent;

namespace Proxy
{
    internal sealed class BufferPool
    {
        public const int BufferSize = 8192;

        private static readonly ConcurrentBag<byte[]> ObjectPool = new ConcurrentBag<byte[]>();

        public static byte[] Get() => ObjectPool.TryTake(out var buffer) ? buffer : new byte[BufferSize];
        public static void Put(byte[] buffer) => ObjectPool.Add(buffer);
    }
}
