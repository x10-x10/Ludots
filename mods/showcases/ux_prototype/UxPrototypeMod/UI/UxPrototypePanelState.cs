using System.Collections.Generic;

namespace UxPrototypeMod.UI;

internal sealed record UxPrototypePanelState(
    string ActiveModeId,
    string ActiveModeLabel,
    string Objective,
    List<string> EventQueue,
    int Food,
    int Iron,
    int Credits,
    int Energy,
    int FoodDelta,
    int IronDelta,
    int CreditsDelta,
    int EnergyDelta,
    bool ResourceDropdownOpen,
    List<ResourceBreakdownEntry> ResourceBreakdown,
    int Year,
    int Month,
    int Day,
    float DayProgress,
    string EraLabel,
    string GanzhiLabel,
    string MoonPhaseLabel,
    string TimeSummary,
    string RosterTab,
    bool RosterCollapsed,
    bool GridLayout,
    string RosterSummary,
    List<RosterRow> RosterRows,
    string FactionTab,
    List<FactionOperationEntry> FactionOperations,
    string GlobalTab,
    bool GlobalExpanded,
    List<GlobalEntry> GlobalEntries,
    string SelectedEntityLabel,
    string SelectedEntityType,
    string SelectedSummary,
    List<SkillEntry> SelectedSkills,
    ProductionQueueSnapshot SelectedQueue,
    List<ProductionRow> ProductionRows,
    string RallySummary,
    string? ActiveBuildTemplateId,
    List<BuildEntry> BuildEntries,
    bool PreferencesOpen,
    bool SettingsOpen,
    bool Paused,
    int SpeedIndex,
    bool SmartCastBatch,
    bool QuickCastOnRelease,
    bool ShowTargetLines,
    bool ShowRangeIndicators,
    bool ShowTooltips,
    bool RoadSnapping,
    bool ObstacleFill,
    bool NavmeshVisible,
    bool NavmeshBakeRunning,
    float NavmeshBakeProgress,
    int RoadNodeCount,
    int RoadSegmentCount,
    int ObstacleCount,
    string TooltipTitle,
    string TooltipBody,
    List<MinimapDot> MinimapDots,
    string MinimapSelectedLabel);

internal sealed record ResourceBreakdownEntry(string Label, int Count, string Summary);
internal sealed record RosterRow(string Label, int Count, string Summary);
internal sealed record FactionOperationEntry(string Label, string Summary);
internal sealed record GlobalEntry(string ActionId, string Label, string Summary, bool Active);
internal sealed record SkillEntry(string ActionId, string Hotkey, string Label, string Summary, bool Enabled, bool Active, string CountText);
internal sealed record ProductionQueueEntry(string QueueToken, string Label, int ProgressPercent, bool CanCancel);
internal sealed record ProductionQueueSnapshot(string Header, List<ProductionQueueEntry> Entries);
internal sealed record ProductionRow(string Label, string Summary);
internal sealed record BuildEntry(string ActionId, string Label, string Summary, bool Active);
internal sealed record MinimapDot(string Label, float X, float Y, string Color, bool Selected);
