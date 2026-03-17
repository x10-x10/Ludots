using System;
using Arch.Core;
using CoreInputMod.ViewMode;
using Ludots.Core.Engine;
using Ludots.Core.Presentation.Hud;
using Ludots.Core.Scripting;
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

                if (ChampionSkillSandboxIds.IsStressMap(_engine?.CurrentMapSession?.MapId.Value))
                {
                    ChampionSkillStressControlState? control = ResolveStressControl();
                    ChampionSkillStressTelemetry? telemetry = ResolveStressTelemetry();
                    RenderDebugState? renderDebug = ResolveRenderDebugState();
                    revision ^= (uint)(control?.DesiredTeamA ?? 0);
                    revision ^= (uint)((control?.DesiredTeamB ?? 0) << 5);
                    revision ^= (uint)((telemetry?.LiveTeamA ?? 0) << 10);
                    revision ^= (uint)((telemetry?.LiveTeamB ?? 0) << 15);
                    revision ^= (uint)((telemetry?.ProjectileCount ?? 0) << 20);
                    revision ^= (renderDebug?.DrawWorldHudBars ?? true) ? 1u << 25 : 0u;
                    revision ^= (renderDebug?.DrawWorldHudText ?? true) ? 1u << 26 : 0u;
                    revision ^= (renderDebug?.DrawCombatText ?? true) ? 1u << 27 : 0u;
                }

                return revision;
            }
        }

        public string Title => ChampionSkillSandboxIds.IsStressMap(_engine?.CurrentMapSession?.MapId.Value)
            ? "Stress Harness"
            : "Cast Mode";

        public string Subtitle
        {
            get
            {
                if (!ChampionSkillSandboxIds.IsStressMap(_engine?.CurrentMapSession?.MapId.Value))
                {
                    return "Cast + Camera | F1/F2/F3 Cast | RMB Move";
                }

                ChampionSkillStressControlState? control = ResolveStressControl();
                ChampionSkillStressTelemetry? telemetry = ResolveStressTelemetry();
                RenderDebugState? renderDebug = ResolveRenderDebugState();
                return $"A {telemetry?.LiveTeamA ?? 0}/{control?.DesiredTeamA ?? 0} | B {telemetry?.LiveTeamB ?? 0}/{control?.DesiredTeamB ?? 0} | Proj {telemetry?.ProjectileCount ?? 0} peak {telemetry?.PeakProjectileCount ?? 0} | HUD {(renderDebug?.DrawWorldHudBars ?? true ? "B" : "-")}{(renderDebug?.DrawWorldHudText ?? true ? "T" : "-")}{(renderDebug?.DrawCombatText ?? true ? "F" : "-")}";
            }
        }

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

            bool isStressMap = ChampionSkillSandboxIds.IsStressMap(_engine?.CurrentMapSession?.MapId.Value);
            string? activeModeId = ResolveViewModeManager()?.ActiveMode?.Id;
            string activeFollowModeId = ResolveActiveCameraFollowMode();
            RenderDebugState? renderDebug = ResolveRenderDebugState();
            var buttons = new EntityCommandPanelToolbarButtonView[isStressMap ? 14 : 7];
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
            if (isStressMap)
            {
                buttons[7] = new EntityCommandPanelToolbarButtonView(
                    ChampionSkillSandboxIds.StressTeamADecreaseToolbarButtonId,
                    "A-",
                    false,
                    "#FF9B7A");
                buttons[8] = new EntityCommandPanelToolbarButtonView(
                    ChampionSkillSandboxIds.StressTeamAIncreaseToolbarButtonId,
                    "A+",
                    false,
                    "#FF9B7A");
                buttons[9] = new EntityCommandPanelToolbarButtonView(
                    ChampionSkillSandboxIds.StressTeamBDecreaseToolbarButtonId,
                    "B-",
                    false,
                    "#67D4FF");
                buttons[10] = new EntityCommandPanelToolbarButtonView(
                    ChampionSkillSandboxIds.StressTeamBIncreaseToolbarButtonId,
                    "B+",
                    false,
                    "#67D4FF");
                buttons[11] = new EntityCommandPanelToolbarButtonView(
                    ChampionSkillSandboxIds.StressHudBarToggleToolbarButtonId,
                    "Bar",
                    renderDebug?.DrawWorldHudBars ?? true,
                    "#E2D7A6");
                buttons[12] = new EntityCommandPanelToolbarButtonView(
                    ChampionSkillSandboxIds.StressHudTextToggleToolbarButtonId,
                    "Text",
                    renderDebug?.DrawWorldHudText ?? true,
                    "#F2E3B3");
                buttons[13] = new EntityCommandPanelToolbarButtonView(
                    ChampionSkillSandboxIds.StressCombatTextToggleToolbarButtonId,
                    "Float",
                    renderDebug?.DrawCombatText ?? true,
                    "#FFCF86");
            }

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

            ChampionSkillStressControlState? control = ResolveStressControl();
            RenderDebugState? renderDebug = ResolveRenderDebugState();
            if (control != null)
            {
                if (string.Equals(buttonId, ChampionSkillSandboxIds.StressTeamADecreaseToolbarButtonId, StringComparison.OrdinalIgnoreCase))
                {
                    control.AdjustTeamA(-ChampionSkillStressControlState.Step);
                    return;
                }

                if (string.Equals(buttonId, ChampionSkillSandboxIds.StressTeamAIncreaseToolbarButtonId, StringComparison.OrdinalIgnoreCase))
                {
                    control.AdjustTeamA(ChampionSkillStressControlState.Step);
                    return;
                }

                if (string.Equals(buttonId, ChampionSkillSandboxIds.StressTeamBDecreaseToolbarButtonId, StringComparison.OrdinalIgnoreCase))
                {
                    control.AdjustTeamB(-ChampionSkillStressControlState.Step);
                    return;
                }

                if (string.Equals(buttonId, ChampionSkillSandboxIds.StressTeamBIncreaseToolbarButtonId, StringComparison.OrdinalIgnoreCase))
                {
                    control.AdjustTeamB(ChampionSkillStressControlState.Step);
                    return;
                }

                if (string.Equals(buttonId, ChampionSkillSandboxIds.StressHudBarToggleToolbarButtonId, StringComparison.OrdinalIgnoreCase))
                {
                    if (renderDebug != null)
                    {
                        renderDebug.DrawWorldHudBars = !renderDebug.DrawWorldHudBars;
                    }

                    return;
                }

                if (string.Equals(buttonId, ChampionSkillSandboxIds.StressHudTextToggleToolbarButtonId, StringComparison.OrdinalIgnoreCase))
                {
                    if (renderDebug != null)
                    {
                        renderDebug.DrawWorldHudText = !renderDebug.DrawWorldHudText;
                    }

                    return;
                }

                if (string.Equals(buttonId, ChampionSkillSandboxIds.StressCombatTextToggleToolbarButtonId, StringComparison.OrdinalIgnoreCase))
                {
                    if (renderDebug != null)
                    {
                        renderDebug.DrawCombatText = !renderDebug.DrawCombatText;
                    }

                    return;
                }
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

        private ChampionSkillStressControlState? ResolveStressControl()
        {
            return _engine?.GlobalContext.TryGetValue(ChampionSkillStressControlState.GlobalKey, out var value) == true &&
                   value is ChampionSkillStressControlState control
                ? control
                : null;
        }

        private ChampionSkillStressTelemetry? ResolveStressTelemetry()
        {
            return _engine?.GlobalContext.TryGetValue(ChampionSkillStressTelemetry.GlobalKey, out var value) == true &&
                   value is ChampionSkillStressTelemetry telemetry
                ? telemetry
                : null;
        }

        private RenderDebugState? ResolveRenderDebugState()
        {
            return _engine?.GetService(CoreServiceKeys.RenderDebugState);
        }
    }
}
