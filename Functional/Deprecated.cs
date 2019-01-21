using System.Linq;

namespace PlayStudios.Functional
{
    
    /// <summary>
    /// Please do not use any of these utilities. They turned out to be bad ideas.
    /// </summary>
    public static class Deprecated
    {
        /// <summary>
        /// Depricated because a foreach statement is more readable and easier to debug.
        /// </summary>



        /// <summary>
        /// Depricated because it encourages hard-to-maintain code.
        /// Null collections usually shouldn't be allowed where empty collections are permitted.
        /// Using this utility makes it too unclear to readers when a collection might really be null.
        /// </summary>
        public static bool IsNullOrEmpty<T>(System.Collections.Generic.IEnumerable<T> collection)
        {
            return (collection == null || !collection.Any());
        }
    }
}
