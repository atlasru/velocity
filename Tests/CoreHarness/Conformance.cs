using System;
using System.IO;
using System.Net;
using System.Threading;
using Velocity.Core;

internal static partial class Program
{
    private static void Conformance()
    {
        Test("slope change clips into the next plane without normalizing speed",()=>{
            var world=new CollisionWorld();
            world.Brushes.Add(BrushFactory.Ramp(new Vec3(0,0,-6),32,12,55));
            world.Brushes.Add(BrushFactory.Ramp(new Vec3(0,0,6),32,12,57));
            var physics=new MovementPhysics(new MovementConfig());
            var state=new MovementState{Position=new Vec3(0,.55f,-6),Velocity=new Vec3(0,-2,18)};
            for(int tick=0;tick<80;tick++)physics.Step(ref state,Input(tick),world);
            Assert(state.Position.Z>4&&state.Velocity.Z>15.5f,"slope transition lost longitudinal momentum: "+state.Position+" "+state.Velocity);
            Assert(!world.Overlaps(state.Position,physics.PlayerHull(state)),"transition penetrated brush");
        });
        Test("triangulated bank seam is traversable with continuous sweeps",()=>{
            var world=new CollisionWorld();float slope=(float)Math.Tan(55*Math.PI/180);
            Vec3 a=new Vec3(-5,5*slope,-8),b=new Vec3(5,-5*slope,-8),c=new Vec3(5,-5*slope,8),d=new Vec3(-5,5*slope,8);
            world.Brushes.Add(BrushFactory.TrianglePrism(a,c,b));world.Brushes.Add(BrushFactory.TrianglePrism(a,d,c));
            var physics=new MovementPhysics(new MovementConfig());
            var state=new MovementState{Position=new Vec3(0,.45f,-6),Velocity=new Vec3(0,0,16)};
            for(int tick=0;tick<60;tick++)physics.Step(ref state,Input(tick),world);
            Assert(state.Position.Z>1&&state.Velocity.Z>15.8f,"triangle seam blocked: "+state.Position+" "+state.Velocity);
            Assert(!world.Overlaps(state.Position,physics.PlayerHull(state)),"bank penetration");
        });
        Test("explicit speed limit is optional and preserves vertical motion",()=>{
            var world=new CollisionWorld();var unlimited=new MovementPhysics(new MovementConfig{sv_gravity=0});
            var limited=new MovementPhysics(new MovementConfig{sv_gravity=0,SpeedLimit=10});
            MovementState a=new MovementState{Position=new Vec3(0,20,0),Velocity=new Vec3(50,4,0)},b=a;
            unlimited.Step(ref a,Input(),world);limited.Step(ref b,Input(),world);
            Near(a.Velocity.HorizontalSpeed,50,.0001f,"default cap");Near(b.Velocity.HorizontalSpeed,10,.0001f,"configured cap");Near(b.Velocity.Y,4,.0001f,"vertical preservation");
        });
        Test("authoritative movement tuning survives the welcome packet",()=>{
            var config=MovementConfig.Preset(MapMode.Bhop);config.sv_airaccelerate=77;config.sv_gravity=25;config.sv_autobunnyhopping=false;
            var packet=new NetworkPacket{Type=PacketType.Welcome,PlayerId=2,Map=1,Tick=10,Nonce=12,Token=34,Config=config};
            byte[] data=NetworkProtocol.Encode(packet);Assert(NetworkProtocol.TryDecode(data,data.Length,out NetworkPacket read),"welcome rejected");
            Assert(config.SameRules(read.Config),"physics rules changed in transit");
        });
        Test("atomic storage survives replacement, interrupted temp and corrupt primary",()=>{
            string folder=Path.Combine(Path.GetTempPath(),"velocity-atomic-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
            try
            {
                string path=Path.Combine(folder,"settings.json");AtomicFile.Write(path,"{\"fov\":100}");AtomicFile.Write(path,"{\"fov\":112}");
                File.WriteAllText(path+".tmp","partial write");
                bool Valid(string json){try{System.Text.Json.JsonDocument.Parse(json).Dispose();return true;}catch(System.Text.Json.JsonException){return false;}}
                Assert(AtomicFile.Read(path,Valid).Contains("112"),"temp replaced primary");File.WriteAllText(path,"corrupt");
                Assert(AtomicFile.Read(path,Valid).Contains("100"),"backup recovery failed");
            }
            finally{Directory.Delete(folder,true);}
        });
        Test("lag compensation uses bounded server history",()=>{
            var sim=new GameSimulation(2);var shooter=sim.AddPlayer(1,"Shooter");var target=sim.AddPlayer(2,"Target");sim.Tick=300;
            shooter.Movement.Position=new Vec3(45,.002f,-5);target.Movement.Position=new Vec3(45,.002f,5);target.InvulnerableUntil=0;
            sim.AdvanceTick();sim.Simulate(2,Input(1));int historical=sim.Tick;target.Movement.Position=new Vec3(30,.002f,5);sim.AdvanceTick();sim.Simulate(2,Input(2));
            var shot=new Shot{Weapon=1,Damage=100,Range=30};var stale=Input();stale.ServerTick=0;DamageSystem.Apply(sim,shooter,stale,shot);Assert(target.Health==100,"unbounded rewind accepted");
            var valid=Input();valid.ServerTick=historical;DamageSystem.Apply(sim,shooter,valid,shot);Assert(target.Health==0,"historical hit not compensated");
        });
        Test("real UDP accepts eight players and rejects a ninth",EightPlayerTest);
        Test("real UDP combat is resolved by server simulation",CombatNetworkTest);
        Test("combat course commands cannot renew health, ammo or spawn protection",()=>{
            var sim=new GameSimulation(2);var p=sim.AddPlayer(1,"Authority");sim.Tick=300;
            p.Health=34;p.Weapon.Ammo[0]=7;p.InvulnerableUntil=0;
            for(int i=0;i<4;i++){sim.AdvanceTick();sim.Simulate(1,Input(i,0,0,i%2==0?InputButtons.Restart|InputButtons.Checkpoint:InputButtons.None));}
            Assert(p.Health==34&&p.Weapon.Ammo[0]==7&&p.InvulnerableUntil==0,"client command reset combat state");
        });
        Test("custom movement rules remain practice after entering the start again",()=>{
            var sim=new GameSimulation(1){CustomRules=true};var p=sim.AddPlayer(1,"Custom");
            p.Timer.Finished=true;p.Timer.Practice=true;p.Timer.WasInStart=false;p.Movement.Position=sim.Map.Spawn;
            sim.AdvanceTick();sim.Simulate(1,Input());Assert(p.Timer.Practice&&!p.Timer.Finished,"modified rules validated a record");
        });
    }
    private static void EightPlayerTest()
    {
        using(var server=new LanServer(1,"Eight players",false,0))
        {
            var clients=new LanClient[8];
            try
            {
                for(int i=0;i<clients.Length;i++)clients[i]=new LanClient("127.0.0.1","Peer "+i,server.Transport.Port);
                for(int tick=0;tick<120;tick++)
                {
                    double now=tick/120d;
                    foreach(LanClient client in clients)client.Pump(now);
                    server.Pump(now);server.Step(Input(tick));
                    foreach(LanClient client in clients){client.Pump(now);if(client.Connected)client.Step(Input(tick));}
                    if(tick%4==0)Thread.Sleep(1);
                }
                int connected=0,rejected=0;foreach(LanClient client in clients){if(client.Connected)connected++;if(client.Status=="Server full")rejected++;}
                Assert(connected==7&&rejected==1&&server.Simulation.Players.Count==8,"capacity incorrect: connected="+connected+" rejected="+rejected);
            }
            finally{foreach(LanClient client in clients)client?.Dispose();}
        }
    }
    private static void CombatNetworkTest()
    {
        using(var server=new LanServer(2,"Combat",false,0))
        using(var client=new LanClient("127.0.0.1","Shooter",server.Transport.Port))
        {
            double now=0;
            for(int i=0;i<60&&!client.Connected;i++){now+=.01;client.Pump(now);server.Pump(now);client.Pump(now);Thread.Sleep(1);}
            Assert(client.Connected,"combat handshake failed");server.Simulation.Tick=300;
            PlayerState target=server.Simulation.Players[1],shooter=server.Simulation.Players[client.LocalId];
            target.Movement=new MovementState{Position=new Vec3(45,.002f,5)};target.InvulnerableUntil=0;
            shooter.Movement=new MovementState{Position=new Vec3(45,.002f,-5)};shooter.InvulnerableUntil=0;
            for(int tick=0;tick<80;tick++)
            {
                now+=1d/120;client.Pump(now);server.Pump(now);
                PlayerInput input=Input(tick,0,0,tick>=32&&tick<36?InputButtons.Fire:InputButtons.None);input.Weapon=1;
                client.Step(input);server.Step(Input(tick));server.Pump(now);client.Pump(now);
                if(tick%4==0)Thread.Sleep(1);
            }
            Assert(target.Health==0&&shooter.Kills==1&&server.Simulation.Events.Count==1,"server did not resolve remote hitscan");
            Assert(client.Roster.ContainsKey(1)&&client.Roster[1].Health==0&&client.Events.Count==1,"kill state did not replicate");
        }
    }
    private static void RestartNetworkTest()
    {
        var first=new LanServer(1,"Before restart",false,0);int port=first.Transport.Port;
        using(var client=new LanClient("127.0.0.1","Survivor",port))
        {
            try
            {
                for(int tick=0;tick<100;tick++)
                {
                    double now=tick/120d;client.Pump(now);first.Pump(now);first.Step(Input(tick));client.Pump(now);
                    if(client.Connected)client.Step(Input(tick,1));
                    if(tick%4==0)Thread.Sleep(1);
                }
                Assert(client.Connected&&client.NextSequence>20,"initial prediction was not established");
            }
            finally{first.Transport.DropForTesting=(data,endpoint)=>true;first.Dispose();}
            using(var restarted=new LanServer(0,"After restart",false,port))
            {
                restarted.Simulation.Movement.Config.sv_airaccelerate=77;
                for(int tick=0;tick<100;tick++)
                {
                    double now=12+tick*.02;client.Pump(now);restarted.Pump(now);client.Pump(now);
                    if(client.Connected&&client.Simulation.Map.Id==0)break;
                    Thread.Sleep(1);
                }
                Assert(client.Connected&&client.Simulation.Map.Id==0,"did not reconnect to restarted server");
                Assert(client.NextSequence==0&&client.Prediction.Pending.Count==0&&client.Simulation.Movement.Config.sv_airaccelerate==77,"stale prediction or rules survived new token");
            }
        }
    }
}
