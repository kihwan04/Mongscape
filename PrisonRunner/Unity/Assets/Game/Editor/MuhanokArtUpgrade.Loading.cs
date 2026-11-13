using Muhanok.Bootstrap;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Muhanok.Editor
{
    public static partial class MuhanokArtUpgrade
    {
        private static Texture2D ConfigureTitleLogoTexture()
        {
            const string path = "Assets/Game/Content/ArtV2/Textures/MuhanokLogo.png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new System.InvalidOperationException("Title logo is missing.");
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Texture2D ConfigureLoadingTexture()
        {
            const string path="Assets/External/Staging/ReferenceUI/LoadingReference.png";
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(importer==null) throw new System.InvalidOperationException("Loading reference image is missing.");
            importer.npotScale=TextureImporterNPOTScale.None;
            importer.mipmapEnabled=false;
            importer.maxTextureSize=2048;
            importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.wrapMode=TextureWrapMode.Clamp;
            importer.filterMode=FilterMode.Bilinear;
            importer.SaveAndReimport();
            var image=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if(image.width!=1672||image.height!=941) throw new System.InvalidOperationException("Loading image aspect must match its source.");
            return image;
        }

        [MenuItem("Mongscape/Refresh Loading Reference")]
        public static void RefreshLoadingReference()
        {
            var scene=EditorSceneManager.OpenScene("Assets/Game/Scenes/GameScene.unity");
            var game=Object.FindFirstObjectByType<GameBootstrap>();
            game.LoadingImage=ConfigureLoadingTexture();
            EditorUtility.SetDirty(game); EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("MUHANOK_LOADING_REFERENCE_PASS: 1672x941 / original aspect / uncompressed / one progress bar");
        }
    }
}
