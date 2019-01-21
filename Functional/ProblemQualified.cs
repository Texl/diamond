using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations; // replacement for System.Diagnostics.Contracts to get alternate PureAttribute - in attempt to get [Pure] to be seen by resharper
using Microsoft.FSharp.Core;
using static PlayStudios.Functional.Alg;

namespace PlayStudios.Functional
{
    public sealed class ProblemSink
    {
        private ProblemSink(Action<Array1<string>> reportTerminals, Action<string> reportNonTerminal)
        {
            ReportTerminals = reportTerminals;
            ReportNonTerminal = reportNonTerminal;
        }

        public static ProblemSink FromTerminalNonTerminal(Action<string> reportTerminal, Action<string> reportNonTerminal) =>
            new ProblemSink(s => reportTerminal(MergedStrings(Intersperse("\r\n", s.ToEnumerable()))), reportNonTerminal);

        public readonly Action<Array1<string>> ReportTerminals;
        public readonly Action<string> ReportNonTerminal;

        public void ReportTerminal(string terminal) => ReportTerminals(Array1.Build(terminal));

        // So commonly done in tests and others that just want to chuck out failure, putting it here.
        public static readonly ProblemSink ThrowOnErrorOrWarnings =
            FromTerminalNonTerminal(
                e => throw new Exception(e),
                e => throw new Exception(e));
    }

    public sealed class ProblemQualified<T>
    {
        private ProblemQualified(Either<Array1<string>, T> terminalOrValue, IEnumerable<string> nonTerminalWarnings)
        {
            NonTerminalWarnings = nonTerminalWarnings.ToArray();
            TerminalOrValue = terminalOrValue;
        }

        public static ProblemQualified<T> ForTerminal(string terminal) => new ProblemQualified<T>(Either<Array1<string>, T>.FromLeft(Array1.Build(terminal)), Enumerable.Empty<string>());
        public static ProblemQualified<T> ForTerminal(string terminal, IEnumerable<string> nonTerminalWarnings) => new ProblemQualified<T>(Either<Array1<string>, T>.FromLeft(Array1.Build(terminal)), nonTerminalWarnings);

        public static ProblemQualified<T> ForTerminal(Array1<string> terminal, IEnumerable<string> nonTerminalWarnings) => new ProblemQualified<T>(Either<Array1<string>, T>.FromLeft(terminal), nonTerminalWarnings);

        public static ProblemQualified<T> Success(T t) => new ProblemQualified<T>(Either<Array1<string>, T>.FromRight(t), Enumerable.Empty<string>());

        public static ProblemQualified<T> Success(T t, IEnumerable<string> nonTerminalWarnings) => new ProblemQualified<T>(Either<Array1<string>, T>.FromRight(t), nonTerminalWarnings);

        private readonly string[] NonTerminalWarnings;
        private readonly Either<Array1<string>, T> TerminalOrValue;

        public Tuple<Either<U, T>, string[]> AsSuccessOrFailure<U>(Func<Array1<string>, U> forFailure) => Tuple.Create(TerminalOrValue.Map(forFailure, t => t), NonTerminalWarnings);


        // Cardinal bind function
        public ProblemQualified<U> SelectMany<U>(Func<T, ProblemQualified<U>> f) => SelectMany(f, (a, b) => b);

        [Pure]
        public ProblemQualified<V> SelectMany<U, V>(Func<T, ProblemQualified<U>> tu, Func<T, U, V> tuv) =>
            TerminalOrValue.Match(
                term => new ProblemQualified<V>(Either<Array1<string>, V>.FromLeft(term), NonTerminalWarnings),
                val =>
                    tu(val).Let(nq => new ProblemQualified<V>(nq.TerminalOrValue.Map(x => x, x => tuv(val, x)), NonTerminalWarnings.Concat(nq.NonTerminalWarnings))));


        public ProblemQualified<U> Select<U>(Func<T, U> f) => SelectMany(t => ProblemQualified<U>.Success(f(t)));

        // Handy to have in the kind of code calling FMap
        // Reconsider this upon making SQL syntax available - as we no longer have to demolish a scope variable
        public ProblemQualified<U> FMapS<U>(Func<U> f) => SelectMany(t => ProblemQualified<U>.Success(f()));


        // Here for handiness - a common case, a bool and a fatal.  If already fatal, don't add the new fatal, only bind in for successful value
        public ProblemQualified<T> BindTrivialFatalCheck(bool isFatal, Func<string> getFatalError) => BindTrivialFatalCheck(_ => isFatal, _ => getFatalError());

        public ProblemQualified<T> BindTrivialFatalCheck(Func<T, bool> isFatal, Func<T, string> getFatalError) => TerminalOrValue.Match(_ => this, v => isFatal(v) ? ForTerminal(getFatalError(v)) : this);

