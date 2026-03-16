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

        public const string SmartCastActionId = "CastModeSmart";
        public const string IndicatorActionId = "CastModeIndicator";
        public const string PressReleaseActionId = "CastModePressRelease";
        public const string SelectionIndicatorPerformerKey = "champion_skill_sandbox.selection_indicator";
        public const int SelectionIndicatorScopeId = 4101;
        public const int EffectAppliedHudDiscriminator = 41;

        public const string EzrealAlphaName = "Ezreal Alpha";
        public const string EzrealCooldownName = "Ezreal Cooldown";
        public const string GarenAlphaName = "Garen Alpha";
        public const string GarenCourageName = "Garen Courage";
        public const string JayceCannonName = "Jayce Cannon";
        public const string JayceHammerName = "Jayce Hammer";

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
    }
}
