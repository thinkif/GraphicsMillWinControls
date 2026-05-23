// Copyright (c) 2018 Aurigma Inc. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
//
namespace Aurigma.GraphicsMill.WinControls
{
    internal sealed class HostStateUndoUnit : IUndoUnit
    {
        private readonly UndoRedoTracker _tracker;
        private readonly HostStateSnapshot _before;
        private readonly HostStateSnapshot _after;

        public HostStateUndoUnit(UndoRedoTracker tracker, HostStateSnapshot before, HostStateSnapshot after)
        {
            if (tracker == null)
                throw new System.ArgumentNullException("tracker");
            if (before == null)
                throw new System.ArgumentNullException("before");
            if (after == null)
                throw new System.ArgumentNullException("after");

            _tracker = tracker;
            _before = before;
            _after = after;
        }

        public void Apply()
        {
            _tracker.ApplyHostState(_after);
        }

        public void Unapply()
        {
            _tracker.ApplyHostState(_before);
        }
    }
}
