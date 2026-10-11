using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Velocity.Editor
{
    public static class VelocityBuild
    {
        public const string ScenePath="Assets/Velocity/Scenes/Bootstrap.unity";
        [MenuItem("Velocity/Configure project")]
        public static void Configure()
        {
            Directory.CreateDirectory("Assets/Velocity/Settings");
            const string rendererPath="Assets/Velocity/Settings/VelocityRenderer.asset";
            const string pipelinePath="Assets/Velocity/Settings/VelocityURP.asset";
            UniversalRendererData renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if(renderer==null) { renderer=ScriptableObject.CreateInstance<UniversalRendererData>(); AssetDatabase.CreateAsset(renderer,rendererPath); }
            UniversalRenderPipelineAsset pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            if(pipeline==null)
            {
                pipeline=UniversalRenderPipelineAsset.Create(renderer);
                var serialized=new SerializedObject(pipeline);
                SerializedProperty list=serialized.FindProperty("m_RendererDataList");
                list.arraySize=1; list.GetArrayElementAtIndex(0).objectReferenceValue=renderer;
                serialized.FindProperty("m_MainLightShadowsSupported").boolValue=false;
                serialized.FindProperty("m_AdditionalLightsRenderingMode").intValue=0;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                pipeline.renderScale=1; pipeline.msaaSampleCount=1; pipeline.shadowDistance=0;
                AssetDatabase.CreateAsset(pipeline,pipelinePath);
            }
            GraphicsSettings.defaultRenderPipeline=pipeline;
            QualitySettings.renderPipeline=pipeline;
            // Populate URP's shader/resource settings using the pinned package's editor hook.
            typeof(UniversalRenderPipelineGlobalSettings).GetMethod("Ensure",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)?.Invoke(null,new object[]{true});
            var graphics=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            SerializedProperty included=graphics.FindProperty("m_AlwaysIncludedShaders");
            foreach(string shaderName in new[]{"Universal Render Pipeline/Lit","Universal Render Pipeline/Unlit","Velocity/VertexUnlit","UI/Default","GUI/Text Shader"})
            {
                Shader shader=Shader.Find(shaderName); if(shader==null) continue; bool exists=false;
                for(int i=0;i<included.arraySize;i++) if(included.GetArrayElementAtIndex(i).objectReferenceValue==shader) exists=true;
                if(!exists) { int index=included.arraySize++; included.GetArrayElementAtIndex(index).objectReferenceValue=shader; }
            }
            graphics.ApplyModifiedPropertiesWithoutUndo();
            QualitySettings.vSyncCount=0;
            QualitySettings.shadows=ShadowQuality.Disable;
            PlayerSettings.companyName="atlasru"; PlayerSettings.productName="Velocity";
            PlayerSettings.bundleVersion="0.1.0";
            PlayerSettings.colorSpace=ColorSpace.Linear;
            PlayerSettings.defaultScreenWidth=1920; PlayerSettings.defaultScreenHeight=1080;
            PlayerSettings.runInBackground=true;
            PlayerSettings.defaultInterfaceOrientation=UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToPortrait=false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown=false;
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android,"ru.atlas.velocity");
            PlayerSettings.Android.minSdkVersion=AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetSdkVersion=(AndroidSdkVersions)36;
            PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
            PlayerSettings.Android.bundleVersionCode=100;
            PlayerSettings.Android.forceInternetPermission=true;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
            PlayerSettings.SetApiCompatibilityLevel(UnityEditor.Build.NamedBuildTarget.Android,ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.SetApiCompatibilityLevel(UnityEditor.Build.NamedBuildTarget.Standalone,ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android,false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,new[]{GraphicsDeviceType.OpenGLES3});
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64,false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,new[]{GraphicsDeviceType.Direct3D11});
            // Legacy input is deliberately used for portable touch/mouse APIs with no extra packages.
            var playerAsset=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            SerializedProperty activeInput=playerAsset.FindProperty("activeInputHandler");
            if(activeInput!=null) { activeInput.intValue=0; playerAsset.ApplyModifiedPropertiesWithoutUndo(); }
            if(!File.Exists(ScenePath))
            {
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene,ScenePath);
            }
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
            AssetDatabase.SaveAssets();
        }
        public static void Build()
        {
            Configure();
            string targetName=Argument("-buildTarget","StandaloneWindows64");
            BuildTarget target=(BuildTarget)Enum.Parse(typeof(BuildTarget),targetName,true);
            string output=Argument("-buildPath",$"build/{targetName}/"+(target==BuildTarget.Android?"Velocity.apk":"Velocity.exe"));
            if(target==BuildTarget.Android&&!output.EndsWith(".apk",StringComparison.OrdinalIgnoreCase)) output=Path.Combine(output,"Velocity.apk");
            if(target==BuildTarget.StandaloneWindows64&&!output.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)) output=Path.Combine(output,"Velocity.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            EditorUserBuildSettings.buildAppBundle=false;
            BuildReport report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName=output,target=target,options=BuildOptions.None});
            if(report.summary.result!=BuildResult.Succeeded) throw new Exception("Velocity build failed: "+report.summary.result+", "+report.summary.totalErrors+" errors");
            Debug.Log("Velocity build verified: "+output);
        }
        private static string Argument(string key,string fallback)
        {
            string[] args=Environment.GetCommandLineArgs();
            for(int i=0;i<args.Length-1;i++) if(args[i]==key) return args[i+1];
            return fallback;
        }
    }
    [InitializeOnLoad]
    internal static class ConfigureOnFirstImport
    {
        static ConfigureOnFirstImport()
        {
            if(!File.Exists("Assets/Velocity/Settings/VelocityURP.asset")) EditorApplication.delayCall+=VelocityBuild.Configure;
        }
    }
}
