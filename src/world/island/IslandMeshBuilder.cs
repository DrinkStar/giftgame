// Original (Iter8.5)
namespace SeaAnomaly;

using System;
using Godot;

/// <summary>
///   Builds the visual ArrayMesh and the HeightMapShape3D collision for one
///   island (T8.5.1), both from the same 0..1 normalized heightmap so the
///   rendered terrain and the physics surface coincide exactly.
///
///   Mesh layout: vertex (col, row) sits at local
///   <c>(col·cell - Radius, (h01 - 0.5)·HeightScale, row·cell - Radius)</c>
///   with <c>cell = Radius·2 / (resolution - 1)</c>; UVs are world
///   coordinates (spec.Center + local XZ) so a texture would tile in world
///   space. Triangles are wound counter-clockwise seen from above, so
///   <c>GenerateNormals</c> produces upward normals.
///
///   Collision: <see cref="Godot.HeightMapShape3D"/>. Verified against
///   Godot 4.7.1 (GodotSharp 4.7.1 + godotengine/godot@4.7 source):
///   <c>MapData</c> holds RAW local-space heights (grid points 1 unit apart,
///   centered on the node), and <c>MinHeight</c>/<c>MaxHeight</c> are
///   read-only values derived from the data — there is NO settable min/max
///   mapping in 4.7. To match the mesh (cell = <paramref name="gridScale"/>
///   meters) the caller scales the CollisionShape3D node UNIFORMLY by
///   <paramref name="gridScale"/> and the stored values are pre-divided by
///   it (GodotPhysics3D only supports uniform scaling of heightmap shapes —
///   the docs' recommended workaround). The world height of a value v is
///   then exactly <c>minHeight + v · (maxHeight - minHeight)</c>.
/// </summary>
public static class IslandMeshBuilder
{
    /// <summary>
    ///   Builds the island terrain mesh from the normalized heightmap.
    /// </summary>
    /// <param name="spec">Island description; must not be null.</param>
    /// <param name="heightmap">
    ///   The normalized heightmap from <see cref="IslandHeightmap.Generate"/>
    ///   (row-major, <c>resolution * resolution</c> values).
    /// </param>
    /// <param name="resolution">Grid size per axis; must match the heightmap.</param>
    /// <returns>
    ///   The ArrayMesh plus the world-space Y range
    ///   <c>(MinHeight, MaxHeight) = (-HeightScale/2, +HeightScale/2)</c>
    ///   that <see cref="BuildCollision"/> maps the normalized heights onto.
    /// </returns>
    public static (ArrayMesh Mesh, float MinHeight, float MaxHeight) Build(
        IslandSpec spec, float[] heightmap, int resolution)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(heightmap);
        if (resolution < 2)
            throw new ArgumentOutOfRangeException(nameof(resolution), "resolution must be >= 2.");
        if (heightmap.Length != resolution * resolution)
            throw new ArgumentException(
                $"heightmap must hold {resolution * resolution} values (got {heightmap.Length}).",
                nameof(heightmap));

