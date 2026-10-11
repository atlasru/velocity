using System;
using System.Collections.Generic;

namespace Velocity.Core
{
    public struct TouchPoint { public int Id; public float X,Y; public bool Ended; }
    [Serializable]
    public sealed class TouchButton
    {
        public string Action; public float X,Y,Size;
        public TouchButton(string action,float x,float y,float size) { Action=action; X=x; Y=y; Size=size; }
        public TouchButton() { }
    }
    public sealed class MobileControls
    {
        private readonly Dictionary<int,string> owners=new Dictionary<int,string>();
        private readonly Dictionary<int,TouchPoint> previous=new Dictionary<int,TouchPoint>();
        private int joystick=-1;
        private float joyX,joyY;
        public float Side,Forward,LookX,LookY;
        public InputButtons Buttons;
        public bool Assist,Editing;
        public float Radius=.105f,ButtonScale=1;
        public readonly List<TouchButton> Layout=new List<TouchButton>{new TouchButton("Jump",.88f,.29f,.075f),new TouchButton("Crouch",.76f,.17f,.06f),new TouchButton("Fire",.88f,.52f,.07f),new TouchButton("Reload",.72f,.43f,.05f),new TouchButton("Aim",.95f,.68f,.05f),new TouchButton("Weapon",.69f,.6f,.045f)};
        public bool WeaponPressed;
        public float JoystickX => joystick<0?.15f:joyX;
        public float JoystickY => joystick<0?.2f:joyY;
        public void Clear() { owners.Clear(); previous.Clear(); joystick=-1; Side=Forward=LookX=LookY=0; Buttons=InputButtons.None; }
        public void Update(IReadOnlyList<TouchPoint> touches,float aspect,bool combat)
        {
            LookX=LookY=0; Buttons=InputButtons.None; WeaponPressed=false;
            var alive=new HashSet<int>();
            foreach(TouchPoint t in touches)
            {
                if(t.Ended) { owners.Remove(t.Id); previous.Remove(t.Id); if(joystick==t.Id) joystick=-1; continue; }
                alive.Add(t.Id);
                bool fresh=!owners.TryGetValue(t.Id,out string owner);
                if(fresh)
                {
                    owner="Look";
                    foreach(TouchButton b in Layout)
                    {
                        if(!combat&&(b.Action=="Fire"||b.Action=="Reload"||b.Action=="Aim"||b.Action=="Weapon")) continue;
                        float dx=(t.X-b.X)*aspect,dy=t.Y-b.Y;
                        if(dx*dx+dy*dy<=b.Size*b.Size*ButtonScale*ButtonScale) { owner=b.Action; break; }
                    }
                    if(owner=="Look"&&t.X<.42f&&joystick<0) { owner="Joystick"; joystick=t.Id; joyX=t.X; joyY=t.Y; }
                    owners[t.Id]=owner;
                }
                if(Editing)
                {
                    foreach(TouchButton b in Layout) if(b.Action==owner) { b.X=MathEx.Clamp(t.X,.04f,.96f); b.Y=MathEx.Clamp(t.Y,.06f,.94f); }
                }
                else if(owner=="Joystick")
                {
                    float x=(t.X-joyX)*aspect/Radius,y=(t.Y-joyY)/Radius;
                    float length=(float)Math.Sqrt(x*x+y*y); if(length>1) { x/=length; y/=length; }
                    Side=x; Forward=y;
                    // Assist changes thumb ergonomics only. No velocity or automatic camera input.
                    if(Assist&&Math.Abs(x)>.2f) { Side=Math.Sign(x)*MathEx.Clamp(Math.Abs(x)*1.4f,0,1); Forward=Math.Abs(y)>.65f?y:0; }
                }
                else if(owner=="Look"&&previous.TryGetValue(t.Id,out TouchPoint old)) { LookX+=(t.X-old.X)*aspect; LookY+=t.Y-old.Y; }
                else if(owner=="Weapon") { if(fresh) WeaponPressed=true; }
                else if(owner!="Look"&&Enum.TryParse(owner=="Jump"?"Jump":owner=="Crouch"?"Crouch":owner=="Fire"?"Fire":owner=="Reload"?"Reload":"Aim",out InputButtons flag)) Buttons|=flag;
                previous[t.Id]=t;
            }
            var remove=new List<int>(); foreach(int id in owners.Keys) if(!alive.Contains(id)) remove.Add(id);
            foreach(int id in remove) { owners.Remove(id); previous.Remove(id); if(joystick==id) joystick=-1; }
            if(joystick<0) Side=Forward=0;
        }
    }
}
