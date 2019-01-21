using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace PlayStudios.Functional
{
    [Serializable]
    public sealed class MultiException
        : Exception
    {
        private Exception[] mInnerExceptions;

        public IEnumerable<Exception> InnerExceptions
        {
            get
            {
                if (mInnerExceptions != null)
                {
                    for (int i = 0; i < mInnerExceptions.Length; ++i)
                    {
                        yield return mInnerExceptions[i];
                    }
                }
            }
        }

        public MultiException()
        {
        }

        public MultiException(string message)
            : base(message)
        {
        }

        public MultiException(string message, Exception innerException)
            : base(message, innerException)
        {
            mInnerExceptions = new Exception[1] { innerException };
        }

        public MultiException(IEnumerable<Exception> innerExceptions)
            : this(null, innerExceptions)
        {
        }

        public MultiException(Exception[] innerExceptions)
            : this(null, (IEnumerable<Exception>)innerExceptions)
        {
        }

        public MultiException(string message, Exception[] innerExceptions)
            : this(message, (IEnumerable<Exception>)innerExceptions)
        {
        }

        public MultiException(string message, IEnumerable<Exception> innerExceptions)
            : base(message, innerExceptions.FirstOrDefault())
        {
            if (innerExceptions.Any(item => item == null))
            {
                throw new ArgumentNullException();
            }

            mInnerExceptions = innerExceptions.ToArray();
        }

        private MultiException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
