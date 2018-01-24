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
    [SourceDisabledByDefault]
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

        public Task<EntropySourceInitialisationResult> Initialise(IEntropySourceConfig config, Func<IRandomNumberGenerator> prngFactory)
        {
            if (config.IsTruthy("CounterSource.Enabled") == true)
                return Task.FromResult(EntropySourceInitialisationResult.Successful());
            else
                return Task.FromResult(EntropySourceInitialisationResult.Failed(EntropySourceInitialisationReason.DisabledByConfig, "CounterSource is disabled by default. Set CounterSource.Enabled in configuration to enable."));
        }

        public Task<byte[]> GetEntropyAsync()
        {
            // Increment the counter and return its value.
            _Counter.Increment();
            return Task.FromResult(_Counter.GetCounter());
        }
    }
}
