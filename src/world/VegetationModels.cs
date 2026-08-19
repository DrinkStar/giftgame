// Original — Kenney CC0 vegetation mounts
namespace SeaAnomaly;

using Godot;

/// <summary>
///   Paths and scales for the Kenney Nature Kit / Pirate Kit GLBs under
///   <c>res://assets/models/vegetation/</c>. <see cref="TryMount"/> instances
///   a packed scene as a child named "Model" and is fail-closed: missing or
///   unimported files leave the caller to keep its gray-box placeholder.
/// </summary>
public static class VegetationModels
{
    public const string Oak = "res://assets/models/vegetation/tree_oak.glb";
    public const string OakDark = "res://assets/models/vegetation/tree_oak_dark.glb";
    public const string Pine = "res://assets/models/vegetation/tree_pineTallA.glb";
    public const string PineDefault = "res://assets/models/vegetation/tree_pineDefaultA.glb";
    public const string Palm = "res://assets/models/vegetation/palm-detailed-straight.glb";
    public const string PalmBend = "res://assets/models/vegetation/palm-detailed-bend.glb";
    public const string Grass = "res://assets/models/vegetation/grass.glb";
    public const string GrassLarge = "res://assets/models/vegetation/grass_large.glb";
    public const string Shrub = "res://assets/models/vegetation/plant_bush.glb";
    public const string ShrubSmall = "res://assets/models/vegetation/plant_bushSmall.glb";
    public const string RockSmall = "res://assets/models/vegetation/rock_smallA.glb";
    public const string RockTall = "res://assets/models/vegetation/rock_tallA.glb";
    public const string RockLarge = "res://assets/models/vegetation/rock_largeA.glb";
    public const string RockSand = "res://assets/models/vegetation/rocks-sand-a.glb";
    public const string Driftwood = "res://assets/models/vegetation/log.glb";
    public const string DriftwoodLarge = "res://assets/models/vegetation/log_large.glb";
    public const string Stump = "res://assets/models/vegetation/stump_old.glb";
    public const string Barrel = "res://assets/models/props/barrel.glb";
    public const string Crate = "res://assets/models/props/crate.glb";
    public const string Chest = "res://assets/models/props/chest.glb";
    public const string WoodenChest = "res://assets/models/props/wooden_chest/WoodenChest.glb";
    public const string ShipWreck = "res://assets/models/props/ship-wreck.glb";
    public const string SandAlbedo = "res://assets/textures/sand_01_diff_1k.jpg";
    public const string CoastSandAlbedo = "res://assets/textures/coast_sand_01_diff_1k.jpg";
    public const string RockAlbedo = "res://assets/textures/rocks_ground_01_diff_1k.jpg";
    public const string VolcanoAlbedo = "res://assets/textures/rock_ground_02_diff_1k.jpg";
    public const string SnowAlbedo = "res://assets/textures/snow_02_diff_1k.jpg";

    public const float OakScale = 1.7f;
    public const float PineScale = 1.9f;
    public const float PalmScale = 1.15f;
    public const float GrassScale = 1.35f;
    public const float ShrubScale = 1.4f;
    public const float RockScale = 1.6f;
    public const float RockSandScale = 1.2f;
    public const float SalvageScale = 1.35f;
    public const float WoodenChestScale = 1f;
    public const float ShipWreckScale = 1.6f;

    /// <summary>
    ///   Mounts <paramref name="path"/> under <paramref name="host"/>. Returns
    ///   false when the GLB is missing so the host can keep a placeholder.
    /// </summary>
    public static bool TryMount(Node3D host, string path, float scale)
    {
        if (host == null || string.IsNullOrEmpty(path))
            return false;
        if (!ResourceLoader.Exists(path))
            return false;

        var packed = GD.Load<PackedScene>(path);
        if (packed == null)
            return false;

        var model = packed.Instantiate<Node3D>();
        if (model == null)
            return false;

        model.Name = "Model";
        model.Scale = Vector3.One * Mathf.Max(scale, 0.01f);
        host.AddChild(model);
        return true;
    }

    /// <summary>Hides named primitive meshes once a real model is mounted.</summary>
    public static void HidePlaceholders(Node host, params string[] names)
    {
        foreach (var name in names)
        {
            if (host.GetNodeOrNull<Node3D>(name) is { } node)
                node.Visible = false;
        }
    }
}