        public ProblemQualified<U> BindTrivialPresenceCheck<U>(Func<T, Option<U>> f, Func<string> getFatalError) =>
            SelectMany(
                t =>
                {
                    var candidate = f(t);
                    return BindTrivialFatalCheck(!candidate.HasValue, getFatalError).Select(_ => candidate.Value);
                });

        public IEnumerable<string> GetQualifiedNonTerminalWarnings(Func<string, string> qualified) => NonTerminalWarnings.Select(qualified);

        // yucky - a method - but whatever
        public T ReconciledWithProblemSink(ProblemSink problemSink)
        {
            NonTerminalWarnings.ForEach(problemSink.ReportNonTerminal);
            return TerminalOrValue.Match(
                e =>
                {
                    problemSink.ReportTerminals(e);
                    return default(T);
                },
                t => t);
        }

        public U Match<U>(Func<T, string[], U> ifSuccess, Func<Array1<string>, string[], U> ifFailure) =>
            TerminalOrValue.Match(ss => ifFailure(ss, NonTerminalWarnings), t => ifSuccess(t, NonTerminalWarnings));

        public U Match<U>(Func<T, U> ifSuccess, Func<Array1<string>, U> ifFailure) =>
            TerminalOrValue.Match(ifFailure, ifSuccess);

        public void Apply(Action<T, string[]> ifSuccess, Action<Array1<string>, string[]> ifFailure) =>
            TerminalOrValue.Apply(ss => ifFailure(ss, NonTerminalWarnings), t => ifSuccess(t, NonTerminalWarnings));

        public void Apply(Action<T> ifSuccess, Action<Array1<string>> ifFailure) =>
            TerminalOrValue.Apply(ifFailure, ifSuccess);

        public void ForEach(Action<T, string[]> ifSuccess) =>
            TerminalOrValue.RightIfPresent().ForEach(t => ifSuccess(t, NonTerminalWarnings));

        public void ForEach(Action<T> ifSuccess) =>
            TerminalOrValue.RightIfPresent().ForEach(ifSuccess);
    }

    /// <summary>
    /// These are here specifically for "Success" - since the type inference on the Generic comes from the input param,
    /// it saves you having to type it in again
    /// </summary>
    public static class ProblemQualified
    {
        public static ProblemQualified<T> Success<T>(T t) => ProblemQualified<T>.Success(t);

        public static ProblemQualified<T> Success<T>(T t, IEnumerable<string> nonTerminalWarnings) => ProblemQualified<T>.Success(t, nonTerminalWarnings);

        public static ProblemQualified<T> ForTerminal<T>(string error) => ProblemQualified<T>.ForTerminal(error);
        public static ProblemQualified<T> ForTerminal<T>(string error, IEnumerable<string> nonTerminalWarnings) => ProblemQualified<T>.ForTerminal(error, nonTerminalWarnings);
        public static ProblemQualified<T> ForTerminal<T>(Array1<string> errors, IEnumerable<string> nonTerminalWarnings) => ProblemQualified<T>.ForTerminal(errors, nonTerminalWarnings);

        public static ProblemQualified<T> FromTrivialPresenceCheck<T>(Option<T> candidate, Func<string> getFatalError) => Success(UnitValue).BindTrivialPresenceCheck(_ => candidate, getFatalError);

        public static ProblemQualified<Unit> FromFatalCheckToInert(bool isFatal, Func<string> getFatalError) => Success(UnitValue).BindTrivialFatalCheck(isFatal, getFatalError);

        public static ProblemQualified<T> FromValidInvalid<T>(Either<T, string> e) => e.Match(Success, ForTerminal<T>);

        public static ProblemQualified<T> ExceptionIsFatal<T>(Func<T> f)
        {
            try
            {
                return Success(f());
            }
            catch (Exception ex)
            {
                return ForTerminal<T>(ex.ToString());
            }
        }

        public static ProblemQualified<T[]> ChainedUp<T>(IEnumerable<ProblemQualified<T>> pqs)
        {
            var fatals = new List<Array1<string>>();
            var warningSets = new List<string[]>();
            var results = new List<T>(); // that will only be used if there are no fatals
            foreach (var pq in pqs)
            {
                var successOrFailure = pq.AsSuccessOrFailure(x => x);
                warningSets.Add(successOrFailure.Item2);
                successOrFailure.Item1.Apply(fatals.Add, results.Add);
            }

            if (fatals.Any())
            {
                return ProblemQualified<T[]>.ForTerminal(Array1.Insist(fatals.SelectMany(x => x.ToEnumerable())), warningSets.SelectMany(x => x));
            }
            return Success(results.ToArray(), warningSets.SelectMany(x => x));
        }

