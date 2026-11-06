using System.Collections.Generic;
using ProcurementHero.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProcurementHero
{
    public partial class GameManager
    {
        // ───────── 타이틀 ─────────

        TextMeshProUGUI titleBestText;

        static string TitleBestLabel() => SaveService.HasBest
            ? $"최고 기록 {F(SaveService.BestScore)}점 · 최다 성공 {SaveService.BestSuccess}개"
            : "최고 기록 없음";

        /// <summary>타이틀이 떠 있으면 최고 기록 문구를 저장 상태에 맞춘다. 진행 상태는 건드리지 않는다.</summary>
        void RefreshTitleBest()
        {
            if (titleBestText) titleBestText.text = TitleBestLabel();
        }

        public void ShowTitle()
        {
            BeginScreen(GameScreen.Title);
            var root = UIKit.Rect(screenRoot, "TitleScreen").Fill();

            // 아래쪽에 문제를 안고 있는 새봄마을 세 시설
            var village = UIKit.Rect(root, "Village").Region(0, 0.04f, 1, 0.34f);
            for (int i = 0; i < database.missions.Count && i < 3; i++)
            {
                var slot = UIKit.Rect(village, "Slot").Region(i / 3f, 0, (i + 1) / 3f, 1, 10);
                FacilityView.Create(slot, database.missions[i].facility).SetState(0, 0);
            }

            var title = UIKit.Text(root, "Title", "조달 히어로", 110, Theme.Navy, TextAlignmentOptions.Center, true);
            title.rectTransform.Region(0.1f, 0.83f, 0.9f, 0.98f);
            var sub = UIKit.Text(root, "Subtitle", "마을을 바꾸는 선택", 48, Theme.TealDark, TextAlignmentOptions.Center, true);
            sub.rectTransform.Region(0.1f, 0.77f, 0.9f, 0.84f);
            var tag = UIKit.Text(root, "Tagline", "새봄마을 공공구매 지원팀 신입 담당자가 되어 학교 · 보건소 · 대피소의 요청을 해결해요", 26,
                Theme.NavySoft, TextAlignmentOptions.Center);
            tag.rectTransform.Region(0.1f, 0.725f, 0.9f, 0.775f);

            var menu = UIKit.Panel(root, "Menu", Theme.WithAlpha(Theme.Paper, 0.95f)).rectTransform;
            menu.Region(0.37f, 0.385f, 0.63f, 0.715f);
            var list = UIKit.Rect(menu, "List").Fill(24, 24, 18, 18);
            UIKit.VerticalList(list, 10).childForceExpandHeight = true;

            var buttons = new List<Selectable>();
            if (Progress.HasAnyResult && !Progress.IsComplete)
            {
                buttons.Add(UIKit.Button(list, "Continue", "이어하기", ShowMap));
                buttons.Add(UIKit.Button(list, "NewGame", "처음부터 시작", ConfirmNewGame, ButtonStyle.Secondary));
            }
            else
            {
                buttons.Add(UIKit.Button(list, "Start", "게임 시작", ConfirmNewGame));
            }
            buttons.Add(UIKit.Button(list, "HowTo", "게임 방법", OpenHowTo, ButtonStyle.Secondary));
            buttons.Add(UIKit.Button(list, "Sound", "소리 설정", OpenSettings, ButtonStyle.Secondary));
            buttons.Add(UIKit.Button(list, "Credits", "작품 소개와 출처", OpenCredits, ButtonStyle.Secondary));
#if !UNITY_WEBGL
            buttons.Add(UIKit.Button(list, "Quit", "종료", QuitGame, ButtonStyle.Secondary));
#endif
            UIKit.ChainNavigation(buttons.ToArray(), true);
            Select(buttons[0]);

            titleBestText = UIKit.Text(root, "Best", TitleBestLabel(), 24, Theme.NavySoft, TextAlignmentOptions.Center);
            titleBestText.rectTransform.Region(0.3f, 0.345f, 0.7f, 0.385f);

            var note = UIKit.Text(root, "Note", "조달정책을 소재로 한 창작 게임입니다 · 조달청 공식 제작물이 아닙니다", 20,
                Theme.NavySoft, TextAlignmentOptions.Center);
            note.rectTransform.Region(0.2f, 0.004f, 0.8f, 0.038f);
        }

        void ConfirmNewGame()
        {
            if (Progress.HasAnyResult)
                Confirm("처음부터 시작할까요?", "지금까지의 미션 결과가 초기화돼요.\n(최고 기록과 소리 설정은 그대로 남아요.)",
                    "처음부터 시작", StartNewGame, "취소");
            else
                StartNewGame();
        }

        void QuitGame()
        {
            Confirm("게임을 종료할까요?", "최고 기록과 설정은 저장돼 있어요.", "종료", () =>
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }, "취소");
        }

        // ───────── 마을 지도 ─────────

        public void ShowMap()
        {
            BeginScreen(GameScreen.Map);
            var root = UIKit.Rect(screenRoot, "MapScreen").Fill();
            Header(root, "새봄마을 지도", $"현재 점수 합계 {F(Progress.TotalScore)}점 · 성공 {Progress.SuccessCount}/{Progress.MissionCount}");

            var guide = UIKit.Text(root, "Guide", "시설을 골라 요청을 해결해요. 미션은 차례대로 열리고, 해결한 미션은 다시 도전해 결과를 바꿀 수 있어요.",
                26, Theme.Navy, TextAlignmentOptions.Center);
            guide.rectTransform.Region(0.05f, 0.82f, 0.95f, 0.89f);

            var buttons = new List<Selectable>();
            Selectable first = null;
            for (int i = 0; i < database.missions.Count; i++)
            {
                int index = i;
                var m = database.missions[i];
                var r = Progress.GetResult(i);
                bool unlocked = Progress.IsUnlocked(i);

                var card = UIKit.Panel(root, "Facility" + i, Theme.Paper).rectTransform;
                card.Region(i / 3f, 0.13f, (i + 1) / 3f, 0.81f, 18);

                var viewHolder = UIKit.Rect(card, "View").Region(0, 0.38f, 1, 1, 12);
                var view = FacilityView.Create(viewHolder, m.facility);
                if (r != null) view.SetState(FacilityView.LevelOf(r.OutcomeA), FacilityView.LevelOf(r.OutcomeB));
                else view.SetState(0, 0);

                var name = UIKit.Text(card, "Name", $"미션 {i + 1} · {m.facilityName}", 30, Theme.Navy, TextAlignmentOptions.Center, true);
                name.rectTransform.Region(0, 0.29f, 1, 0.38f);
                var title = UIKit.Text(card, "Mission", m.title, 26, Theme.TealDark, TextAlignmentOptions.Center, true);
                title.rectTransform.Region(0, 0.22f, 1, 0.29f);

                string status;
                Color statusColor;
                if (r != null)
                {
                    status = $"{Mark(r.Success)} · {F(r.TotalScore)}점";
                    statusColor = r.Success ? Theme.Success : Theme.Warning;
                }
                else if (unlocked)
                {
                    status = "도전할 수 있어요";
                    statusColor = Theme.TealDark;
                }
                else
                {
                    status = "× 잠김 · 이전 미션을 먼저 해결해요";
                    statusColor = Theme.NavySoft;
                }
                var statusText = UIKit.Text(card, "Status", status, 24, statusColor, TextAlignmentOptions.Center, true);
                statusText.rectTransform.Region(0, 0.15f, 1, 0.22f);

                var button = UIKit.Button(card, "Play", r != null ? "다시 도전" : "도전하기", () => StartMission(index),
                    r != null ? ButtonStyle.Secondary : ButtonStyle.Primary);
                ((RectTransform)button.transform).Region(0.2f, 0.02f, 0.8f, 0.14f);
                button.interactable = unlocked;
                buttons.Add(button);
                if (unlocked && r == null && first == null) first = button;
            }

            var bottom = UIKit.Rect(root, "Bottom").Region(0.3f, 0.02f, 0.7f, 0.11f);
            UIKit.HorizontalList(bottom, 24);
            var title2 = UIKit.Button(bottom, "Title", "타이틀로", ShowTitle, ButtonStyle.Secondary);
            buttons.Add(title2);
            if (Progress.IsComplete)
            {
                var ending = UIKit.Button(bottom, "Ending", "엔딩 보기", ShowEnding);
                buttons.Add(ending);
                if (first == null) first = ending;
            }
            Select(first ? first : buttons[0]);
        }

        // ───────── 엔딩 ─────────

        public void ShowEnding()
        {
            if (!Progress.IsComplete)
            {
                ShowMap();
                return;
            }
            BeginScreen(GameScreen.Ending);
            var root = UIKit.Rect(screenRoot, "EndingScreen").Fill();

            string endingTitle = Progress.Ending switch
            {
                EndingKind.Hero => "모두의 하루를 바꾼 조달 히어로",
                EndingKind.Growing => "성장하는 공공구매 해결사",
                _ => "다시 도전하는 마을 지원팀"
            };
            string endingLine = Progress.Ending switch
            {
                EndingKind.Hero => "축제의 불빛, 시원한 쉼터, 안전한 대피소. 새봄마을의 하루가 달라졌어요!",
                EndingKind.Growing => "대부분의 요청을 해결했어요. 남은 한 곳도 곧 바꿀 수 있어요.",
                _ => "아직 불편한 곳이 남았어요. 성능 · 납기 · 비용을 다시 살펴 재도전해 봐요."
            };

            bool newRecord = SaveService.SubmitRecord(Progress.TotalScore, Progress.SuccessCount);
            sound.Play(Progress.Ending == EndingKind.Hero ? Sfx.Success : Sfx.Partial);

            Header(root, "최종 엔딩", $"성공 {Progress.SuccessCount}/{Progress.MissionCount}");
            var title = UIKit.Text(root, "EndingTitle", endingTitle, 58, Theme.TealDark, TextAlignmentOptions.Center, true);
            title.rectTransform.Region(0.05f, 0.8f, 0.95f, 0.895f);
            var line = UIKit.Text(root, "EndingLine", endingLine, 28, Theme.Navy, TextAlignmentOptions.Center);
            line.rectTransform.Region(0.05f, 0.75f, 0.95f, 0.8f);

            for (int i = 0; i < database.missions.Count; i++)
            {
                var m = database.missions[i];
                var r = Progress.GetResult(i);
                var card = UIKit.Panel(root, "Final" + i, Theme.Paper).rectTransform;
                card.Region(i / 3f, 0.24f, (i + 1) / 3f, 0.74f, 16);
                var holder = UIKit.Rect(card, "View").Region(0, 0.28f, 1, 1, 10);
                FacilityView.Create(holder, m.facility).SetState(FacilityView.LevelOf(r.OutcomeA), FacilityView.LevelOf(r.OutcomeB));
                var caption = UIKit.Text(card, "Caption",
                    $"<b>{m.facilityName} · {m.title}</b>\n<color=#{ColorUtility.ToHtmlStringRGB(r.Success ? Theme.Success : Theme.Warning)}>{Mark(r.Success)}</color> · {F(r.TotalScore)}점\n{r.ProductA.Name} + {r.ProductB.Name}",
                    23, Theme.Navy, TextAlignmentOptions.Center);
                caption.rectTransform.Region(0, 0, 1, 0.28f, 8);
            }

            string best = $"최종 점수 <b>{F(Progress.TotalScore)}</b>점  ·  최고 기록 {F(SaveService.BestScore)}점" + (newRecord ? "  <color=#E0862A>★ 새 기록!</color>" : "");
            var score = UIKit.Text(root, "Score", best, 34, Theme.Navy, TextAlignmentOptions.Center);
            score.rectTransform.Region(0.05f, 0.15f, 0.95f, 0.23f);

            var bottom = UIKit.Rect(root, "Buttons").Region(0.22f, 0.025f, 0.78f, 0.12f);
            UIKit.HorizontalList(bottom, 24);
            var restart = UIKit.Button(bottom, "Restart", "다시 시작", ConfirmNewGame);
            var map = UIKit.Button(bottom, "Map", "마을 지도 (재도전)", ShowMap, ButtonStyle.Secondary);
            var toTitle = UIKit.Button(bottom, "Title", "타이틀로", ShowTitle, ButtonStyle.Secondary);
            UIKit.ChainNavigation(new Selectable[] { restart, map, toTitle }, false);
            Select(restart);
        }
    }
}
