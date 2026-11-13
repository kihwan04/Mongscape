using UnityEngine;

namespace Muhanok.Presentation
{
    public static class ViewportLayout
    {
        public static Rect Fit(float width,float height,Rect safe)
        {
            safe=Rect.MinMaxRect(Mathf.Clamp(safe.xMin,0,width),Mathf.Clamp(safe.yMin,0,height),Mathf.Clamp(safe.xMax,0,width),Mathf.Clamp(safe.yMax,0,height));
            float scale=Mathf.Min(safe.width/1280f,safe.height/720f);
            float fittedWidth=Mathf.Min(safe.width,1280*scale), fittedHeight=Mathf.Min(safe.height,720*scale);
            return new Rect(safe.x+(safe.width-fittedWidth)*.5f,safe.y+(safe.height-fittedHeight)*.5f,fittedWidth,fittedHeight);
        }
        public static Rect Current=>Fit(Screen.width,Screen.height,Screen.safeArea);
        public static Matrix4x4 GuiMatrix
        {
            get { var r=Current; return Matrix4x4.TRS(new Vector3(r.x,Screen.height-r.yMax,0),Quaternion.identity,Vector3.one*(r.width/1280f)); }
        }
    }
    [RequireComponent(typeof(Camera))]
    public sealed class GameViewport : MonoBehaviour
    {
        private Camera output;
        private void Awake() { output=GetComponent<Camera>(); }
        private void LateUpdate()
        {
            var r=ViewportLayout.Current;
            output.rect=new Rect(r.x/Screen.width,r.y/Screen.height,r.width/Screen.width,r.height/Screen.height);
            output.aspect=16f/9f;
        }
        private void OnGUI()
        {
            var r=ViewportLayout.Current; float top=Screen.height-r.yMax;
            var color=GUI.color; var matrix=GUI.matrix; GUI.matrix=Matrix4x4.identity; GUI.color=Color.black;
            GUI.DrawTexture(new Rect(0,0,Screen.width,top),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0,top+r.height,Screen.width,Screen.height-top-r.height),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0,top,r.x,r.height),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax,top,Screen.width-r.xMax,r.height),Texture2D.whiteTexture);
            GUI.color=color; GUI.matrix=matrix;
        }
    }
}
