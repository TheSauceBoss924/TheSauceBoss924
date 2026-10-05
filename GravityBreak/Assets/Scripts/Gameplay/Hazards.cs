using UnityEngine;

namespace GravityBreak
{
    public enum HazardKind { Debris, Arc, Vent }

    /// <summary>
    /// Environmental hazard. Shapes differ per kind (diamond debris, triangle arcs, cross vents) so they
    /// are distinguishable without colour.
    /// </summary>
    public class Hazard : MonoBehaviour
    {
        public HazardKind kind;
        public float damage = 12f;
        public float onTime = 1.5f, offTime = 1.5f, phase;

        SpriteRenderer sr;
        PaletteTint tint;
        bool wasActive;

        public bool Active { get; private set; }

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            tint = GetComponent<PaletteTint>();
            Active = kind == HazardKind.Debris;
        }

        void Update()
        {
            if (kind == HazardKind.Debris) return;
            float cycle = onTime + offTime;
            Active = ((Time.time + phase) % cycle) < onTime;
            if (tint != null) tint.alpha = Active ? 1f : 0.25f;
            if (Active && !wasActive) AudioCues.Play(kind == HazardKind.Arc ? Cue.ArcOn : Cue.Vent, transform.position);
            if (Active && kind == HazardKind.Arc) transform.localScale = Vector3.one * (0.9f + 0.15f * Mathf.Sin(Time.time * 40f));
            wasActive = Active;
        }

        void OnTriggerStay2D(Collider2D other)
        {
            if (!Active || kind == HazardKind.Debris) return;
            PlayerController p;
            if (!other.TryGetComponent(out p)) return;
            if (kind == HazardKind.Arc) p.Integrity.Damage(damage, DamageSource.Hazard);
            else p.Integrity.Damage(damage * Time.deltaTime, DamageSource.Hazard, true); // vent = damage over time
        }

        void OnCollisionEnter2D(Collision2D c)
        {
            if (kind != HazardKind.Debris) return;
            PlayerController p;
            if (c.collider.TryGetComponent(out p) && c.relativeVelocity.magnitude > 2f)
                p.Integrity.Damage(damage, DamageSource.Debris);
        }
    }

    /// <summary>Debris falls along the *current* gravity vector, so rotating gravity re-aims it.</summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class DebrisBody : MonoBehaviour
    {
        Rigidbody2D rb;
        float life = 12f;

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
        }

        void FixedUpdate()
        {
            GravityManager g = GravityManager.Instance;
            if (g != null) rb.AddForce(g.Vector * g.strength * rb.mass);
            Vector2 v = rb.GetVel();
            if (v.magnitude > 18f) rb.SetVel(v.normalized * 18f);
            life -= Time.fixedDeltaTime;
            if (life <= 0f) Destroy(gameObject);
        }
    }

    /// <summary>Rains debris in from the side opposite gravity, faster as the section collapses.</summary>
    public class DebrisSpawner : MonoBehaviour
    {
        public Rect bounds;
        public float interval = 3.5f;
        public SectionRunner runner;
        float timer = 2.5f;

        void Update()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.Playing) return;
            timer -= Time.deltaTime;
            if (timer > 0f) return;

            float progress = runner != null ? runner.CollapseProgress : 0f;
            timer = Mathf.Lerp(interval, interval * 0.4f, progress) * Random.Range(0.8f, 1.2f);
            StartCoroutine(SpawnRoutine());
        }

        System.Collections.IEnumerator SpawnRoutine()
        {
            Vector2 g = GravityManager.Instance.Vector;
            Vector2 pos;
            const float margin = 1.2f;
            if (Mathf.Abs(g.y) >= Mathf.Abs(g.x))
            {
                float y = g.y < 0 ? bounds.yMax - margin : bounds.yMin + margin;
                pos = new Vector2(Random.Range(bounds.xMin + margin, bounds.xMax - margin), y);
            }
            else
            {
                float x = g.x < 0 ? bounds.xMax - margin : bounds.xMin + margin;
                pos = new Vector2(x, Random.Range(bounds.yMin + margin, bounds.yMax - margin));
            }
            if (Physics2D.OverlapCircle(pos, 0.7f) != null) yield break; // spot is blocked; skip this one

            // Telegraph: a warning marker + audio cue before the debris drops.
            GameObject warn = SpriteFactory.Make("DebrisWarning", transform, Shape.Triangle, pos, new Vector2(0.8f, 0.8f), 8, Palette.Get(Role.Hazard));
            warn.transform.rotation = Quaternion.Euler(0, 0, GravityManager.Instance.Dir.AngleDeg() + 90f);
            AudioCues.Play(Cue.DebrisIncoming, pos);
            yield return new WaitForSeconds(0.8f);
            Destroy(warn);

            GameObject d = SpriteFactory.Make("Debris", transform, Shape.Diamond, pos, new Vector2(0.9f, 0.9f), 4, Color.white);
            d.AddComponent<PaletteTint>().role = Role.Hazard;
            Rigidbody2D rb = d.AddComponent<Rigidbody2D>();
            rb.mass = 1f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.angularVelocity = Random.Range(-90f, 90f);
            BoxCollider2D col = d.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.8f, 0.8f);
            Hazard h = d.AddComponent<Hazard>();
            h.kind = HazardKind.Debris;
            h.damage = 14f;
            d.AddComponent<DebrisBody>();
        }
    }
}
