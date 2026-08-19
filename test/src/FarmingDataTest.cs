// Original (Iter5) — no upstream port
namespace SeaAnomaly;

using Chickensoft.GoDotTest;
using Godot;
using Shouldly;

/// <summary>
///   Data-layer locks for the Iter5 farming content: the six shipped crops
///   match the Decision 3 table field-for-field, all thirteen W1 items load
///   with their file stem as id, and the flour/bread recipes carry the
///   Decision 5 ingredients. Also exercises the static lazy
///   <see cref="FarmingData"/> registry by crop id and seed id.
/// </summary>
public class FarmingDataTest : TestClass
{
  public FarmingDataTest(Node testScene)
    : base(testScene) { }

  [Test]
  public void AllSixCropsLoadWithDecision3Fields()
  {
    // Decision 3 table: (id, seed, produce, growth seconds, min-max yield).
    (string Id, string Seed, string Produce, float Growth, int Min, int Max)[] table =
    {
      ("potato", "potato_seed", "potato", 60f, 2, 4),
      ("carrot", "carrot_seed", "carrot", 75f, 2, 3),
      ("berry", "berry_seed", "berries", 90f, 3, 6),
      ("mushroom", "mushroom_seed", "mushroom", 120f, 2, 4),
      ("corn", "corn_seed", "corn", 100f, 2, 3),
      ("wheat", "wheat_seed", "wheat", 80f, 3, 5)
    };

    foreach (var (id, seed, produce, growth, min, max) in table)
    {
      var crop = GD.Load<CropData>($"res://assets/crops/{id}.tres");
      crop.ShouldNotBeNull();
      crop!.Id.ShouldBe(id);
      crop.DisplayName.ShouldNotBeNullOrEmpty();
      ContainsHan(crop.DisplayName).ShouldBeTrue($"{id}: {crop.DisplayName}");
      crop.SeedItemId.ShouldBe(seed);
      crop.ProduceItemId.ShouldBe(produce);
      crop.GrowthSeconds.ShouldBe(growth);
      crop.MinYield.ShouldBe(min);
      crop.MaxYield.ShouldBe(max);
    }
  }

  [Test]
  public void W1ThirteenItemsLoadWithFileStemIds()
  {
    // Decision 4 W1 set: 5 produce, 6 seeds, wheat_flour, bread. berries is
    // reused from the existing item set and intentionally not re-listed.
    string[] itemIds =
    {
      "potato", "carrot", "corn", "mushroom", "wheat",
      "potato_seed", "carrot_seed", "berry_seed", "mushroom_seed", "corn_seed",
      "wheat_seed", "wheat_flour", "bread"
    };

    foreach (var id in itemIds)
    {
      var item = GD.Load<ItemData>($"res://assets/items/{id}.tres");
      item.ShouldNotBeNull();
      item!.Id.ShouldBe(id);
      item.DisplayName.ShouldNotBeNullOrEmpty();
      ContainsHan(item.DisplayName).ShouldBeTrue($"{id}: {item.DisplayName}");
    }
  }

  [Test]
  public void FlourAndBreadRecipesLoadWithCorrectIngredients()
  {
    // Decision 5: flour = 1 wheat -> wheat_flour (no station, 5s).
    var flour = GD.Load<CraftingRecipe>("res://assets/recipes/flour.tres");
    flour.ShouldNotBeNull();
    flour!.Id.ShouldBe("flour");
    flour.Result.ShouldNotBeNull();
    flour.Result!.Id.ShouldBe("wheat_flour");
    flour.ResultAmount.ShouldBe(1);
    flour.Ingredients.Count.ShouldBe(1);
    flour.Ingredients[0].Item!.Id.ShouldBe("wheat");
    flour.Ingredients[0].Amount.ShouldBe(1);
    flour.RequiresCampfire.ShouldBeFalse();
    flour.RequiresWorkbench.ShouldBeFalse();
    flour.CraftingTime.ShouldBe(5f);

    // Decision 5: bread = 2 wheat_flour -> bread (campfire, 10s).
    var bread = GD.Load<CraftingRecipe>("res://assets/recipes/bread.tres");
    bread.ShouldNotBeNull();
    bread!.Id.ShouldBe("bread");
    bread.Result.ShouldNotBeNull();
    bread.Result!.Id.ShouldBe("bread");
    bread.ResultAmount.ShouldBe(1);
    bread.Ingredients.Count.ShouldBe(1);
    bread.Ingredients[0].Item!.Id.ShouldBe("wheat_flour");
    bread.Ingredients[0].Amount.ShouldBe(2);
    bread.RequiresCampfire.ShouldBeTrue();
    bread.RequiresWorkbench.ShouldBeFalse();
    bread.CraftingTime.ShouldBe(10f);
  }

  [Test]
  public void FarmingDataRegistryResolvesByCropIdAndSeedId()
  {
    // Static lazy registry: first access triggers the six GD.Loads.
    FarmingData.Crops.Count.ShouldBe(6);

    var potato = FarmingData.Get("potato");
    potato.ShouldNotBeNull();
    potato!.SeedItemId.ShouldBe("potato_seed");

    FarmingData.Get("wheat").ShouldNotBeNull();
    FarmingData.Get("not_a_crop").ShouldBeNull();

    var fromSeed = FarmingData.GetBySeedId("berry_seed");
    fromSeed.ShouldNotBeNull();
    fromSeed!.Id.ShouldBe("berry");
    fromSeed.ProduceItemId.ShouldBe("berries");

    FarmingData.GetBySeedId("wheat_seed")!.Id.ShouldBe("wheat");
    FarmingData.GetBySeedId("stone").ShouldBeNull();
  }

  [Test]
  public void HarvestPromptUsesChineseCropDisplayName()
  {
    var plot = new FarmPlot();
    try
    {
      plot.ApplySaveState(new FarmSaveData
      {
        CropId = "potato",
        ElapsedSeconds = 60f,
        Ready = true
      });
      plot.GetInteractionPrompt().ShouldBe("[E] 收获 土豆");
    }
    finally
    {
      plot.Free();
    }
  }

  private static bool ContainsHan(string text)
  {
    foreach (var c in text)
    {
      if (c >= 0x4E00 && c <= 0x9FFF)
        return true;
    }

    return false;
  }
}
