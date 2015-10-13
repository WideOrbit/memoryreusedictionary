using System;
using System.Collections;
using System.Collections.Generic;

namespace MemoryReuseDictionary
{
    class MemoryReuseDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    {
        private struct Elem
        {
            public TKey Key;
            public TValue Value;
            private int _next;

            public int Next
            {
                get { return _next >> 1; }
                set { _next = value << 1 | (_next & 1); }
            }

            public bool Used
            {
                get { return (_next & 1) != 0; }
                set
                {
                    if (value)
                    {
                        _next |= 1;
                    }
                    else
                    {
                        _next &= ~1;
                    }
                }
            }

            public void Recycle(int next)
            {
                Key = default(TKey);
                Value = default(TValue);
                Next = next;
            }
        };

        private Elem[][] _elems;

        private int[][] _roots;

        private int _size;

        private int _freeElemIndex;

        private int _numElems; // 'max' size before rebuild

        private int _freeElemRoot;

        private int _outerMask = 0;

        // use 4096 max values

        private const int _shr = 12;

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

        private enum SetFlag
        {
            Add,
            Set,
            AddNew
        };

        private void Set(TKey key, TValue value, SetFlag flag, bool used)
        {
            var hash = key.GetHashCode();
            int index = 0;
            if (flag != SetFlag.AddNew)
            {
                index = _roots[(hash >> _shr) & _outerMask][hash & _innerMask];
                while (index != -1)
                {
                    var elem = _elems[(index >> _shr) & _outerMask][index & _innerMask];
                    if (elem.Key.Equals(key))
                    {
                        if (elem.Used && flag == SetFlag.Add)
                        {
                            throw new InvalidOperationException("Key already exists");
                        }
                        else
                        {
                            _elems[(index >> _shr) & _outerMask][index & _innerMask].Key = key;
                        }

                        return;
                    }

                    index = elem.Next;
                }
            }

            // Create new
            if (_freeElemIndex != _numElems)
            {
                index = _freeElemIndex++;
            }
            else if (_freeElemRoot != -1)
            {
                index = _freeElemRoot;
                _freeElemRoot = _elems[(index >> _shr) & _outerMask][index & _innerMask].Next;
            }
            else
            {
                Resize(Math.Max(1, 2 * _numElems));

                index = _freeElemIndex++;
            }

            var newElem = new Elem
            {
                Key = key,
                Value = value,
                Next = _roots[(hash >> _shr) & _outerMask][hash & _innerMask],
                Used = used
            };
            _elems[(index >> _shr) & _outerMask][index & _innerMask] = newElem;
            _roots[(hash >> _shr) & _outerMask][hash & _innerMask] = index;
        }

        public MemoryReuseDictionary()
            : this(1)
        {

        }

        private MemoryReuseDictionary(int numElems)
        {
            _numElems = numElems = upper_power_of_two(numElems);
            int innerSize = 0;
            if (numElems > 4096)
            {
                _elems = new Elem[numElems >> 12][];
                _roots = new int[numElems >> 12][];
                innerSize = 4096;
            }
            else
            {
                _elems = new Elem[1][];
                _roots = new int[1][];
                innerSize = numElems;
            }

            _innerMask = innerSize - 1;
            _outerMask = Math.Max(0, (numElems - 1) >> 12);

            for (int i = 0; i < _elems.Length; ++i)
            {
                _elems[i] = new Elem[innerSize];
                var t = _roots[i] = new int[innerSize];
                for (int j = 0; j < innerSize; ++j)
                {
                    t[j] = -1;
                }
            }

            _freeElemIndex = 0;
            _freeElemRoot = -1;
        }

        private void Resize(int numElems)
        {
            var newDict = new MemoryReuseDictionary<TKey, TValue>(numElems);

            var i = GetElemEnumerator();
            while (i.MoveNext())
            {
                var e = i.Current;
                newDict.Set(e.Key, e.Value, SetFlag.AddNew, e.Used);
            }

            _elems = newDict._elems;
            _roots = newDict._roots;
            _innerMask = newDict._innerMask;
            _outerMask = newDict._outerMask;
            //_shr = newDict._shr;
            _size = newDict._size;
            _freeElemIndex = newDict._freeElemIndex;
            _freeElemRoot = newDict._freeElemRoot;
            _numElems = newDict._numElems;
        }

