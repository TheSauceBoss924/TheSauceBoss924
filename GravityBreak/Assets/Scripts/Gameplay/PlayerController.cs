using System;
using UnityEngine;

namespace GravityBreak
{
    /// <summary>
    /// Platformer controller that works for any gravity direction. Movement is decomposed into a component
    /// along gravity ("fall") and a component along the tangent ("run"), so jump / run / landing logic is
    /// written once and the 4 (later 8) orientations come for free.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(StructuralIntegrity))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        public float moveSpeed = 8f;
        public float groundAccel = 70f;
        public float airAccel = 40f;
        public float jumpSpeed = 12.5f;
        public float maxFall = 20f;
        public float coyoteTime = 0.1f;
        public float jumpBuffer = 0.12f;

        [Header("Landing")]
        public bool landingDamage = true;
        public float hardLandSpeed = 18f;
        public float damagePerSpeed = 3f;

        [Header("Abilities")]
        public float anchorDuration = 1.6f;
        public float anchorCooldown = 6f;
        public float pulseCooldown = 4f;
        public float pulseRadius = 5.5f;
        public float pulseImpulse = 14f;

        public event Action Jumped;

        public bool Grounded { get; private set; }
        public StructuralIntegrity Integrity { get; private set; }
        public Interactable Nearby;
        public float AnchorCooldownLeft { get { return Mathf.Max(0f, anchorCd); } }
        public float PulseCooldownLeft { get { return Mathf.Max(0f, pulseCd); } }
        public bool Anchored { get { return anchorTimer > 0f; } }

        Rigidbody2D rb;
        BoxCollider2D box;
        float coyoteTimer, bufferTimer, anchorTimer, anchorCd, pulseCd, lastAlong;
        bool jumpHeld, wasGrounded;
        readonly Collider2D[] hits = new Collider2D[8];
        readonly Collider2D[] groundHits = new Collider2D[4];

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            box = GetComponent<BoxCollider2D>();
            Integrity = GetComponent<StructuralIntegrity>();

            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.mass = 2f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            box.size = new Vector2(0.8f, 1f);
            box.sharedMaterial = new PhysicsMaterial2D("gb_player") { friction = 0f, bounciness = 0f };

