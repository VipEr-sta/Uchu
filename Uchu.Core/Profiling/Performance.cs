using System;

namespace Uchu.Core.Profiling
{
    public static partial class Performance
    {
        public static IDisposable Token(string title)
        {
            return new PerformanceToken(title);
        }

        public static IDisposable Memory(string title)
        {
            return new MemoryToken(title);
        }
    }
}