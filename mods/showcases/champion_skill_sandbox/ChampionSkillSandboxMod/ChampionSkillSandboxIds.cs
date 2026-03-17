using System;

namespace ChampionSkillSandboxMod
{
    internal static class ChampionSkillSandboxIds
    {
        public const string MapId = "champion_skill_sandbox";
        public const string InputContextId = "ChampionSkillSandbox.Controls";

        public const string SmartCastModeId = "ChampionSkillSandbox.Mode.SmartCast";
        public const string IndicatorModeId = "ChampionSkillSandbox.Mode.Indicator";
        public const string PressReleaseModeId = "ChampionSkillSandbox.Mode.PressReleaseAim";
        public const string TacticalCameraId = "ChampionSkillSandbox.Camera.Tactical";

        public const string SmartCastActionId = "CastModeSmart";
        public const string IndicatorActionId = "CastModeIndicator";
        public const string PressReleaseActionId = "CastModePressRelease";
        public const string ResetCameraActionId = "ResetCamera";
        public const string FreeCameraToolbarButtonId = "ChampionSkillSandbox.Camera.Free";
        public const string FollowSelectionToolbarButtonId = "ChampionSkillSandbox.Camera.Selection";
        public const string FollowSelectionGroupToolbarButtonId = "ChampionSkillSandbox.Camera.SelectionGroup";
        public const string ResetCameraToolbarButtonId = "ChampionSkillSandbox.Camera.Reset";
        public const string ResetCameraRequestKey = "ChampionSkillSandbox.Camera.ResetRequested";
        public const string CameraFollowModeKey = "ChampionSkillSandbox.Camera.FollowMode";
        public const string SelectionIndicatorPerformerKey = "champion_skill_sandbox.selection_indicator";
        public const string HoverIndicatorPerformerKey = "champion_skill_sandbox.hover_indicator";
        public const int SelectionIndicatorScopeId = 4101;
        public const int HoverIndicatorScopeId = 4102;

        public const string EzrealAlphaName = "Ezreal Alpha";
        public const string EzrealCooldownName = "Ezreal Cooldown";
        public const string GarenAlphaName = "Garen Alpha";
        public const string GarenCourageName = "Garen Courage";
        public const string JayceCannonName = "Jayce Cannon";
        public const string JayceHammerName = "Jayce Hammer";
        public const string GeomancerAlphaName = "Geomancer Alpha";
        public const string RunicBeaconName = "Runic Beacon";
        public const string RuneFieldName = "Rune Field";
        public const string StonePillarName = "Stone Pillar";

        public const string GarenCourageTag = "State.Champion.Garen.Courage";
        public const string JayceHammerTag = "State.Champion.Jayce.Hammer";
        public const string EzrealBlockedTag = "Cooldown.Champion.Ezreal.R";

        public static bool IsSandboxMap(string? mapId)
        {
            return string.Equals(mapId, MapId, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsSandboxMode(string? modeId)
        {
            return string.Equals(modeId, SmartCastModeId, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(modeId, IndicatorModeId, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(modeId, PressReleaseModeId, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsCameraFollowMode(string? buttonId)
        {
            return string.Equals(buttonId, FreeCameraToolbarButtonId, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(buttonId, FollowSelectionToolbarButtonId, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(buttonId, FollowSelectionGroupToolbarButtonId, StringComparison.OrdinalIgnoreCase);
        }
    }
}
