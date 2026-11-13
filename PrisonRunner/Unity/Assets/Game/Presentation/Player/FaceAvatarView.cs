using UnityEngine;
namespace Muhanok.Presentation
{
    // A photo badge follows the actual head bone. Its material is owned and cleaned up by this view.
    public sealed class FaceAvatarView : MonoBehaviour
    {
        private Transform head,badge; private Material material; private Renderer badgeRenderer;
        private Vector3 headOffset;private Quaternion headFacing;
        public void Apply(Texture portrait)
        {
            if(portrait==null){if(badge!=null)badge.gameObject.SetActive(false);return;}
            if(badge==null)
            {
                var animator=GetComponentInChildren<Animator>(true);
                head=animator!=null&&animator.isHuman?animator.GetBoneTransform(HumanBodyBones.Head):null;
                if(head!=null)
                {
                    headOffset=head.InverseTransformVector(transform.up*.28f+transform.forward*.52f);
                    headFacing=Quaternion.Inverse(head.rotation)*transform.rotation*Quaternion.Euler(0,180,0);
                }
                badge=GameObject.CreatePrimitive(PrimitiveType.Quad).transform;badge.name="PlayerFaceBadge";badge.SetParent(transform,false);
                Destroy(badge.GetComponent<Collider>()); badge.localScale=new Vector3(.55f,.62f,1);
                var shader=Resources.Load<Shader>("MuhanokUI/FacePhoto")??Shader.Find("Universal Render Pipeline/Unlit");
                material=new Material(shader);if(material.HasProperty("_Cull"))material.SetFloat("_Cull",0);
                badgeRenderer=badge.GetComponent<Renderer>();badgeRenderer.sharedMaterial=material;
            }
            material.mainTexture=portrait;badge.gameObject.SetActive(true);
        }
        private void LateUpdate()
        {
            if(badge==null||!badge.gameObject.activeSelf)return;
            badgeRenderer.enabled=head==null||head.gameObject.activeInHierarchy;
            // The exported head bone starts at the neck; its muzzle protrudes 0.49m in front of that bone.
            badge.position=head!=null?head.TransformPoint(headOffset):transform.position+Vector3.up*1.75f+transform.forward*.52f;
            badge.rotation=head!=null?head.rotation*headFacing:transform.rotation*Quaternion.Euler(0,180,0);
        }
        private void OnDestroy(){if(material!=null)Destroy(material);}
    }
}
