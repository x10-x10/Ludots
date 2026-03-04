using System;
using System.Collections.Generic;
using System.Numerics;

namespace Ludots.Core.Presentation.Config
{
    /// <summary>
    /// Configurable acceptance/debug settings for interactive model verification.
    /// Loaded through ConfigPipeline from Presentation/acceptance_debug.json.
    /// </summary>
    public sealed class AcceptanceDebugConfig
    {
        public ConsoleOptions Console { get; set; } = new ConsoleOptions();
        public AcceptOptions Accept { get; set; } = new AcceptOptions();

        public void Normalize()
        {
            Console ??= new ConsoleOptions();
            Accept ??= new AcceptOptions();
            Accept.Normalize();
        }

        public sealed class ConsoleOptions
        {
            public string ToggleKey { get; set; } = "F8";
            public bool AllowCommandsWithoutPrefix { get; set; } = true;
        }

        public sealed class AcceptOptions
        {
            public float DefaultFocusDistanceCm { get; set; } = 18000f;
            public AcceptanceCasePreset CaseOn { get; set; } = AcceptanceCasePreset.CreateDefaultCaseOn();
            public ProbeVisualOptions ProbeVisual { get; set; } = new ProbeVisualOptions();
            public List<FocusPreset> FocusPresets { get; set; } = new List<FocusPreset>();
            public List<AcceptanceCasePreset> CasePresets { get; set; } = new List<AcceptanceCasePreset>();

            public void Normalize()
            {
                if (DefaultFocusDistanceCm <= 0f) DefaultFocusDistanceCm = 18000f;
                CaseOn ??= AcceptanceCasePreset.CreateDefaultCaseOn();
                CaseOn.NormalizeDefaults();
                ProbeVisual ??= new ProbeVisualOptions();
                ProbeVisual.Normalize();
                FocusPresets ??= new List<FocusPreset>();
                CasePresets ??= new List<AcceptanceCasePreset>();

                for (int i = 0; i < FocusPresets.Count; i++)
                    FocusPresets[i]?.Normalize(DefaultFocusDistanceCm);
                for (int i = 0; i < CasePresets.Count; i++)
                    CasePresets[i]?.NormalizeDefaults();
            }
        }

        public sealed class FocusPreset
        {
            public string Id { get; set; } = string.Empty;
            public string Keyword { get; set; } = string.Empty;
            public float DistanceCm { get; set; } = 18000f;

            public void Normalize(float fallbackDistanceCm)
            {
                Id ??= string.Empty;
                Keyword ??= string.Empty;
                if (DistanceCm <= 0f) DistanceCm = fallbackDistanceCm > 0f ? fallbackDistanceCm : 18000f;
            }
        }

        public sealed class ProbeVisualOptions
        {
            public int MaxProbeCount { get; set; } = 64;
            public float MinHeightMeters { get; set; } = 3f;
            public float MinBoxSizeMeters { get; set; } = 2f;
            public float TipRadiusMeters { get; set; } = 0.35f;
            public float BoxAlpha01 { get; set; } = 0.35f;

            public void Normalize()
            {
                if (MaxProbeCount <= 0) MaxProbeCount = 64;
                if (MinHeightMeters <= 0f) MinHeightMeters = 3f;
                if (MinBoxSizeMeters <= 0f) MinBoxSizeMeters = 2f;
                if (TipRadiusMeters <= 0f) TipRadiusMeters = 0.35f;
                if (BoxAlpha01 < 0f) BoxAlpha01 = 0f;
                if (BoxAlpha01 > 1f) BoxAlpha01 = 1f;
            }
        }

        public sealed class AcceptanceCasePreset
        {
            public string Id { get; set; } = "default";
            public bool EnableRenderCameraDebug { get; set; } = true;
            public float PullBackMeters { get; set; } = 20f;
            public float ScaleMultiplier { get; set; } = 2f;
            public bool DrawLogicalCullingDebug { get; set; } = true;
            public bool DrawAcceptanceProbes { get; set; } = true;
            public float[] PositionOffsetMeters { get; set; } = new[] { 0f, 0f, 0f };
            public float[] TargetOffsetMeters { get; set; } = new[] { 0f, 0f, 0f };

            public void NormalizeDefaults()
            {
                Id ??= "default";
                if (PullBackMeters < 0f) PullBackMeters = 0f;
                if (ScaleMultiplier <= 0f) ScaleMultiplier = 1f;
                PositionOffsetMeters = NormalizeVec3(PositionOffsetMeters);
                TargetOffsetMeters = NormalizeVec3(TargetOffsetMeters);
            }

            public Vector3 PositionOffsetVector => ToVector3(PositionOffsetMeters);
            public Vector3 TargetOffsetVector => ToVector3(TargetOffsetMeters);

            public static AcceptanceCasePreset CreateDefaultCaseOn()
            {
                return new AcceptanceCasePreset
                {
                    Id = "default",
                    EnableRenderCameraDebug = true,
                    PullBackMeters = 20f,
                    ScaleMultiplier = 2f,
                    DrawLogicalCullingDebug = true,
                    DrawAcceptanceProbes = true,
                    PositionOffsetMeters = new[] { 0f, 0f, 0f },
                    TargetOffsetMeters = new[] { 0f, 0f, 0f }
                };
            }

            private static float[] NormalizeVec3(float[] raw)
            {
                if (raw == null || raw.Length < 3) return new[] { 0f, 0f, 0f };
                return new[] { raw[0], raw[1], raw[2] };
            }

            private static Vector3 ToVector3(float[] raw)
            {
                if (raw == null || raw.Length < 3) return Vector3.Zero;
                return new Vector3(raw[0], raw[1], raw[2]);
            }
        }
    }
}
