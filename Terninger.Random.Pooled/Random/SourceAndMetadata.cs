using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MurrayGrant.Terninger.EntropySources;
using MurrayGrant.Terninger.PersistentState;

namespace MurrayGrant.Terninger.Random
{
    internal class SourceAndMetadata
    {
        public SourceAndMetadata(IEntropySource source, string uniqueName)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            UniqueName = uniqueName ?? throw new ArgumentNullException(nameof(uniqueName));
#if NETSTANDARD1_3
            AsyncHint = IsAsync.Unknown;
#else
            var maybeHintAttribute = source.GetType().GetCustomAttributesData().FirstOrDefault(x => x.AttributeType == typeof(AsyncHintAttribute));
            if (maybeHintAttribute != null)
            {
                AsyncHint = (IsAsync)maybeHintAttribute.ConstructorArguments.First(x => x.ArgumentType == typeof(IsAsync)).Value;
            }
#endif
            if (AsyncHint == IsAsync.AfterInit)
                AsyncScore = -8;
            else if (AsyncHint == IsAsync.Never)
                AsyncScore = -10;
            else if (AsyncHint == IsAsync.Rarely)
                AsyncScore = 5;
            else
                AsyncScore = 10;
        }

        public string UniqueName { get; }
        public IEntropySource Source { get; }
        public IsAsync AsyncHint { get; }

        /// <summary>
        /// Positive is biased toward async, negative is biased toward sync.
        /// </summary>
        public int AsyncScore { get; private set; }
        /// <summary>
        /// Positive is biased toward success, negative is biased toward exceptions.
        /// </summary>
        public int ExceptionScore { get; private set; }

        public bool IsExternalStateInitialised { get; private set; }

        public override string ToString() => $"{UniqueName} - {AsyncHint}. AsyncScore: {AsyncScore}, ExceptionScore: {ExceptionScore}";

        public void ScoreAsync()
        {
            if (AsyncScore <= 1000)
                AsyncScore = AsyncScore + 2;
        }
        public void ScoreSync()
        {
            if (AsyncScore >= -1000)
                AsyncScore = AsyncScore - 1;
        }

        public void ScoreException()
        {
            if (ExceptionScore >= -1000)
                ExceptionScore = ExceptionScore - 5;
        }
        public void ScoreSuccess()
        {
            if (ExceptionScore <= 1000)
                ExceptionScore = ExceptionScore + 1;
        }

        public bool TryInitialiseFromExternalState(PersistentItemCollection state)
        {
            if (IsExternalStateInitialised)
                return false;

            if (Source is IPersistentStateSource sourceForPersistentState)
            {
                sourceForPersistentState.Initialise(state.Get(Source.GetType().Name));
                IsExternalStateInitialised = true;
                return true;
            }
            else
            {
                IsExternalStateInitialised = true;
                return false;
            }
        }
    }
}
