using System;
using Arch.Core;
using CoreInputMod.ViewMode;
using Ludots.Core.Engine;
using Ludots.Core.UI.EntityCommandPanels;

namespace ChampionSkillSandboxMod.Runtime
{
    internal sealed class ChampionSkillCastModeToolbarProvider : IEntityCommandPanelToolbarProvider
    {
        private GameEngine? _engine;

        public bool IsVisible => ChampionSkillSandboxIds.IsSandboxMap(_engine?.CurrentMapSession?.MapId.Value);

        public uint Revision
        {
            get
            {
                uint revision = IsVisible ? 1u : 0u;
                string activeModeId = ResolveViewModeManager()?.ActiveMode?.Id ?? string.Empty;
                string activeFollowModeId = ResolveActiveCameraFollowMode();
                if (!string.IsNullOrWhiteSpace(activeModeId))
                {
                    revision ^= (uint)activeModeId.GetHashCode(StringComparison.Ordinal);
                }
                if (!string.IsNullOrWhiteSpace(activeFollowModeId))
                {
                    revision ^= (uint)activeFollowModeId.GetHashCode(StringComparison.Ordinal);
                }

                return revision;
            }
        }

        public string Title => "Cast Mode";
        public string Subtitle => "Cast + Camera | F1/F2/F3 Cast | RMB Move";

        public void Bind(GameEngine engine)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        }

        public int CopyButtons(Span<EntityCommandPanelToolbarButtonView> destination)
        {
            if (!IsVisible || destination.IsEmpty)
            {
                return 0;
            }

            string? activeModeId = ResolveViewModeManager()?.ActiveMode?.Id;
            string activeFollowModeId = ResolveActiveCameraFollowMode();
            var buttons = new EntityCommandPanelToolbarButtonView[7];
            buttons[0] = new EntityCommandPanelToolbarButtonView(
                ChampionSkillSandboxIds.SmartCastModeId,
                "Quick",
                string.Equals(activeModeId, ChampionSkillSandboxIds.SmartCastModeId, StringComparison.OrdinalIgnoreCase),
                "#F6C35B");
            buttons[1] = new EntityCommandPanelToolbarButtonView(
                ChampionSkillSandboxIds.IndicatorModeId,
                "Indicator",
                string.Equals(activeModeId, ChampionSkillSandboxIds.IndicatorModeId, StringComparison.OrdinalIgnoreCase),
                "#61C3FF");
            buttons[2] = new EntityCommandPanelToolbarButtonView(
                ChampionSkillSandboxIds.PressReleaseModeId,
                "RTS",
                string.Equals(activeModeId, ChampionSkillSandboxIds.PressReleaseModeId, StringComparison.OrdinalIgnoreCase),
                "#93E07A");
            buttons[3] = new EntityCommandPanelToolbarButtonView(
                ChampionSkillSandboxIds.FreeCameraToolbarButtonId,
                "Free",
                string.Equals(activeFollowModeId, ChampionSkillSandboxIds.FreeCameraToolbarButtonId, StringComparison.OrdinalIgnoreCase),
                "#D7D2C4");
            buttons[4] = new EntityCommandPanelToolbarButtonView(
                ChampionSkillSandboxIds.FollowSelectionToolbarButtonId,
                "Follow",
                string.Equals(activeFollowModeId, ChampionSkillSandboxIds.FollowSelectionToolbarButtonId, StringComparison.OrdinalIgnoreCase),
                "#8ED9A9");
            buttons[5] = new EntityCommandPanelToolbarButtonView(
                ChampionSkillSandboxIds.FollowSelectionGroupToolbarButtonId,
                "Group",
                string.Equals(activeFollowModeId, ChampionSkillSandboxIds.FollowSelectionGroupToolbarButtonId, StringComparison.OrdinalIgnoreCase),
                "#F0C35A");
            buttons[6] = new EntityCommandPanelToolbarButtonView(
                ChampionSkillSandboxIds.ResetCameraToolbarButtonId,
                "Reset",
                false,
                "#D7D2C4");
            int count = Math.Min(destination.Length, buttons.Length);
            buttons[..count].CopyTo(destination);
            return count;
        }

        public void Activate(string buttonId)
        {
            if (string.IsNullOrWhiteSpace(buttonId))
            {
                return;
            }

            if (string.Equals(buttonId, ChampionSkillSandboxIds.ResetCameraToolbarButtonId, StringComparison.OrdinalIgnoreCase))
            {
                if (_engine != null)
                {
                    _engine.GlobalContext[ChampionSkillSandboxIds.ResetCameraRequestKey] = true;
                }

                return;
            }

            if (ChampionSkillSandboxIds.IsCameraFollowMode(buttonId))
            {
                if (_engine != null)
                {
                    _engine.GlobalContext[ChampionSkillSandboxIds.CameraFollowModeKey] = buttonId;
                }

                return;
            }

            ResolveViewModeManager()?.SwitchTo(buttonId);
        }

        private string ResolveActiveCameraFollowMode()
        {
            if (_engine?.GlobalContext.TryGetValue(ChampionSkillSandboxIds.CameraFollowModeKey, out var modeObj) == true &&
                modeObj is string modeId &&
                ChampionSkillSandboxIds.IsCameraFollowMode(modeId))
            {
                return modeId;
            }

            return ChampionSkillSandboxIds.FreeCameraToolbarButtonId;
        }

        private ViewModeManager? ResolveViewModeManager()
        {
            if (_engine?.GlobalContext.TryGetValue(ViewModeManager.GlobalKey, out var managerObj) == true &&
                managerObj is ViewModeManager manager)
            {
                return manager;
            }

            return null;
        }
    }
}
