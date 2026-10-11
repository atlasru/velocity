using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Velocity.Core
{
    public enum PacketType : byte { Hello=1,Welcome=2,Ready=3,Inputs=4,Snapshot=5,Disconnect=6,Reject=7 }
    public sealed class NetworkPacket
    {
        public PacketType Type; public ulong Token,Nonce; public int Tick; public byte PlayerId,Map;
        public bool Teams; public string Name;
        public MovementConfig Config;
        public readonly List<PlayerInput> Inputs=new List<PlayerInput>();
        public readonly List<PlayerState> Players=new List<PlayerState>();
        public readonly List<GameEvent> Events=new List<GameEvent>();
    }
    public static class NetworkProtocol
    {
        public const uint Magic=0x56454c4f;
        public const byte Version=1;
        public const int MaxDatagram=1400;
        public static byte[] Encode(NetworkPacket packet)
        {
            using(var stream=new MemoryStream())
            using(var w=new BinaryWriter(stream,Encoding.UTF8,true))
            {
                w.Write(Magic); w.Write(Version); w.Write((byte)packet.Type); w.Write(packet.Token);
                switch(packet.Type)
                {
                    case PacketType.Hello: w.Write(packet.Nonce); WriteName(w,packet.Name); break;
                    case PacketType.Welcome: w.Write(packet.Nonce); w.Write(packet.PlayerId); w.Write(packet.Map); w.Write(packet.Teams); w.Write(packet.Tick); WriteConfig(w,packet.Config??MovementConfig.Preset((MapMode)packet.Map)); break;
                    case PacketType.Ready: case PacketType.Disconnect: break;
                    case PacketType.Reject: WriteName(w,packet.Name); break;
                    case PacketType.Inputs:
                        if(packet.Inputs.Count>12) throw new InvalidDataException("Input bundle too large");
                        w.Write((byte)packet.Inputs.Count);
                        foreach(PlayerInput i in packet.Inputs) { w.Write(i.Sequence); w.Write(i.ServerTick); w.Write(i.Side); w.Write(i.Forward); w.Write(i.Yaw); w.Write(i.Pitch); w.Write((ushort)i.Buttons); w.Write(i.Weapon); }
                        break;
                    case PacketType.Snapshot:
                        if(packet.Players.Count>8||packet.Events.Count>4) throw new InvalidDataException("Snapshot too large");
                        w.Write(packet.Tick); w.Write((byte)packet.Events.Count);
                        foreach(GameEvent e in packet.Events) { w.Write(e.Id); w.Write(e.Tick); w.Write(e.Killer); w.Write(e.Victim); w.Write(e.Weapon); }
                        w.Write((byte)packet.Players.Count); foreach(PlayerState p in packet.Players) WritePlayer(w,p);
                        break;
                    default: throw new InvalidDataException("Unknown packet type");
                }
                w.Flush(); if(stream.Length>MaxDatagram) throw new InvalidDataException("Datagram exceeds MTU budget"); return stream.ToArray();
            }
        }
        public static bool TryDecode(byte[] data,int length,out NetworkPacket packet)
        {
            packet=null;
            if(length<14||length>MaxDatagram||length>data.Length) return false;
            try
            {
                using(var stream=new MemoryStream(data,0,length,false))
                using(var r=new BinaryReader(stream,Encoding.UTF8))
                {
                    if(r.ReadUInt32()!=Magic||r.ReadByte()!=Version) return false;
                    var p=new NetworkPacket{Type=(PacketType)r.ReadByte(),Token=r.ReadUInt64()};
                    switch(p.Type)
                    {
                        case PacketType.Hello: p.Nonce=r.ReadUInt64(); p.Name=ReadName(r); break;
                        case PacketType.Welcome: p.Nonce=r.ReadUInt64(); p.PlayerId=r.ReadByte(); p.Map=r.ReadByte(); p.Teams=r.ReadBoolean(); p.Tick=r.ReadInt32();p.Config=ReadConfig(r); if(p.PlayerId<2||p.PlayerId>8||p.Map>2||p.Tick<0||!p.Config.Valid) return false; break;
                        case PacketType.Ready: case PacketType.Disconnect: break;
                        case PacketType.Reject: p.Name=ReadName(r); break;
                        case PacketType.Inputs:
                            int count=r.ReadByte(); if(count<1||count>12) return false;
                            for(int n=0;n<count;n++)
                            {
                                var i=new PlayerInput{Sequence=r.ReadInt32(),ServerTick=r.ReadInt32(),Side=r.ReadSingle(),Forward=r.ReadSingle(),Yaw=r.ReadSingle(),Pitch=r.ReadSingle(),Buttons=(InputButtons)r.ReadUInt16(),Weapon=r.ReadByte()};
                                if(!i.Valid) return false; p.Inputs.Add(i);
                            }
                            break;
                        case PacketType.Snapshot:
                            p.Tick=r.ReadInt32(); if(p.Tick<0) return false;
                            int events=r.ReadByte(); if(events>4) return false;
                            for(int n=0;n<events;n++)
                            {
                                var e=new GameEvent{Id=r.ReadInt32(),Tick=r.ReadInt32(),Killer=r.ReadByte(),Victim=r.ReadByte(),Weapon=r.ReadByte()};
                                if(e.Id<0||e.Tick<0||e.Killer<1||e.Killer>8||e.Victim<1||e.Victim>8||e.Weapon>3)return false;
                                p.Events.Add(e);
                            }
                            int players=r.ReadByte(); if(players<1||players>8) return false;
                            var ids=new HashSet<byte>();
                            for(int n=0;n<players;n++) { PlayerState player=ReadPlayer(r); if(!ids.Add(player.Id)) return false; p.Players.Add(player); }
                            break;
                        default: return false;
                    }
                    if(stream.Position!=length) return false;
                    packet=p; return true;
                }
            }
            catch(IOException) { return false; }
            catch(ArgumentException) { return false; }
        }
        private static void WriteName(BinaryWriter w,string name)
        {
            string clean=GameSimulation.CleanName(name); byte[] b=Encoding.UTF8.GetBytes(clean);
            while(b.Length>32) { clean=clean.Substring(0,clean.Length-1); b=Encoding.UTF8.GetBytes(clean); }
            w.Write((byte)b.Length); w.Write(b);
        }
        private static string ReadName(BinaryReader r)
        {
            int size=r.ReadByte(); if(size>32) throw new InvalidDataException("Invalid name length");
            byte[] bytes=r.ReadBytes(size); if(bytes.Length!=size) throw new EndOfStreamException();
            return GameSimulation.CleanName(new UTF8Encoding(false,true).GetString(bytes));
        }
        private static void WriteVec(BinaryWriter w,Vec3 v) { w.Write(v.X); w.Write(v.Y); w.Write(v.Z); }
        private static void WriteConfig(BinaryWriter w,MovementConfig c)
        {
            w.Write(c.sv_gravity);w.Write(c.sv_accelerate);w.Write(c.sv_airaccelerate);w.Write(c.sv_friction);w.Write(c.sv_maxspeed);w.Write(c.sv_stopspeed);w.Write(c.sv_jump_impulse);w.Write(c.sv_air_max_wishspeed);w.Write(c.sv_autobunnyhopping);w.Write(c.SpeedLimit);
        }
        private static MovementConfig ReadConfig(BinaryReader r) => new MovementConfig{sv_gravity=r.ReadSingle(),sv_accelerate=r.ReadSingle(),sv_airaccelerate=r.ReadSingle(),sv_friction=r.ReadSingle(),sv_maxspeed=r.ReadSingle(),sv_stopspeed=r.ReadSingle(),sv_jump_impulse=r.ReadSingle(),sv_air_max_wishspeed=r.ReadSingle(),sv_autobunnyhopping=r.ReadBoolean(),SpeedLimit=r.ReadSingle()};
        private static Vec3 ReadVec(BinaryReader r) => new Vec3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());
        private static void WritePlayer(BinaryWriter w,PlayerState p)
        {
            w.Write(p.Id); WriteName(w,p.Name); w.Write(p.AcknowledgedSequence);
            MovementState m=p.Movement; WriteVec(w,m.Position); WriteVec(w,m.Velocity); w.Write(m.Yaw); w.Write(m.Pitch); w.Write(m.MaxSpeed);
            w.Write((byte)((m.Grounded?1:0)|(m.Crouched?2:0)|(m.JumpHeld?4:0))); w.Write(m.Jumps); w.Write(m.AirTicks); w.Write(m.SyncTicks);
            TimerState t=p.Timer; w.Write(t.StartTick); w.Write(t.ElapsedTicks); w.Write(t.FinishSerial); w.Write(t.Checkpoint);
            w.Write((byte)((t.Active?1:0)|(t.Finished?2:0)|(t.Practice?4:0)|(t.WasInStart?8:0)));
            w.Write((byte)p.Health); w.Write(p.Team); w.Write((ushort)p.Kills); w.Write((ushort)p.Deaths); w.Write(p.RespawnTick); w.Write(p.InvulnerableUntil); w.Write((ushort)p.PreviousButtons);
            WeaponState weapon=p.Weapon; w.Write(weapon.Selected); w.Write(weapon.Burst); w.Write(weapon.FireHeld);
            for(int i=0;i<4;i++) { w.Write(weapon.Ammo[i]); w.Write(weapon.Reserve[i]); }
            w.Write(weapon.NextFireTick); w.Write(weapon.ReloadFinishTick); w.Write(weapon.LastShotTick); w.Write(weapon.ShotSerial);
        }
        private static PlayerState ReadPlayer(BinaryReader r)
        {
            var p=new PlayerState{Id=r.ReadByte(),Name=ReadName(r),AcknowledgedSequence=r.ReadInt32()};
            var m=new MovementState{Position=ReadVec(r),Velocity=ReadVec(r),Yaw=r.ReadSingle(),Pitch=r.ReadSingle(),MaxSpeed=r.ReadSingle()};
            byte flags=r.ReadByte(); m.Grounded=(flags&1)!=0; m.Crouched=(flags&2)!=0; m.JumpHeld=(flags&4)!=0;
            m.Jumps=r.ReadInt32(); m.AirTicks=r.ReadInt32(); m.SyncTicks=r.ReadInt32();
            if(p.Id<1||p.Id>8||!m.Position.Finite||!m.Velocity.Finite||!MathEx.Finite(m.Yaw)||!MathEx.Finite(m.Pitch)||!MathEx.Finite(m.MaxSpeed)||m.Jumps<0||m.AirTicks<0||m.SyncTicks<0) throw new InvalidDataException("Invalid player state");
            p.Movement=m;
            var t=new TimerState{StartTick=r.ReadInt32(),ElapsedTicks=r.ReadInt32(),FinishSerial=r.ReadInt32(),Checkpoint=r.ReadByte()};
            flags=r.ReadByte(); t.Active=(flags&1)!=0; t.Finished=(flags&2)!=0; t.Practice=(flags&4)!=0; t.WasInStart=(flags&8)!=0; p.Timer=t;
            p.Health=r.ReadByte(); p.Team=r.ReadByte(); p.Kills=r.ReadUInt16(); p.Deaths=r.ReadUInt16(); p.RespawnTick=r.ReadInt32(); p.InvulnerableUntil=r.ReadInt32(); p.PreviousButtons=(InputButtons)r.ReadUInt16();
            var w=new WeaponState{Selected=r.ReadByte(),Burst=r.ReadByte(),FireHeld=r.ReadBoolean()};
            for(int i=0;i<4;i++) { w.Ammo[i]=r.ReadByte(); w.Reserve[i]=r.ReadUInt16(); }
            w.NextFireTick=r.ReadInt32(); w.ReloadFinishTick=r.ReadInt32(); w.LastShotTick=r.ReadInt32(); w.ShotSerial=r.ReadInt32();
            if(w.Selected>3||p.Health>100||p.Team>1) throw new InvalidDataException("Invalid combat state");
            p.Weapon=w; return p;
        }
    }
}
