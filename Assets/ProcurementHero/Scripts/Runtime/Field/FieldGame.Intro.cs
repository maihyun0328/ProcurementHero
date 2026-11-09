using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProcurementHero.Field
{
    /// <summary>
    /// 앱 실행 직후 한 번만 나오는 시작 연출(약 4.8초): 어두운 책상 위 닫힌 수첩이 드러남 → AI '내일' 알림 →
    /// 수첩이 펼쳐지며 새봄시 지도와 예상 위험 표시 → 지도가 타이틀 배경으로 이어짐 → 타이틀 메뉴.
    /// 저장·진행·Time.timeScale은 건드리지 않는다. 포커스를 잃으면 연출 시계가 멈춘다.
    /// Space·Enter·Esc·건너뛰기 버튼으로 바로 타이틀로 가며, 정상 재생과 같은 타이틀 상태로 끝난다.
    /// </summary>
    public partial class FieldGame
    {
        public const float IntroSeconds = 4.8f;
        const float IntroRevealEnd = 0.9f, IntroAlertEnd = 1.9f, IntroOpenEnd = 3.0f;
        // 실제 속도 녹화에서 커지는 수첩과 타이틀 카드가 같은 투명도로 겹쳐 글자가 이중으로 보였다.
        // 그래서 수첩이 먼저 빠지고(3.3~4.1초) 그 뒤 타이틀 카드가 나타나게(3.9~4.8초) 나눈다. 알림은 3.5~3.9초에 사라진다.
        const float IntroBookFadeStart = 3.3f, IntroBookFadeLength = 0.8f, IntroTitleFadeStart = 3.9f, IntroAlertFadeStart = 3.5f;

        /// <summary>이번 실행에서 시작 연출을 이미 보여 줬는지. 타이틀 복귀·새 게임·재계획에서는 다시 재생하지 않는다.</summary>
        static bool introPlayed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetIntroFlag() => introPlayed = false;

        RectTransform introRoot;
        CanvasGroup titleGroup;
        Selectable titleFirst;
        Coroutine introRoutine;

        /// <summary>시작 연출이 재생 중인지(검증용).</summary>
        public bool IntroPlaying => State == FieldState.Intro;
        /// <summary>시작 연출의 진행 시간(포커스를 잃은 동안은 멈춘다, 검증용).</summary>
        public float IntroTime { get; private set; }
        /// <summary>이번 실행에서 시작 연출이 재생됐는지(검증용).</summary>
        public static bool IntroPlayedThisRun => introPlayed;

        /// <summary>
        /// 연출 시계: 포커스가 없으면 멈추고, 한 프레임에 0.1초까지만 나아간다
        /// (멈췄다 돌아온 첫 프레임에 쌓인 시간만큼 건너뛰지 않고 이어서 재생).
        /// </summary>
        float IntroDelta => Application.isFocused ? Mathf.Min(Time.unscaledDeltaTime, 0.1f) : 0f;

        void PlayIntro()
        {
            introPlayed = true;
            BeginScreen(FieldState.Intro);
            IntroTime = 0f;
            introRoutine = StartCoroutine(IntroSequence(screenToken));
        }

        /// <summary>건너뛰기(Space·Enter·Esc·버튼). 저장·새 게임·종료와 연결하지 않는다. 여러 번 눌러도 한 번만 처리한다.</summary>
        public void SkipIntro()
        {
            if (State != FieldState.Intro) return;
            if (introRoutine != null) StopCoroutine(introRoutine);
            introRoutine = null;
            FinishIntro();
        }

        IEnumerator IntroSequence(int token)
        {
            introRoot = UIKit.Rect(overlayRoot, "Intro").Fill();
            var dark = UIKit.Panel(introRoot, "Dark", Theme.Hex(0x0B1026), ShapeSprites.Square).rectTransform;
            dark.Fill();
            var darkImage = dark.GetComponent<Image>();
            darkImage.raycastTarget = false;

            // 책상과 위에서 내려오는 조명
            var desk = UIKit.Panel(introRoot, "Desk", Theme.Hex(0x5A3F2C), ShapeSprites.Square).rectTransform;
            desk.Region(0, 0, 1, 0.34f);
            var deskEdge = UIKit.Panel(introRoot, "DeskEdge", Theme.Hex(0x7A5A3E), ShapeSprites.Square).rectTransform;
            deskEdge.Region(0, 0.33f, 1, 0.35f);
            var light = UIKit.Shape(introRoot, "Light", ShapeSprites.Circle, Theme.WithAlpha(Theme.Hex(0xFFE7A8), 0f),
                new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(1300, 1000));
            light.raycastTarget = false;
            var sceneGroup = desk.gameObject.AddComponent<CanvasGroup>();
            var edgeGroup = deskEdge.gameObject.AddComponent<CanvasGroup>();

            // 기존 수첩(FieldBook)을 닫힌 모습으로. 닫혀 있을 때는 오른쪽 절반(표지)만 보이므로 가운데로 옮겨 둔다.
            var book = FieldBook.Build(introRoot, new Vector2(0.24f, 0.17f), new Vector2(0.76f, 0.83f), false);
            FieldBook.SetClosed(book);
            book.group.interactable = false;
            book.group.blocksRaycasts = false;
            BuildIntroPages(book);
            float halfWidth = ((RectTransform)canvas.transform).rect.width * 0.52f / 4f;

            // AI '내일' 알림
            var alert = UIKit.Rect(introRoot, "Alert").Region(0.3f, 0.86f, 0.7f, 0.95f);
            var alertGroup = alert.gameObject.AddComponent<CanvasGroup>();
            alertGroup.alpha = 0f;
            var icon = FieldArt.ForecastIcon(alert, 70);
            icon.Place(new Vector2(0, 0.5f), new Vector2(46, 0), new Vector2(70, 70));
            var alertText = UIKit.Text(alert, "Text", "오늘의 현장 예측이 도착했어요.", 34, Theme.Cream, TextAlignmentOptions.MidlineLeft, true);
            alertText.rectTransform.Fill(100, 0, 0, 0);

            var skip = UIKit.Button(introRoot, "SkipIntro", "건너뛰기 (Space)", SkipIntro, ButtonStyle.Ghost, 24);
            ((RectTransform)skip.transform).Region(0.84f, 0.92f, 0.98f, 0.98f);
            Deselectable(skip);

            // 타이틀 화면(마을 지도·메뉴)은 처음에 어두운 화면 뒤에 숨겨 만들어 둔다. 중간(3초)에 만들면 한 프레임이 길어져
            // 펼친 수첩이 잠깐 멈춰 보였다(실제 속도 기록에서 약 55ms).
            BuildTitleScreen(false);
            titleGroup.alpha = 0f;
            bool alerted = false;
            while (token == screenToken && IntroTime < IntroSeconds)
            {
                IntroTime += IntroDelta;
                float t = IntroTime;

                // ① 어둠 속에서 조명이 켜지며 책상 위 닫힌 수첩이 드러난다.
                float reveal = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / IntroRevealEnd));
                light.color = Theme.WithAlpha(Theme.Hex(0xFFE7A8), 0.09f * reveal);
                darkImage.color = Theme.WithAlpha(Theme.Hex(0x0B1026), 1f);
                sceneGroup.alpha = edgeGroup.alpha = reveal;

                // ② 작은 알림음과 함께 AI 아이콘이 켜진다.
                if (t >= IntroRevealEnd && !alerted)
                {
                    alerted = true;
                    sound.Play(Sfx.Check);
                }
                float a = Mathf.Clamp01((t - IntroRevealEnd) / 0.35f);
                alertGroup.alpha = t < IntroAlertFadeStart ? a : Mathf.Clamp01(1f - (t - IntroAlertFadeStart) / 0.4f);
                icon.localScale = Vector3.one * (a < 1f ? Mathf.Lerp(0.6f, 1.1f, a) : 1f + Mathf.Sin(t * 5f) * 0.03f);

                // ③ 수첩 표지가 넘어가며 새봄시 지도와 예상 위험 표시가 나타난다.
                float open = Mathf.Clamp01((t - IntroAlertEnd) / (IntroOpenEnd - IntroAlertEnd));
                book.cover.localScale = new Vector3(Mathf.Clamp01(1f - open * 2f), 1, 1);
                book.coverInside.localScale = new Vector3(open < 0.5f ? open * 2f : Mathf.Clamp01(2f - open * 2f), 1, 1);
                book.Spread = Mathf.Clamp01(open * 2f - 0.6f);
                book.book.anchoredPosition = new Vector2(Mathf.Lerp(-halfWidth, 0f, Mathf.SmoothStep(0, 1, open)), 0);

                // ④ 지도가 커지며 흐려지고, 그 아래에서 타이틀 배경(같은 마을 지도)이 드러난다.
                float e = Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - IntroBookFadeStart) / IntroBookFadeLength));
                if (t >= IntroBookFadeStart)
                {
                    book.book.localScale = Vector3.one * Mathf.Lerp(1f, 1.4f, e);
                    book.group.alpha = 1f - e;
                    darkImage.color = Theme.WithAlpha(Theme.Hex(0x0B1026), 1f - e);
                    sceneGroup.alpha = edgeGroup.alpha = 1f - e;
                    light.color = Theme.WithAlpha(Theme.Hex(0xFFE7A8), 0.09f * (1f - e));
                }
                // ⑤ 수첩이 거의 빠진 뒤 로고·제목·메뉴가 나타난다(끝날 때까지 누를 수 없음).
                titleGroup.alpha = Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - IntroTitleFadeStart) / (IntroSeconds - IntroTitleFadeStart)));
                yield return null;
            }
            if (token != screenToken) yield break;
            introRoutine = null;
            FinishIntro();
        }

        /// <summary>펼친 수첩 안쪽: 왼쪽 새봄시 지도와 예상 위험 아이콘, 오른쪽 오늘의 현장 세 곳.</summary>
        void BuildIntroPages(FieldBook.Parts book)
        {
            UIKit.Text(book.left, "Title", "새봄시", 34, Theme.Navy, TextAlignmentOptions.MidlineLeft, true).rectTransform.Region(0.08f, 0.84f, 0.92f, 0.95f);
            var map = UIKit.Panel(book.left, "Map", Theme.Hex(0xEDE3CC)).rectTransform;
            map.Region(0.07f, 0.1f, 0.93f, 0.82f);
            UIKit.Panel(map, "RoadA", Theme.Hex(0xD8CBAE), ShapeSprites.Square).rectTransform.Region(0f, 0.42f, 1f, 0.5f);
            UIKit.Panel(map, "RoadB", Theme.Hex(0xD8CBAE), ShapeSprites.Square).rectTransform.Region(0.46f, 0f, 0.54f, 1f);
            var kinds = new[] { FieldProblemKind.Dark, FieldProblemKind.Heat, FieldProblemKind.Flood };
            var spots = new[] { new Vector2(0.24f, 0.72f), new Vector2(0.76f, 0.74f), new Vector2(0.72f, 0.22f) };
            for (int i = 0; i < Mathf.Min(spots.Length, database.missions.Count); i++)
            {
                var marker = UIKit.Panel(map, "Risk" + i, Theme.WithAlpha(Theme.Hex(0xF2C14E), 0.35f)).rectTransform;
                marker.Place(spots[i], Vector2.zero, new Vector2(110, 90));
                FieldArt.UnknownPattern(marker, Theme.WithAlpha(Theme.Navy, 0.18f));
                FieldArt.ProblemIcon(marker, kinds[i], 52).Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(52, 52));
            }
            UIKit.Text(book.right, "Head", "오늘의 현장", 32, Theme.Navy, TextAlignmentOptions.MidlineLeft, true).rectTransform.Region(0.1f, 0.84f, 0.92f, 0.95f);
            for (int i = 0; i < database.missions.Count; i++)
            {
                var row = UIKit.Rect(book.right, "Site" + i).Region(0.1f, 0.62f - i * 0.2f, 0.92f, 0.78f - i * 0.2f);
                FieldArt.ProblemIcon(row, kinds[Mathf.Min(i, kinds.Length - 1)], 50).Place(new Vector2(0, 0.5f), new Vector2(30, 0), new Vector2(50, 50));
                UIKit.Text(row, "Name", database.missions[i].placeName, 26, Theme.Navy, TextAlignmentOptions.MidlineLeft).rectTransform.Fill(70, 0, 0, 0);
            }
        }

        /// <summary>정상 재생 끝과 건너뛰기가 같은 타이틀 상태로 끝나게 한다. 입력은 다음 프레임부터 받는다.</summary>
        void FinishIntro()
        {
            if (State != FieldState.Intro) return;
            if (introRoot) Destroy(introRoot.gameObject);
            introRoot = null;
            if (!titleGroup) BuildTitleScreen(false);
            titleGroup.alpha = 1f;
            titleGroup.interactable = false;
            ResetCamera();
            SetState(FieldState.Title);
            StartCoroutine(EnableTitleNextFrame(stateVersion));
        }

        /// <summary>건너뛴 Space·Enter가 같은 프레임에 '게임 시작'·'이어하기'를 누르지 않도록 한 프레임 뒤에 메뉴를 켠다.</summary>
        IEnumerator EnableTitleNextFrame(int version)
        {
            yield return null;
            if (version != stateVersion || !titleGroup) yield break;
            titleGroup.interactable = true;
            Select(titleFirst);
        }
    }

    /// <summary>타이틀의 은은한 반복 움직임: 밝기만 천천히 바뀐다(크기·위치는 그대로, 번쩍임 없음).</summary>
    public class SoftGlow : MonoBehaviour
    {
        CanvasGroup group;
        void Awake() => group = gameObject.AddComponent<CanvasGroup>();
        void Update()
        {
            if (group) group.alpha = 0.75f + Mathf.Sin(Time.unscaledTime * 1.6f) * 0.25f;
        }
    }
}
