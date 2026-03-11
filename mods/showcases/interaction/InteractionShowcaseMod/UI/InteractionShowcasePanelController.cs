using System;
using System.Collections.Generic;
using Arch.Core;
using CoreInputMod.ViewMode;
using Ludots.Core.Components;
using Ludots.Core.Engine;
using Ludots.Core.Gameplay.Components;
using Ludots.Core.Scripting;
using Ludots.UI;
using Ludots.UI.Compose;
using Ludots.UI.Runtime;
using Ludots.UI.Runtime.Actions;

namespace InteractionShowcaseMod.UI
{
    internal sealed class InteractionShowcasePanelController
    {
        private UiScene? _mountedScene;

        public UiScene BuildScene(GameEngine engine, string mapId, ViewModeManager? viewModeManager)
        {
            var scene = new UiScene();
            int nextId = 1;
            scene.Mount(BuildRoot(engine, mapId, viewModeManager).Build(scene.Dispatcher, ref nextId));
            _mountedScene = scene;
            return scene;
        }

        public void ClearIfOwned(UIRoot root)
        {
            if (ReferenceEquals(root.Scene, _mountedScene))
            {
                root.ClearScene();
            }

            _mountedScene = null;
        }

        private UiElementBuilder BuildRoot(GameEngine engine, string mapId, ViewModeManager? viewModeManager)
        {
            string activeMode = viewModeManager?.ActiveMode?.DisplayName ?? "Unassigned";
            string selectedLabel = ResolveSelectedLabel(engine);
            string roster = ResolveRoster(engine.World);

            return Ui.Card(
                Ui.Text("Interaction Showcase").FontSize(24f).Bold().Color("#F6F8FB"),
                Ui.Text(InteractionShowcaseIds.DescribeMap(mapId)).FontSize(13f).Color("#C7D0DD").WhiteSpace(UiWhiteSpace.Normal),
                Ui.Text($"Map: {mapId}").FontSize(12f).Color("#8D9AAE"),
                Ui.Text($"Mode: {activeMode}").FontSize(12f).Color("#8D9AAE"),
                Ui.Text($"Selected: {selectedLabel}").FontSize(12f).Color("#8D9AAE"),
                Ui.Text("Reference Modes").FontSize(12f).Bold().Color("#F0C36B"),
                Ui.Row(
                    BuildActionButton("WoW", activeMode.Contains("WoW", StringComparison.OrdinalIgnoreCase), _ => viewModeManager?.SwitchTo(InteractionShowcaseIds.WowModeId)),
                    BuildActionButton("LoL", activeMode.Contains("LoL", StringComparison.OrdinalIgnoreCase), _ => viewModeManager?.SwitchTo(InteractionShowcaseIds.LolModeId)),
                    BuildActionButton("SC2", activeMode.Contains("SC2", StringComparison.OrdinalIgnoreCase), _ => viewModeManager?.SwitchTo(InteractionShowcaseIds.Sc2ModeId)),
                    BuildActionButton("Indicator", activeMode.Contains("Indicator", StringComparison.OrdinalIgnoreCase), _ => viewModeManager?.SwitchTo(InteractionShowcaseIds.IndicatorModeId)),
                    BuildActionButton("Action", activeMode.Contains("Action", StringComparison.OrdinalIgnoreCase), _ => viewModeManager?.SwitchTo(InteractionShowcaseIds.ActionModeId))
                ).Wrap().Gap(8f),
                Ui.Text("Maps").FontSize(12f).Bold().Color("#F0C36B"),
                Ui.Row(
                    BuildMapButton("Hub", mapId == InteractionShowcaseIds.HubMapId, _ => LoadShowcaseMap(engine, InteractionShowcaseIds.HubMapId)),
                    BuildMapButton("Stress", mapId == InteractionShowcaseIds.StressMapId, _ => LoadShowcaseMap(engine, InteractionShowcaseIds.StressMapId))
                ).Wrap().Gap(8f),
                Ui.Text("Roster").FontSize(12f).Bold().Color("#F0C36B"),
                Ui.Text(roster).FontSize(12f).Color("#C7D0DD").WhiteSpace(UiWhiteSpace.Normal),
                Ui.Text("Controls").FontSize(12f).Bold().Color("#F0C36B"),
                Ui.Text("LMB select | RMB move/confirm | Shift queue | Tab target | Q/W/E/R/Z/F/Space/X+C abilities | F1-F5 switch reference interaction.")
                    .FontSize(12f)
                    .Color("#8D9AAE")
                    .WhiteSpace(UiWhiteSpace.Normal),
                Ui.Text("Coverage focus: target-first, smart-cast, aim-cast, quick-cast with indicator, context-scored action routing, toggle, queue, double-tap, chord, directional, point, unit, self, and stress throughput.")
                    .FontSize(12f)
                    .Color("#8D9AAE")
                    .WhiteSpace(UiWhiteSpace.Normal)
            ).Width(520f)
             .Padding(16f)
             .Gap(10f)
             .Radius(18f)
             .Background("#0F1724")
             .Absolute(16f, 16f)
             .ZIndex(30);
        }

        private static UiElementBuilder BuildMapButton(string label, bool active, Action<UiActionContext> onClick)
        {
            return Ui.Button(label, onClick)
                .Padding(10f, 8f)
                .Radius(999f)
                .Background(active ? "#23465D" : "#182234")
                .Color(active ? "#F6F8FB" : "#C7D0DD");
        }

        private static UiElementBuilder BuildActionButton(string label, bool active, Action<UiActionContext> onClick)
        {
            return Ui.Button(label, onClick)
                .Padding(10f, 8f)
                .Radius(10f)
                .Background(active ? "#5E4518" : "#121B29")
                .Color("#F6F8FB");
        }

        private static void LoadShowcaseMap(GameEngine engine, string mapId)
        {
            string? currentMapId = engine.CurrentMapSession?.MapId.Value;
            if (string.Equals(currentMapId, mapId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (InteractionShowcaseIds.IsShowcaseMap(currentMapId))
            {
                engine.UnloadMap(currentMapId!);
            }

            engine.LoadMap(mapId);
        }

        private static string ResolveSelectedLabel(GameEngine engine)
        {
            if (!engine.GlobalContext.TryGetValue(CoreServiceKeys.SelectedEntity.Name, out var selectedObj) ||
                selectedObj is not Entity selected ||
                !engine.World.IsAlive(selected))
            {
                return "(none)";
            }

            return engine.World.TryGet(selected, out Name name)
                ? name.Value
                : $"Entity#{selected.Id}";
        }

        private static string ResolveRoster(World world)
        {
            var names = new List<string>(8);
            var query = new QueryDescription().WithAll<Name, PlayerOwner>();
            world.Query(in query, (Entity _, ref Name name, ref PlayerOwner owner) =>
            {
                if (owner.PlayerId == 1 && !string.IsNullOrWhiteSpace(name.Value))
                {
                    names.Add(name.Value);
                }
            });

            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names.Count == 0
                ? "No controllable units loaded."
                : string.Join(" | ", names);
        }
    }
}
