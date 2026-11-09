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

        // ───────── 완주 뒤 현장 재도전 ─────────

        static readonly string[][] AllObserved = { new[] { "A", "B", "C" }, new[] { "H", "W" }, new[] { "P", "R" } };

        /// <summary>세 현장 완주 별 2·3·2.</summary>
        FieldProgress Complete232()
        {
            var p = new FieldProgress(database.missions.Count);
            p.Set(0, Eval(M(0), ("entrance", "walklight"), ("stairtop", "streetlamp")));
            p.Set(1, Eval(M(1), ("hall", "aircon"), ("corridor", "water"), ("yard", "shade")));
            p.Set(2, Eval(M(2), ("panel", "generator"), ("door", "pump")));
            Assert.AreEqual(new[] { 2, 3, 2 }, Enumerable.Range(0, 3).Select(i => p.Get(i).Stars).ToArray());
            Assert.IsTrue(p.IsComplete);
            return p;
        }

        FieldRestored RoundTripRetry(FieldProgress progress, FieldSavePhase phase, FieldPlan draft, FieldRetry retry)
        {
            var data = FieldSave.Capture(database.missions, progress, i => AllObserved[i], retry.Index, phase, draft, null, retry);
            return FieldSave.Restore(database.missions, FieldSave.Parse(FieldSave.ToJson(data)));
        }

        [Test]
        public void RetryWithMoreStarsReplacesOnlyThatMission()
        {
            var p = Complete232();
            var second = p.Get(1);
            var third = p.Get(2);
            var attempt = Eval(M(0), ("entrance", "walklight"), ("stairtop", "streetlamp"), ("park", "walklight"));
            Assert.AreEqual(3, attempt.Stars);
            Assert.IsTrue(p.SubmitRetry(0, attempt));
            Assert.AreSame(attempt, p.Get(0), "대표 결과는 이번 시도 그대로(배정 포함)");
            Assert.AreSame(second, p.Get(1), "다른 현장 결과는 그대로");
            Assert.AreSame(third, p.Get(2));
            Assert.AreEqual(8, p.TotalStars, "합계는 현장별 대표 결과로 다시 계산(누적 아님)");
            Assert.IsTrue(p.IsComplete);
        }

        [Test]
        public void RetryFailureOrFewerStarsKeepsRepresentativeAndUnlocks()
        {
            var p = Complete232();
            var first = p.Get(0);
            var fail = Eval(M(0), ("entrance", "walklight"));
            Assert.AreEqual(1, fail.Stars);
            Assert.IsFalse(p.SubmitRetry(0, fail), "실패는 대표 결과를 바꾸지 않는다");
            Assert.AreSame(first, p.Get(0));
            Assert.IsTrue(p.IsComplete, "완주 유지");
            Assert.IsTrue(p.IsUnlocked(1) && p.IsUnlocked(2), "해금 유지");
            var lower = Eval(M(1), ("hall", "aircon"), ("corridor", "water"));
            Assert.AreEqual(2, lower.Stars);
            Assert.IsFalse(p.SubmitRetry(1, lower), "별 3 → 2 하락은 반영하지 않는다");
            Assert.AreEqual(3, p.Get(1).Stars);
            Assert.AreEqual(7, p.TotalStars);
        }

        [Test]
        public void RetrySameStarsWithDifferentPlanUsesLatestWholePlan()
        {
            var p = Complete232();
            var other = Eval(M(0), ("entrance", "streetlamp"), ("stairtop", "streetlamp"));
            Assert.AreEqual(2, other.Stars);
            Assert.IsTrue(p.SubmitRetry(0, other), "같은 별의 성공이면 최신 배정을 대표 결과로");
            CollectionAssert.AreEquivalent(other.Assignments, p.Get(0).Assignments, "다른 시도의 배정과 섞지 않는다");
            Assert.AreEqual(7, p.TotalStars);
        }

        [Test]
        public void RetryInProgressRoundTripsSeparateFromRepresentative()
        {
            var p = Complete232();
            var r = RoundTripRetry(p, FieldSavePhase.Explore, Plan(M(0), ("entrance", "streetlamp")),
                new FieldRetry { Index = 0, Result = null, Observed = new[] { "A" } });
            Assert.IsTrue(r.Retrying);
            Assert.AreEqual(0, r.RetryIndex);
            Assert.AreEqual(0, r.Current);
            Assert.AreEqual(FieldSavePhase.Explore, r.Phase);
            Assert.IsNull(r.RetryResult);
            CollectionAssert.AreEquivalent(new[] { "A" }, r.RetryObserved, "이번 시도의 관찰");
            CollectionAssert.AreEquivalent(new[] { "A", "B", "C" }, r.Observed[0], "대표 결과의 관찰 기록은 그대로");
            Assert.AreEqual("streetlamp", r.Draft.ItemAt("entrance"));
            Assert.AreEqual(new[] { 2, 3, 2 }, r.Results.Select(x => x.Stars).ToArray(), "중간 종료가 완주 기록을 훼손하지 않는다");
            Assert.IsTrue(r.HasProgress);
        }

        [Test]
        public void ConfirmedRetryRestoresToRetryReviewWithThisAttempt()
        {
            var p = Complete232();
            var fail = Eval(M(0), ("entrance", "walklight"));
            p.SubmitRetry(0, fail);
            var r = RoundTripRetry(p, FieldSavePhase.Review, null, new FieldRetry { Index = 0, Result = fail, Observed = AllObserved[0] });
            Assert.AreEqual(FieldSavePhase.Review, r.Phase);
            Assert.AreEqual(1, r.RetryResult.Stars, "재확인은 이번 시도(별 1) 그대로");
            Assert.AreEqual(2, r.Results[0].Stars, "대표 결과는 별 2 그대로");

            var better = Eval(M(2), ("panel", "generator"), ("door", "pump"), ("detour", "sandbags"));
            var p2 = Complete232();
            Assert.IsTrue(p2.SubmitRetry(2, better));
            var r2 = RoundTripRetry(p2, FieldSavePhase.Review, null, new FieldRetry { Index = 2, Result = better, Observed = AllObserved[2] });
            Assert.AreEqual(3, r2.RetryResult.Stars);
            Assert.AreEqual(3, r2.Results[2].Stars);
            CollectionAssert.AreEquivalent(better.Assignments, r2.Results[2].Assignments, "엔딩 다시 보기에 쓰는 대표 배정");
        }

        [Test]
        public void RetryConfirmedWithoutRequiredObservationIsDemoted()
        {
            var p = Complete232();
            var attempt = Eval(M(0), ("entrance", "walklight"), ("stairtop", "streetlamp"), ("park", "walklight"));
            var r = RoundTripRetry(p, FieldSavePhase.Review, null, new FieldRetry { Index = 0, Result = attempt, Observed = new[] { "A" } });
            Assert.IsTrue(r.Retrying);
            Assert.IsNull(r.RetryResult, "관찰이 모자란 확정은 자동으로 확정하지 않는다");
            Assert.AreEqual(FieldSavePhase.Explore, r.Phase);
            Assert.AreEqual("walklight", r.Draft.ItemAt("park"), "계획으로 남겨 다시 고칠 수 있게");
            Assert.AreEqual(2, r.Results[0].Stars);
        }

        [Test]
        public void RetryInfoIsIgnoredUnlessAllMissionsSucceededOrAfterResult()
        {
            var partial = new FieldProgress(database.missions.Count);
            partial.Set(0, Eval(M(0), ("entrance", "walklight"), ("stairtop", "streetlamp")));
            var data = FieldSave.Capture(database.missions, partial, i => AllObserved[i], 0, FieldSavePhase.Explore, new FieldPlan(), null,
                new FieldRetry { Index = 0, Observed = new string[0] });
            var r = FieldSave.Restore(database.missions, FieldSave.Parse(FieldSave.ToJson(data)));
            Assert.IsFalse(r.Retrying, "완주 전 저장의 재도전 정보는 쓰지 않는다");

            // 재도전 결과를 본 뒤(엔딩까지 본 상태)에는 재도전 정보를 저장하지 않는다.
            var done = FieldSave.Capture(database.missions, Complete232(), i => AllObserved[i], 0, FieldSavePhase.Complete, null, null,
                new FieldRetry { Index = 0, Observed = AllObserved[0] });
            Assert.IsTrue(string.IsNullOrEmpty(done.retryMission));
            var rd = FieldSave.Restore(database.missions, FieldSave.Parse(FieldSave.ToJson(done)));
            Assert.AreEqual(FieldSavePhase.Complete, rd.Phase);
            Assert.IsFalse(rd.Retrying);

            // 이전 형식(재도전 필드 없음)은 일반 진행으로 읽힌다.
            var legacy = FieldSave.ToJson(done).Replace("\"retryMission\"", "\"_old1\"").Replace("\"retryConfirmed\"", "\"_old2\"");
            var rl = FieldSave.Restore(database.missions, FieldSave.Parse(legacy));
            Assert.IsFalse(rl.Retrying);
            Assert.AreEqual(FieldSavePhase.Complete, rl.Phase);
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
