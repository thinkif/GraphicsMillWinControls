// Copyright (c) 2018 Aurigma Inc. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
//
namespace Aurigma.GraphicsMill.WinControls
{
    /// <summary>
    /// Caching renderer of the multiple vector layers.
    /// </summary>
    internal class MultipleLayerRenderer : CachingRendererImpl
    {
        #region "------- Member variables ---------"

        private static int bgGridCellSize = 15;

        private float _renderingResolution;
        private bool _renderBackground;

        private WorkspaceBackgroundStyle _workspaceBackgroundStyle;
        private System.Drawing.Color _backColor;
        private System.Drawing.Color _workspaceBackColor1;
        private System.Drawing.Color _workspaceBackColor2;

        private IViewportImageRenderer[] _childRenderers;
        private System.Drawing.RectangleF _invalidatedRectangle;
        private Aurigma.GraphicsMill.Bitmap _bgGridTemplate;

        #endregion "------- Member variables ---------"

        public MultipleLayerRenderer(bool renderBackground, float renderingResolution)
            : base(renderingResolution)
        {
            if (renderingResolution < 1)
                throw new System.ArgumentOutOfRangeException("renderResolution");

            _renderBackground = renderBackground;
            _renderingResolution = renderingResolution;

            _childRenderers = new IViewportImageRenderer[0];
            _invalidatedRectangle = System.Drawing.RectangleF.Empty;

            _workspaceBackgroundStyle = Aurigma.GraphicsMill.WinControls.WorkspaceBackgroundStyle.Grid;
            _workspaceBackColor1 = System.Drawing.Color.LightGray;
            _workspaceBackColor2 = System.Drawing.Color.DarkGray;
            _backColor = System.Drawing.Color.LightGray;
        }

        private void DisposeLayerRenders()
        {
            for (int i = 0; i < _childRenderers.Length; i++)
            {
                _childRenderers[i].Dispose();
                _childRenderers[i] = null;
            }

            _childRenderers = new IViewportImageRenderer[0];
        }

        public void SetLayers(LayerCollection layers)
        {
            if (layers == null)
                throw new System.ArgumentNullException("layers");

            DisposeLayerRenders();

            _childRenderers = new IViewportImageRenderer[layers.Count];
            for (int i = 0; i < _childRenderers.Length; i++)
                _childRenderers[i] = new LayerRenderer(layers[i], _renderingResolution);
        }

        protected override void DrawNoncachedArea(Aurigma.GraphicsMill.Bitmap canvas, float zoom, System.Drawing.Rectangle viewport, System.Drawing.Rectangle renderingRegion)
        {
            if (_renderBackground)
                DrawViewportBackground(canvas, viewport, renderingRegion);

            for (int i = 0; i < _childRenderers.Length; i++)
                _childRenderers[i].Render(canvas, zoom, viewport, renderingRegion);
        }

        public override void InvalidateLayerRegion(Layer layer, System.Drawing.RectangleF invalidationRectangle)
        {
            for (int i = 0; i < _childRenderers.Length; i++)
                _childRenderers[i].InvalidateLayerRegion(layer, invalidationRectangle);

            if (this.InvalidatedRegion.IsEmpty)
                this.InvalidatedRegion = invalidationRectangle;
            else
                this.InvalidatedRegion = System.Drawing.RectangleF.Union(this.InvalidatedRegion, invalidationRectangle);
        }

        protected override System.Drawing.RectangleF InvalidatedRegion
        {
            get
            {
                return _invalidatedRectangle;
            }
            set
            {
                _invalidatedRectangle = value;
            }
        }

        #region "Viewport background rendering"

        private void CreateBackgroundGridTemplate(int width)
        {
            if (width < 1)
                throw new System.ArgumentOutOfRangeException("width", StringResources.GetString("ExStrValueShouldBeAboveZero"));
            if (_bgGridTemplate != null && _bgGridTemplate.Width >= width)
                return;

            if (_bgGridTemplate != null)
                _bgGridTemplate.Dispose();

            _bgGridTemplate = new Aurigma.GraphicsMill.Bitmap(width, 2 * bgGridCellSize, Aurigma.GraphicsMill.PixelFormat.Format24bppRgb);

            using (Aurigma.GraphicsMill.Drawing.Graphics g = _bgGridTemplate.GetGraphics())
            {
                Aurigma.GraphicsMill.Drawing.SolidBrush brush0 = new Aurigma.GraphicsMill.Drawing.SolidBrush(_workspaceBackColor1),
                                                        brush1 = new Aurigma.GraphicsMill.Drawing.SolidBrush(_workspaceBackColor2);

                System.Drawing.RectangleF cellRect = new System.Drawing.RectangleF(0, 0, bgGridCellSize, bgGridCellSize);

                int n = (int)System.Math.Ceiling((float)width / (2.0f * bgGridCellSize));
                for (int i = 0; i < n; i++)
                {
                    g.FillRectangle(brush0, cellRect);
                    cellRect.Offset(bgGridCellSize, 0);
                    g.FillRectangle(brush1, cellRect);
                    cellRect.Offset(bgGridCellSize, 0);
                }

                cellRect.Location = new System.Drawing.PointF(0, bgGridCellSize);
                for (int i = 0; i < n; i++)
                {
                    g.FillRectangle(brush1, cellRect);
                    cellRect.Offset(bgGridCellSize, 0);
                    g.FillRectangle(brush0, cellRect);
                    cellRect.Offset(bgGridCellSize, 0);
                }
            }
        }

