using System.Collections.Generic;

namespace Velocity.Core
{
    public enum MapMode : byte { Surf, Bhop, Combat }
    public enum ZoneKind : byte { Start, Checkpoint, Finish, Weapon }
    public sealed class MapZone
    {
        public ZoneKind Kind; public Vec3 Center, Size, Spawn; public int Index;
        public bool Contains(Vec3 feet) => System.Math.Abs(feet.X-Center.X)<=Size.X*.5f && System.Math.Abs(feet.Y-Center.Y)<=Size.Y*.5f && System.Math.Abs(feet.Z-Center.Z)<=Size.Z*.5f;
    }
    public sealed class MapDefinition
    {
        public byte Id; public string Name, Hint; public MapMode Mode;
        public float KillHeight;
        public Vec3 Spawn;
        public readonly CollisionWorld World=new CollisionWorld();
        public readonly List<MapZone> Zones=new List<MapZone>();
        public readonly List<Vec3> CombatSpawns=new List<Vec3>();
        public void Box(Vec3 center,Vec3 size,int material=0,float yaw=0) => World.Brushes.Add(BrushFactory.Box(center,size,material,yaw));
        public void Ramp(Vec3 center,float width,float length,float angle,float yaw=0) => World.Brushes.Add(BrushFactory.Ramp(center,width,length,angle,1,yaw));
        public void Zone(ZoneKind kind,Vec3 center,Vec3 size,int index=0,Vec3? spawn=null) => Zones.Add(new MapZone{Kind=kind,Center=center,Size=size,Index=index,Spawn=spawn??center});
    }
    public static class MapCatalog
    {
        public static readonly string[] Names={"Surf Beginner","Bhop Flow","Combat Arena"};
        public static MapDefinition Create(byte id)
        {
            if(id>2) id=0;
            var m=new MapDefinition{Id=id,Name=Names[id],Mode=(MapMode)id};
            if(id==0) Surf(m); else if(id==1) Bhop(m); else Combat(m);
            return m;
        }
        private static void Surf(MapDefinition m)
        {
            m.Hint="Launch with W. On the first ramp hold A, release W, turn gently. Space: jump. T: checkpoint.";
            m.Spawn=new Vec3(-5.5f,32.502f,-14); m.KillHeight=-48;
            m.Box(new Vec3(-4,32,-12),new Vec3(16,1,20));
            m.Ramp(new Vec3(0,22,20),14,44,55);
            m.Box(new Vec3(0,10,51),new Vec3(28,1,18),2);
            m.Ramp(new Vec3(0,0,84),14,48,-55);
            m.Box(new Vec3(0,-12,117),new Vec3(28,1,18),2);
            m.Ramp(new Vec3(0,-22,151),16,50,52);
            m.Box(new Vec3(0,-34,189),new Vec3(32,1,28),3);
            m.Zone(ZoneKind.Start,new Vec3(-4,34,-16),new Vec3(16,5,9));
            m.Zone(ZoneKind.Checkpoint,new Vec3(0,12,51),new Vec3(28,5,18),1,new Vec3(5.5f,10.502f,55));
            m.Zone(ZoneKind.Checkpoint,new Vec3(0,-10,117),new Vec3(28,5,18),2,new Vec3(-6.5f,-11.498f,121));
            m.Zone(ZoneKind.Finish,new Vec3(0,-32,190),new Vec3(32,5,18));
            // Three wide continuous ramps. Each landing is a full-width checkpoint;
            // future curved modules must pass the same collision regression suite.
        }
        private static void Bhop(MapDefinition m)
        {
            m.Hint="Hold Space for auto-bhop. Strafe and turn together. R: restart. T: checkpoint. Side route is easier.";
            m.Spawn=new Vec3(0,1.002f,-5); m.KillHeight=-8;
            m.Box(new Vec3(0,.5f,-4),new Vec3(14,1,16));
            for(int i=0;i<24;i++)
            {
                float z=8+i*6.4f, x=(i%4<2?-1:1)*2.1f;
                m.Box(new Vec3(x,.5f,z),new Vec3(4.8f,1,4.4f),i%6==5?2:0);
                m.Box(new Vec3(8,.25f,z),new Vec3(3.8f,.5f,5.5f),0);
                if(i%6==5)
                {
                    int cp=(i+1)/6;
                    m.Box(new Vec3(2,.5f,z),new Vec3(17,1,5),2);
                    m.Zone(ZoneKind.Checkpoint,new Vec3(2,2,z),new Vec3(17,4,5),cp,new Vec3(0,1.002f,z));
                }
            }
            // Raised, sparse shortcut islands are reachable after building momentum.
            for(int i=0;i<8;i++) m.Box(new Vec3(-8,1,18+i*18),new Vec3(4,2,6),1);
            m.Box(new Vec3(0,.5f,166),new Vec3(24,1,14),3);
            m.Zone(ZoneKind.Start,new Vec3(0,2,-7),new Vec3(14,4,8));
            m.Zone(ZoneKind.Finish,new Vec3(0,2,168),new Vec3(24,4,9));
        }
        private static void Combat(MapDefinition m)
        {
            m.Hint="1 rifle · 2 sniper · 3 pistol · 4 knife. LAN FFA / teams. Solo: practice targets.";
            m.KillHeight=-18; m.Spawn=new Vec3(0,1.002f,-30);
            m.Box(new Vec3(0,-1,0),new Vec3(100,2,100));
            m.Box(new Vec3(0,10,0),new Vec3(12,2,12),2);
            for(int i=0;i<4;i++)
            {
                float yaw=i*90;
                Vec3 f=MathEx.Forward(yaw),r=MathEx.Right(yaw);
                m.Ramp(f*19+Vec3.Up*7,10,30,55,yaw);
                m.Ramp(f*29+r*18+Vec3.Up*4,8,26,-55,yaw+20);
                m.Box(f*41+Vec3.Up*7,new Vec3(18,2,12),2,yaw);
                m.Box(f*34+r*22+Vec3.Up*1,new Vec3(8,2,8));
                Vec3 spawn=f*40+Vec3.Up*8.002f;
                m.CombatSpawns.Add(spawn);
                m.Zone(ZoneKind.Weapon,f*30+Vec3.Up*2,new Vec3(5,4,5),i);
            }
            m.Spawn=m.CombatSpawns[2];
            // Low stairs connect ground to elevated platforms without any speed pads.
            for(int i=0;i<30;i++) m.Box(new Vec3(-15, .125f*(i+1),-38+i*.8f),new Vec3(5,.25f*(i+1),.8f));
        }
    }
}
