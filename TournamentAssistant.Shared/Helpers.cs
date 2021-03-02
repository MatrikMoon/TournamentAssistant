using Google.Protobuf.Reflection;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Discord;
using TournamentAssistantShared.Models.Packets;

namespace TournamentAssistant.Shared
{
    public class Helpers
    {
        public static readonly TypeRegistry TypeRegistry = TypeRegistry.FromMessages(
                    Acknowledgement.Descriptor,
                    Command.Descriptor,
                    Connect.Descriptor,
                    ConnectResponse.Descriptor,
                    Event.Descriptor,
                    File.Descriptor,
                    ForwardingPacket.Descriptor,
                    LoadedSong.Descriptor,
                    LoadSong.Descriptor,
                    PlaySong.Descriptor,
                    Response.Descriptor,
                    ScoreRequest.Descriptor,
                    ScoreRequestResponse.Descriptor,
                    SongFinished.Descriptor,
                    SongList.Descriptor,
                    SubmitScore.Descriptor,
                    Channel.Descriptor,
                    Guild.Descriptor,
                    Beatmap.Descriptor,
                    Characteristic.Descriptor,
                    Coordinator.Descriptor,
                    CoreServer.Descriptor,
                    GameplayModifiers.Descriptor,
                    GameplayParameters.Descriptor,
                    Match.Descriptor,
                    Player.Descriptor,
                    PlayerSpecificSettings.Descriptor,
                    PreviewBeatmapLevel.Descriptor,
                    QualifierEvent.Descriptor,
                    Score.Descriptor,
                    ServerSettings.Descriptor,
                    State.Descriptor,
                    Team.Descriptor,
                    User.Descriptor
                    );
    }
}