using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Okey101.Core.Model;

namespace Okey101.Application.Tests
{
    public sealed class ScenarioLoaderTests
    {
        private string root;
        [SetUp] public void Setup() { root = Path.Combine(Path.GetTempPath(), "okey-scenario-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); }
        [TearDown] public void Cleanup() { Directory.Delete(root, true); }
        private void Save(RoundState state, string name = "valid.json") => new ReplayCodec(root).Write(new ReplayRecord(state, Array.Empty<GameCommand>(), ReplayCodec.Hash(state)), name);
        [Test] public void ValidScenarioReplacesRoundOnlyAfterValidation()
        {
            var expected = PracticeDeals.Create("Swap"); Save(expected);
            using (var session = new LocalSession())
            {
                session.Start(71, 0); var loader = new ScenarioLoader(root);
                Assert.That(loader.LoadInto(session, "valid.json").Success, Is.True);
                Assert.That(session.GetView(0).RoundId, Is.EqualTo(expected.RoundId));
                Assert.That(session.GetView(0).PendingDraw.Source, Is.EqualTo(DrawSource.PreviousDiscard));
            }
        }
        [Test] public void RejectsMalformedUnknownVersionOversizeAndDuplicates()
        {
            Save(PracticeDeals.Create("Series")); var json = File.ReadAllText(Path.Combine(root, "valid.json"));
            var bad = new[] { "{", json.Replace("\"SchemaVersion\":1", "\"SchemaVersion\":999"), json.Replace("101-v1", "101-v2"),
                json.Replace("xorshift32-fy-v1", "unknown"), json.Replace("\"Indicator\":102", "\"Indicator\":20"),
                json.Replace("\"Stock\":[", "\"Stock\":[0,"), "{\"SchemaVersion\":1}", new string('[', 100) + new string(']', 100) };
            using (var session = new LocalSession())
            {
                session.Start(72, 0); var before = session.CaptureReplay().ExpectedFinalHash;
                foreach (var content in bad)
                {
                    File.WriteAllText(Path.Combine(root, "bad.json"), content);
                    Assert.That(new ScenarioLoader(root).LoadInto(session, "bad.json").Success, Is.False, content.Substring(0, Math.Min(80, content.Length)));
                    Assert.That(session.CaptureReplay().ExpectedFinalHash, Is.EqualTo(before));
                }
                using (var file = File.Create(Path.Combine(root, "large.json"))) file.SetLength(ReplayCodec.MaxBytes + 1L);
                Assert.That(new ScenarioLoader(root).LoadInto(session, "large.json").Success, Is.False);
                Assert.That(session.CaptureReplay().ExpectedFinalHash, Is.EqualTo(before));
            }
        }
        [Test] public void RejectsRootedTraversalAndExternalTargets()
        {
            var codec = new ReplayCodec(root); var state = PracticeDeals.Create("Series");
            var record = new ReplayRecord(state, Array.Empty<GameCommand>(), ReplayCodec.Hash(state));
            foreach (var name in new[] { "../escape.json", "sub/../../escape.json", Path.Combine(root, "absolute.json"), "C:\\escape.json", "a.json:stream", "CON.json" })
            {
                Assert.Throws<ArgumentException>(() => codec.Write(record, name), name);
                Assert.That(codec.Read(name).Success, Is.False, name);
            }
            Assert.That(Directory.GetFiles(root), Is.Empty);
        }
        [Test] public void ExistingFileIsNeverOverwritten()
        {
            var state = PracticeDeals.Create("Series"); Save(state); var before = File.ReadAllBytes(Path.Combine(root, "valid.json"));
            Assert.Throws<IOException>(() => Save(PracticeDeals.Create("Pairs")));
            Assert.That(File.ReadAllBytes(Path.Combine(root, "valid.json")), Is.EqualTo(before));
            Assert.That(Directory.GetFiles(root).Select(Path.GetFileName), Is.EquivalentTo(new[] { "valid.json" }));
        }
    }
}
