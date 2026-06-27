using ILGPU;
using Core.Math;
using Core.Entities;

namespace Core.Physics
{
    public static class JointSystem
    {
        public static Vector3 ApplyJointForces(Vector3 currentForce, State myState, ParticleJoints joints, ArrayView<Transform> allTransforms, ArrayView<Vector3> allVelocities)
        {
            currentForce = CalculateLinkForce(currentForce, myState, joints.Link0, allTransforms, allVelocities);
            currentForce = CalculateLinkForce(currentForce, myState, joints.Link1, allTransforms, allVelocities);
            return currentForce;
        }

        private static Vector3 CalculateLinkForce(Vector3 currentForce, State myState, JointLink link, ArrayView<Transform> allTransforms, ArrayView<Vector3> allVelocities)
        {
            if (link.TargetParticleID >= 0)
            {
                Transform targetT = allTransforms[link.TargetParticleID];
                Vector3 targetV = allVelocities[link.TargetParticleID];

                Vector3 diff = myState.Position - targetT.Position;
                float dist = diff.Length();
                if (dist > 0.001f)
                {
                    Vector3 dir = diff / dist;
                    float springExtension = dist - link.RestLength;
                    float springForceMag = springExtension * link.Stiffness;

                    Vector3 relVelocity = myState.Velocity - targetV;
                    float dampingForceMag = (relVelocity.X * dir.X + relVelocity.Y * dir.Y + relVelocity.Z * dir.Z) * link.Damping;

                    float totalJointForce = springForceMag + dampingForceMag;

                    currentForce.X -= dir.X * totalJointForce;
                    currentForce.Y -= dir.Y * totalJointForce;
                    currentForce.Z -= dir.Z * totalJointForce;
                }
            }
            return currentForce;
        }
    }
}