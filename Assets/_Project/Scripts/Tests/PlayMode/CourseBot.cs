using ProtoHarness.ChainRush;
using ProtoHarness.ChainRush.Combat;
using ProtoHarness.ChainRush.Control;
using ProtoHarness.ChainRush.Endless;
using ProtoHarness.ChainRush.Track;
using UnityEngine;

namespace ProtoHarness.Tests.PlayMode
{
    // A test bot for the procedural course: follows the centerline (TrackFollower), jumps shortly before
    // each gap, grapples across gaps that have an anchor, and attacks whenever an enemy can be hit.
    // Everything goes through the tick input, so the simulation sees an ordinary player.
    internal sealed class CourseBot : IInputSource
    {
        // The straight course's long-run test jumped within 4.5 m of the deck edge and grappled above 2.6 m;
        // that suits a grapple gap. A flat jump carries 10.25 m (COURSE.md 6-1), so a jump-only gap of up to
        // 7.7 m needs the takeoff within 2.5 m of the edge: 0.8 m lands 9.45 m past it.
        private const double GrappleGapJumpDistance = 4.5d;
        private const double JumpGapJumpDistance = 0.8d;
        private const float GrappleHeight = 2.6f;

        private readonly ChainRushGame game;
        private readonly RunnerMotor player;
        private readonly GrappleController grapple;
        private readonly EnemyDirector enemies;
        private readonly ProceduralCourse course;
        private readonly TrackFollower follower;

        public CourseBot(ChainRushGame game, RunnerMotor player, GrappleController grapple, EnemyDirector enemies, ProceduralCourse course)
        {
            this.game = game;
            this.player = player;
            this.grapple = grapple;
            this.enemies = enemies;
            this.course = course;
            follower = new TrackFollower(game.Track, player);
        }

        public void Poll() { }
        public void Clear() { }

        public TickInput Consume()
        {
            float steer = follower.Consume().Steer;
            bool primary = false;
            if (course.TryGetNextGap(out double startS, out _, out bool hasAnchor))
            {
                TrackCoord coord = game.Track.Project(player.transform.position);
                if (player.IsGrounded)
                {
                    double toEdge = startS - coord.S;
                    primary = toEdge > 0d && toEdge <= (hasAnchor ? GrappleGapJumpDistance : JumpGapJumpDistance);
                }
                else if (hasAnchor && !grapple.IsAttached && coord.H > GrappleHeight && coord.S > startS - 10d)
                {
                    // PrimaryPressed grapples when airborne (RunnerMotor.PrimaryAction).
                    primary = true;
                }
            }
            return new TickInput(steer, primary, false, enemies.CanAttack);
        }
    }
}
