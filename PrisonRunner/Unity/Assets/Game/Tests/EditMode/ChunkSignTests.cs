using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
namespace Muhanok.Tests
{
    // Chunks carry no text signs; the only TextMesh left in the content is the number on the mine cart.
    public sealed class ChunkSignTests
    {
        private static readonly string[] ChunkIds =
        {
            "CH_Mine_Start","CH_Mine_Straight_A","CH_Mine_Straight_B","CH_Transition_MineToPrison","CH_Prison_Corridor_A","CH_Prison_Corridor_B",
            "CH_Checkpoint","CH_Stair","CH_Laundry","CH_Kitchen","CH_Maintenance","CH_Yard","CH_OuterWall","CH_Mine_Stair","CH_Zoo_Stair","CH_Prison_Exit"
        };
        [Test] public void NoChunkPrefabContainsATextMesh()
        {
            var found = new List<string>();
            foreach (string id in ChunkIds)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Content/Prefabs/" + id + ".prefab");
                Assert.IsNotNull(prefab, id);
                foreach (var text in prefab.GetComponentsInChildren<TextMesh>(true)) found.Add(id + "/" + text.name);
            }
            Assert.IsEmpty(found, "Chunk signs: " + string.Join(", ", found));
        }
        [Test] public void PrisonExitChunkIsRegisteredLast()
        {
            Assert.AreEqual(15, Muhanok.Domain.RouteSurface.PrisonExit);
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Content/Prefabs/CH_Prison_Exit.prefab"));
        }
    }
}
