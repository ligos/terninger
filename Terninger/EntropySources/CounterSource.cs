using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MurrayGrant.Terninger.Generator;
using MurrayGrant.Terninger.Helpers;

namespace MurrayGrant.Terninger.EntropySources
{
    /// <summary>
    /// An entropy source which returns an incrementing counter.
    /// </summary>
    public class CounterSource : IEntropySource
    {
        private readonly CypherCounter _Counter;
        public string Name => typeof(CounterSource).FullName;

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

        public Task<EntropySourceInitialisationResult> Initialise()
        {
            return Task.FromResult(EntropySourceInitialisationResult.Successful);
        }

        public Task<byte[]> GetEntropyAsync()
        {
            // Increment the counter and return its value.
            _Counter.Increment();
            return Task.FromResult(_Counter.GetCounter());
        }
    }
}
