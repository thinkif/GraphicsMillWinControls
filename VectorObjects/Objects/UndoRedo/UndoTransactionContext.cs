// Copyright (c) 2018 Aurigma Inc. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
//
namespace Aurigma.GraphicsMill.WinControls
{
    internal sealed class UndoTransactionContext
    {
        private int _depth;

        public bool Active
        {
            get
            {
                return _depth > 0;
            }
        }

        public void Begin()
        {
            _depth++;
        }

        public void End()
        {
            if (_depth > 0)
                _depth--;
        }

        public void Reset()
        {
            _depth = 0;
        }
    }
}
