using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace LumaKroma.Sps2SetupAssistant.Editor.Compatibility
{
    // Shape checks do not certify semantics or authorise removing source-contract guards.
    internal static class FuryActiveShapeProbe
    {

        internal static bool PreserveExisting(bool state, bool hasUnsupportedAction, int existingCount) =>
            !state || hasUnsupportedAction && existingCount > 0;
        internal struct Capabilities
        {
            internal bool State, ObjectOn, ObjectOff, BlendShape, Clip;
            internal bool Supports(DepthActionSettings item) => item != null && State &&
                (item.kind == DepthActionKind.Object ? (item.objectOn ? ObjectOn : ObjectOff) :
                 item.kind == DepthActionKind.BlendShape ? BlendShape :
                 item.kind == DepthActionKind.AnimationClip && Clip);
        }
        internal static Capabilities Inspect()
        {

            try { return Inspect(
                VrcFuryApi.FindType(SpsBackend.SocketType(SpsFamily.VrcFury)),
                VrcFuryApi.FindType("VF.Model.State"), VrcFuryApi.FindType("VF.Model.StateAction.Action"),
                VrcFuryApi.FindType("com.vrcfury.api.Components.FurySocket"),
                VrcFuryApi.FindType("VF.Model.StateAction.ObjectToggleAction")); }
            catch { return default; }
        }
        internal static Capabilities Inspect(Type socket, Type state, Type action, Type api, Type toggle)
        {
            var result = new Capabilities();
            var method = api?.GetMethod("GetActiveActions", Type.EmptyTypes);
            if (socket == null || state == null || action == null || method == null ||
                socket.GetField("activeActions")?.FieldType != state ||
                state.GetField("actions")?.FieldType != typeof(List<>).MakeGenericType(action)) return result;
            result.State = true;
            var output = method.ReturnType;
            bool Has(string name, params Type[] signature) => output.GetMethod(name, signature)?.ReturnType == typeof(void);
            result.ObjectOn = Has("AddTurnOn", typeof(GameObject));
            var mode = toggle?.GetField("mode")?.FieldType;
            result.ObjectOff = result.ObjectOn && mode != null && mode.IsEnum && Enum.GetNames(mode).Contains("TurnOff");
            result.BlendShape = Has("AddBlendshape", typeof(string), typeof(float), typeof(Renderer));
            result.Clip = Has("AddAnimationClip", typeof(AnimationClip));
            return result;
        }
        internal static void Require(IEnumerable<DepthActionSettings> actions)
        {
            var caps = Inspect();
            if (!caps.State) throw new InvalidOperationException("Active animation state schema is unavailable.");
            foreach (var item in (actions ?? Enumerable.Empty<DepthActionSettings>()).Where(x => x != null))
                if (!caps.Supports(item)) throw new InvalidOperationException("Active operation unavailable: " + item.kind);
        }
    }
}
