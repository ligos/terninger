using System;
using System.Collections.Generic;
using System.Text;

namespace MurrayGrant.Terninger.EntropySources
{
    /// <summary>
    /// Marks how often an IEntropySource is expected to actually be async.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class AsyncHintAttribute : Attribute
    {
        public readonly IsAsync IsAsync;

        public AsyncHintAttribute(IsAsync isAsync)
        {
            this.IsAsync = isAsync;
        }
    }

    public enum IsAsync
    {
        /// <summary>
        /// Unknown how often GetEntroptyAsync() is async.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// GetEntroptyAsync() is never async; it will always resolve synchronously.
        /// </summary>
        Never = 1,

        /// <summary>
        /// GetEntroptyAsync() is async on first call; afterwards it will always resolve synchronously.
        /// </summary>
        AfterInit = 2,

        /// <summary>
        /// GetEntroptyAsync() is occasionally async (under 10% of the time); it will usually resolve synchronously.
        /// </summary>
        Rarely = 3,

        /// <summary>
        /// GetEntroptyAsync() is mostly async (over 10% of the time); it will occasionally resolve synchronously.
        /// </summary>
        Mostly = 4,

        /// <summary>
        /// GetEntroptyAsync() is always async; it will never resolve synchronously.
        /// </summary>
        Always = 5,
    }
}
