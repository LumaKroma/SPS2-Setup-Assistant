using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LumaKroma.Sps2SetupAssistant.Editor.Model
{
    internal static class UndoComponentRegistration
    {
        internal static T Invoke<T>(GameObject target, string undoName, Func<T> create)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (create == null)
            {
                throw new ArgumentNullException(nameof(create));
            }

            var existingComponents = new HashSet<Component>(target.GetComponents<Component>());
            try
            {
                return create();
            }
            finally
            {
                foreach (var component in target.GetComponents<Component>())
                {
                    if (component != null && !existingComponents.Contains(component))
                    {
                        Undo.RegisterCreatedObjectUndo(component, undoName);
                    }
                }
            }
        }
    }
}
