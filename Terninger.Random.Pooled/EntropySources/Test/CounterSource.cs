using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using MurrayGrant.Terninger.Random;
using MurrayGrant.Terninger.Helpers;

namespace MurrayGrant.Terninger.EntropySources.Test
{
    /// <summary>
    /// An entropy source which returns an incrementing counter.
    /// </summary>
    [AsyncHint(IsAsync.Never)]
    public class CounterSource : IEntropySource
    {
        private readonly CypherCounter _Counter;

        public string Name { get; set; }

        public CounterSource() : this(new CypherCounter(8)) { }
        public CounterSource(CypherCounter counter)
        {
            if (counter == null) throw new ArgumentNullException(nameof(counter));
            _Counter = counter;
        }

        public void Dispose()
        {
            _Counter.TryDispose();
        }

        public Task<byte[]> GetEntropyAsync(EntropyPriority priority)
        {
            // Increment the counter and return its value.
            _Counter.Increment();
            return Task.FromResult(_Counter.GetCounter());
        }
    }
}
