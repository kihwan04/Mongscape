using UnityEngine;
namespace Muhanok.Presentation
{
    public sealed class FacialPerformance : MonoBehaviour
    {
        private Animator animator;
        private SkinnedMeshRenderer face;
        private int blink=-1,jaw=-1;
        private float clock,openness;
        private static int FindShape(Mesh mesh,string name)
        {
            for(int i=0;i<mesh.blendShapeCount;i++)
                if(mesh.GetBlendShapeName(i)==name||mesh.GetBlendShapeName(i).EndsWith("."+name))return i;
            return -1;
        }
        private void Awake()
        {
            animator=GetComponent<Animator>();
            foreach(var renderer in GetComponentsInChildren<SkinnedMeshRenderer>())
                if(FindShape(renderer.sharedMesh,"Blink")>=0)
                {face=renderer;blink=FindShape(face.sharedMesh,"Blink");jaw=FindShape(face.sharedMesh,"JawOpen");break;}
        }
        private void LateUpdate()
        {
            if(face==null||animator==null)return;
            clock+=Time.deltaTime;float phase=clock%3.4f;
            float closed=phase<.16f?Mathf.Sin(phase/.16f*Mathf.PI):0;
            face.SetBlendShapeWeight(blink,closed*100);
            var state=animator.GetCurrentAnimatorStateInfo(0);
            float target=state.IsName("AN_Monkey_IdeaReact")?90:
                state.IsName("AN_Monkey_Run")||state.IsName("AN_Monkey_SprintStart")?55:
                state.IsName("AN_Monkey_Stumble")||state.IsName("AN_Monkey_Caught")?100:8;
            openness=Mathf.Lerp(openness,target,1-Mathf.Exp(-10*Time.deltaTime));
            if(jaw>=0)face.SetBlendShapeWeight(jaw,openness);
        }
    }
}
