using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProcurementHero.Core.Field;
using UnityEngine;

namespace ProcurementHero.Field
{
    /// <summary>
    /// 진행 저장 데이터(JSON). 씬 오브젝트·위치·코루틴은 저장하지 않고, 배정·관찰·진행 단계만 저장한다.
    /// 별·비용·해결 구역은 저장하지 않고 불러올 때 FieldRules로 다시 계산한다.
    /// 최고 기록·소리 설정(PlayerPrefs)과는 따로 둔다.
    /// </summary>
    [Serializable]
    public class FieldSaveData
    {
        public int version;
        public string currentMission;
        public string phase;
        public List<FieldSavePick> draft = new List<FieldSavePick>();
        public string selectedSpot;
        public List<FieldSaveMission> missions = new List<FieldSaveMission>();
    }

    [Serializable]
    public class FieldSavePick
    {
        public string spot;
        public string item;
    }

    [Serializable]
    public class FieldSaveMission
    {
        public string id;
        public bool confirmed;
        public List<FieldSavePick> picks = new List<FieldSavePick>();
        public List<string> observed = new List<string>();
    }

    /// <summary>
    /// 이어하기 단계. Explore: 확정 전(탐색·수첩 편집), Replan: 확정 결과가 있는 현장을 다시 계획하는 중,
    /// Review: 확정했고 설치가 끝난 재확인(설치 도중 종료도 여기로), Complete: 엔딩까지 봄.
    /// </summary>
    public enum FieldSavePhase { Explore, Replan, Review, Complete }

    public interface IFieldSaveStore
    {
        string Read();
        void Write(string text);
        void Delete();
    }

    /// <summary>테스트와 플레이 검증용 메모리 저장소.</summary>
    public sealed class MemoryFieldSaveStore : IFieldSaveStore
    {
        public string Text;
        public int Writes;
        public string Read() => Text;
        public void Write(string text) { Text = text; Writes++; }
        public void Delete() => Text = null;
    }

    /// <summary>
    /// 파일 저장. 임시 파일에 다 쓴 뒤 교체하고(이전 파일은 .bak으로 남김), 본 파일이 깨졌으면 .bak을 읽는다.
    /// 쓰는 도중 꺼져도 이전 저장이 남는다.
    /// </summary>
    public sealed class FileFieldSaveStore : IFieldSaveStore
    {
        public readonly string Path;
        public FileFieldSaveStore(string path) { Path = path; }
        string Temp => Path + ".tmp";
        string Backup => Path + ".bak";

        public string Read()
        {
            foreach (var p in new[] { Path, Backup })
            {
                try
                {
                    if (!File.Exists(p)) continue;
                    var text = File.ReadAllText(p);
                    if (FieldSave.Parse(text) != null) return text;
                    Debug.LogWarning("[ProcurementHero] 진행 저장을 읽을 수 없어 건너뜁니다: " + p);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[ProcurementHero] 진행 저장 읽기 실패: " + p + " · " + e.Message);
                }
            }
            return null;
        }

