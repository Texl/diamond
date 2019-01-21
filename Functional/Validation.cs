using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PlayStudios.Functional
{
    public sealed class ValidationNotObservedException : Exception
    {
        public ValidationNotObservedException()
            : base("You must remember to call either Checkpoint or End when validating parameters!")
        {
        }
    }

    public sealed class Validation
    {
        private bool mObserved;
        private List<Exception> mExceptions;

        public Exception[] Exceptions
        {
            get
            {
                mObserved = true;
                return mExceptions.ToArray();
            }
        }

        public Validation AddException(Exception e)
        {
            lock (mExceptions)
            {
                mObserved = false;
                mExceptions.Add(e);
            }

            return this;
        }

        public Validation()
        {
            mObserved = false;
            mExceptions = new List<Exception>(1);   // optimize for one exception
        }

        ~Validation()
        {
            if (!mObserved)
            {
                throw new ValidationNotObservedException();
            }
        }
    }

    public static class Validate
    {
        public static Validation Begin()
        {
            return null;
        }
    }

    public static class ValidationExtensions
    {
        private static Validation Validate(Validation validation)
        {
            return validation ?? new Validation();
        }

        public static Validation Checkpoint(this Validation validation)
        {
            if (validation != null)
            {
                Exception[] exceptions = validation.Exceptions;

                if (exceptions.Length == 1)
                {
                    throw exceptions[0];
                }
                else
                {
                    throw new MultiException(exceptions);
                }
            }

            return validation;
        }

        public static void End(this Validation validation)
        {
            Checkpoint(validation);
        }

        public static Validation IsNotNull<T>(this Validation validation, T theObj, string paramName) where T: class
        {
            if (theObj == null)
            {
                return Validate(validation).AddException(new ArgumentNullException(paramName));
            }

            return validation;
        }

        public static Validation IsPositive(this Validation validation, int value, string paramName)
        {
            if (value <= 0)
            {
                return Validate(validation).AddException(new ArgumentOutOfRangeException(paramName, "must be positive, but was " + value.ToString()));
            }

            return validation;
        }

        public static Validation IsPositive(this Validation validation, float value, string paramName)
        {
            if (value <= 0)
            {
                return Validate(validation).AddException(new ArgumentOutOfRangeException(paramName, "must be positive, but was " + value.ToString()));
            }

            return validation;
        }

        public static Validation IsNotNegative(this Validation validation, int value, string paramName)
        {
            if (value < 0)
            {
                return Validate(validation).AddException(new ArgumentOutOfRangeException(paramName, "must not be negative, but was " + value.ToString()));
            }

            return validation;
        }

        public static Validation IsNotNegative(this Validation validation, float value, string paramName)
        {
            if (value < 0)
            {
                return Validate(validation).AddException(new ArgumentOutOfRangeException(paramName, "must not be negative, but was " + value.ToString()));
            }

            return validation;
        }

        public static Validation Validate(this Validation validation, Func<bool> fn, string message)
        {
            if (!fn())
            {
                return Validate(validation).AddException(new ArgumentException(message));
            }

            return validation;
        }
    }
}
