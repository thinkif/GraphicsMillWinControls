// Copyright (c) 2018 Aurigma Inc. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
//
namespace Aurigma.GraphicsMill.WinControls
{
    /// <summary>
    /// Base implementation of the edit-designer. Supports control-points drawing & dragging, object dragging (using GripsProvider object).
    /// </summary>
    public class GenericVObjectEditDesigner : IDesigner, System.IDisposable
    {
        #region "Construction / destruction / initialization"

        protected GenericVObjectEditDesigner()
        {
            _dragPointIndex = GripsProvider.InvalidPointHandle;
            _multiSelect = true;
            _objectBorderPen = new System.Drawing.Pen(System.Drawing.Color.DarkGray, 1.0f);
            _snapDetectTolerance = 13.5f;
            _snapApplyTolerance = 2.25f;
            _snapReleaseTolerance = 5.25f;
            _snapGuideLineColor = System.Drawing.Color.Blue;
            _snapController = new VObjectSnapController();
        }

        public GenericVObjectEditDesigner(IVObject obj)
            : this()
        {
            if (obj == null)
                throw new System.ArgumentNullException("obj");

            _obj = obj;
            _obj.Changed += new VObjectChangedEventHandler(ObjectChangedHandler);
        }

        public void Dispose()
        {
            try
            {
                Dispose(true);
            }
            finally
            {
                System.GC.SuppressFinalize(this);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_gripsProvider != null)
                {
                    _gripsProvider.Dispose();
                    _gripsProvider = null;
                }

                if (_contextMenu != null)
                {
                    _contextMenu.Dispose();
                    _contextMenu = null;
                }
            }
        }

        #endregion "Construction / destruction / initialization"

        #region IDesigner Members

        public virtual void NotifyConnect(IVObjectHost objectHost)
        {
            if (objectHost == null)
                throw new System.ArgumentNullException("objectHost");

            _objectHost = objectHost;
            _gripsProvider = new GripsProvider(_obj, _objectHost.HostViewer);
            _gripsProvider.VObjectBorderPen = _objectBorderPen;

            InvalidateObjectArea();
        }

        public virtual void NotifyDisconnect()
        {
            if (!Connected)
                return;

            EndDragUndoRedoTransaction(false);

            System.Drawing.Rectangle invalidRect = _gripsProvider.GetInvalidationRectangle();
            if (_snapController.Active)
            {
                invalidRect = System.Drawing.Rectangle.Union(invalidRect, _snapController.ClearGuides());
                _snapController.End();
            }

            _gripsProvider = null;
            _objectHost.HostViewer.RestoreCursorToDefault();
            _objectHost.HostViewer.InvalidateViewer(new MultiLayerViewerInvalidationTarget(invalidRect));
            _objectHost = null;
        }

        public virtual void UpdateSettings()
        {
            _resizeProportionallyWithShift = VObjectsUtils.GetBoolDesignerProperty(_objectHost, DesignerSettingsConstants.ResizeProportionallyWithShift, _resizeProportionallyWithShift);
            _multiSelect = VObjectsUtils.GetBoolDesignerProperty(_objectHost, DesignerSettingsConstants.MultiSelect, _multiSelect);
            _snapEnabled = VObjectsUtils.GetBoolDesignerProperty(_objectHost, DesignerSettingsConstants.SnapEnabled, _snapEnabled);
            _snapNearestOnly = VObjectsUtils.GetBoolDesignerProperty(_objectHost, DesignerSettingsConstants.SnapNearestOnly, _snapNearestOnly);

            object snapDetectTolerance = _objectHost.DesignerOptions[DesignerSettingsConstants.SnapDetectTolerance];
            if (snapDetectTolerance is float)
                _snapDetectTolerance = System.Math.Max(0, (float)snapDetectTolerance);
            else if (snapDetectTolerance is int)
                _snapDetectTolerance = System.Math.Max(0, (int)snapDetectTolerance);

            object snapApplyTolerance = _objectHost.DesignerOptions[DesignerSettingsConstants.SnapApplyTolerance];
            if (snapApplyTolerance is float)
                _snapApplyTolerance = System.Math.Max(0, (float)snapApplyTolerance);
            else if (snapApplyTolerance is int)
                _snapApplyTolerance = System.Math.Max(0, (int)snapApplyTolerance);

            object snapReleaseTolerance = _objectHost.DesignerOptions[DesignerSettingsConstants.SnapReleaseTolerance];
            if (snapReleaseTolerance is float)
                _snapReleaseTolerance = System.Math.Max(0, (float)snapReleaseTolerance);
            else if (snapReleaseTolerance is int)
                _snapReleaseTolerance = System.Math.Max(0, (int)snapReleaseTolerance);

            if (_snapDetectTolerance < _snapApplyTolerance)
                _snapDetectTolerance = _snapApplyTolerance;
            if (_snapReleaseTolerance < _snapApplyTolerance)
                _snapReleaseTolerance = _snapApplyTolerance;

            object snapGuideLineColor = _objectHost.DesignerOptions[DesignerSettingsConstants.SnapGuideLineColor];
            if (snapGuideLineColor is System.Drawing.Color)
                _snapGuideLineColor = (System.Drawing.Color)snapGuideLineColor;
        }

