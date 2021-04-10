using System;

/**
 * Created by Moon on 9/5/2020, 3:25AM
 * Represents all the information needed to start a gameplay scene
 * with the desired song and settings
 */

namespace TournamentAssistantShared.Models
{
    [Serializable]
    public class GameplayParameters
    {
        public Beatmap Beatmap { get; set; }

        public PlayerSpecificSettings PlayerSettings { get; set; }
        public GameplayModifiers GameplayModifiers { get; set; }
    }
}
