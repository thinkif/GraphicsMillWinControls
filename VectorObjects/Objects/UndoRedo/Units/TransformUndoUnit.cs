// Copyright (c) 2018 Aurigma Inc. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
//
namespace Aurigma.GraphicsMill.WinControls
{
    internal sealed class TransformUndoUnit : IUndoUnit
    {
        private readonly System.Collections.Generic.List<VObjectGeometrySnapshot> _before;
        private readonly System.Collections.Generic.List<VObjectGeometrySnapshot> _after;

        public TransformUndoUnit(
            System.Collections.Generic.IEnumerable<VObjectGeometrySnapshot> before,
            System.Collections.Generic.IEnumerable<VObjectGeometrySnapshot> after)
        {
            if (before == null)
                throw new System.ArgumentNullException("before");
            if (after == null)
                throw new System.ArgumentNullException("after");

            _before = new System.Collections.Generic.List<VObjectGeometrySnapshot>(before);
            _after = new System.Collections.Generic.List<VObjectGeometrySnapshot>(after);
        }

        public void Apply()
        {
            ApplySnapshots(_after);
        }

        public void Unapply()
        {
            ApplySnapshots(_before);
        }

        private static void ApplySnapshots(System.Collections.Generic.List<VObjectGeometrySnapshot> snapshots)
        {
            if (snapshots == null)
                return;

            for (int i = 0; i < snapshots.Count; i++)
            {
                VObjectGeometrySnapshot snapshot = snapshots[i];
                if (snapshot == null)
                    continue;

                snapshot.Apply();
                if (snapshot.VObject != null)
                    snapshot.VObject.Update();
            }
        }
    }
}