        public virtual void Draw(System.Drawing.Graphics g)
        {
            if (g == null)
                throw new System.ArgumentNullException("g");

            if (_gripsProvider != null)
            {
                _gripsProvider.DrawGrips(g);
            }

            _snapController.Draw(g, _snapGuideLineColor);
        }

        public virtual bool NotifyMouseUp(System.Windows.Forms.MouseEventArgs e)
        {
            if (e == null)
                throw new System.ArgumentNullException("e");

            if (_dragging && e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                try
                {
                    bool saveState = _objectHost.UndoRedoEnabled && _undoRedoTrackingEnabledBeforeDrag &&
                        (System.Math.Abs(e.X - _dragBeginPoint.X) > 0 || System.Math.Abs(e.Y - _dragBeginPoint.Y) > 0);

                    if (_gripsProvider != null)
                    {
                        System.Drawing.Point clickedPoint = new System.Drawing.Point(e.X, e.Y);
                        int pointIndex = _gripsProvider.TestPoint(clickedPoint);
                        if (pointIndex != GripsProvider.InvalidPointHandle)
                        {
                            _gripsProvider.ClickPoint(pointIndex);
                            _objectHost.HostViewer.InvalidateViewer(new MultiLayerViewerInvalidationTarget(_gripsProvider.GetInvalidationRectangle(), _objectHost.CurrentLayer));
                        }
                    }

                    _obj.DrawMode = VObjectDrawMode.Normal;
                    _dragging = false;
                    _dragPointIndex = GripsProvider.InvalidPointHandle;

                    if (_snapController.Active)
                    {
                        System.Drawing.Rectangle snapInvalidRect = _snapController.ClearGuides();
                        _snapController.End();
                        if (!snapInvalidRect.IsEmpty)
                            _objectHost.HostViewer.InvalidateViewer(new MultiLayerViewerInvalidationTarget(snapInvalidRect));
                    }

                    if (saveState)
                        _objectHost.SaveState();

                    _obj.Update();
                }
                finally
                {
                    EndDragUndoRedoTransaction(false);
                }
            }

            return true;
        }

        public virtual bool NotifyMouseDown(System.Windows.Forms.MouseEventArgs e)
        {
            if (e == null)
                throw new System.ArgumentNullException("e");

            EndDragUndoRedoTransaction(false);
            _dragging = false;
            System.Drawing.Point clickedPoint = new System.Drawing.Point(e.X, e.Y);

            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                //
                // Check if a control points was clicked
                //
                if (_gripsProvider != null)
                {
                    _dragPointIndex = _gripsProvider.TestPoint(clickedPoint);
                    if (_dragPointIndex != GripsProvider.InvalidPointHandle)
                    {
                        _dragging = true;
                        _dragBeginPoint = clickedPoint;
                        _previousDragPoint = clickedPoint;
                        BeginDragUndoRedoTransaction();

                        if (_dragPointIndex == 17 && _snapEnabled)
                            _snapController.Begin(_objectHost, _obj);

                        return true;
                    }
                }

                //
                // Check for a click on another object
                //
                // If MultiSelect option is on we should also process Ctrl+Click action. If another
                // object has been clicked - it should be added to the selected objects.
                IVObject clickedObj = _objectHost.CurrentLayer.Find(_objectHost.HostViewer.ControlToWorkspace(new System.Drawing.Point(e.X, e.Y), Aurigma.GraphicsMill.Unit.Point), VObject.SelectionPrecisionDelta / _objectHost.HostViewer.GetControlPixelsPerUnitX(Aurigma.GraphicsMill.Unit.Point));
                if (clickedObj != _obj)
                {
                    if (_multiSelect && (System.Windows.Forms.Control.ModifierKeys & System.Windows.Forms.Keys.Control) == System.Windows.Forms.Keys.Control && clickedObj != null && !clickedObj.Locked && !_obj.Locked)
                    {
                        _objectHost.CurrentDesigner = new CompositeVObjectEditDesigner(new IVObject[] { _obj, clickedObj });
                        return true;
                    }
                    else if (clickedObj != null)
                    {
                        _objectHost.CurrentDesigner = clickedObj.Designer;
                        return true;
                    }
                }
            }
            else if (e.Button == System.Windows.Forms.MouseButtons.Right)
            {
                //
                // Context menu handling
                //
                if (_contextMenu != null)
                {
                    IVObject clickedObj = _objectHost.CurrentLayer.Find(_objectHost.HostViewer.ControlToWorkspace(new System.Drawing.Point(e.X, e.Y), Aurigma.GraphicsMill.Unit.Point), VObject.SelectionPrecisionDelta / _objectHost.HostViewer.GetControlPixelsPerUnitX(Aurigma.GraphicsMill.Unit.Point));
                    if (clickedObj == _obj)
                        _contextMenu.Show(_objectHost.HostViewer, clickedPoint);
                }

                return true;
            }

