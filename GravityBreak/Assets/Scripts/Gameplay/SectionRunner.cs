using System;
using System.Collections.Generic;
using UnityEngine;

namespace GravityBreak
{
    public class SectionResult
    {
        public bool success;
        public float time;
        public int medal; // 0 none, 1 bronze, 2 silver, 3 gold
        public string failReason = "";
        public string tip = "";
        public AbilityType? newAbility;
    }

    /// <summary>
    /// Runs one section: the section timer, the collapse (which drains Structural Integrity), the 3-step
    /// rescue quest, and the win / lose conditions from the GDD.
    /// </summary>
    public class SectionRunner : MonoBehaviour
    {
        public static readonly string[] StepNames =
        {
            "Restore emergency power",
            "Reroute oxygen / seal the hull breach",
            "Reach and open the airlock"
        };

        public SectionDefinition Def { get; private set; }
        public PlayerController Player { get; private set; }
        public float Elapsed { get; private set; }
        public bool Phase2 { get; private set; }
        public readonly bool[] StepDone = new bool[3];
        public event Action<SectionResult> Finished;

        bool ended;
        float rumbleTimer = 3f;

        public float TimeLeft { get { return Mathf.Max(0f, Def.timeLimit - Elapsed); } }
        public float CollapseProgress { get { return Def.tutorial ? 0f : Mathf.Clamp01(Elapsed / Def.timeLimit); } }
        public int ActiveStep
        {
            get { for (int i = 0; i < StepDone.Length; i++) if (!StepDone[i]) return i; return -1; }
        }

        public void Init(SectionDefinition def, PlayerController player)
        {
            Def = def;
            Player = player;
        }

        void Update()
        {
            if (ended || GameManager.Instance.State != GameState.Playing) return;
            Elapsed += Time.deltaTime;

            if (!Def.tutorial)
            {
                // Collapse: Structural Integrity drains as the section's timer runs down, faster after power
                // is restored and the collapse is "triggered" visibly.
                float rate = Def.collapseDrain * Player.Integrity.max / Def.timeLimit * (Phase2 ? 1.8f : 1f);
                Player.Integrity.Damage(rate * Time.deltaTime, DamageSource.Collapse, true);

                rumbleTimer -= Time.deltaTime;
                if (rumbleTimer <= 0f)
                {
                    rumbleTimer = Phase2 ? 3f : 6f;
                    AudioCues.Play(Cue.Collapse, null);
                    if (CameraRig.Instance != null) CameraRig.Instance.Shake(Phase2 ? 0.2f : 0.1f);
                }

                if (Elapsed >= Def.timeLimit) { End(false, "The section collapsed before you reached the airlock."); return; }
            }
            if (Player.Integrity.IsDepleted) End(false, "Structural Integrity reached zero.");
        }

        /// <summary>Called by an Interactable. Returns true if the step was completed.</summary>
        public bool TryComplete(InteractKind kind)
        {
            if (ended) return false;
            switch (kind)
            {
                case InteractKind.Power:
                    if (StepDone[0]) return false;
                    StepDone[0] = true;
                    StepComplete(0);
                    if (!Def.tutorial)
                    {
                        Phase2 = true; // power surge triggers the visible collapse
                        StationAI.Say("Power surge! The collapse is accelerating - move!", 4f);
                        if (CameraRig.Instance != null) CameraRig.Instance.Shake(0.4f);
                    }
                    return true;
                case InteractKind.Breach:
                    if (!StepDone[0]) { Denied("The panel is dead. Restore power first."); return false; }
                    if (StepDone[1]) return false;
                    StepDone[1] = true;
                    StepComplete(1);
                    return true;
                default:
                    if (!StepDone[0] || !StepDone[1]) { Denied("Airlock locked. Finish the other repairs first."); return false; }
                    StepDone[2] = true;
                    StepComplete(2);
                    Win();
                    return true;
            }
        }

        void Denied(string msg)
        {
            AudioCues.Play(Cue.Denied, Player.transform.position);
            StationAI.Say(msg, 3f);
        }

        void StepComplete(int i)
        {
            AudioCues.Play(i == 2 ? Cue.SectionClear : Cue.QuestStep, Player.transform.position);
            if (i < 2) StationAI.Say("Objective complete: " + StepNames[i] + ".", 3f);
        }

        void Win()
        {
            SectionResult r = new SectionResult { success = true, time = Elapsed };
            r.medal = Elapsed <= Def.gold ? 3 : Elapsed <= Def.silver ? 2 : Elapsed <= Def.bronze ? 1 : 0;
            r.tip = BuildTip();
            End(r);
        }

        void End(bool success, string reason)
        {
            AudioCues.Play(Cue.Fail, null);
            SectionResult r = new SectionResult { success = false, time = Elapsed, failReason = reason };
            r.tip = BuildTip();
            End(r);
        }

        void End(SectionResult r)
        {
            if (ended) return;
            ended = true;
            if (Finished != null) Finished(r);
        }

        /// <summary>Performance-based hint shown between sections.</summary>
        string BuildTip()
        {
            Dictionary<DamageSource, float> d = Player.Integrity.Taken;
            DamageSource worst = DamageSource.Collapse;
            float most = 0f;
            foreach (KeyValuePair<DamageSource, float> kv in d)
                if (kv.Value > most) { most = kv.Value; worst = kv.Key; }

            if (Def.tutorial || most < 5f) return "Clean run. Chain gravity shifts to shave seconds off your time.";
            switch (worst)
            {
                case DamageSource.Debris: return "You lost most of your integrity to falling debris - try rotating gravity earlier to reposition before it lands.";
                case DamageSource.Landing: return "Hard landings cost you integrity - shift gravity closer to your target, or use Gravity Anchor to pause a long fall.";
                case DamageSource.Hazard: return "Electrical arcs and gas vents cycle on and off - watch the pattern and cross when they go dark.";
                default: return "The collapse drained most of your integrity - plan a shorter route and avoid detours. Repair kits restore integrity.";
            }
        }
    }
}
