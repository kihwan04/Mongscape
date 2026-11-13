using UnityEngine;
using Muhanok.Domain;

namespace Muhanok.Presentation
{
    public sealed class WorldAtmosphere : MonoBehaviour
    {
        public Light Fill;
        public Transform Moon;
        private Camera output;
        private void Awake() { output=GetComponent<Camera>(); }
        private void LateUpdate()
        {
            int sequence = Mathf.Max(0, Mathf.FloorToInt(transform.position.z / 24f));
            int type = RouteSurface.TypeAt(sequence), next=RouteSurface.TypeAt(sequence+1);
            bool outdoor = RouteSurface.IsOutdoor(type);
            float blend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(16,24,transform.position.z-sequence*24));
            Color fog = Color.Lerp(Fog(type),Fog(next),blend);
            Color ambient=Color.Lerp(Ambient(type),Ambient(next),blend);
            float outdoorWeight=Mathf.Lerp(outdoor?1:0,RouteSurface.IsOutdoor(next)?1:0,blend);
            float cartWeight=Mathf.Lerp(type==1||type==2?1:0,next==1||next==2?1:0,blend);
            if(type==3&&(RouteSurface.ActivePlan?.Extended??false))
            {
                int previous=RouteSurface.TypeAt(Mathf.Max(0,sequence-1));
                float mix=Mathf.SmoothStep(0,1,Mathf.InverseLerp(6,18,transform.position.z-sequence*24));
                if(RouteSurface.IsOutdoor(next)){outdoorWeight=mix;fog=Color.Lerp(Fog(0),Fog(next),mix);ambient=Color.Lerp(Ambient(0),Ambient(next),mix);}
                else if(RouteSurface.IsOutdoor(previous)){outdoorWeight=1-mix;fog=Color.Lerp(Fog(previous),Fog(0),mix);ambient=Color.Lerp(Ambient(previous),Ambient(0),mix);}
            }
            float t = 1-Mathf.Exp(-Time.deltaTime*1.6f);
            bool extended=RouteSurface.ActivePlan!=null&&RouteSurface.ActivePlan.Extended;
            RenderSettings.fogMode=extended?FogMode.Linear:FogMode.Exponential;
            if(extended){RenderSettings.fogStartDistance=36;RenderSettings.fogEndDistance=68;}
            RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor,fog,t);
            RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity,Mathf.Lerp(Mathf.Lerp(.035f,.023f,cartWeight),.014f,outdoorWeight),t);
            RenderSettings.ambientLight = Color.Lerp(RenderSettings.ambientLight,ambient,t);
            if (Fill != null) { Fill.intensity = Mathf.Lerp(Fill.intensity,Mathf.Lerp(.55f,.50f,outdoorWeight),t); Fill.color = Color.Lerp(Fill.color,Color.Lerp(new Color(.72f,.79f,.92f),new Color(.42f,.57f,1),outdoorWeight),t); Fill.shadows=outdoor?LightShadows.Soft:LightShadows.None; }
            output.clearFlags=outdoorWeight>.05f?CameraClearFlags.Skybox:CameraClearFlags.SolidColor; output.backgroundColor=RenderSettings.fogColor;
            if(Moon!=null) Moon.gameObject.SetActive(outdoorWeight>.2f);
        }
        public static Color Fog(int type)=>RouteSurface.IsOutdoor(type)?new Color(.025f,.043f,.088f):type==1||type==2?new Color(.052f,.080f,.11f):RouteSurface.IsMine(type)?new Color(.11f,.07f,.045f):type==7?new Color(.09f,.035f,.026f):new Color(.055f,.085f,.12f);
        public static Color Ambient(int type)=>RouteSurface.IsOutdoor(type)?new Color(.20f,.25f,.38f):type==1||type==2?new Color(.23f,.29f,.38f):RouteSurface.IsMine(type)?new Color(.28f,.24f,.20f):new Color(.18f,.23f,.31f);
    }
}
