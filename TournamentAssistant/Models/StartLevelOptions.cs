namespace TournamentAssistant.Models
{
    public class StartLevelOptions
    {
        public IPreviewBeatmapLevel Level { get; }
        public BeatmapCharacteristicSO Characteristic { get; }
        public BeatmapDifficulty Difficulty { get; }
        public GameplayModifiers Modifiers { get; }
        public PlayerSpecificSettings Player { get; }
        public OverrideEnvironmentSettings Environment { get; }
        public ColorScheme? Colors { get; }

        public StartLevelOptions(IPreviewBeatmapLevel level, BeatmapCharacteristicSO characteristic, BeatmapDifficulty difficulty, GameplayModifiers modifiers, PlayerSpecificSettings player, OverrideEnvironmentSettings environment, ColorScheme? colors)
        {
            Level = level;
            Characteristic = characteristic;
            Difficulty = difficulty;
            Modifiers = modifiers;
            Player = player;
            Environment = environment;
            Colors = colors;
        }
    }
}