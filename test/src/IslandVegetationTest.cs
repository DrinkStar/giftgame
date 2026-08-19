// Original — ridge vegetation sampling / tutorial dressing
namespace SeaAnomaly;

using System;
using System.Linq;
using Chickensoft.GoDotTest;
using Godot;
using Shouldly;

/// <summary>
///   Island vegetation: Analyze is seed-deterministic, greedy picks follow
///   ridge score (not disc scatter), and the Kenney GLBs are on disk.
/// </summary>
public class IslandVegetationTest : TestClass
{
    public IslandVegetationTest(Node testScene) : base(testScene) { }

    [Test]
    public void AnalyzeIsDeterministicForSameSpec()
    {
        var spec = WorldLayout.GenerateTutorialIsland(12345);
        var map = IslandHeightmap.Generate(spec, 65);
        var a = IslandVegetation.Analyze(spec, map, 65);
        var b = IslandVegetation.Analyze(spec, map, 65);

        a.Length.ShouldBe(b.Length);
        a.Length.ShouldBeGreaterThan(20);
        for (int i = 0; i < a.Length; i++)
        {
            a[i].Grid.ShouldBe(b[i].Grid);
            a[i].Local.ShouldBe(b[i].Local);
            a[i].RidgeScore.ShouldBe(b[i].RidgeScore, 0.0001);
            a[i].Yaw.ShouldBe(b[i].Yaw, 0.0001);
        }
    }

    [Test]
    public void PickPrefersHigherRidgeScoreAndKeepsSpacing()
    {
        var spec = WorldLayout.GenerateTutorialIsland(12345);
        var map = IslandHeightmap.Generate(spec, 65);
        var samples = IslandVegetation.Analyze(spec, map, 65);
        var trees = IslandVegetation.Pick(
            samples,
            s => s.Height01 >= 0.56f && s.Height01 <= 0.82f && s.RadialMeters >= 8f,
            s => s.RidgeScore,
            count: 5,
            minSpacing: 5f);

        trees.Count.ShouldBe(5);
        float minScore = trees.Min(t => t.RidgeScore);
        int betterIgnored = 0;
        foreach (var sample in samples)
        {
            if (sample.Height01 < 0.56f || sample.Height01 > 0.82f || sample.RadialMeters < 8f)
                continue;
            if (sample.RidgeScore <= minScore + 0.0001f)
                continue;
            bool far = trees.All(t =>
            {
                float dx = sample.Local.X - t.Local.X;
                float dz = sample.Local.Z - t.Local.Z;
                return dx * dx + dz * dz >= 25f;
            });
            if (far)
                betterIgnored++;
        }

        betterIgnored.ShouldBe(0);

        for (int i = 0; i < trees.Count; i++)
        {
            for (int j = i + 1; j < trees.Count; j++)
            {
                float dx = trees[i].Local.X - trees[j].Local.X;
                float dz = trees[i].Local.Z - trees[j].Local.Z;
                (dx * dx + dz * dz).ShouldBeGreaterThanOrEqualTo(25f);
            }
        }
    }

    [Test]
    public void KenneyVegetationFilesExist()
    {
        FileAccess.FileExists(VegetationModels.Oak).ShouldBeTrue();
        FileAccess.FileExists(VegetationModels.Pine).ShouldBeTrue();
        FileAccess.FileExists(VegetationModels.Palm).ShouldBeTrue();
        FileAccess.FileExists(VegetationModels.Grass).ShouldBeTrue();
        FileAccess.FileExists(VegetationModels.Shrub).ShouldBeTrue();
        FileAccess.FileExists(VegetationModels.RockSmall).ShouldBeTrue();
        FileAccess.FileExists(VegetationModels.SandAlbedo).ShouldBeTrue();
        FileAccess.FileExists(VegetationModels.PalmBend).ShouldBeTrue();
        FileAccess.FileExists(VegetationModels.RockLarge).ShouldBeTrue();
        FileAccess.FileExists(VegetationModels.RockSand).ShouldBeTrue();
        FileAccess.FileExists(VegetationModels.Driftwood).ShouldBeTrue();
        FileAccess.FileExists(VegetationModels.Barrel).ShouldBeTrue();
        FileAccess.FileExists(VegetationModels.Crate).ShouldBeTrue();
        FileAccess.FileExists(VegetationModels.ShipWreck).ShouldBeTrue();
        FileAccess.FileExists(VegetationModels.WoodenChest).ShouldBeTrue();
        FileAccess.FileExists(VegetationModels.CoastSandAlbedo).ShouldBeTrue();
        FileAccess.FileExists(VegetationModels.RockAlbedo).ShouldBeTrue();
        FileAccess.FileExists(VegetationModels.VolcanoAlbedo).ShouldBeTrue();
        FileAccess.FileExists(VegetationModels.SnowAlbedo).ShouldBeTrue();
    }

    [Test]
    public void AnalyzeSkipsWarpedSeaCellsAndKeepsRidgePicks()
    {
        var spec = WorldLayout.Generate(12345)[0];
        var map = IslandHeightmap.Generate(spec, 65);
        var samples = IslandVegetation.Analyze(spec, map, 65);
        samples.Length.ShouldBeGreaterThan(20);
        foreach (var sample in samples)
            sample.Height01.ShouldBeGreaterThanOrEqualTo(0.46f);

        var trees = IslandVegetation.Pick(
            samples,
            s => s.Height01 >= 0.56f && s.Height01 <= 0.82f && s.RadialMeters >= 16f,
            s => s.RidgeScore,
            count: 8,
            minSpacing: 5f);
        trees.Count.ShouldBe(8);
    }

    [Test]
    public void PickFillsDenseGrassBandAtRealSpacing()
    {
        var spec = WorldLayout.GenerateTutorialIsland(12345);
        var map = IslandHeightmap.Generate(spec, 65);
        var samples = IslandVegetation.Analyze(spec, map, 65);
        float grassSpacing = 1.6f;
        var grass = IslandVegetation.Pick(
            samples,
            s => s.RadialMeters >= 10f
                && s.RadialMeters <= spec.Radius * 0.72f
                && s.Height01 >= 0.55f && s.Height01 <= 0.82f
                && s.Slope < 0.95f,
            s => (1f - Mathf.Abs(s.Slope - 0.22f)) + s.RidgeScore * 0.25f,
            count: IslandBuilder.TutorialGrassCount,
            minSpacing: grassSpacing);

        grass.Count.ShouldBeGreaterThanOrEqualTo(20);
        grass.Count.ShouldBeLessThanOrEqualTo(IslandBuilder.TutorialGrassCount);
    }
}
