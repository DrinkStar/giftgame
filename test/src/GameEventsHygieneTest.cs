// Original — no upstream port
namespace SeaAnomaly;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Chickensoft.GoDotTest;
using Godot;
using Shouldly;

/// <summary>
///   Static-scan hygiene guard for the <see cref="GameEvents"/> bus:
///   subscribe/unsubscribe must stay paired per file, and every declared
///   event must keep at least one producer and one consumer in src/
///   (GameOver is whitelisted: true-ending only, no producer or consumer
///   exists yet by design).
///
///   Scans res://src recursively. Comment stripping is line-based: a line
///   whose TrimStart() starts with "//" or "*" is skipped. Known limitations:
///   inline trailing comments and block-comment-embedded += / -= are not
///   recognized, and a `GameEvents.X +=` inside a string literal is counted
///   as a real subscription — keep such markup out of src/.
/// </summary>
public partial class GameEventsHygieneTest : TestClass
{
  private const string GameEventsFileName = "GameEvents.cs";

  [GeneratedRegex(@"GameEvents\.(\w+)\s*\+=")]
  private static partial Regex SubscribeRegex();

  [GeneratedRegex(@"GameEvents\.(\w+)\s*\-=")]
  private static partial Regex UnsubscribeRegex();

  [GeneratedRegex(@"Raise(\w+)\s*\(")]
  private static partial Regex RaiseCallRegex();

  [GeneratedRegex(@"public static event Action(?:<[^>]+>)?\??\s+(\w+);")]
  private static partial Regex DeclarationRegex();

  private static readonly List<string> WhitelistedProducerlessEvents =
    new() { "GameOver" };

  public GameEventsHygieneTest(Node testScene) : base(testScene) { }

  [Test]
  public void SubscriptionsArePairedPerFile()
  {
    var subscribe = CountMatches(SubscribeRegex(), excludeGameEventsFile: false);
    var unsubscribe = CountMatches(UnsubscribeRegex(), excludeGameEventsFile: false);

    var violations = new List<string>();
    foreach (var key in subscribe.Keys.Union(unsubscribe.Keys))
    {
      subscribe.TryGetValue(key, out var sub);
      unsubscribe.TryGetValue(key, out var unsub);
      if (sub != unsub)
      {
        violations.Add($"{key.File}:{key.Event} +={sub} -={unsub}");
      }
    }

    // The actual value carries the details so Shouldly prints them on failure.
    string.Join("\n", violations).ShouldBeEmpty();
  }

  [Test]
  public void EveryDeclaredEventHasProducerAndConsumer()
  {
    var declared = DeclaredEventNames();
    declared.Count.ShouldBe(51);

    var producers = ProducerFiles();
    var consumers = ConsumerFiles();

    var missing = new List<string>();
    foreach (var name in declared)
    {
      if (WhitelistedProducerlessEvents.Contains(name))
      {
        continue;
      }

      var sides = new List<string>();
      if (!producers.ContainsKey(name))
      {
        sides.Add("producer");
      }
      if (!consumers.ContainsKey(name))
      {
        sides.Add("consumer");
      }
      if (sides.Count > 0)
      {
        missing.Add($"{name}: missing {string.Join(" and ", sides)}");
      }
    }

    // The actual value carries the details so Shouldly prints them on failure.
    string.Join("\n", missing).ShouldBeEmpty();
  }

  [Test]
  public void DeclaredEventNamesAreUnique()
  {
    var declared = DeclaredEventNames();
    var duplicates = declared
      .GroupBy(name => name)
      .Where(group => group.Count() > 1)
      .Select(group => group.Key)
      .ToList();

    // The actual value carries the details so Shouldly prints them on failure.
    string.Join("\n", duplicates).ShouldBeEmpty();
  }

  private static string SrcRoot => ProjectSettings.GlobalizePath("res://src");

  private static List<string> EnumerateSourceFiles() =>
    new(Directory.EnumerateFiles(SrcRoot, "*.cs", SearchOption.AllDirectories));

  private static bool IsCommentLine(string line)
  {
    var trimmed = line.TrimStart();
    return trimmed.StartsWith("//", StringComparison.Ordinal)
      || trimmed.StartsWith('*');
  }

  private static List<string> DeclaredEventNames()
  {
    var path = Path.Combine(SrcRoot, "core", GameEventsFileName);
    var names = new List<string>();
    foreach (var line in File.ReadLines(path))
    {
      var match = DeclarationRegex().Match(line);
      if (match.Success)
      {
        names.Add(match.Groups[1].Value);
      }
    }
    return names;
  }

  private static Dictionary<(string File, string Event), int> CountMatches(
    Regex regex, bool excludeGameEventsFile)
  {
    var counts = new Dictionary<(string File, string Event), int>();
    foreach (var file in EnumerateSourceFiles())
    {
      var fileName = Path.GetFileName(file);
      if (excludeGameEventsFile && fileName == GameEventsFileName)
      {
        continue;
      }

      foreach (var line in File.ReadLines(file))
      {
        if (IsCommentLine(line))
        {
          continue;
        }

        foreach (Match match in regex.Matches(line))
        {
          var key = (fileName, match.Groups[1].Value);
          counts[key] = counts.GetValueOrDefault(key) + 1;
        }
      }
    }
    return counts;
  }

  private static Dictionary<string, HashSet<string>> ProducerFiles()
  {
    var result = new Dictionary<string, HashSet<string>>();
    foreach (var file in EnumerateSourceFiles())
    {
      var fileName = Path.GetFileName(file);
      if (fileName == GameEventsFileName)
      {
        continue;
      }

      foreach (var line in File.ReadLines(file))
      {
        if (IsCommentLine(line))
        {
          continue;
        }

        foreach (Match match in RaiseCallRegex().Matches(line))
        {
          var name = match.Groups[1].Value;
          if (!result.TryGetValue(name, out var files))
          {
            files = new HashSet<string>();
            result[name] = files;
          }
          files.Add(fileName);
        }
      }
    }
    return result;
  }

  private static Dictionary<string, HashSet<string>> ConsumerFiles()
  {
    var result = new Dictionary<string, HashSet<string>>();
    CollectFiles(CountMatches(SubscribeRegex(), excludeGameEventsFile: true), result);
    CollectFiles(CountMatches(UnsubscribeRegex(), excludeGameEventsFile: true), result);
    return result;
  }

  private static void CollectFiles(
    Dictionary<(string File, string Event), int> counts,
    Dictionary<string, HashSet<string>> result)
  {
    foreach (var key in counts.Keys)
    {
      if (!result.TryGetValue(key.Event, out var files))
      {
        files = new HashSet<string>();
        result[key.Event] = files;
      }
      files.Add(key.File);
    }
  }
}
