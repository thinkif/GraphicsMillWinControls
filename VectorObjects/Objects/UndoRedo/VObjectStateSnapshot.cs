// Copyright (c) 2018 Aurigma Inc. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
//
namespace Aurigma.GraphicsMill.WinControls
{
    internal sealed class VObjectStateSnapshot
    {
        public IVObject VObject;
        public Layer Layer;
        public int ObjectIndex;
        public byte[] SerializedState;

        public bool EqualsState(VObjectStateSnapshot other)
        {
            if (other == null)
                return false;

            if (VObject != other.VObject || Layer != other.Layer || ObjectIndex != other.ObjectIndex)
                return false;

            return ByteArrayEquals(SerializedState, other.SerializedState);
        }

        private static bool ByteArrayEquals(byte[] left, byte[] right)
        {
            if (left == right)
                return true;
            if (left == null || right == null || left.Length != right.Length)
                return false;

            for (int i = 0; i < left.Length; i++)
                if (left[i] != right[i])
                    return false;

            return true;
        }
    }
}
