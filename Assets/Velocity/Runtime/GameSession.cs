using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Velocity.Core;

namespace Velocity.Runtime
{
    public sealed class GameSession : MonoBehaviour
    {
        public SettingsManager Settings;
        public readonly PlayerInputSource Input=new PlayerInputSource();
        public PlayerController View;
        public UIManager UI;
        public LanServer Server;
        public LanClient Client;
        public LanDiscovery Discovery;
        public GameSimulation Solo;
        public bool Running,Paused=true,Mobile,InputAiming;
        public string Status="";
        private MapManager mapRenderer;
        private byte renderedMap=255;
        private int sequence,lastFinishSerial;
        private float resolutionTimer,frameAverage=1f/60;
        public PlayerState Local => Client!=null?Client.Simulation!=null&&Client.Simulation.Players.TryGetValue(Client.LocalId,out PlayerState c)?c:null:Server!=null?Server.Simulation.Players[1]:Solo!=null?Solo.Players[1]:null;
        public GameSimulation Simulation => Client?.Simulation??Server?.Simulation??Solo;
        public MapMode Mode => Simulation?.Map.Mode??MapMode.Surf;
        public IEnumerable<PlayerState> VisiblePlayers => Client!=null?Client.Roster.Values:Simulation!=null?Simulation.Players.Values:(IEnumerable<PlayerState>)Array.Empty<PlayerState>();
        private void Awake()
        {
            DontDestroyOnLoad(gameObject); Mobile=Application.isMobilePlatform;
            Settings=new SettingsManager(Application.persistentDataPath);
            if(Settings.Value.TouchLayout!=null&&Settings.Value.TouchLayout.Length>0)
            {
                foreach(TouchButton saved in Settings.Value.TouchLayout)
                    foreach(TouchButton button in Input.Mobile.Controls.Layout)
                        if(saved!=null&&saved.Action==button.Action) {button.X=saved.X;button.Y=saved.Y;button.Size=saved.Size;}
            }
            Time.fixedDeltaTime=MovementConfig.TickDelta; Time.maximumDeltaTime=.1f; Application.runInBackground=true;
            Screen.sleepTimeout=SleepTimeout.NeverSleep; ApplySettings();
            var cameraObject=new GameObject("Player camera"); cameraObject.transform.SetParent(transform); View=cameraObject.AddComponent<PlayerController>(); View.Initialize(this);
            RenderSettings.ambientMode=AmbientMode.Flat; RenderSettings.ambientLight=new Color(.55f,.61f,.66f);
            var sun=new GameObject("Sun").AddComponent<Light>(); sun.transform.SetParent(transform); sun.type=LightType.Directional; sun.intensity=1; sun.shadows=LightShadows.None; sun.transform.rotation=Quaternion.Euler(48,-32,0);
            RenderSettings.fog=true; RenderSettings.fogColor=new Color(.045f,.06f,.075f); RenderSettings.fogMode=FogMode.Exponential; RenderSettings.fogDensity=.0025f;
            ShowMap(MapCatalog.Create(0));
            UI=gameObject.AddComponent<UIManager>(); UI.Initialize(this);
            gameObject.AddComponent<WeaponPresentation>().Initialize(this);
        }
        public void ApplySettings()
        {
            Settings.Value.Validate(); Application.targetFrameRate=Settings.Value.FrameLimit;
            QualitySettings.vSyncCount=0; QualitySettings.shadows=ShadowQuality.Disable;
            QualitySettings.antiAliasing=0;
            if(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline)
            { pipeline.renderScale=Settings.Value.RenderScale; pipeline.msaaSampleCount=Settings.Value.Quality==2?2:1; }
            AudioListener.volume=Settings.Value.Volume;
        }
        private void ShowMap(MapDefinition map)
        {
            if(renderedMap==map.Id) return;
            mapRenderer?.Dispose(); mapRenderer=new MapManager(map); renderedMap=map.Id;
        }
        public void StartSolo(byte map)
        {
            CloseNetwork(); Solo=new GameSimulation(map);ConfigureRules(Solo); Solo.AddPlayer(1,Settings.Value.PlayerName);
            if(map==2) for(byte i=2;i<=4;i++) Solo.AddPlayer(i,"Target "+(i-1));
            Enter(Solo.Map);
        }
        public void Host(byte map,bool teams)
        {
            CloseNetwork(); Solo=null;
            try
            {
                Server=new LanServer(map,Settings.Value.PlayerName,teams);
                ConfigureRules(Server.Simulation);Server.Simulation.Restart(Server.Simulation.Players[1],false);
                try { Discovery=new LanDiscovery(Server); } catch(SocketException e) { Status="Discovery unavailable: "+e.SocketErrorCode+". Join by IP."; }
                Enter(Server.Simulation.Map);
            }
            catch(Exception e) { CloseNetwork(); Status="Cannot host: "+e.Message; UI.MainMenu(); }
        }
        public void Join(string address,int port=LanServer.DefaultPort)
        {
            CloseNetwork(); Solo=null; Running=false; Paused=true;
            try { Client=new LanClient(address,Settings.Value.PlayerName,port); Settings.Value.LastAddress=address; Settings.Save(); Status="Connecting to "+address+":"+port; UI.Connecting(); }
            catch(Exception e) { CloseNetwork(); Status=e.Message; UI.JoinMenu(); }
        }
        public void Browse()
        {
            if(Running) return;
            Discovery?.Dispose(); Discovery=null;
            try { Discovery=new LanDiscovery(); } catch(Exception e) { Status="Discovery unavailable: "+e.Message+". Use manual IP."; }
        }
        private void ConfigureRules(GameSimulation simulation)
        {
            MovementConfig config=simulation.Map.Mode==MapMode.Bhop?Settings.Value.BhopPhysics:Settings.Value.SurfPhysics;
            simulation.Movement.Config.CopyFrom(config);simulation.CustomRules=!config.SameRules(MovementConfig.Preset(simulation.Map.Mode));
        }
        private void Enter(MapDefinition map)
        {
            ShowMap(map); sequence=0; lastFinishSerial=0; Running=true; Paused=false; Status="";
            if(Local!=null) Input.ResetLook(Local.Movement);
            View.ResetView(); SetCapture(); UI.InGame();
        }
        public void Pause(bool pause)
        {
            if(!Running) return;
            Paused=pause; Input.Mobile.Controls.Clear(); SetCapture();
            if(pause) UI.PauseMenu(); else UI.InGame();
        }
        private void SetCapture()
        {
            Input.Enabled=Running&&!Paused;
            Cursor.lockState=Running&&!Paused&&!Mobile?CursorLockMode.Locked:CursorLockMode.None;
            Cursor.visible=Paused||!Running||Mobile;
        }
        public void ReturnToMenu()
        {
            CloseNetwork(); Solo=null; Running=false; Paused=true; SetCapture(); View.ResetView(); UI.MainMenu();
        }
        private void Update()
        {
            double time=Time.realtimeSinceStartupAsDouble;
            Server?.Pump(time); Client?.Pump(time); Discovery?.Pump(time);
            if(Client!=null)
            {
                if(Client.Connected&&!Running) Enter(Client.Simulation.Map);
                if(!Client.Connected&&Running) { Status=Client.Status; ReturnToMenu(); }
                else if(!Running) Status=Client.Status;
            }
            if(Running&&!UI.Binding&&UnityEngine.Input.GetKeyDown(KeyCode.Escape)) Pause(!Paused);
            Input.Update(Settings.Value,Mode==MapMode.Combat,Mobile);
            PlayerState record=Client!=null&&Client.Roster.TryGetValue(Client.LocalId,out PlayerState authoritative)?authoritative:Local;
            if(record!=null&&record.Timer.Finished&&record.Timer.FinishSerial>lastFinishSerial)
            { lastFinishSerial=record.Timer.FinishSerial; Settings.Record(Simulation.Map.Id,record); }
            if(Mobile&&Running&&Settings.Value.AdaptiveResolution)
            {
                frameAverage=Mathf.Lerp(frameAverage,Time.unscaledDeltaTime,.04f); resolutionTimer+=Time.unscaledDeltaTime;
                if(resolutionTimer>3&&GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline)
                {
                    resolutionTimer=0; float budget=1f/Application.targetFrameRate;
                    if(frameAverage>budget*1.18f) pipeline.renderScale=Mathf.Max(.5f,pipeline.renderScale-.05f);
                    else if(frameAverage<budget*.93f) pipeline.renderScale=Mathf.Min(Settings.Value.RenderScale,pipeline.renderScale+.025f);
                    if(pipeline.renderScale<=.5f&&Application.targetFrameRate>30&&frameAverage>budget*1.4f) Application.targetFrameRate=30;
                }
            }
        }
        private void FixedUpdate()
        {
            if(!Running||Simulation==null||(Paused&&Solo!=null)) return;
            View.BeforeTick(); PlayerInput input=Input.Sample(sequence++,Simulation.Tick);
            InputAiming=input.Has(InputButtons.Aim);
            if(Client!=null) Client.Step(input);
            else if(Server!=null) Server.Step(input);
            else
            {
                Solo.AdvanceTick(); Solo.Simulate(1,input);
                if(Solo.Map.Mode==MapMode.Combat)
                    foreach(PlayerState p in Solo.Players.Values) if(p.Id!=1) Solo.Simulate(p.Id,new PlayerInput{Sequence=sequence,Yaw=p.Movement.Yaw,ServerTick=Solo.Tick});
            }
            if((input.Has(InputButtons.Restart)||input.Has(InputButtons.Checkpoint))&&Local!=null) Input.ResetLook(Local.Movement);
        }
        private void CloseNetwork()
        {
            Discovery?.Dispose(); Discovery=null; Client?.Dispose(); Client=null; Server?.Dispose(); Server=null;
        }
        private void OnApplicationFocus(bool focused) { if(!focused&&Running&&!Paused) Pause(true); }
        private void OnApplicationQuit() { Settings?.Save(); CloseNetwork(); }
        private void OnDestroy() { CloseNetwork(); mapRenderer?.Dispose(); }
    }
    public static class RuntimeBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Start()
        {
            if(UnityEngine.Object.FindFirstObjectByType<GameSession>()==null) new GameObject("Velocity").AddComponent<GameSession>();
        }
    }
}
