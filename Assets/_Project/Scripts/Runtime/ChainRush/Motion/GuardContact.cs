using UnityEngine;

namespace ProtoHarness.ChainRush.Motion
{
    // What a guard (a wall along the road edge) does to a runner that touches it. RunnerMotor finds the contact
    // (OnControllerColliderHit) and passes its level normal, pointing away from the guard.
    public static class GuardContact
    {
        // Removes the push into the guard, loses speed by how head-on the hit was (speedLoss for a head-on hit), and
        // turns a facing that points into the guard to run along it in the track's direction.
        public static void Glance(ref Vector3 velocity, ref float heading, Vector3 guardNormal, Vector3 trackForward, float speedLoss)
        {
            var flat = new Vector3(velocity.x, 0f, velocity.z);
            float speed = flat.magnitude;
            float into = -Vector3.Dot(flat, guardNormal);
            if (speed > 0f && into > 0f)
            {
                flat = (flat + guardNormal * into) * (1f - speedLoss * into / speed);
                velocity = new Vector3(flat.x, velocity.y, flat.z);
            }
            if (Vector3.Dot(RunDrive.FacingOf(heading), guardNormal) >= 0f) return;
            Vector3 along = Vector3.Cross(Vector3.up, guardNormal);
            if (Vector3.Dot(along, trackForward) < 0f) along = -along;
            heading = Mathf.Atan2(along.x, along.z) * Mathf.Rad2Deg;
        }

        // Rubbing along a guard: the level speed drops by deceleration (m/s a second) for this tick of contact.
        public static void Scrape(ref Vector3 velocity, float deceleration, float dt)
        {
            var flat = new Vector3(velocity.x, 0f, velocity.z);
            float speed = flat.magnitude;
            if (speed <= 0f) return;
            flat *= Mathf.Max(0f, speed - deceleration * dt) / speed;
            velocity = new Vector3(flat.x, velocity.y, flat.z);
        }
    }
}
