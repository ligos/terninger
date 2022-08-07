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
        public readonly ValueEncoding ValueEncoding { get; }
        public readonly byte[] Value { get; }
        public string ValueAsBase64 => Convert.ToBase64String(Value);
        public string ValueAsHex => Value.ToHexString();
        public string ValueAsUtf8Text => Encoding.UTF8.GetString(Value);
        public string ValueAsEncodedString
            => ValueEncoding == ValueEncoding.Base64 ? ValueAsBase64
              : ValueEncoding == ValueEncoding.Hex ? ValueAsHex
              : ValueEncoding == ValueEncoding.Utf8Text ? ValueAsUtf8Text
              : ValueAsHex;

        public static NamespacedPersistentItem CreateText(string key, string value, string theNamespace = "")
            => new NamespacedPersistentItem(theNamespace, key, ValueEncoding.Utf8Text, Encoding.UTF8.GetBytes(value));

        public static NamespacedPersistentItem CreateBinary(string key, byte[] value, string theNamespace = "")
            => new NamespacedPersistentItem(theNamespace, key, value.Length <= 64 ? ValueEncoding.Hex : ValueEncoding.Base64, value);

        public NamespacedPersistentItem(string theNamespace, string key, ValueEncoding valueEncoding, byte[] value)
        {
            this.Namespace = theNamespace;
            this.Key = key;
            this.ValueEncoding = valueEncoding;
            this.Value = value;
        }

        public override bool Equals(object obj)
            => obj is NamespacedPersistentItem x
            && Equals(x);

        public bool Equals(NamespacedPersistentItem other)
            => Namespace == other.Namespace
            && Key == other.Key 
            && ValueEncoding == other.ValueEncoding
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
            => Namespace + "." + Key + ": " + ValueAsEncodedString;
    }

    public enum ValueEncoding
    {
        Base64,
        Hex,
        Utf8Text,
    }
}
