namespace SeaAnomaly;

using System;
using System.Collections.Generic;
using System.IO;
using Chickensoft.GoDotTest;
using Godot;
using Shouldly;

/// <summary>
///   Crash / session log contract: dumps land in an injected folder, PlayerDied
///   is not a crash, and inventory/save snapshot keys are stripped.
/// </summary>
public class CrashLogTest : TestClass, IDisposable
{
  private const string TestFolder = "user://logs_test_writer";

  private string _absolute = "";
  private CrashLogWriter? _writer;

  public CrashLogTest(Node testScene) : base(testScene) { }

  [Setup]
  public void Setup()
  {
    _absolute = CrashLogWriter.ToAbsolutePath(TestFolder);
    if (Directory.Exists(_absolute))
      Directory.Delete(_absolute, recursive: true);

    Directory.CreateDirectory(_absolute);
    _writer = new CrashLogWriter(TestFolder, writeSessionFile: true, maxCrashFiles: 3);
  }

  [Cleanup]
  public void Cleanup() => Dispose();

  public void Dispose()
  {
    _writer?.Dispose();
    _writer = null;
    if (!string.IsNullOrEmpty(_absolute) && Directory.Exists(_absolute))
      Directory.Delete(_absolute, recursive: true);
    GC.SuppressFinalize(this);
  }

  [Test]
  public void SessionFileReceivesInfoWarnError()
  {
    _writer!.WriteLine(CrashLogLevel.Info, "hello-info");
    _writer.WriteLine(CrashLogLevel.Warn, "hello-warn");
    _writer.WriteLine(CrashLogLevel.Error, "hello-error");

    var session = Path.Combine(_absolute, CrashLogWriter.SessionFileName);
    File.Exists(session).ShouldBeTrue();
    var text = _writer.ReadSessionText();
    text.ShouldContain("[INFO] hello-info");
    text.ShouldContain("[WARN] hello-warn");
    text.ShouldContain("[ERROR] hello-error");
  }

  [Test]
  public void RingBufferKeepsMostRecentLines()
  {
    var writer = new CrashLogWriter(
      TestFolder, writeSessionFile: false, ringCapacity: 3
    );
    try
    {
      writer.WriteLine(CrashLogLevel.Info, "a");
      writer.WriteLine(CrashLogLevel.Info, "b");
      writer.WriteLine(CrashLogLevel.Info, "c");
      writer.WriteLine(CrashLogLevel.Info, "d");
      var ring = writer.CopyRing();
      ring.Count.ShouldBe(3);
      ring[0].ShouldContain("[INFO] b");
      ring[2].ShouldContain("[INFO] d");
    }
    finally
    {
      writer.Dispose();
    }
  }

  [Test]
  public void CrashDumpContainsReasonBodyAndRing()
  {
    _writer!.WriteLine(CrashLogLevel.Warn, "before-crash");
    var path = _writer.WriteCrashDump("test-reason", "boom-body");

    File.Exists(path).ShouldBeTrue();
    var text = File.ReadAllText(path);
    text.ShouldContain("reason=test-reason");
    text.ShouldContain("boom-body");
    text.ShouldContain("before-crash");
    text.ShouldContain("=== SeaAnomaly crash dump ===");
  }

  [Test]
  public void PlayerDiedInfoDoesNotWriteCrashDump()
  {
    _writer!.WriteLine(CrashLogLevel.Info, "PlayerDied — respawning (not a crash).");
    _writer.CrashDumpCount.ShouldBe(0);
    _writer.CountCrashFiles().ShouldBe(0);
  }

  [Test]
  public void ForbiddenSnapshotKeysAreStripped()
  {
    var path = _writer!.WriteCrashDump(
      "keys",
      "body",
      new Dictionary<string, string>
      {
        ["day"] = "3",
        ["inventory"] = "SECRET_STACKS",
        ["backpack"] = "SECRET_PACK",
        ["save_json"] = "SECRET_SAVE",
        ["item_count"] = "99",
        ["os"] = "Windows"
      }
    );

    var text = File.ReadAllText(path);
    text.ShouldContain("day=3");
    text.ShouldContain("os=Windows");
    text.ShouldNotContain("SECRET_STACKS");
    text.ShouldNotContain("SECRET_PACK");
    text.ShouldNotContain("SECRET_SAVE");
    text.ShouldNotContain("item_count=99");
  }

  [Test]
  public void CrashFilesArePrunedToMax()
  {
    for (var i = 0; i < 5; i++)
      _writer!.WriteCrashDump($"reason-{i}", $"body-{i}");

    _writer!.CountCrashFiles().ShouldBe(3);
    _writer.CrashDumpCount.ShouldBe(5);
  }

  [Test]
  public void SessionWriteCanBeDisabled()
  {
    var silent = new CrashLogWriter(TestFolder, writeSessionFile: false);
    try
    {
      silent.WriteLine(CrashLogLevel.Info, "no-file");
      // The Setup writer already created seaanomaly.log; this instance must
      // not be required to create a distinct session file of its own. The
      // ring still records the line.
      silent.CopyRing()[0].ShouldContain("no-file");
    }
    finally
    {
      silent.Dispose();
    }
  }

  [Test]
  public void RunningTestsDetectsGoDotTestFlag()
  {
    CrashLogService.IsRunningTests().ShouldBeTrue();
  }

  [Test]
  public void ServiceEnterTreeUsesTestFolderAndSkipsSessionFile()
  {
    CrashLogService.IsRunningTests().ShouldBeTrue();

    var service = new CrashLogService { Name = "CrashLogServiceProbe" };
    TestScene.AddChild(service);
    try
    {
      service.Writer.ShouldNotBeNull();
      service.Writer!.WritesSessionFile.ShouldBeFalse();
      service.Writer.Folder.ShouldBe(CrashLogService.TestFolder);
    }
    finally
    {
      TestScene.RemoveChild(service);
      service.Free();
    }
  }
}
