using System.Collections.Generic;
using UnityEngine;

namespace LQ {
    public class TriggerRelay : MonoBehaviour, IActivatable {
        public void Activate(GameObject activator) {
            var ent = GetComponent<QEntity>();
            var msg = ent.Get("message"); if (!string.IsNullOrEmpty(msg)) HUD.CenterPrint(msg);
            ent.FireTargets(activator);
        }
    }
}
