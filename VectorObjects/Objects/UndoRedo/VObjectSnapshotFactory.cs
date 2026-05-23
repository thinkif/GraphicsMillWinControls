// Copyright (c) 2018 Aurigma Inc. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
//
namespace Aurigma.GraphicsMill.WinControls
{
    internal static class VObjectSnapshotFactory
    {
        private static readonly float[] IdentityElements = new float[] { 1f, 0f, 0f, 1f, 0f, 0f };

        public static VObjectStateSnapshot Capture(Layer layer, IVObject obj, int objectIndex)
        {
            if (layer == null)
                throw new System.ArgumentNullException("layer");
            if (obj == null)
                throw new System.ArgumentNullException("obj");

            VObjectStateSnapshot snapshot = new VObjectStateSnapshot();
            snapshot.Layer = layer;
            snapshot.VObject = obj;
            snapshot.ObjectIndex = objectIndex;
            snapshot.SerializedState = SerializeWithoutBitmap(obj);

            return snapshot;
        }

        public static void Apply(VObjectStateSnapshot snapshot)
        {
            if (snapshot == null)
                throw new System.ArgumentNullException("snapshot");
            if (snapshot.VObject == null)
                return;

            DeserializeWithoutBitmap(snapshot.VObject, snapshot.SerializedState);
            snapshot.VObject.Update();
        }

        private static byte[] SerializeWithoutBitmap(IVObject obj)
        {
            object state = CreateStateObject(obj);

            System.Runtime.Serialization.Formatters.Binary.BinaryFormatter formatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
            formatter.Binder = new VObjectSerializationBinder();

            using (System.IO.MemoryStream stream = new System.IO.MemoryStream())
            {
                formatter.Serialize(stream, state);
                return stream.ToArray();
            }
        }

        private static object CreateStateObject(IVObject obj)
        {
            ImageVObject imageObj = obj as ImageVObject;
            if (imageObj == null)
                return obj;

            ImageVObjectState state = new ImageVObjectState();
            state.Name = imageObj.Name;
            state.Locked = imageObj.Locked;
            state.CurrentRotation = imageObj.CurrentRotation;
            state.ImageScaleX = imageObj.ImageScaleX;
            state.ImageScaleY = imageObj.ImageScaleY;
            state.TransformElements = CaptureTransformElements(imageObj);
            return state;
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

        private static void DeserializeWithoutBitmap(IVObject destination, byte[] buffer)
        {
            if (destination == null)
                throw new System.ArgumentNullException("destination");
            if (buffer == null)
                return;

            System.Runtime.Serialization.Formatters.Binary.BinaryFormatter formatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
            formatter.Binder = new VObjectSerializationBinder();

            object state;
            using (System.IO.MemoryStream stream = new System.IO.MemoryStream(buffer))
            {
                state = formatter.Deserialize(stream);
            }

            IVObject sourceObject = state as IVObject;
            if (sourceObject != null)
            {
                ApplyFromVObject(destination, sourceObject);
                return;
            }

            ImageVObjectState imageState = state as ImageVObjectState;
            if (imageState != null)
            {
                ApplyFromImageState(destination, imageState);
                return;
            }

            throw new System.InvalidOperationException("Unsupported undo snapshot state.");
        }

        private static void ApplyFromVObject(IVObject destination, IVObject source)
        {
            destination.Name = source.Name;
            destination.Locked = source.Locked;
            destination.CurrentRotation = source.CurrentRotation;

            float[] elements = CaptureTransformElements(source);
            System.Drawing.Drawing2D.Matrix matrix = new System.Drawing.Drawing2D.Matrix(
                elements[0], elements[1],
                elements[2], elements[3],
                elements[4], elements[5]);
            destination.Transform = matrix;
        }

        private static void ApplyFromImageState(IVObject destination, ImageVObjectState state)
        {
            destination.Name = state.Name;
            destination.Locked = state.Locked;
            destination.CurrentRotation = state.CurrentRotation;

            float[] elements = state.TransformElements;
            if (elements == null || elements.Length != 6)
                elements = (float[])IdentityElements.Clone();

            System.Drawing.Drawing2D.Matrix matrix = new System.Drawing.Drawing2D.Matrix(
                elements[0], elements[1],
                elements[2], elements[3],
                elements[4], elements[5]);
            destination.Transform = matrix;

            ImageVObject destinationImage = destination as ImageVObject;
            if (destinationImage != null)
            {
                destinationImage.ImageScaleX = state.ImageScaleX;
                destinationImage.ImageScaleY = state.ImageScaleY;
            }
        }

        [System.Serializable]
        private sealed class ImageVObjectState
        {
            public string Name;
            public bool Locked;
            public float CurrentRotation;
            public float[] TransformElements;
            public float ImageScaleX;
            public float ImageScaleY;
        }
    }
}
