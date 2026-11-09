using System.Collections.Generic;
using System.Linq;
using ProcurementHero.Core.Field;
using ProcurementHero.Field;
using UnityEditor;
using UnityEngine;

namespace ProcurementHero.EditorTools
{
    /// <summary>현장 수첩 데이터 검증: 참조 · 비용 · 성공 조합 · 별 3개 조합 · 관찰 지점 도달 가능성.</summary>
    public static class FieldValidator
    {
        [MenuItem("Tools/Procurement Hero/Validate Field Notebook", priority = 22)]
        static void ValidateMenu()
        {
            var report = Run();
            string head = report.Ok ? "검증 통과" : $"오류 {report.Errors.Count}개";
            Debug.Log("[ProcurementHero] Field " + head + "\n" + report);
            EditorUtility.DisplayDialog("Procurement Hero · Validate Field", head + "\n\n" + report, "확인");
        }

        public static GameValidator.Report Run()
        {
            var report = new GameValidator.Report();
            var database = AssetDatabase.LoadAssetAtPath<FieldDatabase>(FieldSetup.DatabasePath);
            if (!database)
            {
                report.Errors.Add("FieldDatabase가 없습니다. Setup Field Notebook을 먼저 실행하세요.");
                return report;
            }
            Validate(database, report);
            if (!System.IO.File.Exists(FieldSetup.ScenePath)) report.Errors.Add("현장 수첩 씬이 없습니다: " + FieldSetup.ScenePath);
            return report;
        }

        public static void Validate(FieldDatabase database, GameValidator.Report report)
        {
            if (database.missions.Count == 0) report.Errors.Add("현장 미션이 없습니다.");
            var missionIds = new HashSet<string>();
            foreach (var m in database.missions)
            {
                if (!m)
                {
                    report.Errors.Add("빈 현장 미션 참조가 있습니다.");
                    continue;
                }
                string tag = $"[{m.name}]";
                if (!missionIds.Add(m.id)) report.Errors.Add(tag + " 중복 미션 ID " + m.id);
                if (m.budget <= 0) report.Errors.Add(tag + " 예산이 0 이하입니다.");
                if (!m.problems.Any(p => p.required)) report.Errors.Add(tag + " 필수 문제가 없습니다.");
                Unique(tag + " 문제", m.problems.Select(p => p.id), report);
                Unique(tag + " 물품", m.items.Select(i => i.id), report);
                Unique(tag + " 지점", m.spots.Select(s => s.id), report);
                foreach (var o in m.obstacles.Where(o => !string.IsNullOrEmpty(o.clearedBy)))
                    if (m.Problem(o.clearedBy) == null) report.Errors.Add($"{tag} 장애물이 없는 문제 '{o.clearedBy}'로 사라지도록 되어 있습니다.");
                foreach (var r in m.reactions)
                {
                    var spot = m.Spot(r.spotId);
                    var option = spot?.options.FirstOrDefault(o => o.itemId == r.itemId);
                    if (option == null || !option.solves.Contains(r.problemId))
                        report.Errors.Add($"{tag} 주민 반응({r.spotId}/{r.itemId}/{r.problemId})이 실제로 그 문제를 해결하는 배치가 아닙니다.");
                    else if (r.moveResident && !ResidentFits(m, r.residentPosition, r.spotId))
                        report.Errors.Add($"{tag} 주민 반응({r.spotId}/{r.itemId}/{r.problemId})의 이동 위치가 건물·벽·물품과 겹칩니다.");
                }
                foreach (var i in m.items)
                    if (i.cost <= 0) report.Errors.Add($"{tag} 물품 '{i.displayName}' 비용이 0 이하입니다.");
                foreach (var s in m.spots)
                {
                    if (s.options.Count == 0) report.Errors.Add($"{tag} 지점 '{s.displayName}'에 설치 가능한 물품이 없습니다.");
                    foreach (var o in s.options)
                    {
                        if (m.Item(o.itemId) == null) report.Errors.Add($"{tag} 지점 '{s.displayName}'이 없는 물품 '{o.itemId}'을 참조합니다.");
                        foreach (var id in o.solves)
                            if (m.Problem(id) == null) report.Errors.Add($"{tag} 지점 '{s.displayName}'이 없는 문제 '{id}'를 참조합니다.");
                    }
                }

                var spec = m.ToSpec();
                var all = FieldRules.AllPlans(spec).Select(p => FieldRules.Evaluate(spec, p)).ToList();
                var valid = all.Where(e => e.Assignments.Count > 0 && e.WithinBudget).ToList();
                int success = valid.Count(e => e.Success);
                int three = valid.Count(e => e.Stars == 3);
                if (success == 0) report.Errors.Add(tag + " 예산 안에서 성공(별 2개 이상)하는 조합이 없습니다.");
                if (three == 0) report.Errors.Add(tag + " 예산 안에서 별 3개 조합이 없습니다.");
                report.Info.Add($"{tag} 조합 {all.Count}개 · 확정 가능 {valid.Count} · 성공 {success} · 별 3개 {three}" + (m.draftNumbers ? " (개발 초안 수치)" : ""));

                // 계획 단계(상황 전, 깊은 물 없음)에는 모든 관찰 지점에 걸어서 갈 수 있어야 한다.
                foreach (var p in m.problems)
                    if (!Reachable(m, p.observePoint, FieldWorld.ObserveRange * 0.6f))
                        report.Errors.Add($"{tag} '{p.displayName}' 관찰 지점에 걸어서 갈 수 없습니다.");
                // 상황 뒤(모든 깊은 물이 막힌 최악의 경우)에도 재확인·결과 보기를 위해 모든 문제를 물 밖에서 살펴볼 수 있어야 한다.
                foreach (var p in m.problems)
                    if (!Reachable(m, p.observePoint, FieldWorld.ObserveRange * 0.9f, deepWater: true))
                        report.Errors.Add($"{tag} '{p.displayName}'을 깊은 물이 찬 뒤에는 물 밖에서 살펴볼 수 없습니다.");
                // 깊은 물은 주민·이용객 대기 자리·설치 자리를 덮지 않는다(물속에 서 있거나 물품을 놓는 모습이 생기지 않게).
                foreach (var o in m.obstacles.Where(o => o.kind == FieldObstacleKind.Puddle))
                {
                    bool Covers(Vector2 at, float r) => Mathf.Abs(at.x - o.center.x) < o.size.x / 2 + r && Mathf.Abs(at.y - o.center.y) < o.size.y / 2 + r;
                    foreach (var p in m.problems)
                        if (Covers(p.residentPosition + new Vector2(0, -0.45f), 0.3f)) report.Errors.Add($"{tag} '{p.displayName}' 주민이 깊은 물 안에 서 있습니다.");
                    foreach (var r in m.crowdRoutes)
                        if (Covers(r.waitAt + new Vector2(0, -0.45f), 0.3f)) report.Errors.Add($"{tag} 이용객 {r.visitor}의 대기 자리가 깊은 물 안입니다.");
                    foreach (var sp in m.spots)
                        if (Covers(sp.position, 0.4f)) report.Errors.Add($"{tag} 설치 자리 '{sp.displayName}'가 깊은 물 안입니다.");
                }
                if (!Walkable(m, m.playerStart)) report.Errors.Add(tag + " 시작 위치가 막혀 있습니다.");
            }
        }

