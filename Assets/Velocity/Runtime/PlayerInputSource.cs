using System;
using System.Collections.Generic;
using UnityEngine;
using Velocity.Core;

namespace Velocity.Runtime
{
    public sealed class DesktopInput
    {
        private bool crouched;
        public PlayerInput Read(GameSettings settings,float yaw,float pitch,byte weapon)
        {
            bool Key(int i) => Enum.TryParse(settings.Keys[i],out KeyCode code)&&Input.GetKey(code);
            bool Down(int i) => Enum.TryParse(settings.Keys[i],out KeyCode code)&&Input.GetKeyDown(code);
            if(settings.ToggleCrouch&&Down(5)) crouched=!crouched;
            InputButtons buttons=InputButtons.None;
            if(Key(4)) buttons|=InputButtons.Jump;
            if(settings.ToggleCrouch?crouched:Key(5)) buttons|=InputButtons.Crouch;
            if(Key(6)) buttons|=InputButtons.Walk;
            if(Key(7)) buttons|=InputButtons.Fire;
            if(Key(8)) buttons|=InputButtons.Aim;
            if(Key(9)) buttons|=InputButtons.Reload;
            return new PlayerInput{Forward=(Key(0)?1:0)-(Key(1)?1:0),Side=(Key(3)?1:0)-(Key(2)?1:0),Yaw=yaw,Pitch=pitch,Weapon=weapon,Buttons=buttons};
        }
    }
    public sealed class MobileInput
    {
        public readonly MobileControls Controls=new MobileControls();
        private readonly List<TouchPoint> touches=new List<TouchPoint>();
        public void Poll(GameSettings settings,bool combat)
        {
            touches.Clear();
            Rect area=Screen.safeArea;
            foreach(Touch touch in Input.touches) touches.Add(new TouchPoint{Id=touch.fingerId,X=(touch.position.x-area.xMin)/area.width,Y=(touch.position.y-area.yMin)/area.height,Ended=touch.phase==TouchPhase.Ended||touch.phase==TouchPhase.Canceled});
            Controls.Assist=settings.SurfAssist; Controls.ButtonScale=settings.ButtonScale; Controls.Update(touches,area.width/area.height,combat);
        }
        public PlayerInput Read(float yaw,float pitch,byte weapon) => new PlayerInput{Forward=Controls.Forward,Side=Controls.Side,Yaw=yaw,Pitch=pitch,Weapon=weapon,Buttons=Controls.Buttons};
    }
    public sealed class PlayerInputSource
    {
        public readonly MobileInput Mobile=new MobileInput();
        private readonly DesktopInput desktop=new DesktopInput();
        public float Yaw,Pitch; public byte Weapon;
        public bool Enabled=true;
        private PlayerInput latest;
        private InputButtons queued;
        public void Update(GameSettings settings,bool combat,bool isMobile)
        {
            if(!Enabled) { latest=new PlayerInput{Yaw=Yaw,Pitch=Pitch,Weapon=Weapon}; return; }
            if(isMobile)
            {
                Mobile.Poll(settings,combat);
                Yaw=MathEx.WrapAngle(Yaw+Mobile.Controls.LookX*settings.TouchSensitivity);
                Pitch=Mathf.Clamp(Pitch-Mobile.Controls.LookY*settings.TouchSensitivity,-89,89);
                if(Mobile.Controls.WeaponPressed) Weapon=(byte)((Weapon+1)%4);
                latest=Mobile.Read(Yaw,Pitch,Weapon);
            }
            else
            {
                Yaw=MathEx.WrapAngle(Yaw+Input.GetAxisRaw("Mouse X")*settings.MouseSensitivity);
                Pitch=Mathf.Clamp(Pitch-Input.GetAxisRaw("Mouse Y")*settings.MouseSensitivity,-89,89);
                for(int i=0;i<4;i++) if(Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1+i))) Weapon=(byte)i;
                latest=desktop.Read(settings,Yaw,Pitch,Weapon);
                if(Input.GetKeyDown(KeyCode.F5)||(!combat&&Input.GetKeyDown(KeyCode.R))) queued|=InputButtons.Restart;
                if(Input.GetKeyDown(KeyCode.T)) queued|=InputButtons.Checkpoint;
            }
            if(settings.AutoBhop) latest.Buttons|=InputButtons.AutoBhop;
        }
        public void Command(InputButtons command) => queued|=command;
        public PlayerInput Sample(int sequence,int tick)
        {
            PlayerInput input=latest; input.Sequence=sequence; input.ServerTick=tick; input.Buttons|=queued; queued=InputButtons.None; return input;
        }
        public void ResetLook(MovementState state) { Yaw=state.Yaw; Pitch=state.Pitch; Mobile.Controls.Clear(); }
    }
}
