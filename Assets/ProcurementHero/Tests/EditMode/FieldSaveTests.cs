using System.IO;
using System.Linq;
using NUnit.Framework;
using ProcurementHero.Core.Field;
using ProcurementHero.EditorTools;
using ProcurementHero.Field;
using UnityEditor;

namespace ProcurementHero.Tests
{
    /// <summary>진행 자동 저장: 저장 형식·검증·복원 정책·파일 교체. 실제 사용자 저장 대신 메모리·임시 경로를 쓴다.</summary>
    public class FieldSaveTests
    {
        FieldDatabase database;
        string tempDir;

        [OneTimeSetUp]
        public void Load()
        {
            database = AssetDatabase.LoadAssetAtPath<FieldDatabase>(FieldSetup.DatabasePath);
            Assert.IsNotNull(database);
        }

        [SetUp]
        public void MakeTemp()
        {
            tempDir = Path.Combine(Path.GetTempPath(), "ph_save_test_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
        }

        [TearDown]
        public void CleanTemp()
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }

        FieldMissionData M(int i) => database.missions[i];

        static FieldEvaluation Eval(FieldMissionData m, params (string spot, string item)[] picks)
        {
            var spec = m.ToSpec();
            var plan = new FieldPlan();
            foreach (var (spot, item) in picks) plan.Toggle(spec, spot, item);
            return FieldRules.Evaluate(spec, plan);
        }

        static FieldPlan Plan(FieldMissionData m, params (string spot, string item)[] picks)
        {
            var spec = m.ToSpec();
            var plan = new FieldPlan();
            foreach (var (spot, item) in picks) plan.Toggle(spec, spot, item);
            return plan;
        }

        FieldRestored RoundTrip(FieldProgress progress, string[][] observed, int current, FieldSavePhase phase, FieldPlan draft, string spot)
        {
            var data = FieldSave.Capture(database.missions, progress, i => observed[i], current, phase, draft, spot);
            var json = FieldSave.ToJson(data);
            return FieldSave.Restore(database.missions, FieldSave.Parse(json));
        }

        [Test]
        public void UnconfirmedPlanRestoresAsEditableDraftWithObservations()
        {
            var progress = new FieldProgress(database.missions.Count);
            progress.Set(0, Eval(M(0), ("entrance", "streetlamp"), ("stairtop", "streetlamp")));
            var r = RoundTrip(progress, new[] { new[] { "A", "B", "C" }, new[] { "H" }, new string[0] }, 1, FieldSavePhase.Explore,
                Plan(M(1), ("waiting", "aircon")), "corridor");
            Assert.AreEqual(1, r.Current);
            Assert.AreEqual(FieldSavePhase.Explore, r.Phase);
            Assert.AreEqual(2, r.Results[0].Stars, "별은 저장하지 않고 배정으로 다시 계산");
            Assert.IsNull(r.Results[1], "확정 전 계획은 확정되지 않는다");
            Assert.AreEqual("aircon", r.Draft.ItemAt("waiting"));
            Assert.AreEqual("corridor", r.SelectedSpot);
            CollectionAssert.AreEquivalent(new[] { "H" }, r.Observed[1]);
            Assert.IsTrue(r.HasProgress);
        }

        [Test]
        public void ConfirmedResultRestoresToReviewEvenIfQuitDuringInstall()
        {
            var progress = new FieldProgress(database.missions.Count);
            progress.Set(0, Eval(M(0), ("entrance", "walklight"), ("stairtop", "streetlamp"), ("park", "walklight")));
            var r = RoundTrip(progress, new[] { new[] { "A", "B", "C" }, new string[0], new string[0] }, 0, FieldSavePhase.Review, null, null);
            Assert.AreEqual(FieldSavePhase.Review, r.Phase);
            Assert.AreEqual(3, r.Results[0].Stars);
            Assert.AreEqual(75, r.Results[0].Cost);
        }

        [Test]
        public void ReplanRestoresPreviousResultAndEditedDraft()
        {
            var progress = new FieldProgress(database.missions.Count);
            progress.Set(0, Eval(M(0), ("entrance", "streetlamp"), ("stairtop", "streetlamp")));
            var r = RoundTrip(progress, new[] { new[] { "A", "B", "C" }, new string[0], new string[0] }, 0, FieldSavePhase.Replan,
                Plan(M(0), ("entrance", "walklight")), "entrance");
            Assert.AreEqual(FieldSavePhase.Replan, r.Phase);
            Assert.AreEqual(2, r.Results[0].Stars, "이전 확정 결과는 그대로");
            Assert.AreEqual("walklight", r.Draft.ItemAt("entrance"), "편집 중 계획");
            Assert.IsNull(r.Draft.ItemAt("stairtop"));
        }

