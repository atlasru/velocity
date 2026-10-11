using System;
using System.Collections.Generic;

namespace Velocity.Core
{
    [Serializable]
    public struct TimerState
    {
        public int StartTick, ElapsedTicks, FinishSerial;
        public byte Checkpoint;
        public bool Active, Finished, Practice, WasInStart;
        public float Seconds => ElapsedTicks/(float)MovementConfig.TickRate;
    }

    public sealed class PlayerState
    {
        public byte Id, Team;
        public string Name="Player";
        public int AcknowledgedSequence=-1, Health=100, Kills, Deaths, RespawnTick, InvulnerableUntil;
        public MovementState Movement;
        public TimerState Timer;
        public WeaponState Weapon=new WeaponState();
        public InputButtons PreviousButtons;
        public PlayerState Clone()
        {
            var p=(PlayerState)MemberwiseClone(); p.Weapon=Weapon.Clone(); return p;
        }
    }

    public struct GameEvent
    {
        public int Id, Tick;
        public byte Killer, Victim, Weapon;
    }

    public sealed class GameSimulation
    {
        public readonly MapDefinition Map;
        public readonly MovementPhysics Movement;
        public readonly Dictionary<byte,PlayerState> Players=new Dictionary<byte,PlayerState>();
        public readonly List<GameEvent> Events=new List<GameEvent>();
        private readonly Dictionary<byte,HistorySample[]> history=new Dictionary<byte,HistorySample[]>();
        public bool Teams,CustomRules;
        public int Tick;
        private int eventId;
        private struct HistorySample { public int Tick; public Vec3 Position; public bool Crouched; }
        public GameSimulation(byte map,bool teams=false) { Map=MapCatalog.Create(map); Movement=new MovementPhysics(MovementConfig.Preset(Map.Mode)); Teams=teams; }
        public PlayerState AddPlayer(byte id,string name)
        {
            var p=new PlayerState{Id=id,Name=CleanName(name),Team=(byte)((id-1)%2)};
            Players.Add(id,p); history[id]=new HistorySample[32]; Restart(p,false); return p;
        }
        public void RemovePlayer(byte id) { Players.Remove(id); history.Remove(id); }
        public static string CleanName(string name)
        {
            if(string.IsNullOrWhiteSpace(name)) return "Player";
            string result="";
            foreach(char c in name) if(!char.IsControl(c)&&c!='<'&&c!='>') { result+=c; if(result.Length>=16) break; }
            return result.Length==0?"Player":result;
        }
        public void AdvanceTick() { Tick++; }
        public void Simulate(byte id,PlayerInput input,bool authoritative=true)
        {
            if(!Players.TryGetValue(id,out PlayerState p)||!input.Valid) return;
            p.AcknowledgedSequence=input.Sequence;
            if(p.Health<=0)
            {
                if(Tick>=p.RespawnTick) Restart(p,false);
                else return;
            }
            InputButtons pressed=input.Buttons&~p.PreviousButtons;
            p.PreviousButtons=input.Buttons;
            // Course commands must never refill health/ammo or renew spawn protection in combat.
            if((pressed&InputButtons.Restart)!=0&&Map.Mode!=MapMode.Combat) Restart(p,false);
            if((pressed&InputButtons.Checkpoint)!=0&&Map.Mode!=MapMode.Combat) Restart(p,true);
            Movement.Step(ref p.Movement,input,Map.World);
            if(p.Movement.Position.Y<Map.KillHeight)
            {
                if(Map.Mode==MapMode.Combat) { p.Health=0; p.Deaths++; p.RespawnTick=Tick+240; }
                else Restart(p,true);
            }
            if(Map.Mode!=MapMode.Combat)
            {
                TimerSystem.Update(p,Map,Tick);
                if(CustomRules) p.Timer.Practice=true;
            }
            else
            {
                foreach(MapZone zone in Map.Zones)
                    if(zone.Kind==ZoneKind.Weapon&&zone.Contains(p.Movement.Position)) p.Weapon.Refill();
                Shot? shot=WeaponSystem.Step(p.Weapon,input,Tick);
                if(shot.HasValue&&authoritative) DamageSystem.Apply(this,p,input,shot.Value);
            }
            HistorySample[] samples=history[id];
            samples[Tick%samples.Length]=new HistorySample{Tick=Tick,Position=p.Movement.Position,Crouched=p.Movement.Crouched};
        }
        public void Restart(PlayerState p,bool checkpoint)
        {
            Vec3 spawn=Map.Spawn;
            if(Map.Mode==MapMode.Combat&&Map.CombatSpawns.Count>0) spawn=Map.CombatSpawns[(p.Id-1+p.Deaths)%Map.CombatSpawns.Count];
            else if(checkpoint) foreach(MapZone z in Map.Zones) if(z.Kind==ZoneKind.Checkpoint&&z.Index==p.Timer.Checkpoint) spawn=z.Spawn;
            int finishSerial=p.Timer.FinishSerial;
            byte cp=checkpoint?p.Timer.Checkpoint:(byte)0;
            p.Movement=new MovementState{Position=spawn,Yaw=Map.Mode==MapMode.Combat?MathEx.WrapAngle(90*(p.Id-1+p.Deaths)+180):0};
            p.Timer=new TimerState{StartTick=-1,Checkpoint=cp,Practice=checkpoint||CustomRules,WasInStart=!checkpoint,FinishSerial=finishSerial};
            p.Health=100; p.Weapon=new WeaponState(); p.InvulnerableUntil=Tick+120;
        }
        internal Vec3 HistoricalPosition(PlayerState p,int tick,out bool crouched)
        {
            HistorySample s=history[p.Id][Math.Max(0,tick)%32];
            if(s.Tick==tick) { crouched=s.Crouched; return s.Position; }
            crouched=p.Movement.Crouched; return p.Movement.Position;
        }
        internal void Kill(PlayerState killer,PlayerState victim,byte weapon)
        {
            victim.Health=0; victim.Deaths++; victim.RespawnTick=Tick+240; killer.Kills++;
            Events.Add(new GameEvent{Id=++eventId,Tick=Tick,Killer=killer.Id,Victim=victim.Id,Weapon=weapon});
            if(Events.Count>4) Events.RemoveAt(0);
        }
    }

