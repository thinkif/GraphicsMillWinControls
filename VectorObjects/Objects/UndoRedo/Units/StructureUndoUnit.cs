// Copyright (c) 2018 Aurigma Inc. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
//
namespace Aurigma.GraphicsMill.WinControls
{
    internal sealed class StructureUndoUnit : IUndoUnit
    {
        private readonly System.Action _applyAction;
        private readonly System.Action _unapplyAction;

        public StructureUndoUnit(System.Action applyAction, System.Action unapplyAction)
        {
            if (applyAction == null)
                throw new System.ArgumentNullException("applyAction");
            if (unapplyAction == null)
                throw new System.ArgumentNullException("unapplyAction");

            _applyAction = applyAction;
            _unapplyAction = unapplyAction;
        }

        public void Apply()
        {
            _applyAction();
        }

        public void Unapply()
        {
            _unapplyAction();
        }
    }
}