        [Test]
        public void ReplanPartialEditRestoresExactly()
        {
            var progress = new FieldProgress(database.missions.Count);
            progress.Set(0, Eval(M(0), ("entrance", "streetlamp"), ("stairtop", "streetlamp")));
            var r = RoundTrip(progress, new[] { new[] { "A", "B", "C" }, new string[0], new string[0] }, 0, FieldSavePhase.Replan,
                Plan(M(0), ("stairtop", "streetlamp"), ("park", "walklight")), "park");
            CollectionAssert.AreEquivalent(new[] { "stairtop=streetlamp", "park=walklight" }, r.Draft.Assignments.Select(a => a.Key + "=" + a.Value));
            Assert.AreEqual(2, r.Results[0].Stars, "이전 확정 결과는 비교·진행 정보로 남는다");
        }

        [Test]
        public void ReplanWithEverythingClearedStaysEmptyAfterJson()
        {
            var progress = new FieldProgress(database.missions.Count);
            progress.Set(0, Eval(M(0), ("entrance", "streetlamp"), ("stairtop", "streetlamp")));
            var data = FieldSave.Capture(database.missions, progress, i => i == 0 ? new[] { "A", "B", "C" } : new string[0], 0, FieldSavePhase.Replan, new FieldPlan(), "entrance");
            var json = FieldSave.ToJson(data);
            var r = FieldSave.Restore(database.missions, FieldSave.Parse(json));
            Assert.AreEqual(FieldSavePhase.Replan, r.Phase);
            Assert.AreEqual(0, r.Draft.Assignments.Count, "모두 취소한 편집 계획은 빈 채로 돌아온다(이전 확정 배정으로 다시 채우지 않음)");
            Assert.AreEqual(2, r.Results[0].Stars, "이전 결과는 그대로");
            // 같은 데이터로 한 번 더(타이틀 복귀 뒤 이어하기 → 다시 저장 → 재실행과 같은 경로)
            var again = FieldSave.Restore(database.missions, FieldSave.Parse(FieldSave.ToJson(FieldSave.Capture(database.missions, progress,
                i => i == 0 ? new[] { "A", "B", "C" } : new string[0], r.Current, r.Phase, r.Draft, r.SelectedSpot))));
            Assert.AreEqual(0, again.Draft.Assignments.Count);
        }

        [Test]
        public void ExploreWithEmptyPlanFromStartStaysEmpty()
        {
            var progress = new FieldProgress(database.missions.Count);
            var r = RoundTrip(progress, new[] { new[] { "A" }, new string[0], new string[0] }, 0, FieldSavePhase.Explore, new FieldPlan(), "entrance");
            Assert.AreEqual(FieldSavePhase.Explore, r.Phase);
            Assert.AreEqual(0, r.Draft.Assignments.Count);
        }

        [Test]
        public void LegacySaveWithoutDraftMarkerUsesPreviousResultForReplan()
        {
            // 편집 계획 표시(draftSaved)가 없는 이전 형식: 편집 데이터가 누락된 것으로 보고, 재계획은 이전 확정 배정을 고칠 수 있게 연다.
            string json = "{\"version\":1,\"currentMission\":\"f1\",\"phase\":\"Replan\",\"draft\":[],\"selectedSpot\":\"entrance\"," +
                          "\"missions\":[{\"id\":\"f1\",\"confirmed\":true,\"observed\":[\"A\",\"B\",\"C\"],\"picks\":[{\"spot\":\"entrance\",\"item\":\"streetlamp\"},{\"spot\":\"stairtop\",\"item\":\"streetlamp\"}]}]}";
            var r = FieldSave.Restore(database.missions, FieldSave.Parse(json));
            Assert.AreEqual(FieldSavePhase.Replan, r.Phase);
            Assert.AreEqual(2, r.Draft.Assignments.Count);
            // 확정 결과가 없는 이전 형식 탐색 저장은 빈 계획.
            var e = FieldSave.Restore(database.missions, FieldSave.Parse("{\"version\":1,\"currentMission\":\"f1\",\"phase\":\"Explore\"}"));
            Assert.AreEqual(0, e.Draft.Assignments.Count);
        }

        [Test]
        public void UnknownIdsAreDroppedWithoutBreakingRestore()
        {
            var data = new FieldSaveData
            {
                version = FieldSave.Version,
                currentMission = "nope",
                phase = "Explore",
                selectedSpot = "ghost",
                draft = { new FieldSavePick { spot = "entrance", item = "laser" }, new FieldSavePick { spot = "moon", item = "walklight" }, new FieldSavePick { spot = "entrance", item = "walklight" } }
            };
            data.missions.Add(new FieldSaveMission { id = "f1", observed = { "A", "Z" } });
            data.missions.Add(new FieldSaveMission { id = "unknown", confirmed = true });
            var r = FieldSave.Restore(database.missions, data);
            Assert.IsNotNull(r);
            Assert.AreEqual(0, r.Current, "알 수 없는 현장 ID는 첫 미완료 현장으로");
            CollectionAssert.AreEquivalent(new[] { "A" }, r.Observed[0]);
            Assert.AreEqual(1, r.Draft.Assignments.Count);
            Assert.AreEqual("walklight", r.Draft.ItemAt("entrance"));
            Assert.AreEqual(M(0).spots[0].id, r.SelectedSpot);
            Assert.IsNotEmpty(r.Warnings);
        }

