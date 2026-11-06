namespace ProcurementHero.Core
{
    public enum EndingKind
    {
        /// <summary>모든 미션 성공: 모두의 하루를 바꾼 조달 히어로</summary>
        Hero,
        /// <summary>하나 빼고 성공: 성장하는 공공구매 해결사</summary>
        Growing,
        /// <summary>그 외: 다시 도전하는 마을 지원팀</summary>
        TryAgain
    }

    /// <summary>
    /// 한 판의 진행 상태. 미션마다 현재 채택된 결과 하나만 보관하므로
    /// 재도전은 결과를 대체하고 최종 점수에 중복 가산되지 않는다.
    /// </summary>
    public sealed class GameProgress
    {
        readonly PurchaseEvaluation[] results;

        public GameProgress(int missionCount)
        {
            results = new PurchaseEvaluation[missionCount];
        }

        public int MissionCount => results.Length;

        public PurchaseEvaluation GetResult(int missionIndex) => results[missionIndex];

        public void SetResult(int missionIndex, PurchaseEvaluation evaluation)
        {
            results[missionIndex] = evaluation;
        }

        public bool IsUnlocked(int missionIndex)
        {
            return missionIndex == 0 || results[missionIndex - 1] != null;
        }

        public bool HasAnyResult
        {
            get
            {
                foreach (var r in results)
                    if (r != null) return true;
                return false;
            }
        }

        public bool IsComplete
        {
            get
            {
                foreach (var r in results)
                    if (r == null) return false;
                return true;
            }
        }

        public int SuccessCount
        {
            get
            {
                int count = 0;
                foreach (var r in results)
                    if (r != null && r.Success) count++;
                return count;
            }
        }

        public double TotalScore
        {
            get
            {
                double sum = 0;
                foreach (var r in results)
                    if (r != null) sum += r.TotalScore;
                return sum;
            }
        }

        public EndingKind Ending
        {
            get
            {
                int success = SuccessCount;
                if (success >= MissionCount) return EndingKind.Hero;
                if (success == MissionCount - 1) return EndingKind.Growing;
                return EndingKind.TryAgain;
            }
        }

        public void Reset()
        {
            for (int i = 0; i < results.Length; i++) results[i] = null;
        }
    }
}
