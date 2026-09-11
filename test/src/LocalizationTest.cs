// Original — localization locks for shipped DisplayName / prompt copy
namespace SeaAnomaly;

using Chickensoft.GoDotTest;
using Godot;
using Shouldly;

/// <summary>
///   Locks the confirmed Chinese DisplayName pass: every item / recipe / crop
///   resource carries a non-empty Han display name, item .tres files no longer
///   serialize Description, and the loot / harvest prompt shells stay Chinese.
/// </summary>
public class LocalizationTest : TestClass
{
  private static readonly string[] BannedEnglishDisplayNames =
  {
    "Wood", "Stone", "Coconut", "Berries", "Stone Axe", "Wooden Spear", "Torch",
    "Cooked Meat", "Wheat", "Corn", "Mushroom", "Berry", "Carrot", "Potato",
    "Flour", "Iron Ingot", "Fish Soup", "Salted Meat", "Backpack", "Arrow",
    "Milk", "Iron Armor", "Raw Meat", "Wooden Bow"
  };

  public LocalizationTest(Node testScene) : base(testScene) { }

  [Test]
  public void AllItemDisplayNamesAreChineseAndDescriptionIsGone()
  {
    var count = AssertHanResources("res://assets/items", "ItemData");
    count.ShouldBe(49);
  }

  [Test]
  public void AllRecipeDisplayNamesAreChinese()
  {
    var count = AssertHanResources("res://assets/recipes", "CraftingRecipe");
    count.ShouldBe(23);
  }

  [Test]
  public void AllCropDisplayNamesAreChinese()
  {
    var count = AssertHanResources("res://assets/crops", "CropData");
    count.ShouldBe(6);
  }

  [Test]
  public void BuildableDescriptionsThatUiShowsAreChinese()
  {
    using var dir = DirAccess.Open("res://assets/buildables");
    dir.ShouldNotBeNull();
    dir!.ListDirBegin();
    var file = dir.GetNext();
    var checkedCount = 0;
    while (!string.IsNullOrEmpty(file))
    {
      if (file.EndsWith(".tres", System.StringComparison.Ordinal) && file != "buildable_library.tres")
      {
        var path = $"res://assets/buildables/{file}";
        var buildable = GD.Load<BuildableResource>(path);
        buildable.ShouldNotBeNull(path);
        if (!string.IsNullOrEmpty(buildable!.Description))
        {
          ContainsHan(buildable.Description)
            .ShouldBeTrue($"{file}: {buildable.Description}");
          checkedCount++;
        }
      }

      file = dir.GetNext();
    }

    dir.ListDirEnd();
    checkedCount.ShouldBeGreaterThan(0);
  }

  [Test]
  public void SampleDisplayNamesMatchSurvivalIslandTone()
  {
    GD.Load<ItemData>("res://assets/items/wood.tres")!.DisplayName.ShouldBe("木头");
    GD.Load<ItemData>("res://assets/items/stone.tres")!.DisplayName.ShouldBe("石头");
    GD.Load<ItemData>("res://assets/items/coconut.tres")!.DisplayName.ShouldBe("椰子");
    GD.Load<ItemData>("res://assets/items/berries.tres")!.DisplayName.ShouldBe("浆果");
    GD.Load<ItemData>("res://assets/items/stone_axe.tres")!.DisplayName.ShouldBe("石斧");
    GD.Load<ItemData>("res://assets/items/wooden_spear.tres")!.DisplayName.ShouldBe("木矛");
    GD.Load<ItemData>("res://assets/items/torch.tres")!.DisplayName.ShouldBe("火把");
    GD.Load<ItemData>("res://assets/items/cooked_meat.tres")!.DisplayName.ShouldBe("熟肉");
    GD.Load<ItemData>("res://assets/items/wheat.tres")!.DisplayName.ShouldBe("小麦");
    GD.Load<ItemData>("res://assets/items/iron_ingot.tres")!.DisplayName.ShouldBe("铁锭");
    GD.Load<ItemData>("res://assets/items/fish_soup.tres")!.DisplayName.ShouldBe("鱼汤");
    GD.Load<ItemData>("res://assets/items/salted_meat.tres")!.DisplayName.ShouldBe("咸肉");
    GD.Load<ItemData>("res://assets/items/backpack.tres")!.DisplayName.ShouldBe("背包");
    GD.Load<ItemData>("res://assets/items/arrow.tres")!.DisplayName.ShouldBe("箭");
    GD.Load<ItemData>("res://assets/items/milk.tres")!.DisplayName.ShouldBe("牛奶");
    GD.Load<ItemData>("res://assets/items/iron_armor.tres")!.DisplayName.ShouldBe("铁甲");
    GD.Load<CraftingRecipe>("res://assets/recipes/stone_axe.tres")!.DisplayName
      .ShouldBe("石斧");
    GD.Load<CropData>("res://assets/crops/potato.tres")!.DisplayName.ShouldBe("土豆");
  }

  [Test]
  public void GroundLootPromptIsChinesePickup()
  {
    var loot = new GroundLoot { ItemId = "wood", Amount = 1 };
    try
    {
      loot.GetInteractionPrompt().ShouldBe("[E] 拾取 木头");
    }
    finally
    {
      loot.Free();
    }
  }

  [Test]
  public void FarmPlotHarvestPromptIsChinese()
  {
    var plot = new FarmPlot();
    try
    {
      plot.ApplySaveState(new FarmSaveData
      {
        CropId = "wheat",
        ElapsedSeconds = 80f,
        Ready = true
      });
      plot.GetInteractionPrompt().ShouldBe("[E] 收获 小麦");
    }
    finally
    {
      plot.Free();
    }
  }

  private static int AssertHanResources(string folder, string kind)
  {
    using var dir = DirAccess.Open(folder);
    dir.ShouldNotBeNull(folder);
    dir!.ListDirBegin();
    var file = dir.GetNext();
    var count = 0;
    while (!string.IsNullOrEmpty(file))
    {
      if (file.EndsWith(".tres", System.StringComparison.Ordinal))
      {
        count++;
        var path = $"{folder}/{file}";
        var displayName = kind switch
        {
          "ItemData" => GD.Load<ItemData>(path)!.DisplayName,
          "CraftingRecipe" => GD.Load<CraftingRecipe>(path)!.DisplayName,
          _ => GD.Load<CropData>(path)!.DisplayName
        };

        displayName.ShouldNotBeNullOrEmpty($"{path} DisplayName");
        ContainsHan(displayName).ShouldBeTrue($"{path}: {displayName}");
        BannedEnglishDisplayNames.ShouldNotContain(displayName);

        if (kind == "ItemData")
        {
          FileAccess.GetFileAsString(path).Contains("Description =")
            .ShouldBeFalse(path);
        }
      }

      file = dir.GetNext();
    }

    dir.ListDirEnd();
    return count;
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
