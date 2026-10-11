using System.Collections.Generic;
using UnityEngine;
using Velocity.Core;

namespace Velocity.Runtime
{
    public sealed class PlayerController : MonoBehaviour
    {
        public Camera Camera;
        public GameSession Session;
        private readonly Dictionary<byte,GameObject> avatars=new Dictionary<byte,GameObject>();
        private Material red,blue;
        private float eyeHeight=1.62f;
        private Vector3 smoothCorrection;
        private Vec3 previousPosition;
        private bool hadState;
        public void Initialize(GameSession session)
        {
            Session=session; Camera=gameObject.AddComponent<Camera>(); Camera.nearClipPlane=.04f; Camera.farClipPlane=650;
            Camera.clearFlags=CameraClearFlags.SolidColor; Camera.backgroundColor=new Color(.045f,.06f,.075f);
            gameObject.AddComponent<AudioListener>();
            red=new Material(Shader.Find("Velocity/Surface")){color=new Color(.8f,.32f,.24f)};
            blue=new Material(Shader.Find("Velocity/Surface")){color=new Color(.3f,.6f,.8f)};
        }
        public void BeforeTick() { if(Session.Local!=null) { previousPosition=Session.Local.Movement.Position; hadState=true; } }
        public void ResetView() { hadState=false; smoothCorrection=Vector3.zero; }
        private void LateUpdate()
        {
            PlayerState local=Session.Local;
            if(local==null) { transform.position=new Vector3(-20,41,-38); transform.rotation=Quaternion.Euler(15,32,0); ClearAvatars(); return; }
            MovementState state=local.Movement;
            float alpha=Mathf.Clamp01((Time.time-Time.fixedTime)/Time.fixedDeltaTime);
            Vec3 position=hadState&&(state.Position-previousPosition).Length<4?Vec3.Lerp(previousPosition,state.Position,alpha):state.Position;
            eyeHeight=Mathf.Lerp(eyeHeight,state.Crouched?.9f:1.62f,1-Mathf.Exp(-20*Time.unscaledDeltaTime));
            Vector3 correction=Session.Client!=null?MapManager.Unity(Session.Client.Correction):Vector3.zero;
            smoothCorrection=correction;
            if(Session.Client!=null) Session.Client.Correction*=Mathf.Exp(-14*Time.unscaledDeltaTime);
            transform.position=MapManager.Unity(position)+Vector3.up*eyeHeight+smoothCorrection;
            transform.rotation=Quaternion.Euler(Session.Input.Pitch,Session.Input.Yaw,0);
            bool aim=Session.InputAiming&&Session.Mode==MapMode.Combat;
            float target=aim?(Session.Input.Weapon==1?35:72):Session.Settings.Value.Fov;
            Camera.fieldOfView=Mathf.Lerp(Camera.fieldOfView,target,1-Mathf.Exp(-18*Time.unscaledDeltaTime));
            var alive=new HashSet<byte>();
            foreach(PlayerState p in Session.VisiblePlayers)
            {
                if(p.Id==local.Id) continue; alive.Add(p.Id);
                if(!avatars.TryGetValue(p.Id,out GameObject avatar))
                {
                    avatar=GameObject.CreatePrimitive(PrimitiveType.Capsule); avatar.name=p.Name; Destroy(avatar.GetComponent<Collider>()); avatar.GetComponent<Renderer>().sharedMaterial=p.Team==0?red:blue;
                    var nameTag=new GameObject("Name"); nameTag.transform.SetParent(avatar.transform); nameTag.transform.localPosition=new Vector3(0,1.3f,0);
                    var label=nameTag.AddComponent<TextMesh>(); label.text=p.Name; label.fontSize=40; label.characterSize=.07f; label.anchor=TextAnchor.MiddleCenter; label.color=Color.white;
                    label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");nameTag.GetComponent<MeshRenderer>().sharedMaterial=label.font.material;
                    avatars.Add(p.Id,avatar);
                }
                MovementState rendered=p.Movement;
                if(Session.Client!=null&&Session.Client.Remote.TryGetValue(p.Id,out RemoteInterpolation remote)) rendered=remote.At(Session.Client.ServerTick-12+alpha);
                avatar.SetActive(p.Health>0); avatar.transform.position=MapManager.Unity(rendered.Position)+Vector3.up*(rendered.Crouched?.525f:.9f);
                avatar.transform.localScale=new Vector3(.6f,rendered.Crouched?.525f:.9f,.6f); avatar.transform.rotation=Quaternion.Euler(0,rendered.Yaw,0);
                if(avatar.transform.childCount>0) avatar.transform.GetChild(0).rotation=transform.rotation;
            }
            var remove=new List<byte>(); foreach(byte id in avatars.Keys) if(!alive.Contains(id)) remove.Add(id); foreach(byte id in remove) { Destroy(avatars[id]); avatars.Remove(id); }
        }
        private void ClearAvatars() { foreach(GameObject a in avatars.Values) Destroy(a); avatars.Clear(); }
        private void OnDestroy() { ClearAvatars(); Destroy(red); Destroy(blue); }
    }
}