        float cell = spec.Radius * 2f / (resolution - 1);
        float half = (resolution - 1) * 0.5f;
        float minHeight = -spec.HeightScale * 0.5f;
        float maxHeight = spec.HeightScale * 0.5f;

        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);

        // T9.5: per-vertex biome gradient — the shore band (h01 ≈ sea level
        // 0.5) is sand, inland blends to the tier's land color, and the
        // underwater slope is darkened wet sand. Zero textures needed; the
        // material uses VertexColorUseAsAlbedo.
        var sand = new Color(0.87f, 0.80f, 0.62f);
        var inland = spec.Tier switch
        {
            IslandTier.Spawn => new Color(0.32f, 0.55f, 0.30f), // low-risk grass
            IslandTier.Main => new Color(0.24f, 0.46f, 0.27f),  // forest green
            IslandTier.Storm => new Color(0.28f, 0.30f, 0.34f), // barren rock
            _ => new Color(0.5f, 0.5f, 0.5f)
        };

        for (int row = 0; row < resolution; row++)
        {
            float localZ = (row - half) * cell;
            for (int col = 0; col < resolution; col++)
            {
                float localX = (col - half) * cell;
                float h01 = heightmap[row * resolution + col];

                var color = sand.Lerp(inland, Mathf.Clamp((h01 - 0.55f) / 0.18f, 0f, 1f));
                if (h01 < 0.5f)
                {
                    color = color.Darkened(0.45f); // underwater slope
                }

                surface.SetColor(color);
                surface.SetNormal(Vector3.Up);
                surface.SetUV(new Vector2(spec.Center.X + localX, spec.Center.Y + localZ));
                surface.AddVertex(new Vector3(localX, Mathf.Lerp(minHeight, maxHeight, h01), localZ));
            }
        }

        // Two triangles per quad, wound counter-clockwise seen from above
        // (upward normals after GenerateNormals).
        for (int row = 0; row < resolution - 1; row++)
        {
            for (int col = 0; col < resolution - 1; col++)
            {
                int v00 = row * resolution + col;        // (col,   row)
                int v10 = v00 + 1;                       // (col+1, row)
                int v01 = v00 + resolution;              // (col,   row+1)
                int v11 = v01 + 1;                       // (col+1, row+1)

                surface.AddIndex(v00);
                surface.AddIndex(v11);
                surface.AddIndex(v10);

                surface.AddIndex(v00);
                surface.AddIndex(v01);
                surface.AddIndex(v11);
            }
        }

        surface.GenerateNormals();
        var mesh = surface.Commit() as ArrayMesh
            ?? throw new InvalidOperationException("SurfaceTool.Commit did not produce an ArrayMesh.");

        return (mesh, minHeight, maxHeight);
    }

    /// <summary>
    ///   Builds the HeightMapShape3D collision for the island. See the class
    ///   docs for the Godot 4.7 <c>MapData</c> contract (raw local-space
    ///   heights, no settable Min/MaxHeight).
    /// </summary>
    /// <param name="heightmap01">The same normalized heightmap the mesh used.</param>
    /// <param name="minHeight">World Y for heightmap 0 (= -HeightScale / 2).</param>
    /// <param name="maxHeight">World Y for heightmap 1 (= +HeightScale / 2).</param>
    /// <param name="resolution">Grid size per axis; must match the heightmap and the mesh.</param>
    /// <param name="gridScale">
    ///   Mesh cell size in meters (<c>Radius·2 / (resolution - 1)</c>). The
    ///   CollisionShape3D must be scaled uniformly by this factor for the
    ///   collision to align with the mesh.
    /// </param>
    /// <returns>
    ///   A shape whose <c>MapWidth</c>/<c>MapDepth</c> equal
    ///   <paramref name="resolution"/> and whose values, after the uniform
    ///   <paramref name="gridScale"/> node scale, land exactly on the mesh
    ///   surface.
    /// </returns>
    public static HeightMapShape3D BuildCollision(
        float[] heightmap01, float minHeight, float maxHeight, int resolution, float gridScale)
    {
        ArgumentNullException.ThrowIfNull(heightmap01);
        if (resolution < 2)
            throw new ArgumentOutOfRangeException(nameof(resolution), "resolution must be >= 2.");
        if (heightmap01.Length != resolution * resolution)
            throw new ArgumentException(
                $"heightmap must hold {resolution * resolution} values (got {heightmap01.Length}).",
                nameof(heightmap01));
        if (gridScale <= 0f)
            throw new ArgumentOutOfRangeException(nameof(gridScale), "gridScale must be > 0.");

        // Local units are grid cells; after the CollisionShape3D's uniform
        // scale `gridScale`, a stored local height h lands at world
        // `gridScale * h`. We want world height
        // minHeight + h01 * (maxHeight - minHeight), so store that value
        // divided by gridScale (the inverse-scale pre-scaling the docs
        // require for scaled heightmap shapes).
        var data = new float[resolution * resolution];
        for (int i = 0; i < data.Length; i++)
        {
            float worldHeight = Mathf.Lerp(minHeight, maxHeight, heightmap01[i]);
            data[i] = worldHeight / gridScale;
        }

        return new HeightMapShape3D
        {
            MapWidth = resolution,
            MapDepth = resolution,
            MapData = data
        };
    }
}
