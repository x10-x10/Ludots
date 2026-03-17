namespace ChampionSkillSandboxMod.Runtime
{
    internal sealed class ChampionSkillStressTelemetry
    {
        public const string GlobalKey = "ChampionSkillSandbox.StressTelemetry";

        public bool IsActive { get; set; }
        public int DesiredTeamA { get; set; }
        public int DesiredTeamB { get; set; }
        public int LiveTeamA { get; set; }
        public int LiveTeamB { get; set; }
        public int ProjectileCount { get; set; }
        public int PeakProjectileCount { get; set; }
        public int OrdersIssued { get; set; }
        public int QueueDepth { get; set; }

        public void Reset()
        {
            IsActive = false;
            DesiredTeamA = 0;
            DesiredTeamB = 0;
            LiveTeamA = 0;
            LiveTeamB = 0;
            ProjectileCount = 0;
            PeakProjectileCount = 0;
            OrdersIssued = 0;
            QueueDepth = 0;
        }
    }
}
