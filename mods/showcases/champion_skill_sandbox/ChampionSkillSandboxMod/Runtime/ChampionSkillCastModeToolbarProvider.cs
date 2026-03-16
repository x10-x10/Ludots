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
                if (!string.IsNullOrWhiteSpace(activeModeId))
                {
                    revision ^= (uint)activeModeId.GetHashCode(StringComparison.Ordinal);
                }

                return revision;
            }
        }

        public string Title => "Cast Mode";
        public string Subtitle => "F1/F2/F3 Cast | F4 Reset | RMB Move";

        public void Bind(GameEngine engine)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        }

        public int CopyButtons(Span<EntityCommandPanelToolbarButtonView> destination)
        {
            if (!IsVisible || destination.Length < 4)
            {
                return 0;
            }

            string? activeModeId = ResolveViewModeManager()?.ActiveMode?.Id;
            destination[0] = new EntityCommandPanelToolbarButtonView(
                ChampionSkillSandboxIds.SmartCastModeId,
                "Quick",
                string.Equals(activeModeId, ChampionSkillSandboxIds.SmartCastModeId, StringComparison.OrdinalIgnoreCase),
                "#F6C35B");
            destination[1] = new EntityCommandPanelToolbarButtonView(
                ChampionSkillSandboxIds.IndicatorModeId,
                "Indicator",
                string.Equals(activeModeId, ChampionSkillSandboxIds.IndicatorModeId, StringComparison.OrdinalIgnoreCase),
                "#61C3FF");
            destination[2] = new EntityCommandPanelToolbarButtonView(
                ChampionSkillSandboxIds.PressReleaseModeId,
                "RTS",
                string.Equals(activeModeId, ChampionSkillSandboxIds.PressReleaseModeId, StringComparison.OrdinalIgnoreCase),
                "#93E07A");
            destination[3] = new EntityCommandPanelToolbarButtonView(
                ChampionSkillSandboxIds.ResetCameraToolbarButtonId,
                "Reset",
                false,
                "#D7D2C4");
            return 4;
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

            ResolveViewModeManager()?.SwitchTo(buttonId);
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
