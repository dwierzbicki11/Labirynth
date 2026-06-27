using ILGPU;
using Core.Math;
using Core.Entities;

namespace Core.Physics
{
    public static class ForceIntegrator
    {
        public static Derivative EvaluateDerivative(State initial, float dt, Derivative d, float time, ParticleJoints joints, ArrayView<Transform> allTransforms, ArrayView<Vector3> allVelocities)
        {
            State state = new State {
                Position = initial.Position + d.Velocity * dt,
                Velocity = initial.Velocity + d.Acceleration * dt
            };

            Vector3 force = FluidDynamics.CalculateFluidForces(state, time);
            force = JointSystem.ApplyJointForces(force, state, joints, allTransforms, allVelocities);

            // Przyspieszenie wyliczane bezpośrednio jako Siła / Masa (Masa = 1.0)
            return new Derivative { Velocity = state.Velocity, Acceleration = force / 1.0f };
        }

        public static State IntegrateRK4(State state, float dt, float time, ParticleJoints joints, ArrayView<Transform> allTransforms, ArrayView<Vector3> allVelocities)
        {
            Derivative a = EvaluateDerivative(state, 0.0f, new Derivative(), time, joints, allTransforms, allVelocities);
            Derivative b = EvaluateDerivative(state, dt * 0.5f, a, time, joints, allTransforms, allVelocities);
            Derivative c = EvaluateDerivative(state, dt * 0.5f, b, time, joints, allTransforms, allVelocities);
            Derivative d = EvaluateDerivative(state, dt, c, time, joints, allTransforms, allVelocities);

            Vector3 dpos = (a.Velocity + (b.Velocity * 2.0f) + (c.Velocity * 2.0f) + d.Velocity) * (1.0f / 6.0f);
            Vector3 dvel = (a.Acceleration + (b.Acceleration * 2.0f) + (c.Acceleration * 2.0f) + d.Acceleration) * (1.0f / 6.0f);

            return new State {
                Position = state.Position + dpos * dt,
                Velocity = state.Velocity + dvel * dt
            };
        }
    }
}