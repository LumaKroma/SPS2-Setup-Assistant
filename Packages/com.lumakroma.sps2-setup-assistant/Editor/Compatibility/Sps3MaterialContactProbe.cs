using System;
using System.Reflection;
using System.Text.RegularExpressions;

namespace LumaKroma.Sps2SetupAssistant.Editor.Compatibility
{
    internal static class Sps3MaterialContactProbe
    {
        // Public metadata only. Does not read properties, initialize components or write materials.
        internal static bool HasRequiredPublicShape(string sdkVersion, Func<string, Type> resolve)
        {
            if (sdkVersion == null || !Regex.IsMatch(sdkVersion, @"\A(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\z") ||
                !Version.TryParse(sdkVersion, out var version) || version < new Version(3, 10, 6) || resolve == null) return false;
            try
            {
                var receiver = resolve("VRC.SDK3.Dynamics.Contact.Components.VRCContactReceiver");
                var renderer = resolve("UnityEngine.Renderer");
                var baseReceiver = resolve("VRC.Dynamics.ContactReceiver");
                return renderer != null && HasWritablePublicMember(receiver, "writeToMaterials", typeof(bool)) &&
                    HasWritablePublicMember(receiver, "writeToMaterialsContactTags", typeof(bool)) &&
                    HasWritablePublicMember(receiver, "separateShaderParameter", typeof(string)) &&
                    HasWritablePublicMember(receiver, "shaderTargetRenderers", renderer.MakeArrayType()) &&
                    HasPublicIntegerLimit(baseReceiver);
            }
            catch { return false; }
        }

        internal static bool HasWritablePublicMember(Type type, string name, Type expected)
        {
            if (type == null || expected == null) return false;
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
            var field = type.GetField(name, flags);
            if (field != null) return field.FieldType == expected && !field.IsInitOnly && !field.IsLiteral &&
                !field.IsDefined(typeof(ObsoleteAttribute), false);
            var property = type.GetProperty(name, flags);
            return property != null && property.PropertyType == expected && property.GetIndexParameters().Length == 0 &&
                property.GetGetMethod(false) != null && property.GetSetMethod(false) != null &&
                !property.IsDefined(typeof(ObsoleteAttribute), false);
        }

        private static bool HasPublicIntegerLimit(Type type)
        {
            if (type == null) return false;
            var field = type.GetField("SHADER_MAX_UPDATED_RENDERERS", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            // Non-constant fields/properties are deliberately not evaluated: they may run initialization.
            return field != null && field.FieldType == typeof(int) && field.IsLiteral &&
                !field.IsDefined(typeof(ObsoleteAttribute), false) &&
                (int)field.GetRawConstantValue() > 0;
        }
    }
}
