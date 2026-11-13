using UnityEngine;
namespace Muhanok.Presentation.Map
{
    public sealed class CellPrisoner : MonoBehaviour
    {
        public int CellNumber;
        public int Side;
        private void OnEnable()
        {
            var animator=GetComponent<Animator>();
            if(animator!=null)animator.Play("AN_Monkey_CellIdle",0,(CellNumber%7)/7f);
        }
    }
}
