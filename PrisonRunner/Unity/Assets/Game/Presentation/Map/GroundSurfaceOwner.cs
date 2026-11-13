using UnityEngine;
namespace Muhanok.Presentation.Map
{
    // Custom routes own their entire surface. Ordinary routes separate the base from decorative floor tiles.
    public sealed class GroundSurfaceOwner : MonoBehaviour
    {
        private Transform authored,floor;
        private Vector3 originalFloor;
        private bool originalActive,changed;
        public static void Prepare(MuhanokMapChunk chunk,bool custom)
        {
            var owner=chunk.GetComponent<GroundSurfaceOwner>();if(owner==null)owner=chunk.gameObject.AddComponent<GroundSurfaceOwner>();
            owner.Restore();owner.authored=chunk.transform.Find("RouteStructure");
            if(owner.authored==null)return;
            owner.originalActive=owner.authored.gameObject.activeSelf;
            foreach(var filter in owner.authored.GetComponentsInChildren<MeshFilter>(true))
                if(filter.name=="Editable route floor"){owner.floor=filter.transform;break;}
            if(owner.floor!=null)owner.originalFloor=owner.floor.localPosition;
            owner.changed=true;
            if(custom)owner.authored.gameObject.SetActive(false);
            else if(owner.floor!=null)owner.floor.localPosition=owner.originalFloor+Vector3.down*.025f;
        }
        public void Restore()
        {
            if(!changed)return;
            if(authored!=null)authored.gameObject.SetActive(originalActive);
            if(floor!=null)floor.localPosition=originalFloor;
            changed=false;
        }
    }
}
