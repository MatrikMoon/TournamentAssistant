// <auto-generated>
//     Generated by the protocol buffer compiler.  DO NOT EDIT!
//     source: protobuf/Models/match.proto
// </auto-generated>
#pragma warning disable 1591, 0612, 3021
#region Designer generated code

using pb = global::Google.Protobuf;
using pbc = global::Google.Protobuf.Collections;
using pbr = global::Google.Protobuf.Reflection;
using scg = global::System.Collections.Generic;
namespace TournamentAssistantShared.Models {

  /// <summary>Holder for reflection information generated from protobuf/Models/match.proto</summary>
  public static partial class MatchReflection {

    #region Descriptor
    /// <summary>File descriptor for protobuf/Models/match.proto</summary>
    public static pbr::FileDescriptor Descriptor {
      get { return descriptor; }
    }
    private static pbr::FileDescriptor descriptor;

    static MatchReflection() {
      byte[] descriptorData = global::System.Convert.FromBase64String(
          string.Concat(
            "Chtwcm90b2J1Zi9Nb2RlbHMvbWF0Y2gucHJvdG8SIFRvdXJuYW1lbnRBc3Np",
            "c3RhbnRTaGFyZWQuTW9kZWxzGhxwcm90b2J1Zi9Nb2RlbHMvcGxheWVyLnBy",
            "b3RvGhpwcm90b2J1Zi9Nb2RlbHMvdXNlci5wcm90bxorcHJvdG9idWYvTW9k",
            "ZWxzL3ByZXZpZXdfYmVhdG1hcF9sZXZlbC5wcm90bxokcHJvdG9idWYvTW9k",
            "ZWxzL2NoYXJhY3RlcmlzdGljLnByb3RvGihwcm90b2J1Zi9Nb2RlbHMvYmVh",
            "dG1hcF9kaWZmaWN1bHR5LnByb3RvIvwCCgVNYXRjaBIMCgRndWlkGAEgASgJ",
            "EjkKB3BsYXllcnMYAiADKAsyKC5Ub3VybmFtZW50QXNzaXN0YW50U2hhcmVk",
            "Lk1vZGVscy5QbGF5ZXISNgoGbGVhZGVyGAMgASgLMiYuVG91cm5hbWVudEFz",
            "c2lzdGFudFNoYXJlZC5Nb2RlbHMuVXNlchJNCg5zZWxlY3RlZF9sZXZlbBgE",
            "IAEoCzI1LlRvdXJuYW1lbnRBc3Npc3RhbnRTaGFyZWQuTW9kZWxzLlByZXZp",
            "ZXdCZWF0bWFwTGV2ZWwSUQoXc2VsZWN0ZWRfY2hhcmFjdGVyaXN0aWMYBSAB",
            "KAsyMC5Ub3VybmFtZW50QXNzaXN0YW50U2hhcmVkLk1vZGVscy5DaGFyYWN0",
            "ZXJpc3RpYxJQChNzZWxlY3RlZF9kaWZmaWN1bHR5GAYgASgOMjMuVG91cm5h",
            "bWVudEFzc2lzdGFudFNoYXJlZC5Nb2RlbHMuQmVhdG1hcERpZmZpY3VsdHlC",
            "I6oCIFRvdXJuYW1lbnRBc3Npc3RhbnRTaGFyZWQuTW9kZWxzYgZwcm90bzM="));
      descriptor = pbr::FileDescriptor.FromGeneratedCode(descriptorData,
          new pbr::FileDescriptor[] { global::TournamentAssistantShared.Models.PlayerReflection.Descriptor, global::TournamentAssistantShared.Models.UserReflection.Descriptor, global::TournamentAssistantShared.Models.PreviewBeatmapLevelReflection.Descriptor, global::TournamentAssistantShared.Models.CharacteristicReflection.Descriptor, global::TournamentAssistantShared.SharedConstructs.BeatmapDifficultyReflection.Descriptor, },
          new pbr::GeneratedClrTypeInfo(null, null, new pbr::GeneratedClrTypeInfo[] {
            new pbr::GeneratedClrTypeInfo(typeof(global::TournamentAssistantShared.Models.Match), global::TournamentAssistantShared.Models.Match.Parser, new[]{ "Guid", "Players", "Leader", "SelectedLevel", "SelectedCharacteristic", "SelectedDifficulty" }, null, null, null, null)
          }));
    }
    #endregion

  }
  #region Messages
  public sealed partial class Match : pb::IMessage<Match>
  #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      , pb::IBufferMessage
  #endif
  {
    private static readonly pb::MessageParser<Match> _parser = new pb::MessageParser<Match>(() => new Match());
    private pb::UnknownFieldSet _unknownFields;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public static pb::MessageParser<Match> Parser { get { return _parser; } }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public static pbr::MessageDescriptor Descriptor {
      get { return global::TournamentAssistantShared.Models.MatchReflection.Descriptor.MessageTypes[0]; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    pbr::MessageDescriptor pb::IMessage.Descriptor {
      get { return Descriptor; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public Match() {
      OnConstruction();
    }

    partial void OnConstruction();

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public Match(Match other) : this() {
      guid_ = other.guid_;
      players_ = other.players_.Clone();
      leader_ = other.leader_ != null ? other.leader_.Clone() : null;
      selectedLevel_ = other.selectedLevel_ != null ? other.selectedLevel_.Clone() : null;
      selectedCharacteristic_ = other.selectedCharacteristic_ != null ? other.selectedCharacteristic_.Clone() : null;
      selectedDifficulty_ = other.selectedDifficulty_;
      _unknownFields = pb::UnknownFieldSet.Clone(other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public Match Clone() {
      return new Match(this);
    }

    /// <summary>Field number for the "guid" field.</summary>
    public const int GuidFieldNumber = 1;
    private string guid_ = "";
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public string Guid {
      get { return guid_; }
      set {
        guid_ = pb::ProtoPreconditions.CheckNotNull(value, "value");
      }
    }

    /// <summary>Field number for the "players" field.</summary>
    public const int PlayersFieldNumber = 2;
    private static readonly pb::FieldCodec<global::TournamentAssistantShared.Models.Player> _repeated_players_codec
        = pb::FieldCodec.ForMessage(18, global::TournamentAssistantShared.Models.Player.Parser);
    private readonly pbc::RepeatedField<global::TournamentAssistantShared.Models.Player> players_ = new pbc::RepeatedField<global::TournamentAssistantShared.Models.Player>();
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public pbc::RepeatedField<global::TournamentAssistantShared.Models.Player> Players {
      get { return players_; }
    }

    /// <summary>Field number for the "leader" field.</summary>
    public const int LeaderFieldNumber = 3;
    private global::TournamentAssistantShared.Models.User leader_;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public global::TournamentAssistantShared.Models.User Leader {
      get { return leader_; }
      set {
        leader_ = value;
      }
    }

    /// <summary>Field number for the "selected_level" field.</summary>
    public const int SelectedLevelFieldNumber = 4;
    private global::TournamentAssistantShared.Models.PreviewBeatmapLevel selectedLevel_;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public global::TournamentAssistantShared.Models.PreviewBeatmapLevel SelectedLevel {
      get { return selectedLevel_; }
      set {
        selectedLevel_ = value;
      }
    }

    /// <summary>Field number for the "selected_characteristic" field.</summary>
    public const int SelectedCharacteristicFieldNumber = 5;
    private global::TournamentAssistantShared.Models.Characteristic selectedCharacteristic_;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public global::TournamentAssistantShared.Models.Characteristic SelectedCharacteristic {
      get { return selectedCharacteristic_; }
      set {
        selectedCharacteristic_ = value;
      }
    }

    /// <summary>Field number for the "selected_difficulty" field.</summary>
    public const int SelectedDifficultyFieldNumber = 6;
    private global::TournamentAssistantShared.SharedConstructs.BeatmapDifficulty selectedDifficulty_ = global::TournamentAssistantShared.SharedConstructs.BeatmapDifficulty.Easy;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public global::TournamentAssistantShared.SharedConstructs.BeatmapDifficulty SelectedDifficulty {
      get { return selectedDifficulty_; }
      set {
        selectedDifficulty_ = value;
      }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public override bool Equals(object other) {
      return Equals(other as Match);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public bool Equals(Match other) {
      if (ReferenceEquals(other, null)) {
        return false;
      }
      if (ReferenceEquals(other, this)) {
        return true;
      }
      if (Guid != other.Guid) return false;
      if(!players_.Equals(other.players_)) return false;
      if (!object.Equals(Leader, other.Leader)) return false;
      if (!object.Equals(SelectedLevel, other.SelectedLevel)) return false;
      if (!object.Equals(SelectedCharacteristic, other.SelectedCharacteristic)) return false;
      if (SelectedDifficulty != other.SelectedDifficulty) return false;
      return Equals(_unknownFields, other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public override int GetHashCode() {
      int hash = 1;
      if (Guid.Length != 0) hash ^= Guid.GetHashCode();
      hash ^= players_.GetHashCode();
      if (leader_ != null) hash ^= Leader.GetHashCode();
      if (selectedLevel_ != null) hash ^= SelectedLevel.GetHashCode();
      if (selectedCharacteristic_ != null) hash ^= SelectedCharacteristic.GetHashCode();
      if (SelectedDifficulty != global::TournamentAssistantShared.SharedConstructs.BeatmapDifficulty.Easy) hash ^= SelectedDifficulty.GetHashCode();
      if (_unknownFields != null) {
        hash ^= _unknownFields.GetHashCode();
      }
      return hash;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public override string ToString() {
      return pb::JsonFormatter.ToDiagnosticString(this);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public void WriteTo(pb::CodedOutputStream output) {
    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      output.WriteRawMessage(this);
    #else
      if (Guid.Length != 0) {
        output.WriteRawTag(10);
        output.WriteString(Guid);
      }
      players_.WriteTo(output, _repeated_players_codec);
      if (leader_ != null) {
        output.WriteRawTag(26);
        output.WriteMessage(Leader);
      }
      if (selectedLevel_ != null) {
        output.WriteRawTag(34);
        output.WriteMessage(SelectedLevel);
      }
      if (selectedCharacteristic_ != null) {
        output.WriteRawTag(42);
        output.WriteMessage(SelectedCharacteristic);
      }
      if (SelectedDifficulty != global::TournamentAssistantShared.SharedConstructs.BeatmapDifficulty.Easy) {
        output.WriteRawTag(48);
        output.WriteEnum((int) SelectedDifficulty);
      }
      if (_unknownFields != null) {
        _unknownFields.WriteTo(output);
      }
    #endif
    }

    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    void pb::IBufferMessage.InternalWriteTo(ref pb::WriteContext output) {
      if (Guid.Length != 0) {
        output.WriteRawTag(10);
        output.WriteString(Guid);
      }
      players_.WriteTo(ref output, _repeated_players_codec);
      if (leader_ != null) {
        output.WriteRawTag(26);
        output.WriteMessage(Leader);
      }
      if (selectedLevel_ != null) {
        output.WriteRawTag(34);
        output.WriteMessage(SelectedLevel);
      }
      if (selectedCharacteristic_ != null) {
        output.WriteRawTag(42);
        output.WriteMessage(SelectedCharacteristic);
      }
      if (SelectedDifficulty != global::TournamentAssistantShared.SharedConstructs.BeatmapDifficulty.Easy) {
        output.WriteRawTag(48);
        output.WriteEnum((int) SelectedDifficulty);
      }
      if (_unknownFields != null) {
        _unknownFields.WriteTo(ref output);
      }
    }
    #endif

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public int CalculateSize() {
      int size = 0;
      if (Guid.Length != 0) {
        size += 1 + pb::CodedOutputStream.ComputeStringSize(Guid);
      }
      size += players_.CalculateSize(_repeated_players_codec);
      if (leader_ != null) {
        size += 1 + pb::CodedOutputStream.ComputeMessageSize(Leader);
      }
      if (selectedLevel_ != null) {
        size += 1 + pb::CodedOutputStream.ComputeMessageSize(SelectedLevel);
      }
      if (selectedCharacteristic_ != null) {
        size += 1 + pb::CodedOutputStream.ComputeMessageSize(SelectedCharacteristic);
      }
      if (SelectedDifficulty != global::TournamentAssistantShared.SharedConstructs.BeatmapDifficulty.Easy) {
        size += 1 + pb::CodedOutputStream.ComputeEnumSize((int) SelectedDifficulty);
      }
      if (_unknownFields != null) {
        size += _unknownFields.CalculateSize();
      }
      return size;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public void MergeFrom(Match other) {
      if (other == null) {
        return;
      }
      if (other.Guid.Length != 0) {
        Guid = other.Guid;
      }
      players_.Add(other.players_);
      if (other.leader_ != null) {
        if (leader_ == null) {
          Leader = new global::TournamentAssistantShared.Models.User();
        }
        Leader.MergeFrom(other.Leader);
      }
      if (other.selectedLevel_ != null) {
        if (selectedLevel_ == null) {
          SelectedLevel = new global::TournamentAssistantShared.Models.PreviewBeatmapLevel();
        }
        SelectedLevel.MergeFrom(other.SelectedLevel);
      }
      if (other.selectedCharacteristic_ != null) {
        if (selectedCharacteristic_ == null) {
          SelectedCharacteristic = new global::TournamentAssistantShared.Models.Characteristic();
        }
        SelectedCharacteristic.MergeFrom(other.SelectedCharacteristic);
      }
      if (other.SelectedDifficulty != global::TournamentAssistantShared.SharedConstructs.BeatmapDifficulty.Easy) {
        SelectedDifficulty = other.SelectedDifficulty;
      }
      _unknownFields = pb::UnknownFieldSet.MergeFrom(_unknownFields, other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public void MergeFrom(pb::CodedInputStream input) {
    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      input.ReadRawMessage(this);
    #else
      uint tag;
      while ((tag = input.ReadTag()) != 0) {
        switch(tag) {
          default:
            _unknownFields = pb::UnknownFieldSet.MergeFieldFrom(_unknownFields, input);
            break;
          case 10: {
            Guid = input.ReadString();
            break;
          }
          case 18: {
            players_.AddEntriesFrom(input, _repeated_players_codec);
            break;
          }
          case 26: {
            if (leader_ == null) {
              Leader = new global::TournamentAssistantShared.Models.User();
            }
            input.ReadMessage(Leader);
            break;
          }
          case 34: {
            if (selectedLevel_ == null) {
              SelectedLevel = new global::TournamentAssistantShared.Models.PreviewBeatmapLevel();
            }
            input.ReadMessage(SelectedLevel);
            break;
          }
          case 42: {
            if (selectedCharacteristic_ == null) {
              SelectedCharacteristic = new global::TournamentAssistantShared.Models.Characteristic();
            }
            input.ReadMessage(SelectedCharacteristic);
            break;
          }
          case 48: {
            SelectedDifficulty = (global::TournamentAssistantShared.SharedConstructs.BeatmapDifficulty) input.ReadEnum();
            break;
          }
        }
      }
    #endif
    }

    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    void pb::IBufferMessage.InternalMergeFrom(ref pb::ParseContext input) {
      uint tag;
      while ((tag = input.ReadTag()) != 0) {
        switch(tag) {
          default:
            _unknownFields = pb::UnknownFieldSet.MergeFieldFrom(_unknownFields, ref input);
            break;
          case 10: {
            Guid = input.ReadString();
            break;
          }
          case 18: {
            players_.AddEntriesFrom(ref input, _repeated_players_codec);
            break;
          }
          case 26: {
            if (leader_ == null) {
              Leader = new global::TournamentAssistantShared.Models.User();
            }
            input.ReadMessage(Leader);
            break;
          }
          case 34: {
            if (selectedLevel_ == null) {
              SelectedLevel = new global::TournamentAssistantShared.Models.PreviewBeatmapLevel();
            }
            input.ReadMessage(SelectedLevel);
            break;
          }
          case 42: {
            if (selectedCharacteristic_ == null) {
              SelectedCharacteristic = new global::TournamentAssistantShared.Models.Characteristic();
            }
            input.ReadMessage(SelectedCharacteristic);
            break;
          }
          case 48: {
            SelectedDifficulty = (global::TournamentAssistantShared.SharedConstructs.BeatmapDifficulty) input.ReadEnum();
            break;
          }
        }
      }
    }
    #endif

  }

  #endregion

}

#endregion Designer generated code
