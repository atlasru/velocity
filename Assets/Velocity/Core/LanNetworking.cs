using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace Velocity.Core
{
    public sealed class UdpTransport : IDisposable
    {
        private readonly Socket socket;
        private readonly byte[] buffer=new byte[NetworkProtocol.MaxDatagram+1];
        public readonly int Port;
        public Func<byte[],IPEndPoint,bool> DropForTesting;
        public UdpTransport(int port=0,bool broadcast=false)
        {
            socket=new Socket(AddressFamily.InterNetwork,SocketType.Dgram,ProtocolType.Udp);
            socket.Blocking=false; socket.EnableBroadcast=broadcast;
            socket.Bind(new IPEndPoint(IPAddress.Any,port)); Port=((IPEndPoint)socket.LocalEndPoint).Port;
        }
        public void Send(byte[] data,IPEndPoint endpoint)
        {
            if(DropForTesting!=null&&DropForTesting(data,endpoint)) return;
            try { socket.SendTo(data,endpoint); }
            catch(SocketException e) when(e.SocketErrorCode==SocketError.WouldBlock||e.SocketErrorCode==SocketError.NoBufferSpaceAvailable||e.SocketErrorCode==SocketError.NetworkUnreachable||e.SocketErrorCode==SocketError.ConnectionReset) { }
        }
        public void Poll(Action<byte[],int,IPEndPoint> receive)
        {
            for(int n=0;n<256;n++)
            {
                EndPoint endpoint=new IPEndPoint(IPAddress.Any,0);
                int length;
                try { length=socket.ReceiveFrom(buffer,ref endpoint); }
                catch(SocketException e) when(e.SocketErrorCode==SocketError.WouldBlock||e.SocketErrorCode==SocketError.ConnectionReset||e.SocketErrorCode==SocketError.MessageSize) { break; }
                if(length<=NetworkProtocol.MaxDatagram) receive(buffer,length,(IPEndPoint)endpoint);
            }
        }
        public void Dispose() => socket.Dispose();
    }
    public sealed class ServerInputQueue
    {
        private readonly SortedDictionary<int,PlayerInput> queue=new SortedDictionary<int,PlayerInput>();
        private PlayerInput last;
        private bool started;
        private int next=-1;
        public int Acknowledged=-1;
        public bool Add(PlayerInput input)
        {
            if(!input.Valid||input.Sequence<=Acknowledged||input.Sequence>Acknowledged+256||queue.Count>=64) return false;
            if(!queue.ContainsKey(input.Sequence)) queue.Add(input.Sequence,input);
            return true;
        }
        public PlayerInput Consume(int serverTick)
        {
            if(!started)
            {
                if(queue.Count<3) return new PlayerInput{Sequence=Math.Max(0,Acknowledged),Yaw=last.Yaw,Pitch=last.Pitch};
                foreach(int sequence in queue.Keys) { next=sequence; break; }
                started=true;
            }
            if(queue.TryGetValue(next,out PlayerInput input)) { last=input; queue.Remove(next); }
            else last.Buttons&=~(InputButtons.Restart|InputButtons.Checkpoint|InputButtons.Reload);
            last.Sequence=next; last.ServerTick=MathEx.Clamp(last.ServerTick,Math.Max(0,serverTick-24),serverTick);
            Acknowledged=next++;
            return last;
        }
    }
    public sealed class LanServer : IDisposable
    {
        public const int DefaultPort=27027;
        public readonly UdpTransport Transport;
        public readonly GameSimulation Simulation;
        private readonly Dictionary<string,Peer> peers=new Dictionary<string,Peer>();
        private double now;
        private sealed class Peer
        {
            public byte Id; public ulong Token,Nonce; public IPEndPoint Endpoint; public bool Ready;
            public double LastSeen,RateWindow; public int Rate;
            public readonly ServerInputQueue Inputs=new ServerInputQueue();
        }
        public LanServer(byte map,string owner,bool teams=false,int port=DefaultPort)
        {
            Transport=new UdpTransport(port); Simulation=new GameSimulation(map,teams); Simulation.AddPlayer(1,owner);
        }
        public static ulong RandomToken()
        {
            var b=new byte[8]; using(RandomNumberGenerator rng=RandomNumberGenerator.Create()) rng.GetBytes(b); return BitConverter.ToUInt64(b,0);
        }
        public void Pump(double time)
        {
            now=time; Transport.Poll(Receive);
            var stale=new List<string>(); foreach(var pair in peers) if(now-pair.Value.LastSeen>10) stale.Add(pair.Key);
            foreach(string key in stale) { Simulation.RemovePlayer(peers[key].Id); peers.Remove(key); }
        }
        private void Send(Peer peer,NetworkPacket packet) { packet.Token=peer.Token; Transport.Send(NetworkProtocol.Encode(packet),peer.Endpoint); }
        private void Receive(byte[] data,int size,IPEndPoint endpoint)
        {
            if(!NetworkProtocol.TryDecode(data,size,out NetworkPacket packet)) return;
            string key=endpoint.ToString(); peers.TryGetValue(key,out Peer peer);
            if(packet.Type==PacketType.Hello)
            {
                if(peer!=null&&peer.Nonce!=packet.Nonce) { Simulation.RemovePlayer(peer.Id); peers.Remove(key); peer=null; }
                if(peer==null)
                {
                    if(peers.Count>=7) { Transport.Send(NetworkProtocol.Encode(new NetworkPacket{Type=PacketType.Reject,Name="Server full"}),endpoint); return; }
                    byte id=2; while(Simulation.Players.ContainsKey(id)) id++;
                    peer=new Peer{Id=id,Endpoint=endpoint,Token=RandomToken(),Nonce=packet.Nonce,LastSeen=now}; peers.Add(key,peer); Simulation.AddPlayer(id,packet.Name);
                }
                if(now-peer.RateWindow>=1) { peer.RateWindow=now; peer.Rate=0; }
                if(++peer.Rate>150) return;
                peer.LastSeen=now;
                Send(peer,new NetworkPacket{Type=PacketType.Welcome,Nonce=peer.Nonce,PlayerId=peer.Id,Map=Simulation.Map.Id,Teams=Simulation.Teams,Tick=Simulation.Tick,Config=Simulation.Movement.Config}); return;
            }
            if(peer==null||packet.Token!=peer.Token) return;
            if(now-peer.RateWindow>=1) { peer.RateWindow=now; peer.Rate=0; }
            if(++peer.Rate>150) return;
            peer.LastSeen=now;
            if(packet.Type==PacketType.Disconnect) { Simulation.RemovePlayer(peer.Id); peers.Remove(key); return; }
            if(packet.Type==PacketType.Ready) { peer.Ready=true; return; }
            if(packet.Type==PacketType.Inputs)
            {
                peer.Ready=true;
                foreach(PlayerInput input in packet.Inputs) peer.Inputs.Add(input);
            }
        }
        public void Step(PlayerInput ownerInput)
        {
            Simulation.AdvanceTick(); Simulation.Simulate(1,ownerInput);
            foreach(Peer peer in peers.Values) if(peer.Ready) Simulation.Simulate(peer.Id,peer.Inputs.Consume(Simulation.Tick));
            if(Simulation.Tick%4!=0) return;
            foreach(Peer peer in peers.Values)
            {
                if(!peer.Ready) continue;
                var snapshot=new NetworkPacket{Type=PacketType.Snapshot,Tick=Simulation.Tick};
                foreach(PlayerState p in Simulation.Players.Values) snapshot.Players.Add(p);
                snapshot.Events.AddRange(Simulation.Events); Send(peer,snapshot);
            }
        }
        public void Dispose()
        {
            foreach(Peer peer in peers.Values) Send(peer,new NetworkPacket{Type=PacketType.Disconnect});
            Transport.Dispose();
        }
    }
    public sealed class PredictionBuffer
    {
        public readonly List<PlayerInput> Pending=new List<PlayerInput>();
        public void Add(PlayerInput input) { Pending.Add(input); if(Pending.Count>240) Pending.RemoveAt(0); }
        public void Reconcile(GameSimulation sim,byte id,PlayerState authoritative,int serverTick)
        {
            sim.Players[id]=authoritative.Clone(); Pending.RemoveAll(i=>i.Sequence<=authoritative.AcknowledgedSequence);
            sim.Tick=serverTick;
            foreach(PlayerInput i in Pending) { sim.AdvanceTick(); sim.Simulate(id,i,false); }
        }
    }
    public sealed class RemoteInterpolation
    {
        private struct Sample { public int Tick; public MovementState State; }
        private readonly List<Sample> samples=new List<Sample>();
        public void Add(int tick,MovementState state)
        {
            if(samples.Count>0&&tick<=samples[samples.Count-1].Tick) return;
            if(samples.Count>0&&(state.Position-samples[samples.Count-1].State.Position).Length>8) samples.Clear();
            samples.Add(new Sample{Tick=tick,State=state}); if(samples.Count>32) samples.RemoveAt(0);
        }
        public MovementState At(float tick)
        {
            if(samples.Count==0) return new MovementState();
            while(samples.Count>2&&samples[1].Tick<=tick) samples.RemoveAt(0);
            Sample a=samples[0];
            if(samples.Count>1&&tick<=samples[1].Tick)
            {
                Sample b=samples[1]; float t=MathEx.Clamp((tick-a.Tick)/(b.Tick-a.Tick),0,1);
                MovementState state=b.State; state.Position=Vec3.Lerp(a.State.Position,b.State.Position,t); state.Yaw=MathEx.WrapAngle(a.State.Yaw+MathEx.WrapAngle(b.State.Yaw-a.State.Yaw)*t); return state;
            }
            Sample latest=samples[samples.Count-1]; MovementState result=latest.State;
            result.Position+=result.Velocity*(MathEx.Clamp(tick-latest.Tick,0,3)/MovementConfig.TickRate); return result;
        }
    }
    public sealed class LanClient : IDisposable
    {
        public readonly UdpTransport Transport=new UdpTransport();
        public readonly PredictionBuffer Prediction=new PredictionBuffer();
        public readonly Dictionary<byte,PlayerState> Roster=new Dictionary<byte,PlayerState>();
        public readonly Dictionary<byte,RemoteInterpolation> Remote=new Dictionary<byte,RemoteInterpolation>();
        public readonly List<GameEvent> Events=new List<GameEvent>();
        public GameSimulation Simulation;
        public byte LocalId;
        public bool Connected;
        public string Status="Connecting";
        public Vec3 Correction;
        public int ServerTick,NextSequence,LastSnapshotTick=-1;
        public float LastCorrection;
        private readonly IPEndPoint server;
        private readonly string name;
        private readonly ulong nonce=LanServer.RandomToken();
        private ulong token;
        private double now,lastHello=-1,lastSeen;
        public LanClient(string ip,string playerName,int port=LanServer.DefaultPort)
        {
            if(!IPAddress.TryParse(ip,out IPAddress address)||address.AddressFamily!=AddressFamily.InterNetwork) { Transport.Dispose(); throw new ArgumentException("Enter an IPv4 LAN address"); }
            server=new IPEndPoint(address,port); name=playerName;
        }
        public void Pump(double time)
        {
            now=time; Transport.Poll(Receive);
            if(!Connected&&now-lastHello>=.25&&Status!="Disconnected"&&Status!="Server full")
            { Transport.Send(NetworkProtocol.Encode(new NetworkPacket{Type=PacketType.Hello,Nonce=nonce,Name=name}),server); lastHello=now; if(lastSeen==0) lastSeen=now; }
            if(now-lastSeen>10) { Connected=false; Status="Connection timed out"; }
        }
        private void Receive(byte[] data,int size,IPEndPoint endpoint)
        {
            if(!endpoint.Equals(server)||!NetworkProtocol.TryDecode(data,size,out NetworkPacket packet)) return;
            if(packet.Type==PacketType.Reject&&!Connected) { Status=packet.Name; lastSeen=now; return; }
            if(packet.Type==PacketType.Welcome&&packet.Nonce==nonce)
            {
                if(Simulation==null||token!=packet.Token)
                {
                    token=packet.Token; LocalId=packet.PlayerId; Simulation=new GameSimulation(packet.Map,packet.Teams);Simulation.Movement.Config.CopyFrom(packet.Config);Simulation.CustomRules=!packet.Config.SameRules(MovementConfig.Preset((MapMode)packet.Map)); Simulation.AddPlayer(LocalId,name); Simulation.Tick=packet.Tick; ServerTick=packet.Tick;
                    Prediction.Pending.Clear();Roster.Clear();Remote.Clear();Events.Clear();NextSequence=0;LastSnapshotTick=-1;Correction=Vec3.Zero;
                }
                Transport.Send(NetworkProtocol.Encode(new NetworkPacket{Type=PacketType.Ready,Token=token}),server);
                Connected=true; Status="Connected"; lastSeen=now; return;
            }
            if(!Connected||packet.Token!=token) return;
            lastSeen=now;
            if(packet.Type==PacketType.Disconnect) { Connected=false; Status="Disconnected"; return; }
            if(packet.Type!=PacketType.Snapshot||packet.Tick<=LastSnapshotTick) return;
            PlayerState self=null; foreach(PlayerState p in packet.Players) if(p.Id==LocalId) self=p;
            if(self==null) { Connected=false; Status="Removed from server"; return; }
            Vec3 old=Simulation.Players[LocalId].Movement.Position;
            Prediction.Reconcile(Simulation,LocalId,self,packet.Tick);
            Vec3 delta=old-Simulation.Players[LocalId].Movement.Position;
            LastCorrection=delta.Length; Correction=delta.Length<4?Correction+delta:Vec3.Zero;
            NextSequence=Math.Max(NextSequence,self.AcknowledgedSequence+1);
            Roster.Clear(); foreach(PlayerState p in packet.Players)
            {
                Roster[p.Id]=p;
                if(p.Id==LocalId) continue;
                if(!Remote.TryGetValue(p.Id,out RemoteInterpolation remote)) { remote=new RemoteInterpolation(); Remote.Add(p.Id,remote); }
                remote.Add(packet.Tick,p.Movement);
            }
            var removed=new List<byte>(); foreach(byte id in Remote.Keys) if(!Roster.ContainsKey(id)) removed.Add(id); foreach(byte id in removed) Remote.Remove(id);
            Events.Clear(); Events.AddRange(packet.Events); ServerTick=Math.Max(ServerTick,packet.Tick); LastSnapshotTick=packet.Tick;
        }
        public void Step(PlayerInput input)
        {
            if(!Connected||Simulation==null) return;
            input.Sequence=NextSequence++; input.ServerTick=ServerTick; ServerTick++;
            Prediction.Add(input); Simulation.AdvanceTick(); Simulation.Simulate(LocalId,input,false);
            if(input.Sequence%2==0)
            {
                var packet=new NetworkPacket{Type=PacketType.Inputs,Token=token};
                int start=Math.Max(0,Prediction.Pending.Count-12);
                for(int i=start;i<Prediction.Pending.Count;i++) packet.Inputs.Add(Prediction.Pending[i]);
                if(packet.Inputs.Count>0) Transport.Send(NetworkProtocol.Encode(packet),server);
            }
        }
        public void Dispose()
        {
            if(Connected) for(int i=0;i<3;i++) Transport.Send(NetworkProtocol.Encode(new NetworkPacket{Type=PacketType.Disconnect,Token=token}),server);
            Transport.Dispose(); Connected=false;
        }
    }
}
