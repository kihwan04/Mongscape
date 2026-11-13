using System;
using System.IO;
using Muhanok.Bootstrap;
using Muhanok.Domain;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Muhanok.Editor
{
    [InitializeOnLoad]
    public static class MuhanokBuildTools
    {
        private const string PreviewKey = "Muhanok.Preview";
        private const string StepKey = "Muhanok.PreviewStep";
        static MuhanokBuildTools() { EditorApplication.update += PreviewTick; EditorApplication.playModeStateChanged += Changed; }
        public const string ReleaseVersion = "1.0.0";
        // Release: what players download. No development player, no tooling flags, fullscreen on first launch.
        [MenuItem("Mongscape/Build Windows Release")]
        public static void BuildWindowsRelease() => BuildWindows(true);
        // Dev: development player for QA scripts, captures and recordings (they read their command-line flags only here).
        [MenuItem("Mongscape/Build Windows Dev (QA)")]
        public static void BuildWindowsDev() => BuildWindows(false);
        private static void BuildWindows(bool release)
        {
            // A bad face server address stops the build before it starts, not after.
            IdentityServerSetup.Resolve(out _);
            ValidateContent();
            // Graphics options enable this at runtime. Advertise it at build time so Unity retains its compute resources.
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            if(pipeline==null)throw new Exception("PC render pipeline missing");
            pipeline.gpuResidentDrawerMode=GPUResidentDrawerMode.InstancedDrawing;
            EditorUtility.SetDirty(pipeline);AssetDatabase.SaveAssetIfDirty(pipeline);
            PlayerSettings.productName = "Mongscape";
            PlayerSettings.bundleVersion = ReleaseVersion;
            // Interim icon composed from the title logo until dedicated icon art exists.
            var icon=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Game/Content/Branding/MongscapeIcon.png");
            if(icon==null)throw new Exception("App icon missing");
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown,new[]{icon},IconKind.Any);
            PlayerSettings.defaultScreenWidth = OutputResolution.Width; PlayerSettings.defaultScreenHeight = OutputResolution.Height;
            // The saved settings decide the mode after launch; this is only the first frame before they load.
            PlayerSettings.fullScreenMode = release ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            // Ordinary logs keep no stack traces in a release player; warnings and errors keep theirs for bug reports.
            PlayerSettings.SetStackTraceLogType(LogType.Log, release ? StackTraceLogType.None : StackTraceLogType.ScriptOnly);
            string folder = release ? "Windows" : "WindowsDev";
            string destination = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../../Builds/Mongscape/" + folder + "/Mongscape.exe"));
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            string complete=Path.Combine(Path.GetDirectoryName(destination),"Mongscape.complete");
            if(File.Exists(complete))File.Delete(complete);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Game/Scenes/GameScene.unity" }, locationPathName = destination,
                target = BuildTarget.StandaloneWindows64, options = release ? BuildOptions.None : BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("MUHANOK_BUILD_FAIL: " + report.summary.result);
            // The game reads identity-server.json next to the exe; no environment variable is needed on the player PC.
            IdentityServerSetup.ApplyToBuild(Path.GetDirectoryName(destination));
            // "release 1.0.0" or "dev 1.0.0" on the first line: packaging refuses anything but a release build.
            File.WriteAllText(complete,(release ? "release " : "dev ") + ReleaseVersion + Environment.NewLine + DateTime.UtcNow.ToString("O") + Environment.NewLine);
            Debug.Log("MUHANOK_BUILD_PASS: " + (release ? "release " : "dev ") + ReleaseVersion + " / " + destination + " / " + report.summary.totalSize + " bytes");
        }
        public static void ValidateContent()
        {
            int checkedCount = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Game/Content" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid); var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach (var transform in prefab.GetComponentsInChildren<Transform>(true))
                    foreach (var component in transform.GetComponents<Component>())
                        if (component == null) throw new Exception("Missing script: " + path + " / " + transform.name);
                checkedCount++;
            }
            var scene = EditorSceneManager.OpenScene("Assets/Game/Scenes/GameScene.unity");
            foreach (var root in scene.GetRootGameObjects()) foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                foreach (var component in transform.GetComponents<Component>()) if (component == null) throw new Exception("Missing scene script: " + transform.name);
            var game = UnityEngine.Object.FindFirstObjectByType<GameBootstrap>();
            if (game == null || game.Settings == null || game.IntroTimeline == null || game.ChunkPrefabs.Length != MuhanokContentBuilder.ChunkIds.Length)
                throw new Exception("GameScene content reference missing");
            foreach (var chunk in game.ChunkPrefabs)
                if (chunk == null || chunk.Entry == null || chunk.Exit == null || chunk.Obstacles.Length != 6) throw new Exception("Chunk contract incomplete");
            foreach (var track in game.IntroTimeline.GetOutputTracks()) foreach (var clip in track.GetClips())
                if (!(clip.asset is ScriptableObject clipAsset) || MonoScript.FromScriptableObject(clipAsset) == null) throw new Exception("Missing Timeline clip script");
            Debug.Log("MUHANOK_REFERENCES_PASS: " + checkedCount + " prefabs / GameScene / Timeline");
        }
        [MenuItem("Mongscape/Capture Scene Previews")]
        public static void Preview()
        {
            SessionState.SetBool(PreviewKey, true); SessionState.SetInt(StepKey, 0);
            EditorSceneManager.OpenScene("Assets/Game/Scenes/GameScene.unity"); EditorApplication.isPlaying = true;
        }
        private static void PreviewTick()
        {
            if (!SessionState.GetBool(PreviewKey, false) || !EditorApplication.isPlaying || Time.frameCount < 5) return;
            var game = UnityEngine.Object.FindFirstObjectByType<GameBootstrap>(); if (game == null) return;
            int step = SessionState.GetInt(StepKey, 0);
            if (step == 0) { Capture(game.OutputCamera, "Muhanok_TitleStage"); game.StartIntro(); SessionState.SetInt(StepKey, 1); }
            else if (step == 1 && game.Intro.Elapsed >= 3.3f) { Capture(game.OutputCamera, "Muhanok_Idea"); SessionState.SetInt(StepKey, 2); }
            else if (step == 2 && game.Intro.Elapsed >= 7.2f) { Capture(game.OutputCamera, "Muhanok_Takeover"); SessionState.SetInt(StepKey, 3); }
            else if (step == 3 && game.Session.Score.Distance > 92)
            {
                Capture(game.OutputCamera, "Muhanok_FirstPerson"); SessionState.SetInt(StepKey, 4); EditorApplication.isPlaying = false;
            }
        }
        private static void Changed(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(PreviewKey, false))
            {
                SessionState.SetBool(PreviewKey, false); Debug.Log("MUHANOK_PREVIEW_PASS");
                if (UnityEngine.Application.isBatchMode) EditorApplication.Exit(0);
            }
        }
        private static void Capture(Camera camera, string id)
        {
            var target = new RenderTexture(1280, 720, GraphicsFormat.R8G8B8A8_SRGB, CoreUtils.GetDefaultDepthOnlyFormat()); target.Create();
            var request = new RenderPipeline.StandardRequest { destination = target, mipLevel = 0, slice = 0, face = CubemapFace.Unknown };
            RenderPipeline.SubmitRenderRequest(camera, request);
            RenderTexture old = RenderTexture.active; RenderTexture.active = target;
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false); texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply();
            string path = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../../Builds/QA/VisualReview/" + id + ".png"));
            Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllBytes(path, texture.EncodeToPNG());
            RenderTexture.active = old; UnityEngine.Object.Destroy(texture); target.Release(); UnityEngine.Object.Destroy(target);
            Debug.Log("MUHANOK_PREVIEW_SAVED: " + path);
        }
    }
}
