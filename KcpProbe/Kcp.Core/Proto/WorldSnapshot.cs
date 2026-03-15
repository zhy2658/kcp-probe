using System;
using pb = global::Google.Protobuf;
using pbc = global::Google.Protobuf.Collections;
using pbr = global::Google.Protobuf.Reflection;
using scg = global::System.Collections.Generic;

namespace KcpServer {

  public sealed partial class Vector3 : pb::IMessage<Vector3> {
    private static readonly pb::MessageParser<Vector3> _parser = new pb::MessageParser<Vector3>(() => new Vector3());
    public static pb::MessageParser<Vector3> Parser { get { return _parser; } }
    
    // Dummy descriptor to satisfy interface, not actually used for manual parsing
    public static pbr::MessageDescriptor Descriptor { get { return null; } }
    pbr::MessageDescriptor pb::IMessage.Descriptor { get { return Descriptor; } }

    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }

    public Vector3() { }
    public Vector3(Vector3 other) : this() {
      X = other.X;
      Y = other.Y;
      Z = other.Z;
    }

    public Vector3 Clone() { return new Vector3(this); }

    public void WriteTo(pb::CodedOutputStream output) {
      if (X != 0F) { output.WriteRawTag(13); output.WriteFloat(X); }
      if (Y != 0F) { output.WriteRawTag(21); output.WriteFloat(Y); }
      if (Z != 0F) { output.WriteRawTag(29); output.WriteFloat(Z); }
    }

    public int CalculateSize() {
      int size = 0;
      if (X != 0F) size += 1 + 4;
      if (Y != 0F) size += 1 + 4;
      if (Z != 0F) size += 1 + 4;
      return size;
    }

    public void MergeFrom(Vector3 other) {
      if (other == null) return;
      if (other.X != 0F) X = other.X;
      if (other.Y != 0F) Y = other.Y;
      if (other.Z != 0F) Z = other.Z;
    }

    public void MergeFrom(pb::CodedInputStream input) {
      uint tag;
      while ((tag = input.ReadTag()) != 0) {
        switch(tag) {
          case 13: X = input.ReadFloat(); break;
          case 21: Y = input.ReadFloat(); break;
          case 29: Z = input.ReadFloat(); break;
          default: input.SkipLastField(); break;
        }
      }
    }

    public bool Equals(Vector3 other) {
      if (ReferenceEquals(other, null)) return false;
      if (ReferenceEquals(other, this)) return true;
      if (!pbc::ProtobufEqualityComparers.BitwiseSingleEqualityComparer.Equals(X, other.X)) return false;
      if (!pbc::ProtobufEqualityComparers.BitwiseSingleEqualityComparer.Equals(Y, other.Y)) return false;
      if (!pbc::ProtobufEqualityComparers.BitwiseSingleEqualityComparer.Equals(Z, other.Z)) return false;
      return true;
    }

    public override bool Equals(object other) {
      return Equals(other as Vector3);
    }

    public override int GetHashCode() {
      int hash = 1;
      if (X != 0F) hash ^= pbc::ProtobufEqualityComparers.BitwiseSingleEqualityComparer.GetHashCode(X);
      if (Y != 0F) hash ^= pbc::ProtobufEqualityComparers.BitwiseSingleEqualityComparer.GetHashCode(Y);
      if (Z != 0F) hash ^= pbc::ProtobufEqualityComparers.BitwiseSingleEqualityComparer.GetHashCode(Z);
      return hash;
    }

