using UnityEngine;

namespace LQ {
    public class ExploBox : MonoBehaviour, IDamageable {
        float health = 20; public bool IsAlive => health > 0;
        public static void Attach(GameObject go, QEntity ent) {
            var eb = go.AddComponent<ExploBox>();
            var pos = go.transform.position;
            if (Physics.Raycast(pos + Vector3.up * 0.1f, Vector3.down, out var hit, 50f, Combat.WorldMask, QueryTriggerInteraction.Ignore)) pos = hit.point;
            go.transform.position = pos;
            var prefab = Resources.Load<GameObject>("brushmodels/" + (ent.classname == "misc_explobox2" ? "b_exbox2" : "b_explob"));
            if (prefab != null) { var m = Instantiate(prefab, go.transform); m.transform.localPosition = Vector3.zero; }
            else { var c = GameObject.CreatePrimitive(PrimitiveType.Cube); c.transform.SetParent(go.transform, false); c.transform.localScale = new Vector3(1, 2, 1); c.transform.localPosition = new Vector3(0, 1, 0); }
            if (go.GetComponentInChildren<Collider>() == null) { var bc = go.AddComponent<BoxCollider>(); bc.center = new Vector3(0, 1f, 0); bc.size = new Vector3(1, 2, 1); }
        }
        public void TakeDamage(DamageInfo info) {
            if (!IsAlive) return;
            health -= info.amount;
            if (health > 0) return;
            var c = transform.position + Vector3.up * 1f;
            Combat.RadiusDamage(c, 160, 160 * QuakeUnits.Scale, info.attacker, gameObject, gameObject);
            Effects.Explosion(c); SoundBank.Play("weapons/r_exp3.wav", c, 1f, 60f);
            GetComponent<QEntity>()?.FireTargets(info.attacker);
            Destroy(gameObject);
        }
    }
}
