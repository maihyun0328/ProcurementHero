using System.Linq;
using NUnit.Framework;
using ProcurementHero.Core;
using ProcurementHero.EditorTools;
using UnityEditor;
using UnityEngine;

namespace ProcurementHero.Tests
{
    /// <summary>메모리 저장소로 바꿔 시험하므로 사용자의 실제 PlayerPrefs 기록은 바뀌지 않는다.</summary>
    public class SaveServiceTests
    {
        IKeyValueStore original;
        bool realHadBest;
        float realBest;

        [SetUp]
        public void UseMemoryStore()
        {
            realHadBest = PlayerPrefs.HasKey("PH_BestScore");
            realBest = PlayerPrefs.GetFloat("PH_BestScore", -1f);
            original = SaveService.Store;
            SaveService.Store = new MemoryStore();
        }

        [TearDown]
        public void Restore()
        {
            SaveService.Store = original;
            Assert.AreEqual(realHadBest, PlayerPrefs.HasKey("PH_BestScore"), "실제 PlayerPrefs가 바뀌면 안 된다");
            Assert.AreEqual(realBest, PlayerPrefs.GetFloat("PH_BestScore", -1f));
        }

        /// <summary>세 미션 모두 기본형 두 개를 산 경우의 실제 총점(약 158.08235294117645).</summary>
        static double AllBasicTotal()
        {
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(GameSetup.DatabasePath);
            Assert.IsNotNull(db, "Setup Game을 먼저 실행해야 합니다.");
            return db.missions.Sum(m => Scoring.Evaluate(m.ToSpec(),
                m.productsA.Single(p => p.type == ProductType.Basic).ToSpec(),
                m.productsB.Single(p => p.type == ProductType.Basic).ToSpec()).TotalScore);
        }

        [Test]
        public void ReportedCaseTotalIsAsDescribed()
        {
            Assert.AreEqual(158.08235294117645, AllBasicTotal(), 1e-9);
        }

        [Test]
        public void FirstRecordIsNew()
        {
            Assert.IsFalse(SaveService.HasBest);
            Assert.IsTrue(SaveService.SubmitRecord(AllBasicTotal(), 0));
            Assert.IsTrue(SaveService.HasBest);
        }

        [Test]
        public void SameRecordAgainIsNotNew()
        {
            double total = AllBasicTotal();
            Assert.IsTrue(SaveService.SubmitRecord(total, 0));
            Assert.IsFalse(SaveService.SubmitRecord(total, 0), "같은 엔딩을 다시 열면 새 기록이 아니다");
            Assert.IsFalse(SaveService.SubmitRecord(total, 0));
        }

        [Test]
        public void LowerRecordIsNotNewAndKeepsBest()
        {
            SaveService.SubmitRecord(196.5, 3);
            Assert.IsFalse(SaveService.SubmitRecord(158.08235294117645, 0));
            Assert.AreEqual(196.5f, SaveService.BestScore);
            Assert.AreEqual(3, SaveService.BestSuccess);
        }

        [Test]
        public void HigherRecordIsNew()
        {
            SaveService.SubmitRecord(158.08235294117645, 0);
            Assert.IsTrue(SaveService.SubmitRecord(158.2, 1));
            Assert.AreEqual(158.2f, SaveService.BestScore);
        }

        [Test]
        public void SameScoreMoreSuccessUpdatesOnlySuccessCount()
        {
            SaveService.SubmitRecord(180.0, 1);
            Assert.IsFalse(SaveService.SubmitRecord(180.0, 2), "점수가 같으면 새 점수 기록은 아니다");
            Assert.AreEqual(2, SaveService.BestSuccess, "최다 성공 수는 따로 갱신된다");
            Assert.AreEqual(180f, SaveService.BestScore);
        }

        [Test]
        public void ResetBestKeepsSoundSettings()
        {
            SaveService.SfxVolume = 0.3f;
            SaveService.Muted = true;
            SaveService.SubmitRecord(150, 2);
            SaveService.ResetBest();
            Assert.IsFalse(SaveService.HasBest);
            Assert.AreEqual(0, SaveService.BestSuccess);
            Assert.AreEqual(0.3f, SaveService.SfxVolume);
            Assert.IsTrue(SaveService.Muted);
        }
    }
}
