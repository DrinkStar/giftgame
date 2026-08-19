// Original — river-bank drinking (F), no upstream port
namespace SeaAnomaly;

using System;
using System.Collections.Generic;
using Godot;

/// <summary>
///   Pure river-drink rules: F only drinks river water when the hotbar has
///   no usable Food/Drink consumable, the player is inside an island
///   radius, and they stand in that island's carved river channel (not sea).
/// </summary>
public static class RiverDrink
{
    /// <summary>Thirst restored per sip — matches coconut <c>ThirstRestore</c>.</summary>
    public const float ThirstRestore = 25f;

    /// <summary>
    ///   True for an eatable Food (<c>Type == Food &amp;&amp; !RequiresCooking</c>)
    ///   or a Drink. Null, wood, spears, armor, and raw meat are false.
    /// </summary>
    public static bool HasUsableConsumable(ItemData? sel)
    {
        if (sel == null)
            return false;
        if (sel.Type == ItemType.Drink)
            return true;
        return sel.Type == ItemType.Food && !sel.RequiresCooking;
    }

    /// <summary>
    ///   If <paramref name="worldPos"/> is on a river bank of some spec in
    ///   <paramref name="specs"/>, drinks <see cref="ThirstRestore"/> and
    ///   returns true. Sea / off-island / empty specs do nothing.
    /// </summary>
    public static bool TryDrink(
        PlayerStats stats, Vector3 worldPos, IReadOnlyList<IslandSpec> specs)
    {
        ArgumentNullException.ThrowIfNull(stats);
        if (specs == null || specs.Count == 0)
            return false;

        var worldXz = new Vector2(worldPos.X, worldPos.Z);
        var spec = EnemyHabitat.NearestSpec(worldXz, specs);
        if ((worldXz - spec.Center).Length() > spec.Radius)
            return false;
        if (!IslandHeightmap.IsNearRiverChannel(spec, worldXz))
            return false;

        stats.Drink(ThirstRestore);
        return true;
    }
}
