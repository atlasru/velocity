using System;

namespace Velocity.Core
{
    [Flags]
    public enum InputButtons : ushort { None=0, Jump=1, Crouch=2, Walk=4, Fire=8, Aim=16, Reload=32, Restart=64, Checkpoint=128, AutoBhop=256 }

    public struct PlayerInput
    {
        public int Sequence, ServerTick;
        public float Side, Forward, Yaw, Pitch;
        public InputButtons Buttons;
        public byte Weapon;
        public bool Has(InputButtons b) => (Buttons & b) != 0;
        public bool Valid => Sequence >= 0 && MathEx.Finite(Side) && MathEx.Finite(Forward) && MathEx.Finite(Yaw) && MathEx.Finite(Pitch) && Math.Abs(Side) <= 1.001f && Math.Abs(Forward) <= 1.001f && Math.Abs(Yaw) <= 180.01f && Math.Abs(Pitch) <= 89.01f && Weapon < 4 && ((ushort)Buttons & ~511) == 0;
    }

    [Serializable]
    public sealed class MovementConfig
    {
        // Units: metres, seconds. Source units are converted using 0.0254 m/unit.
        public float sv_gravity=20.32f, sv_accelerate=10, sv_airaccelerate=150, sv_friction=4,
            sv_maxspeed=8.128f, sv_stopspeed=2.54f, sv_jump_impulse=6.8072f, sv_air_max_wishspeed=.762f;
        public bool sv_autobunnyhopping=true;
        public float SpeedLimit=0, GroundNormal=.7f, StepHeight=.3f, Radius=.3f, StandingHeight=1.8f, CrouchHeight=1.05f;
        public const int TickRate=120;
        public const float TickDelta=1f/TickRate;
        public static MovementConfig Preset(MapMode mode) => new MovementConfig { sv_airaccelerate=mode==MapMode.Bhop?100:150 };
    }

    [Serializable]
    public struct MovementState
    {
        public Vec3 Position, Velocity, GroundPlane;
        public float Yaw, Pitch, MaxSpeed;
        public bool Grounded, Crouched, JumpHeld;
        public int Jumps, AirTicks, SyncTicks;
        public float SyncPercent => AirTicks==0?0:100f*SyncTicks/AirTicks;
    }

