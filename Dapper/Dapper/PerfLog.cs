using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dapper
{
    /// <summary>
    /// Logs performance of actions. To use, set a TextWriter in Logger and measurements will be written there.
    /// </summary>
    public class PerfLog
    {
        /// <summary>
        /// Logger will write in this stream.
        /// </summary>
        public static Action<string> Logger = null;

        internal static void Do(string msg, Action callbackToMeasure)
        {
            if (Logger == null)
            {
                callbackToMeasure();
                return;
            }

            var timer = Stopwatch.StartNew();
            callbackToMeasure();
            timer.Stop();

            Log(msg, timer.ElapsedMilliseconds);
        }

        private static void Log(string msg, long timeInMs)
        {
            Logger($"{msg}   |{timeInMs}ms|");
        }
    }
}
