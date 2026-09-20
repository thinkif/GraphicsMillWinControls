// Copyright (c) 2018 Aurigma Inc. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
//
namespace Aurigma.GraphicsMill.WinControls
{
    /// <summary>
    /// The implementation of the IViewportImageRenderer interface renders vector object layers.
    /// No caching, no optimizations except clipping not visible objects.
    /// </summary>
    internal class LayerRenderer : IViewportImageRenderer
    {
        #region "------- Member variables ---------"

        private Layer _layer;
        private float _renderingResolution;
        private System.Drawing.Bitmap _scratchBitmap;

        #endregion "------- Member variables ---------"

        public LayerRenderer(Layer layer, float renderingResolution)
        {
            if (renderingResolution < VObject.Eps)
                throw new System.ArgumentOutOfRangeException("renderingResolution");

            _renderingResolution = renderingResolution;
            _layer = layer;
        }

        public void Dispose()
        {
            if (_scratchBitmap != null)
            {
                _scratchBitmap.Dispose();
                _scratchBitmap = null;
            }
        }

        public void InvalidateLayerRegion(Layer layer, System.Drawing.RectangleF invalidationRectangle)
        {
        }

        public void Render(Aurigma.GraphicsMill.Bitmap canvas, float zoom, System.Drawing.Rectangle viewport, System.Drawing.Rectangle renderingRegion)
        {
            if (!_layer.Visible || _layer.VObjects.Count < 1)
                return;

            CoordinateMapper coordinateMapper = new CoordinateMapper();
            coordinateMapper.Viewport = viewport;
            coordinateMapper.Zoom = zoom;
            coordinateMapper.Resolution = _renderingResolution;

            renderingRegion.X -= viewport.X;
            renderingRegion.Y -= viewport.Y;

            // The legacy GDI-based graphics accessor (Bitmap.GetGraphics/GetGdiPlusGraphics) was removed in
            // Graphics Mill 12, so VObjects are rendered into a GDI+ bitmap and then alpha-blended onto the
            // canvas using Bitmap.Draw(overlay, 0, 0, CombineMode.Alpha), which preserves the original
            // GDI+ blending behavior. The scratch bitmap is reused between renders to avoid allocating a
            // full-canvas bitmap on every repaint.
            if (_scratchBitmap == null || _scratchBitmap.Width != canvas.Width || _scratchBitmap.Height != canvas.Height)
            {
                if (_scratchBitmap != null)
                    _scratchBitmap.Dispose();

                _scratchBitmap = new System.Drawing.Bitmap(canvas.Width, canvas.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            }

            using (var g = System.Drawing.Graphics.FromImage(_scratchBitmap))
            {
                g.Clear(System.Drawing.Color.Transparent);
                g.SetClip(renderingRegion);

                for (int i = 0; i < _layer.VObjects.Count; i++)
                {
                    System.Drawing.Rectangle bounds = coordinateMapper.WorkspaceToControl(_layer.VObjects[i].GetTransformedVObjectBounds(), Aurigma.GraphicsMill.Unit.Point);

                    if (bounds.IntersectsWith(renderingRegion))
                        _layer.VObjects[i].Draw(renderingRegion, g, coordinateMapper);
                }
            }

            using (var overlay = new Aurigma.GraphicsMill.Bitmap(_scratchBitmap))
            {
                canvas.Draw(overlay, 0, 0, Aurigma.GraphicsMill.Transforms.CombineMode.Alpha);
            }
        }
    }
}