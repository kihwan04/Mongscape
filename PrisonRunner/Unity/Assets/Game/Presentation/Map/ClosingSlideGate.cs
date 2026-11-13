using UnityEngine;

namespace Muhanok.Presentation.Map
{
    // Leave a crouch-height opening at the crossing, then seal the route behind the player.
    public sealed class ClosingSlideGate : MonoBehaviour
    {
        public Transform Panel;
        public float PanelHeight=3.3f,OpenBottom=4f,SlideBottom=.95f;
        public float Bottom {get;private set;}
        public void Preview(float runnerZ)
        {
            float distance=transform.position.z-runnerZ;
            float closing=Mathf.SmoothStep(0,1,Mathf.InverseLerp(14,3,distance));
            Bottom=distance>=0?Mathf.Lerp(OpenBottom,SlideBottom,closing):Mathf.Lerp(SlideBottom,0,Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,3,-distance)));
            if(Panel!=null)Panel.localPosition=new Vector3(0,Bottom+PanelHeight*.5f,0);
        }
        public void ResetGate(){Bottom=OpenBottom;if(Panel!=null)Panel.localPosition=new Vector3(0,OpenBottom+PanelHeight*.5f,0);}
    }
}
