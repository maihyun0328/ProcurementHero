using System.Collections;
using System.Collections.Generic;
using ProcurementHero.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProcurementHero
{
    public partial class GameManager
    {
        class Card
        {
            public Button button;
            public Image background;
            public TextMeshProUGUI status;
        }

        int missionIndex;
        MissionData mission;
        readonly int[] selected = { -1, -1 };
        readonly Card[][] cards = new Card[2][];
        bool committed;

        TextMeshProUGUI topCostText, costText, scoreText, forecastText, reasonText;
        readonly TextMeshProUGUI[] basketTexts = new TextMeshProUGUI[2];
        Button purchaseButton;

        // 튜토리얼과 짧은 안내
        bool tutorialDone;
        int tutorialStep = -1;
        RectTransform tutorialBanner, tutorialFrame;
        TextMeshProUGUI tutorialText;
        Button tutorialNext;
        RectTransform viewRegion, compareRegion, summaryRegion, purchaseRegion;
        readonly HashSet<string> shownTips = new HashSet<string>();

        public int MissionIndex => missionIndex;
        public bool IsPurchaseCommitted => committed;
        public Button PurchaseButton => purchaseButton;

        public void StartMission(int index)
        {
            BeginScreen(GameScreen.Mission);
            missionIndex = index;
            mission = database.missions[index];
            selected[0] = selected[1] = -1;
            committed = false;

            var root = UIKit.Rect(screenRoot, "MissionScreen").Fill();
            BuildMissionTopBar(root);

            // 마을과 현재 시설
            viewRegion = UIKit.Panel(root, "VillagePanel", Theme.Paper).rectTransform;
            viewRegion.Region(0, 0.2f, 0.4f, 0.9f, 14);
            var viewHolder = UIKit.Rect(viewRegion, "View").Fill(10, 10, 10, 10);
            var view = FacilityView.Create(viewHolder, mission.facility);
            view.SetState(0, 0, mission.request);

            // 제품 비교
            compareRegion = UIKit.Rect(root, "Compare");
            compareRegion.Region(0.4f, 0.2f, 1f, 0.9f, 14);
            for (int slot = 0; slot < 2; slot++)
                BuildColumn(compareRegion, slot);

            BuildBasket(root);
            RefreshMission();
            Select(cards[0][0].button);

            if (index == 0 && !tutorialDone) StartTutorial(root);
        }

        void BuildMissionTopBar(RectTransform root)
        {
            var bar = UIKit.Panel(root, "TopBar", Theme.Navy, ShapeSprites.Square).rectTransform;
            bar.Region(0, 0.9f, 1, 1);
            var title = UIKit.Text(bar, "Mission", $"미션 {missionIndex + 1} · {mission.title}", 34, Color.white,
                TextAlignmentOptions.MidlineLeft, true);
            title.rectTransform.Region(0, 0, 0.36f, 1).offsetMin = new Vector2(36, 0);

            var stats = UIKit.Rect(bar, "Stats").Region(0.36f, 0.12f, 0.86f, 0.88f);
            UIKit.HorizontalList(stats, 14);
            StatChip(stats, $"예산 {mission.budget}");
            topCostText = StatChip(stats, "현재 총비용 0");
            StatChip(stats, $"납기 {mission.deadlineDays}일 이내");
            StatChip(stats, $"목표 성능 {mission.targetPerformance}");

            var pause = UIKit.Button(bar, "Pause", "일시정지 (Esc)", OpenPause, ButtonStyle.Ghost, 24);
            pause.GetComponent<RectTransform>().Region(0.87f, 0.15f, 1f, 0.85f).offsetMax = new Vector2(-24, 0);
        }

        TextMeshProUGUI StatChip(RectTransform parent, string text)
        {
            var chip = UIKit.Panel(parent, "Chip", Theme.WithAlpha(Color.white, 0.12f));
            var t = UIKit.Text(chip.transform, "Text", text, 26, Color.white, TextAlignmentOptions.Center, true);
            t.rectTransform.Fill(8, 8, 0, 0);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.enableAutoSizing = true;
            t.fontSizeMin = 16;
            t.fontSizeMax = 26;
            return t;
        }

        void BuildColumn(RectTransform parent, int slot)
        {
            var column = UIKit.Rect(parent, "Column" + (slot == 0 ? "A" : "B"));
            column.Region(slot * 0.5f, 0, slot * 0.5f + 0.5f, 1);
            column.offsetMin = new Vector2(slot == 0 ? 0 : 8, 0);
            column.offsetMax = new Vector2(slot == 0 ? -8 : 0, 0);

            var head = UIKit.Panel(column, "Head", Theme.TealDark).rectTransform;
            head.anchorMin = new Vector2(0, 1);
            head.anchorMax = new Vector2(1, 1);
            head.pivot = new Vector2(0.5f, 1);
            head.sizeDelta = new Vector2(0, 64);
            head.anchoredPosition = Vector2.zero;
            var headText = UIKit.Text(head, "Text", $"물품 {(slot == 0 ? "A" : "B")} · {mission.ItemName(slot)}", 28, Color.white,
                TextAlignmentOptions.Center, true);
            headText.rectTransform.Fill(10, 10, 0, 0);

            var list = UIKit.Rect(column, "Cards").Fill(0, 0, 76, 0);
            UIKit.VerticalList(list, 12).childForceExpandHeight = true;

            var products = mission.Products(slot);
            cards[slot] = new Card[products.Count];
            for (int i = 0; i < products.Count; i++)
                cards[slot][i] = BuildCard(list, slot, i, products[i]);
        }

        Card BuildCard(RectTransform parent, int slot, int index, ProductEntry p)
        {
            var button = UIKit.Button(parent, "Card_" + p.id, "", () => ToggleProduct(slot, index), ButtonStyle.Secondary);
            Destroy(button.transform.Find("Label").gameObject);
            var bg = (Image)button.targetGraphic;
            var rt = (RectTransform)button.transform;

            var badge = UIKit.Panel(rt, "Type", TypeColor(p.type)).rectTransform;
            badge.Place(new Vector2(0, 1), new Vector2(18, -14), new Vector2(118, 38), new Vector2(0, 1));
            UIKit.Text(badge, "Text", TypeName(p.type), 22, Color.white, TextAlignmentOptions.Center, true).rectTransform.Fill();

            var name = UIKit.Text(rt, "Name", p.displayName, 30, Theme.Navy, TextAlignmentOptions.MidlineLeft, true);
            name.rectTransform.anchorMin = new Vector2(0, 1);
            name.rectTransform.anchorMax = new Vector2(1, 1);
            name.rectTransform.pivot = new Vector2(0, 1);
            name.rectTransform.offsetMin = new Vector2(150, -52);
            name.rectTransform.offsetMax = new Vector2(-150, -10);

            var status = UIKit.Text(rt, "Status", "", 22, Theme.TealDark, TextAlignmentOptions.MidlineRight, true);
            status.rectTransform.anchorMin = new Vector2(0.6f, 1);
            status.rectTransform.anchorMax = new Vector2(1, 1);
            status.rectTransform.offsetMin = new Vector2(0, -52);
            status.rectTransform.offsetMax = new Vector2(-18, -10);

            bool shortPerf = p.performance < mission.targetPerformance;
            bool late = p.deliveryDays > mission.deadlineDays;
            string perf = shortPerf ? $"<color=#{ColorUtility.ToHtmlStringRGB(Theme.Warning)}>성능 {p.performance} △</color>" : $"성능 <b>{p.performance}</b>";
            string ship = late ? $"<color=#{ColorUtility.ToHtmlStringRGB(Theme.Warning)}>배송 {p.deliveryDays}일 △</color>" : $"배송 <b>{p.deliveryDays}일</b>";
            string body =
                $"구매비 <b>{p.purchaseCost}</b>   유지비 <b>{p.maintenanceCost}</b>   합계 <b>{p.TotalCost}</b>\n" +
                $"{perf}   {ship}   환경 <b>{p.ecoScore}</b>";
            var bodyText = UIKit.Text(rt, "Stats", body, 25, Theme.Navy);
            bodyText.rectTransform.Fill(20, 18, 62, 40);
            bodyText.lineSpacing = 8;

            var notes = new List<string>();
            if (shortPerf) notes.Add("목표 성능 미달");
            if (late) notes.Add("납기 지연");
            string summary = notes.Count > 0 ? "△ " + string.Join(" · ", notes) : p.summary;
            var foot = UIKit.Text(rt, "Summary", summary, 21, notes.Count > 0 ? Theme.Warning : Theme.NavySoft);
            foot.rectTransform.anchorMin = Vector2.zero;
            foot.rectTransform.anchorMax = new Vector2(1, 0);
            foot.rectTransform.pivot = new Vector2(0.5f, 0);
            foot.rectTransform.offsetMin = new Vector2(20, 8);
            foot.rectTransform.offsetMax = new Vector2(-18, 38);
            foot.textWrappingMode = TextWrappingModes.NoWrap;
            foot.overflowMode = TextOverflowModes.Ellipsis;

            return new Card { button = button, background = bg, status = status };
        }

        void BuildBasket(RectTransform root)
        {
            var bar = UIKit.Panel(root, "Basket", Theme.CreamDeep, ShapeSprites.Square).rectTransform;
            bar.Region(0, 0, 1, 0.2f);

            var slots = UIKit.Rect(bar, "Slots").Region(0, 0, 0.34f, 1, 16);
            UIKit.VerticalList(slots, 10).childForceExpandHeight = true;
            for (int s = 0; s < 2; s++)
            {
                var slot = UIKit.Panel(slots, "Slot" + s, Theme.Paper);
                basketTexts[s] = UIKit.Text(slot.transform, "Text", "", 25, Theme.Navy, TextAlignmentOptions.MidlineLeft);
                basketTexts[s].rectTransform.Fill(18, 12, 0, 0);
            }

            summaryRegion = UIKit.Panel(bar, "Summary", Theme.Paper).rectTransform;
            summaryRegion.Region(0.34f, 0, 0.74f, 1, 16);
            var lines = UIKit.Rect(summaryRegion, "Lines").Fill(20, 16, 10, 10);
            UIKit.VerticalList(lines, 2, null, TextAnchor.MiddleLeft);
            costText = UIKit.Text(lines, "Cost", "", 26, Theme.Navy, TextAlignmentOptions.MidlineLeft, true);
            scoreText = UIKit.Text(lines, "Score", "", 24, Theme.Navy, TextAlignmentOptions.MidlineLeft);
            forecastText = UIKit.Text(lines, "Forecast", "", 23, Theme.Navy, TextAlignmentOptions.MidlineLeft);

            purchaseRegion = UIKit.Rect(bar, "PurchaseArea").Region(0.74f, 0, 1, 1, 16);
            purchaseButton = UIKit.Button(purchaseRegion, "Purchase", "구매 확정", OnPurchaseClicked, ButtonStyle.Primary, 36);
            ((RectTransform)purchaseButton.transform).Region(0, 0.4f, 1, 1);
            reasonText = UIKit.Text(purchaseRegion, "Reason", "", 21, Theme.Danger, TextAlignmentOptions.Center, true);
            reasonText.rectTransform.Region(0, 0, 1, 0.38f);
        }

        void ToggleProduct(int slot, int index)
        {
            if (committed) return;
            selected[slot] = selected[slot] == index ? -1 : index;
            sound.Play(Sfx.Select);

            if (selected[slot] >= 0)
            {
                var p = mission.Products(slot)[index];
                Tip("compare", "가격뿐 아니라 성능, 유지비, 필요한 시점을 함께 살펴봐요.");
                if (p.deliveryDays > mission.deadlineDays)
                    Tip("late", "필요한 때 도착해야 물품의 가치가 현장에서 이어져요.");
                else if (p.type == ProductType.Innovative)
                    Tip("innovative", "새로운 제품을 현장에서 사용하고 성능을 확인하는 과정도 중요해요.");
            }

            RefreshMission();
            if (tutorialStep == 2 && selected[0] >= 0 && selected[1] >= 0) SetTutorialStep(3);
        }

        /// <summary>선택 중인 제품. 없으면 null.</summary>
        ProductSpec SelectedSpec(int slot) => selected[slot] < 0 ? null : mission.Products(slot)[selected[slot]].ToSpec();

        /// <summary>현재 선택으로 화면 전체를 다시 계산한다. 누적 값 없이 매번 처음부터 계산한다.</summary>
        void RefreshMission()
        {
            var spec = mission.ToSpec();
            var a = SelectedSpec(0);
            var b = SelectedSpec(1);

            for (int slot = 0; slot < 2; slot++)
            {
                for (int i = 0; i < cards[slot].Length; i++)
                {
                    bool on = selected[slot] == i;
                    cards[slot][i].background.color = on ? Theme.TealPale : Theme.Paper;
                    cards[slot][i].status.text = on ? "○ 선택됨" : "";
                }
                var p = slot == 0 ? a : b;
                basketTexts[slot].text = p == null
                    ? $"<b>{mission.ItemName(slot)}</b>   <color=#{ColorUtility.ToHtmlStringRGB(Theme.NavySoft)}>아직 고르지 않았어요</color>"
                    : $"<b>{mission.ItemName(slot)}</b>   {p.Name} ({TypeName(p.Type)}) · 합계 {p.TotalCost}";
            }

            int purchase = (a?.PurchaseCost ?? 0) + (b?.PurchaseCost ?? 0);
            int maintenance = (a?.MaintenanceCost ?? 0) + (b?.MaintenanceCost ?? 0);
            int total = Scoring.TotalCost(a, b);
            bool over = total > spec.Budget;
            costText.text = $"총비용 {total} = 구매비 {purchase} + 유지비 {maintenance}   /   예산 {spec.Budget}" +
                            (over ? "  × 예산 초과" : $"  (남는 예산 {spec.Budget - total})");
            costText.color = over ? Theme.Danger : Theme.Navy;
            topCostText.text = $"현재 총비용 {total}";
            topCostText.color = over ? Theme.Hex(0xFFB3AE) : Color.white;

            if (a != null && b != null)
            {
                var e = Scoring.Evaluate(spec, a, b);
                scoreText.text = $"예상 점수 <b>{F(e.TotalScore)}</b>점  (성과 {F(e.PerformanceScore)} · 환경 {F(e.EcoScore)} · 예산 {F(e.BudgetScore)})";
                var warnings = new List<string>();
                if (!e.MeetsPerformanceA) warnings.Add($"{mission.itemNameA} 성능 부족");
                if (!e.MeetsPerformanceB) warnings.Add($"{mission.itemNameB} 성능 부족");
                if (!e.OnTimeA) warnings.Add($"{mission.itemNameA} 납기 지연");
                if (!e.OnTimeB) warnings.Add($"{mission.itemNameB} 납기 지연");
                if (e.Success)
                {
                    forecastText.text = "예상 결과: ○ 요청을 모두 해결할 수 있어요";
                    forecastText.color = Theme.Success;
                }
                else if (over)
                {
                    forecastText.text = "예상 결과: × 예산을 넘어 구매할 수 없어요";
                    forecastText.color = Theme.Danger;
                }
                else
                {
                    forecastText.text = "예상 결과: △ 부분 해결 — " + string.Join(", ", warnings);
                    forecastText.color = Theme.Warning;
                }
            }
            else
            {
                scoreText.text = "두 물품을 모두 고르면 예상 점수가 표시돼요.";
                forecastText.text = "";
            }

            var reason = Scoring.GetBlockReason(spec, a, b);
            purchaseButton.interactable = reason == PurchaseBlockReason.None && !committed;
            reasonText.color = Theme.Danger;
            reasonText.text = reason switch
            {
                PurchaseBlockReason.MissingBoth => "× 두 물품을 각각 하나씩 골라 주세요",
                PurchaseBlockReason.MissingA => $"× {mission.itemNameA}을(를) 골라 주세요",
                PurchaseBlockReason.MissingB => $"× {mission.itemNameB}을(를) 골라 주세요",
                PurchaseBlockReason.OverBudget => $"× 예산을 {total - spec.Budget} 넘어 구매할 수 없어요",
                _ => ""
            };
            if (reason == PurchaseBlockReason.None)
            {
                reasonText.color = Theme.TealDark;
                reasonText.text = "구매할 수 있어요 (확정 전에는 예산이 차감되지 않아요)";
            }
        }

        void OnPurchaseClicked()
        {
            if (committed) return;
            var spec = mission.ToSpec();
            var a = SelectedSpec(0);
            var b = SelectedSpec(1);
            if (Scoring.GetBlockReason(spec, a, b) != PurchaseBlockReason.None) return;

            var e = Scoring.Evaluate(spec, a, b);
            if (e.Success)
            {
                CommitPurchase();
                return;
            }

            var lines = new List<string>();
            if (!e.MeetsPerformanceA) lines.Add($"· {mission.itemNameA}: 성능 {a.Performance} < 목표 {spec.TargetPerformance}");
            if (!e.MeetsPerformanceB) lines.Add($"· {mission.itemNameB}: 성능 {b.Performance} < 목표 {spec.TargetPerformance}");
            if (!e.OnTimeA) lines.Add($"· {mission.itemNameA}: 배송 {a.DeliveryDays}일 > 납기 {spec.DeadlineDays}일");
            if (!e.OnTimeB) lines.Add($"· {mission.itemNameB}: 배송 {b.DeliveryDays}일 > 납기 {spec.DeadlineDays}일");
            Confirm("이대로 구매할까요?",
                "이 조합은 요청을 일부만 해결해요.\n" + string.Join("\n", lines) + "\n\n결과를 확인한 뒤 재도전할 수도 있어요.",
                "그래도 구매", CommitPurchase, "다시 고르기");
        }

        /// <summary>결과 스냅샷을 한 번만 만든다. 연타나 중복 호출은 무시된다.</summary>
        public void CommitPurchase()
        {
            if (committed || CurrentScreen != GameScreen.Mission) return;
            var spec = mission.ToSpec();
            var a = SelectedSpec(0);
            var b = SelectedSpec(1);
            if (Scoring.GetBlockReason(spec, a, b) != PurchaseBlockReason.None) return;

            committed = true;
            purchaseButton.interactable = false;
            var result = Scoring.Evaluate(spec, a, b);
            Progress.SetResult(missionIndex, result);
            sound.Play(Sfx.Purchase);
            if (tutorialStep >= 0) tutorialDone = true;
            ShowResult(missionIndex, result);
        }

        /// <summary>테스트와 자동 검증용: 제품 번호로 선택한다.</summary>
        public void SelectForTest(int slot, int index)
        {
            if (selected[slot] != index) ToggleProduct(slot, index);
        }

        // ───────── 튜토리얼 ─────────

        static readonly string[] TutorialLines =
        {
            "① 요청 확인 — 주민의 요청과 위쪽의 예산 · 납기 · 목표 성능을 확인해요.",
            "② 제안 비교 — 카드마다 구매비 · 유지비 · 성능 · 배송일 · 환경점수가 적혀 있어요.",
            "③ 두 물품 선택 — 물품 A와 물품 B에서 하나씩 카드를 눌러 골라 보세요.",
            "④ 비용 확인 — 총비용은 구매비와 유지비를 더한 값이에요. 예산 안인지 확인해요.",
            "⑤ 구매 — 준비가 되면 구매 확정을 눌러요. 튜토리얼은 점수에 영향을 주지 않아요."
        };

        void StartTutorial(RectTransform root)
        {
            tutorialFrame = UIKit.Rect(root, "TutorialFrame");
            foreach (var edge in new[] { (0f, 0f, 1f, 0f), (0f, 1f, 1f, 1f), (0f, 0f, 0f, 1f), (1f, 0f, 1f, 1f) })
            {
                var img = UIKit.Panel(tutorialFrame, "Edge", Theme.Warning, ShapeSprites.Square).rectTransform;
                img.anchorMin = new Vector2(edge.Item1, edge.Item2);
                img.anchorMax = new Vector2(edge.Item3, edge.Item4);
                img.sizeDelta = new Vector2(edge.Item1 == edge.Item3 ? 6 : 12, edge.Item2 == edge.Item4 ? 6 : 12);
                img.anchoredPosition = Vector2.zero;
            }

            var banner = UIKit.Panel(root, "TutorialBanner", Theme.Navy, null, true);
            tutorialBanner = banner.rectTransform;
            tutorialBanner.Region(0.01f, 0.215f, 0.39f, 0.43f);
            tutorialText = UIKit.Text(tutorialBanner, "Text", "", 25, Color.white);
            tutorialText.rectTransform.Fill(22, 22, 16, 70);
            tutorialNext = UIKit.Button(tutorialBanner, "Next", "다음", () => SetTutorialStep(tutorialStep + 1), ButtonStyle.Primary, 24);
            ((RectTransform)tutorialNext.transform).Place(new Vector2(1, 0), new Vector2(-18, 14), new Vector2(150, 48), new Vector2(1, 0));
            var skip = UIKit.Button(tutorialBanner, "Skip", "튜토리얼 건너뛰기", EndTutorial, ButtonStyle.Ghost, 22);
            ((RectTransform)skip.transform).Place(new Vector2(0, 0), new Vector2(18, 14), new Vector2(240, 48), new Vector2(0, 0));

            SetTutorialStep(0);
            StartCoroutine(PulseFrame());
        }

        void SetTutorialStep(int step)
        {
            if (step >= TutorialLines.Length) { EndTutorial(); return; }
            tutorialStep = step;
            tutorialText.text = TutorialLines[step];
            bool showNext = step == 0 || step == 1 || step == 3;
            if (!showNext && tutorialNext.gameObject.activeSelf) KeepKeyboardFocus(tutorialNext.gameObject);
            tutorialNext.gameObject.SetActive(showNext);
            // 이미 두 물품을 골랐다면 3단계는 바로 넘긴다.
            if (step == 2 && selected[0] >= 0 && selected[1] >= 0) { SetTutorialStep(3); return; }

            var target = step == 0 ? viewRegion : step <= 2 ? compareRegion : step == 3 ? summaryRegion : purchaseRegion;
            tutorialFrame.SetParent(target, false);
            tutorialFrame.Fill(-6, -6, -6, -6);
            tutorialFrame.SetAsLastSibling();
        }

        void EndTutorial()
        {
            tutorialDone = true;
            tutorialStep = -1;
            if (tutorialBanner)
            {
                KeepKeyboardFocus(tutorialBanner.gameObject);
                // Destroy는 프레임 끝에 일어나므로 먼저 꺼서 선택 대상에서 바로 빠지게 한다.
                tutorialBanner.gameObject.SetActive(false);
                Destroy(tutorialBanner.gameObject);
            }
            if (tutorialFrame) Destroy(tutorialFrame.gameObject);
        }

        /// <summary>
        /// 숨기거나 지울 UI(leaving)에 키보드 선택이 있으면 다음 프레임에 쓸 수 있는 UI로 옮긴다.
        /// 한 프레임 늦추는 이유: 같은 Enter 입력이 새로 선택된 버튼까지 누르지 않게 하기 위해서다.
        /// 그 사이 사용자가 다른 활성 UI를 골랐다면 선택을 빼앗지 않는다.
        /// </summary>
        void KeepKeyboardFocus(GameObject leaving)
        {
            var es = EventSystem.current;
            if (!es) return;
            var current = es.currentSelectedGameObject;
            if (current && !current.transform.IsChildOf(leaving.transform)) return;
            StartCoroutine(RestoreFocusNextFrame(leaving));
        }

        IEnumerator RestoreFocusNextFrame(GameObject leaving)
        {
            yield return null;
            var es = EventSystem.current;
            if (!es || CurrentScreen != GameScreen.Mission || committed) yield break;
            var current = es.currentSelectedGameObject;
            bool lost = !current || !current.activeInHierarchy || (leaving && current.transform.IsChildOf(leaving.transform)) ||
                        (current.TryGetComponent(out Selectable s) && !s.IsInteractable());
            if (lost) Select(FocusFallback());
        }

        /// <summary>지금 단계에서 키보드로 이어서 조작할 UI. 구매할 수 있으면 구매 버튼, 아니면 아직 고르지 않은 물품의 카드.</summary>
        Selectable FocusFallback()
        {
            bool buyStep = tutorialStep == 4 || tutorialStep < 0;
            if (buyStep && purchaseButton && purchaseButton.IsInteractable()) return purchaseButton;
            int slot = selected[0] < 0 ? 0 : selected[1] < 0 ? 1 : 0;
            return cards[slot][0].button;
        }

        IEnumerator PulseFrame()
        {
            while (tutorialFrame)
            {
                float a = 0.55f + Mathf.Sin(Time.unscaledTime * 5f) * 0.45f;
                foreach (Transform edge in tutorialFrame)
                    edge.GetComponent<Image>().color = Theme.WithAlpha(Theme.Warning, a);
                yield return null;
            }
        }

        // ───────── 짧은 안내 ─────────

        void Tip(string key, string text)
        {
            if (!shownTips.Add(key)) return;
            StartCoroutine(ToastRoutine(text));
        }

        IEnumerator ToastRoutine(string text)
        {
            UIKit.Clear(toastRoot);
            var panel = UIKit.Panel(toastRoot, "Toast", Theme.Teal);
            // 제품 카드를 가리지 않도록 시설 그림 아래쪽(튜토리얼 중에는 그 위)에 띄운다.
            if (tutorialBanner) panel.rectTransform.Region(0.01f, 0.44f, 0.39f, 0.53f);
            else panel.rectTransform.Region(0.01f, 0.215f, 0.39f, 0.30f);
            var group = panel.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            var t = UIKit.Text(panel.transform, "Text", "TIP  " + text, 25, Color.white, TextAlignmentOptions.Center, true);
            t.rectTransform.Fill(16, 16, 4, 4);
            t.enableAutoSizing = true;
            t.fontSizeMin = 16;
            t.fontSizeMax = 25;

            float time = 0;
            while (time < 4f && panel)
            {
                time += Time.unscaledDeltaTime;
                group.alpha = time < 0.25f ? time / 0.25f : time > 3.4f ? (4f - time) / 0.6f : 1f;
                yield return null;
            }
            if (panel) Destroy(panel.gameObject);
        }
    }
}
