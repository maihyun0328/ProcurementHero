using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProcurementHero
{
    public partial class GameManager
    {
        public const string PolicyUrl = "https://pps.go.kr/kor/bbs/view.do?bbsSn=2608180009&key=00634";

        class Modal
        {
            public GameObject root;
            public RectTransform body;
            public RectTransform buttons;
            public GameObject previousSelection;
            public bool isPause;
        }

        readonly List<Modal> modalStack = new List<Modal>();

        public bool IsPaused => paused;
        public int ModalCount => modalStack.Count;

        Modal OpenModal(string title, float width, float height)
        {
            var blocker = UIKit.Panel(overlayRoot, "Modal_" + title, Theme.Shade, ShapeSprites.Square, true);
            blocker.rectTransform.Fill();
            var box = UIKit.Panel(blocker.transform, "Box", Theme.Paper).rectTransform;
            box.Region((1 - width) / 2, (1 - height) / 2, (1 + width) / 2, (1 + height) / 2);

            var head = UIKit.Text(box, "Title", title, 40, Theme.Navy, TextAlignmentOptions.Center, true);
            head.rectTransform.anchorMin = new Vector2(0, 1);
            head.rectTransform.anchorMax = new Vector2(1, 1);
            head.rectTransform.offsetMin = new Vector2(30, -90);
            head.rectTransform.offsetMax = new Vector2(-30, -20);

            var body = UIKit.Rect(box, "Body").Fill(48, 48, 100, 120);
            var buttons = UIKit.Rect(box, "Buttons");
            buttons.anchorMin = new Vector2(0, 0);
            buttons.anchorMax = new Vector2(1, 0);
            buttons.offsetMin = new Vector2(60, 28);
            buttons.offsetMax = new Vector2(-60, 98);
            UIKit.HorizontalList(buttons, 24);

            var modal = new Modal
            {
                root = blocker.gameObject,
                body = body,
                buttons = buttons,
                previousSelection = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null
            };
            modalStack.Add(modal);
            modals.Add(blocker.gameObject);
            return modal;
        }

        void CloseModal(Modal modal)
        {
            if (modal == null || !modalStack.Remove(modal)) return;
            modals.Remove(modal.root);
            Destroy(modal.root);
            if (modal.isPause) SetPaused(false);
            if (modal.previousSelection && modal.previousSelection.activeInHierarchy && EventSystem.current)
                EventSystem.current.SetSelectedGameObject(modal.previousSelection);
        }

        void CloseTopModal()
        {
            if (modalStack.Count > 0) CloseModal(modalStack[modalStack.Count - 1]);
        }

        void CloseAllModals()
        {
            foreach (var m in modalStack) if (m.root) Destroy(m.root);
            modalStack.Clear();
            modals.Clear();
        }

        Button ModalButton(Modal modal, string label, UnityAction action, ButtonStyle style = ButtonStyle.Primary)
        {
            return UIKit.Button(modal.buttons, label, label, action, style);
        }

        TextMeshProUGUI BodyText(Modal modal, string text, float size = 27)
        {
            var t = UIKit.Text(modal.body, "Text", text, size, Theme.Navy, TextAlignmentOptions.TopLeft);
            t.rectTransform.Fill();
            t.lineSpacing = 10;
            return t;
        }

        void Confirm(string title, string message, string yes, UnityAction onYes, string no)
        {
            var modal = OpenModal(title, 0.5f, 0.5f);
            BodyText(modal, message).alignment = TextAlignmentOptions.Center;
            var noButton = ModalButton(modal, no, () => CloseModal(modal), ButtonStyle.Secondary);
            var yesButton = ModalButton(modal, yes, () =>
            {
                CloseModal(modal);
                onYes?.Invoke();
            });
            UIKit.ChainNavigation(new Selectable[] { noButton, yesButton }, false);
            Select(noButton);
        }

        // ───────── 일시정지 ─────────

        public void OpenPause()
        {
            if (paused || modalStack.Count > 0) return;
            SetPaused(true);
            var modal = OpenModal("일시정지", 0.34f, 0.66f);
            modal.isPause = true;
            UIKit.VerticalList(modal.body, 14).childForceExpandHeight = true;
            modal.body.offsetMin = new Vector2(70, 40);
            modal.body.offsetMax = new Vector2(-70, -100);

            var items = new List<Selectable>
            {
                UIKit.Button(modal.body, "Resume", "계속하기", () => CloseModal(modal)),
                UIKit.Button(modal.body, "HowTo", "게임 방법", OpenHowTo, ButtonStyle.Secondary),
                UIKit.Button(modal.body, "Sound", "소리 설정", OpenSettings, ButtonStyle.Secondary),
                UIKit.Button(modal.body, "Map", "마을 지도로", ShowMap, ButtonStyle.Secondary),
                UIKit.Button(modal.body, "Title", "타이틀로", ShowTitle, ButtonStyle.Secondary)
            };
            UIKit.ChainNavigation(items.ToArray(), true);
            Select(items[0]);
        }

        // ───────── 게임 방법 ─────────

        void OpenHowTo()
        {
            var modal = OpenModal("게임 방법", 0.62f, 0.8f);
            BodyText(modal,
                "<b>목표</b>  학교 · 보건소 · 대피소의 요청에 맞춰 물품 A와 물품 B를 하나씩 구매해요.\n" +
                "<b>조작</b>  마우스로 카드를 눌러 고르고, 다시 누르면 선택이 취소돼요.\n" +
                "            키보드: 방향키로 이동 · Enter로 선택 · Esc 일시정지 · Space 연출 건너뛰기\n" +
                "<b>총비용</b>  두 제품의 구매비 + 유지비(평가 기간 전체). 예산 안이어야 구매할 수 있어요.\n" +
                "<b>성공 조건</b>  예산 이내 + 두 제품 모두 목표 성능 이상 + 두 제품 모두 납기 안에 도착\n" +
                "<b>점수</b>  성과(최대 60) + 환경(최대 15) + 예산 절약(최대 25)\n" +
                "            납기를 넘긴 제품은 성과가 40%만 인정돼요.\n" +
                "<b>재도전</b>  결과 화면이나 지도에서 다시 도전하면 그 미션의 결과가 새 결과로 바뀌어요.\n\n" +
                "<size=22><color=#55607E>※ 게임 속 제품 분류 · 수치 · 점수식은 모두 창작 규칙이며 실제 조달 제도나 심사 기준이 아니에요.</color></size>",
                26);
            Select(ModalButton(modal, "닫기", () => CloseModal(modal)));
        }

        // ───────── 소리 설정 ─────────

        void OpenSettings()
        {
            var modal = OpenModal("소리 설정", 0.46f, 0.66f);
            UIKit.VerticalList(modal.body, 18).childForceExpandHeight = false;

            var sfx = SliderRow(modal.body, "효과음", SaveService.SfxVolume, v => { SaveService.SfxVolume = v; sound.ApplySettings(); sound.Play(Sfx.Select); });
            var bgm = SliderRow(modal.body, "배경음", SaveService.BgmVolume, v => { SaveService.BgmVolume = v; sound.ApplySettings(); });

            Button mute = null;
            mute = UIKit.Button(modal.body, "Mute", MuteLabel(), () =>
            {
                SaveService.Muted = !SaveService.Muted;
                sound.ApplySettings();
                UIKit.SetLabel(mute, MuteLabel());
            }, ButtonStyle.Secondary);
            UIKit.Layout(mute, 64);

            TextMeshProUGUI bestLabel = null;
            var reset = UIKit.Button(modal.body, "ResetBest", "최고 기록 초기화", () =>
                Confirm("최고 기록을 지울까요?", "저장된 최고 기록만 지워져요.\n진행 중인 미션 결과와 소리 설정은 그대로예요.", "기록 지우기", () =>
                {
                    SaveService.ResetBest();
                    if (bestLabel) bestLabel.text = BestLabel();
                    RefreshTitleBest();
                }, "취소"), ButtonStyle.Danger);
            UIKit.Layout(reset, 64);
            bestLabel = UIKit.Text(modal.body, "Best", BestLabel(), 22, Theme.NavySoft, TextAlignmentOptions.Center);
            UIKit.Layout(bestLabel, 36);

            var close = ModalButton(modal, "닫기", () => CloseModal(modal));
            UIKit.ChainNavigation(new Selectable[] { sfx, bgm, mute, reset, close }, true);
            Select(sfx);
        }

        static string MuteLabel() => SaveService.Muted ? "음소거: 켜짐 (눌러서 소리 켜기)" : "음소거: 꺼짐 (눌러서 음소거)";

        static string BestLabel() => SaveService.HasBest
            ? $"현재 최고 기록 {F(SaveService.BestScore)}점 · 최다 성공 {SaveService.BestSuccess}개"
            : "저장된 최고 기록이 없어요";

        Slider SliderRow(RectTransform parent, string label, float value, UnityAction<float> onChange)
        {
            var row = UIKit.Rect(parent, "Row_" + label);
            UIKit.Layout(row, 64);
            var text = UIKit.Text(row, "Label", label, 28, Theme.Navy, TextAlignmentOptions.MidlineLeft, true);
            text.rectTransform.Region(0, 0, 0.25f, 1);

            var sliderRt = UIKit.Rect(row, "Slider").Region(0.27f, 0.2f, 1f, 0.8f);
            var bg = UIKit.Panel(sliderRt, "Background", Theme.CreamDeep, null, true);
            bg.rectTransform.Fill();
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
            slider.minValue = 0;
            slider.maxValue = 1;
            slider.value = value;
            UIKit.ApplyStyle(slider, ButtonStyle.Secondary);
            handle.color = Theme.TealDark;
            slider.onValueChanged.AddListener(onChange);
            return slider;
        }

        // ───────── 작품 소개와 출처 ─────────

        void OpenCredits()
        {
            var modal = OpenModal("작품 소개와 출처", 0.66f, 0.84f);
            BodyText(modal,
                "<b>조달 히어로: 마을을 바꾸는 선택</b>\n" +
                "공공구매의 가치를 알리기 위한 창작 게임입니다.\n" +
                "실제 계약 절차·평가 방식·제품 성능을 재현하지 않습니다.\n\n" +
                "· 새봄마을, 공공구매 지원팀, 모든 제품과 비용은 가상이며 비용은 게임 단위예요.\n" +
                "· 기본형 · 고효율형 · 혁신형 분류와 점수식은 게임을 위한 창작 규칙이에요.\n" +
                "· 조달청이 승인하거나 공식 제작한 게임이 아니에요. 공식 캐릭터와 로고를 쓰지 않았어요.\n\n" +
                "<b>정책 참고자료</b>\n조달청, 2026-08-18 혁신제품 시범구매 관련 보도자료\n" +
                "<size=20>" + PolicyUrl + "</size>\n\n" +
                "<b>글꼴</b>  Pretendard (길형진, SIL Open Font License 1.1)\n" +
                "<b>그래픽 · 효과음 · 배경음</b>  기본 도형과 코드로 직접 생성\n" +
                "<b>제작</b>  Unity 6000.0.61f1 · AI 도구를 활용해 제작했어요 (자세한 내용은 제출 문서 참고)",
                24);
            var close = ModalButton(modal, "닫기", () => CloseModal(modal));
            var link = ModalButton(modal, "보도자료 링크 열기", () => Application.OpenURL(PolicyUrl), ButtonStyle.Secondary);
            UIKit.ChainNavigation(new Selectable[] { link, close }, false);
            Select(close);
        }
    }
}
