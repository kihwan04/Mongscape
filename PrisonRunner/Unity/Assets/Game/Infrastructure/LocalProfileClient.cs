using System;
using System.Collections;
using System.IO;
using System.Text;
using Muhanok.Application;
using Muhanok.Domain;
using UnityEngine;
using UnityEngine.Networking;
namespace Muhanok.Infrastructure
{
    // Account server client: face capture plus the account API. Who is signed in lives in AccountSession, not here.
    public sealed class LocalProfileClient : MonoBehaviour, IAccountGateway
    {
        private const string ConnectionFailed="얼굴 서버 연결 실패. 서버 실행 상태와 주소 설정을 확인하세요.";
        private string endpoint;
        private string configurationError;
        private void Awake()
        {
            try { endpoint=IdentityServerConfig.Load(); }
            catch(Exception) { configurationError="얼굴 서버 주소 설정을 확인하세요. 원격 서버는 HTTPS 주소가 필요합니다.";Message=configurationError; }
        }
        public static class IdentityServerConfig
        {
            [Serializable] private sealed class Settings { public string url; }
            public static string Load()
            {
                string configured=Environment.GetEnvironmentVariable("MONGSCAPE_IDENTITY_URL");
                if(string.IsNullOrWhiteSpace(configured))
                {
                    string path=Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,"../identity-server.json"));
                    if(File.Exists(path))
                    {
                        var settings=JsonUtility.FromJson<Settings>(File.ReadAllText(path));
                        if(settings==null||string.IsNullOrWhiteSpace(settings.url))throw new FormatException("Missing identity URL");
                        configured=settings.url;
                    }
                }
                return Normalize(string.IsNullOrWhiteSpace(configured)?"http://127.0.0.1:8765":configured);
            }
            public static string Normalize(string value)
            {
                if(!Uri.TryCreate(value?.Trim(),UriKind.Absolute,out var uri)||string.IsNullOrEmpty(uri.Host)
                    ||!string.IsNullOrEmpty(uri.UserInfo)||!string.IsNullOrEmpty(uri.Query)||!string.IsNullOrEmpty(uri.Fragment)
                    ||uri.AbsolutePath!="/"||(uri.Scheme!="https"&&!(uri.Scheme=="http"&&uri.IsLoopback)))
                    throw new FormatException("Use an HTTPS origin, or loopback HTTP for local Docker");
                return uri.GetLeftPart(UriPartial.Authority);
            }
        }
        [Serializable] private sealed class FaceRequest { public string[] images; }
        [Serializable] private sealed class NameRequest { public string name; }
        [Serializable] private sealed class ItemRequest { public int item; }
        [Serializable] private sealed class BoardRequest { public int mode; public bool today; }
        private bool capturing; private int faceCalls;
        // Capturing faces, or a login/enroll/delete call is waiting for the server.
        public bool Busy=>capturing||faceCalls>0;
        public string Message {get;private set;}="얼굴 등록 후 다음 방문부터 얼굴로 로그인하세요.";
        // Takes three corrected webcam frames as base64 JPGs for /login or /enroll. Nothing is called back when the camera is not ready.
        public void CaptureFaces(Texture camera,Action<string[]> captured)
        {
            if(Busy)return;
            if(configurationError!=null){Message=configurationError;return;}
            if(!(camera is WebCamTexture webcam)||!webcam.isPlaying||webcam.width<64){Message="카메라가 준비된 뒤 다시 시도하세요.";return;}
            StartCoroutine(Capture(webcam,captured));
        }
        private IEnumerator Capture(WebCamTexture camera,Action<string[]> captured)
        {
            capturing=true; Message="정면을 보고 잠시 가만히 계세요. 얼굴 3장 확인 중…";
            var images=new string[3];
            for(int i=0;i<3;i++)
            {
                yield return new WaitForSecondsRealtime(.4f);
                if(camera==null||!camera.isPlaying||camera.width<64){capturing=false;Message="카메라가 중단되었습니다.";yield break;}
                var pixels=camera.GetPixels32(); int w=camera.width,h=camera.height;
                // WebCamTexture pixels start at the lower left. Correct the driver's rotation and vertical mirror before encoding.
                int rotation=camera.videoRotationAngle; bool swapped=rotation==90||rotation==270;
                int ow=swapped?h:w,oh=swapped?w:h;
                var corrected=new Color32[pixels.Length];
                for(int y=0;y<h;y++)for(int x=0;x<w;x++)
                {
                    int cy=camera.videoVerticallyMirrored?h-1-y:y,dx=x,dy=cy;
                    if(rotation==90){dx=cy;dy=w-1-x;}else if(rotation==180){dx=w-1-x;dy=h-1-cy;}else if(rotation==270){dx=h-1-cy;dy=x;}
                    corrected[dy*ow+dx]=pixels[y*w+x];
                }
                var texture=new Texture2D(ow,oh,TextureFormat.RGB24,false);texture.SetPixels32(corrected);texture.Apply();
                images[i]=Convert.ToBase64String(texture.EncodeToJPG(85));Destroy(texture);
            }
            capturing=false;
            captured?.Invoke(images);
        }

        // IAccountGateway
        public void Health(Action<AccountResult> done)=>StartCoroutine(Ping(done));
        private IEnumerator Ping(Action<AccountResult> done)
        {
            if(configurationError!=null){done?.Invoke(new AccountResult{message=configurationError});yield break;}
            using(var request=UnityWebRequest.Get(endpoint+"/health"))
            {
                request.timeout=5;yield return request.SendWebRequest();
                AccountResult reply=null;
                try { reply=JsonUtility.FromJson<AccountResult>(request.downloadHandler.text); } catch { }
                bool ok=request.result==UnityWebRequest.Result.Success&&reply!=null&&reply.ok;
                // The server's own health text is English; players see the Korean connection hint.
                var result=new AccountResult{ok=ok,message=ok?"":ConnectionFailed};
                result.status=request.result==UnityWebRequest.Result.ConnectionError?0:request.responseCode;
                done?.Invoke(result);
            }
        }
        public void NameCheck(string name,Action<AccountReply> done)=>Post("/name-check",JsonUtility.ToJson(new NameRequest{name=name}),"",done);
        public void Enroll(AccountEnrollment enrollment,Action<AccountReply> done)=>Face("/enroll",JsonUtility.ToJson(enrollment),done);
        public void Login(string[] images,Action<AccountReply> done)=>Face("/login",JsonUtility.ToJson(new FaceRequest{images=images}),done);
        public void FetchProfile(string token,Action<AccountReply> done)=>Post("/profile","{}",token,done);
        public void UpdateProfile(string token,AccountProfileUpdate update,Action<AccountReply> done)=>Post("/profile/update",update!=null?update.ToJson():"{}",token,done);
        public void SaveRun(string token,AccountRun run,Action<AccountReply> done)=>Post("/runs",JsonUtility.ToJson(run),token,reply=>
        {
            if(!reply.ok)Debug.LogWarning("Mongscape: run save failed: "+reply.message);
            done?.Invoke(reply);
        });
        public void Buy(string token,ItemKind item,Action<AccountReply> done)=>Post("/shop/buy",JsonUtility.ToJson(new ItemRequest{item=(int)item}),token,done);
        public void Leaderboard(string token,PlayStyle mode,bool today,Action<LeaderboardReply> done)=>
            StartCoroutine(Send("/leaderboard",JsonUtility.ToJson(new BoardRequest{mode=(int)mode,today=today}),token,done));
        public void Stats(string token,Action<StatsReply> done)=>StartCoroutine(Send("/stats","{}",token,done));
        // Sent and forgotten when the game quits: SendWebRequest starts before the first yield.
        public void Logout(string token,Action<AccountReply> done)
        {
            Message="로그아웃했습니다.";
            Post("/logout","{}",token,reply=>{Message="로그아웃했습니다.";done?.Invoke(reply);});
        }
        public void Delete(string token,Action<AccountReply> done)
        {
            faceCalls++;
            Post("/delete","{}",token,reply=>{faceCalls--;if(reply.ok)Message="얼굴과 프로필·기록을 삭제했습니다.";done?.Invoke(reply);});
        }
        private void Face(string path,string json,Action<AccountReply> done)
        {
            faceCalls++;
            Post(path,json,"",reply=>{faceCalls--;if(reply.ok)Message=(reply.profile!=null&&!string.IsNullOrEmpty(reply.profile.name)?reply.profile.name:reply.name)+"님 로그인 완료";done?.Invoke(reply);});
        }
        private void Post(string path,string json,string token,Action<AccountReply> done)=>StartCoroutine(Send(path,json,token,done));
        private IEnumerator Send<T>(string path,string json,string token,Action<T> done) where T:AccountResult,new()
        {
            if(configurationError!=null){Message=configurationError;done?.Invoke(new T{message=configurationError});yield break;}
            using(var request=new UnityWebRequest(endpoint+path,"POST"))
            {
                request.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));request.downloadHandler=new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type","application/json"); if(!string.IsNullOrEmpty(token))request.SetRequestHeader("Authorization","Bearer "+token);
                request.timeout=30;yield return request.SendWebRequest();
                T reply=null;
                try { reply=JsonUtility.FromJson<T>(request.downloadHandler.text); } catch { }
                if(reply==null)reply=new T{message=ConnectionFailed};
                reply.status=request.result==UnityWebRequest.Result.ConnectionError?0:request.responseCode;
                if(!reply.ok) Message=string.IsNullOrEmpty(reply.message)?ConnectionFailed:reply.message;
                done?.Invoke(reply);
            }
        }
    }
}
