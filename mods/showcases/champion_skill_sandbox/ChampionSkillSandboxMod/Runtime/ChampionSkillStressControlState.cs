namespace ChampionSkillSandboxMod.Runtime
{
    internal sealed class ChampionSkillStressControlState
    {
        public const string GlobalKey = "ChampionSkillSandbox.StressControl";
        public const int DefaultTeamCount = 48;
        public const int MinTeamCount = 8;
        public const int MaxTeamCount = 256;
        public const int Step = 8;

        public int DesiredTeamA { get; private set; } = DefaultTeamCount;
        public int DesiredTeamB { get; private set; } = DefaultTeamCount;

        public void AdjustTeamA(int delta)
        {
            DesiredTeamA = Clamp(DesiredTeamA + delta);
        }

        public void AdjustTeamB(int delta)
        {
            DesiredTeamB = Clamp(DesiredTeamB + delta);
        }

        private static int Clamp(int value)
        {
            if (value < MinTeamCount)
            {
                return MinTeamCount;
            }

            if (value > MaxTeamCount)
            {
                return MaxTeamCount;
            }

            return value;
        }
    }
}
