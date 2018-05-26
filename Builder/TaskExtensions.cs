using System;
using System.Threading.Tasks;

namespace RSCoreLib
    {
    /// <summary>
    /// Wraps the common task of Logging Exceptions which might happen during a task. When firing and forgetting about a task, calling this
    /// will ensure the exceptions don't get lost.
    /// </summary>
    public static class TaskExtensions
        {
        public static void SwallowAndLogExceptions (this Task t)
            {
            if (t == null)
                return;

            t.ContinueWith((t2) => Log.Error("Exception during async operation. {0}", t.Exception), TaskContinuationOptions.OnlyOnFaulted);
            }
        }

    /// <summary>
    /// Central helper class for random delays to test UI responsiveness.
    /// </summary>
    public static class TaskHelper
        {
        private static Lazy<Random> s_random = new Lazy<Random>();
        public async static Task RandomDelay (string description)
            {
            int x = s_random.Value.Next(1, 3000);
            Log.Information("Delaying {0} by {1:0.##} seconds", description, (double)x / 1000);
            await Task.Delay(x);
            }
        }
    }
