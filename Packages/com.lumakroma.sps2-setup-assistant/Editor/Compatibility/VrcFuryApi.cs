using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;

namespace LumaKroma.Sps2SetupAssistant.Editor.Compatibility
{
    // Late-bind public APIs so versions predating the API (or VRCFury itself) can display guidance.
    public static class VrcFuryApi
    {
        internal static Type FindType(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(name, false)).FirstOrDefault(t => t != null);

        internal static MethodInfo Method(Type type, string name, params Type[] arguments) =>
            type?.GetMethod(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static, null, arguments, null);

        internal static object Call(object target, string name, Type[] signature, params object[] arguments)
        {
            if (target == null) throw new InvalidOperationException("VRCFury API の生成結果がありません: " + name + "。" + VrcFuryCapabilities.UpdateGuide);
            var type = target as Type ?? target.GetType();
            var method = Method(type, name, signature);
            if (method == null) throw new InvalidOperationException("VRCFury API がありません: " + name + "。VCC で VRCFury を更新してください。");
            try { return method.Invoke(target is Type ? null : target, arguments); }
            catch (TargetInvocationException e) { throw new InvalidOperationException("VRCFury: " + e.InnerException?.Message, e.InnerException ?? e); }
        }

        internal static object Create(string name, GameObject obj)
        {
            var factory = FindType("com.vrcfury.api.FuryComponents");
            return Method(factory, name, typeof(GameObject)) == null ? null : Call(factory, name, new[] { typeof(GameObject) }, obj);
        }

        public static Socket CreateSocket(GameObject obj)
        {
            var api = Create("CreateSocket", obj);
            var native = VrcFuryCompatibility.FindSocket(obj);
            if (native == null)
            {
                var type = FindType(VrcFuryCompatibility.SocketType);
                if (type == null || !typeof(Component).IsAssignableFrom(type))
                    throw new InvalidOperationException("SPS Socket がありません。VCC で VRCFury を導入・更新してください。");
                native = obj.AddComponent(type);
            }
            return new Socket(api, native);
        }

        public sealed class Socket
        {
            private readonly object api;
            private readonly Component native;
            internal Socket(object api, Component native) { this.api = api; this.native = native; }
            public void SetName(string value)
            {
                var data = new SerializedObject(native);
                VrcFuryCompatibility.Require(data, "name", SerializedPropertyType.String).stringValue = value;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            public void SetMode(string value)
            {
                var data = new SerializedObject(native);
                var mode = VrcFuryCompatibility.Require(data, "addLight", SerializedPropertyType.Enum);
                int index = Array.IndexOf(mode.enumNames, value);
                if (index < 0) throw new InvalidOperationException("この版に Socket モードがありません: " + value);
                mode.enumValueIndex = index; data.ApplyModifiedPropertiesWithoutUndo();
            }
            public void UseRadiusOffset()
            {
                var data = new SerializedObject(native);
                var p = data.FindProperty("useRadiusOffset");
                if (p == null) return;
                VrcFuryCompatibility.Require(data, "useRadiusOffset", SerializedPropertyType.Boolean).boolValue = true;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            public Actions AddDepthActions(Vector2 range, float smoothing, bool self) => new Actions(Call(api,
                "AddDepthActions", new[] { typeof(Vector2), typeof(float), typeof(bool) }, range, smoothing, self));
        }

        public sealed class Actions
        {
            private readonly object api;
            internal Actions(object api) { this.api = api; }
            public void AddBlendshape(string shape, float weight, Renderer renderer) =>
                Call(api, "AddBlendshape", new[] { typeof(string), typeof(float), typeof(Renderer) }, shape, weight, renderer);
            public void AddAnimationClip(AnimationClip clip) => Call(api, "AddAnimationClip", new[] { typeof(AnimationClip) }, clip);
            public void AddTurnOn(GameObject obj) => Call(api, "AddTurnOn", new[] { typeof(GameObject) }, obj);
        }

        public static void Attach(GameObject anchor, HumanBodyBones bone)
        {
            var api = VrcFuryCapabilities.Current.PublicAttachment ? LumaKroma.Sps2SetupAssistant.Editor.Model.UndoComponentRegistration.Invoke(anchor,
                "Set Up SPS2 Sockets", () => Create("CreateArmatureLink", anchor)) : null;
            if (api != null)
            {
                Call(api, "LinkTo", new[] { typeof(HumanBodyBones), typeof(string) }, bone, "");
                Call(api, "SetAlign", new[] { typeof(bool) }, true);
                return;
            }
            var animator = anchor.GetComponentInParent<Animator>();
            var target = animator != null && animator.isHuman ? animator.GetBoneTransform(bone) : null;
            if (target == null) throw new InvalidOperationException("追従先の Humanoid ボーンがありません。");
            var constraint = Undo.AddComponent<ParentConstraint>(anchor);
            constraint.AddSource(new ConstraintSource { sourceTransform = target, weight = 1 });
            constraint.SetTranslationOffset(0, target.InverseTransformPoint(anchor.transform.position));
            constraint.SetRotationOffset(0, (Quaternion.Inverse(target.rotation) * anchor.transform.rotation).eulerAngles);
            constraint.weight = 1; constraint.locked = true; constraint.constraintActive = true;
        }
    }
}