        static void Unique(string label, IEnumerable<string> ids, GameValidator.Report report)
        {
            var list = ids.ToList();
            foreach (var dup in list.GroupBy(x => x).Where(g => g.Count() > 1 || string.IsNullOrEmpty(g.Key)))
                report.Errors.Add($"{label} ID 문제: '{dup.Key}'");
        }

        /// <summary>옮겨 간 주민(발 반지름 0.3)이 건물·벽·나무·울타리나 다른 설치 자리의 물품과 겹치지 않는지. 해결한 물품의 자리(ownSpotId)는 제외한다(그늘막 아래처럼 함께 있어야 하므로).</summary>
        public static bool ResidentFits(FieldMissionData m, Vector2 p, string ownSpotId = null)
        {
            var foot = p + new Vector2(0, -0.45f);
            if (Mathf.Abs(foot.x) > FieldWorld.HalfWidth - 0.3f || Mathf.Abs(foot.y) > FieldWorld.HalfHeight - 0.3f) return false;
            foreach (var o in m.obstacles)
            {
                if (o.kind == FieldObstacleKind.Puddle) continue;
                var half = o.size / 2 + new Vector2(0.3f, 0.3f);
                if (Mathf.Abs(foot.x - o.center.x) < half.x && Mathf.Abs(foot.y - o.center.y) < half.y) return false;
            }
            foreach (var s in m.spots)
                if (s.id != ownSpotId && Mathf.Abs(foot.x - s.position.x) < 0.6f && Mathf.Abs(foot.y - s.position.y) < 0.3f) return false;
            return true;
        }

        const float Cell = 0.25f;

        /// <summary>플레이어 몸 반지름을 고려해 지나갈 수 있는 위치인지. deepWater면 깊은 물(Puddle)도 막힌 것으로 본다(상황 뒤 최악의 경우).</summary>
        public static bool Walkable(FieldMissionData m, Vector2 p, bool deepWater = false)
        {
            float r = FieldWorld.PlayerRadius + 0.02f;
            var foot = p + new Vector2(0, -0.1f);
            if (Mathf.Abs(foot.x) > FieldWorld.HalfWidth - r || Mathf.Abs(foot.y) > FieldWorld.HalfHeight - r) return false;
            foreach (var o in m.obstacles)
            {
                if (o.kind == FieldObstacleKind.Puddle && !deepWater) continue;
                var half = o.size / 2 + new Vector2(r, r);
                if (Mathf.Abs(foot.x - o.center.x) < half.x && Mathf.Abs(foot.y - o.center.y) < half.y) return false;
            }
            return true;
        }

        /// <summary>시작 위치에서 격자 탐색으로 target 반경 안의 칸에 닿을 수 있는지.</summary>
        public static bool Reachable(FieldMissionData m, Vector2 target, float within, bool deepWater = false)
        {
            int w = Mathf.RoundToInt(FieldWorld.HalfWidth * 2 / Cell), h = Mathf.RoundToInt(FieldWorld.HalfHeight * 2 / Cell);
            Vector2 Pos(int x, int y) => new Vector2(-FieldWorld.HalfWidth + (x + 0.5f) * Cell, -FieldWorld.HalfHeight + (y + 0.5f) * Cell);
            int sx = Mathf.Clamp(Mathf.FloorToInt((m.playerStart.x + FieldWorld.HalfWidth) / Cell), 0, w - 1);
            int sy = Mathf.Clamp(Mathf.FloorToInt((m.playerStart.y + FieldWorld.HalfHeight) / Cell), 0, h - 1);
            var seen = new bool[w, h];
            var queue = new Queue<(int, int)>();
            queue.Enqueue((sx, sy));
            seen[sx, sy] = true;
            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();
                if (Vector2.Distance(Pos(x, y), target) <= within) return true;
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h || seen[nx, ny]) continue;
                    seen[nx, ny] = true;
                    if (Walkable(m, Pos(nx, ny), deepWater)) queue.Enqueue((nx, ny));
                }
            }
            return false;
        }
    }
}
