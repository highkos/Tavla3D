using UnityEngine;
using UnityEngine.UI;

namespace TavlaAI.Board
{
    /// <summary>Faz 5 Bolum 6.4: havuzlanmis tek bir tas gorseli.</summary>
    public sealed class CheckerView : MonoBehaviour
    {
        /// <summary>Gorsel.</summary>
        public Image Image;

        /// <summary>Dikdortgen.</summary>
        public RectTransform Rect;

        /// <summary>Gorunurluk.</summary>
        public void SetVisible(bool visible)
        {
            if (Image != null && Image.enabled != visible)
            {
                Image.enabled = visible;
            }
        }

        /// <summary>Sprite (pul gorseli).</summary>
        public void SetSprite(Sprite sprite)
        {
            if (Image != null && sprite != null && Image.sprite != sprite)
            {
                Image.sprite = sprite;
            }
        }

        /// <summary>Renk.</summary>
        public void SetColor(Color color)
        {
            if (Image != null)
            {
                Image.color = color;
            }
        }

        /// <summary>Konum ve boyut (tahta koordinatinda, sol-ust koken).</summary>
        public void SetPlacement(float x, float y, float size)
        {
            if (Rect == null)
            {
                return;
            }

            Rect.anchorMin = new Vector2(0f, 1f);
            Rect.anchorMax = new Vector2(0f, 1f);
            Rect.pivot = new Vector2(0.5f, 0.5f);
            Rect.anchoredPosition = new Vector2(x, -y);
            Rect.sizeDelta = new Vector2(size, size);
        }
    }
}
