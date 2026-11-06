using ProcurementHero.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProcurementHero
{
    public partial class GameManager
    {
        FacilityView resultView;
        RectTransform resultPanel;
        Button skipButton;
        bool resultRevealed;
        RectTransform resultHeader;

        public bool ResultRevealed => resultRevealed;

        string ReactionFor(MissionData m, PurchaseEvaluation e)
        {
            if (e.Success) return m.reactionSuccess;
            if (e.OutcomeA == ItemOutcome.Full || e.OutcomeB == ItemOutcome.Full) return m.reactionPartial;
            return m.reactionFail;
        }

        void ShowResult(int index, PurchaseEvaluation e)
        {
            BeginScreen(GameScreen.Result);
            var m = database.missions[index];
            resultRevealed = false;

            var root = UIKit.Rect(screenRoot, "ResultScreen").Fill();
            resultHeader = Header(root, $"미션 {index + 1} 결과 · {m.title}", "배송 차량이 출발했어요");

            var left = UIKit.Panel(root, "VillagePanel", Theme.Paper).rectTransform;
            left.Region(0, 0.12f, 0.55f, 0.9f, 14);
            var holder = UIKit.Rect(left, "View").Fill(10, 10, 10, 10);
            resultView = FacilityView.Create(holder, m.facility);
            resultView.SetState(0, 0);

            skipButton = UIKit.Button(left, "Skip", "연출 건너뛰기 (Space)", SkipResultAnimation, ButtonStyle.Ghost, 24);
            ((RectTransform)skipButton.transform).Place(new Vector2(1, 0), new Vector2(-24, 24), new Vector2(320, 60), new Vector2(1, 0));

            resultPanel = UIKit.Panel(root, "ResultPanel", Theme.Paper).rectTransform;
            resultPanel.Region(0.55f, 0.12f, 1f, 0.9f, 14);
            var waiting = UIKit.Text(resultPanel, "Waiting", "배송 중…\n물품이 도착하면 결과를 알려 드릴게요.", 30, Theme.NavySoft, TextAlignmentOptions.Center);
            waiting.rectTransform.Fill(30, 30, 30, 30);

            var bar = UIKit.Rect(root, "Buttons").Region(0.2f, 0.015f, 0.8f, 0.105f);
            UIKit.HorizontalList(bar, 24);
            var retry = UIKit.Button(bar, "Retry", "이 미션 재도전", () => StartMission(index), ButtonStyle.Secondary);
            var map = UIKit.Button(bar, "Map", "마을 지도", ShowMap, ButtonStyle.Secondary);
            bool last = index >= database.missions.Count - 1;
            var next = UIKit.Button(bar, "Next", last ? "엔딩 보기" : "다음 미션", () =>
            {
                if (last) ShowEnding();
                else StartMission(index + 1);
            });
            UIKit.ChainNavigation(new Selectable[] { retry, map, next }, false);
            Select(skipButton);

            int levelA = FacilityView.LevelOf(e.OutcomeA);
            int levelB = FacilityView.LevelOf(e.OutcomeB);
            resultView.PlayDelivery(levelA, levelB, ReactionFor(m, e), s => sound.Play(s), () => RevealResult(index, e, next));
        }

        public void SkipResultAnimation()
        {
            if (resultView && resultView.IsPlaying) resultView.Skip();
        }

        void RevealResult(int index, PurchaseEvaluation e, Button next)
        {
            if (resultRevealed || !resultPanel) return;
            resultRevealed = true;
            var m = database.missions[index];
            sound.Play(e.Success ? Sfx.Success : Sfx.Partial);
            if (skipButton) skipButton.gameObject.SetActive(false);
            var headerRight = resultHeader ? resultHeader.Find("Right")?.GetComponent<TMP_Text>() : null;
            if (headerRight) headerRight.text = "배송 완료 · 결과가 확정됐어요";
            // 건너뛰기에 쓴 키 입력이 같은 프레임에 '다음' 버튼을 누르지 않도록 한 프레임 뒤에 선택한다.
            StartCoroutine(SelectNextFrame(next));

            UIKit.Clear(resultPanel);
            var list = UIKit.Rect(resultPanel, "List").Fill(32, 32, 26, 20);
            UIKit.VerticalList(list, 8);

            var badge = UIKit.Panel(list, "Badge", e.Success ? Theme.SuccessPale : Theme.WarningPale);
            UIKit.Layout(badge, 70);
            var badgeText = UIKit.Text(badge.transform, "Text", e.Success ? "○ 성공 · 요청을 해결했어요" : "△ 부분 해결 · 일부만 개선됐어요",
                34, e.Success ? Theme.Success : Theme.Warning, TextAlignmentOptions.Center, true);
            badgeText.rectTransform.Fill(10, 10, 0, 0);

            var report = Feedback.Build(e, m.itemNameA, m.itemNameB);
            Line(list, report.Headline, 26, Theme.Navy, true);
            Line(list, $"총점 <b>{F(e.TotalScore)}</b>점   <size=22>성과 {F(e.PerformanceScore)}/60 · 환경 {F(e.EcoScore)}/15 · 예산 {F(e.BudgetScore)}/25</size>", 30, Theme.TealDark);
            Line(list, $"총비용 {e.TotalCost} = 구매비 {e.PurchaseCostSum} + 유지비 {e.MaintenanceCostSum}   (예산 {m.budget})", 23, Theme.Navy);
            Line(list, $"{m.itemNameA}: {e.ProductA.Name} → {OutcomeText(e.OutcomeA)}   |   {m.itemNameB}: {e.ProductB.Name} → {OutcomeText(e.OutcomeB)}", 22, Theme.NavySoft);

            Line(list, "주민 반응", 24, Theme.Navy, true);
            Line(list, "“" + ReactionFor(m, e) + "”", 23, Theme.Navy);

            Line(list, "잘된 선택", 24, Theme.Success, true);
            int count = 0;
            foreach (var g in report.Good) if (count++ < 4) Line(list, "○ " + g, 21, Theme.Navy);
            if (report.Good.Count == 0) Line(list, "다음에는 목표 성능과 납기를 먼저 맞춰 봐요.", 21, Theme.NavySoft);

            Line(list, "아쉬운 선택", 24, Theme.Warning, true);
            count = 0;
            foreach (var r in report.Regret) if (count++ < 4) Line(list, "△ " + r, 21, Theme.Navy);
            if (report.Regret.Count == 0) Line(list, "크게 아쉬운 점이 없는 조합이에요!", 21, Theme.NavySoft);
        }

        System.Collections.IEnumerator SelectNextFrame(Selectable s)
        {
            yield return null;
            Select(s);
        }

        static string OutcomeText(ItemOutcome o) => o switch
        {
            ItemOutcome.Full => "○ 해결",
            ItemOutcome.ShortPerformance => "△ 성능 부족",
            ItemOutcome.Late => "△ 납기 지연",
            _ => "△ 성능 부족 · 지연"
        };

        static TextMeshProUGUI Line(RectTransform parent, string text, float size, Color color, bool bold = false)
        {
            var t = UIKit.Text(parent, "Line", text, size, color, TextAlignmentOptions.TopLeft, bold);
            return t;
        }
    }
}
