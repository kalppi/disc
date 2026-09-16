using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Reusable utility for generating smooth 3D procedural tube / ribbon meshes along arbitrary 3D paths
/// using Rotation Minimizing Frames (Bishop Frames) and zero-allocation reusable vertex buffers.
/// </summary>
public class TubeMeshBuilder
{
    private readonly List<Vector3> _meshVerts;
    private readonly List<Vector3> _meshNorms;
    private readonly List<Color> _meshCols;
    private readonly List<Vector2> _meshUvs;
    private readonly List<int> _meshIndices;
    private readonly Godot.Collections.Array _surfaceArrays;

    public TubeMeshBuilder(int initialVertexCapacity = 512, int initialIndexCapacity = 1024)
    {
        _meshVerts = new List<Vector3>(initialVertexCapacity);
        _meshNorms = new List<Vector3>(initialVertexCapacity);
        _meshCols = new List<Color>(initialVertexCapacity);
        _meshUvs = new List<Vector2>(initialVertexCapacity);
        _meshIndices = new List<int>(initialIndexCapacity);

        _surfaceArrays = new Godot.Collections.Array();
        _surfaceArrays.Resize((int)Mesh.ArrayType.Max);
    }

    /// <summary>
    /// Generates a continuous 3D tube surface on the target ArrayMesh along a sequence of 3D points.
    /// </summary>
    /// <param name="targetMesh">The ArrayMesh to populate with the generated geometry.</param>
    /// <param name="points">List of sequential 3D world/local points defining the path.</param>
    /// <param name="radius">Base thickness radius of the tube mesh.</param>
    /// <param name="radialSegments">Number of polygonal facets around the tube perimeter (e.g. 6-16).</param>
    /// <param name="startColor">Color at the start of the path.</param>
    /// <param name="apexColor">Color at the apex or midpoint.</param>
    /// <param name="endColor">Color at the end of the path.</param>
    /// <param name="apexIndex">Explicit point index for apexColor; if -1, highest Y elevation is used.</param>
    /// <param name="generateCaps">Whether to cap the start and end of the tube.</param>
    /// <param name="startTaper">Scale multiplier for the starting point radius.</param>
    /// <param name="endTaper">Scale multiplier for the ending point radius.</param>
    /// <returns>True if mesh was successfully generated; false if insufficient points.</returns>
    public bool BuildTube(
        ArrayMesh targetMesh,
        IReadOnlyList<Vector3> points,
        float radius = 0.045f,
        int radialSegments = 8,
        Color? startColor = null,
        Color? apexColor = null,
        Color? endColor = null,
        int apexIndex = -1,
        bool generateCaps = true,
        float startTaper = 0.55f,
        float endTaper = 0.40f)
    {
        int pointCount = points.Count;
        if (targetMesh == null || pointCount < 2)
        {
            targetMesh?.ClearSurfaces();
            return false;
        }

        Color colStart = startColor ?? new Color(0.25f, 0.85f, 1.0f, 0.90f);
        Color colApex = apexColor ?? new Color(1.0f, 0.90f, 0.25f, 0.95f);
        Color colEnd = endColor ?? new Color(0.95f, 0.35f, 0.15f, 0.90f);

        _meshVerts.Clear();
        _meshNorms.Clear();
        _meshCols.Clear();
        _meshUvs.Clear();
        _meshIndices.Clear();

        radialSegments = Mathf.Clamp(radialSegments, 3, 32);
        float baseRadius = Mathf.Max(0.001f, radius);

        // Determine apex index if not specified
        if (apexIndex < 0 || apexIndex >= pointCount)
        {
            float maxY = float.MinValue;
            apexIndex = 0;
            for (int i = 0; i < pointCount; i++)
            {
                if (points[i].Y > maxY)
                {
                    maxY = points[i].Y;
                    apexIndex = i;
                }
            }
        }

        // 1. Compute tangents along the curve
        Vector3[] tangents = new Vector3[pointCount];
        Vector3[] normals = new Vector3[pointCount];
        Vector3[] binormals = new Vector3[pointCount];

        for (int i = 0; i < pointCount; i++)
        {
            if (i == 0)
                tangents[i] = (points[1] - points[0]).Normalized();
            else if (i == pointCount - 1)
                tangents[i] = (points[pointCount - 1] - points[pointCount - 2]).Normalized();
            else
                tangents[i] = (points[i + 1] - points[i - 1]).Normalized();

            if (tangents[i].LengthSquared() < 0.0001f)
                tangents[i] = Vector3.Forward;
        }

        // 2. Initialize Bishop frame (Rotation Minimizing Frame) to eliminate roll / twisting
        Vector3 initUp = Vector3.Up;
        if (Mathf.Abs(tangents[0].Dot(initUp)) > 0.95f)
        {
            initUp = Vector3.Right;
        }
        normals[0] = tangents[0].Cross(initUp).Normalized();
        binormals[0] = tangents[0].Cross(normals[0]).Normalized();

        // 3. Propagate frames smoothly using Double Reflection method
        for (int i = 0; i < pointCount - 1; i++)
        {
            Vector3 v1 = points[i + 1] - points[i];
            float c1 = v1.Dot(v1);
            if (c1 > 1e-6f)
            {
                Vector3 rL = normals[i] - (2.0f / c1) * v1.Dot(normals[i]) * v1;
                Vector3 tL = tangents[i] - (2.0f / c1) * v1.Dot(tangents[i]) * v1;
                Vector3 v2 = tangents[i + 1] - tL;
                float c2 = v2.Dot(v2);
                normals[i + 1] = (c2 > 1e-6f) ? (rL - (2.0f / c2) * v2.Dot(rL) * v2).Normalized() : rL.Normalized();
            }
            else
            {
                normals[i + 1] = normals[i];
            }
            binormals[i + 1] = tangents[i + 1].Cross(normals[i + 1]).Normalized();
        }

        // 4. Generate radial vertices along curve rings
        for (int i = 0; i < pointCount; i++)
        {
            Vector3 p = points[i];
            Vector3 n = normals[i];
            Vector3 b = binormals[i];

            // Color gradient calculation (Start -> Apex -> End)
            Color c;
            if (apexIndex > 0 && i <= apexIndex)
            {
                float t = (float)i / apexIndex;
                c = colStart.Lerp(colApex, t);
            }
            else if (apexIndex < pointCount - 1)
            {
                float t = (float)(i - apexIndex) / (pointCount - 1 - apexIndex);
                c = colApex.Lerp(colEnd, t);
            }
            else
            {
                c = colApex;
            }

            // Smooth tapering for ends
            float taper = 1.0f;
            if (i == 0) taper = startTaper;
            else if (i == 1) taper = Mathf.Lerp(startTaper, 1.0f, 0.65f);
            else if (i == pointCount - 1) taper = endTaper;
            else if (i == pointCount - 2) taper = Mathf.Lerp(endTaper, 1.0f, 0.60f);

            float r = baseRadius * taper;
            float vCoord = (float)i / (pointCount - 1);

            for (int s = 0; s < radialSegments; s++)
            {
                float angle = s * Mathf.Tau / radialSegments;
                float cosA = Mathf.Cos(angle);
                float sinA = Mathf.Sin(angle);

                Vector3 radialDir = (n * cosA + b * sinA).Normalized();
                Vector3 vertPos = p + radialDir * r;

                _meshVerts.Add(vertPos);
                _meshNorms.Add(radialDir);
                _meshCols.Add(c);
                _meshUvs.Add(new Vector2((float)s / radialSegments, vCoord));
            }
        }

        // 5. Generate tube surface triangle indices
        for (int i = 0; i < pointCount - 1; i++)
        {
            int ring0 = i * radialSegments;
            int ring1 = (i + 1) * radialSegments;

            for (int s = 0; s < radialSegments; s++)
            {
                int nextS = (s + 1) % radialSegments;

                int v00 = ring0 + s;
                int v01 = ring0 + nextS;
                int v10 = ring1 + s;
                int v11 = ring1 + nextS;

                _meshIndices.Add(v00);
                _meshIndices.Add(v10);
                _meshIndices.Add(v01);

                _meshIndices.Add(v01);
                _meshIndices.Add(v10);
                _meshIndices.Add(v11);
            }
        }

        // 6. Generate Caps if enabled
        if (generateCaps)
        {
            // Start cap
            int startCapIdx = _meshVerts.Count;
            _meshVerts.Add(points[0]);
            _meshNorms.Add(-tangents[0]);
            _meshCols.Add(colStart);
            _meshUvs.Add(new Vector2(0.5f, 0.0f));

            for (int s = 0; s < radialSegments; s++)
            {
                int nextS = (s + 1) % radialSegments;
                _meshIndices.Add(startCapIdx);
                _meshIndices.Add(nextS);
                _meshIndices.Add(s);
            }

            // End cap
            int endCapIdx = _meshVerts.Count;
            _meshVerts.Add(points[pointCount - 1]);
            _meshNorms.Add(tangents[pointCount - 1]);
            _meshCols.Add(colEnd);
            _meshUvs.Add(new Vector2(0.5f, 1.0f));

            int lastRingOffset = (pointCount - 1) * radialSegments;
            for (int s = 0; s < radialSegments; s++)
            {
                int nextS = (s + 1) % radialSegments;
                _meshIndices.Add(endCapIdx);
                _meshIndices.Add(lastRingOffset + s);
                _meshIndices.Add(lastRingOffset + nextS);
            }
        }

        // 7. Update Godot ArrayMesh
        targetMesh.ClearSurfaces();
        _surfaceArrays[(int)Mesh.ArrayType.Vertex] = _meshVerts.ToArray();
        _surfaceArrays[(int)Mesh.ArrayType.Normal] = _meshNorms.ToArray();
        _surfaceArrays[(int)Mesh.ArrayType.Color] = _meshCols.ToArray();
        _surfaceArrays[(int)Mesh.ArrayType.TexUV] = _meshUvs.ToArray();
        _surfaceArrays[(int)Mesh.ArrayType.Index] = _meshIndices.ToArray();

        targetMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, _surfaceArrays);
        return true;
    }
}
