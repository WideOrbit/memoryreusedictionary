using System;
using System.Collections;
using System.Collections.Generic;

namespace MemoryReuseDictionary
{
    public class MemoryReuseDictionary_2<TKey, TValue> : IDictionary<TKey, TValue>
    {
        private readonly Dictionary<TKey, Elem> _dictionary = new Dictionary<TKey, Elem>();

        private int _count;

        public void Clear()
        {
            foreach (var elem in _dictionary)
            {
                elem.Value.Used = false;
            }

            _count = 0;
        }

        public void TrimExcess()
        {
            List<TKey> dele = null;

            foreach (var elem in _dictionary)
            {
                if (!elem.Value.Used)
                {
                    dele = dele ?? new List<TKey>();
                    dele.Add(elem.Key);
                }
            }

            if (dele != null)
            {
                foreach (var del in dele)
                {
                    _dictionary.Remove(del);
                }
            }
        }

        public void Add(TKey key, TValue value)
        {
            Elem elem;
            if (_dictionary.TryGetValue(key, out elem))
            {
                if (elem.Used == false)
                {
                    elem.Used = true;
                    elem.Value = value;
                }
                else
                {
                    throw new ArgumentException();
                }
            }
            else
            {
                _dictionary.Add(key, new Elem(value));
            }

            ++_count;
        }

        public bool ContainsKey(TKey key)
        {
            Elem elem;
            return _dictionary.TryGetValue(key, out elem) && elem.Used;
        }

        public ICollection<TKey> Keys
        {
            get { return new KeysCollection(this); }
        }

        public bool Remove(TKey key)
        {
            Elem elem;
            if (_dictionary.TryGetValue(key, out elem) && elem.Used)
            {
                elem.Used = false;
                --_count;
                return true;
            }
            return false;
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            Elem elem;
            if (_dictionary.TryGetValue(key, out elem) && elem.Used)
            {
                value = elem.Value;
                return true;
            }
            else
            {
                value = default(TValue);
                return false;
            }
        }

        public bool TryGetOldValue(TKey key, out TValue value)
        {
            Elem elem;
            if (_dictionary.TryGetValue(key, out elem) && !elem.Used)
            {
                value = elem.Value;
                return true;
            }
            else
            {
                value = default(TValue);
                return false;
            }
        }

        public bool ContainsOldKey(TKey key)
        {
            Elem elem;
            return _dictionary.TryGetValue(key, out elem) && !elem.Used;
        }

        public ICollection<TValue> Values
        {
            get { return new ValuesCollection(this); }
        }

        public TValue this[TKey key]
        {
            get
            {
                Elem elem;
                if (_dictionary.TryGetValue(key, out elem) && elem.Used)
                {
                    return elem.Value;
                }
                throw new KeyNotFoundException();
            }
            set
            {
                Elem elem;
                if (_dictionary.TryGetValue(key, out elem))
                {
                    if (!elem.Used)
                    {
                        ++_count;
                    }
                    elem.Used = true;
                    elem.Value = value;
                }
                else
                {
                    _dictionary.Add(key, new Elem(value));
                    ++_count;
                }
            }
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            Add(item.Key, item.Value);
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            Elem elem;
            return _dictionary.TryGetValue(item.Key, out elem) && elem.Used && elem.Value.Equals(item.Value);
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            foreach (var kvp in _dictionary)
            {
                if (kvp.Value.Used)
                {
                    array[arrayIndex++] = new KeyValuePair<TKey, TValue>(kvp.Key, kvp.Value.Value);
                }
            }
        }

        public int Count
        {
            get { return _count; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            Elem elem;
            if (_dictionary.TryGetValue(item.Key, out elem) && elem.Used && elem.Value.Equals(item.Value))
            {
                elem.Used = false;
                --_count;
                return true;
            }

            return false;
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            foreach (var kvp in _dictionary)
            {
                if (kvp.Value.Used)
                {
                    yield return new KeyValuePair<TKey, TValue>(kvp.Key, kvp.Value.Value);
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        private class Elem
        {
            public Elem(TValue value)
            {
                Value = value;
                Used = true;
            }
            public TValue Value { get; set; }
            public bool Used { get; set; }
        }

        private class KeysCollection : ICollection<TKey>
        {
            private MemoryReuseDictionary_2<TKey, TValue> _dict;

            public KeysCollection(MemoryReuseDictionary_2<TKey, TValue> dict)
            {
                _dict = dict;
            }

            public void Add(TKey item)
            {
                throw new InvalidOperationException();
            }

            public void Clear()
            {
                throw new InvalidOperationException();
            }

            public bool Contains(TKey item)
            {
                return _dict.ContainsKey(item);
            }

            public void CopyTo(TKey[] array, int arrayIndex)
            {
                foreach (var kvp in _dict)
                {
                    array[arrayIndex++] = kvp.Key;
                }
            }

            public int Count
            {
                get { return _dict.Count; }
            }

            public bool IsReadOnly
            {
                get { return true; }
            }

            public bool Remove(TKey item)
            {
                throw new InvalidOperationException();
            }

            public IEnumerator<TKey> GetEnumerator()
            {
                foreach (var kvp in _dict)
                {
                    yield return kvp.Key;
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        private class ValuesCollection : ICollection<TValue>
        {
            private readonly MemoryReuseDictionary_2<TKey, TValue> _dict;

            public ValuesCollection(MemoryReuseDictionary_2<TKey, TValue> dict)
            {
                _dict = dict;
            }

            public void Add(TValue item)
            {
                throw new InvalidOperationException();
            }

            public void Clear()
            {
                throw new InvalidOperationException();
            }

            public bool Contains(TValue item)
            {
                foreach (var kvp in _dict)
                {
                    if (item.Equals(kvp.Value)) return true;
                }

                return false;
            }

            public void CopyTo(TValue[] array, int arrayIndex)
            {
                foreach (var kvp in _dict)
                {
                    array[arrayIndex++] = kvp.Value;
                }
            }

            public int Count
            {
                get { return _dict.Count; }
            }

            public bool IsReadOnly
            {
                get { return true; }
            }

            public bool Remove(TValue item)
            {
                throw new InvalidOperationException();
            }

            public IEnumerator<TValue> GetEnumerator()
            {
                foreach (var kvp in _dict)
                {
                    yield return kvp.Value;
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }
    }
}