        [Test]
        public void OverBudgetOrUnobservedConfirmedPlanIsNotAutoConfirmed()
        {
            var data = new FieldSaveData { version = FieldSave.Version, currentMission = "f1", phase = "Review" };
            // 세 곳 가로등 105 > 75: 예산 초과는 확정으로 되살리지 않는다.
            data.missions.Add(new FieldSaveMission
            {
                id = "f1", confirmed = true, observed = { "A", "B", "C" },
                picks = { new FieldSavePick { spot = "entrance", item = "streetlamp" }, new FieldSavePick { spot = "stairtop", item = "streetlamp" }, new FieldSavePick { spot = "park", item = "streetlamp" } }
            });
            // 앞 현장이 성공하지 않았는데 확정된 다음 현장도 버린다.
            data.missions.Add(new FieldSaveMission { id = "f2", confirmed = true, observed = { "H", "W" }, picks = { new FieldSavePick { spot = "waiting", item = "aircon" }, new FieldSavePick { spot = "corridor", item = "water" } } });
            var r = FieldSave.Restore(database.missions, data);
            Assert.IsNull(r.Results[0]);
            Assert.IsNull(r.Results[1]);
            Assert.AreEqual(FieldSavePhase.Explore, r.Phase, "확정 결과가 없으면 재확인이 아니라 편집 상태");
            Assert.AreEqual(3, r.Draft.Assignments.Count, "버린 확정은 고칠 수 있는 계획으로 남는다");
            Assert.AreEqual(0, r.Current);
        }

        [Test]
        public void CorruptedEmptyAndOtherVersionSavesStartFresh()
        {
            Assert.IsNull(FieldSave.Restore(database.missions, FieldSave.Parse("{ not json")));
            Assert.IsNull(FieldSave.Restore(database.missions, FieldSave.Parse("")));
            Assert.IsNull(FieldSave.Restore(database.missions, FieldSave.Parse(null)));
            Assert.IsNull(FieldSave.Restore(database.missions, FieldSave.Parse("{\"version\":0}")));
            Assert.IsNull(FieldSave.Restore(database.missions, FieldSave.Parse("{\"version\":99,\"currentMission\":\"f1\"}")), "모르는 버전은 쓰지 않는다");
        }

        [Test]
        public void CompletePhaseNeedsAllMissionsSucceeded()
        {
            var progress = new FieldProgress(database.missions.Count);
            progress.Set(0, Eval(M(0), ("entrance", "streetlamp"), ("stairtop", "streetlamp")));
            var r = RoundTrip(progress, new[] { new[] { "A", "B", "C" }, new string[0], new string[0] }, 0, FieldSavePhase.Complete, null, null);
            Assert.AreNotEqual(FieldSavePhase.Complete, r.Phase);
        }

        [Test]
        public void FileStoreReplacesThroughTempAndFallsBackToBackup()
        {
            var path = Path.Combine(tempDir, "sub", "field_progress.json");
            var store = new FileFieldSaveStore(path);
            Assert.IsNull(store.Read());
            store.Write("{\"version\":1,\"currentMission\":\"f1\"}");
            store.Write("{\"version\":1,\"currentMission\":\"f2\"}");
            Assert.IsFalse(File.Exists(path + ".tmp"), "임시 파일은 교체 뒤 남지 않는다");
            StringAssert.Contains("f2", store.Read());
            StringAssert.Contains("f1", File.ReadAllText(path + ".bak"), "직전 저장은 백업으로 남는다");
            // 본 파일이 깨지면(쓰다 꺼진 경우 등) 백업을 읽는다.
            File.WriteAllText(path, "{ broken");
            StringAssert.Contains("f1", store.Read());
            store.Delete();
            Assert.IsFalse(File.Exists(path) || File.Exists(path + ".bak"));
        }

        [Test]
        public void ProgressSaveIsSeparateFromRecordsAndSettings()
        {
            var oldKv = SaveService.Store;
            var oldSave = FieldSave.Store;
            try
            {
                var kv = new MemoryStore();
                SaveService.Store = kv;
                var save = new MemoryFieldSaveStore { Text = "{\"version\":1}" };
                FieldSave.Store = save;
                FieldRecords.SubmitTotal(7);
                kv.SetFloat("PH_SfxVolume", 0.3f);
                // 새 게임(진행 삭제)은 기록·설정을 지우지 않는다.
                FieldSave.Store.Delete();
                Assert.AreEqual(7, FieldRecords.BestTotal);
                Assert.AreEqual(0.3f, kv.GetFloat("PH_SfxVolume", 0f));
                // 최고 기록 초기화는 진행 저장을 지우지 않는다.
                save.Text = "{\"version\":1}";
                FieldRecords.Reset(database.missions.Select(m => m.id));
                Assert.AreEqual(-1, FieldRecords.BestTotal);
                Assert.IsNotNull(save.Text);
            }
            finally
            {
                SaveService.Store = oldKv;
                FieldSave.Store = oldSave;
            }
        }
    }
}
