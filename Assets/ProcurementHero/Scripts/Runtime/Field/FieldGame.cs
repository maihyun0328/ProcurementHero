using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ProcurementHero.Core.Field;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ProcurementHero.Field
{
    /// <summary>현장 수첩 버전의 화면 상태. 입력은 상태마다 허용되는 것만 받는다.</summary>
    public enum FieldState
    {
        Title,
        Explore,
        Notebook,
        Installing,
        Review,
        Result,
        Ending
    }

    /// <summary>
    /// 타이틀 → 현장 탐색 → 수첩 계획 → 확정 → 설치 → 재확인 → 별 평가 → 다음 현장 → 엔딩.
    /// 현장은 SpriteRenderer와 Collider2D로, 수첩과 HUD는 uGUI로 만든다.
    /// </summary>
    public partial class FieldGame : MonoBehaviour
    {
        [SerializeField] FieldDatabase database;
        [SerializeField] TMP_FontAsset font;
        [SerializeField] Canvas canvas;

        public const float MoveSpeed = 6f;

        public FieldDatabase Database => database;
        public FieldState State { get; private set; }
        public FieldProgress Progress { get; private set; }
        public int MissionIndex { get; private set; }
        public FieldMissionData Mission { get; private set; }
        public FieldMissionSpec Spec { get; private set; }
        public FieldPlan Plan { get; private set; } = new FieldPlan();
        public string SelectedSpot { get; private set; }
        public FieldWorld World => world;
        public IReadOnlyCollection<string> Observed => observed;
        public bool Paused => paused;
        public FieldEvaluation LastConfirmed { get; private set; }

        AudioService sound;
        Camera cam;
        Transform worldRoot;
        FieldWorld world;
        RectTransform screenRoot, hudRoot, overlayRoot, toastRoot;
        readonly HashSet<string> observed = new HashSet<string>();
        readonly Dictionary<int, HashSet<string>> observedByMission = new Dictionary<int, HashSet<string>>();
        bool paused;
        bool confirmLocked;
        bool reviewChecked;
        int stateVersion;
        int inputLockFrame = -1;
        HashSet<string> highlightUnsolved = new HashSet<string>();

        // HUD
        RectTransform magnifier, bubble, goalRow;
        TextMeshProUGUI bubbleText, observeCounter, toastText;
        Button notebookButton, resultButton, skipButton;
        Image notebookBadge;
        RectTransform reviewBanner;
        string bubbleFor;
        float bubbleTimer;

        void Awake()
        {
            if (!canvas) canvas = FindFirstObjectByType<Canvas>();
            if (!database || database.missions.Count == 0 || !canvas)
            {
                Debug.LogError("[ProcurementHero] FieldDatabase 또는 Canvas 참조가 없습니다. Tools/Procurement Hero/Setup Field Notebook을 실행하세요.");
                enabled = false;
                return;
            }
            UIKit.Font = font;
            Time.timeScale = 1f;
            Progress = new FieldProgress(database.missions.Count);
            cam = Camera.main;
            if (cam)
            {
                cam.orthographic = true;
                cam.orthographicSize = 9f;
                cam.transform.position = new Vector3(0, 0.6f, -10);
            }

            sound = gameObject.AddComponent<AudioService>();
            sound.Init();
            UIKit.OnButtonSound = () => sound.Play(Sfx.Click);

            worldRoot = new GameObject("World").transform;
            screenRoot = UIKit.Rect(canvas.transform, "Screens").Fill();
            hudRoot = UIKit.Rect(canvas.transform, "HUD").Fill();
            toastRoot = UIKit.Rect(canvas.transform, "Toasts").Fill();
            overlayRoot = UIKit.Rect(canvas.transform, "Overlays").Fill();
        }

        void Start()
        {
            if (enabled) ShowTitle();
        }

        /// <summary>현장 연출에 쓰는 시간. 수첩·일시정지·결과 창에서는 멈춘다.</summary>
        public float FieldDelta
        {
            get
            {
                if (paused || modalStack.Count > 0) return 0f;
                return State == FieldState.Explore || State == FieldState.Installing || State == FieldState.Review || State == FieldState.Title
                    ? Time.deltaTime : 0f;
            }
        }

        void SetState(FieldState next)
        {
            State = next;
            stateVersion++;
            // 같은 키 입력이 바뀐 상태에서 한 번 더 처리되지 않게 이번 프레임의 현장 입력을 막는다.
            inputLockFrame = Time.frameCount;
            if (world) world.MovePlayer(Vector2.zero, 0);
        }

        // ───────── 입력 ─────────

        static readonly Key[] TrackedKeys = { Key.Escape, Key.Tab, Key.E, Key.Enter, Key.NumpadEnter, Key.Space };
        readonly Dictionary<Key, bool> heldLastFrame = new Dictionary<Key, bool>();

        /// <summary>이번 프레임에 새로 눌렸는지. 직전 프레임의 눌림 상태와 비교해 한 번만 참이 된다.</summary>
        bool Pressed(Keyboard kb, Key key) => kb[key].isPressed && !(heldLastFrame.TryGetValue(key, out var held) && held);

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            HandleInput(kb);
            foreach (var key in TrackedKeys) heldLastFrame[key] = kb[key].isPressed;
        }

        void HandleInput(Keyboard kb)
        {
            UpdateBubble();
            if (Time.frameCount == inputLockFrame)
            {
                if (world) world.MovePlayer(Vector2.zero, 0);
                return;
            }

            if (Pressed(kb, Key.Escape))
            {
                inputLockFrame = Time.frameCount;
                if (modalStack.Count > 0) CloseTopModal();
                else if (State == FieldState.Notebook) CloseNotebook();
                else if (State == FieldState.Explore || State == FieldState.Installing || State == FieldState.Review) OpenPause();
                return;
            }

            bool canMove = !paused && modalStack.Count == 0 && (State == FieldState.Explore || State == FieldState.Review);
            if (world) world.MovePlayer(canMove ? ReadMove(kb) : Vector2.zero, MoveSpeed);
            UpdateMagnifier(canMove);
            if (paused || modalStack.Count > 0) return;

            switch (State)
            {
                case FieldState.Explore:
                    if (Pressed(kb, Key.Tab)) OpenNotebook();
                    else if (Pressed(kb, Key.E) || (EnterPressed(kb) && NothingSelected())) TryObserve();
                    else if (WorldClick()) TryObserveAtPointer();
                    break;
                case FieldState.Notebook:
                    if (Pressed(kb, Key.Tab)) CloseNotebook();
                    break;
                case FieldState.Installing:
                    if (Pressed(kb, Key.Space)) SkipInstall();
                    break;
                case FieldState.Review:
                    if (Pressed(kb, Key.E) || (EnterPressed(kb) && NothingSelected())) TryObserve();
                    else if (WorldClick()) TryObserveAtPointer();
                    break;
            }
        }

        static Vector2 ReadMove(Keyboard kb)
        {
            float x = 0, y = 0;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) y -= 1;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) y += 1;
            var v = new Vector2(x, y);
            return v.sqrMagnitude > 1f ? v.normalized : v;
        }

        bool EnterPressed(Keyboard kb) => Pressed(kb, Key.Enter) || Pressed(kb, Key.NumpadEnter);

        static bool NothingSelected()
        {
            var es = EventSystem.current;
            return !es || !es.currentSelectedGameObject || !es.currentSelectedGameObject.activeInHierarchy;
        }

        /// <summary>UI 위가 아닌 곳을 클릭했는지. UI 클릭은 현장으로 전달하지 않는다.</summary>
        static bool WorldClick()
        {
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return false;
            return !(EventSystem.current && EventSystem.current.IsPointerOverGameObject());
        }

        // ───────── 관찰 ─────────

        void TryObserveAtPointer()
        {
            if (!cam || !world) return;
            Vector2 p = cam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            var clicked = world.Problems.FirstOrDefault(pr =>
                Vector2.Distance(p, pr.observePoint) < 1.6f || Vector2.Distance(p, pr.residentPosition) < 1.0f);
            if (clicked == null) return;
            if (Vector2.Distance(world.PlayerPosition, clicked.observePoint) > FieldWorld.ObserveRange)
            {
                Toast("더 가까이 가야 살펴볼 수 있어요");
                return;
            }
            Observe(clicked);
        }

        public void TryObserve()
        {
            var target = world ? world.NearestInRange() : null;
            if (target != null) Observe(target);
        }

        void Observe(FieldProblemEntry problem)
        {
            if (State == FieldState.Review)
            {
                bool solved = LastConfirmed != null && LastConfirmed.IsSolved(problem.id);
                ShowBubble(problem, solved ? problem.solvedLine : problem.line);
                sound.Play(Sfx.Observe);
                if (!reviewChecked)
                {
                    reviewChecked = true;
                    ShowResultButton();
                }
                return;
            }
            if (State != FieldState.Explore) return;

            ShowBubble(problem, problem.line);
            if (!observed.Add(problem.id)) return; // 다시 봐도 기록은 늘지 않는다.
            world.MarkObserved(problem.id, true);
            sound.Play(Sfx.Observe);
            Toast($"수첩에 '{problem.displayName}'을(를) 기록했어요");
            StartCoroutine(FlyToNotebook(problem));
            if (notebookBadge) notebookBadge.gameObject.SetActive(true);
            RefreshHud();
        }

        /// <summary>테스트와 자동 확인용: 플레이어를 문제 앞으로 옮긴 뒤 관찰한다.</summary>
        public void ObserveForTest(string problemId)
        {
            var p = Mission.Problem(problemId);
            world.Player.position = world.WorldPoint(p.observePoint + new Vector2(0, -0.8f));
            world.Player.transform.position = world.Player.position;
            Observe(p);
        }

        // ───────── 미션 ─────────

        void BeginScreen(FieldState next)
        {
            StopAllCoroutines();
            CloseAllModals();
            SetPaused(false);
            UIKit.Clear(screenRoot);
            UIKit.Clear(hudRoot);
            UIKit.Clear(toastRoot);
            UIKit.Clear(overlayRoot);
            magnifier = bubble = goalRow = reviewBanner = null;
            notebookButton = resultButton = skipButton = null;
            notebookRoot = null;
            if (world) Destroy(world.gameObject);
            world = null;
            SetState(next);
        }

        public void StartMission(int index, bool replan = false)
        {
            BeginScreen(FieldState.Explore);
            MissionIndex = index;
            Mission = database.missions[index];
            Spec = Mission.ToSpec();
            confirmLocked = false;
            reviewChecked = false;

            if (!observedByMission.TryGetValue(index, out var saved)) observedByMission[index] = saved = new HashSet<string>();
            observed.Clear();
            if (replan) observed.UnionWith(saved);
            else saved.Clear();

            var previous = Progress.Get(index);
            if (replan && previous != null)
            {
                // 이전 계획을 고쳐 쓸 수 있게 복사한다. 비용과 설치물은 누적하지 않는다.
                Plan = new FieldPlan();
                foreach (var pair in previous.Assignments) Plan.Toggle(Spec, pair.Key, pair.Value);
                highlightUnsolved = new HashSet<string>(Mission.problems.Where(p => !previous.IsSolved(p.id)).Select(p => p.id));
            }
            else
            {
                Plan = new FieldPlan();
                highlightUnsolved.Clear();
            }
            SelectedSpot = Mission.spots.Count > 0 ? Mission.spots[0].id : null;

            world = FieldWorld.Build(worldRoot, Mission, true);
            world.Delta = () => FieldDelta;
            foreach (var id in observed) world.MarkObserved(id, true);
            BuildHud();
            if (replan) OpenNotebook();
            else StartCoroutine(Intro());
        }

        IEnumerator Intro()
        {
            var banner = UIKit.Panel(toastRoot, "Intro", Theme.WithAlpha(Theme.Navy, 0.92f));
            banner.rectTransform.Region(0.25f, 0.62f, 0.75f, 0.8f);
            var t = UIKit.Text(banner.transform, "Text",
                $"<size=26><color=#{ColorUtility.ToHtmlStringRGB(Theme.TealPale)}>현장 {MissionIndex + 1} · {Mission.placeName}</color></size>\n<b>{Mission.title}</b>\n<size=24>{Mission.intro}</size>",
                40, Color.white, TextAlignmentOptions.Center);
            t.rectTransform.Fill(20, 20, 10, 10);
            var group = banner.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            float time = 0;
            while (time < 3.2f && banner)
            {
                time += Time.unscaledDeltaTime;
                group.alpha = time > 2.6f ? (3.2f - time) / 0.6f : Mathf.Clamp01(time / 0.25f);
                yield return null;
            }
            if (banner) Destroy(banner.gameObject);
        }

        /// <summary>확정: 배정 목록을 결과 스냅샷으로 고정하고 설치를 시작한다. 연타는 무시한다.</summary>
        public bool ConfirmPlan()
        {
            if (confirmLocked || State != FieldState.Notebook) return false;
            if (FieldRules.CanConfirm(Spec, Plan, observed) != ConfirmBlock.None) return false;
            confirmLocked = true;
            var snapshot = FieldRules.Evaluate(Spec, Plan);
            LastConfirmed = snapshot;
            Progress.Set(MissionIndex, snapshot);
            observedByMission[MissionIndex] = new HashSet<string>(observed);
            sound.Play(Sfx.Confirm);
            HideNotebook();
            SetState(FieldState.Installing);
            BuildInstallHud();
            world.PlayInstall(snapshot, s => sound.Play(s), OnInstalled);
            return true;
        }

        public void SkipInstall()
        {
            if (State == FieldState.Installing && world) world.SkipInstall();
        }

        void OnInstalled()
        {
            if (State != FieldState.Installing) return;
            SetState(FieldState.Review);
            if (skipButton) Destroy(skipButton.gameObject);
            skipButton = null;
            reviewBanner = UIKit.Panel(hudRoot, "ReviewBanner", Theme.WithAlpha(Theme.Navy, 0.9f)).rectTransform;
            reviewBanner.Region(0.3f, 0.82f, 0.7f, 0.9f);
            var t = UIKit.Text(reviewBanner, "Text", "설치 완료! 바뀐 현장을 걸어 보고, 문제였던 곳을 다시 살펴봐요 (E)", 24, Color.white, TextAlignmentOptions.Center);
            t.rectTransform.Fill(14, 14, 4, 4);
            RefreshHud();
        }

        void ShowResultButton()
        {
            if (resultButton) return;
            resultButton = UIKit.Button(hudRoot, "ShowResult", "결과 보기", ShowResult, ButtonStyle.Primary, 32);
            ((RectTransform)resultButton.transform).Region(0.4f, 0.03f, 0.6f, 0.11f);
            resultButton.gameObject.AddComponent<Pulse>();
            Select(resultButton);
        }

        // ───────── HUD ─────────

        void BuildHud()
        {
            var top = UIKit.Panel(hudRoot, "TopBar", Theme.WithAlpha(Theme.Navy, 0.88f), ShapeSprites.Square).rectTransform;
            top.Region(0, 0.925f, 1, 1);
            var title = UIKit.Text(top, "Title", $"현장 {MissionIndex + 1} · {Mission.title}", 30, Color.white, TextAlignmentOptions.MidlineLeft, true);
            title.rectTransform.Region(0, 0, 0.3f, 1).offsetMin = new Vector2(28, 0);

            goalRow = UIKit.Rect(top, "Goals").Region(0.3f, 0.06f, 0.72f, 0.94f);
            UIKit.HorizontalList(goalRow, 12, null, TextAnchor.MiddleCenter, false);

            var budget = UIKit.Rect(top, "Budget").Region(0.72f, 0.1f, 0.86f, 0.9f);
            UIKit.Shape(budget, "Coin", ShapeSprites.Circle, Theme.Hex(0xF2C14E), new Vector2(0, 0.5f), new Vector2(26, 0), new Vector2(40, 40));
            UIKit.Text(budget, "Text", $"예산 {Mission.budget}", 28, Color.white, TextAlignmentOptions.MidlineLeft, true).rectTransform.Fill(56, 0, 0, 0);

            var help = UIKit.Button(top, "Help", "도움말", OpenHowTo, ButtonStyle.Ghost, 22);
            ((RectTransform)help.transform).Region(0.86f, 0.15f, 0.925f, 0.85f);
            var pause = UIKit.Button(top, "Pause", "일시정지", OpenPause, ButtonStyle.Ghost, 22);
            ((RectTransform)pause.transform).Region(0.93f, 0.15f, 0.995f, 0.85f);
            Deselectable(help, pause);

            var counter = UIKit.Panel(hudRoot, "ObserveCounter", Theme.WithAlpha(Theme.Paper, 0.94f)).rectTransform;
            counter.Place(new Vector2(0, 0), new Vector2(24, 24), new Vector2(300, 84), new Vector2(0, 0));
            var mag = FieldArt.Magnifier(counter, 60);
            mag.Place(new Vector2(0, 0.5f), new Vector2(46, 0), new Vector2(60, 60));
            observeCounter = UIKit.Text(counter, "Text", "", 26, Theme.Navy, TextAlignmentOptions.MidlineLeft, true);
            observeCounter.rectTransform.Fill(88, 10, 0, 0);

            notebookButton = UIKit.Button(hudRoot, "NotebookButton", "", OpenNotebook, ButtonStyle.Secondary);
            Destroy(notebookButton.transform.Find("Label").gameObject);
            var nb = (RectTransform)notebookButton.transform;
            nb.Place(new Vector2(1, 0), new Vector2(-24, 24), new Vector2(250, 96), new Vector2(1, 0));
            var cover = UIKit.Panel(nb, "BookIcon", Theme.Hex(0x7A4E2D)).rectTransform;
            cover.Place(new Vector2(0, 0.5f), new Vector2(50, 0), new Vector2(58, 70));
            UIKit.Panel(cover, "Page", Theme.Hex(0xFFF9EC)).rectTransform.Fill(8, 6, 8, 8);
            UIKit.Text(nb, "Label", "수첩 (Tab)", 28, Theme.Navy, TextAlignmentOptions.MidlineLeft, true).rectTransform.Fill(96, 10, 0, 0);
            notebookBadge = UIKit.Shape(nb, "NewBadge", ShapeSprites.Circle, Theme.Danger, new Vector2(1, 1), new Vector2(-12, -12), new Vector2(26, 26));
            notebookBadge.gameObject.SetActive(false);
            Deselectable(notebookButton);

            magnifier = UIKit.Rect(hudRoot, "ObserveTarget");
            magnifier.sizeDelta = new Vector2(150, 110);
            var magButton = magnifier.gameObject.AddComponent<Button>();
            var magBg = magnifier.gameObject.AddComponent<Image>();
            magBg.sprite = ShapeSprites.Rounded;
            magBg.type = Image.Type.Sliced;
            magBg.color = Theme.WithAlpha(Theme.Paper, 0.95f);
            magButton.targetGraphic = magBg;
            magButton.onClick.AddListener(TryObserve);
            Deselectable(magButton);
            FieldArt.Magnifier(magnifier, 64).Place(new Vector2(0.5f, 1f), new Vector2(0, -38), new Vector2(64, 64));
            UIKit.Text(magnifier, "Key", "E 살펴보기", 20, Theme.Navy, TextAlignmentOptions.Center, true).rectTransform.Region(0, 0, 1, 0.34f);
            magnifier.gameObject.SetActive(false);

            bubble = UIKit.Panel(hudRoot, "Bubble", Theme.Paper).rectTransform;
            bubble.sizeDelta = new Vector2(440, 76);
            bubble.gameObject.AddComponent<Outline>().effectColor = Theme.WithAlpha(Theme.Navy, 0.4f);
            bubbleText = UIKit.Text(bubble, "Text", "", 26, Theme.Navy, TextAlignmentOptions.Center);
            bubbleText.rectTransform.Fill(14, 14, 4, 4);
            bubbleText.enableAutoSizing = true;
            bubbleText.fontSizeMin = 18;
            bubbleText.fontSizeMax = 26;
            bubble.gameObject.SetActive(false);

            RefreshHud();
        }

        /// <summary>현장 HUD 버튼은 마우스로만 누르고 키보드 선택을 갖지 않는다(Enter가 관찰과 겹치지 않게).</summary>
        static void Deselectable(params Selectable[] items)
        {
            foreach (var s in items) s.navigation = new Navigation { mode = Navigation.Mode.None };
        }

        void RefreshHud()
        {
            if (!goalRow) return;
            UIKit.Clear(goalRow);
            foreach (var p in Mission.problems)
            {
                var cell = UIKit.Rect(goalRow, "Goal_" + p.id);
                UIKit.Layout(cell, -1, 150);
                bool seen = observed.Contains(p.id);
                bool solved = (State == FieldState.Review || State == FieldState.Result) && LastConfirmed != null && LastConfirmed.IsSolved(p.id);
                FieldArt.ProblemIcon(cell, p.kind, 46).Place(new Vector2(0, 0.5f), new Vector2(26, 0), new Vector2(46, 46));
                string label = (p.required ? "" : "추가 ") + p.displayName;
                var text = UIKit.Text(cell, "Name", label, 20, seen ? Color.white : Theme.WithAlpha(Color.white, 0.6f), TextAlignmentOptions.MidlineLeft, true);
                text.rectTransform.Fill(54, 0, 0, 0);
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.enableAutoSizing = true;
                text.fontSizeMin = 14;
                text.fontSizeMax = 20;
                if (solved) FieldArt.Tick(cell, 30, Theme.Success).Place(new Vector2(0, 0.5f), new Vector2(42, -14), new Vector2(30, 30));
                else if (!seen && p.mustObserve)
                    UIKit.Text(cell, "Unknown", "?", 28, Theme.Warning, TextAlignmentOptions.Center, true).rectTransform.Place(new Vector2(0, 0.5f), new Vector2(42, -14), new Vector2(30, 30));
            }
            int need = Mission.problems.Count(p => p.mustObserve);
            int done = Mission.problems.Count(p => p.mustObserve && observed.Contains(p.id));
            if (observeCounter) observeCounter.text = done < need ? $"관찰 {done}/{need}" : "관찰 완료";
        }

        void BuildInstallHud()
        {
            skipButton = UIKit.Button(hudRoot, "SkipInstall", "설치 건너뛰기 (Space)", SkipInstall, ButtonStyle.Ghost, 26);
            // 배송 트럭이 다니는 아래쪽 길을 가리지 않도록 위쪽에 둔다.
            ((RectTransform)skipButton.transform).Region(0.38f, 0.82f, 0.62f, 0.9f);
            Select(skipButton);
        }

        void UpdateMagnifier(bool canAct)
        {
            if (!magnifier || !world) return;
            var target = canAct ? world.NearestInRange() : null;
            magnifier.gameObject.SetActive(target != null);
            if (target != null) magnifier.anchoredPosition = ToCanvas(target.observePoint + new Vector2(2.2f, 0.6f));
        }

        void ShowBubble(FieldProblemEntry p, string line)
        {
            if (!bubble) return;
            bubbleFor = p.id;
            bubbleTimer = 3.5f;
            bubbleText.text = line;
            bubble.gameObject.SetActive(true);
        }

        void UpdateBubble()
        {
            if (!bubble || !bubble.gameObject.activeSelf || world == null) return;
            bubbleTimer -= Time.unscaledDeltaTime;
            var p = Mission.Problem(bubbleFor);
            if (bubbleTimer <= 0 || p == null || (State != FieldState.Explore && State != FieldState.Review))
            {
                bubble.gameObject.SetActive(false);
                return;
            }
            bubble.anchoredPosition = ToCanvas(p.residentPosition + new Vector2(0, 1.7f));
        }

        Vector2 ToCanvas(Vector2 worldLocal)
        {
            var screen = cam.WorldToScreenPoint(world.WorldPoint(worldLocal));
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform, screen, null, out var local);
            return local;
        }

        IEnumerator FlyToNotebook(FieldProblemEntry p)
        {
            if (!notebookButton) yield break;
            var icon = FieldArt.ProblemIcon(toastRoot, p.kind, 70);
            Vector2 from = ToCanvas(p.observePoint);
            Vector2 to = ((RectTransform)notebookButton.transform).position;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform, RectTransformUtility.WorldToScreenPoint(null, to), null, out to);
            float t = 0;
            while (t < 0.6f && icon)
            {
                t += Time.unscaledDeltaTime;
                float e = Mathf.SmoothStep(0, 1, t / 0.6f);
                icon.anchoredPosition = Vector2.Lerp(from, to, e) + new Vector2(0, Mathf.Sin(e * Mathf.PI) * 120);
                icon.localScale = Vector3.one * Mathf.Lerp(1.2f, 0.5f, e);
                yield return null;
            }
            if (icon) Destroy(icon.gameObject);
        }

        Coroutine toastRoutine;

        void Toast(string text)
        {
            if (toastRoutine != null) StopCoroutine(toastRoutine);
            toastRoutine = StartCoroutine(ToastRoutine(text));
        }

        IEnumerator ToastRoutine(string text)
        {
            var old = toastRoot.Find("Toast");
            if (old) Destroy(old.gameObject);
            var panel = UIKit.Panel(toastRoot, "Toast", Theme.WithAlpha(Theme.Teal, 0.95f));
            panel.rectTransform.Region(0.32f, 0.84f, 0.68f, 0.905f);
            var group = panel.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            toastText = UIKit.Text(panel.transform, "Text", text, 26, Color.white, TextAlignmentOptions.Center, true);
            toastText.rectTransform.Fill(12, 12, 2, 2);
            float time = 0;
            while (time < 2.4f && panel)
            {
                time += Time.unscaledDeltaTime;
                group.alpha = time > 1.9f ? (2.4f - time) / 0.5f : 1f;
                yield return null;
            }
            if (panel) Destroy(panel.gameObject);
        }

        // ───────── 키보드 선택 ─────────

        void Select(Selectable s)
        {
            var es = EventSystem.current;
            if (!es) return;
            es.SetSelectedGameObject(null);
            if (s && s.gameObject.activeInHierarchy) es.SetSelectedGameObject(s.gameObject);
        }

        /// <summary>
        /// 다음 프레임에 선택을 옮긴다. 같은 Enter가 새 버튼까지 누르지 않게 하기 위해서다.
        /// 그 사이 화면 상태가 바뀌었거나 사용자가 다른 활성 UI를 골랐다면 아무것도 하지 않는다.
        /// </summary>
        void SelectNextFrame(Func<Selectable> pick)
        {
            StartCoroutine(SelectLater(pick, stateVersion));
        }

        IEnumerator SelectLater(Func<Selectable> pick, int version)
        {
            yield return null;
            if (version != stateVersion) yield break;
            var es = EventSystem.current;
            if (!es) yield break;
            var current = es.currentSelectedGameObject;
            bool lost = !current || !current.activeInHierarchy || (current.TryGetComponent(out Selectable cs) && !cs.IsInteractable());
            if (!lost) yield break;
            var target = pick();
            if (target && target.gameObject.activeInHierarchy && target.IsInteractable()) Select(target);
        }

        void SetPaused(bool value)
        {
            paused = value;
            Time.timeScale = value ? 0f : 1f;
            if (world) world.MovePlayer(Vector2.zero, 0);
        }
    }

    /// <summary>눈에 띄어야 하는 버튼을 살짝 커졌다 작아지게 한다(일시정지 중에도 동작).</summary>
    public class Pulse : MonoBehaviour
    {
        void Update() => transform.localScale = Vector3.one * (1f + Mathf.Sin(Time.unscaledTime * 4f) * 0.04f);
    }
}
