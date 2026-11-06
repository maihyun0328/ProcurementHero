using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NUnit.Framework;
using ProcurementHero.EditorTools;

namespace ProcurementHero.Tests
{
    /// <summary>시스템 임시 폴더 안에서만 시험한다. 실제 Builds 폴더는 건드리지 않는다.</summary>
    public class BuildPublisherTests
    {
        string root, finalDir, zipPath;
        static readonly BuildPublisher.ExtraFile License = new BuildPublisher.ExtraFile("LICENSE.txt", "ofl");
        static readonly BuildPublisher.ExtraFile Readme = new BuildPublisher.ExtraFile("README_KO.txt", "readme");

        [SetUp]
        public void CreateSandbox()
        {
            root = Path.Combine(Path.GetTempPath(), "PH_BuildPublisherTest_" + Guid.NewGuid().ToString("N"));
            finalDir = Path.Combine(root, "Windows", "ProcurementHero");
            zipPath = Path.Combine(root, "ProcurementHero_Windows.zip");
            Directory.CreateDirectory(Path.Combine(root, "Windows"));
            BuildPublisher.FaultHook = null;
        }

        [TearDown]
        public void RemoveSandbox()
        {
            BuildPublisher.FaultHook = null;
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        void MakePreviousBuild(bool withUserFile = false)
        {
            FakeBuild(finalDir, "old");
            if (withUserFile) File.WriteAllText(Path.Combine(finalDir, "my_notes.txt"), "user");
            File.WriteAllText(zipPath, "old zip");
        }

        static string FakeBuild(string dir, string marker)
        {
            Directory.CreateDirectory(Path.Combine(dir, "ProcurementHero_Data"));
            File.WriteAllText(Path.Combine(dir, "ProcurementHero.exe"), marker);
            File.WriteAllText(Path.Combine(dir, "UnityPlayer.dll"), "dll");
            File.WriteAllText(Path.Combine(dir, "ProcurementHero_Data", "data.bin"), marker);
            Directory.CreateDirectory(Path.Combine(dir, "조달 히어로_BurstDebugInformation_DoNotShip"));
            File.WriteAllText(Path.Combine(dir, "조달 히어로_BurstDebugInformation_DoNotShip", "x.txt"), "debug");
            return null;
        }

        bool Publish(Func<string, string> build, out string message) =>
            BuildPublisher.Publish(finalDir, zipPath, "ProcurementHero.exe", License, Readme, build, out message);

        void AssertOldKept()
        {
            Assert.AreEqual("old", File.ReadAllText(Path.Combine(finalDir, "ProcurementHero.exe")), "이전 실행 폴더가 남아야 한다");
            Assert.AreEqual("old zip", File.ReadAllText(zipPath), "이전 ZIP이 남아야 한다");
        }

        void AssertNoWorkFolder()
        {
            Assert.IsEmpty(Directory.GetDirectories(Path.Combine(root, "Windows"), ".build_*"), "임시 작업 폴더가 남으면 안 된다");
        }

        [Test]
        public void SuccessReplacesFolderAndZip()
        {
            MakePreviousBuild();
            Assert.IsTrue(Publish(d => FakeBuild(d, "new"), out string message), message);
            Assert.AreEqual("new", File.ReadAllText(Path.Combine(finalDir, "ProcurementHero.exe")));
            Assert.IsTrue(File.Exists(Path.Combine(finalDir, "LICENSE.txt")));
            Assert.IsFalse(Directory.GetDirectories(finalDir, "*_BurstDebugInformation_DoNotShip").Any());
            using (var zip = ZipFile.OpenRead(zipPath))
            {
                Assert.IsTrue(zip.Entries.Any(e => e.FullName == "ProcurementHero/ProcurementHero.exe"));
                Assert.IsTrue(zip.Entries.Any(e => e.FullName == "ProcurementHero/LICENSE.txt"));
                Assert.IsFalse(zip.Entries.Any(e => e.FullName.Contains("BurstDebug")));
            }
            AssertNoWorkFolder();
        }

        [Test]
        public void SuccessWithoutPreviousBuild()
        {
            Assert.IsTrue(Publish(d => FakeBuild(d, "new"), out string message), message);
            Assert.IsTrue(File.Exists(zipPath));
            AssertNoWorkFolder();
        }

        [Test]
        public void BuildFailureKeepsPreviousResult()
        {
            MakePreviousBuild();
            Assert.IsFalse(Publish(d => "빌드 실패: Failed", out string message));
            StringAssert.DoesNotContain("성공", message);
            AssertOldKept();
            AssertNoWorkFolder();
        }

        [Test]
        public void MissingRequiredFileKeepsPreviousResult()
        {
            MakePreviousBuild();
            Assert.IsFalse(Publish(d => { FakeBuild(d, "new"); File.Delete(Path.Combine(d, "UnityPlayer.dll")); return null; }, out string message));
            StringAssert.Contains("UnityPlayer.dll", message);
            AssertOldKept();
            AssertNoWorkFolder();
        }

        [TestCase("after-build")]
        [TestCase("before-zip")]
        [TestCase("after-zip")]
        [TestCase("swap-dir")]
        [TestCase("swap-zip")]
        public void FaultAtAnyStageKeepsOrRestoresPreviousResult(string stage)
        {
            MakePreviousBuild();
            BuildPublisher.FaultHook = s => { if (s == stage) throw new IOException("시험용 실패 " + s); };
            Assert.IsFalse(Publish(d => FakeBuild(d, "new"), out string message));
            StringAssert.Contains("시험용 실패", message);
            AssertOldKept();
            AssertNoWorkFolder();
        }

        [Test]
        public void ExceptionInsideBuildStepKeepsPreviousResult()
        {
            MakePreviousBuild();
            Assert.IsFalse(Publish(d => throw new InvalidOperationException("빌드 중 예외"), out _));
            AssertOldKept();
            AssertNoWorkFolder();
        }

        [Test]
        public void PreviousFolderWithUserFilesIsKeptNotDeleted()
        {
            MakePreviousBuild(withUserFile: true);
            Assert.IsTrue(Publish(d => FakeBuild(d, "new"), out string message), message);
            var kept = Directory.GetDirectories(Path.Combine(root, "Windows"), "ProcurementHero_previous_*");
            Assert.AreEqual(1, kept.Length, message);
            Assert.AreEqual("user", File.ReadAllText(Path.Combine(kept[0], "my_notes.txt")));
            Assert.AreEqual("new", File.ReadAllText(Path.Combine(finalDir, "ProcurementHero.exe")));
            AssertNoWorkFolder();
        }
    }
}
