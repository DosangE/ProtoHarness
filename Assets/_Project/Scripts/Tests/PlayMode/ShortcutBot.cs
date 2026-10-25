using ProtoHarness.ChainRush;
using ProtoHarness.ChainRush.Control;
using ProtoHarness.ChainRush.Track;
using UnityEngine;

namespace ProtoHarness.Tests.PlayMode
{
    // A test bot for a track's shortcuts (Finger Lanes' nails, DECISIONS 2026-10-25): it follows the centerline
    // (TrackFollower) and at each shortcut hooks the first corner pole as it reaches the junction, lets go a quarter turn
    // later, hooks the second at the end of the straight and lets go on the next row. It does not steer while it swings:
    // steering works the rope (PoleSwing).
    internal sealed class ShortcutBot : IInputSource
    {
        private const float QuarterTurn = 88f;
        // How far before a hook point the bot presses: more than one tick's run at the fastest speed class.
        private const double HookWindow = 0.6d;

        private enum Stage { OnLoop, FirstSwing, Straight, SecondSwing }

        private readonly ChainRushGame game;
        private readonly RunnerMotor runner;
        private readonly TrackFollower follower;
        private readonly double[] leaveS;
        private readonly double[] straightEndS;
        private Stage stage;
        private int active = -1;
        private float turnFrom;
        // The swing key went down for the tick just before this one: by now the runner swings, or the hook missed.
        private bool pressed;

        public ShortcutBot(ChainRushGame game, RunnerMotor runner, TrackDefinition definition)
        {
            this.game = game;
            this.runner = runner;
            follower = new TrackFollower(game.Track, runner);
            int count = definition.Shortcuts.Count;
            leaveS = new double[count];
            straightEndS = new double[count];
            for (int i = 0; i < count; i++)
            {
                TrackDefinition.Shortcut shortcut = definition.Shortcuts[i];
                leaveS[i] = shortcut.FromS;
                // The S the track gives the straight's end: S runs over the shortcut in proportion (Centerline.AddBranch).
                double along = Mathf.PI * shortcut.Radius * 0.5d + shortcut.Straight;
                straightEndS[i] = shortcut.FromS + along / shortcut.Length * (shortcut.ToS - shortcut.FromS);
            }
        }

        // Shortcuts ridden through, both swings done.
        public int Taken { get; private set; }
        // Hooks that found no pole: the bot gives that shortcut up and follows the loop.
        public int Missed { get; private set; }

        public void Poll() { }
        public void Clear() { }

        public TickInput Consume()
        {
            double s = game.Track.Project(runner.transform.position).S;
            switch (stage)
            {
                case Stage.OnLoop:
                    for (int i = 0; i < leaveS.Length; i++)
                        if (s >= leaveS[i] - HookWindow && s < leaveS[i]) Hook(i, Stage.FirstSwing);
                    break;
                case Stage.Straight:
                    if (s >= straightEndS[active] - HookWindow) Hook(active, Stage.SecondSwing);
                    break;
                default:
                    if (pressed && game.Racer.SwingTicks == 0)
                    {
                        Missed++;
                        follower.SwingHold = false;
                        stage = Stage.OnLoop;
                    }
                    else if (Mathf.DeltaAngle(turnFrom, runner.Heading) >= QuarterTurn)
                    {
                        follower.SwingHold = false;
                        if (stage == Stage.SecondSwing) Taken++;
                        stage = stage == Stage.FirstSwing ? Stage.Straight : Stage.OnLoop;
                    }
                    break;
            }
            pressed = follower.SwingPress;
            bool swinging = stage == Stage.FirstSwing || stage == Stage.SecondSwing;
            follower.Steering = !swinging;
            follower.Override = 0f;
            return follower.Consume();
        }

        private void Hook(int shortcut, Stage next)
        {
            active = shortcut;
            stage = next;
            turnFrom = runner.Heading;
            follower.SwingPress = true;
            follower.SwingHold = true;
        }
    }
}
