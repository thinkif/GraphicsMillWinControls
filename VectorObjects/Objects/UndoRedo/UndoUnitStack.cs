// Copyright (c) 2018 Aurigma Inc. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
//
namespace Aurigma.GraphicsMill.WinControls
{
    internal sealed class UndoUnitStack
    {
        private readonly System.Collections.Generic.List<IUndoUnit> _items;
        private int _capacity;

        public UndoUnitStack(int capacity)
        {
            if (capacity < 1)
                throw new System.ArgumentOutOfRangeException("capacity");

            _capacity = capacity;
            _items = new System.Collections.Generic.List<IUndoUnit>(capacity);
        }

        public int Capacity
        {
            get
            {
                return _capacity;
            }
            set
            {
                if (value < 1)
                    throw new System.ArgumentOutOfRangeException("value");

                _capacity = value;
                TrimToCapacity();
            }
        }

        public int Count
        {
            get
            {
                return _items.Count;
            }
        }

        public void Clear()
        {
            _items.Clear();
        }

        public void Push(IUndoUnit unit)
        {
            if (unit == null)
                throw new System.ArgumentNullException("unit");

            if (_items.Count == _capacity)
                _items.RemoveAt(0);

            _items.Add(unit);
        }

        public IUndoUnit Pop()
        {
            if (_items.Count == 0)
                throw new Aurigma.GraphicsMill.ObjectEmptyException();

            int index = _items.Count - 1;
            IUndoUnit unit = _items[index];
            _items.RemoveAt(index);
            return unit;
        }

        private void TrimToCapacity()
        {
            while (_items.Count > _capacity)
                _items.RemoveAt(0);
        }
    }
}