            // Placeholder art: a body with a visor so the orientation is readable. Visuals live on children
            // so the root keeps scale 1 and the collider stays the size we set.
            GameObject body = SpriteFactory.Make("Body", transform, Shape.Square, transform.position, new Vector2(0.8f, 1f), 5, new Color(0.85f, 0.9f, 1f));
            body.transform.localPosition = Vector3.zero;
            GameObject visor = SpriteFactory.Make("Visor", transform, Shape.Square, transform.position, new Vector2(0.45f, 0.25f), 6, new Color(0.1f, 0.15f, 0.3f));
            visor.transform.localPosition = new Vector3(0.12f, 0.2f, 0f);
        }

        void Start()
        {
            GravityManager.Instance.Changed += OnGravityChanged;
        }

        void OnDestroy()
        {
            if (GravityManager.Instance != null) GravityManager.Instance.Changed -= OnGravityChanged;
        }

        void OnGravityChanged(GravityDir prev, GravityDir now)
        {
            // Velocity is kept in world space, so momentum carries through the shift (continuous, not jarring).
            AudioCues.Play(Cue.GravityShift, transform.position);
            if (CameraRig.Instance != null) CameraRig.Instance.Shake(0.08f);
        }

        // ---- input (per frame) ------------------------------------------------------------------
        void Update()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.Playing) return;
            GameInput input = GameInput.Instance;
            float dt = Time.deltaTime;

            if (input.Pressed(Act.Jump)) bufferTimer = jumpBuffer; else bufferTimer -= dt;
            jumpHeld = input.Held(Act.Jump);

            HandleGravityInput(input);

            if (input.Pressed(Act.Interact) && Nearby != null) Nearby.TryActivate();
            if (input.Pressed(Act.Anchor)) TryAnchor();
            if (input.Pressed(Act.Pulse)) TryPulse();

            anchorTimer -= dt;
            anchorCd -= dt;
            pulseCd -= dt;
        }

        void HandleGravityInput(GameInput input)
        {
            GravityDir? pressed = null;
            if (input.Pressed(Act.GravDown)) pressed = GravityDir.Down;
            else if (input.Pressed(Act.GravUp)) pressed = GravityDir.Up;
            else if (input.Pressed(Act.GravLeft)) pressed = GravityDir.Left;
            else if (input.Pressed(Act.GravRight)) pressed = GravityDir.Right;
            if (!pressed.HasValue) return;

            GravityManager gravity = GravityManager.Instance;
            GravityDir target = pressed.Value;

            // Diagonal Shift: with the modifier held, a cardinal press perpendicular to the current
            // cardinal gravity aims gravity halfway between the two.
            if (input.Held(Act.Diagonal) && Abilities.IsActive(AbilityType.DiagonalShift) && gravity.Dir.IsCardinal()
                && Mathf.Abs(Vector2.Dot(gravity.Dir.ToVector(), target.ToVector())) < 0.1f)
            {
                target = GravityDirs.Combine(gravity.Dir, target);
            }

            if (target == gravity.Dir) return;
            if (!gravity.TryShift(target)) AudioCues.Play(Cue.Denied, transform.position);
        }

        void TryAnchor()
        {
            if (!Abilities.IsActive(AbilityType.GravityAnchor)) return;
            if (anchorCd > 0f || Grounded) { AudioCues.Play(Cue.Denied, transform.position); return; }
            anchorTimer = anchorDuration;
            anchorCd = anchorCooldown;
            AudioCues.Play(Cue.Interact, transform.position, false);
            Captions.Add("[Gravity anchored]", transform.position);
        }

        void TryPulse()
        {
            if (!Abilities.IsActive(AbilityType.GravityPulse)) return;
            if (pulseCd > 0f) { AudioCues.Play(Cue.Denied, transform.position); return; }
            pulseCd = pulseCooldown;
            Vector2 origin = transform.position;
            int n = Physics2D.OverlapCircleNonAlloc(origin, pulseRadius, hits);
            for (int i = 0; i < n; i++)
            {
                Rigidbody2D body = hits[i].attachedRigidbody;
                if (body == null || body == rb || body.bodyType != RigidbodyType2D.Dynamic) continue;
                Vector2 dir = (body.position - origin).normalized;
                body.AddForce(dir * pulseImpulse * body.mass, ForceMode2D.Impulse);
            }
            AudioCues.Play(Cue.GravityShift, transform.position, false);
            Captions.Add("[Gravity pulse]", transform.position);
        }

        // ---- physics (fixed step) ---------------------------------------------------------------
        void FixedUpdate()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.Playing)
            {
                rb.SetVel(Vector2.zero);
                return;
            }
            GravityManager gravity = GravityManager.Instance;
            float dt = Time.fixedDeltaTime;

            Vector2 g = gravity.Vector;
            Vector2 t = new Vector2(-g.y, g.x); // "run right" relative to the current floor
            Vector2 v = rb.GetVel();
            float along = Vector2.Dot(v, g);   // + = falling toward the floor
            float tan = Vector2.Dot(v, t);

            // Orientation follows the smoothed roll so the player turns with the camera.
            rb.rotation = gravity.Roll;

            wasGrounded = Grounded;
            Grounded = CheckGround(g);
            if (Grounded) coyoteTimer = coyoteTime; else coyoteTimer -= dt;

            // Landing.
            if (Grounded && !wasGrounded)
            {
                float impact = lastAlong;
                if (impact > 3f) AudioCues.Play(Cue.Land, transform.position, false);
                if (landingDamage && impact > hardLandSpeed)
                {
                    Integrity.Damage((impact - hardLandSpeed) * damagePerSpeed + 2f, DamageSource.Landing);
                    AudioCues.Play(Cue.HardLand, transform.position);
                }
            }

            // Run.
            float axis = Mathf.Clamp(GameInput.Instance.Value(Act.MoveRight) - GameInput.Instance.Value(Act.MoveLeft), -1f, 1f);
            tan = Mathf.MoveTowards(tan, axis * moveSpeed, (Grounded ? groundAccel : airAccel) * dt);

            // Fall.
            if (anchorTimer > 0f) along = 0f;
            else if (Grounded && along > 0f) along = 0f;
            else along = Mathf.Min(along + gravity.strength * dt, maxFall);

            // Jump (with coyote time + input buffering) and a short-hop cut when released early.
            if (bufferTimer > 0f && coyoteTimer > 0f)
            {
                along = -jumpSpeed;
                bufferTimer = 0f;
                coyoteTimer = 0f;
                AudioCues.Play(Cue.Jump, transform.position, false);
                if (Jumped != null) Jumped();
            }
            else if (!jumpHeld && along < -jumpSpeed * 0.4f)
            {
                along *= 0.85f;
            }

            rb.SetVel(t * tan + g * along);
            lastAlong = along;
        }

        bool CheckGround(Vector2 g)
        {
            Vector2 center = (Vector2)transform.position + g * 0.52f;
            ContactFilter2D filter = new ContactFilter2D();
            filter.useTriggers = false;
            int n = Physics2D.OverlapBox(center, new Vector2(0.7f, 0.12f), rb.rotation, filter, groundHits);
            for (int i = 0; i < n; i++) if (groundHits[i] != box) return true;
            return false;
        }

        public void SnapTo(Vector2 pos)
        {
            transform.position = pos;
            rb.position = pos;
            rb.SetVel(Vector2.zero);
            lastAlong = 0f;
        }
    }
}