        public static ProblemQualified<R> Composed<T1, T2, R>(ProblemQualified<T1> a, ProblemQualified<T2> b, Func<T1, T2, R> f)
        {
            var result = ChainedUp(new[] { a.Select(x => (object)x), b.Select(x => (object)x) });
            return result.Select(r => f((T1)r[0], (T2)r[1]));
        }

        public static ProblemQualified<R> Composed<T1, T2, T3, R>(ProblemQualified<T1> a, ProblemQualified<T2> b, ProblemQualified<T3> c, Func<T1, T2, T3, R> f)
        {
            var result = ChainedUp(new[] { a.Select(x => (object)x), b.Select(x => (object)x), c.Select(x => (object)x) });
            return result.Select(r => f((T1)r[0], (T2)r[1], (T3)r[2]));
        }

        public static ProblemQualified<R> Composed<T1, T2, T3, T4, R>(ProblemQualified<T1> a, ProblemQualified<T2> b, ProblemQualified<T3> c, ProblemQualified<T4> d, Func<T1, T2, T3, T4, R> f)
        {
            var result = ChainedUp(new[] { a.Select(x => (object)x), b.Select(x => (object)x), c.Select(x => (object)x), d.Select(x => (object)x) });
            return result.Select(r => f((T1)r[0], (T2)r[1], (T3)r[2], (T4)r[3]));
        }

        public static ProblemQualified<R> Composed<T1, T2, T3, T4, T5, R>(ProblemQualified<T1> a, ProblemQualified<T2> b, ProblemQualified<T3> c, ProblemQualified<T4> d, ProblemQualified<T5> e, Func<T1, T2, T3, T4, T5, R> f)
        {
            var result = ChainedUp(new[] { a.Select(x => (object)x), b.Select(x => (object)x), c.Select(x => (object)x), d.Select(x => (object)x), e.Select(x => (object)x) });
            return result.Select(r => f((T1)r[0], (T2)r[1], (T3)r[2], (T4)r[3], (T5)r[4]));
        }

        public static ProblemQualified<R> Composed<T1, T2, T3, T4, T5, T6, R>(ProblemQualified<T1> t1, ProblemQualified<T2> t2, ProblemQualified<T3> t3, ProblemQualified<T4> t4, ProblemQualified<T5> t5, ProblemQualified<T6> t6, Func<T1, T2, T3, T4, T5, T6, R> f)
        {
            var result = ChainedUp(new[] { t1.Select(x => (object)x), t2.Select(x => (object)x), t3.Select(x => (object)x), t4.Select(x => (object)x), t5.Select(x => (object)x), t6.Select(x => (object)x) });
            return result.Select(r => f((T1)r[0], (T2)r[1], (T3)r[2], (T4)r[3], (T5)r[4], (T6)r[5]));
        }

        public static ProblemQualified<R> Composed<T1, T2, T3, T4, T5, T6, T7, R>(ProblemQualified<T1> t1, ProblemQualified<T2> t2, ProblemQualified<T3> t3, ProblemQualified<T4> t4, ProblemQualified<T5> t5, ProblemQualified<T6> t6, ProblemQualified<T7> t7, Func<T1, T2, T3, T4, T5, T6, T7, R> f)
        {
            var result = ChainedUp(new[] { t1.Select(x => (object)x), t2.Select(x => (object)x), t3.Select(x => (object)x), t4.Select(x => (object)x), t5.Select(x => (object)x), t6.Select(x => (object)x), t7.Select(x => (object)x) });
            return result.Select(r => f((T1)r[0], (T2)r[1], (T3)r[2], (T4)r[3], (T5)r[4], (T6)r[5], (T7)r[6]));
        }

        public static ProblemQualified<R> Composed<T1, T2, T3, T4, T5, T6, T7, T8, R>(ProblemQualified<T1> t1, ProblemQualified<T2> t2, ProblemQualified<T3> t3, ProblemQualified<T4> t4, ProblemQualified<T5> t5, ProblemQualified<T6> t6, ProblemQualified<T7> t7, ProblemQualified<T8> t8, Func<T1, T2, T3, T4, T5, T6, T7, T8, R> f)
        {
            var result = ChainedUp(new[] { t1.Select(x => (object)x), t2.Select(x => (object)x), t3.Select(x => (object)x), t4.Select(x => (object)x), t5.Select(x => (object)x), t6.Select(x => (object)x), t7.Select(x => (object)x), t8.Select(x => (object)x) });
            return result.Select(r => f((T1)r[0], (T2)r[1], (T3)r[2], (T4)r[3], (T5)r[4], (T6)r[5], (T7)r[6], (T8)r[7] ));
        }

