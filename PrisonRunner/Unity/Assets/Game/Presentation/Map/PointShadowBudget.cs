using System.Collections.Generic;
using UnityEngine;

namespace Muhanok.Presentation
{
    // A point light needs six atlas faces. Two 512px lights fit the existing 2048px atlas.
    // Keep authored candidate lights but render only the two closest active candidates.
    public sealed class PointShadowBudget : MonoBehaviour
    {
        private readonly List<Light> candidates=new List<Light>();
        private void Start()
        {
            foreach(var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                if(light.type!=LightType.Point||light.shadows==LightShadows.None)continue;
                candidates.Add(light);
            }
        }
        private void LateUpdate()
        {
            Light first=null,second=null;float firstDistance=float.MaxValue,secondDistance=float.MaxValue;
            foreach(var light in candidates)
            {
                if(light==null||!light.isActiveAndEnabled)continue;
                float distance=(light.transform.position-transform.position).sqrMagnitude;
                if(distance>35*35)continue;
                if(distance<firstDistance){second=first;secondDistance=firstDistance;first=light;firstDistance=distance;}
                else if(distance<secondDistance){second=light;secondDistance=distance;}
            }
            foreach(var light in candidates)
                if(light!=null)light.shadows=light==first||light==second?LightShadows.Soft:LightShadows.None;
        }
    }
}