            return false;
        }

        public virtual bool NotifyMouseMove(System.Windows.Forms.MouseEventArgs e)
        {
            if (e == null)
                throw new System.ArgumentNullException("e");

            if (_gripsProvider == null)
                return false;

            if (!_obj.Locked && _dragging && e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                System.Drawing.Rectangle invalidRect = GetObjectInvalidationArea();
                IControlPointsProvider icpp = _obj as IControlPointsProvider;

                ResizeMode prevResizeMode = ResizeMode.Arbitrary;
                bool resizeProportionally = false;
                if (_resizeProportionallyWithShift && (System.Windows.Forms.Control.ModifierKeys & System.Windows.Forms.Keys.Shift) == System.Windows.Forms.Keys.Shift && icpp != null && icpp.SupportedActions.Contains(VObjectAction.Resize))
                {
                    ResizeVObjectAction resizeAction = (ResizeVObjectAction)icpp.SupportedActions[VObjectAction.Resize];
                    if (resizeAction.ResizeMode != ResizeMode.None)
                    {
                        resizeProportionally = true;
                        prevResizeMode = resizeAction.ResizeMode;
                        resizeAction.ResizeMode = ResizeMode.Proportional;
                    }
                }

                _obj.DrawMode = VObjectDrawMode.Draft;

                System.Drawing.Point snappedPoint = new System.Drawing.Point(e.X, e.Y);
                System.Drawing.Point dragDeltaInControl = new System.Drawing.Point(snappedPoint.X - _previousDragPoint.X, snappedPoint.Y - _previousDragPoint.Y);
                if (_dragPointIndex == 17 && _snapEnabled && _snapController.Active)
                {
                    int detectTolerancePixels = ConvertPointsToControlPixels(_snapDetectTolerance);
                    int applyTolerancePixels = ConvertPointsToControlPixels(_snapApplyTolerance);
                    int releaseTolerancePixels = ConvertPointsToControlPixels(_snapReleaseTolerance);

                    System.Drawing.Point snapOffset;
                    System.Drawing.Rectangle snapInvalidRect = _snapController.UpdateSnap(_obj, dragDeltaInControl, detectTolerancePixels, applyTolerancePixels, releaseTolerancePixels, _snapNearestOnly, out snapOffset);
                    snappedPoint.Offset(snapOffset);

                    if (!snapInvalidRect.IsEmpty)
                        invalidRect = System.Drawing.Rectangle.Union(invalidRect, snapInvalidRect);
                }

                _gripsProvider.DragPoint(_dragPointIndex, snappedPoint);
                _previousDragPoint = snappedPoint;

                if (resizeProportionally)
                    ((ResizeVObjectAction)icpp.SupportedActions[VObjectAction.Resize]).ResizeMode = prevResizeMode;

                invalidRect = System.Drawing.Rectangle.Union(invalidRect, GetObjectInvalidationArea());
                _objectHost.HostViewer.InvalidateViewer(new MultiLayerViewerInvalidationTarget(invalidRect, _objectHost.CurrentLayer));
            }
            else
            {
                UpdateCursor(e.X, e.Y);
            }

            return true;
        }

        public virtual bool NotifyMouseDoubleClick(System.EventArgs e)
        {
            return true;
        }

        public virtual bool NotifyKeyUp(System.Windows.Forms.KeyEventArgs e)
        {
            return true;
        }

        public virtual bool NotifyKeyDown(System.Windows.Forms.KeyEventArgs e)
        {
            return true;
        }

        public virtual IVObject[] VObjects
        {
            get
            {
                return new IVObject[] { _obj };
            }
        }

        public bool Connected
        {
            get
            {
                return _objectHost != null;
            }
        }

        #endregion IDesigner Members

        #region "Other members - unsorted"

        private System.Drawing.Rectangle GetObjectInvalidationArea()
        {
            System.Drawing.Rectangle result = GripsProvider.GetInvalidationRectangle();
            result = System.Drawing.Rectangle.Union(result, _objectHost.HostViewer.WorkspaceToControl(_obj.GetTransformedVObjectBounds(), Aurigma.GraphicsMill.Unit.Point));
            result.Inflate(VObject.InvalidationMargin);
            return result;
        }

        protected void InvalidateObjectArea()
        {
            _objectHost.HostViewer.InvalidateViewer(new MultiLayerViewerInvalidationTarget(GetObjectInvalidationArea(), _objectHost.CurrentLayer));
        }

        protected void InvalidateDesigner()
        {
            System.Drawing.Rectangle invalidationRect = GripsProvider.GetInvalidationRectangle();
            invalidationRect.Inflate(VObject.InvalidationMargin);
            _objectHost.HostViewer.InvalidateViewer(new MultiLayerViewerInvalidationTarget(invalidationRect));
        }

        private int ConvertPointsToControlPixels(float points)
        {
            if (points <= 0)
                return 0;

            float pixelsPerPoint = _objectHost.HostViewer.GetControlPixelsPerUnitX(Aurigma.GraphicsMill.Unit.Point);
            if (pixelsPerPoint <= VObject.Eps)
                return 0;

            return (int)System.Math.Round(points * pixelsPerPoint);
        }

        private void UpdateCursor(int x, int y)
        {
            int point = _gripsProvider.TestPoint(new System.Drawing.Point(x, y));
            if (point != GripsProvider.InvalidPointHandle)
                _objectHost.HostViewer.Cursor = GripsProvider.GetCursor(point);
            else
                _objectHost.HostViewer.RestoreCursorToDefault();
        }

        protected virtual void ObjectChangedHandler(object sender, System.EventArgs e)
        {
            if (this.Connected)
                InvalidateObjectArea();
        }

        private void BeginDragUndoRedoTransaction()
        {
            _undoRedoTrackingSuspendedForDrag = false;
            _undoRedoTrackingEnabledBeforeDrag = false;

            if (_objectHost == null || !_objectHost.UndoRedoEnabled)
                return;

            VObjectHost host = _objectHost as VObjectHost;
            if (host != null)
                host.BeginUndoRedoTransaction();

            _undoRedoTrackingEnabledBeforeDrag = _objectHost.UndoRedoTrackingEnabled;
            if (_undoRedoTrackingEnabledBeforeDrag)
            {
                _objectHost.UndoRedoTrackingEnabled = false;
                _undoRedoTrackingSuspendedForDrag = true;
            }
        }

        private void EndDragUndoRedoTransaction(bool saveState)
        {
            if (_objectHost == null)
                return;

            try
            {
                if (saveState && _objectHost.UndoRedoEnabled && _undoRedoTrackingEnabledBeforeDrag)
                    _objectHost.SaveState();
            }
            finally
            {
                VObjectHost host = _objectHost as VObjectHost;
                if (host != null)
                    host.EndUndoRedoTransaction(saveState && _objectHost.UndoRedoEnabled && _undoRedoTrackingEnabledBeforeDrag);

                if (_undoRedoTrackingSuspendedForDrag)
                    _objectHost.UndoRedoTrackingEnabled = _undoRedoTrackingEnabledBeforeDrag;

                _undoRedoTrackingSuspendedForDrag = false;
                _undoRedoTrackingEnabledBeforeDrag = false;
            }
        }

        #endregion "Other members - unsorted"

        #region "Trivial properties"

        protected IVObject ActualVObject
        {
            get
            {
                return _obj;
            }
            set
            {
                _obj = value;
            }
        }

        protected IVObjectHost VObjectHost
        {
            get
            {
                return _objectHost;
            }
        }

        protected bool Dragging
        {
            get
            {
                return _dragging;
            }
            set
            {
                _dragging = value;
            }
        }

        protected int DraggingPointIndex
        {
            get
            {
                return _dragPointIndex;
            }
            set
            {
                _dragPointIndex = value;
            }
        }

        protected bool MultiSelect
        {
            get
            {
                return _multiSelect;
            }
            set
            {
                _multiSelect = value;
            }
        }

        internal GripsProvider GripsProvider
        {
            get
            {
                return _gripsProvider;
            }
        }

        public System.Windows.Forms.ContextMenu ContextMenu
        {
            get
            {
                return _contextMenu;
            }
            set
            {
                _contextMenu = value;
            }
        }

        public System.Drawing.Pen ObjectBorderPen
        {
            get
            {
                return _objectBorderPen;
            }
            set
            {
                _objectBorderPen = value;
                if (_gripsProvider != null)
                    _gripsProvider.VObjectBorderPen = value;
            }
        }

        #endregion "Trivial properties"

        #region "Member variables"

        private IVObject _obj;

        private GripsProvider _gripsProvider;
        private IVObjectHost _objectHost;

        private bool _dragging;
        private int _dragPointIndex;
        private System.Drawing.Point _dragBeginPoint;
        private System.Drawing.Point _previousDragPoint;

        private bool _multiSelect;
        private bool _resizeProportionallyWithShift;
        private bool _snapEnabled;
        private bool _snapNearestOnly;
        private float _snapDetectTolerance;
        private float _snapApplyTolerance;
        private float _snapReleaseTolerance;
        private System.Drawing.Color _snapGuideLineColor;
        private VObjectSnapController _snapController;

        private System.Windows.Forms.ContextMenu _contextMenu;
        private System.Drawing.Pen _objectBorderPen;

        private bool _undoRedoTrackingSuspendedForDrag;
        private bool _undoRedoTrackingEnabledBeforeDrag;

        #endregion "Member variables"
    }

    internal sealed class VObjectSnapController
    {
        private const int GuideLineInvalidationMargin = 3;

        private readonly System.Collections.Generic.List<float> _candidateX = new System.Collections.Generic.List<float>(64);
        private readonly System.Collections.Generic.List<float> _candidateY = new System.Collections.Generic.List<float>(64);
        private readonly System.Collections.Generic.HashSet<IVObject> _excludedObjects = new System.Collections.Generic.HashSet<IVObject>();

        private IVObjectHost _objectHost;

        private bool _hasVerticalGuide;
        private float _verticalGuideX;

        private bool _hasHorizontalGuide;
        private float _horizontalGuideY;

        private bool _verticalGuideLocked;
        private float _lockedVerticalGuideX;
        private int _lockedVerticalObjectGuideIndex;

        private bool _horizontalGuideLocked;
        private float _lockedHorizontalGuideY;
        private int _lockedHorizontalObjectGuideIndex;

        public bool Active
        {
            get
            {
                return _objectHost != null;
            }
        }

        public void Begin(IVObjectHost objectHost, IVObject movingObject)
        {
            if (objectHost == null)
                throw new System.ArgumentNullException("objectHost");
            if (movingObject == null)
                throw new System.ArgumentNullException("movingObject");

            _objectHost = objectHost;

            _excludedObjects.Clear();
            _excludedObjects.Add(movingObject);

            CompositeVObject composite = movingObject as CompositeVObject;
            if (composite != null)
            {
                foreach (IVObject child in composite.Children)
                    _excludedObjects.Add(child);
            }

            BuildCandidates();
            ResetGuides();
        }

        public System.Drawing.Rectangle ClearGuides()
        {
            System.Drawing.Rectangle oldRect = GetGuidesInvalidationRectangle();
            ResetGuides();
            return oldRect;
        }

        public void End()
        {
            _objectHost = null;
            _candidateX.Clear();
            _candidateY.Clear();
            _excludedObjects.Clear();
            ResetGuides();
        }

        public System.Drawing.Rectangle UpdateSnap(IVObject movingObject, System.Drawing.Point dragDeltaInControl, int detectTolerancePixels, int applyTolerancePixels, int releaseTolerancePixels, bool nearestOnly, out System.Drawing.Point snapOffset)
        {
            if (movingObject == null)
                throw new System.ArgumentNullException("movingObject");

            snapOffset = System.Drawing.Point.Empty;

            if (!this.Active || detectTolerancePixels < 0 || applyTolerancePixels < 0 || releaseTolerancePixels < 0)
                return System.Drawing.Rectangle.Empty;

            if (detectTolerancePixels < applyTolerancePixels)
                detectTolerancePixels = applyTolerancePixels;
            if (releaseTolerancePixels < applyTolerancePixels)
                releaseTolerancePixels = applyTolerancePixels;

            float pixelsPerPointX = _objectHost.HostViewer.GetControlPixelsPerUnitX(Aurigma.GraphicsMill.Unit.Point);
            float pixelsPerPointY = _objectHost.HostViewer.GetControlPixelsPerUnitY(Aurigma.GraphicsMill.Unit.Point);
            if (pixelsPerPointX <= VObject.Eps || pixelsPerPointY <= VObject.Eps)
                return System.Drawing.Rectangle.Empty;

            float detectToleranceX = detectTolerancePixels / pixelsPerPointX;
            float applyToleranceX = applyTolerancePixels / pixelsPerPointX;
            float releaseToleranceX = releaseTolerancePixels / pixelsPerPointX;

            float detectToleranceY = detectTolerancePixels / pixelsPerPointY;
            float applyToleranceY = applyTolerancePixels / pixelsPerPointY;
            float releaseToleranceY = releaseTolerancePixels / pixelsPerPointY;

            System.Drawing.RectangleF movingBoundsInWorkspace = GetSnapBoundsInWorkspace(movingObject);
            System.Drawing.RectangleF tentativeBoundsInWorkspace = movingBoundsInWorkspace;
            tentativeBoundsInWorkspace.Offset(dragDeltaInControl.X / pixelsPerPointX, dragDeltaInControl.Y / pixelsPerPointY);

            System.Drawing.Rectangle oldGuidesRect = GetGuidesInvalidationRectangle();

            float bestDeltaX;
            float bestGuideX;
            int bestGuideIndexX;
            bool foundX = FindBestDelta(
                _candidateX,
                GetObjectXGuides(tentativeBoundsInWorkspace),
                detectToleranceX,
                applyToleranceX,
                releaseToleranceX,
                _verticalGuideLocked,
                _lockedVerticalGuideX,
                _lockedVerticalObjectGuideIndex,
                nearestOnly,
                out bestDeltaX,
                out bestGuideX,
                out bestGuideIndexX);

            float bestDeltaY;
            float bestGuideY;
            int bestGuideIndexY;
            bool foundY = FindBestDelta(
                _candidateY,
                GetObjectYGuides(tentativeBoundsInWorkspace),
                detectToleranceY,
                applyToleranceY,
                releaseToleranceY,
                _horizontalGuideLocked,
                _lockedHorizontalGuideY,
                _lockedHorizontalObjectGuideIndex,
                nearestOnly,
                out bestDeltaY,
                out bestGuideY,
                out bestGuideIndexY);

            int snapX = foundX ? (int)System.Math.Round(bestDeltaX * pixelsPerPointX) : 0;
            int snapY = foundY ? (int)System.Math.Round(bestDeltaY * pixelsPerPointY) : 0;
            snapOffset = new System.Drawing.Point(snapX, snapY);

            if (foundX)
            {
                _hasVerticalGuide = true;
                _verticalGuideX = bestGuideX;
                _verticalGuideLocked = true;
                _lockedVerticalGuideX = bestGuideX;
                _lockedVerticalObjectGuideIndex = bestGuideIndexX;
            }
            else
            {
                _hasVerticalGuide = false;
                _verticalGuideLocked = false;
                _lockedVerticalObjectGuideIndex = -1;
            }

            if (foundY)
            {
                _hasHorizontalGuide = true;
                _horizontalGuideY = bestGuideY;
                _horizontalGuideLocked = true;
                _lockedHorizontalGuideY = bestGuideY;
                _lockedHorizontalObjectGuideIndex = bestGuideIndexY;
            }
            else
            {
                _hasHorizontalGuide = false;
                _horizontalGuideLocked = false;
                _lockedHorizontalObjectGuideIndex = -1;
            }

            System.Drawing.Rectangle newGuidesRect = GetGuidesInvalidationRectangle();
            if (oldGuidesRect.IsEmpty)
                return newGuidesRect;
            if (newGuidesRect.IsEmpty)
                return oldGuidesRect;

            return System.Drawing.Rectangle.Union(oldGuidesRect, newGuidesRect);
        }

        public void Draw(System.Drawing.Graphics g, System.Drawing.Color guideColor)
        {
            if (g == null)
                throw new System.ArgumentNullException("g");
            if (!this.Active)
                return;
            bool showVerticalGuide = _verticalGuideLocked;
            bool showHorizontalGuide = _horizontalGuideLocked;
            if (!showVerticalGuide && !showHorizontalGuide)
                return;

            System.Drawing.Rectangle viewport = _objectHost.HostViewer.GetViewportBounds();
            using (System.Drawing.Pen pen = new System.Drawing.Pen(guideColor, 1.0f))
            {
                pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;

                if (showVerticalGuide)
                {
                    int x = _objectHost.HostViewer.WorkspaceToControl(new System.Drawing.PointF(_verticalGuideX, 0), Aurigma.GraphicsMill.Unit.Point).X;
                    g.DrawLine(pen, x, viewport.Top, x, viewport.Bottom);
                }

                if (showHorizontalGuide)
                {
                    int y = _objectHost.HostViewer.WorkspaceToControl(new System.Drawing.PointF(0, _horizontalGuideY), Aurigma.GraphicsMill.Unit.Point).Y;
                    g.DrawLine(pen, viewport.Left, y, viewport.Right, y);
                }
            }
        }

        private void BuildCandidates()
        {
            _candidateX.Clear();
            _candidateY.Clear();

            AddWorkspaceGuides();

            if (_objectHost.CurrentLayer == null)
                return;

            VObjectCollection objects = _objectHost.CurrentLayer.VObjects;
            for (int i = 0; i < objects.Count; i++)
            {
                IVObject obj = objects[i];
                if (_excludedObjects.Contains(obj))
                    continue;

                System.Drawing.RectangleF bounds = GetSnapBoundsInWorkspace(obj);
                if (bounds.Width <= VObject.Eps || bounds.Height <= VObject.Eps)
                    continue;

                AddObjectGuides(bounds, _candidateX, _candidateY);
            }
        }

        private void AddWorkspaceGuides()
        {
            if (_objectHost == null || _objectHost.HostViewer == null)
                return;

            ViewerBase hostViewer = _objectHost.HostViewer;
            float workspaceWidth = Aurigma.GraphicsMill.UnitConverter.ConvertUnitsToUnits(hostViewer.ViewerResolution, hostViewer.WorkspaceWidth, hostViewer.Unit, Aurigma.GraphicsMill.Unit.Point);
            float workspaceHeight = Aurigma.GraphicsMill.UnitConverter.ConvertUnitsToUnits(hostViewer.ViewerResolution, hostViewer.WorkspaceHeight, hostViewer.Unit, Aurigma.GraphicsMill.Unit.Point);

            if (workspaceWidth <= VObject.Eps || workspaceHeight <= VObject.Eps)
                return;

            AddObjectGuides(System.Drawing.RectangleF.FromLTRB(0, 0, workspaceWidth, workspaceHeight), _candidateX, _candidateY);
        }

        private static void AddObjectGuides(System.Drawing.RectangleF bounds, System.Collections.Generic.List<float> xGuides, System.Collections.Generic.List<float> yGuides)
        {
            float left = bounds.Left;
            float right = bounds.Right;
            float top = bounds.Top;
            float bottom = bounds.Bottom;

            xGuides.Add(left);
            xGuides.Add((left + right) / 2.0f);
            xGuides.Add(right);

            yGuides.Add(top);
            yGuides.Add((top + bottom) / 2.0f);
            yGuides.Add(bottom);
        }

        private static float[] GetObjectXGuides(System.Drawing.RectangleF bounds)
        {
            float left = bounds.Left;
            float right = bounds.Right;

            return new float[]
            {
                left,
                (left + right) / 2.0f,
                right
            };
        }

        private static float[] GetObjectYGuides(System.Drawing.RectangleF bounds)
        {
            float top = bounds.Top;
            float bottom = bounds.Bottom;

            return new float[]
            {
                top,
                (top + bottom) / 2.0f,
                bottom
            };
        }

        private static bool FindBestDelta(
            System.Collections.Generic.List<float> candidates,
            float[] objectGuides,
            float detectTolerance,
            float applyTolerance,
            float releaseTolerance,
            bool lockEnabled,
            float lockedGuide,
            int lockedObjectGuideIndex,
            bool nearestOnly,
            out float delta,
            out float guide,
            out int objectGuideIndex)
        {
            delta = 0;
            guide = 0;
            objectGuideIndex = -1;

            if (candidates.Count == 0)
                return false;

            if (lockEnabled)
            {
                float lockThreshold = nearestOnly ? detectTolerance : releaseTolerance;
                if (FindDeltaToSpecificGuide(objectGuides, lockedGuide, lockedObjectGuideIndex, lockThreshold, out delta, out objectGuideIndex))
                {
                    guide = lockedGuide;

                    if (System.Math.Abs(delta) > applyTolerance)
                        delta = 0;

                    return true;
                }

                return false;
            }

            if (!nearestOnly)
            {
                for (int i = 0; i < objectGuides.Length; i++)
                {
                    float objectGuide = objectGuides[i];
                    for (int j = 0; j < candidates.Count; j++)
                    {
                        float candidate = candidates[j];
                        float currentDelta = candidate - objectGuide;
                        float absDelta = System.Math.Abs(currentDelta);
                        if (absDelta <= applyTolerance)
                        {
                            delta = currentDelta;
                            guide = candidate;
                            objectGuideIndex = i;
                            return true;
                        }
                    }
                }

                return false;
            }

            float bestAbsDelta = float.MaxValue;
            bool found = false;

            for (int i = 0; i < objectGuides.Length; i++)
            {
                float objectGuide = objectGuides[i];
                for (int j = 0; j < candidates.Count; j++)
                {
                    float candidate = candidates[j];
                    float currentDelta = candidate - objectGuide;
                    float absDelta = System.Math.Abs(currentDelta);

                    if (absDelta > detectTolerance)
                        continue;

                    if (!found || absDelta < bestAbsDelta)
                    {
                        found = true;
                        bestAbsDelta = absDelta;
                        delta = currentDelta;
                        guide = candidate;
                        objectGuideIndex = i;
                    }
                }
            }

            if (!found)
                return false;

            if (System.Math.Abs(delta) <= applyTolerance)
                return true;

            delta = 0;
            return true;
        }

        private static bool FindDeltaToSpecificGuide(float[] objectGuides, float specificGuide, int preferredGuideIndex, float threshold, out float delta, out int guideIndex)
        {
            delta = 0;
            guideIndex = -1;

            if (preferredGuideIndex >= 0 && preferredGuideIndex < objectGuides.Length)
            {
                float preferredDelta = specificGuide - objectGuides[preferredGuideIndex];
                if (System.Math.Abs(preferredDelta) <= threshold)
                {
                    delta = preferredDelta;
                    guideIndex = preferredGuideIndex;
                    return true;
                }

                return false;
            }

            float bestAbsDelta = float.MaxValue;
            bool found = false;
            for (int i = 0; i < objectGuides.Length; i++)
            {
                float currentDelta = specificGuide - objectGuides[i];
                float absDelta = System.Math.Abs(currentDelta);
                if (absDelta > threshold)
                    continue;

                if (!found || absDelta < bestAbsDelta)
                {
                    found = true;
                    bestAbsDelta = absDelta;
                    delta = currentDelta;
                    guideIndex = i;
                }
            }

            return found;
        }

        private System.Drawing.RectangleF GetSnapBoundsInWorkspace(IVObject obj)
        {
            if (obj == null)
                throw new System.ArgumentNullException("obj");

            return obj.GetTransformedVObjectBounds();
        }

        private System.Drawing.Rectangle GetGuidesInvalidationRectangle()
        {
            bool showVerticalGuide = _verticalGuideLocked;
            bool showHorizontalGuide = _horizontalGuideLocked;
            if (!this.Active || (!showVerticalGuide && !showHorizontalGuide))
                return System.Drawing.Rectangle.Empty;

            System.Drawing.Rectangle viewport = _objectHost.HostViewer.GetViewportBounds();
            System.Drawing.Rectangle result = System.Drawing.Rectangle.Empty;

            if (showVerticalGuide)
            {
                int x = _objectHost.HostViewer.WorkspaceToControl(new System.Drawing.PointF(_verticalGuideX, 0), Aurigma.GraphicsMill.Unit.Point).X;
                System.Drawing.Rectangle verticalRect = new System.Drawing.Rectangle(x - 1, viewport.Top, 3, System.Math.Max(1, viewport.Height));
                result = verticalRect;
            }

            if (showHorizontalGuide)
            {
                int y = _objectHost.HostViewer.WorkspaceToControl(new System.Drawing.PointF(0, _horizontalGuideY), Aurigma.GraphicsMill.Unit.Point).Y;
                System.Drawing.Rectangle horizontalRect = new System.Drawing.Rectangle(viewport.Left, y - 1, System.Math.Max(1, viewport.Width), 3);
                result = result.IsEmpty ? horizontalRect : System.Drawing.Rectangle.Union(result, horizontalRect);
            }

            if (!result.IsEmpty)
                result.Inflate(GuideLineInvalidationMargin, GuideLineInvalidationMargin);

            return result;
        }

        private void ResetGuides()
        {
            _hasVerticalGuide = false;
            _verticalGuideX = 0;
            _hasHorizontalGuide = false;
            _horizontalGuideY = 0;
            _verticalGuideLocked = false;
            _lockedVerticalGuideX = 0;
            _lockedVerticalObjectGuideIndex = -1;
            _horizontalGuideLocked = false;
            _lockedHorizontalGuideY = 0;
            _lockedHorizontalObjectGuideIndex = -1;
        }
    }
}