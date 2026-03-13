namespace CameraAcceptanceMod.Runtime
{
    internal sealed class CameraAcceptanceDiagnosticsState
    {
        private const float SampleWeight = 0.18f;

        public bool HudEnabled { get; set; } = true;
        public bool TextEnabled { get; set; } = true;

        public float PanelSyncMs { get; private set; }
        public float HudBuildMs { get; private set; }
        public float TextBuildMs { get; private set; }

        public void ObservePanelSync(double sampleMs) => PanelSyncMs = Smooth(PanelSyncMs, (float)sampleMs);
        public void ObserveHudBuild(double sampleMs) => HudBuildMs = Smooth(HudBuildMs, (float)sampleMs);
        public void ObserveTextBuild(double sampleMs) => TextBuildMs = Smooth(TextBuildMs, (float)sampleMs);

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
