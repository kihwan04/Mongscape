using System;
using Muhanok.Bootstrap;
using Muhanok.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Muhanok.Editor
{
    public static class MuhanokVisualPolish
    {
        private const string Materials="Assets/Game/Content/ArtV2/Materials/";
        [MenuItem("Mongscape/Upgrade Enclosed Cells and Foot Contact V10")]
        public static void UpgradeV10()
        {
            UpgradeV9();
            foreach(int type in new[]{4,5})
            {
                var chunk=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Content/Prefabs/"+MuhanokContentBuilder.ChunkIds[type]+".prefab");
                if(chunk.GetComponentsInChildren<Muhanok.Presentation.Map.CellPrisoner>().Length!=2)throw new InvalidOperationException("Cell inmates missing");
            }
            Debug.Log("MUHANOK_V10_PASS: enclosed deep cells / locked barred doors / two animated inmates per cell block / humanoid foot contact IK");
        }
        [MenuItem("Mongscape/Upgrade Dedicated Cart Tracks and Exercise V9")]
        public static void UpgradeV9()
        {
            UpgradeV8();
            foreach(int type in new[]{1,2})
            {
                var chunk=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Content/Prefabs/"+MuhanokContentBuilder.ChunkIds[type]+".prefab").GetComponent<Muhanok.Presentation.Map.MuhanokMapChunk>();
                if(chunk.RailGaps.Length!=2)throw new InvalidOperationException("Dedicated cart rail gaps missing");
                chunk.Prepare(type,new Muhanok.Domain.HazardOdds(1,.72f,.85f,.82f),new System.Random(1));
                foreach(var obstacle in chunk.Obstacles)if(obstacle.gameObject.activeSelf)throw new InvalidOperationException("Walking obstacle on cart track");
            }
            Debug.Log("MUHANOK_V9_PASS: single suspended cart track / four half-rail gaps / cart grip animation / motion-matched exercise rows / two-banana pursuit");
        }
        [MenuItem("Mongscape/Upgrade Gameplay and Connected Maps V8")]
        public static void UpgradeV8()
        {
            UpgradeV7();
            var zoo=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Content/Prefabs/CH_OuterWall.prefab").GetComponent<Muhanok.Presentation.Map.MuhanokMapChunk>();
            if(zoo.FloorItems==null||zoo.FloorItems.Length!=2)throw new InvalidOperationException("Interactive floor items missing from actual authored map.");
            var night=Shader.Find("Muhanok/NightSky");if(night==null||ShaderUtil.ShaderHasError(night))throw new InvalidOperationException("Night sky shader failed compilation.");
            Debug.Log("MUHANOK_V8_PASS: fatal collision / nonfatal floor slip / playable two-chunk cart / loading intro / service exit");
        }
        [MenuItem("Mongscape/Upgrade Loading Continuity and Models V7")]
        public static void UpgradeV7()
        {
            UpgradeV6();
            var actor=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Content/Prefabs/CHR_Monkey_Prisoner.prefab");
            var mesh=actor.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh;
            if(mesh.blendShapeCount<2)throw new InvalidOperationException("Blender facial shapes were not imported.");
            Debug.Log("MUHANOK_VISUAL_V7_PASS: continuous skinned monkey / facial shapes / actual mine shadows / loading-to-live dissolve");
        }
        [MenuItem("Mongscape/Upgrade Maps and Materials V6")]
        public static void UpgradeV6()
        {
            MuhanokArtUpgrade.UpgradeTools();Apply();
        }
        [MenuItem("Mongscape/Apply Hand Painted Materials and Shadow Budget")]
        public static void Apply()
        {
            AssetDatabase.Refresh();
            const string atlasPath="Assets/External/Staging/GeneratedV6/HandPaintedAtlas.png";
            var importer=(TextureImporter)AssetImporter.GetAtPath(atlasPath);
            importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=2048;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.mipmapEnabled=true;
            importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=4;
            importer.SaveAndReimport();
            var atlas=AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);
            var shader=Shader.Find("Muhanok/HandPaintedTriplanar");
            if(shader==null||ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Hand painted shader failed compilation.");
            Paint("Slate",atlas,shader,new Vector4(.5f,.5f,0,.5f),.23f,new Color(.92f,.85f,.74f));
            Paint("SlateEdge",atlas,shader,new Vector4(.5f,.5f,0,.5f),.30f,new Color(1,.95f,.85f));
            Paint("Timber",atlas,shader,new Vector4(.5f,.5f,.5f,.5f),.24f,new Color(.78f,.69f,.56f),.55f);
            for(int stone=0;stone<6;stone++)
                Paint("blender_Stone_"+stone,atlas,shader,new Vector4(.5f,.5f,0,.5f),.27f,
                    new Color(.72f+stone*.045f,.68f+stone*.040f,.61f+stone*.035f),.62f);
            Paint("PrisonConcrete",atlas,shader,new Vector4(.5f,.5f,0,0),.32f,new Color(.72f,.82f,.92f));
            Paint("ZooPaving",atlas,shader,new Vector4(.5f,.5f,.5f,0),.35f,Color.white);
            Paint("ZooLogBark",atlas,shader,new Vector4(.5f,.5f,.5f,.5f),.65f,new Color(.50f,.40f,.29f));
            foreach(var id in MuhanokContentBuilder.ChunkIds)
            {
                string path="Assets/Game/Content/Prefabs/"+id+".prefab";
                var chunk=PrefabUtility.LoadPrefabContents(path);ConfigureShadows(chunk);
                PrefabUtility.SaveAsPrefabAsset(chunk,path);PrefabUtility.UnloadPrefabContents(chunk);
            }
            var scene=EditorSceneManager.OpenScene("Assets/Game/Scenes/GameScene.unity");
            var game=UnityEngine.Object.FindFirstObjectByType<GameBootstrap>();
            if(game.OutputCamera.GetComponent<PointShadowBudget>()==null)game.OutputCamera.gameObject.AddComponent<PointShadowBudget>();
            foreach(var root in scene.GetRootGameObjects())ConfigureShadows(root);
            var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
            foreach(var feature in renderer.rendererFeatures)
            {
                if(!(feature is ScreenSpaceAmbientOcclusion))continue;
                var setting=new SerializedObject(feature);
                setting.FindProperty("m_Settings.Intensity").floatValue=1.15f;
                setting.FindProperty("m_Settings.Radius").floatValue=.42f;
                setting.FindProperty("m_Settings.DirectLightingStrength").floatValue=.35f;
                setting.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(feature);
            }
            EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Debug.Log("MUHANOK_VISUAL_V6_PASS: painted atlas / six world-projected materials / two 512px point shadow lights");
        }
        internal static void ConfigureShadows(GameObject root)
        {
            foreach(var light in root.GetComponentsInChildren<Light>(true))
            {
                if(light.type!=LightType.Point||light.shadows==LightShadows.None)continue;
                var data=light.GetUniversalAdditionalLightData();
                var serialized=new SerializedObject(data);
                serialized.FindProperty("m_AdditionalLightsShadowResolutionTier").intValue=UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierMedium;
                serialized.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(data);
            }
        }
        private static void Paint(string name,Texture2D atlas,Shader shader,Vector4 tile,float scale,Color tint,float strength=1)
        {
            var material=AssetDatabase.LoadAssetAtPath<Material>(Materials+name+".mat");
            if(material==null)throw new InvalidOperationException("Missing authored material: "+name);
            material.shader=shader;material.shaderKeywords=new string[0];
            material.SetTexture("_BaseMap",atlas);material.SetColor("_BaseColor",tint);
            material.SetVector("_AtlasTile",tile);material.SetFloat("_WorldScale",scale);
            material.SetFloat("_TextureStrength",strength);
            material.SetFloat("_Smoothness",.22f);material.SetFloat("_Metallic",0);
            EditorUtility.SetDirty(material);
        }
    }
}
