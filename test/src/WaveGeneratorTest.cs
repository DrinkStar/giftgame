// Original (perf P0/P1)
namespace SeaAnomaly;

using Chickensoft.GoDotTest;
using Godot;
using Shouldly;

/// <summary>Pure MapSize normalization — no GPU / RenderingDevice required.</summary>
public class WaveGeneratorTest : TestClass
{
  public WaveGeneratorTest(Node testScene) : base(testScene) { }

  [Test]
  public void NormalizeMapSize_DefaultAndPowersOfTwoStayValid()
  {
    WaveGenerator.NormalizeMapSize(256).ShouldBe(256);
    WaveGenerator.NormalizeMapSize(512).ShouldBe(512);
    WaveGenerator.NormalizeMapSize(128).ShouldBe(128);
  }

  [Test]
  public void NormalizeMapSize_NonPowerOfTwoSnapsDown()
  {
    WaveGenerator.NormalizeMapSize(300).ShouldBe(256);
    WaveGenerator.NormalizeMapSize(200).ShouldBe(128);
  }

  [Test]
  public void NormalizeMapSize_ClampsToWorkgroupSafeRange()
  {
    WaveGenerator.NormalizeMapSize(16).ShouldBe(128);
    WaveGenerator.NormalizeMapSize(4096).ShouldBe(512);
  }
}
