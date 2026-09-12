using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using LumaKroma.Sps2SetupAssistant.Editor.Compatibility;

namespace LumaKroma.Sps2SetupAssistant.Editor.Generation
{
    public sealed class Sps2SetupAsset : ScriptableObject
    {
        public int schema = 1;
        [HideInInspector] public string stateJson;
    }

    public sealed class Sps2SetupContext
    {
        public GameObject gameObject;
        public Transform transform => gameObject.transform;
        public VRCAvatarDescriptor avatar;
        public Sps2SetupAsset asset;
        public Sps2SetupRoot legacy;
        public string identity;
        public int schema => legacy != null ? legacy.schema : asset.schema;
        public SetupSettings settings;
        public List<GeneratedSocket> sockets = new List<GeneratedSocket>();
        public GameObject testPlug;
        public GameObject longTestPlug;
    }

    internal static class Sps2SetupStorage
    {
        internal const string Prefix = "__SPS2_";
        [Serializable] private sealed class Reference
        {
            public string key, path, guid;
            public long localId;
            public int componentIndex;
        }
        [Serializable] private sealed class SocketRecord
        {
            public string id, anchor, pose;
            public string[] stops;
        }
        [Serializable] private sealed class State
        {
            public SetupSettings settings;
            public List<Reference> references = new List<Reference>();
            public List<SocketRecord> sockets = new List<SocketRecord>();
            public string plug, longPlug;
        }
        internal static bool TryToken(string token, out string identity, out string part)
        {
            identity = part = null;
            if (string.IsNullOrEmpty(token) || !token.StartsWith(Prefix, StringComparison.Ordinal) || token.Length <= Prefix.Length + 33) return false;
            identity = token.Substring(Prefix.Length, 32);
            if (!Guid.TryParseExact(identity, "N", out _) || token[Prefix.Length + 32] != '_') return false;
            part = token.Substring(Prefix.Length + 33); return part.Length != 0;
        }
        internal static string Token(Sps2SetupContext root, string id) => Prefix + root.identity + "_" + id;
        internal static Sps2SetupAsset Create(string avatarName)
        {
            const string folder = "Assets/SPS2Settings";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets", "SPS2Settings");
            string safe = new string(avatarName.Select(c => System.IO.Path.GetInvalidFileNameChars().Contains(c) || c == '/' ? '_' : c).ToArray());
            var asset = ScriptableObject.CreateInstance<Sps2SetupAsset>();
            AssetDatabase.CreateAsset(asset, AssetDatabase.GenerateUniqueAssetPath(folder + "/" + safe + " SPS2.asset"));
            return asset;
        }
        internal static string Identity(Sps2SetupAsset asset) => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
        private static Sps2SetupAsset LoadAsset(string identity)
        {
            var asset = AssetDatabase.LoadAssetAtPath<Sps2SetupAsset>(AssetDatabase.GUIDToAssetPath(identity));
            if (asset == null || asset.schema != 1 || string.IsNullOrEmpty(asset.stateJson))
                throw new InvalidOperationException("SPS2 の設定アセットが見つからないか形式が一致しません。Prefab と対応する設定 .asset を一緒に配置してください。");
            return asset;
        }
        private static string Path(Transform value, Transform root)
        {
            if (value == null) return null;
            if (value != root && !value.IsChildOf(root)) throw new InvalidOperationException("設定の参照が対象アバターの外にあります。");
            var names = new List<string>();
            for (var t = value; t != root; t = t.parent)
            {
                if (t.name.Contains("/") || t.parent.Cast<Transform>().Count(c => c.name == t.name) != 1)
                    throw new InvalidOperationException("設定参照の階層名が重複しているか、/ を含んでいます: " + t.name);
                names.Add(t.name);
            }
            names.Reverse(); return string.Join("/", names);
        }
        private static Transform Resolve(Transform root, string path) => path == null ? null : path.Length == 0 ? root : root.Find(path);
        internal static string Serialize(Sps2SetupContext root)
        {
            var state = new State { settings = root.settings.Copy(), plug = Path(root.testPlug != null ? root.testPlug.transform : null, root.transform), longPlug = Path(root.longTestPlug != null ? root.longTestPlug.transform : null, root.transform) };
            void SaveReference(string key, UnityEngine.Object value)
            {
                if (value == null) return;
                var reference = new Reference { key = key };
                if (value is AnimationClip)
                {
                    if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out reference.guid, out reference.localId))
                        throw new InvalidOperationException("Animation Clip は保存済みアセットを指定してください。");
                }
                else
                {
                    var transform = value is GameObject obj ? obj.transform : ((Component)value).transform;
                    reference.path = Path(transform, root.avatar.transform);
                    if (value is SkinnedMeshRenderer renderer) reference.componentIndex = Array.IndexOf(transform.GetComponents<SkinnedMeshRenderer>(), renderer);
                }
                state.references.Add(reference);
            }
            foreach (var part in state.settings.parts)
            {
                SaveReference(part.id + ":target", part.target); part.target = null;
                for (int i = 0; i < part.actions.Count; i++)
                {
                    var a = part.actions[i]; string key = part.id + ":" + i;
                    SaveReference(key + ":renderer", a.renderer); SaveReference(key + ":clip", a.clip); SaveReference(key + ":object", a.target);
                    a.renderer = null; a.clip = null; a.target = null;
                }
            }
            foreach (var socket in root.sockets)
                state.sockets.Add(new SocketRecord { id = socket.id, anchor = Path(socket.anchor, root.transform), pose = Path(socket.pose, root.transform),
                    stops = socket.pathStops.Select(t => Path(t, root.transform)).ToArray() });
            return JsonUtility.ToJson(state);
        }
        private static SetupSettings BindSettings(State state, VRCAvatarDescriptor avatar)
        {
            var settings = state.settings.Copy();
            Reference Reference(string key) => state.references.SingleOrDefault(r => r.key == key);
            Transform Transform(string key) { var r = Reference(key); return r == null ? null : Resolve(avatar.transform, r.path); }
            foreach (var part in settings.parts)
            {
                part.target = Transform(part.id + ":target");
                for (int i = 0; i < part.actions.Count; i++)
                {
                    var a = part.actions[i]; string key = part.id + ":" + i;
                    var r = Reference(key + ":renderer"); var renderer = Transform(key + ":renderer");
                    a.renderer = renderer == null ? null : renderer.GetComponents<SkinnedMeshRenderer>().ElementAtOrDefault(r.componentIndex);
                    var target = Transform(key + ":object"); a.target = target != null ? target.gameObject : null;
                    var clip = Reference(key + ":clip");
                    if (clip != null) a.clip = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(clip.guid)).OfType<AnimationClip>()
                        .SingleOrDefault(c => AssetDatabase.TryGetGUIDAndLocalFileIdentifier(c, out string guid, out long id) && id == clip.localId);
                }
            }
            return settings;
        }
        internal static Sps2SetupContext Find(VRCAvatarDescriptor avatar, bool build = false)
        {
            if (avatar == null) return null;
            var legacyRoots = avatar.GetComponentsInChildren<Sps2SetupRoot>(true);
            if (legacyRoots.Length > 1) throw new InvalidOperationException("SPS2 生成ルートが重複しています。");
            if (legacyRoots.Length == 1)
            {
                var old = legacyRoots[0];
                if (!build && old.transform.parent != avatar.transform) throw new InvalidOperationException("旧 SPS2 ルートの所有位置が変わっています。");
                return new Sps2SetupContext { gameObject = old.gameObject, avatar = avatar, legacy = old, identity = old.identity,
                    settings = old.settings.Copy(), sockets = old.sockets.Select(s => new GeneratedSocket { id=s.id, anchor=s.anchor, pose=s.pose, socket=s.socket, pathStops=s.pathStops.ToArray(), buildToken=s.buildToken }).ToList(), testPlug = old.testPlug };
            }
            var owned = VrcFuryCompatibility.AllSockets(avatar.gameObject)
                .Select(s => new { socket = s, token = VrcFuryCompatibility.ReadIdentity(s) })
                .Where(s => TryToken(s.token, out _, out _)).ToArray();
            if (owned.Length == 0)
            {
                if (!build && avatar.transform.Find(FullSetupGenerator.RootName) != null)
                    throw new InvalidOperationException("SPS2 という名前の生成物がありますが、所有情報を確認できません。");
                return null;
            }
            var identities = owned.Select(s => { TryToken(s.token, out var id, out _); return id; }).Distinct().ToArray();
            if (identities.Length != 1) throw new InvalidOperationException("対象アバターに複数の SPS2 設定があります。");
            var asset = LoadAsset(identities[0]); var state = JsonUtility.FromJson<State>(asset.stateJson);
            var transform = avatar.transform.Find(FullSetupGenerator.RootName);
            if (!build && transform == null) throw new InvalidOperationException("SPS2 生成ルートが見つかりません。");
            var result = new Sps2SetupContext { avatar = avatar, asset = asset, identity = identities[0], gameObject = transform != null ? transform.gameObject : avatar.gameObject,
                settings = build ? state.settings.Copy() : BindSettings(state, avatar) };
            if (!build && !string.IsNullOrEmpty(state.plug)) result.testPlug = Resolve(transform, state.plug)?.gameObject;
            if (!build && !string.IsNullOrEmpty(state.longPlug)) result.longTestPlug = Resolve(transform, state.longPlug)?.gameObject;
            foreach (var record in state.sockets)
            {
                var sockets = owned.Where(s => s.token == Token(result, record.id)).ToArray();
                if (sockets.Length > 1) throw new InvalidOperationException("SPS2 Socket の識別子が重複しています。");
                var socket = sockets.SingleOrDefault()?.socket;
                var pose = build ? socket?.transform : Resolve(transform, record.pose);
                result.sockets.Add(new GeneratedSocket { id=record.id, socket=socket, pose=pose,
                    anchor=build ? pose?.parent : Resolve(transform, record.anchor),
                    pathStops=build ? Array.Empty<Transform>() : record.stops.Select(p => Resolve(transform, p)).ToArray() });
            }
            if (owned.Length != result.sockets.Count(s => s.socket != null)) throw new InvalidOperationException("設定アセットに記録されていない SPS2 Socket があります。");
            return result;
        }
    }
}
