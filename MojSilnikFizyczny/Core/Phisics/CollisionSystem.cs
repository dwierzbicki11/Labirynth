using ILGPU;
using Core.Math;
using Core.Entities;

namespace Core.Physics
{
    public static class CollisionSystem
    {
        public static Vector3 SolveGridCollisions(int globalIndex, Vector3 myPos, ArrayView<Transform> transforms, ArrayView<int> cellCounts, ArrayView<int> cellIndices, int gridDim, int maxCapacity, float cellSize, float radius)
        {
            int cellX = (int)(myPos.X / cellSize);
            int cellY = (int)(myPos.Y / cellSize);
            int cellZ = (int)(myPos.Z / cellSize);
            Vector3 pushForce = new Vector3(0, 0, 0);
            float targetDistance = radius * 2.0f;

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        int nx = cellX + dx; int ny = cellY + dy; int nz = cellZ + dz;
                        if (nx >= 0 && nx < gridDim && ny >= 0 && ny < gridDim && nz >= 0 && nz < gridDim)
                        {
                            int neighborCellIdx = nx + ny * gridDim + nz * gridDim * gridDim;
                            int count = cellCounts[neighborCellIdx];
                            if (count > maxCapacity) count = maxCapacity;

                            for (int i = 0; i < count; i++)
                            {
                                int otherIdx = cellIndices[neighborCellIdx * maxCapacity + i];
                                if (otherIdx == globalIndex) continue;

                                Transform otherTransform = transforms[otherIdx];
                                Vector3 diff = myPos - otherTransform.Position;
                                float dist = diff.Length();

                                if (dist < targetDistance && dist > 0.001f)
                                {
                                    Vector3 dir = diff / dist;
                                    float overlap = targetDistance - dist;
                                    pushForce = new Vector3(
                                        pushForce.X + dir.X * overlap * 15.0f,
                                        pushForce.Y + dir.Y * overlap * 15.0f,
                                        pushForce.Z + dir.Z * overlap * 15.0f
                                    );
                                }
                            }
                        }
                    }
                }
            }
            return pushForce;
        }
    }
}