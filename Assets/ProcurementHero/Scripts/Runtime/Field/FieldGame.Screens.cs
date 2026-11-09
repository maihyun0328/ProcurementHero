using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ProcurementHero.Core.Field;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProcurementHero.Field
{
    public partial class FieldGame
    {
        public const string PolicyUrl = "https://pps.go.kr/kor/bbs/view.do?bbsSn=2608180009&key=00634";

        // ───────── 타이틀 ─────────

        public void ShowTitle()
        {
            // 타이틀로 돌아올 때 남은 변경을 저장하고, 이어하기 정보를 저장 형식 그대로 다시 만든다.
            bool hadSession = State != FieldState.Title && State != FieldState.Ending && State != FieldState.Intro || saveDirty;
            if (hadSession) SaveProgress();
            RefreshResume();
            BeginScreen(FieldState.Title);
            BuildTitleScreen(true);
        }

        /// <summary>타이틀 화면(배경 마을 지도·카드·메뉴). 시작 연출 중에는 메뉴를 보이지만 누를 수 없게 만들어 두고, 연출이 끝나면 켠다.</summary>
        void BuildTitleScreen(bool selectNow)
        {
            if (Resume == null) RefreshResume();
            if (world) Destroy(world.gameObject);
            world = FieldWorld.Build(worldRoot, database.missions[0], false);
            world.Delta = () => FieldDelta;

            var root = UIKit.Rect(screenRoot, "Title").Fill();
            titleGroup = root.gameObject.AddComponent<CanvasGroup>();
            titleGroup.interactable = selectNow;
            UIKit.Panel(root, "Shade", Theme.WithAlpha(Theme.Navy, 0.35f), ShapeSprites.Square).rectTransform.Fill();
            var card = UIKit.Panel(root, "Card", Theme.WithAlpha(Theme.Cream, 0.96f)).rectTransform;
            card.Region(0.3f, 0.1f, 0.7f, 0.9f);

            var notebook = UIKit.Panel(card, "BookIcon", Theme.Hex(0x7A4E2D)).rectTransform;
            notebook.Place(new Vector2(0.5f, 1f), new Vector2(0, -92), new Vector2(110, 120));
            UIKit.Panel(notebook, "Page", Theme.Hex(0xFFF9EC)).rectTransform.Fill(12, 8, 10, 10);
            FieldArt.Tick(notebook, 60, Theme.TealDark).Place(new Vector2(0.5f, 0.5f), new Vector2(4, 0), new Vector2(60, 60));
            // 수첩 옆 AI '내일' 표시: 밝기만 천천히 바뀐다(타이틀을 떠나면 화면과 함께 사라짐).
            var ai = FieldArt.ForecastIcon(card, 56);
            ai.Place(new Vector2(0.5f, 1f), new Vector2(84, -60), new Vector2(56, 56));
            ai.gameObject.AddComponent<SoftGlow>();

            var title = UIKit.Text(card, "Title", "조달 히어로", 84, Theme.Navy, TextAlignmentOptions.Center, true);
            title.rectTransform.Region(0.05f, 0.66f, 0.95f, 0.8f);
            var sub = UIKit.Text(card, "Sub", "내일을 준비하는 수첩", 38, Theme.TealDark, TextAlignmentOptions.Center, true);
            sub.rectTransform.Region(0.05f, 0.62f, 0.95f, 0.675f);
            // 시대·AI 수첩 설명: 1366×768에서도 약 17px로 읽히게 짧게 줄이고 진하게.
            var era = UIKit.Text(card, "Era", "2038년 새봄시 · AI 수첩 '내일'과 함께하는 현장", 25, Theme.Navy, TextAlignmentOptions.Center);
            era.rectTransform.Region(0.04f, 0.572f, 0.96f, 0.62f);
            era.textWrappingMode = TextWrappingModes.NoWrap;
            era.enableAutoSizing = true;
            era.fontSizeMin = 22;
            era.fontSizeMax = 25;

            var list = UIKit.Rect(card, "Menu").Region(0.15f, 0.12f, 0.85f, 0.56f);
            UIKit.VerticalList(list, 12).childForceExpandHeight = true;
            var buttons = new List<Selectable>();
            var r = Resume;
            if (r != null && r.Retrying && Progress.IsComplete)
            {
                // 완주 뒤 재도전 도중 종료한 저장: 그 시도 이어하기, 다른 현장 고르기, 새로 시작.
                var m = database.missions[r.Current];
                string step = r.Phase == FieldSavePhase.Review ? "상황 후 재확인" : "살펴보기";
                buttons.Add(UIKit.Button(list, "Continue", $"이어하기 · 현장 {r.Current + 1} {ShortPlace(m)} 재도전 ({step})", ContinueGame));
                buttons.Add(UIKit.Button(list, "Select", "현장 선택", ShowFieldSelect, ButtonStyle.Secondary));
                buttons.Add(UIKit.Button(list, "NewGame", "처음부터 시작", ConfirmNewGame, ButtonStyle.Secondary));
            }
            else if (r != null && r.Phase == FieldSavePhase.Complete && Progress.IsComplete)
            {
                // 엔딩까지 본 저장: 이어하기 대신 엔딩 다시 보기(기록은 다시 올라가지 않음), 현장 골라 재도전, 새로 시작.
                buttons.Add(UIKit.Button(list, "Replay", "엔딩 다시 보기", ShowEnding));
                buttons.Add(UIKit.Button(list, "Select", "현장 선택", ShowFieldSelect, ButtonStyle.Secondary));
                buttons.Add(UIKit.Button(list, "NewGame", "처음부터 시작", ConfirmNewGame, ButtonStyle.Secondary));
            }
            else if (r != null && r.HasProgress)
            {
                var m = database.missions[r.Current];
                string step = r.Phase == FieldSavePhase.Review ? "상황 후 재확인" : r.Phase == FieldSavePhase.Replan ? "다시 계획" : "살펴보기";
                // 긴 현장 제목을 버튼에 넣으면 자동 축소로 글자가 작아졌다. 짧은 장소 이름만 넣는다(전체 제목은 현장 HUD·수첩에 있음).
                buttons.Add(UIKit.Button(list, "Continue", $"이어하기 · 현장 {r.Current + 1} {ShortPlace(m)} ({step})", ContinueGame));
                buttons.Add(UIKit.Button(list, "NewGame", "처음부터 시작", ConfirmNewGame, ButtonStyle.Secondary));
            }
            else buttons.Add(UIKit.Button(list, "Start", "현장으로 출발", ConfirmNewGame));
            buttons.Add(UIKit.Button(list, "HowTo", "게임 방법", OpenHowTo, ButtonStyle.Secondary));
            buttons.Add(UIKit.Button(list, "Sound", "소리 설정", OpenSettings, ButtonStyle.Secondary));
            buttons.Add(UIKit.Button(list, "Credits", "작품 소개와 출처", OpenCredits, ButtonStyle.Secondary));
#if !UNITY_WEBGL
            buttons.Add(UIKit.Button(list, "Quit", "종료", QuitGame, ButtonStyle.Secondary));
#endif
            UIKit.ChainNavigation(buttons.ToArray(), true);
            titleFirst = buttons[0];
            if (selectNow) Select(buttons[0]);

            titleRecord = UIKit.Text(card, "Record", RecordLabel(), 22, Theme.NavySoft, TextAlignmentOptions.Center);
            titleRecord.rectTransform.Region(0.05f, 0.05f, 0.95f, 0.11f);
            var noteBack = UIKit.Panel(root, "NoteBack", Theme.WithAlpha(Theme.Navy, 0.72f)).rectTransform;
            noteBack.Region(0.12f, 0.015f, 0.88f, 0.075f);
            var note = UIKit.Text(noteBack, "Note", "가상의 2038년을 그린 창작 게임입니다 · 조달청의 공식 사업·미래 계획이나 공식 제작물이 아닙니다", 23, Color.white, TextAlignmentOptions.Center);
            note.rectTransform.Fill(16, 16, 2, 2);
            note.textWrappingMode = TextWrappingModes.NoWrap;
            note.enableAutoSizing = true;
            note.fontSizeMin = 21;
            note.fontSizeMax = 23;
        }

        /// <summary>'새봄시 보건소' → '보건소'처럼 버튼에 넣을 짧은 장소 이름.</summary>
        static string ShortPlace(FieldMissionData m) => (m.placeName ?? "").Replace("새봄시", "").Trim();

        TextMeshProUGUI titleRecord;

        string RecordLabel()
        {
            int best = FieldRecords.BestTotal;
            return best < 0 ? "최고 기록 없음" : $"최고 기록 ★ {best} / {Progress.MaxStars}";
        }

        void ConfirmNewGame()
        {
            if (Progress.HasAny || (Resume != null && Resume.HasProgress))
                Confirm("처음부터 시작할까요?", "지금까지의 현장 결과와 관찰 기록이 초기화돼요.\n(최고 기록과 소리 설정은 그대로 남아요.)", "처음부터", StartNewGame, "취소");
            else StartNewGame();
        }

        public void StartNewGame()
        {
            // 진행 저장과 메모리 진행을 함께 지운다(최고 기록·소리 설정은 그대로). 남은 저장 예약도 버려 옛 진행이 다시 쓰이지 않게 한다.
            saveDirty = false;
            FieldSave.Store.Delete();
            ApplyRestored(null);
            arrivedMissions.Clear();
            StartMission(0);
        }

        void QuitGame()
        {
            Confirm("게임을 종료할까요?", "진행은 자동으로 저장돼요. 다음에 '이어하기'로 계속할 수 있어요.", "종료", () =>
            {
                SaveProgress();
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }, "취소");
        }

        // ───────── 현장 선택(완주 뒤 재도전) ─────────

        /// <summary>현장 선택 화면에서 마지막으로 고른 카드(돌아왔을 때 같은 카드를 다시 고른다).</summary>
        int selectFocus;
        readonly List<Button> selectButtons = new List<Button>();

        /// <summary>
        /// 세 현장을 모두 성공한 저장에서만 연다. 현장마다 이름·대표 그림·대표 결과의 별·'재도전'을 카드로 보여 준다.
        /// 마우스와 방향키·Enter로 고르고 '뒤로'·Esc로 타이틀에 돌아간다. 진행 초기화는 하지 않는다.
        /// </summary>
        public void ShowFieldSelect()
        {
            if (!Progress.IsComplete)
            {
                ShowTitle();
                return;
            }
            // 재도전을 고르기 전까지는 '엔딩까지 본' 상태로 둔다(진행 중이던 재도전 시도는 새로 고르면 바뀐다).
            if (saveDirty) SaveProgress();
            BeginScreen(FieldState.Select);
            world = FieldWorld.Build(worldRoot, database.missions[0], false);
            world.Delta = () => FieldDelta;

            var root = UIKit.Rect(screenRoot, "FieldSelect").Fill();
            UIKit.Panel(root, "Shade", Theme.WithAlpha(Theme.Navy, 0.68f), ShapeSprites.Square).rectTransform.Fill();
            UIKit.Text(root, "Title", "현장 선택", 64, Color.white, TextAlignmentOptions.Center, true).rectTransform.Region(0.1f, 0.84f, 0.9f, 0.95f);
            UIKit.Text(root, "Sub", "다시 도전할 현장을 골라요 · 더 좋은 결과일 때만 기록이 바뀌어요", 28, Theme.Cream, TextAlignmentOptions.Center)
                .rectTransform.Region(0.1f, 0.78f, 0.9f, 0.84f);

            var row = UIKit.Rect(root, "Cards").Region(0.08f, 0.24f, 0.92f, 0.76f);
            UIKit.HorizontalList(row, 36);
            selectButtons.Clear();
            for (int i = 0; i < database.missions.Count; i++)
            {
                int index = i;
                var m = database.missions[i];
                var r = Progress.Get(i);
                var card = UIKit.Panel(row, "Card" + i, Theme.WithAlpha(Theme.Cream, 0.97f)).rectTransform;
                UIKit.Text(card, "No", $"현장 {i + 1}", 28, Theme.TealDark, TextAlignmentOptions.Center, true).rectTransform.Region(0.05f, 0.86f, 0.95f, 0.96f);
                var name = UIKit.Text(card, "Name", m.title, 32, Theme.Navy, TextAlignmentOptions.Center, true);
                name.rectTransform.Region(0.06f, 0.7f, 0.94f, 0.86f);
                name.enableAutoSizing = true;
                name.fontSizeMin = 24;
                name.fontSizeMax = 32;
                // 대표 그림: 그 현장의 문제 그림(관찰 대상)들.
                var art = UIKit.Panel(card, "Art", Theme.WithAlpha(Theme.NavySoft, 0.1f)).rectTransform;
                art.Region(0.1f, 0.42f, 0.9f, 0.68f);
                var icons = UIKit.Rect(art, "Icons").Fill(8, 8, 8, 8);
                UIKit.HorizontalList(icons, 14, null, TextAnchor.MiddleCenter, false);
                foreach (var kind in m.problems.Select(p => p.kind).Distinct())
                {
                    var slot = UIKit.Rect(icons, "Icon");
                    UIKit.Layout(slot, 96, 96);
                    FieldArt.ProblemIcon(slot, kind, 88).Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(88, 88));
                }
                int stars = r?.Stars ?? 0;
                UIKit.Text(card, "Stars", new string('★', stars) + new string('☆', 3 - stars), 60, Theme.Warning, TextAlignmentOptions.Center, true)
                    .rectTransform.Region(0.05f, 0.28f, 0.95f, 0.41f);
                int best = FieldRecords.BestStars(m.id);
                UIKit.Text(card, "Best", best >= 0 ? $"현장 결과 ★{stars} · 최고 기록 ★{best}" : $"현장 결과 ★{stars}", 24, Theme.NavySoft, TextAlignmentOptions.Center)
                    .rectTransform.Region(0.05f, 0.2f, 0.95f, 0.28f);
                var retry = UIKit.Button(card, "Retry" + i, "재도전", () => StartRetry(index), ButtonStyle.Primary, 32);
                ((RectTransform)retry.transform).Region(0.14f, 0.04f, 0.86f, 0.17f);
                selectButtons.Add(retry);
            }
            UIKit.Text(root, "Total", $"별 합계 ★ {Progress.TotalStars} / {Progress.MaxStars}  · 현장별 결과로 계산", 30, Color.white, TextAlignmentOptions.Center, true)
                .rectTransform.Region(0.1f, 0.15f, 0.9f, 0.22f);
            var back = UIKit.Button(root, "Back", "뒤로 (Esc)", ShowTitle, ButtonStyle.Secondary, 28);
            ((RectTransform)back.transform).Region(0.42f, 0.05f, 0.58f, 0.12f);

            // 카드는 좌우로, 아래로 가면 '뒤로', 위로 가면 마지막에 고른 카드.
            for (int i = 0; i < selectButtons.Count; i++)
                selectButtons[i].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnLeft = selectButtons[(i - 1 + selectButtons.Count) % selectButtons.Count],
                    selectOnRight = selectButtons[(i + 1) % selectButtons.Count],
                    selectOnDown = back
                };
            int focus = Mathf.Clamp(selectFocus, 0, selectButtons.Count - 1);
            back.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = selectButtons[focus] };
            // 이 화면을 연 Enter가 같은 프레임에 '재도전'을 누르지 않게 다음 프레임에 고른다.
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
            SelectNextFrame(() => selectButtons[focus]);
        }

        /// <summary>현장 선택 화면의 재도전 버튼(검증용).</summary>
        public IReadOnlyList<Button> SelectButtons => selectButtons;

        // ───────── 결과 ─────────

        FieldBook.Parts resultBook;
        /// <summary>지금 결과 화면이 재도전 시도의 결과인지(버튼·문구용).</summary>
        bool resultRetry;

        /// <summary>
        /// 결과는 수첩의 기록 페이지로 보여 준다: 손에서 수첩이 펼쳐져 이번 계획 페이지가 보이고,
        /// 페이지를 넘기면 전후 기록과 별·도장이 나온다.
        /// </summary>
        public void ShowResult()
        {
            if (State != FieldState.Review || LastConfirmed == null) return;
            SetState(FieldState.Result);
            // 재도전 결과를 보면 그 시도는 끝난다(대표 결과 반영은 승인 때 이미 끝남). 이후 종료·타이틀은 '엔딩까지 본' 상태로 이어진다.
            resultRetry = Retrying;
            if (resultRetry)
            {
                retryIndex = -1;
                savePhase = FieldSavePhase.Complete;
                SaveProgress();
            }
            if (resultButton) Destroy(resultButton.gameObject);
            resultButton = null;
            if (reviewBanner) Destroy(reviewBanner.gameObject);
            RefreshHud();
            StartCoroutine(ResultSequence(LastConfirmed, screenToken));
        }

        IEnumerator ResultSequence(FieldEvaluation r, int token)
        {
            bool newBest = FieldRecords.SubmitMission(Mission.id, r.Stars);
            var dim = UIKit.Panel(overlayRoot, "Result", Theme.WithAlpha(Theme.Navy, 0.5f), ShapeSprites.Square, true).rectTransform;
            dim.Fill();
            resultBook = FieldBook.Build(dim, new Vector2(0.08f, 0.05f), new Vector2(0.92f, 0.95f), false);
            BuildPlanSpread(resultBook, r);
            resultBook.group.alpha = 0f; // 손에서 펼치기 전까지 숨긴다(완성된 수첩이 한 프레임 보이지 않게).

            yield return Pose(0f, 1f, 0.12f);
            yield return FieldBook.Open(resultBook, HandOnCanvas(), () => sound.Play(Sfx.NotebookOpen));
            if (token != screenToken) yield break;
            yield return Wait(0.25f);
            if (token != screenToken) yield break;

            // 계획 페이지 → 결과 기록 페이지. 두 쪽 새 내용은 넘기기 시작 전에 한 번만 만든다(넘어가는 장 앞면에는 이전 내용).
            resultBook.group.interactable = false;
            yield return FieldBook.TurnPage(resultBook, () =>
            {
                BuildResultLeft(resultBook.left, r);
                BuildResultRight(resultBook.right, r, newBest);
            }, () => sound.Play(Sfx.NotebookOpen));
            if (token != screenToken) yield break;
            sound.Play(Sfx.Stamp);
            yield return FieldBook.Stamp(resultBook, resultBook.right, new Vector2(0.78f, 0.8f), r.Success ? "해결" : "재도전", r.Success ? Theme.Success : Theme.Danger, 170f);
            if (token != screenToken) yield break;
            resultBook.group.interactable = true;
            var first = resultBook.right.Find("Buttons")?.GetComponentInChildren<Button>();
            if (first) Select(first);
        }

        IEnumerator Wait(float seconds)
        {
            float t = 0;
            while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
        }

        static void ClearPage(RectTransform page)
        {
            // 줄 무늬·그늘은 남기고 내용만 지운다.
            for (int i = page.childCount - 1; i >= 0; i--)
            {
                var c = page.GetChild(i);
                if (FieldBook.IsDecoration(c)) continue;
                c.gameObject.SetActive(false);
                Destroy(c.gameObject);
            }
        }

        void BuildPlanSpread(FieldBook.Parts book, FieldEvaluation r)
        {
            var left = book.left;
            UIKit.Text(left, "Title", "이번 계획", 34, Theme.Navy, TextAlignmentOptions.MidlineLeft, true).rectTransform.Region(0.07f, 0.86f, 0.95f, 0.96f);
            var list = UIKit.Rect(left, "Plan").Region(0.07f, 0.2f, 0.93f, 0.84f);
            UIKit.VerticalList(list, 12).childForceExpandHeight = false;
            foreach (var spot in Mission.spots)
            {
                var row = UIKit.Rect(list, "Row_" + spot.id);
                UIKit.Layout(row, 86);
                r.Assignments.TryGetValue(spot.id, out var itemId);
                var item = itemId != null ? Mission.Item(itemId) : null;
                if (item != null) FieldArt.ItemIcon(row, item.visual, 70).Place(new Vector2(0, 0.5f), new Vector2(40, 0), new Vector2(70, 70));
                else UIKit.Shape(row, "Empty", ShapeSprites.Circle, Theme.WithAlpha(Theme.Navy, 0.18f), new Vector2(0, 0.5f), new Vector2(40, 0), new Vector2(34, 34)).raycastTarget = false;
                UIKit.Text(row, "Text", $"<b>{spot.displayName}</b>\n{(item != null ? item.displayName : "비어 있음")}", 24, item != null ? Theme.Navy : Theme.NavySoft,
                    TextAlignmentOptions.MidlineLeft).rectTransform.Fill(96, 0, 0, 0);
            }
            UIKit.Text(left, "Cost", $"사용한 예산 {r.Cost} / {r.Budget}", 26, Theme.NavySoft, TextAlignmentOptions.MidlineLeft, true).rectTransform.Region(0.07f, 0.06f, 0.95f, 0.16f);
            UIKit.Text(book.right, "Title", "결과 기록", 34, Theme.NavySoft, TextAlignmentOptions.Center, true).rectTransform.Region(0.05f, 0.45f, 0.95f, 0.55f);
        }

        void BuildResultLeft(RectTransform left, FieldEvaluation r)
        {
            ClearPage(left);
            UIKit.Text(left, "Title", "현장 전후", 34, Theme.Navy, TextAlignmentOptions.MidlineLeft, true).rectTransform.Region(0.07f, 0.86f, 0.95f, 0.96f);
            var rows = UIKit.Rect(left, "Rows").Region(0.06f, 0.08f, 0.94f, 0.84f);
            UIKit.VerticalList(rows, 12).childForceExpandHeight = false;
            foreach (var p in Mission.problems)
            {
                bool solved = r.IsSolved(p.id);
                var row = UIKit.Panel(rows, "Row_" + p.id, solved ? Theme.SuccessPale : (p.required ? Theme.DangerPale : Theme.WarningPale));
                UIKit.Layout(row, 92);
                FieldArt.ProblemIcon(row.transform, p.kind, 54).Place(new Vector2(0, 0.5f), new Vector2(40, 0), new Vector2(54, 54));
                UIKit.Text(row.transform, "Name", (p.required ? "" : "추가 · ") + p.displayName, 25, Theme.Navy, TextAlignmentOptions.MidlineLeft, true)
                    .rectTransform.Region(0, 0, 0.46f, 1).offsetMin = new Vector2(80, 0);
                // 전 → 후 작은 그림(색과 함께 체크·점 표시로 구분)
                var before = UIKit.Panel(row.transform, "Before", BeforeColor(p.kind)).rectTransform;
                before.Place(new Vector2(0.53f, 0.5f), Vector2.zero, new Vector2(60, 48));
                UIKit.Text(row.transform, "Arrow", "→", 28, Theme.NavySoft, TextAlignmentOptions.Center, true).rectTransform.Place(new Vector2(0.61f, 0.5f), Vector2.zero, new Vector2(40, 40));
                var after = UIKit.Panel(row.transform, "After", solved ? AfterColor(p.kind) : BeforeColor(p.kind)).rectTransform;
                after.Place(new Vector2(0.69f, 0.5f), Vector2.zero, new Vector2(60, 48));
                if (solved) FieldArt.Tick(after, 34, Theme.Success).Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34, 34));
                else UIKit.Text(after, "Still", "…", 26, Color.white, TextAlignmentOptions.Center, true).rectTransform.Fill();
                var state = UIKit.Text(row.transform, "State", solved ? "○ 해결" : "△ 남음", 25, solved ? Theme.Success : (p.required ? Theme.Danger : Theme.Warning), TextAlignmentOptions.MidlineRight, true);
                state.rectTransform.Region(0.74f, 0, 1, 1).offsetMax = new Vector2(-16, 0);
            }
        }

        void BuildResultRight(RectTransform right, FieldEvaluation r, bool newBest)
        {
            ClearPage(right);
            UIKit.Text(right, "Title", $"현장 {MissionIndex + 1} · {Mission.title}", 30, Theme.Navy, TextAlignmentOptions.MidlineLeft, true).rectTransform.Region(0.07f, 0.88f, 0.95f, 0.97f);
            string stars = new string('★', r.Stars) + new string('☆', 3 - r.Stars);
            UIKit.Text(right, "Stars", stars, 92, Theme.Warning, TextAlignmentOptions.Center, true).rectTransform.Region(0.04f, 0.62f, 0.62f, 0.86f);
            UIKit.Text(right, "StarCount", $"별 {r.Stars}개" + (newBest ? "  · 새 기록!" : ""), 26, Theme.Navy, TextAlignmentOptions.Center, true).rectTransform.Region(0.04f, 0.56f, 0.62f, 0.63f);

            string guide = resultRetry ? RetryGuide(r)
                : r.Stars == 3 ? (MissionIndex + 1 >= database.missions.Count ? "미리 준비한 덕분에 상황이 와도 모두 괜찮았어요! 새봄시의 하루를 확인해 봐요." : "미리 준비한 덕분에 상황이 와도 모두 괜찮았어요! 다음 현장으로 가요.")
                : r.Success ? "필요한 곳은 모두 대비했어요. 추가 목표까지 준비하면 별 3개예요."
                : r.Stars == 1 ? "상황이 오자 불편한 곳이 남았어요. 붉게 표시된 곳을 다시 계획해 봐요."
                : "필수 문제에 대비하지 못했어요. 상황 전으로 돌아가 자리와 물품을 다시 골라 봐요.";
            UIKit.Text(right, "Guide", guide, 26, Theme.Navy, TextAlignmentOptions.Center).rectTransform.Region(0.07f, 0.3f, 0.93f, 0.5f);

            var bar = UIKit.Rect(right, "Buttons").Region(0.06f, 0.06f, 0.94f, 0.26f);
            UIKit.VerticalList(bar, 12).childForceExpandHeight = true;
            var list = new List<Selectable>();
            if (resultRetry)
            {
                // 재도전 결과: 같은 현장 다시 · 현장 선택 · 타이틀. 나머지 현장은 다시 시작하지 않는다.
                int index = MissionIndex;
                list.Add(UIKit.Button(bar, "Again", "같은 현장 다시", () => { if (ResultReady) StartRetry(index); }));
                var retryRow = UIKit.Rect(bar, "Row");
                UIKit.HorizontalList(retryRow, 12);
                list.Add(UIKit.Button(retryRow, "Select", "현장 선택", () => { if (ResultReady) ShowFieldSelect(); }, ButtonStyle.Secondary));
                list.Add(UIKit.Button(retryRow, "Title", "타이틀로", () => { if (ResultReady) ShowTitle(); }, ButtonStyle.Secondary));
                UIKit.ChainNavigation(list.ToArray(), true);
                return;
            }
            bool last = MissionIndex >= database.missions.Count - 1;
            if (r.Success)
                list.Add(UIKit.Button(bar, "Next", last ? "엔딩 보기" : "다음 현장", () =>
                {
                    if (resultBook != null && !resultBook.group.interactable) return;
                    if (last) ShowEnding();
                    else StartCoroutine(NextMissionSequence(MissionIndex + 1, screenToken));
                }));
            var buttonsRow = UIKit.Rect(bar, "Row");
            UIKit.HorizontalList(buttonsRow, 12);
            list.Add(UIKit.Button(buttonsRow, "Replan", "다시 계획하기", () => StartMission(MissionIndex, replan: true), r.Success ? ButtonStyle.Secondary : ButtonStyle.Primary));
            list.Add(UIKit.Button(buttonsRow, "Title", "타이틀로", ShowTitle, ButtonStyle.Secondary));
            UIKit.ChainNavigation(list.ToArray(), true);
        }

        bool ResultReady => resultBook == null || resultBook.group.interactable;

        /// <summary>
        /// 재도전 결과 문구: 이번 시도의 별과, 이번 시도가 대표 결과가 됐는지(합계는 현장별 대표 결과로 다시 계산).
        /// 이어하기로 되살린 재확인에서도 맞도록, 대표 결과와 이번 배정이 같은지로 판단한다(이전 별은 이번 실행에서 승인했을 때만 안다).
        /// </summary>
        string RetryGuide(FieldEvaluation r)
        {
            var rep = Progress.Get(MissionIndex);
            string total = $"별 합계 ★ {Progress.TotalStars} / {Progress.MaxStars}";
            bool isRep = rep != null && rep.Assignments.Count == r.Assignments.Count
                         && r.Assignments.All(a => rep.Assignments.TryGetValue(a.Key, out var item) && item == a.Value);
            if (isRep && retryImproved && retryPreviousStars >= 0 && r.Stars > retryPreviousStars)
                return $"대표 결과가 ★{retryPreviousStars} → ★{r.Stars}로 바뀌었어요.\n{total}";
            if (isRep) return $"이번 시도 ★{r.Stars}를 이 현장의 대표 결과로 남겼어요.\n{total}";
            if (r.Success) return $"이번 시도 ★{r.Stars} · 대표 결과 ★{rep?.Stars ?? 0}가 그대로 남아요.\n{total}";
            return $"이번 시도는 필수 대비가 부족했어요. 대표 결과 ★{rep?.Stars ?? 0}와 완주 기록은 그대로예요.";
        }

        /// <summary>현재 현장 완료 → 페이지를 넘겨 다음 현장 소개 → 수첩을 덮고 출발. 건너뛰기(Space·Enter) 가능.</summary>
        IEnumerator NextMissionSequence(int next, int token)
        {
            if (resultBook == null) { StartMission(next); yield break; }
            resultBook.group.interactable = false;
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
            var m = database.missions[next];
            yield return FieldBook.TurnPage(resultBook, () =>
            {
                ClearPage(resultBook.left);
                UIKit.Text(resultBook.left, "Intro", $"<size=30><color=#{ColorUtility.ToHtmlStringRGB(Theme.TealDark)}>현장 {next + 1}</color></size>\n<b>{m.title}</b>\n<size=28>{m.placeName}</size>",
                    52, Theme.Navy, TextAlignmentOptions.Center).rectTransform.Region(0.06f, 0.45f, 0.94f, 0.85f);
                UIKit.Text(resultBook.left, "Line", m.intro, 26, Theme.NavySoft, TextAlignmentOptions.Center).rectTransform.Region(0.08f, 0.2f, 0.92f, 0.42f);
                ClearPage(resultBook.right);
                UIKit.Text(resultBook.right, "Goals", $"살펴볼 곳 {m.problems.Count}곳 <size=24><color=#55607E>· 필수 {m.problems.Count(p => p.mustObserve)}곳</color></size>", 30, Theme.Navy, TextAlignmentOptions.Center, true).rectTransform.Region(0.05f, 0.78f, 0.95f, 0.9f);
                var row = UIKit.Rect(resultBook.right, "Icons").Region(0.06f, 0.42f, 0.94f, 0.74f);
                UIKit.HorizontalList(row, 18, null, TextAnchor.MiddleCenter, false);
                foreach (var p in m.problems)
                {
                    var cell = UIKit.Panel(row, "Unknown", Theme.WithAlpha(Theme.NavySoft, 0.18f)).rectTransform;
                    UIKit.Layout(cell, 130, 130);
                    FieldArt.UnknownPattern(cell, Theme.WithAlpha(Theme.Navy, 0.15f));
                    UIKit.Text(cell, "Q", "?", 60, Theme.Navy, TextAlignmentOptions.Center, true).rectTransform.Fill();
                    if (!p.mustObserve) UIKit.Text(cell, "Extra", "추가", 22, Theme.TealDark, TextAlignmentOptions.Bottom, true).rectTransform.Fill(0, 0, 0, 6);
                }
            }, () => sound.Play(Sfx.NotebookOpen));
            float wait = 0;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            while (wait < 1.1f && token == screenToken)
            {
                wait += Time.unscaledDeltaTime;
                if (kb != null && (kb.spaceKey.isPressed || kb.enterKey.isPressed) && wait > 0.2f) break;
                yield return null;
            }
            if (token != screenToken) yield break;
            yield return FieldBook.Close(resultBook, HandOnCanvas());
            if (token != screenToken) yield break;
            StartMission(next);
        }

        static readonly Color PageColorLight = Theme.Hex(0xFFF9EC);

        static Color BeforeColor(FieldProblemKind k) => k switch
        {
            FieldProblemKind.Dark => Theme.Hex(0x1C2440),
            FieldProblemKind.Heat => Theme.Heat,
            FieldProblemKind.Thirst => Theme.DangerPale,
            FieldProblemKind.Power => Theme.Window,
            _ => Theme.Water
        };

        static Color AfterColor(FieldProblemKind k) => k switch
        {
            FieldProblemKind.Dark => FieldArt.Lamp,
            FieldProblemKind.Heat => Theme.Cool,
            FieldProblemKind.Thirst => Theme.TealPale,
            FieldProblemKind.Power => Theme.WindowLit,
            _ => Theme.Grass
        };

        // ───────── 엔딩 ─────────

        public const string EndingLine = "내일의 불편을 줄이는 오늘의 조달.";
        public const string EndingNotebookLine = "AI는 내일을 예측하고, 우리는 현장을 확인해 필요한 선택을 합니다.";
        const float EndingSceneSeconds = 3.2f;

        Button endingSkip;
        bool endingFinal;
        int endingTotal;
        bool endingNewBest;

        /// <summary>엔딩 연출이 재생 중인지(최종 화면 전).</summary>
        public bool EndingPlaying => State == FieldState.Ending && !endingFinal;
        /// <summary>엔딩 연출이 지금 보여 주는 현장 번호(검증용, 장면 밖이면 -1).</summary>
        public int EndingSceneIndex { get; private set; } = -1;

        /// <summary>
        /// 엔딩: 현장마다 최종 채택된 결과로 마을을 다시 보여 준 뒤(골목 → 보건소 → 대피소),
        /// 담당자가 수첩에 마지막 체크를 하고 덮고, 마무리 문구와 별 기록 화면으로 끝난다. 약 11초, Space·Enter·버튼으로 건너뛰기.
        /// 각 장면은 Progress에 저장된 그 현장의 판정 결과를 FieldWorld.ApplyInstant로 그대로 적용한다(별도 판정 없음).
        /// </summary>
        public void ShowEnding()
        {
            if (!Progress.IsComplete)
            {
                ShowTitle();
                return;
            }
            BeginScreen(FieldState.Ending);
            EndRetry();
            savePhase = FieldSavePhase.Complete;
            SaveProgress();
            endingFinal = false;
            EndingSceneIndex = -1;
            // 기록은 엔딩에 들어올 때 한 번만 등록한다. 건너뛰기·최종 화면은 다시 등록하지 않는다.
            endingTotal = Progress.TotalStars;
            endingNewBest = FieldRecords.SubmitTotal(endingTotal);

            endingSkip = UIKit.Button(hudRoot, "SkipEnding", "건너뛰기 (Space)", SkipEnding, ButtonStyle.Ghost, 24);
            ((RectTransform)endingSkip.transform).Region(0.84f, 0.92f, 0.98f, 0.98f);
            Deselectable(endingSkip);
            StartCoroutine(EndingSequence(screenToken));
        }

        public void SkipEnding()
        {
            if (!EndingPlaying) return;
            inputLockFrame = Time.frameCount;
            ShowEndingFinal();
        }

        void BuildEndingWorld(int index, bool withPlayer)
        {
            if (world) Destroy(world.gameObject);
            world = FieldWorld.Build(worldRoot, database.missions[index], withPlayer);
            world.Delta = () => FieldDelta;
            world.ApplyInstant(Progress.Get(index));
        }

        IEnumerator EndingSequence(int token)
        {
            int count = database.missions.Count;
            // 장면 사이 암전. 화면 맨 아래 층(screenRoot)에 두어 건너뛰기 버튼·수첩을 가리지 않는다.
            var curtain = UIKit.Panel(screenRoot, "Curtain", Theme.Navy, ShapeSprites.Square).rectTransform;
            curtain.Fill();
            var curtainImage = curtain.GetComponent<Image>();
            curtainImage.raycastTarget = false;

            IEnumerator FadeCurtain(float from, float to, float seconds)
            {
                float t = 0;
                while (t < seconds)
                {
                    t += Time.unscaledDeltaTime;
                    if (curtainImage) curtainImage.color = Theme.WithAlpha(Theme.Navy, Mathf.Lerp(from, to, Mathf.Clamp01(t / seconds)));
                    yield return null;
                }
            }

            // ①②③ 현장별 최종 결과: 글은 현장 이름과 주민 한마디만. 주목할 주민 한 명의 변화를 다시 보여 주며 살짝 다가간다.
            for (int i = 0; i < count; i++)
            {
                EndingSceneIndex = i;
                BuildEndingWorld(i, false);
                var focus = PickEndingFocus(i);
                if (focus != null) world.ReplayResolution(focus.id);
                var caption = BuildEndingCaption(i);
                var voice = focus != null ? BuildEndingVoice(world.SolvedLine(focus)) : null;
                sound.Play(Sfx.Arrive);
                Vector2 target = focus != null ? EndingCameraTarget(world.ResidentAnchor(focus.id)) : new Vector2(0, 0.6f);

                float t = 0, hold = EndingSceneSeconds;
                while (t < hold && token == screenToken)
                {
                    t += Time.unscaledDeltaTime;
                    float k = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / hold));
                    if (cam)
                    {
                        cam.orthographicSize = Mathf.Lerp(9f, EndingZoomSize, k);
                        var c = Vector2.Lerp(new Vector2(0, 0.6f), target, k);
                        cam.transform.position = new Vector3(c.x, c.y, -10);
                    }
                    if (curtainImage) curtainImage.color = Theme.WithAlpha(Theme.Navy, t < 0.3f ? 1 - t / 0.3f : t > hold - 0.3f ? (t - (hold - 0.3f)) / 0.3f : 0f);
                    if (voice && focus != null)
                    {
                        // 걸어간 뒤(또는 기뻐한 뒤) 머리 위에 한마디. 머리와 겹치지 않게 위로 띄운다.
                        voice.gameObject.SetActive(t > 1.0f && t < hold - 0.25f);
                        voice.anchoredPosition = ClampToCanvas(ToCanvas(world.ResidentPosition(focus.id) + new Vector2(0, 2.0f)), voice);
                    }
                    yield return null;
                }
                if (token != screenToken) yield break;
                if (caption) Destroy(caption.gameObject);
                if (voice) Destroy(voice.gameObject);
                ResetCamera();
            }

            // ④ 담당자가 수첩에 마지막 체크를 하고 덮는다(마지막 현장 앞에서).
            EndingSceneIndex = -1;
            BuildEndingWorld(count - 1, true);
            world.PlacePlayer(new Vector2(0.6f, -5.4f), Vector2.right);
            yield return FadeCurtain(1f, 0.35f, 0.3f);
            if (token != screenToken) yield break;
            yield return Pose(0f, 1f, 0.15f);
            var book = FieldBook.Build(toastRoot, new Vector2(0.24f, 0.3f), new Vector2(0.76f, 0.92f), false);
            book.group.alpha = 0f;
            var lastTick = BuildEndingBook(book);
            yield return FieldBook.Open(book, HandOnCanvas(), () => sound.Play(Sfx.NotebookOpen));
            if (token != screenToken) yield break;
            yield return Wait(0.25f);
            sound.Play(Sfx.Check);
            yield return DrawEndingTick(lastTick);
            if (token != screenToken) yield break;
            yield return Wait(0.45f);
            if (token != screenToken) yield break;
            yield return FieldBook.Close(book, HandOnCanvas());
            if (token != screenToken) yield break;
            if (book.book) Destroy(book.book.gameObject);
            yield return Pose(1f, 0f, 0.15f);

            // ⑤ 마무리 문구
            var line = UIKit.Text(toastRoot, "EndingLine", EndingLine, 64, Theme.Cream, TextAlignmentOptions.Center, true);
            line.rectTransform.Region(0.1f, 0.42f, 0.9f, 0.58f);
            var lineGroup = line.gameObject.AddComponent<CanvasGroup>();
            lineGroup.blocksRaycasts = false;
            float lt = 0;
            while (lt < 1.5f && token == screenToken)
            {
                lt += Time.unscaledDeltaTime;
                if (curtainImage) curtainImage.color = Theme.WithAlpha(Theme.Navy, Mathf.Lerp(0.35f, 0.7f, Mathf.Clamp01(lt / 0.3f)));
                lineGroup.alpha = Mathf.Clamp01(lt / 0.3f);
                yield return null;
            }
            if (token != screenToken) yield break;
            sound.Play(Sfx.Success);
            ShowEndingFinal();
        }

        /// <summary>현장 장면 위 설명: 현장 이름 · 별 · 문제별 해결/미해결 · 해결된 곳 주민 한마디.</summary>
        const float EndingZoomSize = 7.4f;

        /// <summary>현장 장면 아래 왼쪽의 짧은 이름표. 별·해결 상태는 최종 화면에서 보여 준다.</summary>
        RectTransform BuildEndingCaption(int index)
        {
            var m = database.missions[index];
            var card = UIKit.Panel(screenRoot, "EndingCaption", Theme.WithAlpha(Theme.Cream, 0.92f)).rectTransform;
            card.Region(0.02f, 0.03f, 0.34f, 0.11f);
            UIKit.Text(card, "Name", $"현장 {index + 1} · {m.title}", 30, Theme.Navy, TextAlignmentOptions.Center, true).rectTransform.Fill(12, 12, 4, 4);
            return card;
        }

        /// <summary>주목할 주민의 한마디 말풍선(장면 동안 주민 머리 위를 따라간다).</summary>
        RectTransform BuildEndingVoice(string line)
        {
            var b = UIKit.Panel(screenRoot, "EndingVoice", Theme.Paper).rectTransform;
            b.sizeDelta = new Vector2(520, 72);
            b.gameObject.AddComponent<Outline>().effectColor = Theme.WithAlpha(Theme.Navy, 0.4f);
            var t = UIKit.Text(b, "Text", line, 26, Theme.Navy, TextAlignmentOptions.Center);
            t.rectTransform.Fill(14, 14, 4, 4);
            t.enableAutoSizing = true;
            t.fontSizeMin = 18;
            t.fontSizeMax = 26;
            b.gameObject.SetActive(false);
            return b;
        }

        /// <summary>
        /// 장면마다 보여 줄 주민 한 명: 해결한 물품 쪽으로 옮겨 간 주민(그늘막·냉방기·급수) → 해결한 추가 목표 → 마지막으로 해결한 문제 순.
        /// 판정 결과(Progress)와 반응 데이터만 쓰며 별도 판정은 없다.
        /// </summary>
        FieldProblemEntry PickEndingFocus(int index)
        {
            var m = database.missions[index];
            var r = Progress.Get(index);
            var solved = m.problems.Where(p => r.IsSolved(p.id)).ToList();
            return solved.FirstOrDefault(p => !p.required && world.ResidentMoved(p.id))
                ?? solved.FirstOrDefault(p => world.ResidentMoved(p.id))
                ?? solved.FirstOrDefault(p => !p.required)
                ?? solved.LastOrDefault();
        }

        /// <summary>주민 쪽으로 살짝 다가간 구도. 지도 밖이 보이지 않게 중심을 묶는다.</summary>
        static Vector2 EndingCameraTarget(Vector2 at)
        {
            float halfH = EndingZoomSize, halfW = EndingZoomSize * 16f / 9f;
            float x = Mathf.Clamp(at.x, -FieldWorld.HalfWidth - 1 + halfW, FieldWorld.HalfWidth + 1 - halfW);
            float y = Mathf.Clamp(at.y, -FieldWorld.HalfHeight - 2 + halfH, FieldWorld.HalfHeight + 2 - halfH);
            return Vector2.Lerp(new Vector2(0, 0.6f), new Vector2(x, y), 0.8f);
        }

        /// <summary>엔딩 수첩: 세 현장 목록. 앞의 현장은 이미 체크, 마지막 현장의 체크는 연출로 그린다.</summary>
        RectTransform BuildEndingBook(FieldBook.Parts book)
        {
            UIKit.Text(book.left, "Head", "현장 수첩", 40, Theme.Navy, TextAlignmentOptions.Center, true).rectTransform.Region(0.06f, 0.82f, 0.94f, 0.95f);
            RectTransform last = null;
            int count = database.missions.Count;
            for (int i = 0; i < count; i++)
            {
                var m = database.missions[i];
                var r = Progress.Get(i);
                float top = 0.76f - i * 0.22f;
                var rowRect = UIKit.Rect(book.left, "Row" + i).Region(0.06f, top - 0.18f, 0.94f, top);
                var box = UIKit.Panel(rowRect, "Box", Theme.WithAlpha(Theme.NavySoft, 0.12f)).rectTransform;
                box.Place(new Vector2(0, 0.5f), new Vector2(40, 0), new Vector2(64, 64));
                var tick = FieldArt.Tick(box, 58, Theme.TealDark);
                tick.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(58, 58));
                if (i == count - 1)
                {
                    last = tick;
                    tick.localScale = Vector3.zero;
                }
                UIKit.Text(rowRect, "Name", $"{m.title}\n<size=22>{new string('★', r.Stars)}{new string('☆', 3 - r.Stars)}  해결 {m.problems.Count(p => r.IsSolved(p.id))}/{m.problems.Count}</size>",
                    28, Theme.Navy, TextAlignmentOptions.MidlineLeft).rectTransform.Fill(90, 0, 0, 0);
            }
            UIKit.Text(book.right, "Total", $"별 ★ {endingTotal} / {Progress.MaxStars}", 40, Theme.Navy, TextAlignmentOptions.Center, true).rectTransform.Region(0.06f, 0.55f, 0.94f, 0.7f);
            var closing = UIKit.Text(book.right, "Line", EndingNotebookLine, 26, Theme.NavySoft, TextAlignmentOptions.Center);
            closing.rectTransform.Region(0.08f, 0.28f, 0.92f, 0.52f);
            closing.enableAutoSizing = true;
            closing.fontSizeMin = 18;
            closing.fontSizeMax = 26;
            return last;
        }

        static IEnumerator DrawEndingTick(RectTransform tick)
        {
            if (!tick) yield break;
            tick.localScale = Vector3.one;
            var shortStroke = tick.Find("Short");
            var longStroke = tick.Find("Long");
            float t = 0;
            while (t < 0.3f && tick)
            {
                t += Time.unscaledDeltaTime;
                float a = Mathf.Clamp01(t / 0.12f), b = Mathf.Clamp01((t - 0.1f) / 0.2f);
                if (shortStroke) shortStroke.localScale = new Vector3(1, a, 1);
                if (longStroke) longStroke.localScale = new Vector3(1, b, 1);
                yield return null;
            }
        }

        /// <summary>최종 별·기록 화면. 연출을 끝까지 봐도, 건너뛰어도 같은 화면이다.</summary>
        void ShowEndingFinal()
        {
            if (State != FieldState.Ending || endingFinal) return;
            endingFinal = true;
            EndingSceneIndex = -1;
            // 진행 중인 엔딩 연출(코루틴·임시 UI·수첩)을 모두 멈추고 지운다.
            StopAllCoroutines();
            screenToken++;
            UIKit.Clear(screenRoot);
            UIKit.Clear(toastRoot);
            UIKit.Clear(overlayRoot);
            if (endingSkip) Destroy(endingSkip.gameObject);
            endingSkip = null;
            BuildEndingWorld(database.missions.Count - 1, false);

            int total = endingTotal;
            var root = UIKit.Rect(screenRoot, "Ending").Fill();
            UIKit.Panel(root, "Shade", Theme.WithAlpha(Theme.Navy, 0.45f), ShapeSprites.Square).rectTransform.Fill();
            var page = UIKit.Panel(root, "Page", Theme.Hex(0xFFF9EC)).rectTransform;
            page.Region(0.12f, 0.1f, 0.88f, 0.9f);

            string title = total == Progress.MaxStars ? "모두의 하루를 바꾼 조달 히어로"
                : total >= Progress.MissionCount * 2 + 1 ? "꼼꼼한 현장 해결사"
                : "성장하는 현장 지원 담당자";
            UIKit.Text(page, "Title", title, 54, Theme.TealDark, TextAlignmentOptions.Center, true).rectTransform.Region(0.05f, 0.8f, 0.95f, 0.95f);
            UIKit.Text(page, "Line", EndingLine, 30, Theme.Navy, TextAlignmentOptions.Center, true)
                .rectTransform.Region(0.05f, 0.72f, 0.95f, 0.8f);

            for (int i = 0; i < database.missions.Count; i++)
            {
                var m = database.missions[i];
                var r = Progress.Get(i);
                var card = UIKit.Panel(page, "Mission" + i, Theme.Paper).rectTransform;
                card.Region(0.04f + i * 0.31f, 0.27f, 0.04f + i * 0.31f + 0.29f, 0.69f);
                UIKit.Text(card, "Name", $"현장 {i + 1}\n<b>{m.title}</b>", 26, Theme.Navy, TextAlignmentOptions.Center).rectTransform.Region(0.05f, 0.78f, 0.95f, 0.98f);
                UIKit.Text(card, "Stars", new string('★', r.Stars) + new string('☆', 3 - r.Stars), 44, Theme.Warning, TextAlignmentOptions.Center, true).rectTransform.Region(0.05f, 0.62f, 0.95f, 0.78f);
                // 문제별 해결 상태는 엔딩 장면이 아니라 여기에서 한 번에 본다.
                string teal = ColorUtility.ToHtmlStringRGB(Theme.TealDark), gray = ColorUtility.ToHtmlStringRGB(Theme.NavySoft);
                string lines = string.Join("\n", m.problems.Select(p => r.IsSolved(p.id)
                    ? $"<color=#{teal}><b>해결</b></color> {(p.required ? "" : "추가 ")}{p.displayName}"
                    : $"<color=#{gray}><b>남음</b> {(p.required ? "" : "추가 ")}{p.displayName}</color>"));
                var list = UIKit.Text(card, "Problems", lines, 21, Theme.Navy, TextAlignmentOptions.Center);
                list.rectTransform.Region(0.05f, 0.3f, 0.95f, 0.62f);
                list.enableAutoSizing = true;
                list.fontSizeMin = 15;
                list.fontSizeMax = 21;
                var icons = UIKit.Rect(card, "Items").Region(0.05f, 0.04f, 0.95f, 0.29f);
                UIKit.HorizontalList(icons, 6, null, TextAnchor.MiddleCenter, false);
                foreach (var spot in m.spots)
                {
                    if (!r.Assignments.TryGetValue(spot.id, out var itemId)) continue;
                    var slot = UIKit.Rect(icons, "Slot");
                    UIKit.Layout(slot, 70, 70);
                    FieldArt.ItemIcon(slot, m.Item(itemId).visual, 64).Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64, 64));
                }
            }

            string record = $"별 합계 ★ {total} / {Progress.MaxStars}" + (endingNewBest ? "  · 새 기록!" : $"  · 최고 기록 ★ {FieldRecords.BestTotal}");
            UIKit.Text(page, "Total", record, 34, Theme.Navy, TextAlignmentOptions.Center, true).rectTransform.Region(0.05f, 0.16f, 0.95f, 0.25f);

            var bar = UIKit.Rect(page, "Buttons").Region(0.1f, 0.03f, 0.9f, 0.13f);
            UIKit.HorizontalList(bar, 20);
            var again = UIKit.Button(bar, "Again", "처음부터 다시", ConfirmNewGame);
            var select = UIKit.Button(bar, "Select", "현장 선택", ShowFieldSelect, ButtonStyle.Secondary);
            var toTitle = UIKit.Button(bar, "Title", "타이틀로", ShowTitle, ButtonStyle.Secondary);
            UIKit.ChainNavigation(new Selectable[] { again, select, toTitle }, false);
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
            // 건너뛴 Enter가 같은 프레임에 '처음부터 다시'를 누르지 않게 다음 프레임에 선택한다.
            SelectNextFrame(() => again);
        }

        // ───────── 모달 · 일시정지 ─────────

        class Modal
        {
            public GameObject root;
            public RectTransform body, buttons;
            public GameObject previousSelection;
            public bool isPause;
        }

        readonly List<Modal> modalStack = new List<Modal>();
        public int ModalCount => modalStack.Count;

        Modal OpenModal(string title, float width, float height)
        {
            var blocker = UIKit.Panel(overlayRoot, "Modal_" + title, Theme.Shade, ShapeSprites.Square, true);
            blocker.rectTransform.Fill();
            var box = UIKit.Panel(blocker.transform, "Box", Theme.Paper).rectTransform;
            box.Region((1 - width) / 2, (1 - height) / 2, (1 + width) / 2, (1 + height) / 2);
            var head = UIKit.Text(box, "Title", title, 38, Theme.Navy, TextAlignmentOptions.Center, true);
            head.rectTransform.anchorMin = new Vector2(0, 1);
            head.rectTransform.anchorMax = new Vector2(1, 1);
            head.rectTransform.offsetMin = new Vector2(30, -86);
            head.rectTransform.offsetMax = new Vector2(-30, -18);
            var body = UIKit.Rect(box, "Body").Fill(44, 44, 96, 116);
            var buttons = UIKit.Rect(box, "Buttons");
            buttons.anchorMin = Vector2.zero;
            buttons.anchorMax = new Vector2(1, 0);
            buttons.offsetMin = new Vector2(60, 26);
            buttons.offsetMax = new Vector2(-60, 94);
            UIKit.HorizontalList(buttons, 22);
            var modal = new Modal
            {
                root = blocker.gameObject, body = body, buttons = buttons,
                previousSelection = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null
            };
            modalStack.Add(modal);
            if (world) world.MovePlayer(Vector2.zero, 0);
            return modal;
        }

        void CloseModal(Modal modal)
        {
            if (modal == null || !modalStack.Remove(modal)) return;
            Destroy(modal.root);
            if (modal.isPause) SetPaused(false);
            inputLockFrame = Time.frameCount;
            var es = EventSystem.current;
            if (!es) return;
            if (modal.previousSelection && modal.previousSelection.activeInHierarchy) es.SetSelectedGameObject(modal.previousSelection);
            else es.SetSelectedGameObject(null);
        }

        void CloseTopModal()
        {
            if (modalStack.Count > 0) CloseModal(modalStack[modalStack.Count - 1]);
        }

        void CloseAllModals()
        {
            foreach (var m in modalStack) if (m.root) Destroy(m.root);
            modalStack.Clear();
        }

        Button ModalButton(Modal modal, string label, UnityAction action, ButtonStyle style = ButtonStyle.Primary) =>
            UIKit.Button(modal.buttons, label, label, action, style);

        void Confirm(string title, string message, string yes, UnityAction onYes, string no)
        {
            var modal = OpenModal(title, 0.48f, 0.46f);
            var t = UIKit.Text(modal.body, "Text", message, 27, Theme.Navy, TextAlignmentOptions.Center);
            t.rectTransform.Fill();
            var noButton = ModalButton(modal, no, () => CloseModal(modal), ButtonStyle.Secondary);
            var yesButton = ModalButton(modal, yes, () => { CloseModal(modal); onYes?.Invoke(); });
            UIKit.ChainNavigation(new Selectable[] { noButton, yesButton }, false);
            Select(noButton);
        }

        public void OpenPause()
        {
            if (paused || modalStack.Count > 0) return;
            if (State != FieldState.Explore && State != FieldState.Installing && State != FieldState.Scenario && State != FieldState.Review && State != FieldState.Arriving) return;
            SetPaused(true);
            var modal = OpenModal("일시정지", 0.32f, 0.62f);
            modal.isPause = true;
            UIKit.VerticalList(modal.body, 14).childForceExpandHeight = true;
            modal.body.offsetMin = new Vector2(70, 34);
            modal.body.offsetMax = new Vector2(-70, -96);
            var items = new Selectable[]
            {
                UIKit.Button(modal.body, "Resume", "계속하기", () => CloseModal(modal)),
                UIKit.Button(modal.body, "HowTo", "게임 방법", OpenHowTo, ButtonStyle.Secondary),
                UIKit.Button(modal.body, "Sound", "소리 설정", OpenSettings, ButtonStyle.Secondary),
                UIKit.Button(modal.body, "Title", "타이틀로", ShowTitle, ButtonStyle.Secondary)
            };
            UIKit.ChainNavigation(items, true);
            Select(items[0]);
        }

        void OpenHowTo()
        {
            var modal = OpenModal("게임 방법", 0.7f, 0.86f);
            var body = modal.body;

            var legend = UIKit.Rect(body, "Legend").Region(0, 0.52f, 1, 1);
            UIKit.HorizontalList(legend, 14, null, TextAnchor.MiddleCenter, false);
            void Step(string label, System.Action<RectTransform> draw)
            {
                var cell = UIKit.Panel(legend, "Step", Theme.Cream).rectTransform;
                UIKit.Layout(cell, 210, 230);
                var art = UIKit.Rect(cell, "Art").Region(0, 0.38f, 1, 1);
                draw(art);
                var t = UIKit.Text(cell, "Text", label, 22, Theme.Navy, TextAlignmentOptions.Center, true);
                t.rectTransform.Region(0.04f, 0.02f, 0.96f, 0.38f);
            }
            Step("① AI 예측 확인\n(앞으로의 위험)", a => FieldArt.ForecastIcon(a, 90).Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(90, 90)));
            Step("② 가까이 가서\n현장 확인 (E)", a => FieldArt.Magnifier(a, 90).Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(90, 90)));
            Step("③ 수첩에서 자리와\n물품 체크 (Tab)", a => FieldArt.Tick(a, 80, Theme.TealDark).Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(80, 80)));
            Step("④ 승인·설치 후\n시간이 흘러 상황 발생", a => FieldArt.ItemIcon(a, FieldItemVisual.StreetLamp, 90).Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(90, 90)));
            Step("⑤ 준비가 통했는지\n살피고 별 받기", a => UIKit.Text(a, "Star", "★★☆", 44, Theme.Warning, TextAlignmentOptions.Center, true).rectTransform.Fill());

            var text = UIKit.Text(body, "Keys",
                "<b>이동</b> WASD · 방향키    <b>살펴보기</b> E · Enter · 돋보기 클릭    <b>수첩</b> Tab · 수첩 버튼\n" +
                "<b>수첩</b> 방향키로 이동 · Enter로 선택    <b>닫기</b> Esc · Tab (계획은 그대로)    <b>설치·상황 건너뛰기</b> Space\n" +
                "계획을 승인하기 전에는 시간이 흐르지 않아요. 승인하면 물품이 놓이고, 시간이 흐른 뒤 예측한 상황이 와야 준비한 시설이 작동해요.\n" +
                "<b>별</b> ★★★ 필수 문제와 추가 목표 모두 · ★★ 필수 문제 모두 · ★ 필수 문제 일부 · ☆ 해결 없음\n" +
                "한 자리에는 물품 하나. 다시 누르면 취소, 다른 물품을 누르면 교체. 예산을 넘으면 확정할 수 없어요.\n" +
                "<size=20><color=#55607E>※ AI 수첩 '내일'의 예측은 게임에 정해 둔 데이터예요. 물품·비용·평가 규칙은 창작이며 실제 조달 절차를 재현하지 않아요.</color></size>",
                24, Theme.Navy, TextAlignmentOptions.TopLeft);
            text.rectTransform.Region(0, 0, 1, 0.48f);
            text.lineSpacing = 12;
            Select(ModalButton(modal, "닫기", () => CloseModal(modal)));
        }

        void OpenSettings()
        {
            var modal = OpenModal("소리 설정", 0.44f, 0.62f);
            UIKit.VerticalList(modal.body, 16).childForceExpandHeight = false;
            var sfx = SliderRow(modal.body, "효과음", SaveService.SfxVolume, v => { SaveService.SfxVolume = v; sound.ApplySettings(); sound.Play(Sfx.Check); });
            var bgm = SliderRow(modal.body, "배경음", SaveService.BgmVolume, v => { SaveService.BgmVolume = v; sound.ApplySettings(); });
            Button mute = null;
            mute = UIKit.Button(modal.body, "Mute", MuteLabel(), () =>
            {
                SaveService.Muted = !SaveService.Muted;
                sound.ApplySettings();
                UIKit.SetLabel(mute, MuteLabel());
            }, ButtonStyle.Secondary);
            UIKit.Layout(mute, 62);
            var reset = UIKit.Button(modal.body, "ResetRecord", "현장 기록 초기화", () =>
                Confirm("현장 기록을 지울까요?", "현장 수첩 버전의 별 기록만 지워져요.\n이전 버전 점수, 진행 중인 현장, 소리 설정은 그대로예요.", "기록 지우기", () =>
                {
                    FieldRecords.Reset(database.missions.Select(m => m.id));
                    if (titleRecord) titleRecord.text = RecordLabel();
                }, "취소"), ButtonStyle.Danger);
            UIKit.Layout(reset, 62);
            var close = ModalButton(modal, "닫기", () => CloseModal(modal));
            UIKit.ChainNavigation(new Selectable[] { sfx, bgm, mute, reset, close }, true);
            Select(sfx);
        }

        static string MuteLabel() => SaveService.Muted ? "음소거: 켜짐 (눌러서 소리 켜기)" : "음소거: 꺼짐 (눌러서 음소거)";

        Slider SliderRow(RectTransform parent, string label, float value, UnityAction<float> onChange)
        {
            var row = UIKit.Rect(parent, "Row_" + label);
            UIKit.Layout(row, 62);
            UIKit.Text(row, "Label", label, 28, Theme.Navy, TextAlignmentOptions.MidlineLeft, true).rectTransform.Region(0, 0, 0.25f, 1);
            var sliderRt = UIKit.Rect(row, "Slider").Region(0.27f, 0.2f, 1f, 0.8f);
            UIKit.Panel(sliderRt, "Background", Theme.CreamDeep, null, true).rectTransform.Fill();
            var fillArea = UIKit.Rect(sliderRt, "Fill Area").Fill(6, 6, 6, 6);
            var fill = UIKit.Panel(fillArea, "Fill", Theme.Teal);
            fill.rectTransform.Fill();
            var handleArea = UIKit.Rect(sliderRt, "Handle Area").Fill(14, 14, 0, 0);
            var handle = UIKit.Panel(handleArea, "Handle", Theme.TealDark, ShapeSprites.Circle, true);
            handle.rectTransform.sizeDelta = new Vector2(38, 0);
            handle.rectTransform.anchorMin = new Vector2(0, -0.3f);
            handle.rectTransform.anchorMax = new Vector2(0, 1.3f);
            handle.gameObject.AddComponent<FocusRing>();
            var slider = sliderRt.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.value = value;
            UIKit.ApplyStyle(slider, ButtonStyle.Secondary);
            handle.color = Theme.TealDark;
            slider.onValueChanged.AddListener(onChange);
            return slider;
        }

        void OpenCredits()
        {
            var modal = OpenModal("작품 소개와 출처", 0.64f, 0.84f);
            var t = UIKit.Text(modal.body, "Text",
                "<b>조달 히어로: 내일을 준비하는 수첩</b>\n" +
                "'예측하고 미리 준비하는 조달'을 상상해 본 창작 게임입니다.\n실제 계약 절차·평가 방식·제품 성능, 조달청의 공식 사업이나 확정된 미래 계획을 나타내지 않습니다.\n\n" +
                "· 2038년 가상 조달청의 지역 현장 담당자와 AI 수첩 '내일'은 가상 설정이에요.\n" +
                "· '내일'의 예측은 게임에 정해 둔 문장과 규칙이며, 실제 AI가 분석하거나 학습하지 않아요.\n" +
                "· 새봄시, 모든 물품과 비용, 별 평가는 게임을 위한 창작 규칙이에요.\n" +
                "· 조달청이 승인하거나 공식 제작한 게임이 아니에요. 공식 캐릭터와 로고를 쓰지 않았어요.\n\n" +
                "<b>정책 참고자료</b>\n조달청, 2026-08-18 혁신제품 시범구매 관련 보도자료\n<size=20>" + PolicyUrl + "</size>\n\n" +
                "<b>글꼴</b>  Pretendard (길형진, SIL Open Font License 1.1)\n" +
                "<b>그래픽 · 효과음 · 배경음</b>  기본 도형과 코드로 직접 생성\n" +
                "<b>제작</b>  Unity 6000.0.61f1 · AI 도구를 활용해 제작했어요 (자세한 내용은 제출 문서 참고)",
                23, Theme.Navy, TextAlignmentOptions.TopLeft);
            t.rectTransform.Fill();
            t.lineSpacing = 8;
            var close = ModalButton(modal, "닫기", () => CloseModal(modal));
            var link = ModalButton(modal, "보도자료 링크 열기", () => Application.OpenURL(PolicyUrl), ButtonStyle.Secondary);
            UIKit.ChainNavigation(new Selectable[] { link, close }, false);
            Select(close);
        }
    }
}
