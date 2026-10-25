using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Okey101.Core.V4.Tests
{
    /// <summary>The handoff v4 golden vectors (tests/rules/*.json), one named case per vector id, run on the C# port.</summary>
    public sealed class V4VectorTests
    {
        public const int ExpectedVectorCount = 106;

        /// <summary>The package reuses three ids (MELD-19, MELD-20, FIN-09 twice each; docs/v4/hat-a/EKSIKLER.md EK-101): the second one is named "ID~2".</summary>
        public static readonly string[] DuplicatedPackageIds = { "MELD-19", "MELD-20", "FIN-09" };

        public static IEnumerable<TestCaseData> Vectors()
        {
            var seen = new Dictionary<string, int>();
            var position = 0;
            foreach (var (_, id, _) in VectorRunner.LoadAll())
            {
                seen[id] = seen.TryGetValue(id, out var n) ? n + 1 : 1;
                var name = seen[id] == 1 ? id : id + "~" + seen[id];
                yield return new TestCaseData(position++, id).SetName(name).SetCategory("V4Vectors");
            }
        }

        [TestCaseSource(nameof(Vectors))]
        public void Vector(int position, string id)
        {
            var (file, vid, vector) = VectorRunner.LoadAll().ElementAt(position);
            Assert.That(vid, Is.EqualTo(id));
            var fails = VectorRunner.Run(vector);
            Assert.That(fails, Is.Empty, file + " " + id + " · " + (string)vector["title"] + "\n  " + string.Join("\n  ", fails));
        }

        [Test]
        public void ThePackageHas106Vectors()
        {
            var ids = VectorRunner.LoadAll().Select(v => v.id).ToList();
            Assert.That(ids.Count, Is.EqualTo(ExpectedVectorCount));
            var duplicated = ids.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key).OrderBy(x => x, System.StringComparer.Ordinal).ToList();
            Assert.That(duplicated, Is.EqualTo(DuplicatedPackageIds.OrderBy(x => x, System.StringComparer.Ordinal).ToList()), "duplicate vector ids changed in the package");
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ExpectedVectorCount - DuplicatedPackageIds.Length));
        }

        [Test]
        public void VectorCopiesAreByteIdenticalToThePackage()
        {
            var root = VectorRunner.RepoRoot();
            var package = Path.Combine(root, VectorRunner.PackageVectorsFolder);
            if (!Directory.Exists(package)) Assert.Ignore("The v4 design package is not in this checkout: " + package);
            var index = JObject.Parse(File.ReadAllText(Path.Combine(package, "index.json")));
            var files = new List<string> { "index.json" };
            files.AddRange(index["files"].Select(f => (string)f));
            Assert.That(files.Count, Is.EqualTo(13));
            foreach (var f in files)
            {
                var copy = VectorRunner.VectorsPath(f);
                Assert.That(File.Exists(copy), Is.True, "missing copy " + f);
                Assert.That(File.ReadAllBytes(copy), Is.EqualTo(File.ReadAllBytes(Path.Combine(package, f))), f + " differs from the package");
            }
            var extra = Directory.GetFiles(VectorRunner.VectorsPath(), "*.json").Select(Path.GetFileName).Except(files).ToList();
            Assert.That(extra, Is.Empty, "copies that are not in the package");
        }

        [Test]
        public void TheRunnerReportsAWrongExpectation()
        {
            // Guard against a runner that passes everything: a vector with a wrong expected value must fail.
            var vector = VectorRunner.LoadAll().First(v => v.id == "DEAL-01").vector.DeepClone() as JObject;
            vector["init"]["expect"]["hand.indicator"] = "K3a";
            Assert.That(VectorRunner.Run(vector), Has.Count.EqualTo(1));
            var events = VectorRunner.LoadAll().First(v => v.id == "DEAL-05").vector.DeepClone() as JObject;
            events["steps"][2]["expect"]["events"][1]["seat"] = 2;
            Assert.That(VectorRunner.Run(events), Has.Count.EqualTo(1));
            var nulls = JToken.Parse("null");
            Assert.That(VectorRunner.Partial(nulls, VectorRunner.Undefined), Is.False, "null must not match a missing value");
            Assert.That(VectorRunner.Partial(JToken.Parse("[1,2]"), new List<object> { 1, 2, 3 }), Is.False);
            Assert.That(VectorRunner.Partial(JToken.Parse("{\"a\":1}"), new Dictionary<string, object> { { "a", 1L }, { "b", 2 } }), Is.True);
            Assert.That(VectorRunner.Partial(JToken.Parse("1"), "1"), Is.False, "no type coercion");
        }

        [Test]
        public void ApplyNeverChangesItsInput()
        {
            var s = G101Rules.NewMatch(new MatchOptions(), 42);
            s = G101Rules.Apply(s, GameAction.StartHand()).State;
            var before = JsonConvert.SerializeObject(s.ToTree());
            var seat = s.Hand.Turn.Seat;
            var r = G101Rules.Apply(s, GameAction.Discard(seat, s.Hand.Racks[seat][0]));
            Assert.That(r.Ok, Is.True, r.Error);
            Assert.That(JsonConvert.SerializeObject(s.ToTree()), Is.EqualTo(before));
            var bad = G101Rules.Apply(s, GameAction.Draw(seat, "deck"));
            Assert.That(bad.Ok, Is.False);
            Assert.That(bad.Error, Is.EqualTo("E_PHASE"));
            Assert.That(bad.State, Is.SameAs(s));
            Assert.That(bad.Events, Is.Empty);
        }
    }
}
