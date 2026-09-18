using System.Collections.Generic;
using UnityEngine;

namespace LQ {
    public class PlatField : MonoBehaviour {
        public FuncPlat plat;
        void OnTriggerEnter(Collider other) { plat?.Touched(other.gameObject); }
        void OnTriggerStay(Collider other) { plat?.Touched(other.gameObject); }
    }
}
