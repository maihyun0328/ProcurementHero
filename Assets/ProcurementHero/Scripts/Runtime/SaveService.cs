using System.Collections.Generic;
using UnityEngine;

namespace ProcurementHero
{
    /// <summary>SaveService가 쓰는 키-값 저장소. 실행 중에는 PlayerPrefs, 테스트에서는 메모리 저장소를 쓴다.</summary>
    public interface IKeyValueStore
    {
        bool HasKey(string key);
        float GetFloat(string key, float fallback);
        int GetInt(string key, int fallback);
        void SetFloat(string key, float value);
        void SetInt(string key, int value);
        void DeleteKey(string key);
        void Save();
    }

    public sealed class PlayerPrefsStore : IKeyValueStore
    {
        public bool HasKey(string key) => PlayerPrefs.HasKey(key);
        public float GetFloat(string key, float fallback) => PlayerPrefs.GetFloat(key, fallback);
        public int GetInt(string key, int fallback) => PlayerPrefs.GetInt(key, fallback);
        public void SetFloat(string key, float value) => PlayerPrefs.SetFloat(key, value);
        public void SetInt(string key, int value) => PlayerPrefs.SetInt(key, value);
        public void DeleteKey(string key) => PlayerPrefs.DeleteKey(key);
        public void Save() => PlayerPrefs.Save();
    }

    /// <summary>사용자의 실제 기록을 건드리지 않고 시험하기 위한 메모리 저장소.</summary>
    public sealed class MemoryStore : IKeyValueStore
    {
        readonly Dictionary<string, float> floats = new Dictionary<string, float>();
        readonly Dictionary<string, int> ints = new Dictionary<string, int>();

        public bool HasKey(string key) => floats.ContainsKey(key) || ints.ContainsKey(key);
        public float GetFloat(string key, float fallback) => floats.TryGetValue(key, out var v) ? v : fallback;
        public int GetInt(string key, int fallback) => ints.TryGetValue(key, out var v) ? v : fallback;
        public void SetFloat(string key, float value) => floats[key] = value;
        public void SetInt(string key, int value) => ints[key] = value;
        public void DeleteKey(string key) { floats.Remove(key); ints.Remove(key); }
        public void Save() { }
    }

    /// <summary>최고 기록과 소리 설정만 저장한다. 개인정보는 다루지 않는다.</summary>
    public static class SaveService
    {
        /// <summary>기본은 PlayerPrefs. 테스트는 MemoryStore로 바꿨다가 되돌린다.</summary>
        public static IKeyValueStore Store = new PlayerPrefsStore();

        const string BestScoreKey = "PH_BestScore";
        const string BestSuccessKey = "PH_BestSuccess";
        const string SfxKey = "PH_SfxVolume";
        const string BgmKey = "PH_BgmVolume";
        const string MuteKey = "PH_Mute";

        public static bool HasBest => Store.HasKey(BestScoreKey);
        public static float BestScore => Store.GetFloat(BestScoreKey, 0f);
        public static int BestSuccess => Store.GetInt(BestSuccessKey, 0);

        /// <summary>
        /// 점수가 새 기록이면 저장하고 true를 돌려준다. 최다 성공 수는 점수와 따로 갱신한다.
        /// 저장소는 float로 보관하므로 비교도 저장할 값(float)으로 한다. double 그대로 비교하면
        /// 같은 엔딩을 다시 열 때 저장 중 잘린 자릿수만큼 '더 높은 점수'로 잘못 판정된다.
        /// </summary>
        public static bool SubmitRecord(double totalScore, int successCount)
        {
            float candidate = (float)totalScore;
            bool better = !HasBest || candidate > BestScore;
            if (better) Store.SetFloat(BestScoreKey, candidate);
            if (successCount > BestSuccess) Store.SetInt(BestSuccessKey, successCount);
            Store.Save();
            return better;
        }

        public static void ResetBest()
        {
            Store.DeleteKey(BestScoreKey);
            Store.DeleteKey(BestSuccessKey);
            Store.Save();
        }

        public static float SfxVolume
        {
            get => Store.GetFloat(SfxKey, 0.8f);
            set { Store.SetFloat(SfxKey, value); Store.Save(); }
        }

        public static float BgmVolume
        {
            get => Store.GetFloat(BgmKey, 0.5f);
            set { Store.SetFloat(BgmKey, value); Store.Save(); }
        }

        public static bool Muted
        {
            get => Store.GetInt(MuteKey, 0) == 1;
            set { Store.SetInt(MuteKey, value ? 1 : 0); Store.Save(); }
        }
    }
}
