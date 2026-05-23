// Copyright (c) 2018 Aurigma Inc. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
//
namespace Aurigma.GraphicsMill.WinControls
{
    /// <summary>
    /// This class implements Undo/redo functionality for MultiLayerControl and VObjectsRubberband objects.
    /// </summary>
    internal class UndoRedoTracker : Aurigma.GraphicsMill.IStateNavigable
    {
        public UndoRedoTracker(IVObjectHost objectHost)
        {
            if (objectHost == null)
                throw new System.ArgumentNullException("objectHost");

            _vObjectHost = objectHost;
            _maxUndoStepCount = 10;
            _trackingEnabled = true;

            _vObjectHost.Layers.LayerAdded += new LayerEventHandler(LayerAddedHandler);
            _vObjectHost.Layers.LayerRemoved += new LayerRemovedEventHandler(LayerRemovedHandler);
            _vObjectHost.Layers.LayerChanged += new LayerChangedEventHandler(LayerChangedHandler);
        }

        public event Aurigma.GraphicsMill.StateRestoringEventHandler Undoing;
        public event System.EventHandler Undone;
        public event Aurigma.GraphicsMill.StateRestoringEventHandler Redoing;
        public event System.EventHandler Redone;

        public void ClearUndoHistory()
        {
            if (_undoStack != null)
                _undoStack.Clear();
        }

        public void ClearRedoHistory()
        {
            if (_redoStack != null)
                _redoStack.Clear();
        }

        public void ClearHistory()
        {
            ClearRedoHistory();
            ClearUndoHistory();
        }

        public void SaveState()
        {
            if (!_undoRedoEnabled)
                throw new Aurigma.GraphicsMill.UnexpectedException(StringResources.GetString("ExStrUndoRedoShouldBeEnabled"));

            if (_restoringState)
                return;

            HostStateSnapshot newSnapshot = HostStateSnapshot.Capture(_vObjectHost);
            if (_currentSnapshot == null)
            {
                _currentSnapshot = newSnapshot;
                return;
            }

            if (_currentSnapshot.EqualsState(newSnapshot))
                return;

            ClearRedoHistory();
            _undoStack.Push(new HostStateUndoUnit(this, _currentSnapshot, newSnapshot));
            _currentSnapshot = newSnapshot;
        }

        internal void BeginTransaction()
        {
            if (!_undoRedoEnabled)
                return;

            _transactionDepth++;
        }

        internal void EndTransaction(bool saveState)
        {
            if (!_undoRedoEnabled)
                return;

            if (_transactionDepth > 0)
                _transactionDepth--;

            if (_transactionDepth != 0)
                return;

            bool shouldSave = _transactionDirty;
            _transactionDirty = false;

            if (saveState && shouldSave)
                SaveState();
        }

        public void Undo()
        {
            if (!_undoRedoEnabled)
                throw new Aurigma.GraphicsMill.UnexpectedException(StringResources.GetString("ExStrUndoRedoShouldBeEnabled"));

            if (!CanUndo)
                return;

            Undo(1);
        }

        public void Undo(int undoStepCount)
        {
            if (!_undoRedoEnabled)
                throw new Aurigma.GraphicsMill.UnexpectedException(StringResources.GetString("ExStrUndoRedoShouldBeEnabled"));
            if (undoStepCount < 1 || undoStepCount > UndoStepCount)
                throw new System.ArgumentOutOfRangeException("undoStepCount");

            OnUndoing(new StateRestoringEventArgs());

            while (undoStepCount-- > 0)
            {
                IUndoUnit unit = _undoStack.Pop();
                _applyingRedo = false;
                unit.Unapply();
                _redoStack.Push(unit);
            }

            _currentSnapshot = HostStateSnapshot.Capture(_vObjectHost);
            _vObjectHost.HostViewer.InvalidateViewer();
            OnUndone(System.EventArgs.Empty);
        }

        public void Redo()
        {
            if (!_undoRedoEnabled)
                throw new Aurigma.GraphicsMill.UnexpectedException(StringResources.GetString("ExStrUndoRedoShouldBeEnabled"));

            if (!CanRedo)
                return;

            Redo(1);
        }

