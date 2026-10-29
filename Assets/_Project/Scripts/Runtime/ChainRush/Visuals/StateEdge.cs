namespace ProtoHarness.ChainRush.Visuals
{
    // Each presentation watches its own frames without changing the state it reads.
    public struct StateEdge
    {
        private bool wasOn;
        private bool hasStarted;
        private float startedAt;

        // True only on the frame the state turns on.
        public bool Update(bool on, float now)
        {
            bool started = on && !wasOn;
            wasOn = on;
            if (started)
            {
                hasStarted = true;
                startedAt = now;
            }
            return started;
        }

        // Seconds since the state last turned on; negative when it never did.
        public float Since(float now) => hasStarted ? now - startedAt : -1f;
    }
}
