using UnityEngine;

namespace Muhanok.Presentation
{
    // A short, non-graphic cartoon capture. Uses real elapsed time while the runner is frozen.
    public sealed class PoliceCaptureSequence : MonoBehaviour
    {
        public const float Duration = 3.6f;
        private RunnerView hero, guard;
        private Transform baton, stars;
        private Vector3 origin, approach, target;
        private float age;
        private bool active;
        private ProneCapturePose prone;
        public float ProneBlend=>prone!=null?prone.Blend:0;
        public bool BatonVisible=>baton!=null&&baton.gameObject.activeSelf;
        public int Strikes { get; private set; }
        public bool Complete => active && age >= Duration;
        public float Age => age;
        public void Configure(RunnerView player, RunnerView police)
        {
            hero=player;guard=police;
            prone=hero.gameObject.AddComponent<ProneCapturePose>();prone.Configure(hero);
            baton=new GameObject("CartoonBaton").transform;baton.SetParent(guard.ToolAnchor,false);
            Part(baton,new Vector3(0,.25f,0),new Vector3(.095f,.4f,.095f),new Color(.055f,.07f,.09f));
            Part(baton,new Vector3(0,-.08f,0),new Vector3(.13f,.07f,.13f),new Color(.28f,.23f,.17f));
            stars=new GameObject("CaptureStars").transform;stars.SetParent(transform,false);
            for(int i=0;i<5;i++)
            {
                var star=Part(stars,Vector3.zero,new Vector3(.17f,.025f,.17f),new Color(1,.78f,.07f));
                star.localRotation=Quaternion.Euler(45,45,0);
            }
            ResetSequence();
        }
        private static Transform Part(Transform parent,Vector3 position,Vector3 scale,Color color)
        {
            var node=GameObject.CreatePrimitive(PrimitiveType.Cylinder);Destroy(node.GetComponent<Collider>());
            node.transform.SetParent(parent,false);node.transform.localPosition=position;node.transform.localScale=scale;
            var material=new Material(Resources.Load<Shader>("MuhanokUI/ForkSurface")){color=color};node.GetComponent<Renderer>().sharedMaterial=material;
            return node.transform;
        }
        public void Begin()
        {
            active=true;age=0;Strikes=0;origin=hero.transform.position-hero.transform.forward*2.2f;
            Muhanok.Domain.RouteSurface.Sample(origin.z,out _,out float floor);origin.y=floor;
            target=origin+new Vector3(.85f,0,-.65f);
            approach=guard.transform.position;
            if(Vector3.Distance(approach,origin)>10)approach=origin+new Vector3(.85f,0,-8);
            if(approach.z>origin.z-.9f)approach=origin+new Vector3(.85f,0,-1.4f);
            guard.PoseWorld(approach,true);guard.transform.rotation=Quaternion.identity;
            guard.Play("AN_Guard_ChaseRun");hero.PoseWorld(origin,true);hero.Play("AN_Monkey_Caught");
            prone.Begin();
            baton.gameObject.SetActive(true);stars.gameObject.SetActive(false);
        }
        public void Tick(float dt)
        {
            if(!active)return;
            age+=Mathf.Max(0,dt);
            float arrive=Mathf.SmoothStep(0,1,Mathf.Clamp01(age/.85f));
            guard.PoseWorld(Vector3.Lerp(approach,target,arrive),true);
            if(age>=.85f)
            {
                guard.transform.rotation=Quaternion.LookRotation(origin-guard.transform.position);
                guard.Play("AN_Guard_Catch");
            }
            float cycle=(age-1.05f)/.65f;
            int strike=age<1.25f?0:Mathf.Clamp(1+Mathf.FloorToInt((age-1.25f)/.65f),0,3);
            if(strike>Strikes){Strikes=strike;GetComponent<GameAudio>()?.Bonk();}
            float swing=cycle>=0&&cycle<3?Mathf.Sin(Mathf.Repeat(cycle,1)*Mathf.PI):0;
            baton.localRotation=Quaternion.Euler(15+130*swing,0,-25);
            hero.PoseWorld(origin+Vector3.up*(Mathf.Abs(Mathf.Sin((age-1.25f)*Mathf.PI/.65f))*.08f*(Strikes>0?1:0)),true);
            prone.Apply(Mathf.InverseLerp(.35f,1.05f,age),origin.y,swing);
            stars.gameObject.SetActive(Strikes>0);
            var head=hero.RigAnimator!=null&&hero.Humanoid?hero.RigAnimator.GetBoneTransform(HumanBodyBones.Head):null;
            stars.position=head!=null?head.position+Vector3.up*.4f:origin+Vector3.up;
            for(int i=0;i<stars.childCount;i++)
            {
                float angle=age*4+i*Mathf.PI*2/stars.childCount;
                stars.GetChild(i).localPosition=new Vector3(Mathf.Cos(angle)*.5f,Mathf.Sin(angle*2)*.09f,Mathf.Sin(angle)*.5f);
            }
        }
        public void ResetSequence()
        {
            active=false;age=0;Strikes=0;
            prone?.ResetPose();
            if(baton!=null)baton.gameObject.SetActive(false);if(stars!=null)stars.gameObject.SetActive(false);
        }
        public void ShowPursuitBaton(bool visible)
        {
            if(active)return;
            baton.gameObject.SetActive(visible);baton.localRotation=Quaternion.Euler(15,0,-25);
        }
        private void OnDestroy()
        {
            if(baton!=null)foreach(var renderer in baton.GetComponentsInChildren<Renderer>())Destroy(renderer.sharedMaterial);
            if(stars!=null)foreach(var renderer in stars.GetComponentsInChildren<Renderer>())Destroy(renderer.sharedMaterial);
        }
    }
}
