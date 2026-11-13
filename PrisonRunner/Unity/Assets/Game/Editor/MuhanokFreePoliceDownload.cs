using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Muhanok.Editor
{
    // Uses the installed Package Manager's normal download service and its terms check.
    // No browser cookies, credentials, access tokens or download keys are read.
    public static class MuhanokFreePoliceDownload
    {
        private const long ProductId=107256;
        private static object cache;
        private static MethodInfo localInfo;
        private static double started;
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.FlattenHierarchy;

        public static void OpenInEditor()
        {
            Download();
            UnityEditor.PackageManager.UI.Window.Open("107256");
        }

        [MenuItem("Mongscape/Download Acquired Free Police")]
        public static void Download()
        {
            try
            {
                var containerType=AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a=>a.GetType("UnityEditor.PackageManager.UI.Internal.ServicesContainer"))
                    .First(t=>t!=null);
                var assembly=containerType.Assembly;
                var instanceProperty=containerType.GetProperty("instance",Flags);
                if(instanceProperty==null)throw new InvalidOperationException("ServicesContainer.instance property unavailable");
                var container=instanceProperty.GetValue(null);
                var resolve=containerType.GetMethods(Flags).First(m=>m.Name=="Resolve"&&m.IsGenericMethodDefinition&&m.GetParameters().Length==0);
                object Service(string name)=>resolve.MakeGenericMethod(assembly.GetType("UnityEditor.PackageManager.UI.Internal."+name,true)).Invoke(container,null);
                var account=Service("IUnityConnectProxy");
                var loggedIn=account.GetType().GetProperty("isUserLoggedIn",Flags)?.GetValue(account);
                Debug.Log("MUHANOK_EDITOR_ASSET_ACCOUNT: signedIn="+loggedIn+" / batch="+UnityEngine.Application.isBatchMode);
                cache=Service("IAssetStoreCache");
                if(cache==null)throw new InvalidOperationException("Package Manager cache service unavailable");
                localInfo=cache.GetType().GetMethod("GetLocalInfo",Flags,null,new[]{typeof(long)},null);
                if(localInfo==null)throw new InvalidOperationException("Package Manager local info accessor unavailable");
                var manager=Service("IAssetStoreDownloadManager");
                manager.GetType().GetMethods(Flags).First(m=>m.Name=="Download"&&m.IsPublic&&m.GetParameters().Length==1)
                    .Invoke(manager,new object[]{new[]{ProductId}});
                started=EditorApplication.timeSinceStartup;
                EditorApplication.update-=Check;EditorApplication.update+=Check;
                Debug.Log("MUHANOK_FREE_POLICE_DOWNLOAD_REQUESTED: 107256 / standard Package Manager service");
            }
            catch(Exception ex)
            {
                Debug.LogError("MUHANOK_FREE_POLICE_SERVICE_UNAVAILABLE: "+ex.GetType().Name+(ex is InvalidOperationException?" / "+ex.Message:""));
                if(UnityEngine.Application.isBatchMode)EditorApplication.Exit(1);
            }
        }
        private static void Check()
        {
            var info=localInfo.Invoke(cache,new object[]{ProductId});
            string path=info==null?null:(info.GetType().GetProperty("packagePath",Flags)?.GetValue(info)??info.GetType().GetField("packagePath",Flags)?.GetValue(info)) as string;
            if(!string.IsNullOrEmpty(path)&&File.Exists(path))
            {
                string destination=Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,"../../References/BitGemPolice/PoliceOfficer107256.unitypackage"));
                Directory.CreateDirectory(Path.GetDirectoryName(destination));File.Copy(path,destination,true);
                EditorApplication.update-=Check;
                Debug.Log("MUHANOK_FREE_POLICE_DOWNLOADED: 107256 / package copied to project References");
                if(UnityEngine.Application.isBatchMode)EditorApplication.Exit(0);
            }
            else if(EditorApplication.timeSinceStartup-started>75)
            {
                EditorApplication.update-=Check;
                Debug.LogError("MUHANOK_FREE_POLICE_DOWNLOAD_PENDING: Editor account or Package Manager needs attention.");
                if(UnityEngine.Application.isBatchMode)EditorApplication.Exit(1);
            }
        }
    }
}
