using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static PlayStudios.Functional.Alg;

namespace PlayStudios.Functional
{
    public static class CSV
    {
        private static IEnumerable<Tuple<string, bool>> PickApartLineCSV(IEnumerator<char> iterator)
        {
            bool inQuotes = false;
            // int currentIndex = 0;
            Option<StringBuilder> accumulator = None<StringBuilder>();
            bool moveNextResult = iterator.MoveNext();
            while (moveNextResult)
            {
                char c = iterator.Current;

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if ((c == ',') && !inQuotes)
                {

                    if (accumulator.Any())
                    {
                        yield return Tuple.Create(accumulator.Value.ToString(), false);
                        accumulator = None<StringBuilder>();
                    }
                    else
                    {
                        yield return Tuple.Create("", false);
                    }
                }
                else if ((c == '\r') && !inQuotes)
                {
                    // ignore
                }
                else if ((c == '\n') && !inQuotes)
                {
                    break;
                }
                else
                {
                    if (!accumulator.Any())
                    {
                        accumulator = Some(new StringBuilder());
                    }
                    accumulator.Value.Append(c);
                }
                moveNextResult = iterator.MoveNext();
            }

            if (accumulator.Any())
            {
                yield return Tuple.Create(accumulator.Value.ToString(), !moveNextResult);
            }
        }


        // Good but not amazing implementation, ideal would be fully memoized lines+rows (e.g. perhaps based upon readpoint).
        // Don't have time to deal with it - though not a huge deal as it's only freezing full lines, not all-rows.
        // Only traverses its input once
        public static IEnumerable<IEnumerable<string>> PickApartCSVFromUTF8(byte[] contentsUTF8)
        {
            var enumerator = Encoding.UTF8.GetString(contentsUTF8).GetEnumerator();
            for (;;)
            {
                var line = PickApartLineCSV(enumerator).ToArray();
                var thisEndsIt = !line.Any() || line[line.Length - 1].Item2;
                if (line.Any())
                {
                    yield return line.Select(x => x.Item1);
                }
                if (thisEndsIt)
                {
                    break;
                }
            }
        }
    }
}
