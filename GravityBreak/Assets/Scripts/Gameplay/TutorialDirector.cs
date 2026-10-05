using System;
using System.Collections.Generic;
using UnityEngine;

namespace GravityBreak
{
    /// <summary>Interactive tutorial for the first section: the Station AI waits for the player to do each thing.</summary>
    public class TutorialDirector : MonoBehaviour
    {
        class Step { public Func<string> text; public Func<bool> done; }

        readonly List<Step> steps = new List<Step>();
        int index = -1;
        bool jumped;
        float startX;
        PlayerController player;
        SectionRunner runner;

        public void Init(PlayerController p, SectionRunner r)
        {
            player = p;
            runner = r;
            startX = p.transform.position.x;
            p.Jumped += () => jumped = true;
            GameInput gi = GameInput.Instance;
            GravityManager gm = GravityManager.Instance;

            steps.Add(new Step { text = () => "Welcome aboard, technician. Move with " + gi.Label(Act.MoveLeft) + " and " + gi.Label(Act.MoveRight) + ". Head right.", done = () => player.transform.position.x > startX + 4f });
            steps.Add(new Step { text = () => "Clear the crate: press " + gi.Label(Act.Jump) + " to jump.", done = () => jumped });
            steps.Add(new Step { text = () => "The station's gravity drive is yours to command. Press " + gi.Label(Act.GravUp) + " to rotate gravity upward.", done = () => gm.Dir == GravityDir.Up });
            steps.Add(new Step { text = () => "You are now walking on the ceiling. Reach the power console and press " + gi.Label(Act.Interact) + ".", done = () => runner.StepDone[0] });
            steps.Add(new Step { text = () => "Power restored. Rotate gravity right (" + gi.Label(Act.GravRight) + ") to reach the hull breach on the right wall, then press " + gi.Label(Act.Interact) + ".", done = () => runner.StepDone[1] });
            steps.Add(new Step { text = () => "Breach sealed. Rotate gravity left (" + gi.Label(Act.GravLeft) + ") and reach the airlock on the left wall.", done = () => runner.StepDone[2] });
            Advance();
        }

        void Advance()
        {
            index++;
            if (index < steps.Count) StationAI.Say(steps[index].text(), 9999f, true);
        }

        void Update()
        {
            if (index < 0 || index >= steps.Count || GameManager.Instance.State != GameState.Playing) return;
            if (steps[index].done()) Advance();
        }
    }
}
