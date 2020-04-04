using System;

namespace Uchu.Core.Profiling
{
    public static partial class Performance
    {
        private class MemoryToken : IDisposable
        {
            private string Title { get; }
            
            private long Start { get; }

            public MemoryToken(string title)
            {
                Title = title;

                Start = GC.GetTotalMemory(true);
            }
            
            public void Dispose()
            {
                var end = GC.GetTotalMemory(true);

                var difference = end - Start;
                
                Logger.Debug($"{Title} consumed {difference}bytes");
            }
        }
    }
}