        public void Redo(int redoStepCount)
        {
            if (!_undoRedoEnabled)
                throw new Aurigma.GraphicsMill.UnexpectedException(StringResources.GetString("ExStrUndoRedoShouldBeEnabled"));
            if (redoStepCount < 1 || redoStepCount > RedoStepCount)
                throw new System.ArgumentOutOfRangeException("undoStepCount");

            OnRedoing(new StateRestoringEventArgs());

            while (redoStepCount-- > 0)
            {
                IUndoUnit unit = _redoStack.Pop();
                _applyingRedo = true;
                unit.Apply();
                _undoStack.Push(unit);
            }

            _applyingRedo = false;
            _currentSnapshot = HostStateSnapshot.Capture(_vObjectHost);
            _vObjectHost.HostViewer.InvalidateViewer();
            OnRedone(System.EventArgs.Empty);
        }

        public bool UndoRedoEnabled
        {
            get { return _undoRedoEnabled; }
            set
            {
                if (_undoRedoEnabled != value)
                {
                    _undoRedoEnabled = value;
                    UpdateUndoRedoStructures();
                }
            }
        }

        public int MaxUndoStepCount
        {
            get { return _maxUndoStepCount; }
            set
            {
                if (_maxUndoStepCount != value)
                {
                    _maxUndoStepCount = value;
                    UpdateUndoRedoStructures();
                }
            }
        }

        public int UndoStepCount
        {
            get { return !_undoRedoEnabled ? 0 : _undoStack.Count; }
        }

        public int RedoStepCount
        {
            get { return !_undoRedoEnabled ? 0 : _redoStack.Count; }
        }

        public bool CanUndo
        {
            get { return _undoRedoEnabled && _undoStack.Count > 0; }
        }

        public bool CanRedo
        {
            get { return _undoRedoEnabled && _redoStack.Count > 0; }
        }

        public bool UndoRedoTrackingEnabled
        {
            get { return _trackingEnabled; }
            set { _trackingEnabled = value; }
        }

        internal void ApplyHostState(HostStateSnapshot state)
        {
            if (state == null)
                throw new System.ArgumentNullException("state");

            _restoringState = true;
            LayerOperationOrigin prevOrigin = LayerChangedEventArgs.CurrentOperationOrigin;
            LayerChangedEventArgs.CurrentOperationOrigin = _applyingRedo ? LayerOperationOrigin.Redo : LayerOperationOrigin.Undo;
            try
            {
                if (state.Layers == null)
                    return;

                for (int layerIndex = 0; layerIndex < state.Layers.Count; layerIndex++)
                {
                    HostStateSnapshot.LayerState layerState = state.Layers[layerIndex];
                    Layer layer = layerState.Layer;
                    if (layer == null)
                        continue;

                    layer.Name = layerState.Name;
                    layer.Visible = layerState.Visible;
                    layer.Locked = layerState.Locked;

                    int currentCount = layer.VObjects.Count;
                    for (int i = currentCount - 1; i >= 0; i--)
                    {
                        IVObject existing = layer.VObjects[i];
                        if (!ContainsSnapshotObject(layerState.Objects, existing))
                            layer.VObjects.RemoveAt(i);
                    }

                    for (int i = 0; i < layerState.Objects.Count; i++)
                    {
                        VObjectStateSnapshot objectState = layerState.Objects[i];
                        IVObject obj = objectState.VObject;
                        int existingIndex = layer.VObjects.IndexOf(obj);
                        if (existingIndex < 0)
                        {
                            int insertIndex = System.Math.Min(i, layer.VObjects.Count);
                            layer.VObjects.Insert(insertIndex, obj);
                        }
                        else if (existingIndex != i)
                        {
                            layer.VObjects.RemoveAt(existingIndex);
                            int insertIndex = System.Math.Min(i, layer.VObjects.Count);
                            layer.VObjects.Insert(insertIndex, obj);
                        }

                        VObjectSnapshotFactory.Apply(objectState);
                    }
                }

                _vObjectHost.CurrentLayerIndex = state.CurrentLayerIndex;
            }
            finally
            {
                LayerChangedEventArgs.CurrentOperationOrigin = prevOrigin;
                _restoringState = false;
            }
        }

