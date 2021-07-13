using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MeshUtility
{
    public static Mesh CreatePlaneMesh(int size)
    {
        var mesh = new Mesh();

        var sizePerGrid = 0.5f;

        var totalMeterSize = size * sizePerGrid;
        var gridCount = size * size;
        var triangleCount = gridCount * 2;

        var vOffset = -totalMeterSize * 0.5f;

        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        float uvStrip = 1f / size;
        for (var z = 0; z <= size; z++)
        {
            for (var x = 0; x <= size; x++)
            {
                vertices.Add(new Vector3(vOffset + x * 0.5f, 0, vOffset + z * 0.5f));
                uvs.Add(new Vector2(x * uvStrip, z * uvStrip));
            }
        }
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);

        int[] indices = new int[triangleCount * 3];

        for (var gridIndex = 0; gridIndex < gridCount; gridIndex++)
        {
            var offset = gridIndex * 6;
            var vIndex = (gridIndex / size) * (size + 1) + (gridIndex % size);

            indices[offset] = vIndex;
            indices[offset + 1] = vIndex + size + 1;
            indices[offset + 2] = vIndex + 1;
            indices[offset + 3] = vIndex + 1;
            indices[offset + 4] = vIndex + size + 1;
            indices[offset + 5] = vIndex + size + 2;
        }
        mesh.SetIndices(indices, MeshTopology.Triangles, 0);
        mesh.UploadMeshData(false);
        return mesh;
    }
}
