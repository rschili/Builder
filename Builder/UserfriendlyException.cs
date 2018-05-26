using System;
using System.Runtime.Serialization;

namespace RSCoreLib
    {
    [Serializable]
    public class UserfriendlyException : Exception
        {
        public UserfriendlyException ()
            {
            }
        public UserfriendlyException (string message) : base(message)
            {
            }
        public UserfriendlyException (string message, Exception inner) : base(message, inner)
            {
            }

        // This constructor is needed for serialization.
        protected UserfriendlyException (SerializationInfo info, StreamingContext context) : base(info, context)
            {
            }
        }
    }
