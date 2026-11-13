using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Muhanok.Editor
{
    // Does not download, log in or fabricate external clips. Only imports files actually present.
    public static class MuhanokMixamoImport
    {
        public const string Folder="Assets/External/Staging/Mixamo/Animations";
        [InitializeOnLoadMethod]
        private static void ScheduleMissingMotionRepair()
        {
            EditorApplication.delayCall += RepairMissingMotions;
        }
        [MenuItem("Mongscape/Repair Missing Public Source Motions")]
        public static void RepairMissingMotions()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += RepairMissingMotions;
                return;
            }
            int repaired = 0;
            foreach (string name in new[] { "Monkey", "Police" })
            {
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>("Assets/Game/Content/ArtV2/" + name + "Humanoid.overrideController");
                if (controller == null) continue;
                var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                controller.GetOverrides(pairs);
                bool changed = false;
                for (int i = 0; i < pairs.Count; i++)
                {
                    if (pairs[i].Value != null || pairs[i].Key == null) continue;
                    string path = "Assets/External/Staging/Blender/Characters/" + pairs[i].Key.name + ".fbx";
                    var authored = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__") && c.isHumanMotion);
                    if (authored == null) throw new InvalidOperationException("Missing public-source authored motion: " + pairs[i].Key.name);
                    pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(pairs[i].Key, authored);
                    repaired++; changed = true;
                }
                if (!changed) continue;
                controller.ApplyOverrides(pairs); EditorUtility.SetDirty(controller);
            }
            if (repaired == 0) return;
            AssetDatabase.SaveAssets();
            Debug.Log("MUHANOK_PUBLIC_MOTIONS_PASS: " + repaired + " missing overrides restored from authored Blender motions");
        }
        public static void ValidatePublicSource()
        {
            RepairMissingMotions();
            int checkedCount = 0;
            foreach (string name in new[] { "Monkey", "Police" })
            {
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>("Assets/Game/Content/ArtV2/" + name + "Humanoid.overrideController");
                if (controller == null) throw new InvalidOperationException("Public source controller missing: " + name);
                var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>(); controller.GetOverrides(pairs);
                foreach (var pair in pairs)
                {
                    if (pair.Value == null || !pair.Value.isHumanMotion || pair.Value.length <= 0)
                        throw new InvalidOperationException("Public source motion invalid: " + name + " / " + pair.Key.name);
                    checkedCount++;
                }
            }
            Debug.Log("MUHANOK_PUBLIC_SOURCE_PASS: " + checkedCount + " valid humanoid overrides");
        }
        private static string MotionFor(string state)
        {
            if(state.Contains("Run")||state.Contains("SprintStart")||state.Contains("LaneStep")) return "Run";
            if(state.EndsWith("Jump")) return "Jump";
            if(state.EndsWith("CrouchSlide")) return "Crouch";
            if(state.EndsWith("Stumble")) return "Stumble";
            return null;
        }
        public static AnimationClip Find(string state)
        {
            string motion=MotionFor(state); if(motion==null) return null;
            return AssetDatabase.LoadAllAssetsAtPath(Folder+"/"+motion+".fbx").OfType<AnimationClip>().FirstOrDefault(c=>!c.name.StartsWith("__")&&c.isHumanMotion);
        }
        [MenuItem("Mongscape/Import Downloaded Mixamo Motions")]
        public static void ImportDownloaded()
        {
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh(); int count=0;
            foreach(string motion in new[]{"Run","Jump","Crouch","Stumble"})
            {
                string path=Folder+"/"+motion+".fbx"; if(!File.Exists(path)) continue;
                var importer=(ModelImporter)AssetImporter.GetAtPath(path);
                importer.animationType=ModelImporterAnimationType.Human; importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importCameras=false; importer.importLights=false; importer.SaveAndReimport();
                var clips=importer.defaultClipAnimations;
                if(clips.Length==0) throw new InvalidOperationException("No animation in downloaded file: "+path);
                clips[0].name="Mixamo_"+motion; clips[0].loopTime=motion=="Run"||motion=="Crouch";
                clips[0].lockRootHeightY=true; clips[0].lockRootPositionXZ=true; clips[0].lockRootRotation=true;
                clips[0].keepOriginalOrientation=true; clips[0].keepOriginalPositionXZ=true;
                importer.clipAnimations=clips; importer.SaveAndReimport();
                var avatar=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
                if(avatar==null||!avatar.isValid||!avatar.isHuman) throw new InvalidOperationException("Downloaded motion needs Humanoid bone mapping: "+path);
                count++;
            }
            if(count==0) { Debug.LogWarning("MIXAMO_WAITING_FOR_DOWNLOAD: no external FBX present; authored Blender motions remain active"); return; }
            foreach(string name in new[]{"Monkey","Police"})
            {
                var controller=AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>("Assets/Game/Content/ArtV2/"+name+"Humanoid.overrideController");
                var pairs=new List<KeyValuePair<AnimationClip,AnimationClip>>(); controller.GetOverrides(pairs);
                for(int i=0;i<pairs.Count;i++) { var replacement=Find(pairs[i].Key.name); if(replacement!=null) pairs[i]=new KeyValuePair<AnimationClip,AnimationClip>(pairs[i].Key,replacement); }
                controller.ApplyOverrides(pairs); EditorUtility.SetDirty(controller);
            }
            AssetDatabase.SaveAssets(); ValidateDownloaded(); Debug.Log("MIXAMO_IMPORTED: "+count+" actual external FBX files; humanoid retargeting/root-motion lock applied");
        }
        public static void ValidateDownloaded()
        {
            int mapped=0;
            foreach(string name in new[]{"Monkey","Police"})
            {
                var controller=AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>("Assets/Game/Content/ArtV2/"+name+"Humanoid.overrideController");
                var pairs=new List<KeyValuePair<AnimationClip,AnimationClip>>(); controller.GetOverrides(pairs);
                foreach(var pair in pairs)
                {
                    var expected=Find(pair.Key.name); if(expected==null) continue;
                    if(pair.Value!=expected||!expected.isHumanMotion||expected.length<=0)
                        throw new InvalidOperationException("Mixamo retarget binding failed: "+name+" / "+pair.Key.name);
                    Debug.Log("MIXAMO_BINDING_PASS: "+name+" / "+pair.Key.name+" / "+AssetDatabase.GetAssetPath(expected)+" / "+expected.length+" seconds");
                    mapped++;
                }
            }
            if(mapped>0) Debug.Log("MIXAMO_VALIDATION_PASS: "+mapped+" actual external humanoid state bindings");
        }
    }
}