        private bool TryGetElem(TKey key, out Elem outElem)
        {
            var hash = key.GetHashCode();
            var index = _roots[(hash >> _shr) & _outerMask][hash & _innerMask];
            while (index != -1)
            {
                var elem = _elems[(index >> _shr) & _outerMask][index & _innerMask];
                if (elem.Key.Equals(key))
                {
                    outElem = elem;
                    return true;
                }
                index = elem.Next;
            }

            outElem = default(Elem);
            return false;
        }

        public void TrimExcess()
        {
            if (_roots != null)
            {
                foreach (var root in _roots)
                {
                    for (int i = 0; i < root.Length; ++i)
                    {
                        var index = root[i];
                        var prev = -1;
                        while (index != 1)
                        {
                            var elem = _elems[(index >> _shr) & _outerMask][index & _innerMask];
                            var next = elem.Next;
                            if (!elem.Used)
                            {
                                if (prev != -1)
                                {
                                    _elems[(prev >> _shr) & _outerMask][prev & _innerMask].Next = next;
                                }
                                else
                                {
                                    root[i] = next;
                                }

                                _elems[(index >> _shr) & _outerMask][index & _innerMask].Recycle(_freeElemRoot);
                                _freeElemRoot = index;
                            }

                            prev = index;
                            index = next;
                        }
                    }
                }
            }
        }

        public void Add(TKey key, TValue value)
        {
            Set(key, value, SetFlag.Add, true);
        }

        public bool ContainsKey(TKey key)
        {
            Elem e;

            return (TryGetElem(key, out e) && e.Used);
        }

        public bool ContainsOldKey(TKey key)
        {
            Elem e;

            return (TryGetElem(key, out e) && !e.Used);
        }

        public ICollection<TKey> Keys
        {
            get { return new KeysCollection(this); }
        }

        public bool Remove(TKey key)
        {
            var hash = key.GetHashCode();
            var index = _roots[(hash >> _shr) & _outerMask][hash & _innerMask];
            while (index != 0)
            {
                --index;
                var elem = _elems[(index >> _shr) & _outerMask][index & _innerMask];
                if (elem.Key.Equals(key))
                {
                    var rv = _elems[(index >> _shr) & _outerMask][index & _innerMask].Used;
                    _elems[(index >> _shr) & _outerMask][index & _innerMask].Used = false;
                    return rv;
                }
            }

            return false;
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            Elem e;

            if (TryGetElem(key, out e) && e.Used)
            {
                value = e.Value;
                return true;
            }
            value = default(TValue);
            return false;
        }

        public bool TryGetOldValue(TKey key, out TValue value)
        {
            Elem e;

            if (TryGetElem(key, out e) && !e.Used)
            {
                value = e.Value;
                return true;
            }
            value = default(TValue);
            return false;
        }

        public ICollection<TValue> Values
        {
            get { return new ValuesCollection(this); }
        }

        public TValue this[TKey key]
        {
            get
            {
                Elem e;

                if (TryGetElem(key, out e) && e.Used)
                {
                    return e.Value;
                }
                throw new KeyNotFoundException();
            }
            set
            {
                Set(key, value, SetFlag.Set, true);
            }
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            Add(item.Key, item.Value);
        }

        public void Clear()
        {
            foreach (var e in _elems)
            {
                for (int i = 0; i < e.Length; ++i)
                {
                    e[i].Used = false;
                }
            }

            _size = 0;
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            Elem e;
            return TryGetElem(item.Key, out e) && e.Used && e.Value.Equals(item.Value);
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
            Elem e;
            return TryGetElem(item.Key, out e) && e.Used && e.Value.Equals(item.Value) && Remove(item.Key);
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
            if (_roots != null)
            {
                foreach (var root in _roots)
                {
                    for (int i = 0; i < root.Length; ++i)
                    {
                        var index = root[i];
                        while (index != -1)
                        {
                            var elem = _elems[(index >> _shr) & _outerMask][index & _innerMask];
                            yield return elem;
                            index = elem.Next;
                        }
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