        private void DrawViewportBackground(Aurigma.GraphicsMill.Bitmap canvas, System.Drawing.Rectangle viewport, System.Drawing.Rectangle renderingRegion)
        {
            using (Aurigma.GraphicsMill.Drawing.Graphics g = canvas.GetGraphics())
            {
                System.Drawing.Rectangle screenRect = renderingRegion;
                screenRect.X -= viewport.X;
                screenRect.Y -= viewport.Y;

                if (_workspaceBackgroundStyle == Aurigma.GraphicsMill.WinControls.WorkspaceBackgroundStyle.Grid)
                {
                    int gridPatternSize = 2 * bgGridCellSize;
                    int patternOffsetX = renderingRegion.X % gridPatternSize,
                        patternOffsetY = renderingRegion.Y % gridPatternSize;

                    CreateBackgroundGridTemplate(renderingRegion.Width + gridPatternSize);

                    int tileX = screenRect.X - patternOffsetX,
                        tileY = screenRect.Y - patternOffsetY;
                    int templateWidth = _bgGridTemplate.Width,
                        templateHeight = _bgGridTemplate.Height;

                    int templateRepeats = (int)System.Math.Ceiling((float)(renderingRegion.Height + patternOffsetY) / templateHeight);
                    for (int j = 0; j < templateRepeats; j++)
                    {
                        var tileRect = new System.Drawing.Rectangle(tileX, tileY + j * templateHeight, templateWidth, templateHeight);
                        System.Drawing.Rectangle visibleRectangle = System.Drawing.Rectangle.Intersect(tileRect, screenRect);

                        if (visibleRectangle.Width < 1 || visibleRectangle.Height < 1)
                            continue;

                        // The drawing engine has no rectangle clip, so only the visible part of the tile
                        // is rendered (same result as the original SetClip/ResetClip based drawing).
                        var sourceRectangle = new System.Drawing.Rectangle(visibleRectangle.X - tileRect.X, visibleRectangle.Y - tileRect.Y, visibleRectangle.Width, visibleRectangle.Height);

                        using (var ct = new Aurigma.GraphicsMill.Transforms.Crop(sourceRectangle))
                        using (var tile = ct.Apply(_bgGridTemplate))
                        {
                            g.DrawImage(tile, visibleRectangle.X, visibleRectangle.Y);
                        }
                    }
                }
                else if (_workspaceBackgroundStyle == Aurigma.GraphicsMill.WinControls.WorkspaceBackgroundStyle.Solid)
                {
                    FillBackgroundRectangle(g, new Aurigma.GraphicsMill.Drawing.SolidBrush(_workspaceBackColor1), screenRect);
                }
                else
                {
                    FillBackgroundRectangle(g, new Aurigma.GraphicsMill.Drawing.SolidBrush(_backColor), screenRect);
                }
            }
        }

        private void FillBackgroundRectangle(Aurigma.GraphicsMill.Drawing.Graphics g, Aurigma.GraphicsMill.Drawing.SolidBrush brush, System.Drawing.Rectangle screenRect)
        {
            using (var fill = new Aurigma.GraphicsMill.Bitmap(screenRect.Width, screenRect.Height, Aurigma.GraphicsMill.PixelFormat.Format24bppRgb))
            {
                fill.Fill(brush.Color);
                g.DrawImage(fill, screenRect.X, screenRect.Y);
            }
        }

        public WorkspaceBackgroundStyle WorkspaceBackgroundStyle
        {
            get
            {
                return _workspaceBackgroundStyle;
            }
            set
            {
                _workspaceBackgroundStyle = value;
            }
        }

        public System.Drawing.Color WorkspaceBackColor1
        {
            get
            {
                return _workspaceBackColor1;
            }
            set
            {
                _workspaceBackColor1 = value;
            }
        }

        public System.Drawing.Color WorkspaceBackColor2
        {
            get
            {
                return _workspaceBackColor2;
            }
            set
            {
                _workspaceBackColor2 = value;
            }
        }

        public System.Drawing.Color BackColor
        {
            get
            {
                return _backColor;
            }
            set
            {
                _backColor = value;
            }
        }

        #endregion "Viewport background rendering"

        #region IDisposable Members

        public override void Dispose()
        {
            try
            {
                if (_bgGridTemplate != null)
                {
                    _bgGridTemplate.Dispose();
                    _bgGridTemplate = null;
                }

                DisposeLayerRenders();
            }
            finally
            {
                base.Dispose();
            }
        }

        #endregion IDisposable Members
    }
}