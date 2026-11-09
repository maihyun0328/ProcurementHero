using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProcurementHero.Field
{
    /// <summary>
    /// 담당자가 쓰는 종이 수첩. 표지가 덮인 상태로 담당자 쪽에서 날아와 펼쳐지고,
    /// 닫히면 다시 담당자 쪽으로 돌아간다. 페이지 넘기기와 도장도 여기서 만든다.
    ///
    /// 넘김은 '책등을 축으로 도는 한 장(Leaf)'으로 표현한다. 펼친 책과 책등은 그대로 두고 넘어가는 한 장만 움직인다.
    /// 한 장은 앞면·뒷면에 실제 페이지 내용을 붙인 채 각도 θ(0→180°)만큼 돌며, 화면에서는 가로 크기 cos θ로 보인다.
    /// 시간 곡선은 각도가 아니라 화면에서 보이는 가장자리 위치에 건다.
    /// θ가 90°를 지날 때(폭이 0일 때) 앞면과 뒷면이 바뀐다. 각도에 맞춰 면 그늘과 아래 종이에 지는 부드러운 그림자가 바뀐다.
    /// 표지 열기·닫기도 같은 한 장 방식이며, 표지 뒷면에는 왼쪽 절반(가죽 + 왼쪽 페이지)이 붙어 넘어온다.
    /// UI RectTransform 크기·부모·투명도만 바꾸는 가벼운 연출이며, 일시정지와 무관한 실제 시간으로 움직인다.
    /// </summary>
    public static class FieldBook
    {
        public static readonly Color Page = Theme.Hex(0xFFF9EC);
        public static readonly Color PageBack = Theme.Hex(0xEFE3C8);
        public static readonly Color PageEdge = Theme.Hex(0xE2D3B0);
        public static readonly Color Leather = Theme.Hex(0x7A4E2D);
        public static readonly Color LeatherDark = Theme.Hex(0x5C3A21);
        public static readonly Color Line = Theme.WithAlpha(Theme.Hex(0x7FA7C9), 0.25f);

        // 책 좌표(0~1): 책등은 가운데 0.5. 오른쪽 페이지 [0.503, 0.988], 왼쪽 페이지 [0.012, 0.497] — 책등을 기준으로 서로 거울 위치다.
        const float PageInset = 0.003f, PageWidth = 0.485f, PageBottom = 0.02f, PageTop = 0.98f;
        /// <summary>넘어가는 한 장이 가장 들렸을 때(90°) 세로로 커지는 정도. 화면 쪽으로 다가오는 느낌만 아주 조금 준다.</summary>
        const float Lift = 0.018f;
        /// <summary>한 프레임에 나아가는 최대 시간. 내용 생성 등으로 긴 프레임이 생겨도 동작이 한 번에 끝나 버리지 않게 한다.</summary>
        const float MaxStep = 0.1f;

        public const float OpenFlySeconds = 0.24f, CoverOpenSeconds = 0.4f, CoverCloseSeconds = 0.32f, CloseFlySeconds = 0.2f, TurnSeconds = 0.62f;

        public sealed class Parts
        {
            public RectTransform book, left, right, band, cover;
            public CanvasGroup group, leftGroup, bandGroup;
            internal RectTransform leftHalf;
            internal Leaf coverLeaf;
            internal Turn turning;

            /// <summary>표지가 열린 정도(0 닫힘 ~ 1 펼침). 마지막으로 적용한 값.</summary>
            public float CoverOpen { get; internal set; }
            /// <summary>페이지를 넘기는 중인지(검증용).</summary>
            public bool Turning => turning != null;
        }

        // ───────── 만들기 ─────────

        /// <summary>펼친 책의 뼈대. 처음에는 표지가 오른쪽 페이지를 덮은 '닫힌' 모습이다.</summary>
        public static Parts Build(Transform parent, Vector2 regionMin, Vector2 regionMax, bool withBand)
        {
            var p = new Parts();
            p.book = UIKit.Rect(parent, "Book").Region(regionMin.x, regionMin.y, regionMax.x, regionMax.y);
            p.group = p.book.gameObject.AddComponent<CanvasGroup>();

            // 왼쪽 절반(가죽 뒷면 + 왼쪽 페이지). 닫혀 있을 때는 표지 뒷면에 붙어 있다가 표지와 함께 넘어온다.
            p.leftHalf = UIKit.Rect(p.book, "LeftHalf").Region(0, 0, 0.5f, 1);
            p.leftGroup = p.leftHalf.gameObject.AddComponent<CanvasGroup>();
            UIKit.Panel(p.leftHalf, "BackLeft", Leather).rectTransform.Fill();
            p.left = Paper(p.leftHalf, "LeftPage", new Vector2(0.024f, 0.02f), new Vector2(0.994f, 0.98f), true);

            var rightHalf = UIKit.Rect(p.book, "RightHalf").Region(0.5f, 0, 1, 1);
            UIKit.Panel(rightHalf, "BackRight", Leather).rectTransform.Fill();
            p.right = Paper(rightHalf, "RightPage", new Vector2(0.006f, 0.02f), new Vector2(0.976f, 0.98f), false);

            var spine = UIKit.Panel(p.book, "Spine", LeatherDark, ShapeSprites.Square).rectTransform;
            spine.Region(0.494f, 0.01f, 0.506f, 0.99f);
            for (int i = 0; i < 6; i++)
                UIKit.Shape(spine, "Ring", ShapeSprites.Circle, Theme.Hex(0xC9B48F), new Vector2(0.5f, 0.1f + i * 0.16f), Vector2.zero, new Vector2(22, 22)).raycastTarget = false;

            if (withBand)
            {
                // 아래 띠(계획 목록·예산·확정)는 펼쳐진 뒤에만 보인다.
                p.band = UIKit.Panel(p.book, "PlanBand", Theme.Hex(0xF3E7CC)).rectTransform;
                p.band.Region(0.02f, 0.03f, 0.98f, 0.19f);
                p.bandGroup = p.band.gameObject.AddComponent<CanvasGroup>();
            }

            // 표지: 책등에 붙은 한 장. 앞면은 가죽 표지, 뒷면에는 넘어오는 동안 왼쪽 절반이 붙는다.
            p.coverLeaf = NewLeaf(p.book, "CoverLeaf", 0f, 0f, 1f, 0.5f);
            p.cover = BuildCover(p.coverLeaf.front);
            p.coverLeaf.front.GetComponent<Image>().color = Color.clear;
            p.coverLeaf.frontShade.transform.SetAsLastSibling();
            SetClosed(p);
            return p;
        }

        /// <summary>장식(줄 무늬·책등 그늘·종이 가장자리)인지. 페이지 내용을 지우거나 옮길 때 남겨 둔다.</summary>
        public static bool IsDecoration(Transform t) => t.name == "Rule" || t.name == "Gutter" || t.name == "PageEdge";

        static RectTransform Paper(RectTransform parent, string name, Vector2 min, Vector2 max, bool leftSide)
        {
            var page = UIKit.Panel(parent, name, Page).rectTransform;
            page.Region(min.x, min.y, max.x, max.y);
            Decorate(page, leftSide);
            return page;
        }

        /// <summary>줄 노트 무늬, 책등 쪽 그늘, 바깥쪽 종이 가장자리(얇은 두께감). 클릭은 막지 않는다.</summary>
        static void Decorate(RectTransform page, bool leftSide)
        {
            for (int i = 1; i < 14; i++)
            {
                var line = UIKit.Panel(page, "Rule", Line, ShapeSprites.Square).rectTransform;
                line.anchorMin = new Vector2(0.04f, i / 14f);
                line.anchorMax = new Vector2(0.96f, i / 14f);
                line.sizeDelta = new Vector2(0, 2);
            }
            var shade = UIKit.Panel(page, "Gutter", Theme.WithAlpha(LeatherDark, 0.1f), ShapeSprites.Square).rectTransform;
            shade.Region(leftSide ? 0.94f : 0f, 0, leftSide ? 1f : 0.06f, 1);
            var edge = UIKit.Panel(page, "PageEdge", PageEdge, ShapeSprites.Square).rectTransform;
            edge.anchorMin = new Vector2(leftSide ? 0 : 1, 0);
            edge.anchorMax = new Vector2(leftSide ? 0 : 1, 1);
            edge.pivot = new Vector2(leftSide ? 0 : 1, 0.5f);
            edge.sizeDelta = new Vector2(4, 0);
            edge.anchoredPosition = Vector2.zero;
        }

        static RectTransform BuildCover(RectTransform face)
        {
            var cover = UIKit.Panel(face, "Cover", Leather).rectTransform;
            cover.Fill();
            // 스티치 테두리
            var stitch = UIKit.Rect(cover, "Stitch").Fill(24, 24, 24, 24);
            for (int i = 0; i < 24; i++)
            {
                float t = (i + 0.5f) / 24f;
                Dash(stitch, new Vector2(t, 1), new Vector2(18, 4));
                Dash(stitch, new Vector2(t, 0), new Vector2(18, 4));
            }
            for (int i = 0; i < 18; i++)
            {
                float t = (i + 0.5f) / 18f;
                Dash(stitch, new Vector2(0, t), new Vector2(4, 18));
                Dash(stitch, new Vector2(1, t), new Vector2(4, 18));
            }
            var label = UIKit.Panel(cover, "Label", Page).rectTransform;
            label.Region(0.2f, 0.56f, 0.8f, 0.74f);
            UIKit.Text(label, "Title", "현장 수첩", 44, LeatherDark, TextAlignmentOptions.Center, true).rectTransform.Fill();
            FieldArt.Magnifier(cover, 120).Place(new Vector2(0.5f, 0.38f), Vector2.zero, new Vector2(120, 120));
            var strap = UIKit.Panel(cover, "Strap", LeatherDark, ShapeSprites.Square).rectTransform;
            strap.Region(0.86f, 0, 0.9f, 1);
            return cover;
        }

        static void Dash(RectTransform parent, Vector2 anchor, Vector2 size)
        {
            UIKit.Shape(parent, "Dash", ShapeSprites.Square, Theme.Hex(0xD9B98A), anchor, Vector2.zero, size).raycastTarget = false;
        }

        // ───────── 넘어가는 한 장 ─────────

        /// <summary>
        /// 책등(책 좌표 x=0.5)에 붙은 한 장. root는 책등을 피벗으로 가로 크기 cos θ, 세로 1 + Lift·sin θ.
        /// 앞면은 오른쪽에 보이고, 뒷면은 자기 가운데를 기준으로 좌우를 한 번 더 뒤집어 왼쪽에서 글자가 바르게 보인다.
        /// </summary>
        internal sealed class Leaf
        {
            public RectTransform root, front, back, shadow;
            public CanvasGroup frontGroup, backGroup;
            public Image frontShade, backShade, shadowImage;
            public float width;

            /// <param name="open">0~1. 화면에서 보이는 종이 가장자리 위치(오른쪽 끝 → 책등 → 왼쪽 끝)를 직선으로 나눈 값.
            /// 원근이 없는 화면에서는 폭이 cos θ로만 보이므로, 각도가 아니라 가장자리 위치에 곡선을 걸어야
            /// 출발 직후 '멈춘 듯 보이는' 구간 없이 움직인다(실제 재생에서 확인).</param>
            public void Apply(float open)
            {
                float c = 1f - 2f * Mathf.Clamp01(open);
                float s = Mathf.Sqrt(Mathf.Max(0f, 1f - c * c));
                // 폭이 정확히 0이면 글자 메시가 무너지므로 아주 얇게 둔다(방향 부호는 유지).
                float x = Mathf.Abs(c) < 0.002f ? (c >= 0 ? 0.002f : -0.002f) : c;
                root.localScale = new Vector3(x, 1f + Lift * s, 1f);
                bool frontSide = c >= 0f;
                frontGroup.alpha = frontSide ? 1f : 0f;
                backGroup.alpha = frontSide ? 0f : 1f;
                frontGroup.blocksRaycasts = frontSide;
                backGroup.blocksRaycasts = !frontSide;
                // 면 그늘: 화면에서 멀어지며 기울수록(폭이 좁을수록) 조금 어두워진다.
                float tilt = 1f - Mathf.Abs(c);
                frontShade.color = Theme.WithAlpha(LeatherDark, 0.22f * tilt * tilt);
                backShade.color = Theme.WithAlpha(LeatherDark, 0.22f * tilt * tilt);
                // 아래 종이에 지는 그림자: 넘어가는 장의 가장자리 바로 바깥에 부드럽게, 들린 만큼만.
                float edge = 0.5f + width * c;
                float w = 0.07f * s;
                bool show = s > 0.01f;
                shadow.gameObject.SetActive(show);
                if (show)
                {
                    shadow.anchorMin = new Vector2(frontSide ? edge : edge - w, PageBottom);
                    shadow.anchorMax = new Vector2(frontSide ? edge + w : edge, PageTop);
                    shadow.offsetMin = shadow.offsetMax = Vector2.zero;
                    shadow.localScale = new Vector3(frontSide ? 1 : -1, 1, 1);
                    shadowImage.color = Theme.WithAlpha(Color.black, 0.26f * s);
                }
            }
        }

        static Sprite shadowSprite;

        /// <summary>왼쪽이 진하고 오른쪽으로 갈수록 투명해지는 그림자 그림(코드로 한 번 만든다).</summary>
        static Sprite ShadowSprite
        {
            get
            {
                if (shadowSprite) return shadowSprite;
                var tex = new Texture2D(64, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
                for (int x = 0; x < 64; x++)
                {
                    float a = 1f - x / 63f;
                    for (int y = 0; y < 4; y++) tex.SetPixel(x, y, new Color(1, 1, 1, a * a));
                }
                tex.Apply();
                shadowSprite = Sprite.Create(tex, new Rect(0, 0, 64, 4), new Vector2(0.5f, 0.5f));
                shadowSprite.hideFlags = HideFlags.DontSave;
                return shadowSprite;
            }
        }

        /// <param name="faceMin">한 장 안에서 면이 시작하는 가로 위치(책등 틈을 뺀 페이지 영역).</param>
        static Leaf NewLeaf(RectTransform book, string name, float faceMin, float bottom, float top, float PageWidth)
        {
            var leaf = new Leaf { width = PageWidth };
            // 그림자는 한 장보다 먼저(아래) 그린다.
            leaf.shadow = UIKit.Panel(book, name + "Shadow", Color.clear, ShadowSprite).rectTransform;
            leaf.shadowImage = leaf.shadow.GetComponent<Image>();
            leaf.shadowImage.raycastTarget = false;
            leaf.shadow.pivot = new Vector2(0.5f, 0.5f);
            leaf.shadow.gameObject.SetActive(false);

            leaf.root = UIKit.Rect(book, name);
            leaf.root.anchorMin = new Vector2(0.5f, bottom);
            leaf.root.anchorMax = new Vector2(0.5f + PageWidth, top);
            leaf.root.offsetMin = leaf.root.offsetMax = Vector2.zero;
            leaf.root.pivot = new Vector2(0f, 0.5f);

            float inset = faceMin <= 0f ? 0f : PageInset / PageWidth;
            leaf.front = UIKit.Panel(leaf.root, "Front", Page).rectTransform;
            leaf.front.Region(inset, 0, 1, 1);
            leaf.frontGroup = leaf.front.gameObject.AddComponent<CanvasGroup>();
            leaf.back = UIKit.Panel(leaf.root, "Back", Page).rectTransform;
            leaf.back.Region(inset, 0, 1, 1);
            leaf.back.localScale = new Vector3(-1, 1, 1);
            leaf.backGroup = leaf.back.gameObject.AddComponent<CanvasGroup>();
            leaf.frontShade = UIKit.Panel(leaf.front, "Shade", Color.clear, ShapeSprites.Square).rectTransform.GetComponent<Image>();
            leaf.frontShade.rectTransform.Fill();
            leaf.frontShade.raycastTarget = false;
            leaf.backShade = UIKit.Panel(leaf.back, "Shade", Color.clear, ShapeSprites.Square).rectTransform.GetComponent<Image>();
            leaf.backShade.rectTransform.Fill();
            leaf.backShade.raycastTarget = false;
            return leaf;
        }

        // ───────── 표지 상태 ─────────

        /// <summary>
        /// 표지를 open(0 닫힘 ~ 1 펼침)만큼 넘긴 모습으로 맞춘다. 펼치는 동안 왼쪽 절반은 표지 뒷면에 붙어 함께 넘어오고,
        /// 다 펼치면 책의 왼쪽 자리로 돌아간다. 아래 띠는 마지막에 나타난다. 시작 연출도 이 함수로 표지를 넘긴다.
        /// </summary>
        public static void SetCover(Parts p, float open)
        {
            FinishTurn(p);
            open = Mathf.Clamp01(open);
            p.CoverOpen = open;
            var leaf = p.coverLeaf;
            if (open >= 1f)
            {
                DockLeftHalf(p, true);
                leaf.root.gameObject.SetActive(false);
                leaf.shadow.gameObject.SetActive(false);
            }
            else
            {
                DockLeftHalf(p, false);
                leaf.root.gameObject.SetActive(true);
                leaf.Apply(open);
            }
            p.leftGroup.alpha = 1f;
            if (p.bandGroup) p.bandGroup.alpha = Mathf.SmoothStep(0, 1, Mathf.Clamp01((open - 0.9f) / 0.1f));
        }

        static void DockLeftHalf(Parts p, bool inBook)
        {
            var target = inBook ? p.book : p.coverLeaf.back;
            if (p.leftHalf.parent == target) return;
            p.leftHalf.SetParent(target, false);
            if (inBook)
            {
                p.leftHalf.SetSiblingIndex(0);
                p.leftHalf.Region(0, 0, 0.5f, 1);
            }
            else
            {
                p.leftHalf.SetSiblingIndex(0);
                p.leftHalf.Fill();
            }
            p.leftHalf.localScale = Vector3.one;
        }

        /// <summary>닫힌 상태로 즉시 맞춘다.</summary>
        public static void SetClosed(Parts p) => SetCover(p, 0f);

        /// <summary>펼친 정상 상태로 즉시 맞춘다(위치·회전·크기·투명도 포함). 넘기던 페이지가 있으면 먼저 끝낸다.</summary>
        public static void SetOpen(Parts p)
        {
            SetCover(p, 1f);
            p.book.anchoredPosition = Vector2.zero;
            p.book.localRotation = Quaternion.identity;
            p.book.localScale = Vector3.one;
            p.group.alpha = 1;
        }

        // ───────── 시간 곡선 ─────────

        public static float EaseOutCubic(float t) { t = 1f - Mathf.Clamp01(t); return 1f - t * t * t; }
        public static float EaseInCubic(float t) { t = Mathf.Clamp01(t); return t * t * t; }
        /// <summary>천천히 들고, 중간은 자연스럽게, 끝에서 부드럽게 내려놓는 곡선.</summary>
        public static float EaseInOutSine(float t) => 0.5f - 0.5f * Mathf.Cos(Mathf.Clamp01(t) * Mathf.PI);

        static IEnumerator Tween(float duration, Action<float> step)
        {
            float t = 0;
            while (t < duration)
            {
                t += Mathf.Min(Time.unscaledDeltaTime, MaxStep);
                step(Mathf.Clamp01(t / duration));
                yield return null;
            }
            step(1f);
        }

        // ───────── 열기·닫기 ─────────

        /// <summary>
        /// 담당자 손(화면 좌표 from)에서 닫힌 수첩이 날아와 커진 뒤(약 0.24초, 끝에서 감속), 표지가 책등을 축으로 넘어간다(약 0.4초).
        /// 날아오는 동안 책은 불투명하다(표지 너머 글자가 비치지 않음). onFlip(종이 소리)은 표지가 들리기 시작할 때 한 번.
        /// </summary>
        public static IEnumerator Open(Parts p, Vector2 from, Action onFlip)
        {
            FinishTurn(p);
            SetClosed(p);
            p.group.interactable = false;
            p.group.alpha = 1f;
            // 날아오기의 끝 감속과 표지 넘김의 시작이 이어 붙으면 그 사이가 멈춘 듯 보였다(실제 재생). 두 동작을 조금 겹친다.
            float flipStart = OpenFlySeconds * 0.7f, total = flipStart + CoverOpenSeconds;
            bool flipped = false;
            yield return Tween(total, t =>
            {
                float time = t * total;
                float e = EaseOutCubic(time / OpenFlySeconds);
                p.book.anchoredPosition = Vector2.Lerp(from, Vector2.zero, e);
                p.book.localScale = Vector3.one * Mathf.Lerp(0.12f, 1f, e);
                p.book.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(-6f, 0f, e));
                if (time >= flipStart)
                {
                    if (!flipped) { flipped = true; onFlip?.Invoke(); }
                    SetCover(p, EaseInOutSine((time - flipStart) / CoverOpenSeconds));
                }
            });
            if (!flipped) onFlip?.Invoke();
            SetOpen(p);
            p.group.interactable = true;
        }

        /// <summary>표지가 책등을 축으로 덮이고(약 0.32초), 끝나 갈 즈음 담당자 쪽(to)으로 작아지며 돌아간다(약 0.2초, 마지막에만 흐려짐).</summary>
        public static IEnumerator Close(Parts p, Vector2 to)
        {
            FinishTurn(p);
            p.group.interactable = false;
            float flyStart = CoverCloseSeconds * 0.8f, total = flyStart + CloseFlySeconds;
            yield return Tween(total, t =>
            {
                float time = t * total;
                SetCover(p, 1f - EaseInOutSine(time / CoverCloseSeconds));
                float f = Mathf.Clamp01((time - flyStart) / CloseFlySeconds);
                float e = EaseInCubic(f);
                p.book.anchoredPosition = Vector2.Lerp(Vector2.zero, to, e);
                p.book.localScale = Vector3.one * Mathf.Lerp(1f, 0.1f, e);
                p.group.alpha = 1f - Mathf.Clamp01((f - 0.6f) / 0.4f) * 0.8f;
            });
            SetClosed(p);
        }

        // ───────── 페이지 넘기기 ─────────

        /// <summary>진행 중인 넘김. 끝나거나 중단돼도 Complete로 한 번만 정리한다.</summary>
        internal sealed class Turn
        {
            public Parts parts;
            public Leaf leaf;
            public RectTransform under;
            bool done;

            /// <summary>뒷면에 붙여 둔 새 왼쪽 내용을 왼쪽 페이지로 돌려놓고, 넘기던 장·이전 내용·그림자를 지운다. 여러 번 불러도 한 번만 처리.</summary>
            public void Complete()
            {
                if (done) return;
                done = true;
                if (leaf != null && leaf.back && parts.left) MoveContent(leaf.back, parts.left);
                if (leaf != null) { Kill(leaf.root ? leaf.root.gameObject : null); Kill(leaf.shadow ? leaf.shadow.gameObject : null); }
                Kill(under ? under.gameObject : null);
                if (parts.turning == this) parts.turning = null;
            }
        }

        /// <summary>넘기던 페이지가 있으면 바로 끝낸 상태(새 내용이 제자리)로 정리한다. 건너뛰기·중단·다음 연출 시작 때 쓴다.</summary>
        public static void FinishTurn(Parts p)
        {
            p?.turning?.Complete();
        }

        static void Kill(GameObject go)
        {
            if (!go) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(go);
            else UnityEngine.Object.DestroyImmediate(go);
        }

        /// <summary>장식을 뺀 페이지 내용을 다른 같은 크기 페이지로 옮긴다(앵커·크기 그대로, 장식 아래가 아니라 위에 놓임).</summary>
        static void MoveContent(RectTransform from, RectTransform to)
        {
            var items = new List<Transform>();
            foreach (Transform c in from)
                if (!IsDecoration(c) && c.name != "Shade") items.Add(c);
            foreach (var c in items) c.SetParent(to, false);
            // 면 그늘은 늘 맨 위
            var shade = to.Find("Shade");
            if (shade) shade.SetAsLastSibling();
        }

        /// <summary>
        /// 오른쪽 페이지 한 장을 책등을 축으로 왼쪽으로 넘긴다(약 0.62초).
        /// ① 시작 전에 이전 오른쪽 내용을 넘어갈 장의 앞면으로, 이전 왼쪽 내용을 왼쪽 위 받침 종이로 옮긴다(넘기는 동안 그대로 보임).
        /// ② buildNext를 <b>한 번만</b> 불러 새 내용을 p.left·p.right에 만든다(새 오른쪽은 넘어가는 장 밑에 가려져 있다가 드러남).
        /// ③ 새 왼쪽 내용은 장의 뒷면에 붙여, 장이 내려앉을 때 함께 내려와 이전 왼쪽 내용을 덮는다.
        /// ④ 내용을 만든 프레임이 길어도 동작이 건너뛰지 않도록 한 프레임 쉰 뒤 움직인다. onSound는 종이가 들리기 시작할 때 한 번.
        /// 끝나거나 FinishTurn으로 중단되면 새 왼쪽 내용을 왼쪽 페이지로 돌려놓고 임시 오브젝트를 모두 지운다.
        /// </summary>
        public static IEnumerator TurnPage(Parts p, Action buildNext, Action onSound = null)
        {
            FinishTurn(p);
            if (p.CoverOpen < 1f) SetOpen(p);
            var turn = new Turn { parts = p };
            p.turning = turn;

            // 이전 왼쪽 내용을 받쳐 둘 종이(왼쪽 페이지와 같은 자리·크기)
            turn.under = UIKit.Panel(p.book, "TurnUnder", Page).rectTransform;
            turn.under.anchorMin = new Vector2(0.5f - PageInset - PageWidth, PageBottom);
            turn.under.anchorMax = new Vector2(0.5f - PageInset, PageTop);
            turn.under.offsetMin = turn.under.offsetMax = Vector2.zero;
            turn.under.SetSiblingIndex(p.leftHalf.GetSiblingIndex() + 1);
            Decorate(turn.under, true);

            turn.leaf = NewLeaf(p.book, "TurningPage", 0.5f + PageInset, PageBottom, PageTop, PageWidth + PageInset);
            Decorate(turn.leaf.front, false);
            Decorate(turn.leaf.back, true);
            turn.leaf.frontShade.transform.SetAsLastSibling();
            turn.leaf.backShade.transform.SetAsLastSibling();
            // 그림자 → 넘어가는 장 순서로 맨 위에(띠·다른 내용보다 위)
            turn.leaf.shadow.SetAsLastSibling();
            turn.leaf.root.SetAsLastSibling();

            MoveContent(p.right, turn.leaf.front);
            MoveContent(p.left, turn.under);
            turn.leaf.Apply(0f);

            buildNext?.Invoke();
            MoveContent(p.left, turn.leaf.back);

            // 새 내용을 만든 프레임은 길 수 있다. 한 프레임 쉰 뒤(움직이지 않은 채) 넘기기 시작한다.
            yield return null;
            bool sounded = false;
            yield return Tween(TurnSeconds, t =>
            {
                if (turn.leaf.root == null) return;
                float a = EaseInOutSine(t);
                if (!sounded && a > 0.04f) { sounded = true; onSound?.Invoke(); }
                turn.leaf.Apply(a);
            });
            if (!sounded) onSound?.Invoke();
            turn.Complete();
        }

        /// <summary>종이에 도장을 찍는다: 크게 내려와 찍히고 책이 살짝 눌린다.</summary>
        public static IEnumerator Stamp(Parts p, RectTransform page, Vector2 anchor, string text, Color color, float size = 190f)
        {
            var stamp = UIKit.Panel(page, "Stamp", Theme.WithAlpha(color, 0.92f), ShapeSprites.Circle).rectTransform;
            stamp.Place(anchor, Vector2.zero, new Vector2(size, size));
            stamp.localRotation = Quaternion.Euler(0, 0, -14);
            UIKit.Panel(stamp, "Inner", Page, ShapeSprites.Circle).rectTransform.Fill(size * 0.07f, size * 0.07f, size * 0.07f, size * 0.07f);
            var label = UIKit.Text(stamp, "Text", text, size * 0.26f, color, TextAlignmentOptions.Center, true);
            label.rectTransform.Fill();
            var group = stamp.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            yield return Tween(0.12f, t =>
            {
                stamp.localScale = Vector3.one * Mathf.Lerp(1.9f, 1f, t * t);
                group.alpha = t;
            });
            // 종이가 눌리는 반동
            yield return Tween(0.14f, t => p.book.localScale = new Vector3(1f + Mathf.Sin(t * Mathf.PI) * 0.01f, 1f - Mathf.Sin(t * Mathf.PI) * 0.025f, 1));
            p.book.localScale = Vector3.one;
        }
    }
}
