using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProcurementHero.Field
{
    /// <summary>
    /// 담당자가 쓰는 종이 수첩. 표지가 덮인 상태로 담당자 쪽에서 날아와 펼쳐지고,
    /// 닫히면 다시 담당자 쪽으로 돌아간다. 페이지 넘기기와 도장도 여기서 만든다.
    /// 모두 UI RectTransform 크기·회전·투명도만 바꾸는 가벼운 연출이며 일시정지와 무관한 실제 시간으로 움직인다.
    /// </summary>
    public static class FieldBook
    {
        public static readonly Color Page = Theme.Hex(0xFFF9EC);
        public static readonly Color PageBack = Theme.Hex(0xEFE3C8);
        public static readonly Color Leather = Theme.Hex(0x7A4E2D);
        public static readonly Color LeatherDark = Theme.Hex(0x5C3A21);
        public static readonly Color Line = Theme.WithAlpha(Theme.Hex(0x7FA7C9), 0.25f);

        public sealed class Parts
        {
            public RectTransform book, left, right, band, cover, coverInside;
            public CanvasGroup group, leftGroup, bandGroup;

            /// <summary>왼쪽 페이지와 아래 띠의 보임 정도(펼침 정도).</summary>
            public float Spread
            {
                set
                {
                    leftGroup.alpha = value;
                    if (bandGroup) bandGroup.alpha = value;
                }
            }
        }

        /// <summary>펼친 책의 뼈대. 처음에는 표지가 오른쪽 페이지를 덮은 '닫힌' 모습이다.</summary>
        public static Parts Build(Transform parent, Vector2 regionMin, Vector2 regionMax, bool withBand)
        {
            var p = new Parts();
            p.book = UIKit.Rect(parent, "Book").Region(regionMin.x, regionMin.y, regionMax.x, regionMax.y);
            p.group = p.book.gameObject.AddComponent<CanvasGroup>();

            // 왼쪽 절반(가죽 뒷면 + 왼쪽 페이지)은 펼쳐질 때 나타난다.
            var leftHalf = UIKit.Rect(p.book, "LeftHalf").Region(0, 0, 0.5f, 1);
            p.leftGroup = leftHalf.gameObject.AddComponent<CanvasGroup>();
            UIKit.Panel(leftHalf, "BackLeft", Leather).rectTransform.Fill();
            p.left = Paper(leftHalf, "LeftPage", new Vector2(0.024f, 0.02f), new Vector2(0.994f, 0.98f), true);

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

            p.coverInside = UIKit.Panel(p.book, "CoverInside", PageBack).rectTransform;
            p.coverInside.Region(0, 0, 0.5f, 1);
            p.coverInside.pivot = new Vector2(1, 0.5f);
            p.coverInside.localScale = new Vector3(0, 1, 1);

            p.cover = BuildCover(p.book);
            return p;
        }

        static RectTransform Paper(RectTransform parent, string name, Vector2 min, Vector2 max, bool leftSide)
        {
            var page = UIKit.Panel(parent, name, Page).rectTransform;
            page.Region(min.x, min.y, max.x, max.y);
            // 줄 노트 무늬와 책등 쪽 그늘(장식, 클릭 막지 않음)
            for (int i = 1; i < 14; i++)
            {
                var line = UIKit.Panel(page, "Rule", Line, ShapeSprites.Square).rectTransform;
                line.anchorMin = new Vector2(0.04f, i / 14f);
                line.anchorMax = new Vector2(0.96f, i / 14f);
                line.sizeDelta = new Vector2(0, 2);
            }
            var shade = UIKit.Panel(page, "Gutter", Theme.WithAlpha(LeatherDark, 0.1f), ShapeSprites.Square).rectTransform;
            shade.Region(leftSide ? 0.94f : 0f, 0, leftSide ? 1f : 0.06f, 1);
            return page;
        }

        static RectTransform BuildCover(RectTransform book)
        {
            var cover = UIKit.Panel(book, "Cover", Leather).rectTransform;
            cover.Region(0.5f, 0, 1, 1);
            cover.pivot = new Vector2(0, 0.5f);
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

        /// <summary>닫힌 상태로 즉시 맞춘다.</summary>
        public static void SetClosed(Parts p)
        {
            p.cover.localScale = Vector3.one;
            p.coverInside.localScale = new Vector3(0, 1, 1);
            p.Spread = 0;
        }

        public static void SetOpen(Parts p)
        {
            p.cover.localScale = new Vector3(0, 1, 1);
            p.coverInside.localScale = new Vector3(0, 1, 1);
            p.Spread = 1;
            p.book.anchoredPosition = Vector2.zero;
            p.book.localRotation = Quaternion.identity;
            p.book.localScale = Vector3.one;
            p.group.alpha = 1;
        }

        static float EaseOut(float t) => 1f - (1f - t) * (1f - t);

        static IEnumerator Tween(float duration, Action<float> step)
        {
            float t = 0;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                step(Mathf.Clamp01(t / duration));
                yield return null;
            }
            step(1f);
        }

        /// <summary>담당자 손(화면 좌표 from) → 화면 가운데로 날아오며 커진 뒤 표지를 넘긴다. 약 0.5초.</summary>
        public static IEnumerator Open(Parts p, Vector2 from, Action onFlip)
        {
            SetClosed(p);
            p.group.interactable = false;
            yield return Tween(0.22f, t =>
            {
                float e = EaseOut(t);
                p.book.anchoredPosition = Vector2.Lerp(from, Vector2.zero, e);
                p.book.localScale = Vector3.one * Mathf.Lerp(0.12f, 1f, e);
                p.book.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(-8f, 0f, e));
                p.group.alpha = Mathf.Lerp(0.4f, 1f, e);
            });
            onFlip?.Invoke();
            // 표지가 책등을 축으로 넘어간다: 오른쪽에서 줄어들고, 왼쪽에 안쪽 면이 펼쳐진다.
            yield return Tween(0.13f, t => p.cover.localScale = new Vector3(1 - t, 1, 1));
            yield return Tween(0.13f, t =>
            {
                p.coverInside.localScale = new Vector3(t, 1, 1);
                p.Spread = t;
            });
            yield return Tween(0.08f, t => p.coverInside.localScale = new Vector3(1 - t, 1, 1));
            SetOpen(p);
            p.group.interactable = true;
        }

        /// <summary>표지를 덮고 담당자 쪽(to)으로 작아지며 돌아간다. 약 0.45초.</summary>
        public static IEnumerator Close(Parts p, Vector2 to)
        {
            p.group.interactable = false;
            yield return Tween(0.08f, t =>
            {
                p.coverInside.localScale = new Vector3(t, 1, 1);
                p.Spread = 1 - t;
            });
            yield return Tween(0.1f, t => p.coverInside.localScale = new Vector3(1 - t, 1, 1));
            yield return Tween(0.1f, t => p.cover.localScale = new Vector3(t, 1, 1));
            yield return Tween(0.2f, t =>
            {
                float e = t * t;
                p.book.anchoredPosition = Vector2.Lerp(Vector2.zero, to, e);
                p.book.localScale = Vector3.one * Mathf.Lerp(1f, 0.1f, e);
                p.group.alpha = Mathf.Lerp(1f, 0.2f, e);
            });
        }

        /// <summary>
        /// 오른쪽 페이지를 넘긴다. 앞면이 책등 쪽으로 접히며 그림자가 지고(onMid에서 내용 교체),
        /// 뒷면이 왼쪽에 펼쳐진다. 약 0.7초.
        /// </summary>
        public static IEnumerator TurnPage(Parts p, Action onMid)
        {
            var leaf = UIKit.Panel(p.book, "TurningPage", Page).rectTransform;
            leaf.Region(0.503f, 0.02f, 0.988f, 0.98f);
            leaf.pivot = new Vector2(0, 0.5f);
            for (int i = 1; i < 14; i++)
            {
                var line = UIKit.Panel(leaf, "Rule", Line, ShapeSprites.Square).rectTransform;
                line.anchorMin = new Vector2(0.04f, i / 14f);
                line.anchorMax = new Vector2(0.96f, i / 14f);
                line.sizeDelta = new Vector2(0, 2);
            }
            var curl = UIKit.Panel(leaf, "Curl", Theme.WithAlpha(LeatherDark, 0f), ShapeSprites.Square).rectTransform;
            curl.Region(0.8f, 0, 1, 1);
            var shadow = UIKit.Panel(p.book, "TurnShadow", Theme.WithAlpha(Color.black, 0f), ShapeSprites.Square).rectTransform;
            shadow.Region(0.503f, 0.02f, 0.988f, 0.98f);
            shadow.SetSiblingIndex(leaf.GetSiblingIndex());

            yield return Tween(0.33f, t =>
            {
                float e = t * t;
                leaf.localScale = new Vector3(1 - e, 1, 1);
                leaf.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * Mathf.PI) * -2f);
                curl.GetComponent<Image>().color = Theme.WithAlpha(LeatherDark, 0.35f * t);
                shadow.GetComponent<Image>().color = Theme.WithAlpha(Color.black, 0.18f * Mathf.Sin(t * Mathf.PI));
            });
            onMid?.Invoke();
            leaf.GetComponent<Image>().color = PageBack;
            leaf.Region(0.012f, 0.02f, 0.497f, 0.98f);
            leaf.pivot = new Vector2(1, 0.5f);
            leaf.SetAsLastSibling();
            yield return Tween(0.33f, t =>
            {
                float e = EaseOut(t);
                leaf.localScale = new Vector3(e, 1, 1);
                curl.GetComponent<Image>().color = Theme.WithAlpha(LeatherDark, 0.35f * (1 - t));
                leaf.GetComponent<Image>().color = Theme.WithAlpha(PageBack, 1 - t * t);
                foreach (Transform child in leaf)
                    if (child.TryGetComponent(out Image img)) img.color = Theme.WithAlpha(img.color, (1 - t) * img.color.a);
            });
            UnityEngine.Object.Destroy(leaf.gameObject);
            UnityEngine.Object.Destroy(shadow.gameObject);
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
