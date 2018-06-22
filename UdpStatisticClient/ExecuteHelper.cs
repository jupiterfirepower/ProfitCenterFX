using System;
using System.Threading.Tasks;

namespace UdpStatisticClient
{
    public static class ExecuteHelper
    {
        public static long Count { get; set; } = 0;
        private static DateTime? _lastCall;
        public static void ExecuteWithTimeLimitIgnore(TimeSpan timeSpan, Action codeBlock)
        {
            if (_lastCall == null)
            {
                _lastCall = DateTime.Now;
                codeBlock();
                Count++;
            }
            else if (Math.Abs(DateTime.Now.Subtract(_lastCall.Value).TotalMilliseconds) >= timeSpan.TotalMilliseconds)
            {
                _lastCall = DateTime.Now;
                codeBlock();
                Count++;
            }
        }
    }
}
