using System.Collections;
using UnityEngine;

namespace LQ {
    /// <summary>air_bubbles: cosmetic rising bubbles (s_bubble.spr) in underwater sections, only while the player is near.</summary>
    public class AirBubbles : MonoBehaviour {
        QEntity ent;
        public static void Attach(GameObject go, QEntity e) { go.AddComponent<AirBubbles>().ent = e; }
        void Start() { StartCoroutine(Loop()); }
        IEnumerator Loop() {
            while (true) {
                yield return new WaitForSeconds(Random.Range(0.5f, 1.5f));
                if (Player.Instance == null || (Player.Instance.transform.position - ent.Origin).sqrMagnitude > 60f * 60f) continue;
                var s = SpriteAnimator.Spawn("progs/s_bubble.spr", ent.Origin + Random.insideUnitSphere * 0.2f, true, 8f, 0.5f);
                if (s != null) { var r = s.gameObject.AddComponent<RiseAndDie>(); r.speed = 0.5f + Random.value * 0.4f; r.life = 3f; }
            }
        }
    }
}