        private void UpdateUndoRedoStructures()
        {
            if (!_undoRedoEnabled)
            {
                _undoStack = null;
                _redoStack = null;
                _currentSnapshot = null;
                return;
            }

            if (_undoStack == null)
                _undoStack = new UndoUnitStack(_maxUndoStepCount);
            else
                _undoStack.Capacity = _maxUndoStepCount;

            if (_redoStack == null)
                _redoStack = new UndoUnitStack(_maxUndoStepCount);
            else
                _redoStack.Capacity = _maxUndoStepCount;

            if (_currentSnapshot == null)
                _currentSnapshot = HostStateSnapshot.Capture(_vObjectHost);
        }

        private static bool ContainsSnapshotObject(System.Collections.Generic.List<VObjectStateSnapshot> snapshots, IVObject obj)
        {
            if (snapshots == null)
                return false;

            for (int i = 0; i < snapshots.Count; i++)
                if (snapshots[i].VObject == obj)
                    return true;

            return false;
        }

        private void LayerAddedHandler(object sender, LayerEventArgs e)
        {
            if (_undoRedoEnabled && !_restoringState && _trackingEnabled)
            {
                if (_transactionDepth > 0)
                {
                    _transactionDirty = true;
                    return;
                }

                SaveState();
            }
        }

        private void LayerRemovedHandler(object sender, LayerRemovedEventArgs e)
        {
            if (_undoRedoEnabled && !_restoringState && _trackingEnabled)
            {
                if (_transactionDepth > 0)
                {
                    _transactionDirty = true;
                    return;
                }

                SaveState();
            }
        }

        private void LayerChangedHandler(object sender, LayerChangedEventArgs e)
        {
            if (_undoRedoEnabled && !_restoringState && _trackingEnabled &&
                (e.ChangeType == LayerChangeType.ObjectAdded ||
                 e.ChangeType == LayerChangeType.ObjectRemoved ||
                 e.ChangeType == LayerChangeType.ObjectChanged ||
                 e.ChangeType == LayerChangeType.ObjectZOrderChanged ||
                 e.ChangeType == LayerChangeType.VisibilityChanged ||
                 e.ChangeType == LayerChangeType.LockStatusChanged))
            {
                if (_transactionDepth > 0)
                {
                    _transactionDirty = true;
                    return;
                }

                SaveState();
            }
        }

        protected virtual void OnUndoing(Aurigma.GraphicsMill.StateRestoringEventArgs e)
        {
            if (e == null)
                throw new System.ArgumentNullException("e");

            if (Undoing != null)
                Undoing(this, e);

            if (e.Cancel)
                throw new Aurigma.GraphicsMill.GMException("Aborted");
        }

        protected virtual void OnRedoing(Aurigma.GraphicsMill.StateRestoringEventArgs e)
        {
            if (e == null)
                throw new System.ArgumentNullException("e");

            if (Redoing != null)
                Redoing(this, e);

            if (e.Cancel)
                throw new Aurigma.GraphicsMill.GMException("Aborted");
        }

        protected virtual void OnUndone(System.EventArgs e)
        {
            if (Undone != null)
                Undone(this, e);
        }

        protected virtual void OnRedone(System.EventArgs e)
        {
            if (Redone != null)
                Redone(this, e);
        }

        private readonly IVObjectHost _vObjectHost;
        private bool _undoRedoEnabled;
        private int _maxUndoStepCount;
        private UndoUnitStack _undoStack;
        private UndoUnitStack _redoStack;
        private bool _trackingEnabled;
        private bool _restoringState;
        private bool _applyingRedo;
        private HostStateSnapshot _currentSnapshot;
        private int _transactionDepth;
        private bool _transactionDirty;
    }
}
