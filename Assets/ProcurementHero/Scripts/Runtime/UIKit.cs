using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ProcurementHero
{
    public enum ButtonStyle
    {
        Primary,
        Secondary,
        Danger,
        Ghost
    }

    /// <summary>코드로 uGUI 계층을 만드는 작은 도우미. 앵커 기반이라 해상도가 바뀌어도 배치가 유지된다.</summary>
    public static class UIKit
    {
        public static TMP_FontAsset Font;
        public static Action OnButtonSound;

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // UI
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Fill(this RectTransform rt, float left = 0, float right = 0, float top = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        /// <summary>앵커 구역(비율)을 지정하고 여백을 준다.</summary>
        public static RectTransform Region(this RectTransform rt, float xMin, float yMin, float xMax, float yMax, float pad = 0)
        {
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = new Vector2(pad, pad);
            rt.offsetMax = new Vector2(-pad, -pad);
            return rt;
        }

        /// <summary>한 점에 고정된 크기로 놓는다.</summary>
        public static RectTransform Place(this RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size, Vector2? pivot = null)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        public static Image Panel(Transform parent, string name, Color color, Sprite sprite = null, bool raycast = false)
        {
            var rt = Rect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite ? sprite : ShapeSprites.Rounded;
            img.type = img.sprite == ShapeSprites.Rounded ? Image.Type.Sliced : Image.Type.Simple;
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        public static Image Shape(Transform parent, string name, Sprite sprite, Color color, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var img = Panel(parent, name, color, sprite);
            img.rectTransform.Place(anchor, pos, size);
            return img;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, string text, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Left, bool bold = false)
        {
            var rt = Rect(parent, name);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (Font) t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Button Button(Transform parent, string name, string label, UnityAction onClick,
            ButtonStyle style = ButtonStyle.Primary, float fontSize = 30)
        {
            var img = Panel(parent, name, Color.white, null, true);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            ApplyStyle(btn, style);

            var text = Text(img.transform, "Label", label, fontSize,
                style == ButtonStyle.Secondary || style == ButtonStyle.Ghost ? Theme.Navy : Color.white,
                TextAlignmentOptions.Center, true);
            text.rectTransform.Fill(12, 12, 4, 4);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.enableAutoSizing = true;
            text.fontSizeMin = 16;
            text.fontSizeMax = fontSize;

            img.gameObject.AddComponent<FocusRing>();
            if (onClick != null) btn.onClick.AddListener(onClick);
            btn.onClick.AddListener(() => OnButtonSound?.Invoke());
            return btn;
        }

        public static void ApplyStyle(Selectable s, ButtonStyle style)
        {
            Color normal = style switch
            {
                ButtonStyle.Primary => Theme.Teal,
                ButtonStyle.Danger => Theme.Danger,
                ButtonStyle.Ghost => Theme.WithAlpha(Theme.Paper, 0.85f),
                _ => Theme.Hex(0xF1E6CE)
            };
            s.targetGraphic.color = normal;
            var cb = ColorBlock.defaultColorBlock;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(0.9f, 0.97f, 0.96f);
            cb.pressedColor = new Color(0.75f, 0.85f, 0.85f);
            // 키보드 포커스가 눈에 보이도록 선택 색을 분명하게 둔다.
            cb.selectedColor = new Color(0.82f, 0.93f, 1f);
            cb.disabledColor = new Color(0.78f, 0.77f, 0.74f, 0.8f);
            cb.fadeDuration = 0.08f;
            s.colors = cb;
        }

        public static void SetLabel(Button b, string label)
        {
            var t = b.GetComponentInChildren<TMP_Text>();
            if (t) t.text = label;
        }

        public static VerticalLayoutGroup VerticalList(RectTransform rt, float spacing, RectOffset padding = null,
            TextAnchor align = TextAnchor.UpperLeft)
        {
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = padding ?? new RectOffset(0, 0, 0, 0);
            v.childAlignment = align;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return v;
        }

        public static HorizontalLayoutGroup HorizontalList(RectTransform rt, float spacing, RectOffset padding = null,
            TextAnchor align = TextAnchor.MiddleCenter, bool expand = true)
        {
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.padding = padding ?? new RectOffset(0, 0, 0, 0);
            h.childAlignment = align;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = expand;
            h.childForceExpandHeight = true;
            return h;
        }

        public static LayoutElement Layout(Component c, float preferredHeight = -1, float preferredWidth = -1, float flexibleWidth = -1)
        {
            if (!c.TryGetComponent(out LayoutElement le)) le = c.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = preferredHeight;
            le.preferredWidth = preferredWidth;
            le.flexibleWidth = flexibleWidth;
            return le;
        }

        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                var child = t.GetChild(i).gameObject;
                child.SetActive(false);
                UnityEngine.Object.Destroy(child);
            }
        }

        /// <summary>버튼 목록을 위아래(또는 좌우)로 순서대로 이동하도록 명시 내비게이션을 건다.</summary>
        public static void ChainNavigation(Selectable[] items, bool vertical)
        {
            for (int i = 0; i < items.Length; i++)
            {
                var nav = new Navigation { mode = Navigation.Mode.Explicit };
                var prev = items[(i - 1 + items.Length) % items.Length];
                var next = items[(i + 1) % items.Length];
                if (vertical)
                {
                    nav.selectOnUp = prev;
                    nav.selectOnDown = next;
                }
                else
                {
                    nav.selectOnLeft = prev;
                    nav.selectOnRight = next;
                }
                items[i].navigation = nav;
            }
        }
    }
}
