using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace MurrayGrant.Terninger.EntropySources
{
    /// <summary>
    /// Entropy config which is backed by an in memory Dictionary object.
    /// </summary>
    public class EntropySourceConfigFromDictionary : IEntropySourceConfig
    {
        private readonly IDictionary<string, string> _Store;

        public static readonly EntropySourceConfigFromDictionary Empty = new EntropySourceConfigFromDictionary(new Dictionary<string,string>());

        public EntropySourceConfigFromDictionary(IEnumerable<string> alternateKeyValuePairs)
        {
            if (alternateKeyValuePairs == null) throw new ArgumentNullException(nameof(alternateKeyValuePairs));
            _Store = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string lastKey = null;
            foreach (var s in alternateKeyValuePairs)
            {
                if (lastKey == null)
                {
                    _Store[s] = null;
                    lastKey = s;
                }
                else
                {
                    _Store[lastKey] = s;
                    lastKey = null;
                }
            }
        }
        public EntropySourceConfigFromDictionary(IDictionary<string, string> store)
        {
            if (store == null) throw new ArgumentNullException(nameof(store));
            _Store = store;
        }

        public string Get(string name)
        {
            string result;
            _Store.TryGetValue(name, out result);
            return result;
        }
        public bool ContainsKey(string name) => _Store.ContainsKey(name);

    }
}
