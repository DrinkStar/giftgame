//-:cnd:noEmit
namespace SeaAnomaly;

using Godot;
using Chickensoft.GameTools.Displays;

#if RUN_TESTS
using System.Reflection;
using Chickensoft.GoDotTest;
using Chickensoft.GodotNodeInterfaces;
#endif

// This entry-point file is responsible for determining if we should run tests.
//
// If you want to edit your game's main entry-point, please see Game.tscn and
// Game.cs instead.

public partial class Main : Node2D
{
  public Vector2I DesignResolution => Display.UHD4k;
#if RUN_TESTS
  public TestEnvironment Environment = default!;
#endif

  public override void _Ready()
  {
    EnsureCrashLogService();

    // Correct any erroneous scaling and guess sensible defaults.
    GetWindow().LookGood(WindowScaleBehavior.UIFixed, DesignResolution);

#if RUN_TESTS
    // If this is a debug build, use GoDotTest to examine the
    // command line arguments and determine if we should run tests.
    Environment = TestEnvironment.From(OS.GetCmdlineArgs());
    if (Environment.ShouldRunTests)
    {
      RuntimeContext.IsTesting = true;
      CrashLog.Info("Main: running GoDotTest");
      CallDeferred("RunTests");
      return;
    }
#endif

    // If we don't need to run tests, we can just switch to the game scene.
    CrashLog.Info("Main: launching Game.tscn");
    CallDeferred("RunScene");
  }

  /// <summary>
  ///   C# autoloads can miss their first headless boot (script class not yet
  ///   registered). Guarantee a real CrashLogService under /root before tests
  ///   or the game scene start. Safe to call from <see cref="_Ready"/>.
  /// </summary>
  public void EnsureCrashLogService()
  {
    if (CrashLog.Service != null)
      return;

    var root = GetTree()?.Root;
    if (root == null)
      return;

    var existing = root.GetNodeOrNull("CrashLogService");
    if (existing is CrashLogService)
      return;

    if (existing != null)
      existing.Name = "CrashLogServiceStub";

    root.AddChild(new CrashLogService { Name = "CrashLogService" });
  }

#if RUN_TESTS
  private void RunTests()
    => _ = GoTest.RunTests(Assembly.GetExecutingAssembly(), this, Environment);
#endif

  private void RunScene()
    => GetTree().ChangeSceneToFile("res://src/Game.tscn");
}
//+:cnd:noEmit
