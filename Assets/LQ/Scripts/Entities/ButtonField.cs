using System.Collections.Generic;
using UnityEngine;

namespace LQ {
    public class ButtonField : MonoBehaviour {
        public FuncButton button;
        void OnTriggerEnter(Collider other) { button?.Touched(other.gameObject); }
    }
}
