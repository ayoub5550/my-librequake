using System.Collections.Generic;
using UnityEngine;

namespace LQ {
    public class SecretField : MonoBehaviour {
        public FuncDoorSecret door;
        void OnTriggerEnter(Collider other) { door?.Touched(other.gameObject); }
    }
}
