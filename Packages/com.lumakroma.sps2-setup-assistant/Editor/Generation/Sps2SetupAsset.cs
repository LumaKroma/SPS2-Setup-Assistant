using LumaKroma.Sps2SetupAssistant.Editor.Model;
using static LumaKroma.Sps2SetupAssistant.Editor.Localization.Sps2Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using LumaKroma.Sps2SetupAssistant.Editor.Compatibility;

namespace LumaKroma.Sps2SetupAssistant.Editor.Generation
{
    public enum SpsAuthoringContract { LegacySps2 = 0, Sps3Beta14 = 1 }

    public sealed class Sps2SetupAsset : ScriptableObject
    {
        public int schema = 1;
        // Global SPS Options are intentionally left to the avatar's native state.
        [HideInInspector] public bool globalOptionsUnmanaged;
        // Missing field in older assets remains legacy; installed package never upgrades provenance.
        public SpsAuthoringContract authoringContract;
        [HideInInspector] public string stateJson;
        [HideInInspector] public bool limitedDraft;
        // Editor-side ownership evidence. Older assets leave this at zero and retain
        // their existing component-based ownership path.
        [HideInInspector] public int optionsOwnershipSchema;
        [HideInInspector] public int optionsPolicyVersion, optionsPolicyBaseline;
        [HideInInspector] public int optionsMenuLayoutVersion;
        [HideInInspector] public string optionsMenuSettingsLabel;
        // Evidence only: scene objects are never stored as asset references.
        [HideInInspector] public string manualMenuOwnerId, manualMenuPlan;
        [HideInInspector] public string[] manualMenuModelTypes, manualMenuModelJson;
        // Evidence for a Setup-created Options host, separate from menu-rule evidence.
        [HideInInspector] public string managedOptionsHostId, managedOptionsModelJson;

        // Empty on all legacy assets. New VRC-backed outputs must record the
        // complete owned-attachment evidence before any later regeneration.
        [HideInInspector] public string vrcConstraintEvidence;
    }

