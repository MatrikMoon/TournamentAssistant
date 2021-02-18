protoc -I="." --csharp_out="./Models/Packets" `
"./protobuf/Models/Packets/acknowledgement.proto" `
"./protobuf/Models/Packets/command.proto" `
"./protobuf/Models/Packets/connect.proto" `
"./protobuf/Models/Packets/connect_response.proto" `
"./protobuf/Models/Packets/event.proto" `
"./protobuf/Models/Packets/file.proto" `
"./protobuf/Models/Packets/forwarding_packet.proto" `
"./protobuf/Models/Packets/load_song.proto" `
"./protobuf/Models/Packets/loaded_song.proto" `
"./protobuf/Models/Packets/play_song.proto" `
"./protobuf/Models/Packets/response.proto" `
"./protobuf/Models/Packets/score_request.proto" `
"./protobuf/Models/Packets/score_request_response.proto" `
"./protobuf/Models/Packets/song_finished.proto" `
"./protobuf/Models/Packets/song_list.proto" `
"./protobuf/Models/Packets/submit_score.proto"

protoc -I="." --csharp_out="./Models/Discord" `
"./protobuf/Models/Discord/channel.proto" `
"./protobuf/Models/Discord/guild.proto"

protoc -I="." --csharp_out="./Models" `
"./protobuf/Models/beatmap.proto" `
"./protobuf/Models/beatmap_difficulty.proto" `
"./protobuf/Models/characteristic.proto" `
"./protobuf/Models/coordinator.proto" `
"./protobuf/Models/core_server.proto" `
"./protobuf/Models/gameplay_modifiers.proto" `
"./protobuf/Models/gameplay_parameters.proto" `
"./protobuf/Models/match.proto" `
"./protobuf/Models/packet_type.proto" `
"./protobuf/Models/player.proto" `
"./protobuf/Models/player_specific_settings.proto" `
"./protobuf/Models/preview_beatmap_level.proto" `
"./protobuf/Models/qualifier_event.proto" `
"./protobuf/Models/score.proto" `
"./protobuf/Models/server_settings.proto" `
"./protobuf/Models/state.proto" `
"./protobuf/Models/team.proto" `
"./protobuf/Models/user.proto"
