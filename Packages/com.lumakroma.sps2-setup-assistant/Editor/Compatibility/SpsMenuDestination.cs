using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace LumaKroma.Sps2SetupAssistant.Editor
{
    internal static class SpsMenuDestination
    {
        // Fury moves these root controls after creating the SPS menu. If the
        // destination descends from one of them, removing the source detaches
        // that tree before the destination is resolved again.
        internal static bool IsRelocatedDescendant(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            int separator = 0;
            while ((separator = path.IndexOf('/', separator)) >= 0)
            {
                if (separator == 0 || path[separator - 1] != '\\') break;
                separator++;
            }
            if (separator < 0) return false;
            var root = path.Substring(0, separator).Replace(@"\/", "/");
            bool Reserved(string text)
            {
                string name = text.Trim();
                return name.Equals("SPS", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("Holes", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("Sockets", StringComparison.OrdinalIgnoreCase);
            }
            // Native name matching also considers unformatted names and individual rich-text runs.
            root = root.Replace(@"\n", " ");
            return Reserved(Regex.Replace(root, "<.*?>", "")) || Regex.Split(root, "<.*?>").Any(Reserved);
        }

        private static string Append(string parent, string label) =>
            (parent.Length == 0 ? "" : parent + "/") + label.Replace("/", @"\/");

        internal static bool Exists(VRCExpressionsMenu root, string destination)
        {
            bool Visit(VRCExpressionsMenu node, string parent)
            {
                if (node == null || node.controls == null) return false;
                foreach (var control in node.controls)
                {
                    if (string.IsNullOrEmpty(control?.name)) continue;
                    var path = Append(parent, control.name);
                    if (path.Equals(destination, StringComparison.OrdinalIgnoreCase)) return true;
                    if (destination.StartsWith(path + "/", StringComparison.OrdinalIgnoreCase) &&
                        Visit(control.subMenu, path)) return true;
                }
                return false;
            }
            return Visit(root, "");
        }

        private const string MaEditor = ", nadena.dev.modular-avatar.core.editor";
        private const string MaRuntime = ", nadena.dev.modular-avatar.core";
        private static object Source(object key) => key is ValueTuple<object, object> pair ? pair.Item1 : key;

        internal static string ParentOf(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            for (int i = path.Length - 1; i >= 0; i--)
                if (path[i] == '/' && (i == 0 || path[i - 1] != '\\')) return path.Substring(0, i);
            return "";
        }

        internal static string AtParent(string parent, string previous)
        {
            // Changing location preserves the existing menu's name, including older custom paths.
            string name = string.IsNullOrWhiteSpace(previous) ? "SPS" : previous.Substring(
                ParentOf(previous).Length == 0 ? 0 : ParentOf(previous).Length + 1);
            if (string.IsNullOrWhiteSpace(name)) name = "SPS";
            return string.IsNullOrEmpty(parent) ? (name == "SPS" ? "" : name) : parent + "/" + name;
        }

        internal static bool TryOpenPicker(VRCAvatarDescriptor avatar, Rect anchor, Action<object> onSelect, string title)
        {
            var type = Type.GetType("nadena.dev.modular_avatar.core.editor.AvMenuTreeViewWindow" + MaEditor, false);
            var avatarProperty = type?.GetProperty("Avatar");
            var callback = type?.GetField("OnMenuSelected");
            if (type == null || !typeof(EditorWindow).IsAssignableFrom(type) ||
                avatarProperty?.PropertyType != typeof(VRCAvatarDescriptor) || !avatarProperty.CanWrite ||
                callback?.FieldType != typeof(Action<object>)) return false;
            EditorWindow window = null;
            try
            {
                window = (EditorWindow)ScriptableObject.CreateInstance(type);
                avatarProperty.SetValue(window, avatar);
                callback.SetValue(window, onSelect);
                window.titleContent = new GUIContent(title);
                window.minSize = new Vector2(280, 220);
                window.Show();
                window.position = new Rect(anchor.xMax - 360, anchor.yMax + 4, 360, 360);
                return true;
            }
            catch (Exception e) when (e is TargetInvocationException || e is MemberAccessException ||
                e is ArgumentException || e is TargetException || e is InvalidOperationException)
            {
                if (window != null) window.Close();
                return false;
            }
        }

        internal static bool TryResolveParent(VRCAvatarDescriptor avatar, object selection, out string parent)
        {
            parent = "";
            if (avatar == null) return false;
            var source = Source(selection);
            if (source == null || Equals(source, avatar.expressionsMenu) || source.GetType().Name == "RootMenu") return true;
            var root = selection is Transform transform ? transform.gameObject : selection as GameObject;
            if (root == null && selection is Component component) root = component.gameObject;
            var matches = new List<(string path, object key)>();
            var rows = Collect(avatar, out var rootKey);
            matches.AddRange(rows.Where(row => Equals(row.key, selection)));
            if (matches.Count == 0)
            {
                object children = null;
                if (root != null)
                {
                    if (root.GetComponentInParent<VRCAvatarDescriptor>() != avatar) return false;
                    var node = Type.GetType("nadena.dev.modular_avatar.core.menu.MenuNodesUnder" + MaRuntime, false);
                    if (node != null && node.GetConstructor(new[] { typeof(GameObject) }) != null)
                        children = Activator.CreateInstance(node, root);
                }
                matches.AddRange(rows.Where(row => {
                    var candidate = Source(row.key);
                    return Equals(candidate, source) || (children != null && Equals(candidate, children)) ||
                        (root != null && candidate is Component c && c.gameObject == root);
                }));
            }
            var paths = matches.Select(row => row.path).Distinct(StringComparer.Ordinal).ToArray();
            if (paths.Length == 0 && Equals(source, rootKey)) return true;
            if (paths.Length != 1) return false;
            parent = paths[0];
            return true;
        }

        internal static string[] Parents(VRCAvatarDescriptor avatar) =>
            Collect(avatar, out _).Select(x => x.path).Distinct(StringComparer.Ordinal).ToArray();

        // Only read the graph on selection. Never rebuild it in Layout or Repaint.
        private static List<(string path, object key)> Collect(VRCAvatarDescriptor avatar, out object preferredRoot)
        {
            preferredRoot = avatar.expressionsMenu;
            var rows = new List<(string, object)>();
            var branch = new HashSet<object>();
            void Visit(object node, string parent, Func<object,IEnumerable> controls, Func<object,object> submenu,
                Func<object,object> key)
            {
                if (node == null || !branch.Add(node)) return;
                foreach (var item in controls(node))
                {
                    var control = item as VRCExpressionsMenu.Control;
                    if (string.IsNullOrEmpty(control?.name) || control.type != VRCExpressionsMenu.Control.ControlType.SubMenu) continue;
                    var child = submenu(item);
                    if (child == null || branch.Contains(child)) continue;
                    var path = Append(parent, control.name);
                    rows.Add((path, key(child)));
                    Visit(child, path, controls, submenu, key);
                }
                branch.Remove(node);
            }
            try
            {
                var type = Type.GetType("nadena.dev.modular_avatar.core.editor.menu.VirtualMenu" + MaEditor, false);
                var factory = type?.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                    .FirstOrDefault(m => m.Name == "ForAvatar" && m.GetParameters().Length == 2 &&
                        m.GetParameters()[0].ParameterType == typeof(VRCAvatarDescriptor) && m.GetParameters()[1].IsOptional);
                var rootProperty = type?.GetProperty("RootMenuNode");
                var controls = rootProperty?.PropertyType.GetField("Controls");
                var key = rootProperty?.PropertyType.GetField("NodeKey");
                var element = controls?.FieldType.IsGenericType == true ? controls.FieldType.GetGenericArguments()[0] : null;
                var sub = element?.GetField("SubmenuNode");
                if (factory != null && sub != null && key != null)
                {
                    var graph = factory.Invoke(null, new object[] { avatar, null });
                    var root = graph == null ? null : rootProperty.GetValue(graph);
                    if (root != null)
                    {
                        Visit(root, "", n => (IEnumerable)controls.GetValue(n), n => sub.GetValue(n), n => key.GetValue(n));
                        preferredRoot = type.GetProperty("RootMenuKey")?.GetValue(graph) ?? avatar.expressionsMenu;
                        var installerType = Type.GetType("nadena.dev.modular_avatar.core.ModularAvatarMenuInstaller" + MaRuntime, false);
                        var groupType = Type.GetType("nadena.dev.modular_avatar.core.ModularAvatarMenuGroup" + MaRuntime, false);
                        var targetMenu = installerType?.GetField("installTargetMenu");
                        if (installerType != null && groupType != null && targetMenu != null)
                            foreach (var installer in avatar.GetComponentsInChildren(installerType, true))
                            {
                                var target = targetMenu.GetValue(installer) as VRCExpressionsMenu;
                                if (target != null && !ReferenceEquals(target, preferredRoot)) continue;
                                var group = installer.GetComponent(groupType);
                                if (group == null) continue;
                                preferredRoot = group;
                                break;
                            }
                        return rows;
                    }
                }
            }
            catch (Exception e) when (e is TargetInvocationException || e is MemberAccessException || e is ArgumentException || e is TargetException)
            {
                rows.Clear();
                branch.Clear();
            }
            Visit(avatar.expressionsMenu, "", n => ((VRCExpressionsMenu)n).controls, n => ((VRCExpressionsMenu.Control)n).subMenu, n => n);
            return rows;
        }
    }
}