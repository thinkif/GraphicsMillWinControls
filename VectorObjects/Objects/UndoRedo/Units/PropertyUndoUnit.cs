// Copyright (c) 2018 Aurigma Inc. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
//
namespace Aurigma.GraphicsMill.WinControls
{
    internal sealed class PropertyUndoUnit : IUndoUnit
    {
        private readonly System.Collections.Generic.List<VObjectStateSnapshot> _before;
        private readonly System.Collections.Generic.List<VObjectStateSnapshot> _after;

        public PropertyUndoUnit(
            System.Collections.Generic.IEnumerable<VObjectStateSnapshot> before,
            System.Collections.Generic.IEnumerable<VObjectStateSnapshot> after)
        {
            if (before == null)
                throw new System.ArgumentNullException("before");
            if (after == null)
                throw new System.ArgumentNullException("after");

            _before = new System.Collections.Generic.List<VObjectStateSnapshot>(before);
            _after = new System.Collections.Generic.List<VObjectStateSnapshot>(after);
        }

        public void Apply()
        {
            ApplySnapshots(_after);
        }

        public void Unapply()
        {
            ApplySnapshots(_before);
        }

        private static void ApplySnapshots(System.Collections.Generic.List<VObjectStateSnapshot> snapshots)
        {
            if (snapshots == null)
                return;

            for (int i = 0; i < snapshots.Count; i++)
            {
                VObjectStateSnapshot snapshot = snapshots[i];
                if (snapshot == null)
                    continue;

                VObjectSnapshotFactory.Apply(snapshot);
            }
        }
    }
}
