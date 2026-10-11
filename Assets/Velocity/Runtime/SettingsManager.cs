using System;
using System.IO;
using UnityEngine;
using Velocity.Core;

namespace Velocity.Runtime
{
    [Serializable]
    public sealed class GameSettings
    {
        public string PlayerName="Player", LastAddress="192.168.1.100";
        public float MouseSensitivity=.12f,TouchSensitivity=110,Fov=100,TouchOpacity=.5f,ButtonScale=1,RenderScale=.75f,Volume=.4f;
        public bool AutoBhop=true,ToggleCrouch=false,SurfAssist=false,Training=true,AdaptiveResolution=true;
        public int FrameLimit=60,Quality=0,CrosshairSize=8,CrosshairStyle=0;
        public string[] Keys={"W","S","A","D","Space","LeftControl","LeftShift","Mouse0","Mouse1","R","Tab"};
        public TouchButton[] TouchLayout;
        public MovementConfig SurfPhysics=MovementConfig.Preset(MapMode.Surf),BhopPhysics=MovementConfig.Preset(MapMode.Bhop);
        public void Validate()
        {
            PlayerName=GameSimulation.CleanName(PlayerName); if(LastAddress==null) LastAddress="192.168.1.100";
            MouseSensitivity=Mathf.Clamp(MouseSensitivity,.01f,2); TouchSensitivity=Mathf.Clamp(TouchSensitivity,20,300);
            Fov=Mathf.Clamp(Fov,65,120); TouchOpacity=Mathf.Clamp01(TouchOpacity); ButtonScale=Mathf.Clamp(ButtonScale,.6f,1.8f);
            RenderScale=Mathf.Clamp(RenderScale,.5f,1); Volume=Mathf.Clamp01(Volume); Quality=Mathf.Clamp(Quality,0,2);
            FrameLimit=FrameLimit==30?30:FrameLimit==120?120:FrameLimit==240?240:60;
            CrosshairSize=Mathf.Clamp(CrosshairSize,2,24); CrosshairStyle=Mathf.Clamp(CrosshairStyle,0,2);
            string[] defaults=new GameSettings().Keys;
            if(Keys==null||Keys.Length!=defaults.Length) Keys=defaults;
            for(int i=0;i<Keys.Length;i++) if(!Enum.TryParse(Keys[i],out KeyCode _)) Keys[i]=defaults[i];
            if(TouchLayout!=null) foreach(TouchButton b in TouchLayout)
                if(b!=null) { b.X=Mathf.Clamp(b.X,.04f,.96f); b.Y=Mathf.Clamp(b.Y,.06f,.94f); b.Size=Mathf.Clamp(b.Size,.035f,.11f); }
            if(SurfPhysics==null||!SurfPhysics.Valid) SurfPhysics=MovementConfig.Preset(MapMode.Surf);
            if(BhopPhysics==null||!BhopPhysics.Valid) BhopPhysics=MovementConfig.Preset(MapMode.Bhop);
        }
    }
    [Serializable]
    public sealed class Records
    {
        public int[] BestTicks=new int[3]; public float[] MaxSpeeds=new float[3]; public int[] Completions=new int[3];
    }
    public sealed class SettingsManager
    {
        public readonly GameSettings Value=new GameSettings();
        public readonly Records Records=new Records();
        private readonly string root;
        public SettingsManager(string directory)
        {
            root=directory; Directory.CreateDirectory(root); Read("settings.json",Value); Read("records.json",Records); Value.Validate();
            if(Records.BestTicks==null||Records.BestTicks.Length!=3) Records.BestTicks=new int[3];
            if(Records.MaxSpeeds==null||Records.MaxSpeeds.Length!=3) Records.MaxSpeeds=new float[3];
            if(Records.Completions==null||Records.Completions.Length!=3) Records.Completions=new int[3];
        }
        private void Read(string file,object target)
        {
            string path=Path.Combine(root,file);
            try { if(File.Exists(path)) JsonUtility.FromJsonOverwrite(File.ReadAllText(path),target); }
            catch(Exception e)
            {
                Debug.LogWarning("Recovering "+file+": "+e.Message);
                try { if(File.Exists(path+".bak")) JsonUtility.FromJsonOverwrite(File.ReadAllText(path+".bak"),target); } catch(Exception recovery) { Debug.LogWarning(recovery.Message); }
            }
        }
        private void Write(string file,object value)
        {
            string path=Path.Combine(root,file),temp=path+".tmp";
            using(var stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None))
            using(var writer=new StreamWriter(stream)) { writer.Write(JsonUtility.ToJson(value,true)); writer.Flush(); stream.Flush(true); }
            if(File.Exists(path)) File.Replace(temp,path,path+".bak"); else File.Move(temp,path);
        }
        public void Save() { Value.Validate(); Write("settings.json",Value); }
        public void Record(byte map,PlayerState p)
        {
            if(!p.Timer.Finished||p.Timer.Practice||p.Timer.ElapsedTicks<=0) return;
            Records.Completions[map]++;
            if(Records.BestTicks[map]==0||p.Timer.ElapsedTicks<Records.BestTicks[map]) Records.BestTicks[map]=p.Timer.ElapsedTicks;
            Records.MaxSpeeds[map]=Math.Max(Records.MaxSpeeds[map],p.Movement.MaxSpeed);
            Write("records.json",Records);
        }
    }
}