        public static ProblemQualified<R> Composed<T1, T2, T3, T4, T5, T6, T7, T8, T9, R>(ProblemQualified<T1> t1, ProblemQualified<T2> t2, ProblemQualified<T3> t3, ProblemQualified<T4> t4, ProblemQualified<T5> t5, ProblemQualified<T6> t6, ProblemQualified<T7> t7, ProblemQualified<T8> t8, ProblemQualified<T9> t9, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, R> f)
        {
            var result = ChainedUp(new[] { t1.Select(x => (object)x), t2.Select(x => (object)x), t3.Select(x => (object)x), t4.Select(x => (object)x), t5.Select(x => (object)x), t6.Select(x => (object)x), t7.Select(x => (object)x), t8.Select(x => (object)x), t9.Select(x => (object)x) });
            return result.Select(r => f((T1)r[0], (T2)r[1], (T3)r[2], (T4)r[3], (T5)r[4], (T6)r[5], (T7)r[6], (T8)r[7], (T9)r[8]));
        }

        // Really, just to get the func around it in an array
        public static Func<Option<Array1<string>>> DeferredPossibleFatal(Func<Option<Array1<string>>> getPossibleFatal) => getPossibleFatal;

        // Utility solely to get ability to chain up fatality; but not have to specify the return type on the failure explicitly.
        // I.e. solely to get type inference, which only providing the success as well can provide
        public static ProblemQualified<T> FatalOrSuccessBind<T>(Func<Option<Array1<string>>> getPossibleFatal, Func<ProblemQualified<T>> getOnNotPossibleFatal) =>
            getPossibleFatal().Match(x => ProblemQualified<T>.ForTerminal(x, new string[] {}), getOnNotPossibleFatal);


        // First fatal ends it - don't run the rest
        public static ProblemQualified<T> FatalOrSuccessBindShortCircuitFatals<T>(IEnumerable<Func<Option<Array1<string>>>> getPossibleFatals, Func<ProblemQualified<T>> getOnNotPossibleFatal) =>
            getPossibleFatals.Select(x => x()).SkipWhile(x => ! x.HasValue).Take(1).ToOption().SelectMany(x => x).Match(x => ProblemQualified<T>.ForTerminal(x, new string[] { }), getOnNotPossibleFatal);

        public static ProblemQualified<T> FatalOrSuccessBindAggregateFatals<T>(IEnumerable<Func<Option<Array1<string>>>> getPossibleFatals, Func<ProblemQualified<T>> getOnNotPossibleFatal) =>
            getPossibleFatals.Select(x => x()).SelectMany(x => x).SelectMany(x => x.ToEnumerable()) // aggregate all possible lines
            .Let(Array1.Contingent) // contingent Array1 - if there's anything in there.
            .Match(x => ProblemQualified<T>.ForTerminal(x, new string[] { }), getOnNotPossibleFatal);

        // public static ProblemQualified<T> FatalOrSuccessMap<T>(Func<Option<Array1<string>>> getPossibleFatal, Func<T> getOnNotPossibleFatal) => FatalOrSuccessBind(getPossibleFatal, () => Success(getOnNotPossibleFatal()));


        /// <summary>
        /// Deals with the, IMHO common case of something where, if present, you want qualifying tests; but if absent just success
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="U"></typeparam>
        /// <param name="oi"></param>
        /// <param name="f"></param>
        /// <returns></returns>
        public static ProblemQualified<Option<U>> SpinBound<T, U>(ProblemQualified<Option<T>> oi, Func<T, ProblemQualified<U>> f) => oi.SelectMany(o => o.Match(x => f(x).Select(Some), () => Success(None<U>())));

        public static ProblemQualified<Option<Tuple<I0, I1>>> Aligned<I0, I1>(Option<I0> i0, Option<I1> i1, Func<I0, string> onFirstPresentOnly, Func<I1, string> onSecondPresentOnly) =>
            i0.Match(
                v0 => i1.Match(v1 => Success(Some(Tuple.Create(v0, v1))), () => ProblemQualified<Option<Tuple<I0, I1>>>.ForTerminal(onFirstPresentOnly(v0))),
                () => i1.Match(v1 => ProblemQualified<Option<Tuple<I0, I1>>>.ForTerminal(onSecondPresentOnly(v1)), () => Success(None<Tuple<I0, I1>>())));

        public static ProblemQualified<T> FromExceptionOrSuccess<T>(Either<Exception, T> exceptionOrSuccess, Func<Exception, string> forTerminalMessage) =>
            exceptionOrSuccess.Match(
                exception => ProblemQualified<T>.ForTerminal(forTerminalMessage(exception)),
                Success);

        public static ProblemQualified<T> Try<T>(Func<T> forSuccess, Func<Exception, string> forTerminalMessage) =>
            FromExceptionOrSuccess(TryWithException(forSuccess), forTerminalMessage);

        public static ProblemQualified<T> FailIfFalse<T>(bool doesItFail, Func<T> forSuccess, Func<string> onFalse) =>
            doesItFail ? Success<T>(forSuccess()) : ForTerminal<T>(onFalse());
    }
}
