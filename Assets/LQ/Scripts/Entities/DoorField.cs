using System.Collections.Generic;
using UnityEngine;

namespace LQ {
    public class DoorField : MonoBehaviour {
        public FuncDoor door;
        void OnTriggerEnter(Collider other) { door?.Touched(other.gameObject); }
        void OnTriggerStay(Collider other) { door?.Touched(other.gameObject); }
    }
}
