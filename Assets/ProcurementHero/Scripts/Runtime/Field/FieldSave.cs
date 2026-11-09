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
        /// <summary>
        /// 편집 중 계획을 실제로 저장했는지. true면 draft가 비어 있어도 '모두 취소한 빈 계획'으로 그대로 복원한다.
        /// 이 값이 없는 이전 저장(false)은 편집 데이터가 누락된 것으로 보고, 재계획이면 이전 확정 배정을 고칠 수 있게 연다.
        /// JsonUtility는 빈 목록과 없는 목록을 구분하지 못하므로(둘 다 빈 목록) 이 표시로 구분한다. 버전은 1 그대로(하위 호환).
        /// </summary>
        public bool draftSaved;
        public string selectedSpot;
        public List<FieldSaveMission> missions = new List<FieldSaveMission>();

        // ── 완주 뒤 현장 재도전(이번 형식에서 추가, 버전 1 그대로) ──
        // 이전 저장에는 이 값들이 없고, 읽으면 빈 문자열·false·빈 목록이 되어 '재도전 없음(일반 진행)'으로 복원된다.
        /// <summary>재도전 중인 현장 ID. 비어 있으면 일반 진행. 세 현장을 모두 성공한 저장에서만 유효하다.</summary>
        public string retryMission;
        /// <summary>이번 재도전 시도를 확정(승인)했는지. 확정 배정은 retryPicks, 대표 결과(missions)와 따로 둔다.</summary>
        public bool retryConfirmed;
        public List<FieldSavePick> retryPicks = new List<FieldSavePick>();
        /// <summary>이번 재도전 시도에서 관찰한 곳. 대표 결과의 관찰 기록(missions[].observed)과 섞지 않는다.</summary>
        public List<string> retryObserved = new List<string>();
    }

    /// <summary>저장할 재도전 맥락(일반 진행이면 null).</summary>
    public sealed class FieldRetry
    {
        public int Index;
        public FieldEvaluation Result;
        public IEnumerable<string> Observed;
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
        /// <summary>완주 뒤 재도전 중인 현장(-1이면 일반 진행). Current와 같다.</summary>
        public int RetryIndex = -1;
        /// <summary>이번 재도전 시도의 확정 결과(확정 전이면 null). 대표 결과(Results)와 별개.</summary>
        public FieldEvaluation RetryResult;
        public HashSet<string> RetryObserved = new HashSet<string>();
        public bool Retrying => RetryIndex >= 0;

        /// <summary>이어할 만한 진행이 있는지(관찰·계획·확정 중 하나라도).</summary>
        public bool HasProgress => Results.Any(r => r != null) || Observed.Any(o => o.Count > 0) || Draft.Assignments.Count > 0 || Retrying;
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
            int current, FieldSavePhase phase, FieldPlan draft, string selectedSpot, FieldRetry retry = null)
        {
            bool editing = draft != null && phase != FieldSavePhase.Review && phase != FieldSavePhase.Complete;
            var data = new FieldSaveData
            {
                version = Version,
                currentMission = current >= 0 && current < missions.Count ? missions[current].id : null,
                phase = phase.ToString(),
                selectedSpot = selectedSpot,
                draft = editing ? Picks(draft.Assignments) : new List<FieldSavePick>(),
                draftSaved = editing
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
            if (retry != null && retry.Index >= 0 && retry.Index < missions.Count && phase != FieldSavePhase.Complete)
            {
                data.currentMission = missions[retry.Index].id;
                data.retryMission = missions[retry.Index].id;
                data.retryConfirmed = retry.Result != null;
                data.retryPicks = retry.Result != null ? Picks(retry.Result.Assignments) : new List<FieldSavePick>();
                data.retryObserved = (retry.Observed ?? Enumerable.Empty<string>()).OrderBy(x => x).ToList();
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
                // 이번 형식: 저장한 편집 계획을 그대로(빈 계획도 빈 채로). 이전 형식·누락: 아래 순서로 대신 연다.
                restored.Draft = data.draftSaved || (data.draft != null && data.draft.Count > 0)
                    ? BuildPlan(cm, cspec, data.draft, restored.Warnings)
                    : demotedIndex == current && demotedDraft != null ? demotedDraft
                    : phase == FieldSavePhase.Replan ? BuildPlan(cm, cspec, restored.Results[current].Assignments.Select(a => new FieldSavePick { spot = a.Key, item = a.Value }), restored.Warnings)
                    : new FieldPlan();
            }
            restored.SelectedSpot = cm.Spot(data.selectedSpot) != null ? data.selectedSpot : (cm.spots.Count > 0 ? cm.spots[0].id : null);
            RestoreRetry(missions, data, restored, complete);
            return restored;
        }

        /// <summary>
        /// 재도전 맥락 복원. 세 현장을 모두 성공한 저장에서만 쓰고(아니면 버리고 일반 진행), 그 현장을 처음 상태부터 다시 하는 시도로 되살린다.
        /// 확정한 시도는 그 시도의 관찰로 확정 조건을 다시 검사해, 맞지 않으면 확정 전 계획으로 낮춘다. 대표 결과는 건드리지 않는다.
        /// </summary>
        static void RestoreRetry(IList<FieldMissionData> missions, FieldSaveData data, FieldRestored restored, bool complete)
        {
            if (string.IsNullOrEmpty(data.retryMission)) return;
            int index = Enumerable.Range(0, missions.Count).FirstOrDefault(i => missions[i].id == data.retryMission);
            if (missions[index].id != data.retryMission || !complete || restored.Phase == FieldSavePhase.Complete)
            {
                restored.Warnings.Add("재도전 정보를 쓸 수 없어(알 수 없는 현장·완주 전·이미 끝남) 일반 진행으로 엽니다");
                return;
            }
            var m = missions[index];
            var spec = m.ToSpec();
            restored.RetryIndex = index;
            restored.Current = index;
            restored.RetryObserved = new HashSet<string>((data.retryObserved ?? new List<string>()).Where(id => m.Problem(id) != null));
            restored.Draft = data.phase == FieldSavePhase.Explore.ToString() || !data.retryConfirmed
                ? BuildPlan(m, spec, data.draftSaved ? data.draft : new List<FieldSavePick>(), restored.Warnings)
                : new FieldPlan();
            restored.Phase = FieldSavePhase.Explore;
            if (data.retryConfirmed)
            {
                var plan = BuildPlan(m, spec, data.retryPicks, restored.Warnings);
                if (FieldRules.CanConfirm(spec, plan, restored.RetryObserved) == ConfirmBlock.None)
                {
                    restored.RetryResult = FieldRules.Evaluate(spec, plan);
                    restored.Phase = FieldSavePhase.Review;
                }
                else
                {
                    restored.Warnings.Add($"{m.id}: 재도전 확정 결과가 유효하지 않아 확정 전 계획으로 되돌림");
                    restored.Draft = plan;
                }
            }
            restored.SelectedSpot = m.Spot(data.selectedSpot) != null ? data.selectedSpot : (m.spots.Count > 0 ? m.spots[0].id : null);
        }
    }
}
