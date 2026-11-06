using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ProcurementHero.Core;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace ProcurementHero.EditorTools
{
    /// <summary>Tools/Procurement Hero/Validate Game: 데이터 · 참조 · 한글 글리프 · 성공 가능 조합을 확인한다.</summary>
    public static class GameValidator
    {
        public sealed class Report
        {
            public readonly List<string> Errors = new List<string>();
            public readonly List<string> Warnings = new List<string>();
            public readonly List<string> Info = new List<string>();
            public bool Ok => Errors.Count == 0;

            public override string ToString()
            {
                var lines = new List<string>();
                lines.AddRange(Errors.Select(e => "× " + e));
                lines.AddRange(Warnings.Select(w => "△ " + w));
                lines.AddRange(Info.Select(i => "○ " + i));
                return string.Join("\n", lines);
            }
        }

        [MenuItem("Tools/Procurement Hero/Validate Game", priority = 2)]
        static void ValidateMenu()
        {
            var report = Run();
            string head = report.Ok ? "검증 통과" : $"오류 {report.Errors.Count}개";
            if (report.Ok) Debug.Log("[ProcurementHero] " + head + "\n" + report);
            else Debug.LogError("[ProcurementHero] " + head + "\n" + report);
            EditorUtility.DisplayDialog("Procurement Hero · Validate", head + "\n\n" + Shorten(report.ToString()), "확인");
        }

        static string Shorten(string s) => s.Length > 1800 ? s.Substring(0, 1800) + "\n… (Console 참고)" : s;

        public static Report Run()
        {
            var report = new Report();
            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(GameSetup.DatabasePath);
            if (!database)
            {
                report.Errors.Add("GameDatabase가 없습니다. Setup Game을 먼저 실행하세요: " + GameSetup.DatabasePath);
                return report;
            }
            ValidateData(database, report);
            ValidateScene(database, report);
            ValidateGlyphs(database, report);
            return report;
        }

        public static void ValidateData(GameDatabase database, Report report)
        {
            if (database.missions.Count == 0) report.Errors.Add("미션이 없습니다.");
            var ids = new HashSet<string>();

            for (int i = 0; i < database.missions.Count; i++)
            {
                var m = database.missions[i];
                if (!m)
                {
                    report.Errors.Add($"미션 {i + 1} 참조가 비었습니다.");
                    continue;
                }
                string tag = $"[{m.name}]";
                if (string.IsNullOrWhiteSpace(m.id)) report.Errors.Add(tag + " 미션 ID가 비었습니다.");
                else if (!ids.Add(m.id)) report.Errors.Add(tag + " 중복 ID: " + m.id);
                if (string.IsNullOrWhiteSpace(m.title)) report.Errors.Add(tag + " 미션명이 비었습니다.");
                if (string.IsNullOrWhiteSpace(m.request)) report.Warnings.Add(tag + " 주민 요청 문구가 비었습니다.");
                if (string.IsNullOrWhiteSpace(m.itemNameA) || string.IsNullOrWhiteSpace(m.itemNameB)) report.Errors.Add(tag + " 물품 이름이 비었습니다.");
                if (m.budget <= 0) report.Errors.Add(tag + " 예산이 0 이하입니다.");
                if (m.deadlineDays <= 0) report.Errors.Add(tag + " 납기가 0 이하입니다.");
                if (m.targetPerformance <= 0 || m.targetPerformance > 100) report.Errors.Add(tag + " 목표 성능이 1~100 밖입니다.");
                if (string.IsNullOrWhiteSpace(m.reactionSuccess) || string.IsNullOrWhiteSpace(m.reactionPartial) || string.IsNullOrWhiteSpace(m.reactionFail))
                    report.Warnings.Add(tag + " 주민 반응 문구가 비었습니다.");

                for (int slot = 0; slot < 2; slot++)
                {
                    var list = m.Products(slot);
                    string slotTag = $"{tag} 물품 {(slot == 0 ? "A" : "B")}";
                    if (list == null || list.Count == 0)
                    {
                        report.Errors.Add(slotTag + " 제품이 없습니다.");
                        continue;
                    }
                    if (list.Count != 3) report.Warnings.Add($"{slotTag} 제품이 {list.Count}개입니다 (기획: 3개).");
                    var types = list.Where(p => p != null).Select(p => p.type).ToList();
                    if (types.Distinct().Count() != types.Count) report.Warnings.Add(slotTag + " 같은 제품 유형이 두 번 이상 있습니다.");

                    foreach (var p in list)
                    {
                        if (p == null)
                        {
                            report.Errors.Add(slotTag + " 빈 제품 항목이 있습니다.");
                            continue;
                        }
                        string pt = $"{slotTag} '{p.displayName}'";
                        if (string.IsNullOrWhiteSpace(p.id)) report.Errors.Add(pt + " ID가 비었습니다.");
                        else if (!ids.Add(p.id)) report.Errors.Add(pt + " 중복 ID: " + p.id);
                        if (string.IsNullOrWhiteSpace(p.displayName)) report.Errors.Add(slotTag + " 제품 이름이 비었습니다.");
                        if (p.purchaseCost <= 0) report.Errors.Add(pt + " 구매비가 0 이하입니다.");
                        if (p.maintenanceCost < 0) report.Errors.Add(pt + " 유지비가 음수입니다.");
                        if (p.performance < 0 || p.performance > 100) report.Errors.Add(pt + " 성능이 0~100 밖입니다.");
                        if (p.ecoScore < 0 || p.ecoScore > 100) report.Errors.Add(pt + " 환경점수가 0~100 밖입니다.");
                        if (p.deliveryDays < 1) report.Errors.Add(pt + " 배송일수가 1 미만입니다.");
                    }
                }

                if (m.productsA.Any(p => p == null) || m.productsB.Any(p => p == null) || m.budget <= 0) continue;
                var spec = m.ToSpec();
                int success = 0, combos = 0;
                foreach (var a in m.productsA)
                foreach (var b in m.productsB)
                {
                    combos++;
                    var e = Scoring.Evaluate(spec, a.ToSpec(), b.ToSpec());
                    if (e.Success) success++;
                }
                if (success == 0) report.Errors.Add(tag + " 예산 안에서 성공하는 조합이 없습니다.");
                else report.Info.Add($"{tag} {combos}개 조합 중 성공 {success}개");
            }
        }

        static void ValidateScene(GameDatabase database, Report report)
        {
            if (!File.Exists(GameSetup.ScenePath))
            {
                report.Errors.Add("게임 씬이 없습니다: " + GameSetup.ScenePath);
                return;
            }
            var scenes = EditorBuildSettings.scenes;
            if (scenes.Length == 0 || scenes[0].path != GameSetup.ScenePath || !scenes[0].enabled)
                report.Errors.Add("Build Settings 0번 씬이 게임 씬이 아닙니다.");

            // 열려 있는 씬을 건드리지 않도록 YAML에서 참조 GUID만 확인한다.
            string yaml = File.ReadAllText(GameSetup.ScenePath);
            string dbGuid = AssetDatabase.AssetPathToGUID(GameSetup.DatabasePath);
            string fontGuid = AssetDatabase.AssetPathToGUID(GameSetup.FontAssetPath);
            if (!yaml.Contains("guid: " + dbGuid)) report.Errors.Add("GameManager가 GameDatabase를 참조하지 않습니다.");
            if (!yaml.Contains("guid: " + fontGuid)) report.Errors.Add("GameManager가 한글 폰트 에셋을 참조하지 않습니다.");
            if (!yaml.Contains("m_Name: EventSystem")) report.Errors.Add("씬에 EventSystem이 없습니다.");
            if (!yaml.Contains("m_Name: Canvas")) report.Errors.Add("씬에 Canvas가 없습니다.");
            if (report.Errors.Count == 0) report.Info.Add("씬 · 참조 · 시작 씬 등록 확인");
        }

        static void ValidateGlyphs(GameDatabase database, Report report)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(GameSetup.FontAssetPath);
            if (!font)
            {
                report.Errors.Add("한글 폰트 에셋이 없습니다: " + GameSetup.FontAssetPath);
                return;
            }
            if (font.atlasPopulationMode == AtlasPopulationMode.Dynamic && !font.sourceFontFile)
                report.Errors.Add("동적 폰트 에셋에 원본 폰트 파일 참조가 없습니다.");

            var text = new System.Text.StringBuilder();
            foreach (var m in database.missions.Where(m => m))
            {
                text.Append(m.title).Append(m.facilityName).Append(m.request).Append(m.itemNameA).Append(m.itemNameB)
                    .Append(m.reactionSuccess).Append(m.reactionPartial).Append(m.reactionFail);
                foreach (var p in m.productsA.Concat(m.productsB).Where(p => p != null))
                    text.Append(p.displayName).Append(p.summary);
            }
            // 화면 문구는 런타임 · 코어 스크립트의 문자열 리터럴에서 모은다.
            foreach (var file in Directory.GetFiles(GameSetup.Root + "/Scripts", "*.cs", SearchOption.AllDirectories))
            {
                if (file.Replace('\\', '/').Contains("/Editor/")) continue;
                foreach (Match match in Regex.Matches(File.ReadAllText(file), "\"((?:[^\"\\\\]|\\\\.)*)\""))
                    text.Append(match.Groups[1].Value);
            }

            string all = Regex.Replace(text.ToString(), "<[^>]+>", "");
            var unique = new string(all.Where(c => !char.IsControl(c) && c != '{' && c != '}').Distinct().ToArray());
            if (!font.HasCharacters(unique, out uint[] missing, true, true))
            {
                var real = missing.Select(u => (char)u).Where(c => c != '\\').Distinct().ToList();
                if (real.Count > 0)
                    report.Errors.Add($"폰트에 없는 글자 {real.Count}개: " + new string(real.Take(40).ToArray()));
            }
            else
            {
                report.Info.Add($"화면 문구 글자 {unique.Length}종 모두 폰트에 있음");
            }
        }
    }
}
