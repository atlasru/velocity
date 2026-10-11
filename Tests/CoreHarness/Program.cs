using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Velocity.Core;

internal static partial class Program
{
    private static readonly List<object> Results=new List<object>();
    private static int failed;
    private static void Assert(bool condition,string message) { if(!condition) throw new Exception(message); }
    private static void Near(float a,float b,float tolerance,string message) => Assert(Math.Abs(a-b)<=tolerance,$"{message}: expected {b}, actual {a}");
    private static CollisionWorld Floor()
    {
        var w=new CollisionWorld(); w.Brushes.Add(BrushFactory.Box(new Vec3(0,-1,0),new Vec3(200,2,200))); return w;
    }
    private static void Test(string name,Action action)
    {
        try { action(); Results.Add(new {name,passed=true}); Console.WriteLine("PASS "+name); }
        catch(Exception e) { failed++; Results.Add(new {name,passed=false,error=e.Message}); Console.WriteLine("FAIL "+name+": "+e.Message); }
    }
    private static PlayerInput Input(int sequence=1,float forward=0,float side=0,InputButtons buttons=InputButtons.None,float yaw=0) => new PlayerInput{Sequence=sequence,Forward=forward,Side=side,Buttons=buttons,Yaw=yaw};
    private static MovementState Run(int frames)
    {
        var physics=new MovementPhysics(new MovementConfig()); var world=Floor(); var s=new MovementState{Position=new Vec3(0,.002f,0)};
        double accumulator=0; int tick=0;
        for(int f=0;f<frames*4;f++)
        {
            accumulator+=1d/frames;
            while(accumulator+1e-10>=1d/MovementConfig.TickRate)
            {
                PlayerInput input=Input(++tick,1,0,InputButtons.Jump|InputButtons.AutoBhop);
                input.Yaw=MathEx.WrapAngle(tick*.12f); physics.Step(ref s,input,world); accumulator-=1d/MovementConfig.TickRate;
            }
        }
        Assert(tick==480,"Incorrect fixed-timestep count"); return s;
    }
    private static int Main(string[] args)
    {
        Test("ground acceleration reaches wish speed",()=>{
            var p=new MovementPhysics(new MovementConfig()); var w=Floor(); var s=new MovementState{Position=new Vec3(0,.002f,0)};
            for(int i=0;i<120;i++) p.Step(ref s,Input(i,1),w);
            Near(s.Velocity.HorizontalSpeed,p.Config.sv_maxspeed,.01f,"ground speed"); Assert(s.Grounded,"Lost floor contact");
        });
        Test("friction stops idle ground movement",()=>{
            var p=new MovementPhysics(new MovementConfig()); var w=Floor(); var s=new MovementState{Position=new Vec3(0,.002f,0),Velocity=new Vec3(8,0,0)};
            for(int i=0;i<240;i++) p.Step(ref s,Input(i),w);
            Near(s.Velocity.HorizontalSpeed,0,.001f,"stopping speed");
        });
        Test("air acceleration caps projection without normalizing momentum",()=>{
            var v=MovementPhysics.Accelerate(new Vec3(40,0,0),new Vec3(0,0,1),8.128f,150,1f/120,.762f);
            Near(v.X,40,.00001f,"existing momentum"); Near(v.Z,.762f,.00001f,"projection cap"); Assert(v.Length>40,"No strafe gain");
        });
        Test("jump conserves horizontal momentum and skips landing friction",()=>{
            var p=new MovementPhysics(new MovementConfig()); var s=new MovementState{Position=new Vec3(0,.002f,0),Velocity=new Vec3(30,0,0)};
            p.Step(ref s,Input(1,0,0,InputButtons.Jump),Floor());
            Near(s.Velocity.X,30,.001f,"jump momentum"); Near(s.Velocity.Y,p.Config.sv_jump_impulse-p.Config.sv_gravity/120,.002f,"jump impulse"); Assert(s.Jumps==1&&!s.Grounded,"jump not airborne");
        });
        Test("manual jump needs release; automatic bhop chains",()=>{
            var p=new MovementPhysics(new MovementConfig()); var w=Floor(); var manual=new MovementState{Position=new Vec3(0,.002f,0)}; var auto=manual;
            for(int i=0;i<500;i++) { p.Step(ref manual,Input(i,0,0,InputButtons.Jump),w); p.Step(ref auto,Input(i,0,0,InputButtons.Jump|InputButtons.AutoBhop),w); }
            Assert(manual.Jumps==1,"manual repeats while held"); Assert(auto.Jumps>=5,"automatic bhop failed");
        });
        Test("swept hull does not tunnel at high speed",()=>{
            var w=new CollisionWorld(); w.Brushes.Add(BrushFactory.Box(new Vec3(0,2,0),new Vec3(.1f,6,20)));
            var s=new MovementState{Position=new Vec3(-4,1,0),Velocity=new Vec3(1000,0,0)}; var p=new MovementPhysics(new MovementConfig{sv_gravity=0});
            p.Step(ref s,Input(),w); Assert(s.Position.X<-.34f,"tunneled through thin wall"); Near(s.Velocity.X,0,.001f,"wall velocity");
        });
        Test("wall sliding retains tangential velocity",()=>{
            var w=new CollisionWorld(); w.Brushes.Add(BrushFactory.Box(new Vec3(0,2,0),new Vec3(.1f,6,20)));
            var s=new MovementState{Position=new Vec3(-.36f,1,0),Velocity=new Vec3(8,0,20)};
            new MovementPhysics(new MovementConfig{sv_gravity=0}).Step(ref s,Input(),w);
            Near(s.Velocity.Z,20,.001f,"tangent loss"); Assert(s.Position.Z>.15f,"did not slide");
        });
        Test("two-plane crease does not bounce",()=>{
            Vec3 n1=new Vec3(1,0,0), n2=new Vec3(0,0,-1);
            var v=MovementPhysics.Clip(MovementPhysics.Clip(new Vec3(-20,4,20),n1),n2);
            Near(v.X,0,.001f,"x clip"); Near(v.Z,0,.001f,"z clip"); Near(v.Y,4,.001f,"crease momentum");
        });
        Test("ramp projection retains tangent and gravity gains speed",()=>{
            var w=new CollisionWorld(); w.Brushes.Add(BrushFactory.Ramp(new Vec3(0,0,0),14,80,55));
            var p=new MovementPhysics(new MovementConfig()); var s=new MovementState{Position=new Vec3(-4,6.5f,-10),Velocity=new Vec3(0,0,20)};
            for(int i=0;i<60;i++) p.Step(ref s,Input(i),w);
            Assert(!s.Grounded,"surf ramp classified as ground"); Near(s.Velocity.Z,20,.01f,"ramp tangent"); Assert(s.Velocity.HorizontalSpeed>20,"gravity did not accelerate along ramp"); Assert(!w.Overlaps(s.Position,p.PlayerHull(s)),"ramp penetration");
        });
        Test("coplanar ramp seam conserves momentum",()=>{
            var w=new CollisionWorld(); w.Brushes.Add(BrushFactory.Ramp(new Vec3(0,0,-10),20,20,55)); w.Brushes.Add(BrushFactory.Ramp(new Vec3(0,0,10),20,20,55));
            var p=new MovementPhysics(new MovementConfig{sv_gravity=0}); var s=new MovementState{Position=new Vec3(-2,3.291f,-1),Velocity=new Vec3(0,0,30)};
            for(int i=0;i<20;i++) p.Step(ref s,Input(i),w);
            Assert(s.Position.Z>3,"blocked at seam"); Near(s.Velocity.Z,30,.01f,"seam momentum");
        });
        Test("ramp edge departure is finite and unblocked",()=>{
            var w=new CollisionWorld(); w.Brushes.Add(BrushFactory.Ramp(new Vec3(0,0,0),8,8,55)); var p=new MovementPhysics(new MovementConfig());
            var s=new MovementState{Position=new Vec3(-2,3.5f,3),Velocity=new Vec3(0,0,60)};
            for(int i=0;i<60;i++) p.Step(ref s,Input(i),w);
            Assert(s.Position.Z>25&&s.Position.Finite&&s.Velocity.Finite,"edge snag or non-finite state");
        });
        Test("low step is traversable",()=>{
            var w=Floor(); w.Brushes.Add(BrushFactory.Box(new Vec3(0,.1f,3),new Vec3(8,.2f,2))); var p=new MovementPhysics(new MovementConfig()); var s=new MovementState{Position=new Vec3(0,.002f,0)};
            for(int i=0;i<100;i++) p.Step(ref s,Input(i,1),w);
            Assert(s.Position.Z>4.5f,"failed to step up");
        });
        Test("crouch cannot stand through ceiling",()=>{
            var w=Floor(); w.Brushes.Add(BrushFactory.Box(new Vec3(0,1.5f,0),new Vec3(10,.4f,10))); var s=new MovementState{Position=new Vec3(0,.002f,0),Crouched=true};
            new MovementPhysics(new MovementConfig()).Step(ref s,Input(),w); Assert(s.Crouched,"stood through ceiling");
        });
        Test("30 60 120 240 rendering FPS yield identical fixed-step state",()=>{
            MovementState expected=Run(120);
            foreach(int fps in new[]{30,60,240}) { MovementState s=Run(fps); Near((s.Position-expected.Position).Length,0,.000001f,"position at "+fps); Near((s.Velocity-expected.Velocity).Length,0,.000001f,"velocity at "+fps); Assert(s.Jumps==expected.Jumps,"jump count differs"); }
        });
        Test("all maps have valid collision and clear spawns",()=>{
            for(byte id=0;id<3;id++) { var m=MapCatalog.Create(id); Assert(m.World.Brushes.Count>5,"empty map"); Assert(!m.World.Overlaps(m.Spawn,new Hull(.3f,1.8f)),"spawn embedded "+id); foreach(var b in m.World.Brushes) foreach(var p in b.Planes) Near(p.Normal.Length,1,.0001f,"plane unit normal"); }
        });
        Extended();
        Directory.CreateDirectory("artifacts"); File.WriteAllText("artifacts/core-tests.json",JsonSerializer.Serialize(new {total=Results.Count,failed,results=Results},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"{Results.Count-failed}/{Results.Count} passed"); return failed==0?0:1;
    }
    static partial void Extended();
}
