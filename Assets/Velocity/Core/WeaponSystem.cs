using System;

namespace Velocity.Core
{
    public sealed class WeaponDefinition
    {
        public string Name; public byte Capacity; public int Damage,IntervalTicks,ReloadTicks;
        public float Range,Recoil; public bool Automatic;
    }
    public static class WeaponCatalog
    {
        public static readonly WeaponDefinition[] All={
            new WeaponDefinition{Name="RIFLE",Capacity=30,Damage=26,IntervalTicks=12,ReloadTicks=264,Range=160,Recoil=.65f,Automatic=true},
            new WeaponDefinition{Name="SNIPER",Capacity=5,Damage=100,IntervalTicks=144,ReloadTicks=336,Range=300,Recoil=2},
            new WeaponDefinition{Name="PISTOL",Capacity=12,Damage=34,IntervalTicks=26,ReloadTicks=192,Range=90,Recoil=.9f},
            new WeaponDefinition{Name="KNIFE",Capacity=0,Damage=55,IntervalTicks=60,ReloadTicks=0,Range=2.3f,Recoil=0}
        };
    }
    public sealed class WeaponState
    {
        public byte Selected,Burst;
        public readonly byte[] Ammo={30,5,12,0};
        public readonly ushort[] Reserve={180,30,72,0};
        public int NextFireTick,ReloadFinishTick,LastShotTick,ShotSerial;
        public bool FireHeld;
        public WeaponState Clone()
        {
            var copy=new WeaponState{Selected=Selected,Burst=Burst,NextFireTick=NextFireTick,ReloadFinishTick=ReloadFinishTick,LastShotTick=LastShotTick,ShotSerial=ShotSerial,FireHeld=FireHeld};
            Array.Copy(Ammo,copy.Ammo,4); Array.Copy(Reserve,copy.Reserve,4); return copy;
        }
        public void Refill()
        {
            // Pickups only refill reserve. They cannot bypass shot or reload timing.
            Reserve[0]=180; Reserve[1]=30; Reserve[2]=72;
        }
    }
    public struct Shot
    {
        public byte Weapon; public int Damage; public float Range,RecoilSide,RecoilUp;
    }
    public static class WeaponSystem
    {
        private static readonly float[] Pattern={0,.25f,-.35f,.6f,-.5f,.4f,.8f,-.65f,-.3f,.55f};
        public static Shot? Step(WeaponState state,PlayerInput input,int tick)
        {
            if(input.Weapon!=state.Selected) { state.Selected=input.Weapon; state.ReloadFinishTick=0; state.Burst=0; state.NextFireTick=Math.Max(state.NextFireTick,tick+12); }
            WeaponDefinition weapon=WeaponCatalog.All[state.Selected];
            if(tick-state.LastShotTick>60) state.Burst=0;
            if(state.ReloadFinishTick>0&&tick>=state.ReloadFinishTick)
            {
                int amount=Math.Min(weapon.Capacity-state.Ammo[state.Selected],state.Reserve[state.Selected]);
                state.Ammo[state.Selected]+=(byte)amount; state.Reserve[state.Selected]-=(ushort)amount; state.ReloadFinishTick=0;
            }
            if(input.Has(InputButtons.Reload)&&state.ReloadFinishTick==0&&weapon.Capacity>0&&state.Ammo[state.Selected]<weapon.Capacity&&state.Reserve[state.Selected]>0)
                state.ReloadFinishTick=tick+weapon.ReloadTicks;
            bool fire=input.Has(InputButtons.Fire),pressed=fire&&!state.FireHeld;
            state.FireHeld=fire;
            if(!fire||(!weapon.Automatic&&!pressed)||tick<state.NextFireTick||state.ReloadFinishTick>0) return null;
            if(weapon.Capacity>0&&state.Ammo[state.Selected]==0) return null;
            if(weapon.Capacity>0) state.Ammo[state.Selected]--;
            state.NextFireTick=tick+weapon.IntervalTicks; state.LastShotTick=tick; state.ShotSerial++;
            float recoil=weapon.Recoil*(input.Has(InputButtons.Aim)?.6f:1);
            var shot=new Shot{Weapon=state.Selected,Damage=weapon.Damage,Range=weapon.Range,RecoilSide=Pattern[state.Burst%Pattern.Length]*recoil,RecoilUp=Math.Min((int)state.Burst,8)*recoil*.25f};
            state.Burst=(byte)Math.Min(state.Burst+1,250); return shot;
        }
    }
}
