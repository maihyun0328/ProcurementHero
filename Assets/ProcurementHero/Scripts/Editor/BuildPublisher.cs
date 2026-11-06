using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace ProcurementHero.EditorTools
{
    /// <summary>
    /// 빌드 결과를 안전하게 바꿔 끼운다.
    /// 1) 이번 실행 전용 작업 폴더(.build_시각)에 새 빌드 · 배포 파일 · ZIP을 만들고
    /// 2) 모두 성공하면 기존 실행 폴더와 ZIP을 작업 폴더로 옮긴 뒤 새 결과를 제자리에 놓는다.
    /// 어느 단계에서 실패해도 기존 실행 폴더와 ZIP은 원래 자리에 남거나 되돌려진다.
    /// 지우는 것은 이번 실행이 만든 작업 폴더뿐이며, 빌드 결과가 아닌 파일이 있던 이전 폴더는 지우지 않고 보관한다.
    /// </summary>
    public static class BuildPublisher
    {
        public readonly struct ExtraFile
        {
            public readonly string Name;
            public readonly string Contents;

            public ExtraFile(string name, string contents)
            {
                Name = name;
                Contents = contents;
            }
        }

        /// <summary>테스트 전용: 지정한 단계에서 예외를 던져 실패 경로를 시험한다. 평소에는 null.</summary>
        public static Action<string> FaultHook;

        const string WorkPrefix = ".build_";

        /// <param name="build">stagingDir에 빌드를 만든다. 성공이면 null, 실패면 오류 문구를 돌려준다.</param>
        public static bool Publish(string finalDir, string zipPath, string exeName, ExtraFile license, ExtraFile readme,
            Func<string, string> build, out string message)
        {
            finalDir = Path.GetFullPath(finalDir).TrimEnd(Path.DirectorySeparatorChar);
            zipPath = Path.GetFullPath(zipPath);
            string parent = Path.GetDirectoryName(finalDir);
            string folderName = Path.GetFileName(finalDir);
            string workRoot = Path.Combine(parent, WorkPrefix + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
            string staging = Path.Combine(workRoot, folderName);
            string tempZip = Path.Combine(workRoot, Path.GetFileName(zipPath));
            string backupDir = Path.Combine(workRoot, "previous_" + folderName);
            string backupZip = Path.Combine(workRoot, "previous_" + Path.GetFileName(zipPath));
            const string kept = "\n이전 실행 폴더와 제출용 ZIP은 그대로 두었습니다.";

            // ── 1단계: 작업 폴더에서 준비 (기존 결과는 건드리지 않음)
            try
            {
                Directory.CreateDirectory(staging);
                string error = build(staging);
                if (error != null)
                {
                    TryDeleteWork(workRoot);
                    message = error + kept;
                    return false;
                }
                Fault("after-build");

                // Unity가 만드는 Burst 디버그 폴더는 배포하지 않는다(이름은 제품명을 따른다).
                foreach (var dir in Directory.GetDirectories(staging, "*_BurstDebugInformation_DoNotShip"))
                    Directory.Delete(dir, true);

                // OFL은 글꼴을 함께 배포할 때 라이선스 사본을 동봉하도록 요구한다.
                File.WriteAllText(Path.Combine(staging, license.Name), license.Contents);
                File.WriteAllText(Path.Combine(staging, readme.Name), readme.Contents);

                string missing = MissingRequired(staging, exeName, license.Name, readme.Name);
                if (missing != null)
                {
                    TryDeleteWork(workRoot);
                    message = "빌드 결과에 필요한 파일이 없습니다: " + missing + kept;
                    return false;
                }

                Fault("before-zip");
                MakeZip(staging, tempZip, folderName);
                Fault("after-zip");
            }
            catch (Exception e)
            {
                TryDeleteWork(workRoot);
                message = "빌드 결과를 준비하다 실패했습니다: " + e.Message + kept;
                return false;
            }

            // ── 2단계: 교체. 실패하면 되돌린다.
            bool hadDir = Directory.Exists(finalDir);
            bool hadZip = File.Exists(zipPath);
            bool newDirPlaced = false;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(zipPath));
                if (hadDir) Directory.Move(finalDir, backupDir);
                Fault("swap-dir");
                Directory.Move(staging, finalDir);
                newDirPlaced = true;
                if (hadZip) File.Move(zipPath, backupZip);
                Fault("swap-zip");
                File.Move(tempZip, zipPath);
            }
            catch (Exception e)
            {
                string restoreError = Restore(finalDir, zipPath, staging, backupDir, backupZip, newDirPlaced);
                if (restoreError == null)
                {
                    TryDeleteWork(workRoot);
                    message = "새 빌드로 교체하다 실패해 이전 결과로 되돌렸습니다: " + e.Message + kept;
                }
                else
                {
                    // 되돌리기마저 실패하면 아무것도 지우지 않고 위치를 알려 준다.
                    message = "새 빌드로 교체하다 실패했고 되돌리기도 끝내지 못했습니다: " + e.Message + " / " + restoreError +
                              "\n이전 결과는 다음 폴더에 남아 있습니다: " + workRoot;
                }
                return false;
            }

            // ── 3단계: 정리. 이전 폴더가 순수한 빌드 결과일 때만 지운다.
            message = "";
            if (hadDir && !OnlyBuildOutput(backupDir, exeName, license.Name, readme.Name))
            {
                string keep = Path.Combine(parent, folderName + "_previous_" + Path.GetFileName(workRoot).Substring(WorkPrefix.Length));
                try
                {
                    Directory.Move(backupDir, keep);
                    message = "이전 실행 폴더에 빌드 결과가 아닌 파일이 있어 지우지 않고 보관했습니다: " + keep + "\n";
                }
                catch (Exception e)
                {
                    message = "이전 실행 폴더를 보관 위치로 옮기지 못해 작업 폴더에 남겨 두었습니다: " + workRoot + " (" + e.Message + ")\n";
                    return true;
                }
            }
            TryDeleteWork(workRoot);
            return true;
        }

        static string Restore(string finalDir, string zipPath, string staging, string backupDir, string backupZip, bool newDirPlaced)
        {
            try
            {
                if (newDirPlaced && Directory.Exists(finalDir)) Directory.Move(finalDir, staging);
                if (Directory.Exists(backupDir) && !Directory.Exists(finalDir)) Directory.Move(backupDir, finalDir);
                if (File.Exists(backupZip) && !File.Exists(zipPath)) File.Move(backupZip, zipPath);
                return null;
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }

        static void Fault(string stage) => FaultHook?.Invoke(stage);

        static string MissingRequired(string dir, string exeName, params string[] extras)
        {
            string dataDir = Path.GetFileNameWithoutExtension(exeName) + "_Data";
            if (!File.Exists(Path.Combine(dir, exeName))) return exeName;
            if (!File.Exists(Path.Combine(dir, "UnityPlayer.dll"))) return "UnityPlayer.dll";
            if (!Directory.Exists(Path.Combine(dir, dataDir))) return dataDir;
            foreach (var extra in extras)
                if (!File.Exists(Path.Combine(dir, extra))) return extra;
            return null;
        }

        /// <summary>폴더 맨 위 항목이 모두 Unity Windows 빌드나 이 도구가 만든 파일인지 본다.</summary>
        static bool OnlyBuildOutput(string dir, string exeName, params string[] extras)
        {
            string stem = Path.GetFileNameWithoutExtension(exeName);
            var known = new[]
            {
                exeName, stem + "_Data", "UnityPlayer.dll", "UnityCrashHandler64.exe", "MonoBleedingEdge", "D3D12",
                "WinPixEventRuntime.dll", "baselib.dll", "GameAssembly.dll", "README_실행방법.txt", ".DS_Store"
            }.Concat(extras);
            var names = Directory.GetFileSystemEntries(dir).Select(Path.GetFileName);
            return names.All(n => known.Contains(n) || n.EndsWith("_BurstDebugInformation_DoNotShip", StringComparison.Ordinal));
        }

        static void TryDeleteWork(string workRoot)
        {
            // 이번 실행이 만든 작업 폴더만 지운다.
            if (!Path.GetFileName(workRoot).StartsWith(WorkPrefix, StringComparison.Ordinal)) return;
            try
            {
                if (Directory.Exists(workRoot)) Directory.Delete(workRoot, true);
            }
            catch
            {
                // 남아도 다음 빌드에 영향이 없다.
            }
        }

        /// <summary>실행에 필요한 폴더 전체를 묶고, 배포하면 안 되는 디버그 폴더만 뺀다.</summary>
        static void MakeZip(string sourceDir, string zipPath, string rootName)
        {
            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
                {
                    string relative = file.Substring(sourceDir.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    if (relative.Contains("_BurstDebugInformation_DoNotShip")) continue;
                    archive.CreateEntryFromFile(file, rootName + "/" + relative.Replace('\\', '/'), CompressionLevel.Optimal);
                }
            }
        }
    }
}
