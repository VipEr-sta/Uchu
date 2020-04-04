using System;

namespace Uchu.Core.Profiling
{
    public static partial class Performance
    {
        private class PerformanceToken : IDisposable
        {
            private string Title { get; }
            
            private DateTime Start { get; }

            public PerformanceToken(string title)
            {
                Title = title;
                
                Start = DateTime.Now;
            }
            
            public void Dispose()
            {
                var end = DateTime.Now;
                
                var elapsed = end - Start;
                
                Logger.Debug($"{Title} elapsed in {elapsed.TotalMilliseconds}ms");
            }
        }
    }
}