using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Velocity.Core;

namespace Velocity.Runtime
{
    public sealed class UIManager : MonoBehaviour
    {
        private GameSession session;
        private RectTransform safe,menu,hud,touchRoot;
        private Text speed,timer,details,health,ammo,status,score,feed,hint;
        private Text scoreStats,scoreSpeed;
        private RectTransform joystick,stick;
        private Font font;
        private CanvasScaler scaler;
        private bool showScore,editing;
        private string page="main";
        private Rect previousSafe;
        private int lastBrowserCount=-1;
        private byte selectedMap;
        private readonly List<Text> touchLabels=new List<Text>();
        private readonly List<Image> crosshair=new List<Image>();
        private string bindingAction;
        private int bindingIndex=-1;
        private float hudTime;
        public bool Binding => bindingIndex>=0;
        private static readonly Color Ink=new Color(.89f,.92f,.94f),Muted=new Color(.57f,.63f,.67f),Accent=new Color(.38f,.75f,.76f),Surface=new Color(.12f,.15f,.17f);
        public void Initialize(GameSession game)
        {
            session=game; font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvas=new GameObject("Interface").AddComponent<Canvas>(); canvas.transform.SetParent(transform); canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            scaler=canvas.gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution=session.Mobile?new Vector2(1280,720):new Vector2(1600,900); scaler.matchWidthOrHeight=.5f;
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            if(FindFirstObjectByType<EventSystem>()==null) { var events=new GameObject("UI events"); events.transform.SetParent(transform); events.AddComponent<EventSystem>(); events.AddComponent<StandaloneInputModule>(); }
            safe=Rect("Safe area",canvas.transform); Stretch(safe);
            hud=Rect("HUD",safe); Stretch(hud);
            speed=At("Speed",hud,new Vector2(.5f,.095f),new Vector2(400,45),32,TextAnchor.MiddleCenter);
            timer=At("Time",hud,new Vector2(.5f,.935f),new Vector2(440,35),27,TextAnchor.MiddleCenter);
            details=At("Run",hud,new Vector2(.5f,.89f),new Vector2(700,25),16,TextAnchor.MiddleCenter); details.color=Muted;
            health=At("Health",hud,new Vector2(.035f,.075f),new Vector2(200,35),23,TextAnchor.MiddleLeft);
            ammo=At("Ammo",hud,new Vector2(.965f,.075f),new Vector2(300,35),23,TextAnchor.MiddleRight);
            status=At("Status",hud,new Vector2(.035f,.95f),new Vector2(570,30),17,TextAnchor.MiddleLeft); status.color=Muted;
            hint=At("Training",hud,new Vector2(.5f,.035f),new Vector2(900,24),15,TextAnchor.MiddleCenter); hint.color=Muted;
            feed=At("Kill feed",hud,new Vector2(.96f,.87f),new Vector2(420,180),17,TextAnchor.UpperRight);
            score=At("Scoreboard",hud,new Vector2(.5f,.61f),new Vector2(750,350),22,TextAnchor.UpperCenter);
            score.alignment=TextAnchor.UpperLeft;
            scoreStats=At("K/D",score.transform,new Vector2(.66f,.5f),new Vector2(100,350),22,TextAnchor.UpperLeft);
            scoreSpeed=At("Speed",score.transform,new Vector2(.88f,.5f),new Vector2(100,350),22,TextAnchor.UpperLeft);
            ButtonAt(hud,"≡",new Vector2(.97f,.95f),new Vector2(44,40),()=>session.Pause(true));
            if(session.Mobile)
            {
                ButtonAt(hud,"SCORE",new Vector2(.055f,.88f),new Vector2(95,40),()=>showScore=!showScore);
                ButtonAt(hud,"↻",new Vector2(.055f,.79f),new Vector2(55,45),()=>session.Input.Command(InputButtons.Restart));
                ButtonAt(hud,"CP",new Vector2(.055f,.7f),new Vector2(55,45),()=>session.Input.Command(InputButtons.Checkpoint));
            }
            for(int i=0;i<5;i++) { var o=Rect("Crosshair",hud); o.anchorMin=o.anchorMax=new Vector2(.5f,.5f); var image=o.gameObject.AddComponent<Image>(); image.color=Ink; image.raycastTarget=false; crosshair.Add(image); }
            touchRoot=Rect("Touch controls",hud); Stretch(touchRoot); MakeTouch(); MainMenu();
        }
        private RectTransform Rect(string name,Transform parent)
        {
            var o=new GameObject(name,typeof(RectTransform)); o.transform.SetParent(parent,false); return o.GetComponent<RectTransform>();
        }
        private static void Stretch(RectTransform r) { r.anchorMin=Vector2.zero; r.anchorMax=Vector2.one; r.offsetMin=r.offsetMax=Vector2.zero; }
        private Text Text(string name,Transform parent,int size=22,TextAnchor align=TextAnchor.MiddleLeft)
        {
            var t=Rect(name,parent).gameObject.AddComponent<Text>(); t.font=font; t.fontSize=size; t.color=Ink; t.alignment=align; t.raycastTarget=false; t.supportRichText=false; return t;
        }
        private Text At(string name,Transform parent,Vector2 anchor,Vector2 size,int fontSize,TextAnchor align)
        {
            Text t=Text(name,parent,fontSize,align); RectTransform r=t.rectTransform; r.anchorMin=r.anchorMax=anchor;
            r.pivot=new Vector2(anchor.x<.1f?0:anchor.x>.9f?1:.5f,.5f); r.sizeDelta=size; return t;
        }
        private void Height(Component component,float height)
        {
            var layout=component.gameObject.AddComponent<LayoutElement>(); layout.minHeight=layout.preferredHeight=height;
        }
        private void Label(Transform parent,string value,int size=22,float height=40,Color? color=null)
        {
            Text label=Text(value,parent,size); label.text=value; label.color=color??Ink; Height(label,height);
        }
        private Button Button(Transform parent,string value,Action action,float height=58)
        {
            RectTransform r=Rect(value,parent); var image=r.gameObject.AddComponent<Image>(); image.color=Surface;
            var button=r.gameObject.AddComponent<Button>(); button.targetGraphic=image;
            ColorBlock colors=button.colors; colors.normalColor=Color.white; colors.highlightedColor=new Color(1.3f,1.3f,1.3f); colors.pressedColor=new Color(.65f,.8f,.8f); button.colors=colors;
            button.onClick.AddListener(()=>action());
            Text text=Text(value,r,22); text.text=value; Stretch(text.rectTransform); text.rectTransform.offsetMin=new Vector2(20,0); text.rectTransform.offsetMax=new Vector2(-20,0);
            Height(button,height); return button;
        }
        private void ButtonAt(Transform parent,string text,Vector2 anchor,Vector2 size,Action action)
        {
            Button b=Button(parent,text,action,size.y); RectTransform r=(RectTransform)b.transform; r.anchorMin=r.anchorMax=anchor; r.sizeDelta=size; r.pivot=new Vector2(.5f,.5f);
            Text label=b.GetComponentInChildren<Text>(); label.alignment=TextAnchor.MiddleCenter; label.rectTransform.offsetMin=label.rectTransform.offsetMax=Vector2.zero; label.fontSize=18;
        }
        private InputField Field(Transform parent,string label,string value,Action<string> changed)
        {
            Label(parent,label,17,25,Muted); RectTransform r=Rect(label,parent); r.gameObject.AddComponent<Image>().color=Surface;
            var field=r.gameObject.AddComponent<InputField>(); var text=Text("Value",r,22); Stretch(text.rectTransform); text.rectTransform.offsetMin=new Vector2(16,0); text.rectTransform.offsetMax=new Vector2(-16,0);
            field.textComponent=text; field.text=value; field.characterLimit=32; field.caretColor=Ink; field.customCaretColor=true; field.selectionColor=Accent; field.onEndEdit.AddListener(v=>changed(v)); Height(field,52); return field;
        }
        private void Toggle(Transform parent,string label,bool value,Action<bool> changed)
        {
            Button b=null; b=Button(parent,label+"   "+(value?"ON":"OFF"),()=>{ value=!value; changed(value); b.GetComponentInChildren<Text>().text=label+"   "+(value?"ON":"OFF"); },48);
        }
        private void Slider(Transform parent,string label,float value,float min,float max,Action<float> changed)
        {
            Text heading=Text(label,parent,18); heading.text=label+"   "+value.ToString("0.00"); Height(heading,26);
            RectTransform r=Rect(label+" slider",parent); Height(r,32);
            RectTransform track=Rect("Track",r); track.anchorMin=new Vector2(0,.4f); track.anchorMax=new Vector2(1,.6f); track.offsetMin=track.offsetMax=Vector2.zero; track.gameObject.AddComponent<Image>().color=Surface;
            RectTransform fillArea=Rect("Fill area",r); Stretch(fillArea); RectTransform fill=Rect("Fill",fillArea); Stretch(fill); fill.gameObject.AddComponent<Image>().color=Accent;
            RectTransform handleArea=Rect("Handle area",r); Stretch(handleArea); RectTransform handle=Rect("Handle",handleArea); handle.sizeDelta=new Vector2(18,32); var handleImage=handle.gameObject.AddComponent<Image>(); handleImage.color=Ink;
            var slider=r.gameObject.AddComponent<Slider>(); slider.fillRect=fill; slider.handleRect=handle; slider.targetGraphic=handleImage; slider.minValue=min; slider.maxValue=max; slider.value=value;
            slider.onValueChanged.AddListener(v=>{ heading.text=label+"   "+v.ToString("0.00"); changed(v); });
        }
        private Transform Page(string title)
        {
            if(menu!=null) Destroy(menu.gameObject);
            menu=Rect("Menu",safe); Stretch(menu); menu.gameObject.AddComponent<Image>().color=new Color(.035f,.045f,.055f,.97f);
            RectTransform viewport=Rect("Viewport",menu); viewport.anchorMin=new Vector2(session.Mobile?.14f:.25f,.055f); viewport.anchorMax=new Vector2(session.Mobile?.86f:.75f,.955f); viewport.offsetMin=viewport.offsetMax=Vector2.zero;
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content=Rect("Content",viewport); content.anchorMin=new Vector2(0,1); content.anchorMax=Vector2.one; content.pivot=new Vector2(.5f,1); content.sizeDelta=Vector2.zero;
            var layout=content.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing=10; layout.childControlWidth=true; layout.childControlHeight=true; layout.childForceExpandHeight=false; layout.padding=new RectOffset(10,10,12,20);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            var scroll=viewport.gameObject.AddComponent<ScrollRect>(); scroll.content=content; scroll.viewport=viewport; scroll.horizontal=false; scroll.movementType=ScrollRect.MovementType.Clamped; scroll.scrollSensitivity=35;
            Label(content,title,session.Mobile?36:48,60);
            return content;
        }
        public void MainMenu()
        {
            page="main"; hud.gameObject.SetActive(false); Transform p=Page("VELOCITY");
            Label(p,"SURF / BHOP / LAN",18,28,Accent);
            Field(p,"Player name",session.Settings.Value.PlayerName,v=>{session.Settings.Value.PlayerName=GameSimulation.CleanName(v);session.Settings.Save();});
            Button(p,"Play Solo",()=>MapMenu(false)); Button(p,"Host LAN",()=>MapMenu(true)); Button(p,"Join LAN",JoinMenu); Button(p,"Settings",()=>SettingsMenu(false)); Button(p,"Quit",()=>Application.Quit());
            Label(p,session.Status,17,45,Muted);
        }
        private void MapMenu(bool host)
        {
            page=host?"host":"maps"; Transform p=Page(host?"HOST LAN":"PLAY SOLO");
            Label(p,"Choose a course",18,28,Muted);
            for(byte map=0;map<3;map++)
            {
                byte id=map; Button(p,MapCatalog.Names[id],()=>{ selectedMap=id; if(host&&id==2) TeamMenu(); else if(host) session.Host(id,false); else session.StartSolo(id); });
                string summary=id==0?"Wide ramps · 2 checkpoints":id==1?"24 jump platforms · 3 routes":"Four ramp paths · FFA / teams";
                int best=session.Settings.Records.BestTicks[id]; if(best>0) summary+=" · PB "+Format(best/(float)MovementConfig.TickRate);
                Label(p,summary,17,32,Muted);
            }
            Button(p,"Back",MainMenu,48);
        }
        private void TeamMenu()
        {
            page="teams"; Transform p=Page("COMBAT"); Button(p,"Free for all",()=>session.Host(selectedMap,false)); Button(p,"Team deathmatch",()=>session.Host(selectedMap,true)); Button(p,"Back",()=>MapMenu(true));
        }
        public void JoinMenu()
        {
            if(page!="join") session.Browse(); page="join"; lastBrowserCount=session.Discovery?.Servers.Count??0;
            Transform p=Page("JOIN LAN"); string address=session.Settings.Value.LastAddress;
            Field(p,"Local IPv4 address",address,v=>address=v.Trim());
            Button(p,"Connect",()=>session.Join(address));
            Label(p,"Discovered on this network",19,32,Muted);
            if(session.Discovery!=null)
                foreach(DiscoveredServer server in session.Discovery.Servers.Values) { DiscoveredServer s=server; Button(p,s.Name+" · "+MapCatalog.Names[s.Map]+" · "+s.Players+"/8",()=>session.Join(s.Address,s.Port),55); Label(p,s.Address+":"+s.Port,16,25,Muted); }
            if(lastBrowserCount==0) Label(p,"No servers yet. Manual IP always works.",17,38,Muted);
            Label(p,session.Status,17,38,Muted); Button(p,"Refresh",()=>{session.Browse();JoinMenu();},48); Button(p,"Back",MainMenu,48);
        }
        public void Connecting()
        {
            page="connecting"; Transform p=Page("CONNECTING"); Label(p,"Waiting for LAN host…",22,45); Button(p,"Cancel",()=>session.ReturnToMenu());
        }
        public void InGame()
        {
            page="game"; if(menu!=null) Destroy(menu.gameObject); menu=null; hud.gameObject.SetActive(true); editing=false; session.Input.Mobile.Controls.Editing=false; MakeTouch();
            if(hint!=null) hint.text=session.Simulation.Map.Hint;
        }
        public void PauseMenu()
        {
            page="pause"; Transform p=Page("PAUSED"); Button(p,"Resume",()=>session.Pause(false));
            Button(p,"Restart run",()=>{session.Input.Command(InputButtons.Restart);session.Pause(false);});
            if(session.Mode!=MapMode.Combat) Button(p,"Checkpoint · practice",()=>{session.Input.Command(InputButtons.Checkpoint);session.Pause(false);});
            Button(p,"Settings",()=>SettingsMenu(true)); Button(p,"Main menu",()=>session.ReturnToMenu());
            if(session.Server!=null)
            {
                string addresses="LAN host · port "+session.Server.Transport.Port;
                try { foreach(NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces()) if(adapter.OperationalStatus==OperationalStatus.Up) foreach(UnicastIPAddressInformation ip in adapter.GetIPProperties().UnicastAddresses) if(ip.Address.AddressFamily==AddressFamily.InterNetwork&&!System.Net.IPAddress.IsLoopback(ip.Address)) addresses+="\n"+ip.Address; } catch(NetworkInformationException) { }
                Label(p,addresses,18,80,Muted);
            }
            Label(p,session.Server!=null||session.Client!=null?"LAN simulation continues while this menu is open.":"",16,35,Muted);
        }
        private void SettingsMenu(bool inGame)
        {
            page="settings"; Transform p=Page("SETTINGS"); GameSettings s=session.Settings.Value;
            void Apply() { session.ApplySettings(); }
            Slider(p,"Field of view",s.Fov,65,120,v=>{s.Fov=v;Apply();});
            Slider(p,"Mouse sensitivity",s.MouseSensitivity,.01f,.6f,v=>s.MouseSensitivity=v);
            Slider(p,"Touch sensitivity",s.TouchSensitivity,20,250,v=>s.TouchSensitivity=v);
            Slider(p,"Volume",s.Volume,0,1,v=>{s.Volume=v;Apply();});
            Toggle(p,"Automatic bunnyhop",s.AutoBhop,v=>s.AutoBhop=v); Toggle(p,"Toggle crouch",s.ToggleCrouch,v=>s.ToggleCrouch=v);
            Toggle(p,"Movement training HUD",s.Training,v=>s.Training=v); Toggle(p,"Mobile Surf Assist",s.SurfAssist,v=>s.SurfAssist=v);
            Slider(p,"Render scale",s.RenderScale,.5f,1,v=>{s.RenderScale=v;Apply();});
            Toggle(p,"Adaptive Android resolution",s.AdaptiveResolution,v=>s.AdaptiveResolution=v);
            Button(p,"Frame limit: "+s.FrameLimit,()=>{int[] values={30,60,120,240};int n=Array.IndexOf(values,s.FrameLimit);s.FrameLimit=values[(n+1)%4];Apply();SettingsMenu(inGame);},48);
            Button(p,"Graphics: "+new[]{"Low","Balanced","High"}[s.Quality],()=>{s.Quality=(s.Quality+1)%3;s.RenderScale=s.Quality==0?.75f:s.Quality==1?.9f:1;Apply();SettingsMenu(inGame);},48);
            Slider(p,"Crosshair size",s.CrosshairSize,2,24,v=>s.CrosshairSize=Mathf.RoundToInt(v));
            Button(p,"Crosshair: "+new[]{"Cross","Dot","Open"}[s.CrosshairStyle],()=>{s.CrosshairStyle=(s.CrosshairStyle+1)%3;SettingsMenu(inGame);},48);
            Slider(p,"Touch transparency",1-s.TouchOpacity,0,1,v=>s.TouchOpacity=1-v);
            Slider(p,"Touch button scale",s.ButtonScale,.6f,1.8f,v=>s.ButtonScale=v);
            Button(p,"Movement tuning",()=>MovementMenu(inGame,false),48);
            if(session.Mobile)
            {
                foreach(TouchButton button in session.Input.Mobile.Controls.Layout) { TouchButton b=button; Slider(p,b.Action+" size",b.Size,.035f,.11f,v=>b.Size=v); }
                if(inGame) Button(p,"Drag touch buttons",()=>BeginTouchEdit());
            }
            string[] actions={"Forward","Back","Left","Right","Jump","Crouch","Walk","Shoot","Aim","Reload","Scoreboard"};
            for(int i=0;i<actions.Length;i++) { int index=i; Button(p,actions[i]+": "+s.Keys[i],()=>{bindingIndex=index;bindingAction=actions[index];Transform capture=Page("PRESS A KEY");Label(capture,"Bind "+bindingAction+". Escape cancels.",22,50);},44); }
            Button(p,"Save & back",()=>{s.TouchLayout=session.Input.Mobile.Controls.Layout.ToArray();session.Settings.Save();Apply();bindingIndex=-1;if(inGame) PauseMenu();else MainMenu();});
        }
        private void BeginTouchEdit()
        {
            if(menu!=null) Destroy(menu.gameObject); menu=Rect("Edit buttons",safe); Stretch(menu); page="edit"; editing=true; session.Input.Mobile.Controls.Clear(); session.Input.Mobile.Controls.Editing=true; session.Input.Enabled=true;
            hud.gameObject.SetActive(true); MakeTouch(); hint.text="Drag buttons. Finish to save.";
            ButtonAt(menu,"FINISH",new Vector2(.5f,.94f),new Vector2(200,55),()=>{
                session.Input.Mobile.Controls.Editing=false; session.Input.Enabled=false; session.Settings.Value.TouchLayout=session.Input.Mobile.Controls.Layout.ToArray(); session.Settings.Save(); editing=false; PauseMenu();
            });
        }
        private void MovementMenu(bool inGame,bool bhop)
        {
            Transform p=Page(bhop?"BHOP PHYSICS":"SURF / COMBAT PHYSICS");
            MovementConfig c=bhop?session.Settings.Value.BhopPhysics:session.Settings.Value.SurfPhysics;
            Label(p,"Next session. Modified rules count as practice.",18,45,Muted);
            Button(p,bhop?"Edit surf rules":"Edit bhop rules",()=>MovementMenu(inGame,!bhop),48);
            Slider(p,"sv_gravity (u/s²)",c.sv_gravity/.0254f,0,1600,v=>c.sv_gravity=v*.0254f);
            Slider(p,"sv_accelerate",c.sv_accelerate,1,30,v=>c.sv_accelerate=v);
            Slider(p,"sv_airaccelerate",c.sv_airaccelerate,1,400,v=>c.sv_airaccelerate=v);
            Slider(p,"sv_friction",c.sv_friction,0,12,v=>c.sv_friction=v);
            Slider(p,"sv_maxspeed (u/s)",c.sv_maxspeed/.0254f,100,800,v=>c.sv_maxspeed=v*.0254f);
            Slider(p,"sv_stopspeed (u/s)",c.sv_stopspeed/.0254f,0,300,v=>c.sv_stopspeed=v*.0254f);
            Slider(p,"sv_jump_impulse (u/s)",c.sv_jump_impulse/.0254f,100,600,v=>c.sv_jump_impulse=v*.0254f);
            Slider(p,"sv_air_max_wishspeed (u/s)",c.sv_air_max_wishspeed/.0254f,1,150,v=>c.sv_air_max_wishspeed=v*.0254f);
            Slider(p,"Speed limit (u/s; 0 is unlimited)",c.SpeedLimit/.0254f,0,4000,v=>c.SpeedLimit=v*.0254f);
            Toggle(p,"sv_autobunnyhopping allowed",c.sv_autobunnyhopping,v=>c.sv_autobunnyhopping=v);
            Button(p,"Restore preset",()=>{c.CopyFrom(MovementConfig.Preset(bhop?MapMode.Bhop:MapMode.Surf));MovementMenu(inGame,bhop);});
            Button(p,"Back",()=>SettingsMenu(inGame));
        }
        private void MakeTouch()
        {
            foreach(Transform child in touchRoot) Destroy(child.gameObject); touchLabels.Clear(); touchRoot.gameObject.SetActive(session.Mobile);
            if(!session.Mobile) return;
            foreach(TouchButton button in session.Input.Mobile.Controls.Layout)
            {
                Text label=At(button.Action,touchRoot,new Vector2(button.X,button.Y),new Vector2(100,80),18,TextAnchor.MiddleCenter); label.text=button.Action.ToUpperInvariant();
                var background=Rect("Button ring",label.transform); Stretch(background); background.SetAsFirstSibling(); var image=background.gameObject.AddComponent<TouchGraphic>(); image.color=new Color(.55f,.7f,.7f,.5f); image.raycastTarget=false;
                touchLabels.Add(label);
            }
            joystick=Rect("Joystick",touchRoot);joystick.anchorMin=joystick.anchorMax=new Vector2(.15f,.2f);joystick.sizeDelta=new Vector2(150,150);var ring=joystick.gameObject.AddComponent<TouchGraphic>();ring.raycastTarget=false;ring.color=new Color(.55f,.7f,.7f,.3f);
            stick=Rect("Stick",joystick);stick.anchorMin=stick.anchorMax=new Vector2(.5f,.5f);stick.sizeDelta=new Vector2(44,44);var thumb=stick.gameObject.AddComponent<TouchGraphic>();thumb.Ring=false;thumb.raycastTarget=false;thumb.color=new Color(.55f,.7f,.7f,.4f);
        }
        private static string Format(float seconds)
        {
            int minutes=(int)seconds/60; return minutes.ToString("00")+":"+(seconds%60).ToString("00.000");
        }
        private void Update()
        {
            Rect area=Screen.safeArea;
            if(area!=previousSafe) { previousSafe=area; safe.anchorMin=new Vector2(area.xMin/Screen.width,area.yMin/Screen.height);safe.anchorMax=new Vector2(area.xMax/Screen.width,area.yMax/Screen.height); }
            if(bindingIndex>=0)
            {
                hint.text="Press a key for "+bindingAction;
                foreach(KeyCode key in Enum.GetValues(typeof(KeyCode))) if(UnityEngine.Input.GetKeyDown(key)) { if(key!=KeyCode.Escape) session.Settings.Value.Keys[bindingIndex]=key.ToString();bindingIndex=-1;SettingsMenu(session.Running);break; }
            }
            if(page=="join"&&(session.Discovery?.Servers.Count??0)!=lastBrowserCount) JoinMenu();
            if(page=="connecting") { if(menu!=null) {Text[] labels=menu.GetComponentsInChildren<Text>();if(labels.Length>1) labels[1].text=session.Status;} return; }
            if(!session.Running||session.Local==null) return;
            hudTime+=Time.unscaledDeltaTime;
            if(editing) session.Input.Mobile.Poll(session.Settings.Value,session.Mode==MapMode.Combat);
            if(hudTime<.05f&&!editing) return; hudTime=0;
            PlayerState p=session.Local; MovementState m=p.Movement; bool combat=session.Mode==MapMode.Combat;
            speed.text=Mathf.RoundToInt(m.Velocity.HorizontalSpeed/.0254f)+" u/s";
            timer.text=combat?"":Format(p.Timer.Seconds)+(p.Timer.Finished?"  FINISHED":"");
            int best=session.Settings.Records.BestTicks[session.Simulation.Map.Id];
            details.text=combat?"":(p.Timer.Practice?"PRACTICE · ":"")+"CP "+p.Timer.Checkpoint+" · "+m.Jumps+" jumps · PB "+(best>0?Format(best/(float)MovementConfig.TickRate):"—");
            health.text=combat?(p.Health>0?p.Health+" HP":"RESPAWNING"):"";
            WeaponDefinition weapon=WeaponCatalog.All[p.Weapon.Selected];
            ammo.text=combat?weapon.Name+(weapon.Capacity>0?"   "+p.Weapon.Ammo[p.Weapon.Selected]+" / "+p.Weapon.Reserve[p.Weapon.Selected]:"")+(p.Weapon.ReloadFinishTick>0?"   RELOAD":""):"";
            status.text=session.Simulation.Map.Name+(session.Server!=null?" · LAN HOST":session.Client!=null?" · LAN":" · SOLO");
            if(!editing) hint.text=session.Settings.Value.Training?(m.Grounded?"GROUND":"AIR")+" · "+m.SyncPercent.ToString("0")+"% sync · max "+Mathf.RoundToInt(m.MaxSpeed/.0254f)+" u/s · "+(session.Mobile?"":"R restart / T checkpoint"):"";
            bool held=false; if(Enum.TryParse(session.Settings.Value.Keys[10],out KeyCode scoreKey)) held=UnityEngine.Input.GetKey(scoreKey);
            score.text="";scoreStats.text=scoreSpeed.text="";
            if(showScore||held)
            {
                score.text="PLAYER\n";scoreStats.text="K / D\n";scoreSpeed.text="SPEED\n";
                foreach(PlayerState player in session.VisiblePlayers) {score.text+=player.Name+"\n";scoreStats.text+=player.Kills+" / "+player.Deaths+"\n";scoreSpeed.text+=Mathf.RoundToInt(player.Movement.Velocity.HorizontalSpeed/.0254f)+"\n";}
            }
            feed.text=""; IEnumerable<GameEvent> events=session.Client!=null?session.Client.Events:session.Simulation.Events;
            foreach(GameEvent e in events) if(session.Simulation.Tick-e.Tick<600)
            {
                string killer="#"+e.Killer,victim="#"+e.Victim;
                foreach(PlayerState player in session.VisiblePlayers) {if(player.Id==e.Killer) killer=player.Name;if(player.Id==e.Victim) victim=player.Name;}
                feed.text+=killer+"  · "+WeaponCatalog.All[e.Weapon].Name+" ·  "+victim+"\n";
            }
            UpdateCrosshair();
            for(int i=0;i<touchLabels.Count;i++)
            {
                TouchButton b=session.Input.Mobile.Controls.Layout[i]; Text label=touchLabels[i]; bool movement=b.Action=="Jump"||b.Action=="Crouch";
                label.gameObject.SetActive(combat||movement||editing);
                label.rectTransform.anchorMin=label.rectTransform.anchorMax=new Vector2(b.X,b.Y);
                float diameter=b.Size*2*safe.rect.height*session.Settings.Value.ButtonScale; label.rectTransform.sizeDelta=new Vector2(diameter,diameter);
                label.color=new Color(Ink.r,Ink.g,Ink.b,session.Settings.Value.TouchOpacity);
                TouchGraphic ring=label.GetComponentInChildren<TouchGraphic>(); if(ring!=null) ring.color=new Color(.55f,.7f,.7f,session.Settings.Value.TouchOpacity);
            }
            if(session.Mobile&&joystick!=null)
            {
                MobileControls controls=session.Input.Mobile.Controls;joystick.anchorMin=joystick.anchorMax=new Vector2(controls.JoystickX,controls.JoystickY);
                float diameter=controls.Radius*2*safe.rect.height;joystick.sizeDelta=new Vector2(diameter,diameter);stick.anchoredPosition=new Vector2(controls.Side,controls.Forward)*(diameter*.35f);
            }
        }
        private void UpdateCrosshair()
        {
            int size=session.Settings.Value.CrosshairSize,style=session.Settings.Value.CrosshairStyle;
            for(int i=0;i<crosshair.Count;i++)
            {
                Image image=crosshair[i]; image.gameObject.SetActive(style==1?i==4:style==2?i<4:true);
                RectTransform r=image.rectTransform; r.sizeDelta=i==4?new Vector2(3,3):i<2?new Vector2(size,2):new Vector2(2,size);
                r.anchoredPosition=i==0?new Vector2(-size*.5f-4,0):i==1?new Vector2(size*.5f+4,0):i==2?new Vector2(0,size*.5f+4):i==3?new Vector2(0,-size*.5f-4):Vector2.zero;
            }
        }
    }
}
