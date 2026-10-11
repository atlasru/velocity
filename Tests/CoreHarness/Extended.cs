using System;
using System.Net;
using System.Threading;
using Velocity.Core;

internal static partial class Program
{
    static partial void Extended()
    {
        Test("Surf Beginner is completable through movement inputs without teleports",()=>{
            var sim=new GameSimulation(0); var p=sim.AddPlayer(1,"Surf pilot");
            for(int tick=0;tick<6500&&!p.Timer.Finished;tick++)
            {
                float z=p.Movement.Position.Z;
                bool ramp=z>-2&&z<42||z>60&&z<108||z>126&&z<176;
                float side=ramp?(z>60&&z<108?1:-1):0;
                var input=Input(tick,ramp?0:1,side); sim.AdvanceTick(); sim.Simulate(1,input);
            }
            Assert(p.Timer.Finished&&!p.Timer.Practice&&p.Timer.Checkpoint==2,"surf pilot failed: "+p.Movement.Position+" checkpoint "+p.Timer.Checkpoint+" practice "+p.Timer.Practice);
        });
        Test("Bhop Flow is completable using manual jump timing",()=>{
            var sim=new GameSimulation(1); var p=sim.AddPlayer(1,"Bhop pilot");
            for(int tick=0;tick<5000&&!p.Timer.Finished;tick++)
            {
                var m=p.Movement; float edge=4;
                if(m.Position.Z>4) for(int i=0;i<24;i++) { float center=8+i*6.4f; if(m.Position.Z>=center-2.5f&&m.Position.Z<=center+2.5f) {edge=center+2.2f;break;} }
                bool jump=m.Grounded&&m.Position.Z>=edge-.6f;
                var input=Input(tick,1,0,jump?InputButtons.Jump:InputButtons.None);sim.AdvanceTick();sim.Simulate(1,input);
            }
            Assert(p.Timer.Finished&&!p.Timer.Practice&&p.Timer.Checkpoint==4,"bhop pilot failed: "+p.Movement.Position+" checkpoint "+p.Timer.Checkpoint+" practice "+p.Timer.Practice);
        });
        Test("timer uses simulation ticks and ignores incomplete courses",()=>{
            var sim=new GameSimulation(1); var p=sim.AddPlayer(1,"Timer"); p.Movement.Position=new Vec3(0,1,-2);
            TimerSystem.Update(p,sim.Map,100); Assert(p.Timer.Active,"start exit failed");
            p.Movement.Position=new Vec3(0,1,168); TimerSystem.Update(p,sim.Map,1300); Assert(!p.Timer.Finished,"finish without checkpoints");
            p.Timer.Checkpoint=4; TimerSystem.Update(p,sim.Map,1300); Assert(p.Timer.Finished,"finish not detected"); Near(p.Timer.Seconds,10,.000001f,"timer precision");
        });
        Test("checkpoint teleport marks practice and preserves progress",()=>{
            var sim=new GameSimulation(0); var p=sim.AddPlayer(1,"Checkpoint"); p.Timer.Checkpoint=1; sim.Restart(p,true);
            Assert(p.Timer.Practice&&p.Timer.Checkpoint==1,"checkpoint reset progress or validated practice record");
            Near(p.Movement.Position.Z,55,.001f,"checkpoint spawn"); Assert(!sim.Map.World.Overlaps(p.Movement.Position,sim.Movement.PlayerHull(p.Movement)),"invalid checkpoint hull");
        });
        Test("weapon cadence, ammo and reload cannot be bypassed",()=>{
            var w=new WeaponState(); var input=Input(0,0,0,InputButtons.Fire); int shots=0;
            for(int tick=0;tick<120;tick++) if(WeaponSystem.Step(w,input,tick).HasValue) shots++;
            Assert(shots==10&&w.Ammo[0]==20,"incorrect rifle cadence");
            input.Buttons=InputButtons.Reload|InputButtons.Fire; WeaponSystem.Step(w,input,120); Assert(w.ReloadFinishTick==384,"incorrect reload timing");
            Assert(!WeaponSystem.Step(w,input,383).HasValue,"fired during reload"); input.Buttons=InputButtons.None; WeaponSystem.Step(w,input,384);
            Assert(w.Ammo[0]==30&&w.Reserve[0]==170,"incorrect reload transfer");
        });
        Test("pistol requires a fresh trigger; recoil is reproducible",()=>{
            var a=new WeaponState{Selected=2}; var b=a.Clone(); var input=Input(0,0,0,InputButtons.Fire); input.Weapon=2;
            int shots=0;
            for(int i=0;i<120;i++) { Shot? sa=WeaponSystem.Step(a,input,i),sb=WeaponSystem.Step(b,input,i); if(sa.HasValue) { shots++; Near(sa.Value.RecoilSide,sb.Value.RecoilSide,.000001f,"recoil"); } }
            Assert(shots==1,"semi-auto repeated while held");
        });
        Test("hitscan server damage, team filtering and respawn",()=>{
            var sim=new GameSimulation(2); var shooter=sim.AddPlayer(1,"One"); var victim=sim.AddPlayer(2,"Two");
            shooter.Movement.Position=new Vec3(45,.002f,-5); victim.Movement.Position=new Vec3(45,.002f,5); shooter.InvulnerableUntil=victim.InvulnerableUntil=0; sim.Tick=300;
            var shot=new Shot{Weapon=1,Damage=100,Range=30}; DamageSystem.Apply(sim,shooter,Input(0),shot);
            Assert(victim.Health==0&&shooter.Kills==1&&victim.Deaths==1&&sim.Events.Count==1,"server kill failed");
            sim.Tick=victim.RespawnTick; sim.Simulate(victim.Id,Input()); Assert(victim.Health==100,"respawn failed");
            sim.Teams=true; victim.Team=shooter.Team; victim.InvulnerableUntil=0; victim.Movement.Position=new Vec3(45,.002f,5);
            DamageSystem.Apply(sim,shooter,Input(0),shot); Assert(victim.Health==100,"friendly fire allowed");
        });
        Test("world geometry blocks shots",()=>{
            var sim=new GameSimulation(2); var a=sim.AddPlayer(1,"A"); var b=sim.AddPlayer(2,"B"); sim.Tick=300; a.InvulnerableUntil=b.InvulnerableUntil=0;
            a.Movement.Position=new Vec3(0,.002f,-20); b.Movement.Position=new Vec3(0,.002f,20);
            DamageSystem.Apply(sim,a,Input(),new Shot{Damage=100,Range=100}); Assert(b.Health==100,"shot crossed world brush");
        });
        Test("multi-touch joystick, look and jump retain separate owners",()=>{
            var m=new MobileControls();
            m.Update(new[]{new TouchPoint{Id=1,X=.15f,Y=.2f},new TouchPoint{Id=2,X=.6f,Y=.5f},new TouchPoint{Id=3,X=.88f,Y=.29f}},2,false);
            Assert(m.Buttons==InputButtons.Jump,"spurious first-touch button");
            m.Update(new[]{new TouchPoint{Id=1,X=.18f,Y=.3f},new TouchPoint{Id=2,X=.65f,Y=.53f},new TouchPoint{Id=3,X=.87f,Y=.29f}},2,false);
            Assert(m.Forward>.5f&&m.Side>0&&m.LookX>0&&m.HasJump(),"multi-touch ownership lost");
            m.Update(new[]{new TouchPoint{Id=1,Ended=true},new TouchPoint{Id=2,X=.68f,Y=.53f},new TouchPoint{Id=3,X=.87f,Y=.29f}},2,false);
            Assert(m.Side==0&&m.Forward==0&&m.LookX>0&&m.HasJump(),"release cancelled unrelated touch");
        });
        Test("mobile surf assist changes input only",()=>{
            var m=new MobileControls{Assist=true}; m.Update(new[]{new TouchPoint{Id=1,X=.1f,Y=.2f}},2,false);
            m.Update(new[]{new TouchPoint{Id=1,X=.14f,Y=.21f}},2,false); Assert(m.Side>.8f&&m.Forward==0&&m.LookX==0&&m.LookY==0,"assist steers camera or forces forward");
        });
        Test("protocol roundtrip preserves reconciliation state within MTU",()=>{
            var packet=new NetworkPacket{Type=PacketType.Snapshot,Token=42,Tick=100}; var sim=new GameSimulation(0);
            for(byte id=1;id<=8;id++) { PlayerState p=sim.AddPlayer(id,"ИгрокИгрокИгрок"); p.Movement.Velocity=new Vec3(40,-3,70); p.Weapon.ReloadFinishTick=345; p.Timer.Active=true; packet.Players.Add(p); }
            for(int i=0;i<4;i++) packet.Events.Add(new GameEvent{Id=i,Tick=100,Killer=1,Victim=2,Weapon=0});
            byte[] bytes=NetworkProtocol.Encode(packet); Assert(bytes.Length<=1400,"fragmenting snapshot"); Assert(NetworkProtocol.TryDecode(bytes,bytes.Length,out NetworkPacket decoded),"roundtrip decode failed");
            Assert(decoded.Players.Count==8&&decoded.Players[3].Weapon.ReloadFinishTick==345,"state loss"); Near(decoded.Players[0].Movement.Velocity.Z,70,.000001f,"velocity loss");
        });
        Test("malformed, oversized and nonfinite packets are rejected",()=>{
            byte[] valid=NetworkProtocol.Encode(new NetworkPacket{Type=PacketType.Hello,Nonce=1,Name="Test"});
            for(int len=0;len<valid.Length;len++) Assert(!NetworkProtocol.TryDecode(valid,len,out _),"accepted truncated packet "+len);
            Assert(!NetworkProtocol.TryDecode(new byte[1401],1401,out _),"accepted oversized packet");
            var p=new NetworkPacket{Type=PacketType.Inputs}; p.Inputs.Add(new PlayerInput{Side=float.NaN}); byte[] invalid=NetworkProtocol.Encode(p);
            Assert(!NetworkProtocol.TryDecode(invalid,invalid.Length,out _),"accepted NaN input");
        });
        Test("input queue bounds speed and ignores duplicates and stale inputs",()=>{
            var q=new ServerInputQueue(); Assert(!q.Add(Input(10000)),"accepted distant sequence");
            for(int i=0;i<4;i++) { Assert(q.Add(Input(i,1)),"refused valid input"); q.Add(Input(i,1)); }
            for(int i=0;i<4;i++) Assert(q.Consume(i).Sequence==i,"duplicate advanced simulation");
            Assert(!q.Add(Input(0)),"accepted stale input"); Assert(q.Consume(5).Sequence==4,"packet gap stalled simulation");
        });
        Test("prediction replays unacknowledged inputs exactly",()=>{
            var server=new GameSimulation(1); var authoritative=server.AddPlayer(1,"Predict"); var prediction=new GameSimulation(1); prediction.AddPlayer(1,"Predict"); var buffer=new PredictionBuffer();
            PlayerState snapshot=null;
            for(int tick=0;tick<120;tick++)
            {
                var input=Input(tick,1,0,InputButtons.Jump|InputButtons.AutoBhop); server.AdvanceTick(); server.Simulate(1,input); prediction.AdvanceTick(); prediction.Simulate(1,input,false); buffer.Add(input);
                if(tick==89) snapshot=authoritative.Clone();
            }
            buffer.Reconcile(prediction,1,snapshot,90);
            Near((prediction.Players[1].Movement.Position-authoritative.Movement.Position).Length,0,.000001f,"prediction position");
            Assert(buffer.Pending.Count==30,"pending acknowledgement incorrect");
        });
        Test("remote interpolation wraps angles and limits extrapolation",()=>{
            var remote=new RemoteInterpolation(); remote.Add(0,new MovementState{Position=Vec3.Zero,Yaw=179,Velocity=new Vec3(12,0,0)}); remote.Add(4,new MovementState{Position=new Vec3(.4f,0,0),Yaw=-179,Velocity=new Vec3(12,0,0)});
            Near(remote.At(2).Position.X,.2f,.00001f,"interpolation"); Near(Math.Abs(remote.At(2).Yaw),180,.001f,"yaw wrap"); Near(remote.At(100).Position.X,.7f,.001f,"extrapolation limit");
        });
        Test("real UDP manual join, loss recovery, movement and reconnect",NetworkTest);
        Test("real UDP reconnect resets prediction after host restart",RestartNetworkTest);
        Conformance();
        Test("real UDP discovery answers a manual directed query",()=>{
            using(var server=new LanServer(1,"Discovery",false,0))
            using(var host=new LanDiscovery(server))
            using(var browser=new LanDiscovery())
            {
                browser.Query(new IPEndPoint(IPAddress.Loopback,LanDiscovery.DiscoveryPort));
                for(int i=0;i<50&&browser.Servers.Count==0;i++) { host.Pump(i*.01); browser.Pump(i*.01); Thread.Sleep(1); }
                Assert(browser.Servers.Count>=1,"UDP discovery failed");
            }
        });
    }
    private static void NetworkTest()
    {
        using(var server=new LanServer(1,"Loopback host",false,0))
        {
            using(var client=new LanClient("127.0.0.1","Loopback client",server.Transport.Port))
            {
                int counter=0;
                client.Transport.DropForTesting=(data,endpoint)=>++counter%7==0;
                server.Transport.DropForTesting=(data,endpoint)=>++counter%11==0;
                double now=0; float observedMax=0; int observedJumps=0;
                for(int tick=0;tick<900;tick++)
                {
                    now+=1d/120; client.Pump(now); server.Pump(now);
                    if(client.Connected) client.Step(Input(tick,1,0,tick<120?InputButtons.None:InputButtons.Jump|InputButtons.AutoBhop));
                    server.Step(Input(tick)); server.Pump(now); client.Pump(now);
                    if(client.Connected&&server.Simulation.Players.TryGetValue(client.LocalId,out PlayerState observed)) { observedMax=Math.Max(observedMax,observed.Movement.MaxSpeed); observedJumps=Math.Max(observedJumps,observed.Movement.Jumps); }
                    if(tick%4==0) Thread.Sleep(1);
                }
                Assert(client.Connected&&client.LastSnapshotTick>800,"join or snapshot delivery failed");
                Assert(server.Simulation.Players.Count==2,"player registration failed");
                PlayerState remote=server.Simulation.Players[client.LocalId];
                Assert(observedMax>8&&observedJumps>2,"movement was not simulated from UDP inputs: max="+remote.Movement.MaxSpeed+" jumps="+remote.Movement.Jumps+" pos="+remote.Movement.Position+" ack="+remote.AcknowledgedSequence);
                Assert(client.LastCorrection<1,"large reconciliation correction: "+client.LastCorrection);
                client.Transport.DropForTesting=null;
            }
            server.Pump(8);
            Assert(server.Simulation.Players.Count==1,"disconnect was not processed");
            using(var reconnect=new LanClient("127.0.0.1","Reconnect",server.Transport.Port))
            {
                for(int i=0;i<100&&!reconnect.Connected;i++) { double now=9+i*.01; reconnect.Pump(now); server.Pump(now); reconnect.Pump(now); Thread.Sleep(1); }
                Assert(reconnect.Connected&&server.Simulation.Players.Count==2,"reconnect failed");
            }
        }
    }
}
internal static class TouchChecks { public static bool HasJump(this MobileControls input) => (input.Buttons&InputButtons.Jump)!=0; }
