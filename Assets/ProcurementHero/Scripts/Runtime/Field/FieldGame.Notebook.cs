using System.Collections;
using System.Linq;
using ProcurementHero.Core.Field;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProcurementHero.Field
{
    public partial class FieldGame
    {
        RectTransform notebookRoot, mapArea, observedRow, rightHeader, itemList, planRow, gaugeFill, reasonRow;
        TextMeshProUGUI gaugeText;
        Button confirmButton, closeButton;
        CanvasGroup notebookGroup;

        static readonly Color PageColor = Theme.Hex(0xFFF9EC);
        static readonly Color CoverColor = Theme.Hex(0x7A4E2D);
        static readonly Color InkFaint = Theme.WithAlpha(Theme.Navy, 0.12f);

        public bool NotebookOpen => State == FieldState.Notebook;
        /// <summary>수첩을 펴거나 닫거나 도장을 찍는 중이면 true. 이때 Tab·Esc·확정은 무시한다.</summary>
        public bool NotebookBusy => notebookBusy;

        FieldBook.Parts bookParts;
        float shownCost;
        Coroutine gaugeTween;

        /// <summary>담당자 손의 화면 위치(수첩이 날아오고 돌아가는 곳).</summary>
        Vector2 HandOnCanvas() => world ? ToCanvas(world.PlayerPosition + new Vector2(0.4f, 0.2f)) : Vector2.zero;

        public void OpenNotebook()
        {
            if (State != FieldState.Explore || paused || modalStack.Count > 0 || notebookBusy) return;
            SetState(FieldState.Notebook);
            notebookBusy = true;
            if (notebookBadge) notebookBadge.gameObject.SetActive(false);
            if (magnifier) magnifier.gameObject.SetActive(false);
            StartCoroutine(OpenSequence(screenToken));
        }

        IEnumerator OpenSequence(int token)
        {
            // 1) 담당자가 손의 수첩을 들어 올린다.
            yield return Pose(0f, 1f, 0.12f);
            if (token != screenToken) yield break;
            // 2) 수첩이 손에서 화면 앞으로 날아오며 커지고, 3) 표지가 넘어가 페이지가 펼쳐진다.
            BuildNotebook();
            RefreshNotebook();
            shownCost = FieldRules.Cost(Spec, Plan);
            ApplyGauge(shownCost);
            yield return FieldBook.Open(bookParts, HandOnCanvas(), () => sound.Play(Sfx.NotebookOpen));
            if (token != screenToken || !notebookRoot) yield break;
            notebookBusy = false;
            Select(SpotButton(SelectedSpot));
        }

        /// <summary>계획은 그대로 두고 수첩만 닫는다(책이 닫히며 담당자 손으로 돌아간다).</summary>
        public void CloseNotebook()
        {
            if (State != FieldState.Notebook || notebookBusy) return;
            notebookBusy = true;
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
            StartCoroutine(CloseSequence(screenToken));
        }

        IEnumerator CloseSequence(int token)
        {
            sound.Play(Sfx.NotebookOpen);
            yield return CloseBookAnimation();
            if (token != screenToken) yield break;
            HideNotebook();
            yield return LowerNotebookPose();
            if (token != screenToken) yield break;
            notebookBusy = false;
            SetState(FieldState.Explore);
            // 현장에서는 키보드가 관찰·이동에 쓰이므로 숨겨진 수첩 UI에 선택이 남지 않게 비운다.
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
        }

        IEnumerator CloseBookAnimation()
        {
            if (bookParts == null || !bookParts.book) yield break;
            if (notebookRoot && notebookRoot.TryGetComponent(out Image dim)) dim.raycastTarget = true;
            yield return FieldBook.Close(bookParts, HandOnCanvas());
        }

        IEnumerator LowerNotebookPose() => Pose(1f, 0f, 0.12f);

        IEnumerator Pose(float from, float to, float duration)
        {
            float t = 0;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                if (world) world.SetNotebookPose(Mathf.Lerp(from, to, Mathf.Clamp01(t / duration)));
                yield return null;
            }
            if (world) world.SetNotebookPose(to);
        }

        void HideNotebook()
        {
            if (notebookRoot) Destroy(notebookRoot.gameObject);
            notebookRoot = null;
            bookParts = null;
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
        }

        /// <summary>확정 도장(오른쪽 페이지).</summary>
        IEnumerator StampConfirm()
        {
            if (bookParts == null) yield break;
            bookParts.group.interactable = false;
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
            yield return FieldBook.Stamp(bookParts, bookParts.right, new Vector2(0.62f, 0.5f), "확정", Theme.Success);
            float t = 0;
            while (t < 0.25f) { t += Time.unscaledDeltaTime; yield return null; }
        }

        void BuildNotebook()
        {
            // 현장 HUD보다 위에 그린다(수첩이 열려 있는 동안 HUD는 가려진다).
            var dim = UIKit.Panel(overlayRoot, "Notebook", Theme.WithAlpha(Theme.Navy, 0.45f), ShapeSprites.Square, true);
            notebookRoot = dim.rectTransform;
            notebookRoot.Fill();

            bookParts = FieldBook.Build(notebookRoot, new Vector2(0.05f, 0.04f), new Vector2(0.95f, 0.955f), true);
            notebookGroup = bookParts.group;
            var left = bookParts.left;
            var right = bookParts.right;

            // 왼쪽 페이지: 현장 지도와 관찰 기록
            var lt = UIKit.Text(left, "Title", $"현장 지도 · {Mission.placeName}", 30, Theme.Navy, TextAlignmentOptions.MidlineLeft, true);
            lt.rectTransform.Region(0.05f, 0.9f, 0.95f, 0.98f);
            var mapHolder = UIKit.Rect(left, "MapHolder").Region(0.04f, 0.38f, 0.96f, 0.895f);
            var map = UIKit.Panel(mapHolder, "Map", Theme.Hex(0xEDE3CC)).rectTransform;
            map.anchorMin = map.anchorMax = new Vector2(0.5f, 0.5f);
            var fitter = map.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = FieldWorld.HalfWidth / FieldWorld.HalfHeight;
            mapArea = map;
            // 지도 모서리 테이프
            foreach (var corner in new[] { new Vector2(0, 1), new Vector2(1, 1) })
            {
                var tape = UIKit.Panel(map, "Tape", Theme.WithAlpha(Theme.Hex(0xF2D49B), 0.85f), ShapeSprites.Square).rectTransform;
                tape.Place(corner, Vector2.zero, new Vector2(70, 22));
                tape.localRotation = Quaternion.Euler(0, 0, corner.x > 0 ? -30 : 30);
            }

            var obsTitle = UIKit.Text(left, "ObservedTitle", "관찰 기록", 24, Theme.NavySoft, TextAlignmentOptions.MidlineLeft, true);
            obsTitle.rectTransform.Region(0.05f, 0.315f, 0.6f, 0.365f);
            observedRow = UIKit.Rect(left, "Observed").Region(0.04f, 0.2f, 0.96f, 0.31f);
            UIKit.HorizontalList(observedRow, 10, null, TextAnchor.MiddleLeft, false);

            // 오른쪽 페이지: 고른 자리에 놓을 물품
            rightHeader = UIKit.Rect(right, "Header").Region(0.05f, 0.84f, 0.8f, 0.98f);
            itemList = UIKit.Rect(right, "Items").Region(0.05f, 0.22f, 0.95f, 0.83f);
            UIKit.VerticalList(itemList, 12).childForceExpandHeight = false;

            closeButton = UIKit.Button(right, "Close", "닫기 (Tab)", CloseNotebook, ButtonStyle.Secondary, 22);
            ((RectTransform)closeButton.transform).Region(0.79f, 0.895f, 0.97f, 0.97f);

            // 아래 띠: 전체 계획, 예산, 확정
            var band = bookParts.band;
            planRow = UIKit.Rect(band, "Plan").Region(0.01f, 0.08f, 0.47f, 0.92f);
            UIKit.HorizontalList(planRow, 10, null, TextAnchor.MiddleLeft, true);

            var gauge = UIKit.Rect(band, "Budget").Region(0.48f, 0.12f, 0.74f, 0.88f);
            UIKit.Shape(gauge, "Coin", ShapeSprites.Circle, Theme.Hex(0xF2C14E), new Vector2(0, 0.72f), new Vector2(20, 0), new Vector2(30, 30));
            gaugeText = UIKit.Text(gauge, "Label", "", 26, Theme.Navy, TextAlignmentOptions.MidlineLeft, true);
            gaugeText.rectTransform.Region(0, 0.5f, 1, 1).offsetMin = new Vector2(44, 0);
            var track = UIKit.Panel(gauge, "Track", Theme.WithAlpha(Theme.Navy, 0.15f)).rectTransform;
            track.Region(0, 0.08f, 1, 0.42f);
            gaugeFill = UIKit.Panel(track, "Fill", Theme.Teal).rectTransform;
            gaugeFill.anchorMin = Vector2.zero;
            gaugeFill.pivot = new Vector2(0, 0.5f);

            confirmButton = UIKit.Button(band, "Confirm", "계획 확정", () => ConfirmPlan(), ButtonStyle.Primary, 32);
            ((RectTransform)confirmButton.transform).Region(0.755f, 0.42f, 0.99f, 0.94f);
            reasonRow = UIKit.Rect(band, "Reason").Region(0.755f, 0.04f, 0.99f, 0.4f);
        }

        Button SpotButton(string spotId)
        {
            if (!mapArea || spotId == null) return null;
            var t = mapArea.Find("Spot_" + spotId);
            return t ? t.GetComponent<Button>() : null;
        }

        /// <summary>현재 계획에서 지도·물품 목록·예산을 다시 그린다. 비용은 매번 배정 목록에서 새로 계산한다.</summary>
        void RefreshNotebook()
        {
            if (!notebookRoot) return;
            string keep = EventSystem.current && EventSystem.current.currentSelectedGameObject
                ? EventSystem.current.currentSelectedGameObject.name : null;

            var preview = FieldRules.Evaluate(Spec, Plan);
            DrawMap(preview);
            DrawObserved();
            DrawItems();
            DrawPlan();
            DrawBudgetAndConfirm(preview);

            // 다시 그린 뒤에도 같은 항목에 키보드 선택을 되돌린다.
            if (keep != null)
            {
                var again = notebookRoot.GetComponentsInChildren<Selectable>(true).FirstOrDefault(s => s.name == keep && s.gameObject.activeInHierarchy);
                if (again) Select(again);
                else SelectNextFrame(() => SpotButton(SelectedSpot));
            }
        }

        static Vector2 MapPoint(Vector2 world) =>
            new Vector2((world.x + FieldWorld.HalfWidth) / (FieldWorld.HalfWidth * 2), (world.y + FieldWorld.HalfHeight) / (FieldWorld.HalfHeight * 2));

        static void AnchorRect(RectTransform rt, Vector2 center, Vector2 size)
        {
            rt.anchorMin = MapPoint(center - size / 2);
            rt.anchorMax = MapPoint(center + size / 2);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        void DrawMap(FieldEvaluation preview)
        {
            UIKit.Clear(mapArea);
            foreach (var p in Mission.paths)
                AnchorRect(UIKit.Panel(mapArea, "Path", Theme.Hex(0xD8CBAE)).rectTransform, p.center, p.size);
            foreach (var o in Mission.obstacles)
            {
                Color c = o.kind == FieldObstacleKind.Puddle ? Theme.WithAlpha(Theme.Water, 0.6f)
                    : o.kind == FieldObstacleKind.Tree ? Theme.Hex(0x9CC48E) : Theme.Hex(0xB7A99A);
                AnchorRect(UIKit.Panel(mapArea, "Block", c).rectTransform, o.center, o.size);
            }

            // 문제 구역: 관찰한 곳만 '해결 예상'을 보여 준다(판정과 같은 데이터, 관찰하지 않은 곳은 숨김).
            var shown = FieldRules.PreviewSolved(preview, observed);
            foreach (var p in Mission.problems)
            {
                bool seen = observed.Contains(p.id);
                bool lit = seen && shown.Contains(p.id);
                Color fill = !seen ? Theme.WithAlpha(Theme.NavySoft, 0.22f) : lit ? Theme.WithAlpha(FieldArt.Lamp, 0.85f) : Theme.WithAlpha(Theme.Navy, 0.42f);
                var zone = UIKit.Panel(mapArea, "Zone_" + p.id, fill).rectTransform;
                AnchorRect(zone, p.zoneCenter, p.zoneSize);
                if (highlightUnsolved.Contains(p.id) && seen)
                {
                    var outline = zone.gameObject.AddComponent<Outline>();
                    outline.effectColor = Theme.Danger;
                    outline.effectDistance = new Vector2(4, -4);
                }
                if (!seen)
                {
                    // 아직 조사하지 않은 곳: 사선 무늬 + 물음표 + '조사 전'. 문제 종류와 해결 예상은 숨긴다.
                    FieldArt.UnknownPattern(zone, Theme.WithAlpha(Theme.Navy, 0.18f));
                    UIKit.Text(zone, "Unknown", "?", 34, Theme.Navy, TextAlignmentOptions.Center, true).rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 8), new Vector2(40, 40));
                    UIKit.Text(zone, "Name", "조사 전", 16, Theme.Navy, TextAlignmentOptions.Center, true).rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, -22), new Vector2(150, 22));
                    continue;
                }
                FieldArt.ProblemIcon(zone, p.kind, 40).Place(new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(40, 40));
                var name = UIKit.Text(zone, "Name", (p.required ? "" : "추가 · ") + p.displayName, 17, lit ? Theme.Navy : Color.white, TextAlignmentOptions.Center, true);
                name.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, -22), new Vector2(160, 24));
                // 해결 예상은 색만이 아니라 표시 모양으로도 구분한다: 체크 = 밝아짐/해결, 점 세 개 = 그대로.
                if (lit) FieldArt.Tick(zone, 26, Theme.Success).Place(new Vector2(1, 1), new Vector2(-16, -16), new Vector2(26, 26));
                else UIKit.Text(zone, "Still", "…", 22, Color.white, TextAlignmentOptions.Center, true).rectTransform.Place(new Vector2(1, 1), new Vector2(-18, -14), new Vector2(30, 24));
            }

            // 설치 지점 버튼
            foreach (var s in Mission.spots)
            {
                string spotId = s.id;
                bool selected = spotId == SelectedSpot;
                var btn = UIKit.Button(mapArea, "Spot_" + spotId, "", () => SelectSpot(spotId), ButtonStyle.Secondary);
                Destroy(btn.transform.Find("Label").gameObject);
                var rt = (RectTransform)btn.transform;
                var p = MapPoint(s.position);
                rt.anchorMin = rt.anchorMax = p;
                rt.sizeDelta = new Vector2(selected ? 70 : 58, selected ? 70 : 58);
                var img = (Image)btn.targetGraphic;
                img.sprite = ShapeSprites.Circle;
                img.type = Image.Type.Simple;
                img.color = selected ? Theme.Teal : Theme.Paper;
                string item = Plan.ItemAt(spotId);
                if (item != null)
                    FieldArt.ItemIcon(rt, Mission.Item(item).visual, selected ? 52 : 44).Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(44, 44));
                else
                    UIKit.Shape(rt, "Empty", ShapeSprites.Circle, selected ? Theme.Paper : Theme.WithAlpha(Theme.Teal, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18, 18)).raycastTarget = false;
                var label = UIKit.Text(rt, "Name", s.displayName, 17, Theme.Navy, TextAlignmentOptions.Center, true);
                label.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0, -16), new Vector2(150, 24));
                var bg = label.gameObject.AddComponent<Outline>();
                bg.effectColor = Theme.WithAlpha(PageColor, 0.9f);
            }
        }

        void SelectSpot(string spotId)
        {
            if (State != FieldState.Notebook) return;
            SelectedSpot = spotId;
            sound.Play(Sfx.Select);
            RefreshNotebook();
        }

        void DrawObserved()
        {
            UIKit.Clear(observedRow);
            foreach (var p in Mission.problems.Where(p => p.mustObserve || observed.Contains(p.id)))
            {
                bool seen = observed.Contains(p.id);
                var card = UIKit.Panel(observedRow, "Obs_" + p.id, seen ? Theme.Paper : InkFaint);
                UIKit.Layout(card, 90, 150);
                if (seen) FieldArt.ProblemIcon(card.transform, p.kind, 46).Place(new Vector2(0.5f, 1f), new Vector2(0, -30), new Vector2(46, 46));
                else FieldArt.Magnifier(card.transform, 40).Place(new Vector2(0.5f, 1f), new Vector2(0, -28), new Vector2(40, 40));
                var t = UIKit.Text(card.transform, "Name", seen ? p.displayName : "아직 못 봄", 18, seen ? Theme.Navy : Theme.NavySoft, TextAlignmentOptions.Center, seen);
                t.rectTransform.Region(0, 0, 1, 0.36f);
                if (seen && !string.IsNullOrEmpty(p.memo))
                {
                    // 관찰한 곳은 한 줄 메모를 남긴다.
                    UIKit.Layout(card, 90, 190);
                    t.text = $"<b>{p.displayName}</b>\n<size=15><color=#{ColorUtility.ToHtmlStringRGB(Theme.NavySoft)}>{p.memo}</color></size>";
                    t.rectTransform.Region(0, 0, 1, 0.48f);
                    t.enableAutoSizing = true;
                    t.fontSizeMin = 12;
                    t.fontSizeMax = 18;
                }
                if (seen) FieldArt.Tick(card.transform, 24, Theme.Success).Place(new Vector2(1, 1), new Vector2(-14, -14), new Vector2(24, 24));
            }
        }

        void DrawItems()
        {
            UIKit.Clear(rightHeader);
            UIKit.Clear(itemList);
            var spot = Mission.Spot(SelectedSpot);
            if (spot == null) return;

            var h = UIKit.Text(rightHeader, "Spot", $"<size=22><color=#{ColorUtility.ToHtmlStringRGB(Theme.NavySoft)}>고른 자리</color></size>\n{spot.displayName}", 34,
                Theme.Navy, TextAlignmentOptions.MidlineLeft, true);
            h.rectTransform.Fill();

            string current = Plan.ItemAt(spot.id);
            foreach (var item in Mission.items)
            {
                string itemId = item.id;
                bool allowed = spot.options.Any(o => o.itemId == itemId);
                bool on = current == itemId;
                var row = UIKit.Button(itemList, "Check_" + itemId, "", () => ToggleItem(itemId), ButtonStyle.Secondary);
                Destroy(row.transform.Find("Label").gameObject);
                UIKit.Layout(row, 132);
                ((Image)row.targetGraphic).color = on ? Theme.TealPale : allowed ? Theme.Paper : InkFaint;
                row.interactable = allowed;
                var rt = (RectTransform)row.transform;

                FieldArt.ItemIcon(rt, item.visual, 100).Place(new Vector2(0, 0.5f), new Vector2(70, 0), new Vector2(100, 100));
                var name = UIKit.Text(rt, "Name", item.displayName, 32, allowed ? Theme.Navy : Theme.NavySoft, TextAlignmentOptions.MidlineLeft, true);
                name.rectTransform.Region(0, 0.5f, 0.62f, 0.95f).offsetMin = new Vector2(140, 0);
                var hint = UIKit.Text(rt, "Hint", allowed ? item.hint : "이 자리에 설치 불가", 22, allowed ? Theme.TealDark : Theme.Danger, TextAlignmentOptions.MidlineLeft, !allowed);
                hint.rectTransform.Region(0, 0.08f, 0.62f, 0.5f).offsetMin = new Vector2(140, 0);

                var price = UIKit.Rect(rt, "Price").Region(0.62f, 0.2f, 0.8f, 0.8f);
                UIKit.Shape(price, "Coin", ShapeSprites.Circle, Theme.Hex(0xF2C14E), new Vector2(0, 0.5f), new Vector2(16, 0), new Vector2(28, 28)).raycastTarget = false;
                UIKit.Text(price, "Cost", item.cost.ToString(), 30, Theme.Navy, TextAlignmentOptions.MidlineLeft, true).rectTransform.Fill(36, 0, 0, 0);

                var box = UIKit.Panel(rt, "Box", allowed ? Color.white : Theme.WithAlpha(Color.white, 0.4f)).rectTransform;
                box.Place(new Vector2(1, 0.5f), new Vector2(-56, 0), new Vector2(68, 68));
                box.gameObject.AddComponent<Outline>().effectColor = Theme.Navy;
                if (on) FieldArt.Tick(box, 58, Theme.TealDark).Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(58, 58));
                else if (!allowed) UIKit.Text(box, "No", "×", 40, Theme.Danger, TextAlignmentOptions.Center, true).rectTransform.Fill();
            }
            var tip = UIKit.Text(itemList, "Tip", "체크하면 이 자리에 하나 놓아요 · 다시 누르면 취소 · 다른 물품은 교체", 20, Theme.NavySoft, TextAlignmentOptions.Center);
            UIKit.Layout(tip, 40);
        }

        public AssignOutcome ToggleItem(string itemId)
        {
            if (State != FieldState.Notebook || SelectedSpot == null) return AssignOutcome.UnknownSpot;
            if (notebookBusy) return AssignOutcome.UnknownSpot;
            string before = Plan.ItemAt(SelectedSpot);
            var outcome = Plan.Toggle(Spec, SelectedSpot, itemId);
            sound.Play(outcome == AssignOutcome.Cleared ? Sfx.Uncheck : outcome == AssignOutcome.NotAllowed ? Sfx.Fail : Sfx.Check);
            RefreshNotebook();
            // 책 전체를 다시 펴지 않고, 바뀐 체크 칸만 짧게 움직인다.
            if (outcome == AssignOutcome.Assigned || outcome == AssignOutcome.Replaced) StartCoroutine(DrawTick(itemId));
            if (outcome == AssignOutcome.Cleared || outcome == AssignOutcome.Replaced) StartCoroutine(EraseTick(before));
            if (outcome == AssignOutcome.NotAllowed) StartCoroutine(Shake(itemList ? itemList.Find("Check_" + itemId) : null));
            return outcome;
        }

        /// <summary>테스트와 자동 확인용.</summary>
        public void SelectSpotForTest(string spotId) => SelectSpot(spotId);

        void DrawPlan()
        {
            UIKit.Clear(planRow);
            foreach (var s in Mission.spots)
            {
                string item = Plan.ItemAt(s.id);
                var slot = UIKit.Panel(planRow, "Plan_" + s.id, item != null ? Theme.Paper : InkFaint);
                if (s.id == SelectedSpot) slot.gameObject.AddComponent<Outline>().effectColor = Theme.Teal;
                if (item != null) FieldArt.ItemIcon(slot.transform, Mission.Item(item).visual, 54).Place(new Vector2(0, 0.5f), new Vector2(40, 0), new Vector2(54, 54));
                else UIKit.Shape(slot.transform, "Empty", ShapeSprites.Circle, Theme.WithAlpha(Theme.Navy, 0.2f), new Vector2(0, 0.5f), new Vector2(40, 0), new Vector2(30, 30)).raycastTarget = false;
                var t = UIKit.Text(slot.transform, "Name", s.displayName + "\n" + (item != null ? Mission.Item(item).displayName : "비어 있음"), 18,
                    item != null ? Theme.Navy : Theme.NavySoft, TextAlignmentOptions.MidlineLeft);
                t.rectTransform.Fill(74, 6, 4, 4);
                t.enableAutoSizing = true;
                t.fontSizeMin = 13;
                t.fontSizeMax = 18;
            }
        }

        void DrawBudgetAndConfirm(FieldEvaluation preview)
        {
            // 숫자와 게이지는 현재 배정 목록의 비용으로 부드럽게 따라간다(누적하지 않음).
            if (gaugeTween != null) StopCoroutine(gaugeTween);
            gaugeTween = StartCoroutine(TweenGauge(preview.Cost));

            var block = FieldRules.CanConfirm(Spec, Plan, observed);
            confirmButton.interactable = block == ConfirmBlock.None && !confirmLocked;
            UIKit.Clear(reasonRow);
            string text;
            Color color = Theme.Danger;
            switch (block)
            {
                case ConfirmBlock.ObservationMissing:
                    FieldArt.Magnifier(reasonRow, 34).Place(new Vector2(0, 0.5f), new Vector2(20, 0), new Vector2(34, 34));
                    text = $"관찰이 {FieldRules.MissingObservations(Spec, observed)}곳 남았어요";
                    break;
                case ConfirmBlock.NothingAssigned:
                    UIKit.Shape(reasonRow, "Spot", ShapeSprites.Circle, Theme.Teal, new Vector2(0, 0.5f), new Vector2(20, 0), new Vector2(26, 26));
                    text = "자리를 골라 물품에 체크해요";
                    break;
                case ConfirmBlock.OverBudget:
                    UIKit.Shape(reasonRow, "Warn", ShapeSprites.Triangle, Theme.Danger, new Vector2(0, 0.5f), new Vector2(20, 0), new Vector2(32, 30));
                    text = $"예산을 {preview.Cost - Mission.budget} 넘었어요";
                    break;
                default:
                    FieldArt.Tick(reasonRow, 30, Theme.Success).Place(new Vector2(0, 0.5f), new Vector2(20, 0), new Vector2(30, 30));
                    text = "확정할 수 있어요";
                    color = Theme.Success;
                    break;
            }
            var t = UIKit.Text(reasonRow, "Text", text, 21, color, TextAlignmentOptions.MidlineLeft, true);
            t.rectTransform.Fill(44, 0, 0, 0);
            t.enableAutoSizing = true;
            t.fontSizeMin = 14;
            t.fontSizeMax = 21;
        }
    
        IEnumerator TweenGauge(int target)
        {
            float from = shownCost, t = 0;
            while (t < 0.22f)
            {
                t += Time.unscaledDeltaTime;
                ApplyGauge(Mathf.Lerp(from, target, Mathf.Clamp01(t / 0.22f)));
                yield return null;
            }
            ApplyGauge(target);
        }

        void ApplyGauge(float cost)
        {
            shownCost = cost;
            if (!gaugeText) return;
            int shown = Mathf.RoundToInt(cost);
            bool over = shown > Mission.budget;
            gaugeText.text = over ? $"예산 {shown} / {Mission.budget}  <color=#D24B46>△ 초과</color>" : $"예산 {shown} / {Mission.budget}";
            float ratio = Mission.budget > 0 ? Mathf.Clamp01(cost / Mission.budget) : 0;
            gaugeFill.anchorMax = new Vector2(ratio, 1);
            gaugeFill.offsetMin = gaugeFill.offsetMax = Vector2.zero;
            gaugeFill.GetComponent<Image>().color = over ? Theme.Danger : Theme.Teal;
        }

        /// <summary>체크 표시가 짧게 그려진다(짧은 획 → 긴 획).</summary>
        IEnumerator DrawTick(string itemId)
        {
            var tick = itemList ? itemList.Find($"Check_{itemId}/Box/Tick") : null;
            if (!tick) yield break;
            var shortStroke = tick.Find("Short");
            var longStroke = tick.Find("Long");
            shortStroke.localScale = new Vector3(1, 0, 1);
            longStroke.localScale = new Vector3(1, 0, 1);
            float t = 0;
            while (t < 0.16f && tick)
            {
                t += Time.unscaledDeltaTime;
                float a = Mathf.Clamp01(t / 0.07f), b = Mathf.Clamp01((t - 0.06f) / 0.1f);
                shortStroke.localScale = new Vector3(1, a, 1);
                longStroke.localScale = new Vector3(1, b, 1);
                tick.localScale = Vector3.one * (1f + Mathf.Sin(Mathf.Clamp01(t / 0.16f) * Mathf.PI) * 0.15f);
                yield return null;
            }
            if (tick)
            {
                shortStroke.localScale = longStroke.localScale = tick.localScale = Vector3.one;
            }
        }

        /// <summary>취소·교체된 체크가 작아지며 사라진다.</summary>
        IEnumerator EraseTick(string itemId)
        {
            var box = itemId != null && itemList ? itemList.Find($"Check_{itemId}/Box") as RectTransform : null;
            if (!box) yield break;
            var ghost = FieldArt.Tick(box, 58, Theme.WithAlpha(Theme.Danger, 0.8f));
            ghost.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(58, 58));
            var images = ghost.GetComponentsInChildren<Image>();
            float t = 0;
            while (t < 0.18f && ghost)
            {
                t += Time.unscaledDeltaTime;
                float e = Mathf.Clamp01(t / 0.18f);
                ghost.localScale = Vector3.one * (1 - e * 0.6f);
                ghost.localRotation = Quaternion.Euler(0, 0, e * 25f);
                foreach (var img in images) img.color = Theme.WithAlpha(Theme.Danger, 0.8f * (1 - e));
                yield return null;
            }
            if (ghost) Destroy(ghost.gameObject);
        }

        static IEnumerator Shake(Transform target)
        {
            if (!target) yield break;
            var start = target.localPosition;
            float t = 0;
            while (t < 0.2f && target)
            {
                t += Time.unscaledDeltaTime;
                target.localPosition = start + new Vector3(Mathf.Sin(t * 60f) * 6f * (1 - t / 0.2f), 0, 0);
                yield return null;
            }
            if (target) target.localPosition = start;
        }
    }
}
