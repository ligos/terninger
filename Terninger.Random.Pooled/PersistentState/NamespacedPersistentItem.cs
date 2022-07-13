using MurrayGrant.Terninger.Helpers;
using System;
using System.Collections.Generic;
using System.Text;

namespace MurrayGrant.Terninger.PersistentState
{
    /// <summary>
    /// Key value pair of persistent state within a namespace.
    /// </summary>
    /// <remarks>
    /// Keys and namespaces may use any unicode characters except U+0000-U+001F, U+0080-U+009F
    /// </remarks>
    public readonly struct NamespacedPersistentItem : IEquatable<NamespacedPersistentItem>
    {
        public readonly string Namespace { get; }
        public readonly string Key { get; }
        public readonly byte[] Value { get; }
        public string ValueAsBase64 => Convert.ToBase64String(Value);
        public string ValueAsHex => Value.ToHexString();

        public NamespacedPersistentItem(string theNamespace, string key, byte[] value)
        {
            this.Namespace = theNamespace;
            this.Key = key;
            this.Value = value;
        }

        public override bool Equals(object obj)
            => obj is NamespacedPersistentItem x
            && Equals(x);

        public bool Equals(NamespacedPersistentItem other)
            => Namespace == other.Namespace
            && Key == other.Key 
            && Value.AllEqual(other.Value);

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = 206514262;
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Namespace);
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Key);
                hashCode = hashCode * -1521134295 + EqualityComparer<byte[]>.Default.GetHashCode(Value);
                return hashCode;
            }
        }

        public override string ToString()
            => Namespace + "." + Key + ": " + ValueAsHex;
    }
}
