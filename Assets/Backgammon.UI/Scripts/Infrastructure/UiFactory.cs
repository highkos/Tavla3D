using UnityEngine;
using UnityEngine.UI;

namespace TavlaAI.Infrastructure
{
    /// <summary>Faz 5 Bolum 6.3: sahne ogelerini <b>kodla</b> kuran yardimcilar.</summary>
    /// <remarks>
    /// Yer tutucu gorseller duz renkli <see cref="Image"/> olarak uretilir (Bolum 6.1:
    /// "yer tutucu duz renkler kabul"). Yazi icin Unity'nin yerlesik yazi tipi kullanilir.
    /// </remarks>
    public static class UiFactory
    {
        private static Font _font;

        /// <summary>Yerlesik yazi tipi (Unity 6: <c>LegacyRuntime.ttf</c>).</summary>
        public static Font Font
        {
            get
            {
                if (_font == null)
                {
                    _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    if (_font == null)
                    {
                        _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                    }
                }

                return _font;
            }
        }

        /// <summary>Bos bir <see cref="RectTransform"/> dondurur.</summary>
        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;
            return rect;
        }

        /// <summary>Sag ust koseye bagli bir <see cref="RectTransform"/> dondurur.</summary>
        /// <remarks>
        /// Kenar cubugu ogeleri sag kenara baglanir; boylece pencere orani degistiginde
        /// cubuk ekran disina tasmaz.
        /// </remarks>
        public static RectTransform CreateRightAnchored(string name, Transform parent, float rightMargin,
                                                        float y, float width, float height)
        {
            RectTransform rect = CreateRect(name, parent);
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-rightMargin, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        /// <summary>Duz renkli gorsel dondurur.</summary>
        public static Image CreateImage(string name, Transform parent, Color color)
        {
            RectTransform rect = CreateRect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Sprite'li gorsel dondurur.</summary>
        public static Image CreateImage(string name, Transform parent, Color color, Sprite sprite)
        {
            Image image = CreateImage(name, parent, color);
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Simple;
                image.preserveAspect = false;
            }

            return image;
        }

        /// <summary>Yazi dondurur.</summary>
        public static Text CreateText(string name, Transform parent, string content, int size,
                                      TextAnchor anchor, Color color)
        {
            RectTransform rect = CreateRect(name, parent);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = Font;
            text.text = content;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        /// <summary>Dikdortgeni <c>BoardLayout</c> koordinatindan Unity koordinatina uygular.</summary>
        public static void ApplyLayout(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        /// <summary>Rengi saydamliga dogru karistirir.</summary>
        public static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
