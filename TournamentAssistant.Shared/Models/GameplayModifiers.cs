// <auto-generated>
//     Generated by the protocol buffer compiler.  DO NOT EDIT!
//     source: protobuf/Models/gameplay_modifiers.proto
// </auto-generated>
#pragma warning disable 1591, 0612, 3021
#region Designer generated code

using pb = global::Google.Protobuf;
using pbc = global::Google.Protobuf.Collections;
using pbr = global::Google.Protobuf.Reflection;
using scg = global::System.Collections.Generic;
namespace TournamentAssistantShared.Models {

  /// <summary>Holder for reflection information generated from protobuf/Models/gameplay_modifiers.proto</summary>
  public static partial class GameplayModifiersReflection {

    #region Descriptor
    /// <summary>File descriptor for protobuf/Models/gameplay_modifiers.proto</summary>
    public static pbr::FileDescriptor Descriptor {
      get { return descriptor; }
    }
    private static pbr::FileDescriptor descriptor;

    static GameplayModifiersReflection() {
      byte[] descriptorData = global::System.Convert.FromBase64String(
          string.Concat(
            "Cihwcm90b2J1Zi9Nb2RlbHMvZ2FtZXBsYXlfbW9kaWZpZXJzLnByb3RvEiBU",
            "b3VybmFtZW50QXNzaXN0YW50U2hhcmVkLk1vZGVscyL9AgoRR2FtZXBsYXlN",
            "b2RpZmllcnMSUAoHb3B0aW9ucxgBIAEoDjI/LlRvdXJuYW1lbnRBc3Npc3Rh",
            "bnRTaGFyZWQuTW9kZWxzLkdhbWVwbGF5TW9kaWZpZXJzLkdhbWVPcHRpb25z",
            "IpUCCgtHYW1lT3B0aW9ucxIICgROb25lEAASCgoGTm9GYWlsEAESCwoHTm9C",
            "b21icxACEgwKCE5vQXJyb3dzEAQSDwoLTm9PYnN0YWNsZXMQCBIMCghTbG93",
            "U29uZxAQEg0KCUluc3RhRmFpbBAgEg8KC0ZhaWxPbkNsYXNoEEASEgoNQmF0",
            "dGVyeUVuZXJneRCAARIOCglGYXN0Tm90ZXMQgAISDQoIRmFzdFNvbmcQgAQS",
            "FwoSRGlzYXBwZWFyaW5nQXJyb3dzEIAIEg8KCkdob3N0Tm90ZXMQgBASDwoK",
            "RGVtb05vRmFpbBCAIBIUCg9EZW1vTm9PYnN0YWNsZXMQgEASEgoMU3RyaWN0",
            "QW5nbGVzEICAAUIjqgIgVG91cm5hbWVudEFzc2lzdGFudFNoYXJlZC5Nb2Rl",
            "bHNiBnByb3RvMw=="));
      descriptor = pbr::FileDescriptor.FromGeneratedCode(descriptorData,
          new pbr::FileDescriptor[] { },
          new pbr::GeneratedClrTypeInfo(null, null, new pbr::GeneratedClrTypeInfo[] {
            new pbr::GeneratedClrTypeInfo(typeof(global::TournamentAssistantShared.Models.GameplayModifiers), global::TournamentAssistantShared.Models.GameplayModifiers.Parser, new[]{ "Options" }, null, new[]{ typeof(global::TournamentAssistantShared.Models.GameplayModifiers.Types.GameOptions) }, null, null)
          }));
    }
    #endregion

  }
  #region Messages
  public sealed partial class GameplayModifiers : pb::IMessage<GameplayModifiers>
  #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      , pb::IBufferMessage
  #endif
  {
    private static readonly pb::MessageParser<GameplayModifiers> _parser = new pb::MessageParser<GameplayModifiers>(() => new GameplayModifiers());
    private pb::UnknownFieldSet _unknownFields;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public static pb::MessageParser<GameplayModifiers> Parser { get { return _parser; } }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public static pbr::MessageDescriptor Descriptor {
      get { return global::TournamentAssistantShared.Models.GameplayModifiersReflection.Descriptor.MessageTypes[0]; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    pbr::MessageDescriptor pb::IMessage.Descriptor {
      get { return Descriptor; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public GameplayModifiers() {
      OnConstruction();
    }

    partial void OnConstruction();

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public GameplayModifiers(GameplayModifiers other) : this() {
      options_ = other.options_;
      _unknownFields = pb::UnknownFieldSet.Clone(other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public GameplayModifiers Clone() {
      return new GameplayModifiers(this);
    }

    /// <summary>Field number for the "options" field.</summary>
    public const int OptionsFieldNumber = 1;
    private global::TournamentAssistantShared.Models.GameplayModifiers.Types.GameOptions options_ = global::TournamentAssistantShared.Models.GameplayModifiers.Types.GameOptions.None;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public global::TournamentAssistantShared.Models.GameplayModifiers.Types.GameOptions Options {
      get { return options_; }
      set {
        options_ = value;
      }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public override bool Equals(object other) {
      return Equals(other as GameplayModifiers);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public bool Equals(GameplayModifiers other) {
      if (ReferenceEquals(other, null)) {
        return false;
      }
      if (ReferenceEquals(other, this)) {
        return true;
      }
      if (Options != other.Options) return false;
      return Equals(_unknownFields, other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public override int GetHashCode() {
      int hash = 1;
      if (Options != global::TournamentAssistantShared.Models.GameplayModifiers.Types.GameOptions.None) hash ^= Options.GetHashCode();
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
      if (Options != global::TournamentAssistantShared.Models.GameplayModifiers.Types.GameOptions.None) {
        output.WriteRawTag(8);
        output.WriteEnum((int) Options);
      }
      if (_unknownFields != null) {
        _unknownFields.WriteTo(output);
      }
    #endif
    }

    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    void pb::IBufferMessage.InternalWriteTo(ref pb::WriteContext output) {
      if (Options != global::TournamentAssistantShared.Models.GameplayModifiers.Types.GameOptions.None) {
        output.WriteRawTag(8);
        output.WriteEnum((int) Options);
      }
      if (_unknownFields != null) {
        _unknownFields.WriteTo(ref output);
      }
    }
    #endif

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public int CalculateSize() {
      int size = 0;
      if (Options != global::TournamentAssistantShared.Models.GameplayModifiers.Types.GameOptions.None) {
        size += 1 + pb::CodedOutputStream.ComputeEnumSize((int) Options);
      }
      if (_unknownFields != null) {
        size += _unknownFields.CalculateSize();
      }
      return size;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public void MergeFrom(GameplayModifiers other) {
      if (other == null) {
        return;
      }
      if (other.Options != global::TournamentAssistantShared.Models.GameplayModifiers.Types.GameOptions.None) {
        Options = other.Options;
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
          case 8: {
            Options = (global::TournamentAssistantShared.Models.GameplayModifiers.Types.GameOptions) input.ReadEnum();
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
          case 8: {
            Options = (global::TournamentAssistantShared.Models.GameplayModifiers.Types.GameOptions) input.ReadEnum();
            break;
          }
        }
      }
    }
    #endif

    #region Nested types
    /// <summary>Container for nested types declared in the GameplayModifiers message type.</summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public static partial class Types {
      public enum GameOptions {
        [pbr::OriginalName("None")] None = 0,
        /// <summary>
        ///Negative modifiers
        /// </summary>
        [pbr::OriginalName("NoFail")] NoFail = 1,
        [pbr::OriginalName("NoBombs")] NoBombs = 2,
        [pbr::OriginalName("NoArrows")] NoArrows = 4,
        [pbr::OriginalName("NoObstacles")] NoObstacles = 8,
        [pbr::OriginalName("SlowSong")] SlowSong = 16,
        /// <summary>
        ///Positive Modifiers
        /// </summary>
        [pbr::OriginalName("InstaFail")] InstaFail = 32,
        [pbr::OriginalName("FailOnClash")] FailOnClash = 64,
        [pbr::OriginalName("BatteryEnergy")] BatteryEnergy = 128,
        [pbr::OriginalName("FastNotes")] FastNotes = 256,
        [pbr::OriginalName("FastSong")] FastSong = 512,
        [pbr::OriginalName("DisappearingArrows")] DisappearingArrows = 1024,
        [pbr::OriginalName("GhostNotes")] GhostNotes = 2048,
        /// <summary>
        ///1.12.2 Additions
        /// </summary>
        [pbr::OriginalName("DemoNoFail")] DemoNoFail = 4096,
        [pbr::OriginalName("DemoNoObstacles")] DemoNoObstacles = 8192,
        [pbr::OriginalName("StrictAngles")] StrictAngles = 16384,
      }

    }
    #endregion

  }

  #endregion

}

#endregion Designer generated code
