using System;
using System.Collections;
using System.Collections.Generic;

namespace MemoryReuseDictionary
{
    class MemoryReuseDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    {
        private class Elem
        {
            public TKey Key;
            public TValue Value;
            public Elem Next;
            public bool Used;
        };

        private Elem[][] _elems;

        private int _size;

        private int _usedCapacity;

        private int _slots; // 'max' size before rebuild

        private int _outerMask = 0;

        // use 4096 max values

        private int _shr = 0;

        private int _innerMask = 0;

        private static int upper_power_of_two(int v)
        {
            v--;
            v |= v >> 1;
            v |= v >> 2;
            v |= v >> 4;
            v |= v >> 8;
            v |= v >> 16;
            v++;
            return v;
        }

        const uint PRIME32_2 = 2246822519U;
        const uint PRIME32_3 = 3266489917U;

        private static uint rotate(uint v)
        {
            v ^= v >> 15;
            v *= PRIME32_2;
            v ^= v >> 13;
            v *= PRIME32_3;
            v ^= v >> 16;
            return v;
        }

        private void Set(TKey key, TValue value, bool add)
        {
            var hash = key.GetHashCode();
            var e = Get(key, hash);
            if (e != null)
            {
                if (e.Used && add)
                {
                    throw new InvalidOperationException("Key already exists");
                }
                e.Value = value;
                if (e.Used == false) ++_size;
                e.Used = true;
            }
            else
            {
                // insert new
                if (_usedCapacity >= _slots * 4)
                {
                    SetSlots(Math.Max(1, _usedCapacity * 2));
                }

                var elem = new Elem();
                elem.Used = true;
                elem.Key = key;
                elem.Value = value;
                UncheckedAdd(elem, hash);
                ++_size;
            }
        }

        public void TrimExcess()
        {
            if (_elems != null)
            {
                foreach (var e in _elems)
                {
                    for (int i = 0; i < e.Length; ++i)
                    {
                        Elem prev = null;
                        var curr = e[i];
                        while (curr != null)
                        {
                            var next = curr.Next;

                            if (!curr.Used)
                            {
                                if (prev == null)
                                {
                                    e[i] = next;
                                }
                                else
                                {
                                    prev.Next = next;
                                }
                                --_usedCapacity;
                            }

                            prev = curr;
                            curr = next;
                        }
                    }
                }
            }
        }

        private void SetSlots(int newSlots)
        {
            var dict = new MemoryReuseDictionary<TKey, TValue>();
            newSlots = upper_power_of_two(newSlots);
            int innerSize = 0;
            if (newSlots > 4096)
            {
                dict._elems = new Elem[newSlots >> 12][];
                innerSize = 4096;
            }
            else
            {
                dict._elems = new Elem[1][];
                innerSize = newSlots;
            }

            dict._innerMask = innerSize - 1;
            dict._outerMask = Math.Max(0, (newSlots - 1) >> 12);

            for (int i = 0; i < dict._elems.Length; ++i)
            {
                dict._elems[i] = new Elem[innerSize];
            }
            dict._slots = newSlots;

            while (innerSize > 0)
            {
                ++dict._shr;
                innerSize = innerSize >> 1;
            }

            if (_elems != null)
            {
                foreach (var e in _elems)
                {
                    for (int i = 0; i < e.Length; ++i)
                    {
                        while (true)
                        {
                            var x = e[i];
                            if (x == null) break;
                            e[i] = x.Next;
                            x.Next = null;
                            dict.UncheckedAdd(x);
                        }
                    }
                }
            }

            _elems = dict._elems;
            _innerMask = dict._innerMask;
            _outerMask = dict._outerMask;
            _size = dict._size;
            _slots = dict._slots;
            _shr = dict._shr;
            _usedCapacity = dict._usedCapacity;
        }

        private Elem Get(TKey key)
        {
            return Get(key, key.GetHashCode());
        }

        private Elem Get(TKey key, int hash)
        {
            if (_elems != null)
            {
                var high = hash >> _shr;
                var e = _elems[high & _outerMask];
                var first = e[hash & _innerMask];
                while (first != null && !first.Key.Equals(key))
                {
                    first = first.Next;
                }
                return first;
            }

            return null;
        }


        public void Add(TKey key, TValue value)
        {
            Set(key, value, true);
        }

        private void UncheckedAdd(Elem x)
        {
            UncheckedAdd(x, x.Key.GetHashCode());
        }

        private void UncheckedAdd(Elem x, int hash)
        {
            var high = hash >> _shr;
            var e = _elems[high & _outerMask];
            x.Next = e[hash & _innerMask];
            e[hash & _innerMask] = x;
            ++_usedCapacity;
        }

        public bool ContainsKey(TKey key)
        {
            var e = Get(key);
            return e != null && e.Used;
        }

        public bool ContainsOldKey(TKey key)
        {
            var e = Get(key);
            return e != null && !e.Used;
        }

        public ICollection<TKey> Keys
        {
            get { return new KeysCollection(this); }
        }

        public bool Remove(TKey key)
        {
            var e = Get(key);
            if (e != null && e.Used)
            {
                e.Used = false;
                --_size;
                return true;
            }
            return false;
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            var e = Get(key);
            if (e != null && e.Used)
            {
                value = e.Value;
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
            var e = Get(key);
            if (e != null && !e.Used)
            {
                value = e.Value;
                return true;
            }
            else
            {
                value = default(TValue);
                return false;
            }
        }
        public ICollection<TValue> Values
        {
            get { return new ValuesCollection(this); }
        }

        public TValue this[TKey key]
        {
            get
            {
                var e = Get(key);
                if (e != null && e.Used)
                {
                    return e.Value;
                }
                throw new KeyNotFoundException();
            }
            set
            {
                Set(key, value, false);
            }
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            Add(item.Key, item.Value);
        }

        public void Clear()
        {
            var e = GetElemEnumerator();
            while (e.MoveNext())
            {
                e.Current.Used = false;
            }
            _size = 0;
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            var e = Get(item.Key);
            return e != null && e.Used && e.Value.Equals(item.Value);
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            var e = GetElemEnumerator();
            while (e.MoveNext())
            {
                if (e.Current.Used)
                {
                    array[arrayIndex++] = new KeyValuePair<TKey, TValue>(e.Current.Key, e.Current.Value);
                }
            }
        }

        public int Count
        {
            get { return _size; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            throw new NotImplementedException();
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            var e = GetElemEnumerator();
            while (e.MoveNext())
            {
                if (e.Current.Used) yield return new KeyValuePair<TKey, TValue>(e.Current.Key, e.Current.Value);
            }
        }

        private IEnumerator<Elem> GetElemEnumerator()
        {
            for (int i = 0; i < _elems.Length; ++i)
            {
                var e = _elems[i];
                for (int j = 0; j < e.Length; ++j)
                {
                    for (var l = e[j]; l != null; l = l.Next)
                    {
                        yield return l;
                    }
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        private class KeysCollection : ICollection<TKey>
        {
            private MemoryReuseDictionary<TKey, TValue> _dict;

            public KeysCollection(MemoryReuseDictionary<TKey, TValue> dict)
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
            private readonly MemoryReuseDictionary<TKey, TValue> _dict;

            public ValuesCollection(MemoryReuseDictionary<TKey, TValue> dict)
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
