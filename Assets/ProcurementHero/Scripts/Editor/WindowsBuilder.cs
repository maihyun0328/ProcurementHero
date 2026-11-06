using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ProcurementHero.EditorTools
{
    /// <summary>Tools/Procurement Hero/Build Windows: 검증 후 Windows 64비트 빌드와 제출용 ZIP을 만든다.</summary>
    public static class WindowsBuilder
    {
        public const string OutputFolder = "Builds/Windows/ProcurementHero";
        public const string ExeName = "ProcurementHero.exe";
        public const string ZipPath = "Builds/ProcurementHero_Windows.zip";

        [MenuItem("Tools/Procurement Hero/Build Windows", priority = 3)]
        static void BuildMenu()
        {
            string message = Build(out bool ok);
            if (ok) Debug.Log("[ProcurementHero] " + message);
            else Debug.LogError("[ProcurementHero] " + message);
            EditorUtility.DisplayDialog("Procurement Hero · Build Windows", message, "확인");
            if (ok) EditorUtility.RevealInFinder(Path.GetFullPath(OutputFolder));
        }

        public static string Build(out bool ok)
        {
            ok = false;
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
                return "Windows Build Support 모듈이 이 Unity(" + Application.unityVersion + ")에 설치되어 있지 않습니다.\n" +
                       "Unity Hub → Installs → " + Application.unityVersion + " → Add modules → 'Windows Build Support (Mono)'를 추가한 뒤 다시 실행하세요.";

            var validation = GameValidator.Run();
            if (!validation.Ok)
                return "검증 오류가 있어 빌드를 멈췄습니다.\n" + validation;

            var registered = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (registered.Length == 0 || registered[0] != GameSetup.ScenePath)
                return "시작 씬이 등록되지 않았습니다. Setup Game을 먼저 실행하세요.";
            // 게임은 씬 하나로 동작한다. Build Settings의 다른 씬(예: 템플릿 SampleScene)은 목록에 남겨 두고 빌드에서만 뺀다.
            var scenes = new[] { GameSetup.ScenePath };

            string licensePath = Path.GetFullPath(GameSetup.FontFolder + "/OFL.txt");
            if (!File.Exists(licensePath))
                return "글꼴 라이선스 파일이 없어 빌드를 멈췄습니다: " + licensePath;

            string summaryText = "";
            // 새 빌드는 임시 폴더에 만들고, 모든 단계가 성공한 뒤에만 기존 결과와 바꾼다.
            ok = BuildPublisher.Publish(
                Path.GetFullPath(OutputFolder),
                Path.GetFullPath(ZipPath),
                ExeName,
                new BuildPublisher.ExtraFile("LICENSE_Pretendard_OFL.txt", File.ReadAllText(licensePath)),
                new BuildPublisher.ExtraFile("README_KO.txt",
                    "조달 히어로: 마을을 바꾸는 선택 (Windows 64비트)\r\n\r\n" +
                    "1. 이 폴더 전체를 그대로 둔 채 ProcurementHero.exe를 실행하세요.\r\n" +
                    "   (exe 파일만 따로 옮기면 실행되지 않습니다. ProcurementHero_Data 폴더 등이 함께 있어야 합니다.)\r\n" +
                    "2. 마우스 클릭으로 진행하며, 키보드(방향키 · Enter · Esc · Space)도 쓸 수 있습니다.\r\n\r\n" +
                    "공공구매의 가치를 알리기 위한 창작 게임입니다. 실제 계약 절차·평가 방식·제품 성능을 재현하지 않습니다.\r\n" +
                    "글꼴: Pretendard (SIL Open Font License 1.1) — LICENSE_Pretendard_OFL.txt 참고\r\n"),
                stagingDir =>
                {
                    BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                    {
                        scenes = scenes,
                        locationPathName = Path.Combine(stagingDir, ExeName),
                        target = BuildTarget.StandaloneWindows64,
                        targetGroup = BuildTargetGroup.Standalone,
                        options = BuildOptions.None
                    });
                    var summary = report.summary;
                    if (summary.result != BuildResult.Succeeded)
                    {
                        var errors = report.steps.SelectMany(s => s.messages)
                            .Where(m => m.type == LogType.Error || m.type == LogType.Exception)
                            .Select(m => m.content).Take(10);
                        return $"빌드 실패: {summary.result}, 오류 {summary.totalErrors}개\n" + string.Join("\n", errors);
                    }
                    summaryText = $"{summary.totalTime.TotalSeconds:0}초, {summary.totalSize / (1024f * 1024f):0.0} MB";
                    return null;
                },
                out string message);

            if (!ok) return message;
            long size = new FileInfo(Path.GetFullPath(ZipPath)).Length;
            return $"빌드 성공 ({summaryText})\n" +
                   $"실행 폴더: {Path.GetFullPath(OutputFolder)}\n제출용 ZIP: {Path.GetFullPath(ZipPath)} ({size / (1024f * 1024f):0.0} MB)\n" +
                   message +
                   "※ Mac에서 만든 빌드는 Windows PC에서 직접 실행해 확인해야 합니다.";
        }
    }
}