    public sealed class Sps2SetupContext
    {
        public GameObject gameObject;
        public Transform transform => gameObject.transform;
        public VRCAvatarDescriptor avatar;
        public Sps2SetupAsset asset;
        public string identity;
        public int schema => asset.schema;
        public SpsAuthoringContract AuthoringContract => asset.authoringContract;
        public SetupSettings settings;
        internal string[] missingLegacyReferences;
        public bool oralBoundaryPath;
        public string nativePathProof;
        public List<GeneratedSocket> sockets = new List<GeneratedSocket>();
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
            public bool oralBoundaryPath;
        public string nativePathProof;
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
        internal static Sps2SetupAsset Create(string avatarName, SpsAuthoringContract contract = SpsAuthoringContract.LegacySps2)
        {
            SpsAuthoringCompatibility.ValidateContract(contract);
            const string folder = "Assets/SPS2Settings";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets", "SPS2Settings");
            string safe = new string(avatarName.Select(c => System.IO.Path.GetInvalidFileNameChars().Contains(c) || c == '/' ? '_' : c).ToArray());
            var asset = ScriptableObject.CreateInstance<Sps2SetupAsset>();
            asset.authoringContract = contract;
            string path=AssetDatabase.GenerateUniqueAssetPath(folder + "/" + safe + " SPS2.asset");
            if (System.IO.File.Exists(path) || System.IO.File.Exists(path+".meta") || AssetDatabase.LoadMainAssetAtPath(path) != null)
                throw new InvalidOperationException("New settings asset path is already occupied: " + path);
            AssetDatabase.CreateAsset(asset, path);
            FullSetupGenerator.RecordFreshAsset(path);
            return asset;
        }
        internal static string Identity(Sps2SetupAsset asset) => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
        internal static Sps2SetupAsset CreateLimitedDraft(VRCAvatarDescriptor avatar, SetupSettings draft)
        {
            var context = new Sps2SetupContext { avatar = avatar, gameObject = avatar.gameObject, settings = draft };
            // Serialize before creating an asset, so invalid scene references cannot leave partial output.
            var json = Serialize(context);
            var asset = Create(avatar.name + " LimitedDraft");
            try {
                asset.limitedDraft = true; asset.stateJson = json;
                EditorUtility.SetDirty(asset); AssetDatabase.SaveAssetIfDirty(asset);
                return asset;
            } catch {
                var ownPath = AssetDatabase.GetAssetPath(asset);
                if (!string.IsNullOrEmpty(ownPath)) AssetDatabase.DeleteAsset(ownPath);
                throw;
            }
        }
        internal static SetupSettings LoadLimitedDraft(string identity, VRCAvatarDescriptor avatar)
        {
            var asset = LoadAsset(identity);
            if (!asset.limitedDraft) throw new InvalidOperationException("The selected settings asset is not a limited-output draft.");
            var state = ReadState(asset.stateJson);
            foreach (var reference in state.references) {
                if (!string.IsNullOrEmpty(reference.guid)) {
                    var path = AssetDatabase.GUIDToAssetPath(reference.guid);
                    if (string.IsNullOrEmpty(path) || !AssetDatabase.LoadAllAssetsAtPath(path).Any(value =>
                        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out string guid, out long id) && guid == reference.guid && id == reference.localId))
                        throw new InvalidOperationException("A saved animation asset reference is unavailable; the saved draft remains unchanged: " + reference.key);
                } else {
                    var target = Resolve(avatar.transform, reference.path);
                    if (target == null || reference.key.EndsWith(":renderer", StringComparison.Ordinal) &&
                        (reference.componentIndex < 0 || reference.componentIndex >= target.GetComponents<SkinnedMeshRenderer>().Length))
                        throw new InvalidOperationException("A saved avatar reference is unavailable; the saved draft remains unchanged: " + reference.key);
                }
            }
            return BindSettings(state, avatar);
        }
        // Editor draft storage: validate every reference before rebinding, never overwrite the stored draft on failure.
        [Serializable] internal struct LegacyDraftOrigin {
            public string identity;
            public bool autoFromScene;

            internal SetupSettings ForRegeneration(string currentIdentity, SetupSettings input, bool savedAuto,
                string savedJson, ref string[] unresolved)
            {
                if (string.IsNullOrEmpty(identity) || identity != currentIdentity) return null;
                var draft = input.Copy();
                // Apply can display the observed native value. Only a Setup edit is authoritative on regeneration.
                if (autoFromScene) draft.autoMode = savedAuto;
                var missing = unresolved?.ToList();
                InitializeUpgradeDraft(draft, savedJson, missing);
                unresolved = missing?.ToArray();
                return draft;
            }
        }
        [Serializable] private sealed class WindowDraftEnvelope { public LegacyDraftOrigin legacyOrigin; public int version=2; public string data; public string[] keys, objectIds, rendererMeshIds; }
        private static IEnumerable<KeyValuePair<string,UnityEngine.Object>> WindowDraftReferences(SetupSettings settings)
        {
            foreach(var part in settings.parts) {
                if(part.target!=null) yield return new KeyValuePair<string,UnityEngine.Object>(part.id+":target",part.target);
                foreach(var row in StoredActions(part)) {
                    var a=row.Value; string key=row.Key;
                    if(a.renderer!=null) yield return new KeyValuePair<string,UnityEngine.Object>(key+":renderer",a.renderer);
                    if(a.target!=null) yield return new KeyValuePair<string,UnityEngine.Object>(key+":object",a.target);
                    if(a.clip!=null) yield return new KeyValuePair<string,UnityEngine.Object>(key+":clip",a.clip);
                    if(a.generatedBlendShapeClip!=null) yield return new KeyValuePair<string,UnityEngine.Object>(key+":customBlendShapeClip",a.generatedBlendShapeClip);
                }
            }
        }
        internal static string SerializeWindowDraft(VRCAvatarDescriptor avatar, SetupSettings settings)
            => SerializeWindowDraftWithOrigin(avatar, settings, default);
        internal static string SerializeWindowDraftWithOrigin(VRCAvatarDescriptor avatar, SetupSettings settings, LegacyDraftOrigin legacyOrigin)
        {
            var refs=WindowDraftReferences(settings).ToArray();
            if(refs.GroupBy(r=>r.Key).Any(g=>g.Count()!=1)) throw new InvalidOperationException("Duplicate draft reference keys.");
            var ids=refs.Select(r=> {
                var id=GlobalObjectId.GetGlobalObjectIdSlow(r.Value);
                if(id.targetObjectId==0 || GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id)!=r.Value)
                    throw new InvalidOperationException("Draft reference has no stable saved identity: "+r.Key);
                return id.ToString();
            }).ToArray();
            var meshIds=refs.Select(r=> {
                if (!(r.Value is SkinnedMeshRenderer renderer)) return "";
                if (renderer.sharedMesh == null) return "";
                var meshId=GlobalObjectId.GetGlobalObjectIdSlow(renderer.sharedMesh);
                if(meshId.targetObjectId==0 || GlobalObjectId.GlobalObjectIdentifierToObjectSlow(meshId)!=renderer.sharedMesh)
                    throw new InvalidOperationException("Draft renderer mesh has no stable identity: "+r.Key);
                return meshId.ToString();
            }).ToArray();
            return JsonUtility.ToJson(new WindowDraftEnvelope {legacyOrigin=legacyOrigin,data=Serialize(new Sps2SetupContext { avatar=avatar, gameObject=avatar.gameObject, settings=settings }),keys=refs.Select(r=>r.Key).ToArray(),objectIds=ids,rendererMeshIds=meshIds});
        }
        private static Transform ResolveUniqueDraftPath(Transform root, string path)
        {
            if(root==null || path==null) return null;
            var current=root;
            foreach(var part in path.Length==0 ? Array.Empty<string>() : path.Split('/')) {
                var matches=current.Cast<Transform>().Where(t=>t.name==part).ToArray();
                if(matches.Length!=1) throw new InvalidOperationException("Saved draft scene path is missing or ambiguous: "+path);
                current=matches[0];
            }
            return current;
        }
        private static void ClearDraftReference(SetupSettings settings,string key)
        {
            foreach(var part in settings.parts) {
                if(key==part.id+":target") part.target=null;
                foreach(var row in StoredActions(part)) {
                    var action=row.Value; string prefix=row.Key;
                    if(key==prefix+":renderer") action.renderer=null;
                    else if(key==prefix+":object") action.target=null;
                    else if(key==prefix+":clip") action.clip=null;
                    else if(key==prefix+":customBlendShapeClip") action.generatedBlendShapeClip=null;
                }
            }
        }
        internal static string[] StillMissingDraftReferences(SetupSettings settings, IEnumerable<string> keys)
        {
            var present=new HashSet<string>(WindowDraftReferences(settings).Select(r=>r.Key),StringComparer.Ordinal);
            return keys.Where(k=>!present.Contains(k)).Distinct(StringComparer.Ordinal).ToArray();
        }
        // Project only unavailable optional actions out of an output copy; never alter the saved draft.
        internal static SetupSettings DraftOutputForUnresolved(SetupSettings draft,IEnumerable<string> unresolved)
        {
            if(draft==null) return null;
            var missing=new HashSet<string>(unresolved ?? Array.Empty<string>(),StringComparer.Ordinal);
            if(missing.Count==0) return draft;
            var output=draft.Copy();
            bool MissingAction(string prefix) => new[]{":renderer",":object",":clip",":customBlendShapeClip"}.Any(s=>missing.Contains(prefix+s));
            foreach(var part in output.parts) {
                if(part.custom && missing.Contains(part.id+":target")) part.included=false;
                if(part.actions!=null) for(int i=part.actions.Count-1;i>=0;i--) if(MissingAction(part.id+":"+i)) part.actions.RemoveAt(i);
                if(part.animationGroups!=null) foreach(var group in part.animationGroups.Where(g=>g!=null && g.actions!=null))
                    for(int i=group.actions.Count-1;i>=0;i--) if(MissingAction(group.ReferenceKey(part.id,i))) group.actions.RemoveAt(i);
            }
            return output;
        }
        internal static string[] AssignedDraftReferenceKeys(SetupSettings settings) =>
            WindowDraftReferences(settings).Select(r => r.Key).ToArray();
        internal static string[] ClearDestroyedDraftReferences(SetupSettings settings, IEnumerable<string> assigned = null)
        {
            var tracked = assigned == null ? null : new HashSet<string>(assigned, StringComparer.Ordinal);
            var missing=new List<string>();
            bool Destroyed(UnityEngine.Object value) => !ReferenceEquals(value,null) && value==null;
            foreach(var part in settings.parts) {
                if(Destroyed(part.target)) missing.Add(part.id+":target");
                foreach(var row in StoredActions(part)) {
                    var action=row.Value; var key=row.Key;
                    if(Destroyed(action.renderer)) missing.Add(key+":renderer");
                    if(Destroyed(action.target)) missing.Add(key+":object");
                    if(Destroyed(action.clip)) missing.Add(key+":clip");
                    if(Destroyed(action.generatedBlendShapeClip)) missing.Add(key+":customBlendShapeClip");
                }
            }
            if (tracked != null) missing.RemoveAll(key => !tracked.Contains(key));
            foreach(var key in missing) ClearDraftReference(settings,key);
            return missing.ToArray();
        }
        internal static SetupSettings DeserializeWindowDraftPartial(string json, VRCAvatarDescriptor avatar,out string[] unresolved)
            => DeserializeWindowDraftWithOrigin(json, avatar, out unresolved, out _);
        internal static SetupSettings DeserializeWindowDraftWithOrigin(string json, VRCAvatarDescriptor avatar, out string[] unresolved, out LegacyDraftOrigin origin)
        {
            var envelope=JsonUtility.FromJson<WindowDraftEnvelope>(json);
            origin = envelope != null ? envelope.legacyOrigin : default;
            if(envelope==null || (envelope.version!=1 && envelope.version!=2) || (envelope.version==2 && (envelope.rendererMeshIds==null || envelope.rendererMeshIds.Length!=envelope.keys?.Length)) || string.IsNullOrEmpty(envelope.data) || envelope.keys==null || envelope.objectIds==null || envelope.keys.Length!=envelope.objectIds.Length || envelope.keys.Any(string.IsNullOrEmpty) || envelope.keys.Distinct(StringComparer.Ordinal).Count()!=envelope.keys.Length)
                throw new InvalidOperationException("Saved window draft identity evidence is invalid.");
            var state=ReadState(envelope.data);
            if(state.references==null || state.references.Any(r=>r==null || string.IsNullOrEmpty(r.key)) ||
               state.references.GroupBy(r=>r.key,StringComparer.Ordinal).Any(g=>g.Count()!=1) ||
               !new HashSet<string>(state.references.Select(r=>r.key),StringComparer.Ordinal).SetEquals(envelope.keys))
                throw new InvalidOperationException("Saved window draft reference table is invalid.");
            var bad=new HashSet<string>(StringComparer.Ordinal);
            foreach (var reference in state.references) {
                try {
                    if (!string.IsNullOrEmpty(reference.guid)) {
                        var path=AssetDatabase.GUIDToAssetPath(reference.guid);
                        if (string.IsNullOrEmpty(path) || !AssetDatabase.LoadAllAssetsAtPath(path).Any(value =>
                            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value,out string guid,out long id) && guid==reference.guid && id==reference.localId)) bad.Add(reference.key);
                    } else {
                        var target=ResolveUniqueDraftPath(avatar.transform,reference.path);
                        if(target==null || reference.key.EndsWith(":renderer",StringComparison.Ordinal) &&
                           (reference.componentIndex<0 || reference.componentIndex>=target.GetComponents<SkinnedMeshRenderer>().Length)) bad.Add(reference.key);
                    }
                } catch { bad.Add(reference.key); }
            }
            // Prevent BindSettings from temporarily assigning an ambiguous object.
            state.references=state.references.Where(r=>!bad.Contains(r.key)).ToList();
            var settings=BindSettings(state,avatar);
            var ids=envelope.keys.Select((key,index)=>new {key,id=envelope.objectIds[index]}).ToDictionary(x=>x.key,x=>x.id,StringComparer.Ordinal);
            var refs=WindowDraftReferences(settings).ToArray();
            foreach(var item in refs) {
                var actual=GlobalObjectId.GetGlobalObjectIdSlow(item.Value);
                if(!ids.TryGetValue(item.Key,out var expected) || actual.ToString()!=expected || GlobalObjectId.GlobalObjectIdentifierToObjectSlow(actual)!=item.Value) bad.Add(item.Key);
            }
            // Legacy drafts contain no prior mesh identity. Preserve the original
            // serialized draft and require explicit reassignment instead of assuming
            // the currently attached mesh is the same asset.
            if (envelope.version == 1)
                foreach (var item in refs.Where(r=>r.Value is SkinnedMeshRenderer)) bad.Add(item.Key);            if (envelope.version == 2) {
                var meshIds=envelope.keys.Select((key,index)=>new {key,id=envelope.rendererMeshIds[index]}).ToDictionary(x=>x.key,x=>x.id,StringComparer.Ordinal);
                foreach(var item in refs.Where(r=>r.Value is SkinnedMeshRenderer)) {
                    var renderer=(SkinnedMeshRenderer)item.Value;
                    var mesh=renderer.sharedMesh;
                    string actualMesh=mesh==null ? "" : GlobalObjectId.GetGlobalObjectIdSlow(mesh).ToString();
                    if(!meshIds.TryGetValue(item.Key,out var expectedMesh) || actualMesh!=(expectedMesh ?? "")) bad.Add(item.Key);
                }
            }            if (envelope.version == 2) foreach (var part in settings.parts) foreach (var row in StoredActions(part)) {
                var action=row.Value;
                if (action != null && action.kind == DepthActionKind.BlendShape && action.renderer != null &&
                    !string.IsNullOrEmpty(action.shape) && (action.renderer.sharedMesh == null || action.renderer.sharedMesh.GetBlendShapeIndex(action.shape) < 0))
                    bad.Add(row.Key + ":renderer");
            }            var present=new HashSet<string>(refs.Select(r=>r.Key),StringComparer.Ordinal);
            foreach(var key in envelope.keys) if(!present.Contains(key)) bad.Add(key);
            foreach(var key in bad) ClearDraftReference(settings,key);
            unresolved=bad.OrderBy(x=>x,StringComparer.Ordinal).ToArray();
            return settings;
        }
        internal static SetupSettings DeserializeWindowDraft(string json, VRCAvatarDescriptor avatar)
        {
            var settings=DeserializeWindowDraftPartial(json,avatar,out var missing);
            if(missing.Length!=0) throw new InvalidOperationException("Saved draft references unavailable: "+string.Join(", ",missing));
            return settings;
        }
        internal static SpsAuthoringContract ReadAuthoringContract(string identity) => LoadAsset(identity).authoringContract;

        // Build-only compatibility: absent settings cannot establish new provenance.
        // Retain the old native-only legacy version check; existing invalid assets still fail strictly.
        internal static SpsAuthoringContract ReadBuildAuthoringContract(string identity) =>
            string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(identity))
                ? SpsAuthoringContract.LegacySps2 : ReadAuthoringContract(identity);

        private static Sps2SetupAsset LoadAsset(string identity)
        {
            var asset = AssetDatabase.LoadAssetAtPath<Sps2SetupAsset>(AssetDatabase.GUIDToAssetPath(identity));
            if (asset == null || asset.schema != 1 || string.IsNullOrEmpty(asset.stateJson))
                throw new InvalidOperationException(L("SPS2 の設定アセットが見つからないか形式が一致しません。Prefab と対応する設定 .asset を一緒に配置してください。"));
            SpsAuthoringCompatibility.ValidateContract(asset.authoringContract);
            return asset;
        }
        private static string Path(Transform value, Transform root)
        {
            if (value == null) return null;
            if (value != root && !value.IsChildOf(root)) throw new InvalidOperationException(L("設定の参照が対象アバターの外にあります。"));
            var names = new List<string>();
            for (var t = value; t != root; t = t.parent)
            {
                if (t.name.Contains("/") || t.parent.Cast<Transform>().Count(c => c.name == t.name) != 1)
                    throw new InvalidOperationException(L("設定参照の階層名が重複しているか、/ を含んでいます: ") + t.name);
                names.Add(t.name);
            }
            names.Reverse(); return string.Join("/", names);
        }
        // Binding loses the distinction between an absent input and a saved reference
        // whose target disappeared. Inspect the original record before permitting a skip.
        internal static bool ProvesReferenceNeverAssigned(Sps2SetupContext root, string key)
        {
            if (root == null) return true;
            if (root.asset == null || root.asset.schema != 1 || string.IsNullOrEmpty(root.asset.stateJson)) return false;
            // Unity materializes absent and even explicit-null inline objects/lists.
            // Require their recorded JSON types before binding can erase that distinction.
            if (!HasExplicitHistoryFields(root.asset.stateJson)) return false;
            var state = ReadState(root.asset.stateJson);
            return state.settings?.parts != null && state.references != null && state.sockets != null &&
                !state.references.Any(r => r == null || string.IsNullOrEmpty(r.key) || r.key == key) &&
                state.references.Select(r => r.key).Distinct(StringComparer.Ordinal).Count() == state.references.Count;
        }

        private static bool HasExplicitHistoryFields(string json)
        {
            // Use the framework JSON reader, not substring/regex matching against nested
            // values. This introduces no package dependency or persisted schema change.
            using (var reader = JsonReaderWriterFactory.CreateJsonReader(Encoding.UTF8.GetBytes(json), XmlDictionaryReaderQuotas.Max))
            {
                var root = XElement.Load(reader);
                if ((string)root.Attribute("type") != "object") return false;
                // A duplicated record key could otherwise overwrite evidence of a past
                // assignment. Reject duplicate properties throughout the history object.
                if (root.DescendantsAndSelf().Where(e => (string)e.Attribute("type") == "object")
                    .Any(e => e.Elements().GroupBy(child => child.Name.NamespaceName == "item"
                        ? (string)child.Attribute("item") : child.Name.LocalName, StringComparer.Ordinal)
                        .Any(group => group.Count() != 1))) return false;
                XElement Field(XElement parent, string name, string type)
                {
                    var values = parent.Elements(name).ToArray();
                    return values.Length == 1 && (string)values[0].Attribute("type") == type ? values[0] : null;
                }
                var settings = Field(root, "settings", "object");
                return settings != null && Field(settings, "parts", "array") != null &&
                    Field(root, "references", "array") != null && Field(root, "sockets", "array") != null;
            }
        }

        // Project an imported 1.2.0 draft for explicit fresh regeneration only.
        // Older current-format drafts have no legacy origin and remain authoritative.
        private static void InitializeUpgradeDraft(SetupSettings draft, string savedJson, IList<string> missing)
        {
            using var reader = JsonReaderWriterFactory.CreateJsonReader(Encoding.UTF8.GetBytes(savedJson), XmlDictionaryReaderQuotas.Max);
            var saved = XElement.Load(reader).Element("settings");
            var parts = saved.Element("parts").Elements().ToDictionary(e => (string)e.Element("id"), StringComparer.Ordinal);
            bool hasPolicy = saved.Elements().Any(e => e.Name.LocalName.StartsWith("sps3", StringComparison.Ordinal)) ||
                parts.Values.Any(e => e.Element("sps3AutoParticipant") != null);
            if (!hasPolicy && draft.sps3PolicyVersion == 0 && draft.sps3AutoEligibilityVersion == 0)
                FullSetupCatalog.InitializeSps3AutoDefaults(draft, draft.autoMode);

            foreach (var part in draft.parts) {
                parts.TryGetValue(part.id, out var raw);
                // Even version 0 or an explicitly empty group list in a newer Setup is saved input.
                if (part.animationModelVersion != 0 || raw != null &&
                    (raw.Element("animationModelVersion") != null || raw.Element("animationGroups") != null ||
                     raw.Element("animationEnabled") != null)) continue;
                // A part added in the window already has its own authoring values.
                FullSetupCatalog.InitializeNewSps3Depth(part, raw == null || raw.Element("range") != null,
                    raw == null || raw.Element("units") != null);
                // Preserve unresolved-reference warnings on the promoted action as well as its flat mirror.
                if (missing == null || missing.Count == 0) continue;
                var group = part.animationGroups.Single();
                for (int i = 0; i < group.actions.Count; i++)
                    foreach (var field in new[] { ":renderer", ":object", ":clip", ":customBlendShapeClip" })
                        if (missing.Contains(part.id + ":" + i + field)) missing.Add(group.ReferenceKey(part.id, i) + field);
            }
        }

        private static State ReadState(string json)
        {
            var state = JsonUtility.FromJson<State>(json);
            ValidateAnimationJson(json, state?.settings);
            return state;
        }
        internal static void ValidateAnimationJson(string json, SetupSettings settings)
        {
            using (var reader = JsonReaderWriterFactory.CreateJsonReader(Encoding.UTF8.GetBytes(json), XmlDictionaryReaderQuotas.Max))
            {
                var root = XElement.Load(reader);
                var parts = root.Element("settings")?.Element("parts")?.Elements().ToArray();
                if (parts == null || settings?.parts == null || parts.Length != settings.parts.Count)
                    throw new InvalidOperationException("Invalid animation settings structure.");
                for (int i = 0; i < parts.Length; i++)
                {
                    var raw = parts[i]; var part = settings.parts[i];
                    var versions = raw.Elements("animationModelVersion").ToArray();
                    if (versions.Length == 0) { part.animationModelVersion = 0; continue; }
                    if (versions.Length != 1 || (string)versions[0].Attribute("type") != "number" ||
                        !int.TryParse(versions[0].Value, out int version) || version < 0 || version > 1)
                        throw new InvalidOperationException("Unknown animation model version.");
                    part.animationModelVersion = version;
                    if (version == 0) continue;
                    void Require(XElement value, string name, string type)
                    {
                        var fields = value?.Elements(name).ToArray();
                        if (fields == null || fields.Length != 1 || (string)fields[0].Attribute("type") != type)
                            throw new InvalidOperationException("Missing or malformed animation field: " + name);
                    }
                    Require(raw, "animationEnabled", "boolean"); Require(raw, "animationGroups", "array");
                    var ids = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var group in raw.Element("animationGroups").Elements())
                    {
                        Require(group, "id", "string"); Require(group, "kind", "number"); Require(group, "actions", "array");
                        string id = group.Element("id").Value;
                        if (string.IsNullOrEmpty(id) || !ids.Add(id) || id != SocketAnimationGroup.LegacyId && !Guid.TryParseExact(id, "N", out _))
                            throw new InvalidOperationException("Invalid or duplicate animation group identity.");
                        if (!int.TryParse(group.Element("kind").Value, out int kind) || kind < 0 || kind > 2)
                            throw new InvalidOperationException("Unknown animation group kind.");
                        if (group.Element("actions").Elements().Any(a => (string)a.Attribute("type") != "object"))
                            throw new InvalidOperationException("Missing animation action.");
                        foreach(var action in group.Element("actions").Elements()) {
                            if(action.Element("blendShapeMode") == null && action.Element("blendShapePoints") == null)continue;
                            Require(action,"blendShapeMode","number"); Require(action,"blendShapePoints","array");
                            foreach(var point in action.Element("blendShapePoints").Elements()) {
                                if((string)point.Attribute("type")!="object")throw new InvalidOperationException("Malformed BlendShape point.");
                                Require(point,"position","number");Require(point,"value","number");
                            }
                        }
                        foreach (var name in new[] { "depthSettings", "entrySettings" })
                        {
                            Require(group, name, "object"); var values = group.Element(name);
                            Require(values, "range", "object"); Require(values.Element("range"), "x", "number");
                            Require(values.Element("range"), "y", "number"); Require(values, "units", "number");
                            foreach (var field in new[] { "enableSelf", "enableOthers", "radiusWeightEnabled" }) Require(values, field, "boolean");
                            foreach (var field in new[] { "smoothingSeconds", "radiusZero", "radiusFull" }) Require(values, field, "number");
                            // Optional as a group: old snapshots stay manual; malformed partial banks cannot lose manual values.
                            if (values.Element("radiusAutoCorrection") != null || values.Element("radiusManualZero") != null || values.Element("radiusManualFull") != null) {
                                Require(values, "radiusAutoCorrection", "boolean");
                                Require(values, "radiusManualZero", "number"); Require(values, "radiusManualFull", "number");
                            }
                        }
                    }
                }
            }
        }

        private static Transform Resolve(Transform root, string path) => path == null ? null : path.Length == 0 ? root : root.Find(path);
        // Legacy payload is retained independently. Stable group IDs, never display order or kind,
        // identify new references, including inactive groups and inactive action kinds.
        private static IEnumerable<KeyValuePair<string, DepthActionSettings>> StoredActions(SocketSettings part)
        {
            if (part.actions != null)
                for (int i = 0; i < part.actions.Count; i++)
                    if (part.actions[i] != null) yield return new KeyValuePair<string, DepthActionSettings>(part.id + ":" + i, part.actions[i]);
            if (part.animationModelVersion != 0 && part.animationGroups != null)
                foreach (var group in part.animationGroups)
                    if (group?.actions != null)
                        for (int i = 0; i < group.actions.Count; i++)
                            if (group.actions[i] != null) yield return new KeyValuePair<string, DepthActionSettings>(group.ReferenceKey(part.id, i), group.actions[i]);
        }

        internal static string Serialize(Sps2SetupContext root)
        {
            var state = new State { settings = root.settings.Copy(), oralBoundaryPath = root.oralBoundaryPath, nativePathProof = root.nativePathProof };
            void SaveReference(string key, UnityEngine.Object value)
            {
                if (value == null) return;
                var reference = new Reference { key = key };
                if (value is AnimationClip)
                {
                    if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out reference.guid, out reference.localId))
                        throw new InvalidOperationException(L("Animation Clip は保存済みアセットを指定してください。"));
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
                foreach (var row in StoredActions(part))
                {
                    var a = row.Value; string key = row.Key;
                    SaveReference(key + ":customBlendShapeClip", a.generatedBlendShapeClip); SaveReference(key + ":renderer", a.renderer); SaveReference(key + ":clip", a.clip); SaveReference(key + ":object", a.target);
                    a.renderer = null; a.clip = null; a.target = null; a.generatedBlendShapeClip = null;
                }
            }
            foreach (var socket in root.sockets)
                state.sockets.Add(new SocketRecord { id = socket.id, anchor = Path(socket.anchor, root.transform), pose = Path(socket.pose, root.transform),
                    stops = socket.pathStops.Select(t => Path(t, root.transform)).ToArray() });
            var json = JsonUtility.ToJson(state);
            ValidateAnimationJson(json, state.settings);
            return json;
        }
        private static SetupSettings BindSettings(State state, VRCAvatarDescriptor avatar, List<string> missingLegacyReferences = null)
        {
            var settings = state.settings.Copy();
            Reference Reference(string key) => state.references.SingleOrDefault(r => r.key == key);
            Transform Transform(string key)
            {
                var r = Reference(key);
                if (r == null) return null;
                if (missingLegacyReferences == null) return Resolve(avatar.transform, r.path);
                // 1.2.0 recorded a relative path, not a persistent scene object ID.
                try { return ResolveUniqueDraftPath(avatar.transform, r.path); }
                catch (InvalidOperationException) { missingLegacyReferences.Add(key); return null; }
            }
            foreach (var part in settings.parts)
            {
                part.target = Transform(part.id + ":target");
                foreach (var row in StoredActions(part))
                {
                    var a = row.Value; string key = row.Key;
                    var r = Reference(key + ":renderer"); var renderer = Transform(key + ":renderer");
                    a.renderer = renderer == null ? null : renderer.GetComponents<SkinnedMeshRenderer>().ElementAtOrDefault(r.componentIndex);
                    if (renderer != null && a.renderer == null) missingLegacyReferences?.Add(key + ":renderer");
                    var target = Transform(key + ":object"); a.target = target != null ? target.gameObject : null;
                    var generated = Reference(key + ":customBlendShapeClip");
                    a.generatedBlendShapeClip = generated == null ? null : AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(generated.guid)).OfType<AnimationClip>()
                        .SingleOrDefault(c => AssetDatabase.TryGetGUIDAndLocalFileIdentifier(c, out string g, out long i) && i == generated.localId);
                    var clip = Reference(key + ":clip");
                    // Like renderer/target above, rebind only from the authoritative reference
                    // table. JsonUtility can produce a non-null managed wrapper for instanceID 0.
                    // An absent assignment must not retain that placeholder. A present but
                    // unresolved GUID remains recorded and is rejected by the history check.
                    a.clip = clip == null ? null : AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(clip.guid)).OfType<AnimationClip>()
                        .SingleOrDefault(c => AssetDatabase.TryGetGUIDAndLocalFileIdentifier(c, out string guid, out long id) && id == clip.localId);
                    if (clip != null && a.clip == null) missingLegacyReferences?.Add(key + ":clip");
                }
            }
            return settings;
        }
        internal static Sps2SetupContext Find(VRCAvatarDescriptor avatar, bool build = false, SpsFamily? buildFamily = null, Transform excludedStagingRoot = null)
        {
            if (avatar == null) return null;
            // SDK callbacks serve Fury only. An NDMF setup must remain invisible to
            // them even when its authoring asset is missing or stale.
            var owned = SpsBackend.AllSockets(avatar.gameObject)
                // A staged candidate can coexist with the old root during creation.
                // Exclusion is explicit and scoped to this lookup, never global.
                .Where(s => excludedStagingRoot == null || !s.transform.IsChildOf(excludedStagingRoot))
                .Where(s => !build || !buildFamily.HasValue || SpsBackend.FamilyOf(s) == buildFamily.Value)
                .Select(s => new { socket = s, token = VrcFuryCompatibility.ReadIdentity(s) })
                .Where(s => TryToken(s.token, out _, out _)).ToArray();
            if (owned.Length == 0)
            {
                if (!build && FullSetupGenerator.HasGeneratedRoot(avatar))
                    throw new InvalidOperationException(SpsBackend.MissingSourceGuidance);
                return null;
            }
            var identities = owned.Select(s => { TryToken(s.token, out var id, out _); return id; }).Distinct().ToArray();
            if (identities.Length != 1) throw new InvalidOperationException(L("対象アバターに複数の SPS2 設定があります。"));
            var asset = LoadAsset(identities[0]); var state = ReadState(asset.stateJson);
            var transform = FullSetupGenerator.ResolveNamedRoot(avatar, excludedStagingRoot);
            if (!build && transform == null) throw new InvalidOperationException(L("SPS2 生成ルートが見つかりません。"));
            var missing = !build && asset.authoringContract == SpsAuthoringContract.LegacySps2 ? new List<string>() : null;
            var result = new Sps2SetupContext { avatar = avatar, asset = asset, identity = identities[0], gameObject = transform != null ? transform.gameObject : avatar.gameObject,
                settings = build ? state.settings.Copy() : BindSettings(state, avatar, missing), missingLegacyReferences = missing?.ToArray(), oralBoundaryPath = state.oralBoundaryPath, nativePathProof = state.nativePathProof };
            foreach (var record in state.sockets)
            {
                var sockets = owned.Where(s => s.token == Token(result, record.id)).ToArray();
                if (sockets.Length > 1) throw new InvalidOperationException(L("SPS2 Socket の識別子が重複しています。"));
                var socket = sockets.SingleOrDefault()?.socket;
                var pose = build ? socket?.transform : Resolve(transform, record.pose);
                result.sockets.Add(new GeneratedSocket { id=record.id, socket=socket, pose=pose,
                    anchor=build ? pose?.parent : Resolve(transform, record.anchor),
                    pathStops=build ? Array.Empty<Transform>() : record.stops.Select(p => Resolve(transform, p)).ToArray() });
            }
            if (owned.Length != result.sockets.Count(s => s.socket != null)) throw new InvalidOperationException(L("設定アセットに記録されていない SPS2 Socket があります。"));
            SpsBackend.ValidateExisting(result, result.settings.family);
            FullSetupCatalog.UpgradeDisplayNames(result.settings);
            return result;
        }
    }
}
