using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PlayStudios.Functional
{
    public static class SpreadsheetRelated
    {
        public static IEnumerable<char> BytesToChars(IEnumerable<byte> bytes)
        {
            return bytes.Select(x => (char)x);
        }

        public static IEnumerable<byte> CharsToBytes(IEnumerable<char> chars)
        {
            return chars.Select(x => (byte)x);
        }

        public static IEnumerable<string> CharsToLines(IEnumerable<char> chars)
        {
            List<char> accumulator = new List<char>();
            ReadPoint<char> currentChar = Alg.ReadPoint(chars);
            while (!currentChar.AtEnd)
            {
                char c = currentChar.Value;
                if (c == '\n')
                {
                    yield return Alg.MergedChars(accumulator);
                    accumulator = new List<char>();
                }
                else
                    if (c == '\r')
                    {   // have to handle '\r'-only case.

                        if (!currentChar.Next.AtEnd)
                        {
                            if (currentChar.Next.Value != '\n')
                            {
                                yield return Alg.MergedChars(accumulator);
                                accumulator = new List<char>();
                            }
                        }
                    }
                    else
                    {
                        accumulator.Add(c);
                    }
                currentChar = currentChar.Next;

            }

            if (accumulator.Count != 0)
            {
                yield return Alg.MergedChars(accumulator);
                accumulator = new List<char>();
            }
        }

        private static bool IsAtCSVQuote(ReadPoint<char> currentReadPoint)
        {
            ReadPoint<char> curr = currentReadPoint;
            for (int i = 0; i < 1; ++i)
            {
                if (curr.Next.AtEnd) { return false; }
                if (curr.Next.Value != '\"') { return false; }
                curr = curr.Next;
            }
            return true;
        }

        public static IEnumerable<string> LineToCSFEntries(string line, char delimiter, bool quotesHaveMeaning)
        {
            ReadPoint<char> currentChar = Alg.ReadPoint(line.ToCharArray());
            List<char> accumulatedElement = new List<char>();
            bool inQuote = false;
            bool haveSeenAComma = false;
            while (!currentChar.AtEnd)
            {
                char c = currentChar.Value;
                if (inQuote)
                {
                    if (c == '\"')
                    {   // may be a double-quote
                        if (IsAtCSVQuote(currentChar))
                        {
                            accumulatedElement.Add('\"');
                            currentChar = currentChar.Next; // next add will be done at end of this loop.
                        }
                        else
                        {
                            accumulatedElement.Add(c);
                            inQuote = false;
                        }
                    }
                    else
                    {
                        accumulatedElement.Add(c);
                    }
                }
                else
                {
                    if (c == delimiter)
                    {
                        haveSeenAComma = true;
                        yield return Alg.MergedChars(accumulatedElement).Trim();
                        accumulatedElement = new List<char>();
                    }
                    else
                    {
                        if (quotesHaveMeaning && (c == '\"'))
                        {
                            inQuote = true;
                            accumulatedElement.Add(c);
                        }
                        else
                        {
                            accumulatedElement.Add(c);
                        }
                    }
                }
                currentChar = currentChar.Next;
            }
            if ((accumulatedElement.Count != 0) || haveSeenAComma)
            {
                yield return Alg.MergedChars(accumulatedElement).Trim();   // deliberately return "last"
                accumulatedElement = new List<char>();
            }
        }

        public static IEnumerable<IEnumerable<string>> LinesToCSFEntries(IEnumerable<string> lines, char delimiter, bool quotesHaveMeaning)
        {
            return lines.Select(line => LineToCSFEntries(line, delimiter, quotesHaveMeaning));
        }

        public static IEnumerable<IEnumerable<string>> PullPureCSVRows(IEnumerable<byte> fileContents, char delimiter, bool quotesHaveMeaning)
        {
            IEnumerable<IEnumerable<string>> r = LinesToCSFEntries(CharsToLines(BytesToChars(fileContents)), delimiter, quotesHaveMeaning);
            return r;
        }
        public static IEnumerable<IEnumerable<string>> PullPureCSVRows(IEnumerable<byte> fileContents, char delimiter) // defaults quotesHaveMeaning to true
        {
            return PullPureCSVRows(fileContents, delimiter, true);
        }

        public static string StringFromFile(IEnumerable<byte> fileContents)
        {
            return Alg.MergedChars(BytesToChars(fileContents));
        }

        public static string LineFromCSVRow(IEnumerable<string> csvRow)
        {
            return Alg.MergedStrings(Alg.Intersperse<string>(",", csvRow));
        }

        public static IEnumerable<string> LinesFromCSVRows(IEnumerable<IEnumerable<string>> csvRows)
        {
            return csvRows.Select(LineFromCSVRow);
        }

    }
}
