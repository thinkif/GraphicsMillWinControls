// Copyright (c) 2018 Aurigma Inc. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
//
namespace Aurigma.GraphicsMill.WinControls
{
    internal sealed class HostStateSnapshot
    {
        internal sealed class LayerState
        {
            public Layer Layer;
            public string Name;
            public bool Visible;
            public bool Locked;
            public System.Collections.Generic.List<VObjectStateSnapshot> Objects;
        }

        public int CurrentLayerIndex;
        public System.Collections.Generic.List<LayerState> Layers;

        public static HostStateSnapshot Capture(IVObjectHost host)
        {
            if (host == null)
                throw new System.ArgumentNullException("host");

            HostStateSnapshot snapshot = new HostStateSnapshot();
            snapshot.CurrentLayerIndex = host.CurrentLayerIndex;
            snapshot.Layers = new System.Collections.Generic.List<LayerState>(host.Layers.Count);

            for (int layerIndex = 0; layerIndex < host.Layers.Count; layerIndex++)
            {
                Layer layer = host.Layers[layerIndex];
                LayerState layerState = new LayerState();
                layerState.Layer = layer;
                layerState.Name = layer.Name;
                layerState.Visible = layer.Visible;
                layerState.Locked = layer.Locked;
                layerState.Objects = new System.Collections.Generic.List<VObjectStateSnapshot>(layer.VObjects.Count);

                for (int objectIndex = 0; objectIndex < layer.VObjects.Count; objectIndex++)
                {
                    IVObject obj = layer.VObjects[objectIndex];
                    layerState.Objects.Add(VObjectSnapshotFactory.Capture(layer, obj, objectIndex));
                }

                snapshot.Layers.Add(layerState);
            }

            return snapshot;
        }

        public bool EqualsState(HostStateSnapshot other)
        {
            if (other == null)
                return false;

            if (CurrentLayerIndex != other.CurrentLayerIndex)
                return false;
            if (Layers == null || other.Layers == null || Layers.Count != other.Layers.Count)
                return false;

            for (int i = 0; i < Layers.Count; i++)
            {
                LayerState left = Layers[i];
                LayerState right = other.Layers[i];
                if (!object.ReferenceEquals(left.Layer, right.Layer))
                    return false;
                if (!string.Equals(left.Name, right.Name, System.StringComparison.Ordinal) ||
                    left.Visible != right.Visible ||
                    left.Locked != right.Locked)
                    return false;

                if (left.Objects == null || right.Objects == null || left.Objects.Count != right.Objects.Count)
                    return false;

                for (int j = 0; j < left.Objects.Count; j++)
                {
                    if (!left.Objects[j].EqualsState(right.Objects[j]))
                        return false;
                }
            }

            return true;
        }
    }
}
