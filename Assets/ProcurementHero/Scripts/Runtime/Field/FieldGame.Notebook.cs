using System;
using System.Collections.Generic;
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
            yield return FieldBook.Stamp(bookParts, bookParts.right, new Vector2(0.62f, 0.5f), "승인", Theme.Success);
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
            // 지도는 현장을 옮긴 디지털 트윈이라는 설정: 물품을 고르면 예상 영향 구역을 가상으로 비춰 본다(새 시뮬레이션은 없다).
            var lt = UIKit.Text(left, "Title", $"디지털 트윈 지도 · {Mission.placeName}", 30, Theme.Navy, TextAlignmentOptions.MidlineLeft, true);
            lt.rectTransform.Region(0.05f, 0.92f, 0.95f, 0.985f);
            lt.enableAutoSizing = true;
            lt.fontSizeMin = 20;
            lt.fontSizeMax = 30;
            var fc = UIKit.Text(left, "Forecast", $"<color=#{ColorUtility.ToHtmlStringRGB(Theme.Danger)}>예측</color> {Mission.forecast}  ·  {Mission.eventTime}까지 준비", 19, Theme.NavySoft, TextAlignmentOptions.MidlineLeft);
            fc.rectTransform.Region(0.05f, 0.88f, 0.95f, 0.92f);
            fc.enableAutoSizing = true;
            fc.fontSizeMin = 13;
            fc.fontSizeMax = 19;
            var mapHolder = UIKit.Rect(left, "MapHolder").Region(0.04f, 0.4f, 0.96f, 0.875f);
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

            // 지도 범례: 무늬·기호·짧은 글로 '예상'과 '현장 확인'을 구분한다.
            var legend = UIKit.Text(left, "Legend",
                "<b>빗금·?</b> AI 예상(확인 전)   <b>단색</b> 현장 확인   <b>청록 테두리</b> 고른 물품의 예상 영향   <b>✓</b> 대비 예상",
                17, Theme.NavySoft, TextAlignmentOptions.MidlineLeft);
            legend.rectTransform.Region(0.05f, 0.36f, 0.95f, 0.4f);
            legend.enableAutoSizing = true;
            legend.fontSizeMin = 12;
            legend.fontSizeMax = 17;
            var obsTitle = UIKit.Text(left, "ObservedTitle", "현장 확인 기록", 24, Theme.NavySoft, TextAlignmentOptions.MidlineLeft, true);
            obsTitle.rectTransform.Region(0.05f, 0.315f, 0.6f, 0.36f);
            observedRow = UIKit.Rect(left, "Observed").Region(0.04f, 0.2f, 0.96f, 0.31f);
            UIKit.HorizontalList(observedRow, 10, null, TextAnchor.MiddleLeft, false);

            // 오른쪽 페이지: 고른 자리에 놓을 물품
            rightHeader = UIKit.Rect(right, "Header").Region(0.05f, 0.84f, 0.8f, 0.98f);
            itemList = UIKit.Rect(right, "Items").Region(0.05f, 0.22f, 0.95f, 0.83f);
            UIKit.VerticalList(itemList, 12).childForceExpandHeight = false;

            closeButton = UIKit.Button(right, "Close", "닫기 (Tab)", CloseNotebook, ButtonStyle.Secondary, 22);
            ((RectTransform)closeButton.transform).Region(0.79f, 0.895f, 0.97f, 0.97f);
            // 보조 기능: 2·3 현장에서 필수 관찰을 마치면 'AI 단서'(한 가지 단서) → 원하면 '배치안까지 보기'(전체 추천안).
            recommendButton = null;
            if (MissionIndex > 0)
            {
                recommendButton = UIKit.Button(right, "AiHint", "AI 단서", OpenHint, ButtonStyle.Secondary, 20);
                ((RectTransform)recommendButton.transform).Region(0.79f, 0.84f, 0.97f, 0.89f);
            }

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

            confirmButton = UIKit.Button(band, "Confirm", "계획 승인", () => ConfirmPlan(), ButtonStyle.Primary, 32);
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
            ShowReach(null);
            if (recommendButton) recommendButton.interactable = !confirmLocked && FieldRules.MissingObservations(Spec, observed) == 0;

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

        /// <summary>관찰한 구역마다 '이 물품이 닿는 곳' 테두리. 물품 줄을 고르거나 마우스를 올리면 켠다.</summary>
        readonly Dictionary<string, GameObject> zoneReach = new Dictionary<string, GameObject>();

        void DrawMap(FieldEvaluation preview)
        {
            UIKit.Clear(mapArea);
            zoneReach.Clear();
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
                    UIKit.Text(zone, "Name", "AI 예상 · 확인 전", 16, Theme.Navy, TextAlignmentOptions.Center, true).rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, -22), new Vector2(170, 22));
                    continue;
                }
                var reach = UIKit.Panel(zone, "Reach", Theme.WithAlpha(Theme.Teal, 0.12f)).rectTransform;
                reach.Fill();
                reach.GetComponent<Image>().raycastTarget = false;
                var reachLine = reach.gameObject.AddComponent<Outline>();
                reachLine.effectColor = Theme.Teal;
                reachLine.effectDistance = new Vector2(5, -5);
                reachLine.useGraphicAlpha = false;
                reach.gameObject.SetActive(false);
                zoneReach[p.id] = reach.gameObject;
                FieldArt.ProblemIcon(zone, p.kind, 40).Place(new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(40, 40));
                var name = UIKit.Text(zone, "Name", (p.required ? "" : "추가 · ") + p.displayName, 17, lit ? Theme.Navy : Color.white, TextAlignmentOptions.Center, true);
                // 이름은 구역 위쪽에 둔다(구역 안의 설치 자리 이름과 겹치지 않게).
                name.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0, -14), new Vector2(160, 24));
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
            if (State != FieldState.Notebook || recommendPanel) return;
            SelectedSpot = spotId;
            MarkSaveDirty();
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
                    UIKit.Layout(card, 150, 230);
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
                // 이 자리에 놓으면 닿는 곳: 관찰한 문제 구역만 이름으로 알려 준다(관찰 전 구역은 숨김).
                string hintText = allowed ? $"{item.hint}\n<b>예상 영향</b> {ReachText(spot, itemId)}" : "이 자리에 설치 불가";
                var hint = UIKit.Text(rt, "Hint", hintText, 22, allowed ? Theme.TealDark : Theme.Danger, TextAlignmentOptions.MidlineLeft, !allowed);
                hint.rectTransform.Region(0, 0.04f, 0.62f, 0.52f).offsetMin = new Vector2(140, 0);
                hint.enableAutoSizing = true;
                hint.fontSizeMin = 15;
                hint.fontSizeMax = 22;
                if (allowed)
                {
                    var hover = row.gameObject.AddComponent<ReachHover>();
                    hover.Changed = on2 => ShowReach(on2 ? itemId : null);
                }

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

        string ReachText(FieldSpotEntry spot, string itemId)
        {
            var option = spot.options.FirstOrDefault(o => o.itemId == itemId);
            var names = option == null ? new List<string>()
                : Mission.problems.Where(p => option.solves.Contains(p.id) && observed.Contains(p.id)).Select(p => p.displayName).ToList();
            return names.Count > 0 ? string.Join(" · ", names) : "살펴본 곳 중 없음";
        }

        /// <summary>고른 자리에 이 물품을 놓으면 닿는 관찰 구역을 지도에서 테두리로 보여 준다(null이면 끈다).</summary>
        void ShowReach(string itemId)
        {
            // 아무 물품 줄도 고르지 않았으면 지금 이 자리에 체크한 물품의 영향 구역을 보여 준다.
            itemId ??= SelectedSpot != null ? Plan.ItemAt(SelectedSpot) : null;
            var option = itemId == null ? null : Mission.Spot(SelectedSpot)?.options.FirstOrDefault(o => o.itemId == itemId);
            foreach (var pair in zoneReach)
                if (pair.Value) pair.Value.SetActive(option != null && option.solves.Contains(pair.Key));
        }

        /// <summary>테스트·확인용: 지금 지도에서 '닿는 곳' 테두리가 켜진 구역.</summary>
        public IEnumerable<string> ReachShown => zoneReach.Where(p => p.Value && p.Value.activeSelf).Select(p => p.Key);

        public AssignOutcome ToggleItem(string itemId)
        {
            if (State != FieldState.Notebook || SelectedSpot == null) return AssignOutcome.UnknownSpot;
            if (notebookBusy || recommendPanel) return AssignOutcome.UnknownSpot;
            string before = Plan.ItemAt(SelectedSpot);
            var outcome = Plan.Toggle(Spec, SelectedSpot, itemId);
            if (outcome == AssignOutcome.Assigned || outcome == AssignOutcome.Replaced || outcome == AssignOutcome.Cleared) MarkSaveDirty();
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
                    text = "승인할 수 있어요";
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

    public partial class FieldGame
    {
        Button recommendButton;
        RectTransform recommendPanel;
        FieldPlan recommended;

        /// <summary>AI 단서 또는 추천안 창이 열려 있는지(검증용).</summary>
        public bool RecommendOpen => recommendPanel;
        /// <summary>지금 보여 주는 추천안(검증용, 단서 단계거나 닫혀 있으면 null).</summary>
        public FieldPlan RecommendedPlan => recommendPanel && recommendShown ? recommended : null;
        /// <summary>마지막으로 보여 준 AI 단서 문장(검증용).</summary>
        public string HintText { get; private set; }
        bool recommendShown;

        static string Need(FieldProblemKind k) => k switch
        {
            FieldProblemKind.Dark => "조명",
            FieldProblemKind.Heat => "더위",
            FieldProblemKind.Thirst => "마실 물",
            FieldProblemKind.Power => "전력",
            _ => "침수"
        };

        /// <summary>단서 문장. 관찰한 문제·지금 계획에 놓은 자리만 이름으로 말한다.</summary>
        string HintSentence(FieldHint h)
        {
            var p = h.ProblemId != null ? Mission.Problem(h.ProblemId) : null;
            var spot = h.SpotId != null ? Mission.Spot(h.SpotId) : null;
            switch (h.Kind)
            {
                case FieldHintKind.NeedObservation: return $"아직 살펴볼 곳이 {h.Amount}곳 남았어요. 현장을 먼저 확인해요.";
                case FieldHintKind.MissingRequired: return $"관찰한 곳 중 '{p.displayName}'에 {Need(p.kind)} 대비가 빠져 있어요.";
                case FieldHintKind.Overlap: return $"'{p.displayName}'에 {Need(p.kind)} 대비 효과가 겹치고 있어요. 한쪽 예산을 다른 곳에 쓸 수 있어요.";
                case FieldHintKind.NoEffect: return $"'{spot.displayName}'에 고른 물품은 관찰한 곳 중 닿는 곳이 없어요.";
                case FieldHintKind.OverBudget: return $"예산을 {h.Amount} 넘었어요. 꼭 필요한 대비부터 남겨 봐요.";
                case FieldHintKind.MissingBonus: return $"관찰한 곳의 필수 대비가 갖춰졌어요. 추가 목표 '{p.displayName}'의 {Need(p.kind)} 대비도 예산 안에서 가능한지 살펴봐요.";
                default: return "관찰한 곳의 필수 대비가 갖춰졌어요.";
            }
        }

        RectTransform NewHelpPanel()
        {
            if (recommendPanel) Destroy(recommendPanel.gameObject);
            var panel = UIKit.Panel(bookParts.right, "RecommendPanel", Theme.WithAlpha(Theme.Paper, 0.97f), null, true).rectTransform;
            panel.Region(0.03f, 0.2f, 0.97f, 0.83f);
            panel.gameObject.AddComponent<Outline>().effectColor = Theme.WithAlpha(Theme.Teal, 0.9f);
            FieldArt.ForecastIcon(panel, 54).Place(new Vector2(0, 1), new Vector2(46, -42), new Vector2(54, 54));
            return panel;
        }

        /// <summary>
        /// 첫 번째 도움 'AI 단서': 지금 계획과 관찰한 문제를 비교한 한 가지(FieldRules.Hint). 계획은 바꾸지 않는다.
        /// 더 원하면 '배치안까지 보기'를 눌러야 전체 추천안이 나온다.
        /// </summary>
        public void OpenHint()
        {
            if (State != FieldState.Notebook || notebookBusy || confirmLocked || recommendPanel || MissionIndex == 0) return;
            if (FieldRules.MissingObservations(Spec, observed) > 0) return;
            sound.Play(Sfx.Select);
            recommendShown = false;
            recommendPanel = NewHelpPanel();
            HintText = HintSentence(FieldRules.Hint(Spec, Plan, observed));
            UIKit.Text(recommendPanel, "Title", "AI 단서 <size=20>(관찰한 곳 · 지금 계획 기준)</size>", 30, Theme.Navy, TextAlignmentOptions.MidlineLeft, true)
                .rectTransform.Region(0.14f, 0.86f, 0.98f, 0.98f);
            var body = UIKit.Text(recommendPanel, "Body", HintText, 30, Theme.Navy, TextAlignmentOptions.MidlineLeft);
            body.rectTransform.Region(0.07f, 0.3f, 0.95f, 0.8f);
            body.enableAutoSizing = true;
            body.fontSizeMin = 18;
            body.fontSizeMax = 30;
            var bar = UIKit.Rect(recommendPanel, "Buttons").Region(0.05f, 0.03f, 0.95f, 0.17f);
            UIKit.HorizontalList(bar, 16);
            var close = UIKit.Button(bar, "Cancel", "닫기", CloseRecommend, ButtonStyle.Primary, 24);
            var more = UIKit.Button(bar, "ShowPlan", "배치안까지 보기", ShowRecommendation, ButtonStyle.Secondary, 22);
            UIKit.ChainNavigation(new Selectable[] { close, more }, false);
            Select(close);
        }

        /// <summary>단서 창에서 명시적으로 고른 경우에만: 관찰한 곳 기준 전체 추천안과 지금 계획의 비교. 적용은 따로 눌러야 한다.</summary>
        public void ShowRecommendation()
        {
            if (!recommendPanel || recommendShown || State != FieldState.Notebook || confirmLocked) return;
            recommended = FieldRules.Recommend(Spec, observed);
            if (recommended == null) return;
            sound.Play(Sfx.Select);
            recommendShown = true;
            recommendPanel = NewHelpPanel();
            var eval = FieldRules.Evaluate(Spec, recommended);
            var now = FieldRules.Evaluate(Spec, Plan);
            string Seen(FieldEvaluation e)
            {
                var names = Mission.problems.Where(p => observed.Contains(p.id) && e.IsSolved(p.id)).Select(p => p.displayName).ToList();
                return names.Count > 0 ? string.Join(" · ", names) : "없음";
            }
            UIKit.Text(recommendPanel, "Title", "가상 실증 배치안 <size=20>(관찰한 곳 기준)</size>", 30, Theme.Navy, TextAlignmentOptions.MidlineLeft, true)
                .rectTransform.Region(0.14f, 0.86f, 0.98f, 0.98f);
            var lines = Mission.spots.Select(sp =>
            {
                var item = recommended.ItemAt(sp.id);
                return $"<b>{sp.displayName}</b>  {(item != null ? Mission.Item(item).displayName : "<color=#8A90A6>비워 둠</color>")}";
            });
            var body = UIKit.Text(recommendPanel, "Body",
                string.Join("\n", lines) +
                $"\n\n<b>배치안</b> 예산 {eval.Cost} / {Mission.budget} · 대비 {Seen(eval)}" +
                $"\n<b>지금 계획</b> 예산 {now.Cost} / {Mission.budget} · 대비 {Seen(now)}\n" +
                "<size=19><color=#55607E>관찰한 필수 문제 → 관찰한 추가 목표 → 낮은 비용 순으로 고른 비교안이에요. 적용해도 직접 고칠 수 있고, 승인은 따로 해요.</color></size>",
                24, Theme.Navy, TextAlignmentOptions.TopLeft);
            body.rectTransform.Region(0.05f, 0.2f, 0.97f, 0.84f);
            body.enableAutoSizing = true;
            body.fontSizeMin = 15;
            body.fontSizeMax = 24;
            var bar = UIKit.Rect(recommendPanel, "Buttons").Region(0.05f, 0.03f, 0.95f, 0.17f);
            UIKit.HorizontalList(bar, 16);
            var cancel = UIKit.Button(bar, "Cancel", "취소", CloseRecommend, ButtonStyle.Secondary, 24);
            var apply = UIKit.Button(bar, "Apply", "이 계획 적용", ApplyRecommend, ButtonStyle.Primary, 24);
            UIKit.ChainNavigation(new Selectable[] { cancel, apply }, false);
            // 지도 위 반투명 표시: 추천 물품을 설치 자리에 흐리게 겹쳐 그린다.
            foreach (var sp in Mission.spots)
            {
                var item = recommended.ItemAt(sp.id);
                if (item == null) continue;
                var ghost = UIKit.Rect(mapArea, "Ghost_" + sp.id);
                ghost.anchorMin = ghost.anchorMax = MapPoint(sp.position) + new Vector2(0, 0.09f);
                ghost.sizeDelta = new Vector2(60, 60);
                var group = ghost.gameObject.AddComponent<CanvasGroup>();
                group.alpha = 0.55f;
                group.blocksRaycasts = false;
                UIKit.Shape(ghost, "Ring", ShapeSprites.Circle, Theme.WithAlpha(Theme.Teal, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60, 60)).raycastTarget = false;
                FieldArt.ItemIcon(ghost, Mission.Item(item).visual, 46).Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(46, 46));
            }
            SelectNextFrame(() => cancel);
        }

        public void CloseRecommend()
        {
            if (!recommendPanel) return;
            Destroy(recommendPanel.gameObject);
            recommendPanel = null;
            recommendShown = false;
            if (mapArea)
                for (int i = mapArea.childCount - 1; i >= 0; i--)
                    if (mapArea.GetChild(i).name.StartsWith("Ghost_")) Destroy(mapArea.GetChild(i).gameObject);
            inputLockFrame = Time.frameCount;
            Select(recommendButton ? recommendButton : SpotButton(SelectedSpot));
        }

        /// <summary>추천안을 편집 중 계획으로 옮긴다(확정은 하지 않는다).</summary>
        public void ApplyRecommend()
        {
            if (!recommendPanel || !recommendShown || recommended == null || State != FieldState.Notebook || confirmLocked) return;
            var chosen = recommended;
            Plan = new FieldPlan();
            foreach (var pair in chosen.Assignments) Plan.Toggle(Spec, pair.Key, pair.Value);
            MarkSaveDirty();
            sound.Play(Sfx.Check);
            CloseRecommend();
            RefreshNotebook();
        }
    }

    /// <summary>물품 줄을 키보드로 고르거나 마우스를 올렸을 때 지도에 예상 영향 구역을 켠다.</summary>
    public class ReachHover : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public Action<bool> Changed;
        public void OnSelect(BaseEventData e) => Changed?.Invoke(true);
        public void OnDeselect(BaseEventData e) => Changed?.Invoke(false);
        public void OnPointerEnter(PointerEventData e) => Changed?.Invoke(true);
        public void OnPointerExit(PointerEventData e) => Changed?.Invoke(false);
    }
}
