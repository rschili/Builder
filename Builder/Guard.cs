using System;
using System.Runtime.CompilerServices;

namespace RSCoreLib
    {
    public static class Guard
        {
        public static void NotNull (object value, [CallerMemberName] string caller = "", [CallerLineNumber] int callerLineNumber = 0)
            {
            if (value == null)
                {
                Log.Error($"Requirement: NotNull for caller {caller} line {callerLineNumber} failed.");
                throw new ArgumentNullException();
                }
            }

        public static void NotNullOrWhitespace (string value, [CallerMemberName] string caller = "", [CallerLineNumber] int callerLineNumber = 0)
            {
            if (value == null)
                {
                Log.Error($"Requirement: NotNull for caller {caller} line {callerLineNumber} failed.");
                throw new ArgumentNullException();
                }

            if (string.IsNullOrWhiteSpace(value))
                {
                Log.Error($"Requirement: NotNullOrWhitespace for caller { caller} line {callerLineNumber} failed.");
                throw new ArgumentException("String must not be null, empty or whitespaces.");
                }
            }
        }
    }
