﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using MurrayGrant.Terninger.Random;

namespace MurrayGrant.Terninger.EntropySources.Local
{
    /// <summary>
    /// A source of entropy, supplied from an external source.
    /// </summary>
    [AsyncHint(IsAsync.Never)]
    public class UserSuppliedSource : IEntropySource
    {
        public string Name { get; set; }

        private byte[] _Entropy;

        public UserSuppliedSource() { }
        public UserSuppliedSource(byte[] initialEntropy)
        {
            this._Entropy = initialEntropy;
        }

        public void Dispose()
        {
            // Nothing required.
        }


        // Theading: SetEntropy() and GetEntropyAsync() may be accessed from different threads.
        // I'm not too concerned because its a single reference field being accessed, so the worst that can happen is a race (in a polling loop).


        public void SetEntropy(byte[] entropy)
        {
            this._Entropy = entropy;
        }

        public Task<byte[]> GetEntropyAsync(EntropyPriority priority)
        {
            var result = _Entropy;
            _Entropy = null;
            return Task.FromResult(result);
        }
    }
}
