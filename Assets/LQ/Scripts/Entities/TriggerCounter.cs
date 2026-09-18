using System.Collections.Generic;
using UnityEngine;

namespace LQ {
    public class TriggerCounter : MonoBehaviour, IActivatable {
        int count;
        public void Activate(GameObject activator) {
            var ent = GetComponent<QEntity>();
            if (count == 0) count = Mathf.Max(1, ent.GetInt("count", 2));
            count--;
            if (count > 0) {
                if (!ent.HasFlag(1)) HUD.CenterPrint(count == 1 ? "Only 1 more to go..." : "Only " + count + " more to go...");
                return;
            }
            if (!ent.HasFlag(1)) HUD.CenterPrint("Sequence completed!");
            var msg = ent.Get("message"); if (!string.IsNullOrEmpty(msg)) HUD.CenterPrint(msg);
            SoundBank.Play2D("misc/talk.wav");
            ent.FireTargets(activator);
        }
    }
}
