using UnityEngine;

namespace ProcurementHero
{
    public enum Sfx
    {
        Click,
        Select,
        Purchase,
        Arrive,
        Success,
        Partial,
        Fail,
        // 현장 수첩 버전
        Observe,
        NotebookOpen,
        Check,
        Uncheck,
        Confirm,
        Install,
        Stamp
    }

    /// <summary>
    /// 외부 음원 없이 실행 시 합성한 효과음과 배경음을 재생한다.
    /// 클립 생성에 실패해도 게임 진행은 막히지 않는다.
    /// </summary>
    public class AudioService : MonoBehaviour
    {
        const int Rate = 44100;
        AudioSource sfxSource;
        AudioSource bgmSource;
        AudioClip[] clips;

        public void Init()
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.playOnAwake = false;
            bgmSource.loop = true;
            // 일시정지(timeScale 0) 중에도 효과음은 들려야 하므로 AudioListener.pause는 쓰지 않는다.
            try
            {
                clips = new[]
                {
                    Tone("click", new[] { 660f }, 0.05f, 0.25f),
                    Tone("select", new[] { 784f, 988f }, 0.07f, 0.3f),
                    Tone("purchase", new[] { 523f, 659f, 784f }, 0.09f, 0.35f),
                    Tone("arrive", new[] { 392f, 523f }, 0.1f, 0.3f),
                    Tone("success", new[] { 523f, 659f, 784f, 1047f }, 0.13f, 0.4f),
                    Tone("partial", new[] { 523f, 587f, 523f }, 0.13f, 0.35f),
                    Tone("fail", new[] { 494f, 440f, 349f }, 0.16f, 0.35f),
                    Tone("observe", new[] { 880f, 1175f }, 0.06f, 0.3f),
                    Tone("notebook", new[] { 330f, 392f }, 0.07f, 0.25f),
                    Tone("check", new[] { 988f }, 0.07f, 0.32f),
                    Tone("uncheck", new[] { 587f, 440f }, 0.06f, 0.28f),
                    Tone("confirm", new[] { 523f, 784f, 1047f }, 0.1f, 0.38f),
                    Tone("install", new[] { 392f, 587f }, 0.08f, 0.32f),
                    Tone("stamp", new[] { 196f, 262f, 392f }, 0.12f, 0.45f)
                };
                bgmSource.clip = Melody();
                bgmSource.Play();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[ProcurementHero] 오디오 생성 실패, 소리 없이 진행합니다: " + e.Message);
                clips = null;
            }
            ApplySettings();
        }

        public void ApplySettings()
        {
            bool muted = SaveService.Muted;
            if (sfxSource) sfxSource.volume = muted ? 0f : SaveService.SfxVolume;
            if (bgmSource) bgmSource.volume = muted ? 0f : SaveService.BgmVolume * 0.35f;
        }

        public void Play(Sfx sfx)
        {
            if (clips == null || !sfxSource) return;
            var clip = clips[(int)sfx];
            if (clip) sfxSource.PlayOneShot(clip);
        }

        static AudioClip Tone(string name, float[] notes, float noteLength, float gain)
        {
            int perNote = Mathf.CeilToInt(noteLength * Rate);
            var data = new float[perNote * notes.Length];
            for (int n = 0; n < notes.Length; n++)
            for (int i = 0; i < perNote; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Clamp01(i / (Rate * 0.005f)) * Mathf.Exp(-t * 18f / noteLength * 0.25f) * (1f - i / (float)perNote);
                float wave = Mathf.Sin(2 * Mathf.PI * notes[n] * t) * 0.8f + Mathf.Sin(4 * Mathf.PI * notes[n] * t) * 0.2f;
                data[n * perNote + i] = wave * env * gain;
            }
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>오음계로 만든 8마디짜리 잔잔한 반복곡.</summary>
        static AudioClip Melody()
        {
            float[] scale = { 392f, 440f, 523f, 587f, 659f, 784f };
            int[] tune = { 2, 3, 4, 2, 1, 2, 3, -1, 4, 5, 4, 3, 2, 3, 1, -1, 2, 3, 4, 5, 4, 3, 2, -1, 1, 2, 3, 2, 1, 0, 1, -1 };
            int[] bass = { 0, 0, 2, 2, 1, 1, 0, 0 };
            float beat = 0.32f;
            int perBeat = Mathf.CeilToInt(beat * Rate);
            var data = new float[perBeat * tune.Length];
            for (int b = 0; b < tune.Length; b++)
            for (int i = 0; i < perBeat; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Clamp01(i / (Rate * 0.01f)) * Mathf.Exp(-t * 4f);
                float v = 0f;
                if (tune[b] >= 0) v += Mathf.Sin(2 * Mathf.PI * scale[tune[b]] * t) * env * 0.22f;
                float bassFreq = scale[bass[b / 4 % bass.Length]] * 0.5f;
                float bassT = ((b % 4) * perBeat + i) / (float)Rate;
                v += Mathf.Sin(2 * Mathf.PI * bassFreq * bassT) * 0.08f * (1f - bassT / (4f * beat));
                data[b * perBeat + i] = v;
            }
            var clip = AudioClip.Create("bgm", data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
