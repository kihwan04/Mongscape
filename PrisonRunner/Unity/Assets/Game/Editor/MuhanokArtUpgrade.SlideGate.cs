using Muhanok.Domain;
using Muhanok.Presentation.Map;
using UnityEngine;

namespace Muhanok.Editor
{
    public static partial class MuhanokArtUpgrade
    {
        // The prison stair has no closing door any more and carries no entrance decor: it is reused inside the corridor.
        private static void AddClosingPrisonEntry(GameObject root,int type)
        {
            var chunk=root.GetComponent<MuhanokMapChunk>();
            foreach(var old in root.GetComponentsInChildren<ClosingSlideGate>(true))Object.DestroyImmediate(old);
            chunk.SlideGates=new ClosingSlideGate[0];
        }
    }
}
