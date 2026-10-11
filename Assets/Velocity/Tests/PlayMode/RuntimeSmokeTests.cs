using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Velocity.Core;
using Velocity.Runtime;

namespace Velocity.Tests
{
    public sealed class RuntimeSmokeTests
    {
        [UnityTest]
        public IEnumerator EveryMapStartsWithMeshCameraAndClearPlayerHull()
        {
            GameSession game=Object.FindFirstObjectByType<GameSession>();
            if(game==null) game=new GameObject("Test session").AddComponent<GameSession>();
            for(byte map=0;map<3;map++)
            {
                game.StartSolo(map);game.Pause(true);yield return null;
                Assert.IsNotNull(game.Local);
                Assert.IsInstanceOf<UniversalRenderPipelineAsset>(GraphicsSettings.currentRenderPipeline);
                Assert.IsFalse(game.Simulation.Map.World.Overlaps(game.Local.Movement.Position,game.Simulation.Movement.PlayerHull(game.Local.Movement)));
                Assert.IsNotNull(game.View.Camera);
                Assert.Greater(Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).Length,4);
                LogAssert.NoUnexpectedReceived();
            }
            game.ReturnToMenu();
        }
        [UnityTest]
        public IEnumerator SoloPauseStopsTimerAndMenuCanResume()
        {
            GameSession game=Object.FindFirstObjectByType<GameSession>();
            if(game==null) game=new GameObject("Test session").AddComponent<GameSession>();
            game.StartSolo(1);game.Pause(true);int tick=game.Simulation.Tick;
            yield return new WaitForSecondsRealtime(.1f);
            Assert.AreEqual(tick,game.Simulation.Tick);
            game.Pause(false);yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();
            Assert.Greater(game.Simulation.Tick,tick);game.ReturnToMenu();
        }
        [Test]
        public void SettingsAndRecordsSurviveAStorageRestart()
        {
            string folder=System.IO.Path.Combine(Application.temporaryCachePath,"velocity-storage-"+System.Guid.NewGuid().ToString("N"));
            try
            {
                var store=new SettingsManager(folder);store.Value.Fov=112;store.Value.Keys[4]="V";store.Save();
                var player=new PlayerState{Timer=new TimerState{Finished=true,ElapsedTicks=1200},Movement=new MovementState{MaxSpeed=20}};store.Record(0,player);
                var loaded=new SettingsManager(folder);Assert.AreEqual(112,loaded.Value.Fov);Assert.AreEqual("V",loaded.Value.Keys[4]);Assert.AreEqual(1200,loaded.Records.BestTicks[0]);
                store.Value.Fov=90;store.Save();System.IO.File.WriteAllText(System.IO.Path.Combine(folder,"settings.json"),"corrupt json");
                var recovered=new SettingsManager(folder);Assert.AreEqual(112,recovered.Value.Fov);
            }
            finally {if(System.IO.Directory.Exists(folder))System.IO.Directory.Delete(folder,true);}
        }
    }
}
