using System.Collections.Generic;
using ProcurementHero.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ProcurementHero
{
    public enum GameScreen
    {
        Title,
        Map,
        Mission,
        Result,
        Ending
    }

    /// <summary>
    /// 한 씬 안에서 타이틀 → 지도 → 미션 → 결과 → 엔딩을 상태 전환으로 연결한다.
    /// 화면은 들어갈 때마다 새로 만들어서 재도전 시 이전 상태가 남지 않는다.
    /// </summary>
    public partial class GameManager : MonoBehaviour
    {
        [SerializeField] GameDatabase database;
        [SerializeField] TMP_FontAsset font;
        [SerializeField] Canvas canvas;

        public GameDatabase Database => database;
        public GameScreen CurrentScreen { get; private set; }
        public GameProgress Progress { get; private set; }

        AudioService sound;
        RectTransform screenRoot;
        RectTransform overlayRoot;
        RectTransform toastRoot;
        readonly List<GameObject> modals = new List<GameObject>();
        bool paused;

        void Awake()
        {
            if (!canvas) canvas = FindFirstObjectByType<Canvas>();
            if (!database || database.missions.Count == 0 || !canvas)
            {
                Debug.LogError("[ProcurementHero] GameDatabase 또는 Canvas 참조가 없습니다. Tools/Procurement Hero/Setup Game을 실행하세요.");
                enabled = false;
                return;
            }

            UIKit.Font = font;
            Progress = new GameProgress(database.missions.Count);
            Time.timeScale = 1f;

            sound = gameObject.AddComponent<AudioService>();
            sound.Init();
            UIKit.OnButtonSound = () => sound.Play(Sfx.Click);

            var background = UIKit.Panel(canvas.transform, "Background", Theme.Cream, ShapeSprites.Square);
            background.rectTransform.Fill();
            screenRoot = UIKit.Rect(canvas.transform, "Screens").Fill();
            toastRoot = UIKit.Rect(canvas.transform, "Toasts").Fill();
            overlayRoot = UIKit.Rect(canvas.transform, "Overlays").Fill();
        }

        void Start()
        {
            if (!enabled) return;
            ShowTitle();
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.escapeKey.wasPressedThisFrame)
            {
                if (modals.Count > 0) CloseTopModal();
                else if (CurrentScreen == GameScreen.Mission || CurrentScreen == GameScreen.Result) OpenPause();
            }
            if (CurrentScreen == GameScreen.Result && modals.Count == 0 && kb.spaceKey.wasPressedThisFrame)
                SkipResultAnimation();
        }

        // ───────── 화면 전환 ─────────

        void BeginScreen(GameScreen screen)
        {
            StopAllCoroutines();
            CloseAllModals();
            SetPaused(false);
            UIKit.Clear(screenRoot);
            UIKit.Clear(toastRoot);
            CurrentScreen = screen;
            resultView = null;
            tutorialStep = -1;
        }

        void Select(Selectable s)
        {
            if (!s || !EventSystem.current) return;
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(s.gameObject);
        }

        void SetPaused(bool value)
        {
            paused = value;
            Time.timeScale = value ? 0f : 1f;
        }

        public void StartNewGame()
        {
            Progress.Reset();
            StartMission(0);
        }

        // ───────── 공통 UI 조각 ─────────

        RectTransform Header(RectTransform parent, string title, string right = null)
        {
            var bar = UIKit.Panel(parent, "Header", Theme.Navy, ShapeSprites.Square).rectTransform;
            bar.Region(0, 0.9f, 1, 1);
            var t = UIKit.Text(bar, "Title", title, 38, Color.white, TextAlignmentOptions.MidlineLeft, true);
            t.rectTransform.Fill(40, 600, 0, 0);
            if (right != null)
            {
                var r = UIKit.Text(bar, "Right", right, 26, Theme.TealPale, TextAlignmentOptions.MidlineRight);
                r.rectTransform.Fill(600, 40, 0, 0);
            }
            return bar;
        }

        static string Mark(bool success) => success ? "○ 성공" : "△ 부분 해결";

        static string TypeName(ProductType type) => type switch
        {
            ProductType.Basic => "기본형",
            ProductType.Efficient => "고효율형",
            _ => "혁신형"
        };

        static Color TypeColor(ProductType type) => type switch
        {
            ProductType.Basic => Theme.NavySoft,
            ProductType.Efficient => Theme.Success,
            _ => Theme.Hex(0x7A5FC0)
        };

        static string F(double v) => Scoring.Format1(v);
    }
}
