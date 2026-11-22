using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LumaKroma.Sps2SetupAssistant.Editor.Compatibility
{
    // Structural capability only; actual save/reload and execution require Unity tests.
    internal static class FuryBasicDepthProbe
    {
        internal static bool Available() => Inspect(
            VrcFuryApi.FindType("VF.Component.VRCFuryHapticSocket"),
            VrcFuryApi.FindType("com.vrcfury.api.FuryComponents"),
            VrcFuryApi.FindType("com.vrcfury.api.Components.FurySocket"),
            VrcFuryApi.FindType("com.vrcfury.api.Actions.FuryActionSet"));

        internal static bool Inspect(Type socket, Type factory, Type api, Type actions) =>
            Inspect(socket, factory, api, actions, socket?.Assembly.GetType("VF.Model.StateAction.ObjectToggleAction", false));

        internal static bool Inspect(Type socket, Type factory, Type api, Type actions, Type toggle)
        {
            if (socket == null || factory == null || api == null || actions == null) return false;
            var field = socket.GetField("depthActions2");
            var list = field?.FieldType;
            if (list == null || !list.IsGenericType || list.GetGenericTypeDefinition() != typeof(List<>)) return false;
            var element = list.GetGenericArguments()[0];
            var units = element.GetField("units")?.FieldType;
            var state = element.GetField("actionSet")?.FieldType;
            var stateActions = state?.GetField("actions")?.FieldType;
            var toggleMode = toggle?.GetField("mode")?.FieldType;
            return element.GetField("range")?.FieldType == typeof(Vector2) &&
                element.GetField("enableSelf")?.FieldType == typeof(bool) &&
                element.GetField("enableOthers")?.FieldType == typeof(bool) &&
                element.GetField("smoothingSeconds")?.FieldType == typeof(float) &&
                element.GetField("reverseClip")?.FieldType == typeof(bool) &&
                stateActions != null && stateActions.IsGenericType && stateActions.GetGenericTypeDefinition() == typeof(List<>) &&
                toggle != null && stateActions.GetGenericArguments()[0].IsAssignableFrom(toggle) &&
                toggleMode != null && toggleMode.IsEnum && Enum.GetNames(toggleMode).Contains("TurnOn") &&
                units != null && units.IsEnum &&
                Enum.GetNames(units).Contains("Local") &&
                VrcFuryApi.Method(factory, "CreateSocket", typeof(GameObject))?.ReturnType == api &&
                VrcFuryApi.Method(api, "AddDepthActions", typeof(Vector2), typeof(float), typeof(bool))?.ReturnType == actions &&
                VrcFuryApi.Method(actions, "AddTurnOn", typeof(GameObject))?.ReturnType == typeof(void);
        }
    }
}