    public static class TimerSystem
    {
        public static void Update(PlayerState p,MapDefinition map,int tick)
        {
            TimerState t=p.Timer; bool start=false; int required=0;
            foreach(MapZone z in map.Zones)
            {
                if(z.Kind==ZoneKind.Start&&z.Contains(p.Movement.Position)) start=true;
                if(z.Kind==ZoneKind.Checkpoint) required=Math.Max(required,z.Index);
            }
            if(t.WasInStart&&!start&&!t.Finished) { t.Active=true; t.StartTick=tick; }
            if(start&&t.Finished) { t.Active=false; t.Finished=false; t.StartTick=-1; t.ElapsedTicks=0; t.Checkpoint=0; t.Practice=false; }
            t.WasInStart=start;
            if(t.Practice&&!start&&!t.Active&&!t.Finished) { t.Active=true; t.StartTick=tick; }
            if(t.Active) t.ElapsedTicks=tick-t.StartTick;
            foreach(MapZone z in map.Zones)
            {
                if(!z.Contains(p.Movement.Position)) continue;
                if(z.Kind==ZoneKind.Checkpoint&&z.Index==t.Checkpoint+1) t.Checkpoint=(byte)z.Index;
                if(z.Kind==ZoneKind.Finish&&t.Active&&t.Checkpoint==required)
                { t.Active=false; t.Finished=true; t.FinishSerial++; }
            }
            p.Timer=t;
        }
    }

    public static class DamageSystem
    {
        public static void Apply(GameSimulation sim,PlayerState shooter,PlayerInput input,Shot shot)
        {
            Vec3 origin=shooter.Movement.Position+Vec3.Up*(shooter.Movement.Crouched?.9f:1.62f);
            Vec3 direction=MathEx.Aim(input.Yaw+shot.RecoilSide,input.Pitch-shot.RecoilUp).Normalized;
            float closest=sim.Map.World.Raycast(origin,direction,shot.Range);
            PlayerState target=null;
            int rewind=MathEx.Clamp(input.ServerTick,Math.Max(0,sim.Tick-24),sim.Tick);
            foreach(PlayerState victim in sim.Players.Values)
            {
                if(victim.Id==shooter.Id||victim.Health<=0||victim.InvulnerableUntil>sim.Tick||(sim.Teams&&victim.Team==shooter.Team)) continue;
                Vec3 position=sim.HistoricalPosition(victim,rewind,out bool crouched);
                float height=crouched?sim.Movement.Config.CrouchHeight:sim.Movement.Config.StandingHeight;
                if(RayBox(origin,direction,position+new Vec3(-.3f,0,-.3f),position+new Vec3(.3f,height,.3f),out float distance)&&distance<closest)
                { closest=distance; target=victim; }
            }
            if(target==null) return;
            target.Health-=shot.Damage;
            if(target.Health<=0) sim.Kill(shooter,target,shot.Weapon);
        }
        public static bool RayBox(Vec3 origin,Vec3 dir,Vec3 min,Vec3 max,out float distance)
        {
            float enter=0,leave=float.MaxValue;
            bool ok=Slab(origin.X,dir.X,min.X,max.X,ref enter,ref leave)&&Slab(origin.Y,dir.Y,min.Y,max.Y,ref enter,ref leave)&&Slab(origin.Z,dir.Z,min.Z,max.Z,ref enter,ref leave);
            distance=enter; return ok;
        }
        private static bool Slab(float o,float d,float min,float max,ref float enter,ref float leave)
        {
            if(Math.Abs(d)<1e-8f) return o>=min&&o<=max;
            float a=(min-o)/d,b=(max-o)/d;
            if(a>b) { float t=a; a=b; b=t; }
            enter=Math.Max(enter,a); leave=Math.Min(leave,b); return enter<=leave;
        }
    }
}
