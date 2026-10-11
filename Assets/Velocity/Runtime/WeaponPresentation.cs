using UnityEngine;
using Velocity.Core;

namespace Velocity.Runtime
{
    public sealed class WeaponPresentation : MonoBehaviour
    {
        private GameSession session;
        private GameObject model,flash;
        private AudioSource audioSource;
        private AudioClip[] sounds;
        private Material gun,metal;
        private int shotSerial;
        private byte selected=255;
        private float flashTime,kick;
        public void Initialize(GameSession game)
        {
            session=game; audioSource=gameObject.AddComponent<AudioSource>(); audioSource.playOnAwake=false;
            gun=new Material(Shader.Find("Universal Render Pipeline/Unlit")){color=new Color(.15f,.18f,.2f)};
            metal=new Material(Shader.Find("Universal Render Pipeline/Unlit")){color=new Color(.48f,.5f,.52f)};
            sounds=new AudioClip[4];
            for(int w=0;w<4;w++)
            {
                float duration=w==1?.32f:.14f; int count=(int)(44100*duration); var samples=new float[count]; uint noise=42;
                for(int i=0;i<count;i++) { noise=noise*1664525+1013904223; float n=((noise>>8)&65535)/32768f-1; float envelope=Mathf.Exp(-i/(44100f*(w==1?.075f:.028f))); samples[i]=(n*.5f+Mathf.Sin(i*(w==3?.04f:.013f))*.3f)*envelope; }
                sounds[w]=AudioClip.Create("Weapon "+w,count,1,44100,false); sounds[w].SetData(samples,0);
            }
        }
        private GameObject Part(string name,Vector3 position,Vector3 scale,Material material)
        {
            GameObject p=GameObject.CreatePrimitive(PrimitiveType.Cube); p.name=name; Destroy(p.GetComponent<Collider>());
            p.transform.SetParent(model.transform,false); p.transform.localPosition=position; p.transform.localScale=scale; p.GetComponent<Renderer>().sharedMaterial=material; return p;
        }
        private void MakeModel(byte weapon)
        {
            if(model!=null) Destroy(model);
            model=new GameObject(WeaponCatalog.All[weapon].Name); model.transform.SetParent(session.View.transform,false);
            if(weapon==3) { Part("Blade",new Vector3(0,0,.2f),new Vector3(.05f,.015f,.3f),metal); Part("Grip",new Vector3(0,0,-.02f),new Vector3(.06f,.035f,.13f),gun); }
            else
            {
                float length=weapon==1?.58f:weapon==0?.42f:.22f;
                Part("Receiver",Vector3.zero,new Vector3(.1f,.1f,length*.6f),gun);
                Part("Barrel",new Vector3(0,.016f,length*.5f),new Vector3(.035f,.035f,length*.5f),metal);
                Part("Grip",new Vector3(0,-.09f,-.03f),new Vector3(.065f,.15f,.08f),gun);
                if(weapon!=2) Part("Magazine",new Vector3(0,-.08f,.08f),new Vector3(.055f,.12f,.09f),gun);
                if(weapon==1) Part("Scope",new Vector3(0,.09f,.02f),new Vector3(.055f,.05f,.18f),metal);
            }
            flash=Part("Muzzle",new Vector3(0,0,weapon==1?.55f:.4f),new Vector3(.06f,.06f,.08f),metal); flash.SetActive(false);
            selected=weapon;
        }
        private void LateUpdate()
        {
            PlayerState p=session.Local;
            bool combat=p!=null&&session.Mode==MapMode.Combat;
            if(!combat) { if(model!=null) model.SetActive(false); return; }
            if(selected!=session.Input.Weapon) MakeModel(session.Input.Weapon);
            model.SetActive(p.Health>0&&!session.Paused);
            if(p.Weapon.ShotSerial!=shotSerial)
            { shotSerial=p.Weapon.ShotSerial; flashTime=.045f; kick=.04f; audioSource.PlayOneShot(sounds[selected]); }
            flashTime-=Time.unscaledDeltaTime; kick=Mathf.MoveTowards(kick,0,Time.unscaledDeltaTime*.3f);
            flash.SetActive(selected!=3&&flashTime>0);
            model.transform.localPosition=Vector3.Lerp(model.transform.localPosition,new Vector3(session.InputAiming?.02f:.25f,-.22f,.42f-kick),1-Mathf.Exp(-20*Time.unscaledDeltaTime));
            model.transform.localRotation=Quaternion.Euler(-kick*60,0,0);
        }
        private void OnDestroy() { if(model!=null) Destroy(model); Destroy(gun); Destroy(metal); if(sounds!=null) foreach(AudioClip c in sounds) Destroy(c); }
    }
}
