using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.Runtime.InteropServices;
#if FEATURE_ASYNC_IO
using System.Threading.Tasks;
#endif


namespace PlayStudios.Functional
{
    /// <summary>
    /// Takes the place of a string by representing the subset of a larger string
    /// .ToString() will build a new string; but the intent is that it be used without doing so.
    /// Equality and comparison are based upon the focussed string not the fields.
    /// </summary>
    public sealed class StringSource : IComparable<StringSource>
    {
        public StringSource(string resourceString, int startOffset, int length)
        {
            ResourceString = resourceString;
            StartOffset = startOffset;
            Length = length;
        }
        public readonly string ResourceString;
        public readonly int StartOffset;
        public readonly int Length;

        public int CompareTo(StringSource other)
        {
            if (other.Length != Length)
            {
                return Length.CompareTo(other.Length);
            }
            for (int i = 0; i < Length; ++i)
            {
                var c = ResourceString[i + StartOffset].CompareTo(other.ResourceString[i + other.StartOffset]);
                if (c != 0)
                {
                    return c;
                }
            }
            return 0;
        }

        public override string ToString() =>
            ((StartOffset == 0) && (Length == ResourceString.Length))
            ? ResourceString // optimization for redundant StringSource
            : ResourceString.Substring(StartOffset, Length);

        public static StringSource FromDirectString(string str) => new StringSource(str, 0, str.Length);

        public override bool Equals(object obj)
        {
            if (obj is StringSource)
            {
                StringSource other = (StringSource)obj;
                if (other.Length != Length)
                {
                    return false;
                }
                for (int i = 0; i < Length; ++i)
                {
                    if (ResourceString[i + StartOffset] != other.ResourceString[i + other.StartOffset])
                    {
                        return false;
                    }
                }
                return true;
            }
            return false;
        }

        public override int GetHashCode()
        {
            int r = 0;
            for (int i = 0; i < StartOffset; ++i)
            {
                r ^= ((- i).GetHashCode() ^ ResourceString[i + StartOffset]) ^ (2 << (i % 29));
            }
            return r ^ Length.GetHashCode();
        }

        public TextReader GetTextReader() => new SStringReader(ResourceString, StartOffset, Length);

        [Serializable]
        [ComVisible(true)]
        private class SStringReader : TextReader // copied out of StringReader
        {
            private String _s;
            private int _pos;
            private int _length;
            private int _endPos;

            public SStringReader(string s, int initialPos, int length) // so a subset can be marked out.
            {
                if (s == null)
                    throw new ArgumentNullException("s");
                Contract.EndContractBlock();
                _s = s;
                _length = length;
                _pos = initialPos;
                _endPos = initialPos + length;
            }

            // Closes this StringReader. Following a call to this method, the String
            // Reader will throw an ObjectDisposedException.
            public override void Close()
            {
                Dispose(true);
            }

            protected override void Dispose(bool disposing)
            {
                _s = null;
                _pos = 0;
                _length = 0;
                base.Dispose(disposing);
            }

            // Returns the next available character without actually reading it from
            // the underlying string. The current position of the StringReader is not
            // changed by this operation. The returned value is -1 if no further
            // characters are available.
            //
            [Pure]
            public override int Peek()
            {
                if (_pos == _length) return -1;
                return _s[_pos];
            }

            // Reads the next character from the underlying string. The returned value
            // is -1 if no further characters are available.
            //
            public override int Read()
            {
                if (_pos == _endPos) return -1;
                return _s[_pos++];
            }

            // Reads a block of characters. This method will read up to count
            // characters from this StringReader into the buffer character
            // array starting at position index. Returns the actual number of
            // characters read, or zero if the end of the string is reached.
            //
            public override int Read([In, Out] char[] buffer, int index, int count)
            {
                Contract.EndContractBlock();

                int n = _endPos - _pos;
                if (n > 0)
                {
                    if (n > count) n = count;
                    _s.CopyTo(_pos, buffer, index, n);
                    _pos += n;
                }
                return n;
            }

            public override String ReadToEnd()
            {
                String s;
                if (_pos == 0)
                    s = _s;
                else
                    s = _s.Substring(_pos, _endPos - _pos);
                _pos = _endPos;
                return s;
            }

            // Reads a line. A line is defined as a sequence of characters followed by
            // a carriage return ('\r'), a line feed ('\n'), or a carriage return
            // immediately followed by a line feed. The resulting string does not
            // contain the terminating carriage return and/or line feed. The returned
            // value is null if the end of the underlying string has been reached.
            //
            public override String ReadLine()
            {
                int i = _pos;
                while (i < _endPos)
                {
                    char ch = _s[i];
                    if (ch == '\r' || ch == '\n')
                    {
                        String result = _s.Substring(_pos, i - _pos);
                        _pos = i + 1;
                        if (ch == '\r' && _pos < _endPos && _s[_pos] == '\n') _pos++;
                        return result;
                    }
                    i++;
                }
                if (i > _pos)
                {
                    String result = _s.Substring(_pos, i - _pos);
                    _pos = i;
                    return result;
                }
                return null;
            }

#if FEATURE_ASYNC_IO
            #region Task based Async APIs
        [ComVisible(false)]
        public override Task<String> ReadLineAsync()
        {
            return Task.FromResult(ReadLine());
        }

        [ComVisible(false)]
        public override Task<String> ReadToEndAsync()
        {
            return Task.FromResult(ReadToEnd());
        }

        [ComVisible(false)]
        public override Task<int> ReadBlockAsync(char[] buffer, int index, int count)
        {
            if (buffer==null)
                throw new ArgumentNullException("buffer", Environment.GetResourceString("ArgumentNull_Buffer"));
            if (index < 0 || count < 0)
                throw new ArgumentOutOfRangeException((index < 0 ? "index" : "count"), Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegNum"));
            if (buffer.Length - index < count)
                throw new ArgumentException(Environment.GetResourceString("Argument_InvalidOffLen"));

            Contract.EndContractBlock();

            return Task.FromResult(ReadBlock(buffer, index, count));
        }

        [ComVisible(false)]
        public override Task<int> ReadAsync(char[] buffer, int index, int count)
        {
            if (buffer==null)
                throw new ArgumentNullException("buffer", Environment.GetResourceString("ArgumentNull_Buffer"));
            if (index < 0 || count < 0)
                throw new ArgumentOutOfRangeException((index < 0 ? "index" : "count"), Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegNum"));
            if (buffer.Length - index < count)
                throw new ArgumentException(Environment.GetResourceString("Argument_InvalidOffLen"));
            Contract.EndContractBlock();

            return Task.FromResult(Read(buffer, index, count));
        }
            #endregion
#endif //FEATURE_ASYNC_IO
        }
    }

}