    public override string ToString() {
      return $"({X}, {Y}, {Z})";
    }
  }

  public sealed partial class EntityState : pb::IMessage<EntityState> {
    private static readonly pb::MessageParser<EntityState> _parser = new pb::MessageParser<EntityState>(() => new EntityState());
    public static pb::MessageParser<EntityState> Parser { get { return _parser; } }
    public static pbr::MessageDescriptor Descriptor { get { return null; } }
    pbr::MessageDescriptor pb::IMessage.Descriptor { get { return Descriptor; } }

    public uint EntityId { get; set; }
    public Vector3 Pos { get; set; }
    public Vector3 Vel { get; set; }
    public float Yaw { get; set; }
    public int Hp { get; set; }
    public uint StateFlags { get; set; }

    public EntityState() { }
    public EntityState(EntityState other) : this() {
      EntityId = other.EntityId;
      Pos = other.Pos?.Clone();
      Vel = other.Vel?.Clone();
      Yaw = other.Yaw;
      Hp = other.Hp;
      StateFlags = other.StateFlags;
    }

    public EntityState Clone() { return new EntityState(this); }

    public void WriteTo(pb::CodedOutputStream output) {
      if (EntityId != 0) { output.WriteRawTag(8); output.WriteUInt32(EntityId); }
      if (Pos != null) { output.WriteRawTag(18); output.WriteMessage(Pos); }
      if (Vel != null) { output.WriteRawTag(26); output.WriteMessage(Vel); }
      if (Yaw != 0F) { output.WriteRawTag(37); output.WriteFloat(Yaw); }
      if (Hp != 0) { output.WriteRawTag(40); output.WriteInt32(Hp); }
      if (StateFlags != 0) { output.WriteRawTag(48); output.WriteUInt32(StateFlags); }
    }

    public int CalculateSize() {
      int size = 0;
      if (EntityId != 0) size += 1 + pb::CodedOutputStream.ComputeUInt32Size(EntityId);
      if (Pos != null) size += 1 + pb::CodedOutputStream.ComputeMessageSize(Pos);
      if (Vel != null) size += 1 + pb::CodedOutputStream.ComputeMessageSize(Vel);
      if (Yaw != 0F) size += 1 + 4;
      if (Hp != 0) size += 1 + pb::CodedOutputStream.ComputeInt32Size(Hp);
      if (StateFlags != 0) size += 1 + pb::CodedOutputStream.ComputeUInt32Size(StateFlags);
      return size;
    }

    public void MergeFrom(EntityState other) {
      if (other == null) return;
      if (other.EntityId != 0) EntityId = other.EntityId;
      if (other.Pos != null) {
        if (Pos == null) Pos = new Vector3();
        Pos.MergeFrom(other.Pos);
      }
      if (other.Vel != null) {
        if (Vel == null) Vel = new Vector3();
        Vel.MergeFrom(other.Vel);
      }
      if (other.Yaw != 0F) Yaw = other.Yaw;
      if (other.Hp != 0) Hp = other.Hp;
      if (other.StateFlags != 0) StateFlags = other.StateFlags;
    }

    public void MergeFrom(pb::CodedInputStream input) {
      uint tag;
      while ((tag = input.ReadTag()) != 0) {
        switch(tag) {
          case 8: EntityId = input.ReadUInt32(); break;
          case 18: {
            if (Pos == null) Pos = new Vector3();
            input.ReadMessage(Pos);
            break;
          }
          case 26: {
            if (Vel == null) Vel = new Vector3();
            input.ReadMessage(Vel);
            break;
          }
          case 37: Yaw = input.ReadFloat(); break;
          case 40: Hp = input.ReadInt32(); break;
          case 48: StateFlags = input.ReadUInt32(); break;
          default: input.SkipLastField(); break;
        }
      }
    }

    public bool Equals(EntityState other) {
      if (ReferenceEquals(other, null)) return false;
      if (ReferenceEquals(other, this)) return true;
      if (EntityId != other.EntityId) return false;
      if (!object.Equals(Pos, other.Pos)) return false;
      if (!object.Equals(Vel, other.Vel)) return false;
      if (!pbc::ProtobufEqualityComparers.BitwiseSingleEqualityComparer.Equals(Yaw, other.Yaw)) return false;
      if (Hp != other.Hp) return false;
      if (StateFlags != other.StateFlags) return false;
      return true;
    }

    public override bool Equals(object other) { return Equals(other as EntityState); }
    public override int GetHashCode() { return EntityId.GetHashCode(); }
  }

  public sealed partial class WorldSnapshot : pb::IMessage<WorldSnapshot> {
    private static readonly pb::MessageParser<WorldSnapshot> _parser = new pb::MessageParser<WorldSnapshot>(() => new WorldSnapshot());
    public static pb::MessageParser<WorldSnapshot> Parser { get { return _parser; } }
    public static pbr::MessageDescriptor Descriptor { get { return null; } }
    pbr::MessageDescriptor pb::IMessage.Descriptor { get { return Descriptor; } }

    public uint Seq { get; set; }
    public uint ServerTime { get; set; }
    private static readonly pb::FieldCodec<EntityState> _repeated_entities_codec
        = pb::FieldCodec.ForMessage(26, EntityState.Parser);
    public readonly pbc::RepeatedField<EntityState> Entities = new pbc::RepeatedField<EntityState>();

    public WorldSnapshot() { }
    public WorldSnapshot(WorldSnapshot other) : this() {
      Seq = other.Seq;
      ServerTime = other.ServerTime;
      Entities.Add(other.Entities);
    }

    public WorldSnapshot Clone() { return new WorldSnapshot(this); }

    public void WriteTo(pb::CodedOutputStream output) {
      if (Seq != 0) { output.WriteRawTag(8); output.WriteUInt32(Seq); }
      if (ServerTime != 0) { output.WriteRawTag(16); output.WriteUInt32(ServerTime); }
      Entities.WriteTo(output, _repeated_entities_codec);
    }

    public int CalculateSize() {
      int size = 0;
      if (Seq != 0) size += 1 + pb::CodedOutputStream.ComputeUInt32Size(Seq);
      if (ServerTime != 0) size += 1 + pb::CodedOutputStream.ComputeUInt32Size(ServerTime);
      size += Entities.CalculateSize(_repeated_entities_codec);
      return size;
    }

    public void MergeFrom(WorldSnapshot other) {
      if (other == null) return;
      if (other.Seq != 0) Seq = other.Seq;
      if (other.ServerTime != 0) ServerTime = other.ServerTime;
      Entities.Add(other.Entities);
    }

    public void MergeFrom(pb::CodedInputStream input) {
      uint tag;
      while ((tag = input.ReadTag()) != 0) {
        switch(tag) {
          case 8: Seq = input.ReadUInt32(); break;
          case 16: ServerTime = input.ReadUInt32(); break;
          case 26: Entities.AddEntriesFrom(input, _repeated_entities_codec); break;
          default: input.SkipLastField(); break;
        }
      }
    }

    public bool Equals(WorldSnapshot other) {
      if (ReferenceEquals(other, null)) return false;
      if (ReferenceEquals(other, this)) return true;
      if (Seq != other.Seq) return false;
      if (ServerTime != other.ServerTime) return false;
      if (!Entities.Equals(other.Entities)) return false;
      return true;
    }

    public override bool Equals(object other) { return Equals(other as WorldSnapshot); }
    public override int GetHashCode() { return Seq.GetHashCode(); }
    
    public override string ToString() {
        return $"Snapshot Seq:{Seq} Time:{ServerTime} Entities:{Entities.Count}";
    }
  }

}
