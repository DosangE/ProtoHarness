namespace ProtoHarness.ChainRush.Track
{
    // A world position in track terms: S is distance along the centerline since the run began,
    // D is signed offset to the right, H is height above the centerline.
    public readonly struct TrackCoord
    {
        public readonly double S;
        public readonly float D;
        public readonly float H;

        public TrackCoord(double s, float d, float h)
        {
            S = s;
            D = d;
            H = h;
        }
    }
}
