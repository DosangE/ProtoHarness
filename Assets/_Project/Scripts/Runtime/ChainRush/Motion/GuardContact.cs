using UnityEngine;

namespace ProtoHarness.ChainRush.Motion
{
    // What a guard (a wall along the road edge) does to a runner that touches it. RunnerMotor finds the contact
    // (OnControllerColliderHit) and passes its level normal, pointing away from the guard.
    public static class GuardContact
    {
        // Removes the push into the guard and loses speed by how head-on the hit was (speedLoss for a head-on hit). The
        // facing stays the player's: a guard never turns it (user 2026-11-04).
        public static void Glance(ref Vector3 velocity, Vector3 guardNormal, float speedLoss)
        {
            var flat = new Vector3(velocity.x, 0f, velocity.z);
            float speed = flat.magnitude;
            float into = -Vector3.Dot(flat, guardNormal);
            if (speed > 0f && into > 0f)
            {
                flat = (flat + guardNormal * into) * (1f - speedLoss * into / speed);
                velocity = new Vector3(flat.x, velocity.y, flat.z);
            }
        }

        // A run pushing into a guard goes along it instead (user 2026-11-04): the speed along the guard that the runner
        // arrived with moves toward the run target's share along the guard, so a shallow rub keeps going and a square hit
        // stops. arrived is the velocity before this tick's drive, whose side grip would otherwise eat that speed.
        public static void Slide(ref Vector3 velocity, Vector3 arrived, Vector3 facing, Vector3 guardNormal, in RunDrive.Push push, float dt)
        {
            Vector3 along = Vector3.Cross(Vector3.up, guardNormal);
            float speed = Mathf.MoveTowards(Vector3.Dot(arrived, along), push.Target * Vector3.Dot(facing, along), push.Acceleration * dt);
            velocity = along * speed + Vector3.up * velocity.y;
        }

        // Against a guard the speed that sets the turn limit is gone, so steering keeps at least turnRate (degrees a second)
        // there: the player turns the runner out. turnLimit is what this tick's steering already had.
        public static void TurnOut(ref float heading, float steer, float turnLimit, float turnRate, float dt)
        {
            float extra = steer * Mathf.Max(0f, turnRate - turnLimit) * dt;
            heading = Mathf.Repeat(heading + extra + 180f, 360f) - 180f;
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
