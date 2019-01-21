using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static PlayStudios.Functional.Alg;

namespace PlayStudios.Functional
{
    public static class ImportAndTestWork
    {
        public static Func<T> AddWorkUnit<T>(Func<T> workUnit) => ThreadWorkGroup.AddWorkUnit(workUnit);

        public static Action AddWorkUnitReturnBlock(Action workUnit) =>
            ThreadWorkGroup.AddWorkUnit(() =>
            {
                workUnit();
                return UnitValue;
            }).Let(f => new Action(() => { f(); }));


        private static readonly ThreadWorkGroup ThreadWorkGroup =
            ForThreadWorkGroup.BuildThreadWorkGroup(
                Environment.ProcessorCount // not an exact matter - as good a measure of useful threads as any.
                // (In practice the kind ofcode using this will also bog on heavy garbage creation)
                );
    }
}
