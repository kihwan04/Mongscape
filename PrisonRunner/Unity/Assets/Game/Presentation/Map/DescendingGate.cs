using UnityEngine;

namespace Muhanok.Presentation.Map
{
    public sealed class DescendingGate : MonoBehaviour
    {
        public Transform Panel;
        public float ClosedCenterY=1.65f,OpenCenterY=5f;
        public float Closure {get; private set;}
        public void Preview(float runnerZ)
        {
            Closure=Mathf.SmoothStep(0,1,Mathf.InverseLerp(18,8,transform.position.z-runnerZ));
            if(Panel!=null) Panel.localPosition=new Vector3(0,Mathf.Lerp(OpenCenterY,ClosedCenterY,Closure),0);
        }
        public void ResetGate(){Closure=0;if(Panel!=null) Panel.localPosition=new Vector3(0,OpenCenterY,0);}
    }
}
