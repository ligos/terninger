﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Collections;

namespace MurrayGrant.Terninger.PersistentState
{
    /// <summary>
    /// A collection of all persistent items for a PooledEntropyCprngGenerator instance.
    /// </summary>
    /// <remarks>
    /// Namespaces may use any unicode characters except U+0000-U+001F, U+0080-U+009F
    /// </remarks>
    public sealed class PersistentItemCollection : IReadOnlyCollection<NamespacedPersistentItem>
    {
        private static readonly Dictionary<string, (ValueEncoding encoding, byte[] value)> _EmptyResult 
            = new Dictionary<string, (ValueEncoding encoding, byte[] bytes)>();

        private readonly Dictionary<string, Dictionary<string, (ValueEncoding encoding, byte[] bytes)>> _ItemLookup 
            = new Dictionary<string, Dictionary<string, (ValueEncoding encoding, byte[] bytes)>>();

        public int Count => this._ItemLookup.Values.Sum(x => x.Count);

        public IEnumerator<NamespacedPersistentItem> GetEnumerator()
            => Items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator()
            => Items.GetEnumerator();
        IEnumerable<NamespacedPersistentItem> Items
            => _ItemLookup.SelectMany(x => x.Value, (ns, kvp) => new NamespacedPersistentItem(ns.Key, kvp.Key, kvp.Value.encoding, kvp.Value.bytes));

        public PersistentItemCollection() : this(Enumerable.Empty<NamespacedPersistentItem>()) { }
        public PersistentItemCollection(IEnumerable<NamespacedPersistentItem> items)
        {
            foreach (var item in items)
                SetItem(item);
        }

        public IDictionary<string, (ValueEncoding encoding, byte[] bytes)> Get(string itemNamespace)
        {
            if (_ItemLookup.TryGetValue(itemNamespace, out var result))
                return result;
            else
                return _EmptyResult;
        }

        public void SetNamespaceItems(string itemNamespace, IDictionary<string, (ValueEncoding encoding, byte[] bytes)> items)
        {
            AssertValidKey(itemNamespace, nameof(itemNamespace));
            foreach (var kvp in items)
                AssertValidKey(kvp.Key, nameof(items));

            if (items is Dictionary<string, (ValueEncoding encoding, byte[] bytes)> d)
                _ItemLookup[itemNamespace] = d;
            else
                _ItemLookup[itemNamespace] = items.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        public void SetItem(NamespacedPersistentItem item)
            => SetItem(item.Namespace, item.Key, item.ValueEncoding, item.Value);
        public void SetItem(string itemNamespace, string key, ValueEncoding encoding, byte[] bytes)
        {
            AssertValidKey(itemNamespace, nameof(itemNamespace));
            AssertValidKey(key, nameof(key));

            if (!_ItemLookup.TryGetValue(itemNamespace, out var items))
                items = new Dictionary<string, (ValueEncoding encoding, byte[] bytes)>();
            items[key] = (encoding, bytes);
            _ItemLookup[itemNamespace] = items;
        }

        public void RemoveNamespace(string itemNamespace)
        {
            _ItemLookup.Remove(itemNamespace);
        }

        public void RemoveItem(string itemNamespace, string key)
        {
            if (_ItemLookup.TryGetValue(itemNamespace, out var items))
            {
                items.Remove(key);
            }
        }

        public static void AssertValidKey(string keyOrNamespace, string parmName)
        {
            if (!ValidKey(keyOrNamespace))
                throw new ArgumentException($"Key or Namespace '{keyOrNamespace}' contains invalid character. {InvalidKeyUnicodeRanges} are not valid.", parmName);
        }
        public const string InvalidKeyUnicodeRanges = "U+0000-U+001F,U+0080-U+009F.";
        public static bool ValidKey(string keyOrNamespace)
            // TODO: cope with surrogate pairs correctly.
            => !String.IsNullOrEmpty(keyOrNamespace)
            && !keyOrNamespace.Any(ch => (ch >= 0x0000 && ch <= 0x001f) || (ch >= 0x0080 && ch <= 0x009f));


    }
}
