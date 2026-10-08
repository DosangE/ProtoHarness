using UnityEngine;
using ProtoHarness.ChainRush.Track;

namespace ProtoHarness.ChainRush.Endless
{
    // What the game and the enemy director need from an endless course, whichever way it lays its road:
    // EndlessCourse recycles fixed straight sectors, ProceduralCourse streams generated modules. Step runs
    // once per simulation tick and is called only by ChainRushGame.FixedUpdate.
    public abstract class CourseStream : MonoBehaviour
    {
        // Metres run since the start of the run.
        public abstract double Distance { get; }

        public abstract void Step();

        // Restarts the centerline at its origin with enough road ahead of the start.
        public abstract void SeedTrack(Centerline track);

        public abstract void ResetCourse();

        // True when the runner is grounded with enough solid road ahead for an encounter of this length.
        public abstract bool CanStartEncounter(float duration);
    }
}
