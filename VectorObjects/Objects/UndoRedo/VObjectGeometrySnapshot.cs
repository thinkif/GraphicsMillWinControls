// Copyright (c) 2018 Aurigma Inc. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
//
namespace Aurigma.GraphicsMill.WinControls
{
    internal sealed class VObjectGeometrySnapshot
    {
        private const float Epsilon = 0.00001f;
        private static readonly float[] IdentityElements = new float[] { 1f, 0f, 0f, 1f, 0f, 0f };

        public IVObject VObject;
        public float[] TransformElements;
        public float CurrentRotation;

        public static VObjectGeometrySnapshot Capture(IVObject obj)
        {
            if (obj == null)
                throw new System.ArgumentNullException("obj");

            VObjectGeometrySnapshot snapshot = new VObjectGeometrySnapshot();
            snapshot.VObject = obj;
            snapshot.CurrentRotation = obj.CurrentRotation;
            snapshot.TransformElements = CaptureTransformElements(obj);
            return snapshot;
        }

        private static float[] CaptureTransformElements(IVObject obj)
        {
            System.Drawing.Drawing2D.Matrix transform = null;
            try
            {
                transform = obj.Transform;
                if (transform == null)
                    return (float[])IdentityElements.Clone();

                using (System.Drawing.Drawing2D.Matrix clone = transform.Clone())
                {
                    float[] elements = clone.Elements;
                    if (elements == null || elements.Length != 6)
                        return (float[])IdentityElements.Clone();

                    return (float[])elements.Clone();
                }
            }
            catch (System.ArgumentException)
            {
                return (float[])IdentityElements.Clone();
            }
            catch (System.ObjectDisposedException)
            {
                return (float[])IdentityElements.Clone();
            }
        }

        public void Apply()
        {
            if (VObject == null)
                return;

            if (TransformElements == null || TransformElements.Length != 6)
                TransformElements = (float[])IdentityElements.Clone();

            System.Drawing.Drawing2D.Matrix matrix = new System.Drawing.Drawing2D.Matrix(
                TransformElements[0], TransformElements[1],
                TransformElements[2], TransformElements[3],
                TransformElements[4], TransformElements[5]);
            try
            {
                VObject.Transform = matrix;
                VObject.CurrentRotation = CurrentRotation;
            }
            finally
            {
                matrix.Dispose();
            }
        }

        public bool EqualsGeometry(VObjectGeometrySnapshot other)
        {
            if (other == null || VObject != other.VObject)
                return false;

            if (System.Math.Abs(CurrentRotation - other.CurrentRotation) > Epsilon)
                return false;

            if (TransformElements == null || other.TransformElements == null || TransformElements.Length != other.TransformElements.Length)
                return false;

            for (int i = 0; i < TransformElements.Length; i++)
            {
                if (System.Math.Abs(TransformElements[i] - other.TransformElements[i]) > Epsilon)
                    return false;
            }

            return true;
        }
    }
}
