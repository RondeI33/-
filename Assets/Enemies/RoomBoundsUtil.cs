using System.Collections.Generic;
using UnityEngine;

public static class RoomBoundsUtil
{
    public static List<Bounds> GenerateMultiBounds(Renderer[] renderers, float cellSize)
    {
        List<Bounds> result = new List<Bounds>();
        if (renderers.Length == 0) return result;

        Bounds fullBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            fullBounds.Encapsulate(renderers[i].bounds);

        int gridX = Mathf.Max(1, Mathf.CeilToInt(fullBounds.size.x / cellSize));
        int gridZ = Mathf.Max(1, Mathf.CeilToInt(fullBounds.size.z / cellSize));

        bool[,] occupied = new bool[gridX, gridZ];
        Vector3 origin = fullBounds.min;

        for (int r = 0; r < renderers.Length; r++)
        {
            Bounds rb = renderers[r].bounds;
            int minCX = Mathf.Clamp(Mathf.FloorToInt((rb.min.x - origin.x) / cellSize), 0, gridX - 1);
            int maxCX = Mathf.Clamp(Mathf.FloorToInt((rb.max.x - origin.x) / cellSize), 0, gridX - 1);
            int minCZ = Mathf.Clamp(Mathf.FloorToInt((rb.min.z - origin.z) / cellSize), 0, gridZ - 1);
            int maxCZ = Mathf.Clamp(Mathf.FloorToInt((rb.max.z - origin.z) / cellSize), 0, gridZ - 1);

            for (int x = minCX; x <= maxCX; x++)
                for (int z = minCZ; z <= maxCZ; z++)
                    occupied[x, z] = true;
        }

        return GreedyMerge(occupied, gridX, gridZ, origin, cellSize, fullBounds);
    }

    private static List<Bounds> GreedyMerge(bool[,] occupied, int gridX, int gridZ, Vector3 origin, float cellSize, Bounds fullBounds)
    {
        List<Bounds> result = new List<Bounds>();
        bool[,] used = new bool[gridX, gridZ];

        for (int z = 0; z < gridZ; z++)
        {
            for (int x = 0; x < gridX; x++)
            {
                if (!occupied[x, z] || used[x, z]) continue;

                int endX = x;
                while (endX + 1 < gridX && occupied[endX + 1, z] && !used[endX + 1, z])
                    endX++;

                int endZ = z;
                bool canExpand = true;
                while (canExpand && endZ + 1 < gridZ)
                {
                    for (int cx = x; cx <= endX; cx++)
                    {
                        if (!occupied[cx, endZ + 1] || used[cx, endZ + 1])
                        {
                            canExpand = false;
                            break;
                        }
                    }
                    if (canExpand) endZ++;
                }

                for (int cz = z; cz <= endZ; cz++)
                    for (int cx = x; cx <= endX; cx++)
                        used[cx, cz] = true;

                Vector3 min = new Vector3(
                    origin.x + x * cellSize,
                    fullBounds.min.y,
                    origin.z + z * cellSize
                );
                Vector3 max = new Vector3(
                    origin.x + (endX + 1) * cellSize,
                    fullBounds.max.y,
                    origin.z + (endZ + 1) * cellSize
                );

                Bounds b = new Bounds();
                b.SetMinMax(min, max);
                result.Add(b);
            }
        }

        return result;
    }
}
