using System;
using LumaKroma.Sps2SetupAssistant.Editor.Compatibility;
using UnityEngine;

namespace LumaKroma.Sps2SetupAssistant.Editor
{
    public sealed partial class Sps2SetupAssistantWindow
    {
        [NonSerialized] private AutoDisplayCache<VrcFuryCapabilities> depthDisplayCache;
        [NonSerialized] private DisplayPassCache<GenerationPreview> generationPreviewPass;
        [NonSerialized] private DisplayPassCache<RadiusFeatureDisplay> radiusFeaturePass;
        private sealed class RadiusFeatureDisplay { internal bool HasMinimum; }
        private void BeginDepthDisplayPass()
        {
            if (depthDisplayCache == null) depthDisplayCache = new AutoDisplayCache<VrcFuryCapabilities>();
            if (generationPreviewPass == null) generationPreviewPass = new DisplayPassCache<GenerationPreview>();
            if (radiusFeaturePass == null) radiusFeaturePass = new DisplayPassCache<RadiusFeatureDisplay>();
            generationPreviewPass.Begin(Event.current != null && PreviewUsesMemo(Event.current.type));
            radiusFeaturePass.Begin(Event.current != null && PreviewUsesMemo(Event.current.type));
        }
        private void EndDepthDisplayPass() { generationPreviewPass?.End(); radiusFeaturePass?.End(); }
        private bool RadiusMinimumForDrawing()
        {
            if (radiusFeaturePass == null) return FuryNativeFeatureShapes.InspectInstalled().RadiusZero;
            return radiusFeaturePass.Get(
                () => new RadiusFeatureDisplay { HasMinimum = FuryNativeFeatureShapes.InspectInstalled().RadiusZero },
                () => {
                    int revision = shapeSelectionRevision, contextRevision = autoContextRevision;
                    var draft = settings; var avatar = descriptor;
                    return () => this != null && ReferenceEquals(settings, draft) && ReferenceEquals(descriptor, avatar) &&
                        (ReferenceEquals(avatar, null) || avatar != null) && revision == shapeSelectionRevision && contextRevision == autoContextRevision;
                }).HasMinimum;
        }
        private VrcFuryCapabilities DepthCapabilitiesForDrawing
        {
            get
            {
                // Retain display results across drawing passes until relevant state changes.
                // Generation and popup commits still validate against the live state.
                if (depthDisplayCache == null) return SpsAuthoringCompatibility.ForSelection(settings.family, descriptor, true);
                return depthDisplayCache.Get(UnityEditor.EditorApplication.timeSinceStartup,
                    () => SpsAuthoringCompatibility.ForSelection(settings.family, descriptor, true),
                    () => {
                        var avatar = descriptor; var draft = settings; var family = settings.family;
                        int revision = shapeSelectionRevision, contextRevision = autoContextRevision;
                        return () => this != null && ReferenceEquals(descriptor, avatar) &&
                            (ReferenceEquals(avatar, null) || avatar != null) &&
                            ReferenceEquals(settings, draft) && settings != null && settings.family == family &&
                            revision == shapeSelectionRevision && contextRevision == autoContextRevision;
                    });
            }
        }
    }
}
