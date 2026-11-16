using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TavlaAI.Board
{
    /// <summary>Faz 5 Bolum 6.3: bar gorseli (barda bekleyen tas sayisi).</summary>
    public sealed class BarView : MonoBehaviour, IPointerClickHandler
    {
        /// <summary>Bar'in temsil ettigi hane: Beyaz <c>0</c>, Siyah <c>25</c>.</summary>
        public int Point { get; private set; }

        /// <summary>Bar gorseli.</summary>
        public Image Background;

        /// <summary>Sayi etiketi.</summary>
        public Text CountLabel;

        /// <summary>Tiklandi (bar hanesi: 0 veya 25).</summary>
        public event Action<int> Clicked;

        private Color _baseColor;

        /// <summary>Bari kurar.</summary>
        public void Initialize(int point)
        {
            Point = point;
            if (Background != null)
            {
                _baseColor = Background.color;
            }
        }

        /// <summary>Bekleyen tas sayisini gosterir.</summary>
        public void SetCount(int count)
        {
            if (CountLabel != null)
            {
                CountLabel.text = count > 0 ? count.ToString() : string.Empty;
            }
        }

        /// <summary>Secili bar vurgusu.</summary>
        public void SetHighlight(bool on)
        {
            if (Background == null)
            {
                return;
            }

            Background.color = on ? new Color(1f, 0.85f, 0.1f, 0.75f) : _baseColor;
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
