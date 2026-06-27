using Core.Math;

namespace Core.Math
{
    public static class PhysicsMath
    {
        public static Vector3 IntegrateVelocity(Vector3 velocity, Vector3 acceleration, float dt)
        {
            return new Vector3(
                velocity.X + acceleration.X * dt,
                velocity.Y + acceleration.Y * dt,
                velocity.Z + acceleration.Z * dt
            );
        }

        public static Vector3 IntegratePosition(Vector3 position, Vector3 velocity, float dt)
        {
            return new Vector3(
                position.X + velocity.X * dt,
                position.Y + velocity.Y * dt,
                position.Z + velocity.Z * dt
            );
        }
    }
}