    public sealed class MovementPhysics
    {
        public readonly MovementConfig Config;
        private readonly Vec3[] planes = new Vec3[8];
        public MovementPhysics(MovementConfig config) { Config=config; }
        public Hull PlayerHull(MovementState s) => new Hull(Config.Radius, s.Crouched?Config.CrouchHeight:Config.StandingHeight);
        public static Vec3 Clip(Vec3 velocity, Vec3 normal)
        {
            float into = Vec3.Dot(velocity, normal);
            if (into >= 0) return velocity;
            Vec3 result=velocity-normal*into;
            if(Math.Abs(result.X)<.00001f) result.X=0;
            if(Math.Abs(result.Y)<.00001f) result.Y=0;
            if(Math.Abs(result.Z)<.00001f) result.Z=0;
            return result;
        }
        public static Vec3 Accelerate(Vec3 velocity, Vec3 wishDirection, float wishSpeed, float acceleration, float dt, float projectionLimit)
        {
            float add=projectionLimit-Vec3.Dot(velocity,wishDirection);
            if(add<=0) return velocity;
            return velocity+wishDirection*Math.Min(add,acceleration*wishSpeed*dt);
        }
        public Vec3 Friction(Vec3 v, float dt)
        {
            float speed=v.HorizontalSpeed;
            if(speed<.0001f) return new Vec3(0,v.Y,0);
            float ratio=Math.Max(0,speed-Math.Max(speed,Config.sv_stopspeed)*Config.sv_friction*dt)/speed;
            return new Vec3(v.X*ratio,v.Y,v.Z*ratio);
        }
        public void Step(ref MovementState s, PlayerInput input, IMovementWorld world, float dt=MovementConfig.TickDelta)
        {
            s.Yaw=input.Yaw; s.Pitch=input.Pitch;
            if(input.Has(InputButtons.Crouch)) s.Crouched=true;
            else if(s.Crouched&&!world.Overlaps(s.Position,new Hull(Config.Radius,Config.StandingHeight))) s.Crouched=false;
            Hull hull=PlayerHull(s);
            Categorize(ref s,world,hull);
            bool jump=input.Has(InputButtons.Jump);
            bool wantsJump=jump&&(!s.JumpHeld||(Config.sv_autobunnyhopping&&input.Has(InputButtons.AutoBhop)));
            bool wasGrounded=s.Grounded;
            if(wantsJump&&s.Grounded) { s.Grounded=false; s.Velocity.Y=Config.sv_jump_impulse; s.Jumps++; }
            s.JumpHeld=jump;
            Vec3 wish=MathEx.Right(input.Yaw)*input.Side+MathEx.Forward(input.Yaw)*input.Forward;
            float amount=Math.Min(1,wish.Length);
            float wishSpeed=Config.sv_maxspeed*amount*(input.Has(InputButtons.Walk)?.45f:1)*(s.Crouched?.34f:1);
            Vec3 direction=wish.Normalized;
            if(s.Grounded)
            {
                s.Velocity=Friction(s.Velocity,dt);
                s.Velocity=Accelerate(s.Velocity,direction,wishSpeed,Config.sv_accelerate,dt,wishSpeed);
                s.Velocity=Clip(s.Velocity,s.GroundPlane);
            }
            else
            {
                s.Velocity.Y-=Config.sv_gravity*dt*.5f;
                float before=s.Velocity.HorizontalSpeed;
                // The Source cap limits the projection, not the total momentum.
                s.Velocity=Accelerate(s.Velocity,direction,wishSpeed,Config.sv_airaccelerate,dt,Math.Min(wishSpeed,Config.sv_air_max_wishspeed));
                if(amount>.05f) { s.AirTicks++; if(s.Velocity.HorizontalSpeed>before+.00001f) s.SyncTicks++; }
            }
            Vec3 start=s.Position, startVelocity=s.Velocity;
            Vec3 contact=Slide(ref s,world,hull,dt);
            // A step is accepted only when it lands on a walkable floor and travels further.
            if(wasGrounded&&!wantsJump&&Config.StepHeight>0 && (s.Position-start).HorizontalSpeed+.001f<(startVelocity*dt).HorizontalSpeed)
            {
                MovementState stepped=s; stepped.Position=start; stepped.Velocity=startVelocity;
                Trace up=world.Sweep(start,Vec3.Up*Config.StepHeight,hull);
                if(!up.StartSolid&&up.Fraction==1)
                {
                    stepped.Position+=Vec3.Up*Config.StepHeight;
                    Slide(ref stepped,world,hull,dt);
                    Trace down=world.Sweep(stepped.Position,-Vec3.Up*(Config.StepHeight+.05f),hull);
                    if(down.Fraction<1&&down.Normal.Y>=Config.GroundNormal)
                    {
                        stepped.Position-=Vec3.Up*(Config.StepHeight+.05f)*down.Fraction;
                        if((stepped.Position-start).HorizontalSpeed>(s.Position-start).HorizontalSpeed+.0001f) { s=stepped; contact=down.Normal; }
                    }
                }
            }
            if(!s.Grounded)
            {
                s.Velocity.Y-=Config.sv_gravity*dt*.5f;
                if(contact.Y>0) s.Velocity=Clip(s.Velocity,contact);
            }
            Categorize(ref s,world,hull);
            if(s.Grounded) s.Velocity=Clip(s.Velocity,s.GroundPlane);
            float horizontal=s.Velocity.HorizontalSpeed;
            if(Config.SpeedLimit>0&&horizontal>Config.SpeedLimit)
            { float f=Config.SpeedLimit/horizontal; s.Velocity.X*=f; s.Velocity.Z*=f; }
            s.MaxSpeed=Math.Max(s.MaxSpeed,s.Velocity.HorizontalSpeed);
            if(!s.Position.Finite||!s.Velocity.Finite) throw new InvalidOperationException("Non-finite movement state");
        }
        private void Categorize(ref MovementState s, IMovementWorld world, Hull hull)
        {
            s.Grounded=false; s.GroundPlane=Vec3.Zero;
            if(s.Velocity.Y>1) return;
            Trace t=world.Sweep(s.Position,-Vec3.Up*.055f,hull);
            if(!t.StartSolid&&t.Fraction<1&&t.Normal.Y>=Config.GroundNormal)
            { s.Position-=Vec3.Up*.055f*t.Fraction; s.Grounded=true; s.GroundPlane=t.Normal; }
        }
        private Vec3 Slide(ref MovementState s, IMovementWorld world, Hull hull, float dt)
        {
            float remaining=dt; int count=0; Vec3 contact=Vec3.Zero, primal=s.Velocity;
            for(int bump=0;bump<8&&remaining>.0000001f;bump++)
            {
                Trace t=world.Sweep(s.Position,s.Velocity*remaining,hull);
                if(t.StartSolid&&t.Fraction==1) { s.Velocity=Vec3.Zero; break; }
                s.Position+=s.Velocity*(remaining*t.Fraction);
                if(t.Fraction>=1) break;
                remaining*=1-t.Fraction;
                if(t.Normal.LengthSquared<.9f) { s.Velocity=Vec3.Zero; break; }
                if(t.Normal.Y>contact.Y) contact=t.Normal;
                bool duplicate=false;
                for(int i=0;i<count;i++) if(Vec3.Dot(planes[i],t.Normal)>.999f) duplicate=true;
                if(!duplicate) planes[count++]=t.Normal;
                Vec3 incoming=s.Velocity, candidate=Vec3.Zero;
                bool found=false;
                for(int i=0;i<count;i++)
                {
                    candidate=Clip(incoming,planes[i]); bool valid=true;
                    for(int j=0;j<count;j++) if(j!=i&&Vec3.Dot(candidate,planes[j])<-.00001f) { valid=false; break; }
                    if(valid) { found=true; break; }
                }
                if(!found&&count==2)
                { Vec3 crease=Vec3.Cross(planes[0],planes[1]).Normalized; candidate=crease*Vec3.Dot(incoming,crease); found=true; }
                s.Velocity=found?candidate:Vec3.Zero;
                if(Vec3.Dot(s.Velocity,primal)<=0) { s.Velocity=Vec3.Zero; break; }
            }
            return contact;
        }
    }
}