        public void Write(string text)
        {
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
                File.WriteAllText(Temp, text);
                if (File.Exists(Path)) File.Replace(Temp, Path, Backup, true);
                else File.Move(Temp, Path);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ProcurementHero] 진행 저장 실패(이전 저장은 그대로): " + e.Message);
            }
        }

        public void Delete()
        {
            foreach (var p in new[] { Path, Backup, Temp })
            {
                try { if (File.Exists(p)) File.Delete(p); }
                catch (Exception e) { Debug.LogWarning("[ProcurementHero] 진행 저장 삭제 실패: " + p + " · " + e.Message); }
            }
        }
    }

    /// <summary>불러와 검증한 진행. 잘못된 ID·예산 초과·순서가 맞지 않는 결과는 버리거나 확정 전 계획으로 낮춘다.</summary>
    public sealed class FieldRestored
    {
        public FieldEvaluation[] Results;
        public HashSet<string>[] Observed;
        public int Current;
        public FieldSavePhase Phase;
        public FieldPlan Draft = new FieldPlan();
        public string SelectedSpot;
        public readonly List<string> Warnings = new List<string>();

        /// <summary>이어할 만한 진행이 있는지(관찰·계획·확정 중 하나라도).</summary>
        public bool HasProgress => Results.Any(r => r != null) || Observed.Any(o => o.Count > 0) || Draft.Assignments.Count > 0;
    }

    public static class FieldSave
    {
        public const int Version = 1;

        /// <summary>
        /// 기본 저장소. 에디터 플레이는 프로젝트 Library 아래(커밋 안 됨)에, 빌드는 사용자 데이터 폴더에 저장해
        /// 에디터 시험이 실제 플레이 저장을 덮어쓰지 않게 한다. 테스트는 메모리 저장소로 바꿔 쓴다.
        /// </summary>
        public static IFieldSaveStore Store = new FileFieldSaveStore(DefaultPath());

        public static string DefaultPath()
        {
#if UNITY_EDITOR
            return System.IO.Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Library", "ProcurementHero", "field_progress.json");
#else
            return System.IO.Path.Combine(Application.persistentDataPath, "field_progress.json");
#endif
        }

        public static FieldSaveData Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                var data = JsonUtility.FromJson<FieldSaveData>(json);
                return data != null && data.version > 0 ? data : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static string ToJson(FieldSaveData data) => JsonUtility.ToJson(data, true);

        static List<FieldSavePick> Picks(IReadOnlyDictionary<string, string> assignments) =>
            assignments.OrderBy(a => a.Key).Select(a => new FieldSavePick { spot = a.Key, item = a.Value }).ToList();

        /// <summary>지금 진행을 저장 데이터로 만든다. 확정 결과는 배정만, 별·비용은 저장하지 않는다.</summary>
        public static FieldSaveData Capture(IList<FieldMissionData> missions, FieldProgress progress, Func<int, IEnumerable<string>> observed,
            int current, FieldSavePhase phase, FieldPlan draft, string selectedSpot)
        {
            var data = new FieldSaveData
            {
                version = Version,
                currentMission = current >= 0 && current < missions.Count ? missions[current].id : null,
                phase = phase.ToString(),
                selectedSpot = selectedSpot,
                draft = draft != null && phase != FieldSavePhase.Review && phase != FieldSavePhase.Complete ? Picks(draft.Assignments) : new List<FieldSavePick>()
            };
            for (int i = 0; i < missions.Count; i++)
            {
                var r = progress.Get(i);
                data.missions.Add(new FieldSaveMission
                {
                    id = missions[i].id,
                    confirmed = r != null,
                    picks = r != null ? Picks(r.Assignments) : new List<FieldSavePick>(),
                    observed = (observed(i) ?? Enumerable.Empty<string>()).OrderBy(x => x).ToList()
                });
            }
            return data;
        }

        static FieldPlan BuildPlan(FieldMissionData m, FieldMissionSpec spec, IEnumerable<FieldSavePick> picks, List<string> warnings)
        {
            var plan = new FieldPlan();
            foreach (var p in picks ?? Enumerable.Empty<FieldSavePick>())
            {
                if (p == null || m.Spot(p.spot) == null || m.Item(p.item) == null)
                {
                    warnings.Add($"{m.id}: 알 수 없는 배정 {p?.spot}={p?.item} 건너뜀");
                    continue;
                }
                if (plan.ItemAt(p.spot) != null) continue;
                if (plan.Toggle(spec, p.spot, p.item) == AssignOutcome.NotAllowed)
                    warnings.Add($"{m.id}: 놓을 수 없는 배정 {p.spot}={p.item} 건너뜀");
            }
            return plan;
        }

        /// <summary>
        /// 저장 데이터를 현재 미션 데이터로 검증해 복원한다. 손상·구버전·알 수 없는 데이터는 null(새 게임처럼 시작).
        /// 확정 결과는 저장된 배정으로 FieldRules가 다시 판정하며, 예산 초과·확정 조건 미달이면 확정하지 않는다.
        /// </summary>
        public static FieldRestored Restore(IList<FieldMissionData> missions, FieldSaveData data)
        {
            if (data == null || missions == null || missions.Count == 0) return null;
            if (data.version != Version) return null;

            int n = missions.Count;
            var restored = new FieldRestored { Results = new FieldEvaluation[n], Observed = new HashSet<string>[n] };
            FieldPlan demotedDraft = null;
            int demotedIndex = -1;
            for (int i = 0; i < n; i++)
            {
                var m = missions[i];
                var spec = m.ToSpec();
                var entry = data.missions?.FirstOrDefault(x => x != null && x.id == m.id);
                restored.Observed[i] = new HashSet<string>((entry?.observed ?? new List<string>()).Where(id => m.Problem(id) != null));
                if (entry == null || !entry.confirmed) continue;

                var plan = BuildPlan(m, spec, entry.picks, restored.Warnings);
                bool unlocked = i == 0 || (restored.Results[i - 1] != null && restored.Results[i - 1].Success);
                if (unlocked && FieldRules.CanConfirm(spec, plan, restored.Observed[i]) == ConfirmBlock.None)
                    restored.Results[i] = FieldRules.Evaluate(spec, plan);
                else
                {
                    // 자동으로 확정하지 않는다. 계획만 남겨 다시 고칠 수 있게 한다.
                    restored.Warnings.Add($"{m.id}: 저장된 확정 결과가 유효하지 않아 확정 전 계획으로 되돌림");
                    if (demotedDraft == null) { demotedDraft = plan; demotedIndex = i; }
                }
            }

            // 이어할 현장: 저장된 현장이 열려 있으면 그곳, 아니면 아직 성공하지 못한 첫 현장.
            int firstOpen = Enumerable.Range(0, n).FirstOrDefault(i => restored.Results[i] == null || !restored.Results[i].Success);
            int current = Enumerable.Range(0, n).FirstOrDefault(i => missions[i].id == data.currentMission);
            bool known = missions.Any(m => m.id == data.currentMission);
            if (!known)
            {
                if (!string.IsNullOrEmpty(data.currentMission)) restored.Warnings.Add("알 수 없는 현장 ID " + data.currentMission);
                current = firstOpen;
            }
            bool currentUnlocked = current == 0 || (restored.Results[current - 1] != null && restored.Results[current - 1].Success);
            if (!currentUnlocked) current = firstOpen;
            restored.Current = current;

            if (!Enum.TryParse(data.phase, out FieldSavePhase phase)) phase = FieldSavePhase.Explore;
            bool hasResult = restored.Results[current] != null;
            bool complete = restored.Results.All(r => r != null && r.Success);
            if (phase == FieldSavePhase.Complete && !complete) phase = hasResult ? FieldSavePhase.Review : FieldSavePhase.Explore;
            if ((phase == FieldSavePhase.Review || phase == FieldSavePhase.Replan) && !hasResult) phase = FieldSavePhase.Explore;
            restored.Phase = phase;

            var cm = missions[current];
            var cspec = cm.ToSpec();
            if (phase == FieldSavePhase.Explore || phase == FieldSavePhase.Replan)
            {
                restored.Draft = data.draft != null && data.draft.Count > 0
                    ? BuildPlan(cm, cspec, data.draft, restored.Warnings)
                    : demotedIndex == current && demotedDraft != null ? demotedDraft
                    : phase == FieldSavePhase.Replan ? BuildPlan(cm, cspec, restored.Results[current].Assignments.Select(a => new FieldSavePick { spot = a.Key, item = a.Value }), restored.Warnings)
                    : new FieldPlan();
            }
            restored.SelectedSpot = cm.Spot(data.selectedSpot) != null ? data.selectedSpot : (cm.spots.Count > 0 ? cm.spots[0].id : null);
            return restored;
        }
    }
}
