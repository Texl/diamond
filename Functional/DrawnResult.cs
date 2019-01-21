using System;

namespace PlayStudios.Functional
{
    public sealed class DrawnResult<TFinalValue, TDrawInput, TDrawResult>
    {
        private DrawnResult(Either<TFinalValue, Draw> outcomeOrDrawNeeded)
        {
            mOutcomeOrDrawNeeded = outcomeOrDrawNeeded;
        }

        public static DrawnResult<TFinalValue, TDrawInput, TDrawResult> Return(TFinalValue t) => new DrawnResult<TFinalValue, TDrawInput, TDrawResult>(Either<TFinalValue, Draw>.FromLeft(t));

        public static DrawnResult<TFinalValue, TDrawInput, TDrawResult> Create(TDrawInput drawInput, Func<TDrawResult, DrawnResult<TFinalValue, TDrawInput, TDrawResult>> getOutcomeFromDraw) =>
            new DrawnResult<TFinalValue, TDrawInput, TDrawResult>(Either<TFinalValue, Draw>.FromRight(new Draw(drawInput, getOutcomeFromDraw)));

        public static DrawnResult<TFinalValue, TDrawInput, TDrawResult> CreateToSingle(TDrawInput drawInput, Func<TDrawResult, TFinalValue> getKnownFromDraw) =>
            new DrawnResult<TFinalValue, TDrawInput, TDrawResult>(Either<TFinalValue, Draw>.FromRight(new Draw(drawInput, new Func<TDrawResult, DrawnResult<TFinalValue, TDrawInput, TDrawResult>>(w => Return(getKnownFromDraw(w))))));

        public DrawnResult<U, TDrawInput, TDrawResult> Select<U>(Func<TFinalValue, U> f) =>
            mOutcomeOrDrawNeeded.Match(x => DrawnResult<U, TDrawInput, TDrawResult>.Return(f(x)), wd => DrawnResult<U, TDrawInput, TDrawResult>.Create(wd.DrawInput, drawn => wd.Extract(drawn).Select(f)));

        public DrawnResult<U, TDrawInput, TDrawResult> SelectMany<U>(Func<TFinalValue, DrawnResult<U, TDrawInput, TDrawResult>> f) =>
            mOutcomeOrDrawNeeded.Match(f, wd => DrawnResult<U, TDrawInput, TDrawResult>.Create(wd.DrawInput, drawn => wd.Extract(drawn).SelectMany(f)));

        public DrawnResult<V, TDrawInput, TDrawResult> SelectMany<U, V>(Func<TFinalValue, DrawnResult<U, TDrawInput, TDrawResult>> tu, Func<TFinalValue, U, V> tuv) => SelectMany(t => tu(t).Select(u => tuv(t, u)));


        // Assess in future if chaining up a lot of things like this, returning some
        // Composable purer computation element, i.e. one that cares about T but not that it came from a DrawnOutcome
        public TFinalValue Reconciled(Func<TDrawInput, TDrawResult> drawSingle)
        {   // do in a loop not recursive - this effectively "trampolines" the whole computation.
            var current = mOutcomeOrDrawNeeded;
            while (current.IsRight)
            {
                var wd = current.RightIfPresent().Value;
                current = wd.Extract(drawSingle(wd.DrawInput)).mOutcomeOrDrawNeeded;
            }
            return current.LeftIfPresent().Value;
        }

        public sealed class Draw
        {
            public Draw(TDrawInput drawInput, Func<TDrawResult, DrawnResult<TFinalValue, TDrawInput, TDrawResult>> extract)
            {
                DrawInput = drawInput;
                Extract = extract;
            }
            public readonly TDrawInput DrawInput;
            public readonly Func<TDrawResult, DrawnResult<TFinalValue, TDrawInput, TDrawResult>> Extract;
        }


        private readonly Either<TFinalValue, Draw> mOutcomeOrDrawNeeded;
    }

    public static class DrawnResult
    {
        public static DrawnResult<TFinalValue, TDrawInput, TDrawResult> Create<TFinalValue, TDrawInput, TDrawResult>(TDrawInput drawInput, Func<TDrawResult, DrawnResult<TFinalValue, TDrawInput, TDrawResult>> getOutcomeFromDraw) =>
            DrawnResult<TFinalValue, TDrawInput, TDrawResult>.Create(drawInput, getOutcomeFromDraw);
        public static DrawnResult<TFinalValue, TDrawInput, TDrawResult> CreateToSingle<TFinalValue, TDrawInput, TDrawResult>(TDrawInput drawInput, Func<TDrawResult, TFinalValue> getKnownFromDraw) =>
            DrawnResult<TFinalValue, TDrawInput, TDrawResult>.CreateToSingle(drawInput, getKnownFromDraw);
    }

}
