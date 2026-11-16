using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TavlaAI.Board
{
    /// <summary>Faz 5 Bolum 6.3: toplama tepsisi (toplanan tas sayisi).</summary>
    public sealed class TrayView : MonoBehaviour, IPointerClickHandler
    {
        /// <summary>Tepsinin toplama hedefi: Beyaz <c>25</c>, Siyah <c>0</c>.</summary>
        public int Point { get; private set; }

        /// <summary>Tepsi gorseli.</summary>
        public Image Background;

        /// <summary>Sayi etiketi.</summary>
        public Text CountLabel;

        /// <summary>Tiklandi (toplama hedefi: 0 veya 25).</summary>
        public event Action<int> Clicked;

        private Color _baseColor;

        /// <summary>Tepsiyi kurar.</summary>
        public void Initialize(int point)
        {
            Point = point;
            if (Background != null)
            {
                _baseColor = Background.color;
            }
        }

        /// <summary>Toplanan tas sayisini gosterir.</summary>
        public void SetCount(int count)
        {
            if (CountLabel != null)
            {
                CountLabel.text = count > 0 ? count.ToString() : string.Empty;
            }
        }

        /// <summary>Yasal toplama hedefi vurgusu.</summary>
        public void SetHighlight(bool on)
        {
            if (Background == null)
            {
                return;
            }

            Background.color = on ? new Color(0.2f, 0.9f, 0.3f, 0.55f) : _baseColor;
        }

        /// <inheritdoc />
        public void OnPointerClick(PointerEventData eventData)
        {
            Action<int> handler = Clicked;
            if (handler != null)
            {
                handler(Point);
            }
        }
    }
}
