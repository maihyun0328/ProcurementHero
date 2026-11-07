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
        Arriving,
        Explore,
        Notebook,
        Installing,
        /// <summary>계획 승인·설치 뒤 시간이 흐르고 예측한 상황이 일어나 시설이 대응하는 단계. 이동·관찰·결과 진입은 막는다.</summary>
        Scenario,
        Review,
        Result,
        Ending
    }

    /// <summary>
    /// 타이틀 → 출동·예측 확인 → 현장 탐색·관찰 → 수첩 계획(가상 실증) → 승인 → 배송·설치 → 시간 경과·상황 발생·시설 대응
    /// → 재확인 → 별 평가 → 재계획 또는 다음 현장 → 엔딩.
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
        /// <summary>화면이 바뀔 때마다 늘어난다. 이전 화면의 연출 콜백이 늦게 와도 무시하기 위해 쓴다.</summary>
        int screenToken;
        bool notebookBusy;
        readonly HashSet<int> arrivedMissions = new HashSet<int>();
        int inputLockFrame = -1;
        HashSet<string> highlightUnsolved = new HashSet<string>();

        // HUD
        RectTransform magnifier, bubble, goalRow;
        TextMeshProUGUI bubbleText, observeCounter, toastText;
        Button notebookButton, resultButton, skipButton;
        Image notebookBadge;
        RectTransform reviewBanner;
        Func<Vector2> bubbleAnchor;
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
            ResetCamera();

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
            if (!enabled) return;
            LoadProgress();
            ShowTitle();
        }

        /// <summary>현장 연출에 쓰는 시간. 수첩·일시정지·결과 창에서는 멈춘다.</summary>
        public float FieldDelta
        {
            get
            {
                if (paused || modalStack.Count > 0) return 0f;
                return State == FieldState.Explore || State == FieldState.Installing || State == FieldState.Scenario || State == FieldState.Review
                       || State == FieldState.Title || State == FieldState.Arriving || State == FieldState.Ending
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
            KeepSelection(kb);
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
                else if (State == FieldState.Notebook && recommendPanel) CloseRecommend();
                else if (State == FieldState.Notebook) CloseNotebook();
                else if (State == FieldState.Explore || State == FieldState.Installing || State == FieldState.Scenario || State == FieldState.Review || State == FieldState.Arriving) OpenPause();
                return;
            }

            bool canMove = !paused && modalStack.Count == 0 && (State == FieldState.Explore || State == FieldState.Review);
            if (world) world.MovePlayer(canMove ? ReadMove(kb) : Vector2.zero, MoveSpeed);
            UpdateMagnifier(canMove);
            if (paused || modalStack.Count > 0) return;

            switch (State)
            {
                case FieldState.Arriving:
                    if (Pressed(kb, Key.Space) || EnterPressed(kb) || Pressed(kb, Key.E) || WorldClick()) SkipArrival();
                    break;
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
                case FieldState.Scenario:
                    if (Pressed(kb, Key.Space) || EnterPressed(kb)) SkipScenario();
                    break;
                case FieldState.Review:
                    if (Pressed(kb, Key.E) || (EnterPressed(kb) && NothingSelected())) TryObserve();
                    else if (WorldClick()) TryObserveAtPointer();
                    break;
                case FieldState.Ending:
                    if (EndingPlaying && (Pressed(kb, Key.Space) || EnterPressed(kb))) SkipEnding();
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
            // 주민이 옮겨 갔다면 지금 서 있는 자리를 기준으로 판정한다.
            var clicked = world.Problems.FirstOrDefault(pr =>
                Vector2.Distance(p, world.ObservePoint(pr)) < 1.6f || Vector2.Distance(p, world.ResidentPosition(pr.id)) < 1.0f);
            if (clicked == null) return;
            if (Vector2.Distance(world.PlayerPosition, world.ObservePoint(clicked)) > FieldWorld.ObserveRange)
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
                world.PlayObserve(problem);
                // 상황이 지난 뒤의 대사: 해결된 곳은 해결 대사, 남은 곳은 상황 대사(판정 결과가 정한 화면 상태를 따른다).
                ShowBubble(problem, world.LineFor(problem));
                sound.Play(Sfx.Observe);
                if (!reviewChecked)
                {
                    reviewChecked = true;
                    ShowResultButton();
                }
                return;
            }
            if (State != FieldState.Explore) return;

            ShowBubble(problem, world.LineFor(problem));
            if (!observed.Add(problem.id)) return; // 다시 봐도 기록이나 기록 연출은 반복하지 않는다.
            if (!observedByMission.TryGetValue(MissionIndex, out var keep)) observedByMission[MissionIndex] = keep = new HashSet<string>();
            keep.Add(problem.id);
            MarkSaveDirty();
            // 담당자가 몸을 숙여 살피고 → 주민이 반응하고 → 기록 그림이 수첩으로 날아가 → 관찰 수가 오른다.
            world.PlayObserve(problem);
            world.MarkObserved(problem.id, true);
            sound.Play(Sfx.Observe);
            StartCoroutine(FlyToNotebook(problem));
        }

        /// <summary>테스트와 자동 확인용: 플레이어를 문제 앞으로 옮긴 뒤 관찰한다.</summary>
        public void ObserveForTest(string problemId)
        {
            var p = Mission.Problem(problemId);
            if (State == FieldState.Arriving) SkipArrival();
            world.Player.position = world.WorldPoint(world.ObservePoint(p) + new Vector2(0, -0.8f));
            world.Player.transform.position = world.Player.position;
            Observe(p);
        }

        // ───────── 미션 ─────────

        // ───────── 진행 자동 저장 ─────────

        FieldSavePhase savePhase = FieldSavePhase.Explore;
        bool saveDirty;
        /// <summary>타이틀의 '이어하기'가 쓸 복원 정보(저장 파일 또는 지금 진행에서 만든 것).</summary>
        FieldRestored resume;

        public FieldSavePhase SavePhase => savePhase;
        public FieldRestored Resume => resume;

        /// <summary>바뀐 진행을 표시만 해 두고 그 프레임 끝(LateUpdate)에 한 번 저장한다. 매 프레임 쓰지 않는다.</summary>
        void MarkSaveDirty() => saveDirty = true;

        void LateUpdate()
        {
            if (saveDirty) SaveProgress();
        }

        IEnumerable<string> ObservedFor(int index) =>
            observedByMission.TryGetValue(index, out var set) ? set : Enumerable.Empty<string>();

        public void SaveProgress()
        {
            saveDirty = false;
            if (!database) return;
            var data = FieldSave.Capture(database.missions, Progress, ObservedFor, MissionIndex, savePhase, Plan, SelectedSpot);
            FieldSave.Store.Write(FieldSave.ToJson(data));
        }

        void FlushSave()
        {
            if (saveDirty && enabled) SaveProgress();
        }

        // 종료 이벤트 하나에만 기대지 않는다: 진행이 바뀔 때마다 저장하고, 포커스를 잃거나 멈출 때도 남은 변경을 쓴다.
        void OnApplicationQuit() => FlushSave();
        void OnApplicationPause(bool pause) { if (pause) FlushSave(); }
        void OnApplicationFocus(bool focus)
        {
            if (!focus) FlushSave();
            else RestoreSelection();
        }

        // ───────── 키보드 선택 유지 ─────────

        GameObject lastSelected;

        /// <summary>버튼으로만 조작하는 화면(타이틀·수첩·결과·엔딩·창)인지. 현장 이동 화면은 선택을 일부러 비워 둔다.</summary>
        bool MenuScreen => modalStack.Count > 0 || State == FieldState.Title || State == FieldState.Notebook || State == FieldState.Result || State == FieldState.Ending;

        /// <summary>
        /// 창 전환(Alt+Tab 등)이나 빈 곳 클릭으로 키보드 선택이 사라지면 Enter·방향키가 아무것도 하지 않게 된다.
        /// 버튼 화면에서 선택이 비어 있을 때 키를 누르면 직전에 고른 버튼(아직 보이고 누를 수 있을 때)을 다시 고른다.
        /// </summary>
        void KeepSelection(Keyboard kb)
        {
            var es = EventSystem.current;
            if (!es) return;
            var current = es.currentSelectedGameObject;
            if (current && current.activeInHierarchy)
            {
                lastSelected = current;
                return;
            }
            if (!MenuScreen) return;
            bool navKey = kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame
                          || kb.upArrowKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame;
            if (navKey) RestoreSelection();
        }

        void RestoreSelection()
        {
            var es = EventSystem.current;
            if (!es || !MenuScreen || (es.currentSelectedGameObject && es.currentSelectedGameObject.activeInHierarchy)) return;
            if (lastSelected && lastSelected.activeInHierarchy && lastSelected.TryGetComponent(out Selectable s) && s.IsInteractable()
                && s.navigation.mode != Navigation.Mode.None)
                es.SetSelectedGameObject(lastSelected);
        }

        /// <summary>저장 파일을 읽어 검증하고 메모리 진행에 적용한다. 손상·구버전이면 새 게임처럼 시작한다.</summary>
        public void LoadProgress()
        {
            var text = FieldSave.Store.Read();
            var data = FieldSave.Parse(text);
            var restored = FieldSave.Restore(database.missions, data);
            if (!string.IsNullOrEmpty(text) && restored == null)
                Debug.LogWarning("[ProcurementHero] 진행 저장을 쓸 수 없어(손상 또는 다른 버전) 새로 시작합니다. 파일은 다음 저장 때 교체됩니다.");
            if (restored != null)
                foreach (var w in restored.Warnings) Debug.LogWarning("[ProcurementHero] 진행 복원: " + w);
            ApplyRestored(restored);
        }

        void ApplyRestored(FieldRestored r)
        {
            Progress.Reset();
            observedByMission.Clear();
            resume = r;
            saveDirty = false;
            if (r == null)
            {
                MissionIndex = 0;
                savePhase = FieldSavePhase.Explore;
                Plan = new FieldPlan();
                return;
            }
            for (int i = 0; i < r.Results.Length; i++)
            {
                if (r.Results[i] != null) Progress.Set(i, r.Results[i]);
                observedByMission[i] = new HashSet<string>(r.Observed[i]);
            }
            MissionIndex = r.Current;
            Mission = database.missions[r.Current];
            Spec = Mission.ToSpec();
            savePhase = r.Phase;
            Plan = r.Draft ?? new FieldPlan();
            SelectedSpot = r.SelectedSpot;
            LastConfirmed = Progress.Get(r.Current);
        }

        /// <summary>지금 진행을 저장 형식으로 만들었다가 같은 검증으로 되살린다(앱을 다시 켰을 때와 같은 이어하기).</summary>
        void RefreshResume()
        {
            if (!database) return;
            resume = FieldSave.Restore(database.missions, FieldSave.Capture(database.missions, Progress, ObservedFor, MissionIndex, savePhase, Plan, SelectedSpot));
        }

        /// <summary>타이틀 '이어하기': 저장된 단계 그대로 되살린다(연출 중간 프레임은 되살리지 않는다).</summary>
        public void ContinueGame()
        {
            var r = resume;
            if (r == null)
            {
                StartMission(0);
                return;
            }
            switch (r.Phase)
            {
                case FieldSavePhase.Review:
                    StartMissionReview(r.Current);
                    break;
                case FieldSavePhase.Replan:
                    StartMission(r.Current, replan: true, draft: r.Draft, spot: r.SelectedSpot);
                    break;
                case FieldSavePhase.Complete:
                    ShowEnding();
                    break;
                default:
                    StartMission(r.Current, draft: r.Draft, spot: r.SelectedSpot, keepObserved: true);
                    break;
            }
        }

        /// <summary>확정 결과를 바로 적용한 재확인 상태로 현장을 연다(설치 도중·결과 화면에서 종료했던 경우).</summary>
        void StartMissionReview(int index)
        {
            var result = Progress.Get(index);
            if (result == null)
            {
                StartMission(index, keepObserved: true);
                return;
            }
            BeginScreen(FieldState.Installing);
            MissionIndex = index;
            Mission = database.missions[index];
            Spec = Mission.ToSpec();
            confirmLocked = true;
            reviewChecked = false;
            LastConfirmed = result;
            observed.Clear();
            if (observedByMission.TryGetValue(index, out var saved)) observed.UnionWith(saved);
            Plan = new FieldPlan();
            foreach (var pair in result.Assignments) Plan.Toggle(Spec, pair.Key, pair.Value);
            SelectedSpot = Mission.spots.Count > 0 ? Mission.spots[0].id : null;
            highlightUnsolved.Clear();
            savePhase = FieldSavePhase.Review;
            arrivedMissions.Add(index);
            world = FieldWorld.Build(worldRoot, Mission, true);
            world.Delta = () => FieldDelta;
            foreach (var id in observed) world.MarkObserved(id, true);
            world.ApplyInstant(result);
            BuildHud();
            EnterReview();
        }

        /// <summary>현장 카메라 기본 구도(엔딩에서 잠깐 다가갔다가 화면이 바뀌면 되돌린다).</summary>
        void ResetCamera()
        {
            if (!cam) return;
            cam.orthographic = true;
            cam.orthographicSize = 9f;
            cam.transform.position = new Vector3(0, 0.6f, -10);
        }

        void BeginScreen(FieldState next)
        {
            StopAllCoroutines();
            ResetCamera();
            CloseAllModals();
            SetPaused(false);
            UIKit.Clear(screenRoot);
            UIKit.Clear(hudRoot);
            UIKit.Clear(toastRoot);
            UIKit.Clear(overlayRoot);
            screenToken++;
            notebookBusy = false;
            magnifier = bubble = goalRow = reviewBanner = null;
            notebookButton = resultButton = skipButton = null;
            notebookRoot = null;
            scenarioPanel = installBanner = null;
            recommendPanel = null;
            if (world) Destroy(world.gameObject);
            world = null;
            SetState(next);
        }

        /// <param name="draft">이어하기: 확정 전(또는 재계획 중) 계획을 그대로 되살린다.</param>
        /// <param name="keepObserved">이어하기: 이 현장에서 이미 관찰한 기록을 지우지 않는다.</param>
        public void StartMission(int index, bool replan = false, FieldPlan draft = null, string spot = null, bool keepObserved = false)
        {
            BeginScreen(FieldState.Explore);
            MissionIndex = index;
            Mission = database.missions[index];
            Spec = Mission.ToSpec();
            confirmLocked = false;
            reviewChecked = false;

            if (!observedByMission.TryGetValue(index, out var saved)) observedByMission[index] = saved = new HashSet<string>();
            observed.Clear();
            if (replan || keepObserved) observed.UnionWith(saved);
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
            if (draft != null)
            {
                Plan = new FieldPlan();
                foreach (var pair in draft.Assignments) Plan.Toggle(Spec, pair.Key, pair.Value);
            }
            SelectedSpot = spot != null && Mission.Spot(spot) != null ? spot : Mission.spots.Count > 0 ? Mission.spots[0].id : null;
            savePhase = replan ? FieldSavePhase.Replan : FieldSavePhase.Explore;
            MarkSaveDirty();

            world = FieldWorld.Build(worldRoot, Mission, true);
            world.Delta = () => FieldDelta;
            foreach (var id in observed) world.MarkObserved(id, true);
            BuildHud();
            if (replan)
            {
                // 재계획은 도착 연출 없이 바로 수첩을 연다.
                OpenNotebook();
                return;
            }
            // 처음 오는 현장은 차량 도착부터, 다시 온 현장은 짧게.
            bool fast = arrivedMissions.Contains(index) || index > 0;
            arrivedMissions.Add(index);
            SetState(FieldState.Arriving);
            int token = screenToken;
            StartCoroutine(Intro());
            BuildArrivalSkip();
            world.PlayArrival(fast, s => sound.Play(s),
                () => { if (token == screenToken) ShowPlayerBubble(string.IsNullOrEmpty(Mission.arrivalLine) ? "어디가 불편한지 살펴볼까?" : Mission.arrivalLine); },
                () => { if (token == screenToken) OnArrived(); });
        }

        Button arrivalSkip;

        void BuildArrivalSkip()
        {
            arrivalSkip = UIKit.Button(hudRoot, "SkipArrival", "건너뛰기 (Space)", SkipArrival, ButtonStyle.Ghost, 24);
            ((RectTransform)arrivalSkip.transform).Region(0.42f, 0.82f, 0.58f, 0.89f);
            Deselectable(arrivalSkip);
        }

        public void SkipArrival()
        {
            if (State == FieldState.Arriving && world) world.SkipArrival();
        }

        void OnArrived()
        {
            if (State != FieldState.Arriving) return;
            if (arrivalSkip) Destroy(arrivalSkip.gameObject);
            arrivalSkip = null;
            SetState(FieldState.Explore);
            RefreshHud();
        }

        /// <summary>
        /// 출동 알림: 현장 이름과 가상 AI 수첩 '내일'의 예측 한 줄. 예측은 미션 데이터에 정의된 문장이며 실제 AI 분석이 아니다.
        /// 계획을 승인하기 전에는 시간이 흐르지 않으므로 카운트다운은 없다.
        /// </summary>
        IEnumerator Intro()
        {
            var banner = UIKit.Panel(toastRoot, "Intro", Theme.WithAlpha(Theme.Navy, 0.92f));
            banner.rectTransform.Region(0.24f, 0.6f, 0.76f, 0.8f);
            FieldArt.ForecastIcon(banner.transform, 72).Place(new Vector2(0, 0.5f), new Vector2(64, 0), new Vector2(72, 72));
            var t = UIKit.Text(banner.transform, "Text",
                $"<size=24><color=#{ColorUtility.ToHtmlStringRGB(Theme.TealPale)}>현장 {MissionIndex + 1} · {Mission.placeName} · 지금 {Mission.nowTime}</color></size>\n<b>{Mission.title}</b>\n" +
                $"<size=24><color=#{ColorUtility.ToHtmlStringRGB(Theme.Hex(0xF2C14E))}>AI 수첩 '내일' 예측</color>  {Mission.forecast}</size>",
                36, Color.white, TextAlignmentOptions.Center);
            t.rectTransform.Fill(120, 20, 10, 10);
            t.enableAutoSizing = true;
            t.fontSizeMin = 22;
            t.fontSizeMax = 36;
            var group = banner.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            float time = 0;
            while (time < 3.6f && banner)
            {
                time += Time.unscaledDeltaTime;
                group.alpha = time > 3.0f ? (3.6f - time) / 0.6f : Mathf.Clamp01(time / 0.25f);
                yield return null;
            }
            if (banner) Destroy(banner.gameObject);
        }

        /// <summary>
        /// 확정: 배정 목록을 결과 스냅샷으로 즉시 고정한다(이후 연타·수첩 조작은 무시).
        /// 그다음 도장 → 책 닫기 → 배송·설치 순서로 연출한다.
        /// </summary>
        public bool ConfirmPlan()
        {
            if (confirmLocked || State != FieldState.Notebook || notebookBusy || recommendPanel) return false;
            if (FieldRules.CanConfirm(Spec, Plan, observed) != ConfirmBlock.None) return false;
            confirmLocked = true;
            notebookBusy = true;
            var snapshot = FieldRules.Evaluate(Spec, Plan);
            LastConfirmed = snapshot;
            Progress.Set(MissionIndex, snapshot);
            observedByMission[MissionIndex] = new HashSet<string>(observed);
            // 확정 즉시 저장한다. 설치 도중 꺼져도 다음 실행에서 이 결과를 적용한 재확인 상태로 이어진다.
            savePhase = FieldSavePhase.Review;
            SaveProgress();
            if (toastRoutine != null) StopCoroutine(toastRoutine);
            UIKit.Clear(toastRoot);
            StartCoroutine(ConfirmSequence(snapshot, screenToken));
            return true;
        }

        IEnumerator ConfirmSequence(FieldEvaluation snapshot, int token)
        {
            sound.Play(Sfx.Stamp);
            yield return StampConfirm();
            yield return CloseBookAnimation();
            if (token != screenToken) yield break;
            HideNotebook();
            notebookBusy = false;
            yield return LowerNotebookPose();
            if (token != screenToken) yield break;
            SetState(FieldState.Installing);
            BuildInstallHud();
            sound.Play(Sfx.Confirm);
            world.PlayInstall(snapshot, s => sound.Play(s), () => { if (token == screenToken) OnInstalled(snapshot, token); });
        }

        public void SkipInstall()
        {
            if (State == FieldState.Installing && world) world.SkipInstall();
        }

        /// <summary>배송·설치가 끝났다. 이제 이야기상의 시간이 흐르고 예측한 상황이 일어난다(확정 스냅샷 그대로).</summary>
        void OnInstalled(FieldEvaluation snapshot, int token)
        {
            if (State != FieldState.Installing || token != screenToken) return;
            SetState(FieldState.Scenario);
            if (skipButton) Destroy(skipButton.gameObject);
            skipButton = null;
            if (installBanner) Destroy(installBanner.gameObject);
            installBanner = null;
            BuildScenarioHud();
            world.PlayScenario(snapshot, s => sound.Play(s), stage => { if (token == screenToken) OnScenarioStage(stage); },
                () => { if (token == screenToken) EnterReview(); });
        }

        public void SkipScenario()
        {
            if (State == FieldState.Scenario && world) world.SkipScenario();
        }

        /// <summary>상황이 모두 지나간 뒤(또는 이어하기로 그 상태를 되살린 뒤) 현장을 다시 걸으며 확인하는 단계.</summary>
        void EnterReview()
        {
            if (State != FieldState.Installing && State != FieldState.Scenario) return;
            SetState(FieldState.Review);
            if (skipButton) Destroy(skipButton.gameObject);
            skipButton = null;
            if (scenarioPanel) Destroy(scenarioPanel.gameObject);
            scenarioPanel = null;
            reviewBanner = UIKit.Panel(hudRoot, "ReviewBanner", Theme.WithAlpha(Theme.Navy, 0.9f)).rectTransform;
            reviewBanner.Region(0.3f, 0.82f, 0.7f, 0.9f);
            var t = UIKit.Text(reviewBanner, "Text", $"{Mission.eventTime} 상황이 지나갔어요. 준비한 곳을 걸으며 다시 살펴봐요 (E)", 24, Color.white, TextAlignmentOptions.Center);
            t.rectTransform.Fill(14, 14, 4, 4);
            t.enableAutoSizing = true;
            t.fontSizeMin = 16;
            t.fontSizeMax = 24;
            RefreshHud();
        }

        // ───────── 시간 경과 HUD ─────────

        RectTransform scenarioPanel, installBanner;
        TextMeshProUGUI scenarioClock, scenarioCaption;

        void BuildScenarioHud()
        {
            scenarioPanel = UIKit.Panel(hudRoot, "Scenario", Theme.WithAlpha(Theme.Navy, 0.9f)).rectTransform;
            scenarioPanel.Region(0.3f, 0.76f, 0.7f, 0.9f);
            scenarioClock = UIKit.Text(scenarioPanel, "Clock", Mission.nowTime, 44, Theme.Hex(0xF2C14E), TextAlignmentOptions.Center, true);
            scenarioClock.rectTransform.Region(0.02f, 0.05f, 0.26f, 0.95f);
            scenarioCaption = UIKit.Text(scenarioPanel, "Caption", "시간이 흐르는 중…", 26, Color.white, TextAlignmentOptions.MidlineLeft);
            scenarioCaption.rectTransform.Region(0.27f, 0.05f, 0.98f, 0.95f);
            scenarioCaption.enableAutoSizing = true;
            scenarioCaption.fontSizeMin = 16;
            scenarioCaption.fontSizeMax = 26;
            skipButton = UIKit.Button(hudRoot, "SkipScenario", "건너뛰기 (Space)", SkipScenario, ButtonStyle.Ghost, 24);
            ((RectTransform)skipButton.transform).Region(0.42f, 0.69f, 0.58f, 0.75f);
            Deselectable(skipButton);
            StartCoroutine(TickClock(screenToken));
        }

        /// <summary>시계는 현장 상황(hazard)을 그대로 읽는다. 일시정지면 상황도 시계도 멈춘다.</summary>
        IEnumerator TickClock(int token)
        {
            int from = Minutes(Mission.nowTime), to = Minutes(Mission.eventTime);
            while (token == screenToken && State == FieldState.Scenario && scenarioClock && world)
            {
                int m = Mathf.RoundToInt(Mathf.Lerp(from, to, world.Hazard));
                scenarioClock.text = $"{m / 60 % 24:00}:{m % 60:00}";
                yield return null;
            }
        }

        static int Minutes(string hhmm)
        {
            var parts = (hhmm ?? "").Split(':');
            return parts.Length == 2 && int.TryParse(parts[0], out var h) && int.TryParse(parts[1], out var m) ? h * 60 + m : 0;
        }

        void OnScenarioStage(int stage)
        {
            if (!scenarioCaption) return;
            scenarioCaption.text = stage == 0 ? "시간이 흐르는 중…"
                : stage == 1 ? $"<b>{Mission.eventCaption}</b>"
                : $"<b>{Mission.eventCaption}</b>\n<size=22>{Mission.responseCaption} · {Mission.ResponseSummary(LastConfirmed?.Assignments)}</size>";
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
            title.enableAutoSizing = true;
            title.fontSizeMin = 18;
            title.fontSizeMax = 30;

            // AI 수첩 '내일'의 예측: 짧게 늘 보이게(예상 무늬 아이콘 + 한 줄). 현장 확인 정보는 수첩에 따로 쌓인다.
            var chip = UIKit.Panel(hudRoot, "Forecast", Theme.WithAlpha(Theme.Navy, 0.82f)).rectTransform;
            chip.Region(0.008f, 0.86f, 0.3f, 0.918f);
            FieldArt.ForecastIcon(chip, 40).Place(new Vector2(0, 0.5f), new Vector2(30, 0), new Vector2(40, 40));
            var fc = UIKit.Text(chip, "Text", $"<color=#{ColorUtility.ToHtmlStringRGB(Theme.Hex(0xF2C14E))}>예측</color> {Mission.forecast}", 20, Color.white, TextAlignmentOptions.MidlineLeft);
            fc.rectTransform.Fill(58, 10, 2, 2);
            fc.enableAutoSizing = true;
            fc.fontSizeMin = 13;
            fc.fontSizeMax = 20;

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
            bubble.sizeDelta = new Vector2(520, 76);
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
            installBanner = UIKit.Panel(hudRoot, "InstallBanner", Theme.WithAlpha(Theme.Navy, 0.88f)).rectTransform;
            installBanner.Region(0.3f, 0.76f, 0.7f, 0.81f);
            UIKit.Text(installBanner, "Text", $"승인한 물품 배송·설치 중 · 상황은 아직이에요 (지금 {Mission.nowTime})", 22, Color.white, TextAlignmentOptions.Center, true)
                .rectTransform.Fill(10, 10, 2, 2);
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
            if (target != null) magnifier.anchoredPosition = ClampToCanvas(ToCanvas(world.ObservePoint(target) + new Vector2(2.2f, 0.6f)), magnifier);
        }

        void ShowBubble(FieldProblemEntry p, string line)
        {
            // 말풍선은 주민을 따라간다(해결 후 자리를 옮겨도 맞게).
            var w = world;
            SetBubble(line, () => (w ? w.ResidentPosition(p.id) : p.residentPosition) + new Vector2(0, 2.0f));
        }

        /// <summary>담당자 머리 위 말풍선(도착 대사).</summary>
        void ShowPlayerBubble(string line) => SetBubble(line, () => world ? world.PlayerHeadPoint : Vector2.zero);

        void SetBubble(string line, Func<Vector2> anchor)
        {
            if (!bubble) return;
            bubbleAnchor = anchor;
            bubbleTimer = 3.2f;
            bubbleText.text = line;
            bubble.gameObject.SetActive(true);
            bubble.localScale = Vector3.one * 0.6f;
        }

        void UpdateBubble()
        {
            if (!bubble || !bubble.gameObject.activeSelf || world == null) return;
            bubbleTimer -= Time.unscaledDeltaTime;
            if (bubbleTimer <= 0 || bubbleAnchor == null || (State != FieldState.Explore && State != FieldState.Review && State != FieldState.Arriving))
            {
                bubble.gameObject.SetActive(false);
                return;
            }
            bubble.localScale = Vector3.one * Mathf.MoveTowards(bubble.localScale.x, 1f, Time.unscaledDeltaTime * 6f);
            bubble.anchoredPosition = ClampToCanvas(ToCanvas(bubbleAnchor()), bubble);
        }

        /// <summary>말풍선 · 돋보기가 화면 가장자리에서 잘리지 않도록 캔버스 안쪽으로 붙잡는다.</summary>
        Vector2 ClampToCanvas(Vector2 pos, RectTransform rt)
        {
            var area = ((RectTransform)canvas.transform).rect;
            var size = rt.rect.size;
            var pivot = rt.pivot;
            const float margin = 12f;
            float minX = area.xMin + size.x * pivot.x + margin, maxX = area.xMax - size.x * (1 - pivot.x) - margin;
            float minY = area.yMin + size.y * pivot.y + margin, maxY = area.yMax - size.y * (1 - pivot.y) - 96f;
            return new Vector2(Mathf.Clamp(pos.x, minX, Mathf.Max(minX, maxX)), Mathf.Clamp(pos.y, minY, Mathf.Max(minY, maxY)));
        }

        Vector2 ToCanvas(Vector2 worldLocal)
        {
            var screen = cam.WorldToScreenPoint(world.WorldPoint(worldLocal));
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform, screen, null, out var local);
            return local;
        }

        IEnumerator FlyToNotebook(FieldProblemEntry p)
        {
            if (!notebookButton)
            {
                RefreshHud();
                yield break;
            }
            var icon = FieldArt.ProblemIcon(toastRoot, p.kind, 70);
            Vector2 from = ToCanvas(world ? world.ResidentPosition(p.id) : p.observePoint);
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
            // 기록이 수첩에 닿은 뒤에 관찰 수와 배지를 올린다.
            sound.Play(Sfx.Check);
            if (notebookBadge) notebookBadge.gameObject.SetActive(true);
            RefreshHud();
            if (observeCounter) StartCoroutine(Bump(observeCounter.transform.parent));
            if (notebookButton) StartCoroutine(Bump(notebookButton.transform));
        }

        static IEnumerator Bump(Transform target)
        {
            float t = 0;
            while (t < 0.25f && target)
            {
                t += Time.unscaledDeltaTime;
                target.localScale = Vector3.one * (1f + Mathf.Sin(t / 0.25f * Mathf.PI) * 0.12f);
                yield return null;
            }
            if (target) target.localScale = Vector3.one;
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
