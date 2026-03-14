using System;

namespace CameraAcceptanceMod.Runtime
{
    internal sealed class CameraAcceptanceDiagnosticsState
    {
        private const float SampleWeight = 0.18f;

        public bool HudEnabled { get; set; } = true;
        public bool TextEnabled { get; set; } = true;
        public bool HotpathBarsEnabled { get; set; } = true;
        public bool HotpathHudTextEnabled { get; set; } = true;
        public bool HotpathCullCrowdEnabled { get; set; } = true;

        public float SmoothedFrameMs { get; private set; } = 16.67f;
        public float PanelSyncMs { get; private set; }
        public float HudBuildMs { get; private set; }
        public float TextBuildMs { get; private set; }
        public float HotpathBarBuildMs { get; private set; }
        public float HotpathHudTextBuildMs { get; private set; }
        public float HotpathPrimitiveBuildMs { get; private set; }
        public float HotpathVisibleSampleMs { get; private set; }

        public int HotpathCrowdCount { get; private set; }
        public int HotpathVisibleCrowdCount { get; private set; }
        public int HotpathBarItemCount { get; private set; }
        public int HotpathHudTextItemCount { get; private set; }
        public int HotpathPrimitiveItemCount { get; private set; }
        public int HotpathSelectionLabelCount { get; private set; }
        public int HotpathVisibleSampleStride { get; private set; } = 1;
        public int HotpathSweepCycle { get; private set; }
        public string HotpathSweepPhase { get; private set; } = "inactive";
        public string HotpathSweepTarget { get; private set; } = "none";
        public string[] HotpathVisibleSampleWindow { get; private set; } = Array.Empty<string>();

        public float SmoothedFps => SmoothedFrameMs > 0.001f ? 1000f / SmoothedFrameMs : 0f;

        public void ObserveFrameTime(double sampleMs) => SmoothedFrameMs = Smooth(SmoothedFrameMs, (float)sampleMs);
        public void ObservePanelSync(double sampleMs) => PanelSyncMs = Smooth(PanelSyncMs, (float)sampleMs);
        public void ObserveHudBuild(double sampleMs) => HudBuildMs = Smooth(HudBuildMs, (float)sampleMs);
        public void ObserveTextBuild(double sampleMs) => TextBuildMs = Smooth(TextBuildMs, (float)sampleMs);
        public void ObserveHotpathBars(double sampleMs) => HotpathBarBuildMs = Smooth(HotpathBarBuildMs, (float)sampleMs);
        public void ObserveHotpathHudText(double sampleMs) => HotpathHudTextBuildMs = Smooth(HotpathHudTextBuildMs, (float)sampleMs);
        public void ObserveHotpathPrimitives(double sampleMs) => HotpathPrimitiveBuildMs = Smooth(HotpathPrimitiveBuildMs, (float)sampleMs);
        public void ObserveHotpathVisibleSample(double sampleMs) => HotpathVisibleSampleMs = Smooth(HotpathVisibleSampleMs, (float)sampleMs);

        public void PublishHotpathLaneCounts(int crowdCount, int visibleCrowdCount, int barItemCount, int hudTextItemCount, int primitiveItemCount)
        {
            HotpathCrowdCount = crowdCount < 0 ? 0 : crowdCount;
            HotpathVisibleCrowdCount = visibleCrowdCount < 0 ? 0 : visibleCrowdCount;
            HotpathBarItemCount = barItemCount < 0 ? 0 : barItemCount;
            HotpathHudTextItemCount = hudTextItemCount < 0 ? 0 : hudTextItemCount;
            HotpathPrimitiveItemCount = primitiveItemCount < 0 ? 0 : primitiveItemCount;
        }

        public void PublishHotpathSelectionLabelCount(int selectionLabelCount)
        {
            HotpathSelectionLabelCount = selectionLabelCount < 0 ? 0 : selectionLabelCount;
        }

        public void PublishHotpathVisibleSample(string[] lines, int stride)
        {
            HotpathVisibleSampleWindow = lines ?? Array.Empty<string>();
            HotpathVisibleSampleStride = stride <= 0 ? 1 : stride;
        }

        public void PublishHotpathSweep(string phase, int cycle, string target)
        {
            HotpathSweepPhase = string.IsNullOrWhiteSpace(phase) ? "inactive" : phase;
            HotpathSweepCycle = cycle < 0 ? 0 : cycle;
            HotpathSweepTarget = string.IsNullOrWhiteSpace(target) ? "none" : target;
        }

        public void ResetHotpathState()
        {
            ObserveHotpathBars(0d);
            ObserveHotpathHudText(0d);
            ObserveHotpathPrimitives(0d);
            ObserveHotpathVisibleSample(0d);
            PublishHotpathLaneCounts(0, 0, 0, 0, 0);
            PublishHotpathSelectionLabelCount(0);
            PublishHotpathVisibleSample(Array.Empty<string>(), 1);
            PublishHotpathSweep("inactive", 0, "none");
        }

        private static float Smooth(float current, float sampleMs)
        {
            if (sampleMs < 0f)
            {
                sampleMs = 0f;
            }

            return current <= 0.001f
                ? sampleMs
                : (current * (1f - SampleWeight)) + (sampleMs * SampleWeight);
        }
    }
}
