using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TavlaAI.Board
{
    /// <summary>Faz 5 Bolum 6.4: hane vurgulama turleri.</summary>
    public enum PointHighlight
    {
        /// <summary>Vurgu yok.</summary>
        None = 0,

        /// <summary>Yasal hedef (yarim saydam yesil).</summary>
        LegalTarget = 1,

        /// <summary>Secili kaynak (sari halka).</summary>
        Selected = 2,

        /// <summary>Vurulacak tas (kirmizi halka).</summary>
        Capture = 3
    }

    /// <summary>Faz 5 Bolum 6.4 / 6.8: tek hane; tikla-tikla ve surukle-birak girisini iletir.</summary>
    public sealed class BoardPointView : MonoBehaviour, IPointerClickHandler, IBeginDragHandler,
                                    IDragHandler, IEndDragHandler
    {
        /// <summary>Tahta hanesi (1..24).</summary>
        public int Point { get; private set; }

        /// <summary>Dikdortgen.</summary>
        public RectTransform Rect { get; private set; }

        /// <summary>Hane ucgeni gorseli.</summary>
        public Image Face { get; private set; }

        /// <summary>Vurgu gorseli.</summary>
        public Image Highlight { get; private set; }

        /// <summary>Tas sayisi etiketi.</summary>
        public Text CountLabel { get; private set; }

        /// <summary>Tiklandi.</summary>
        public event Action<int> Clicked;

        /// <summary>Surukleme basladi.</summary>
        public event Action<int> DragStarted;

        /// <summary>Surukleniyor (ekran konumu).</summary>
        public event Action<int, Vector2> Dragged;

        /// <summary>Birakildi (ekran konumu).</summary>
        public event Action<int, Vector2> DragEnded;

        /// <summary>Haneyi kurar.</summary>
        public void Initialize(int point, RectTransform rect, Image face, Image highlight, Text countLabel)
        {
            Point = point;
            Rect = rect;
            Face = face;
            Highlight = highlight;
            CountLabel = countLabel;
            SetHighlight(PointHighlight.None);
        }

        /// <summary>
        /// Ucgenin yonunu ayarlar.
        /// </summary>
        /// <param name="pointDown">
        /// <c>true</c> ise ucgen asagi bakar (ust satir), <c>false</c> ise yukari bakar (alt satir).
        /// </param>
        /// <remarks>
        /// Yon <b>hane numarasindan degil satirdan</b> turetilmelidir. Eskiden <c>point &gt; 12</c>
        /// kontrolu kullaniliyordu; dikey aynalamada alt satir 13..24 oldugu icin tum ucgenler
        /// ters yone bakiyor ve tahta "karman corman" gorunuyordu.
        /// </remarks>
        public void SetFaceFlipped(bool pointDown)
        {
            if (Face == null)
            {
                return;
            }

            Face.rectTransform.localScale = pointDown
                ? new Vector3(1f, -1f, 1f)
                : Vector3.one;
        }

        /// <summary>
        /// Ucgen rengini ayarlar.
        /// </summary>
        /// <remarks>
        /// Renk <b>gorsel yuvaya</b> gore verilir (hane numarasina gore degil). Boylece tahta
        /// aynalandiginda acik/koyu ucgen deseni ayni kalir; yalnizca hangi hanenin nereye
        /// cizildigi degisir.
        /// </remarks>
        public void SetFaceColor(Color color)
        {
            if (Face != null)
            {
                Face.color = color;
            }
        }

        /// <summary>Vurguyu ayarlar.</summary>
        public void SetHighlight(PointHighlight kind)
        {
            if (Highlight == null)
            {
                return;
            }

            switch (kind)
            {
                case PointHighlight.LegalTarget:
                    Highlight.enabled = true;
                    Highlight.color = new Color(0.2f, 0.9f, 0.3f, 0.35f);
                    break;
                case PointHighlight.Selected:
                    Highlight.enabled = true;
                    Highlight.color = new Color(1f, 0.85f, 0.1f, 0.55f);
                    break;
                case PointHighlight.Capture:
                    Highlight.enabled = true;
                    Highlight.color = new Color(0.95f, 0.2f, 0.2f, 0.55f);
                    break;
                default:
                    Highlight.enabled = false;
                    break;
            }
        }

        /// <summary>Tas sayisi etiketini ayarlar (<c>0</c> = gizli).</summary>
        public void SetCount(int count)
        {
            if (CountLabel == null)
            {
                return;
            }

            bool show = count > 5;
            CountLabel.enabled = show;
            if (show)
            {
                CountLabel.text = count.ToString();
            }
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

        /// <inheritdoc />
        public void OnBeginDrag(PointerEventData eventData)
        {
            Action<int> handler = DragStarted;
            if (handler != null)
            {
                handler(Point);
            }
        }

        /// <inheritdoc />
        public void OnDrag(PointerEventData eventData)
        {
            Action<int, Vector2> handler = Dragged;
            if (handler != null)
            {
                handler(Point, eventData.position);
            }
        }

        /// <inheritdoc />
        public void OnEndDrag(PointerEventData eventData)
        {
            Action<int, Vector2> handler = DragEnded;
            if (handler != null)
            {
                handler(Point, eventData.position);
            }
        }
    }
}
