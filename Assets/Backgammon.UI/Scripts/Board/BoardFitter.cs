using TavlaAI.Infrastructure;
using UnityEngine;

namespace TavlaAI.Board
{
    /// <summary>
    /// Tahtayi ebeveyn alanina <b>en-boy oranini koruyarak</b> sigdirir.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Tahta sabit 16:10 oranini korur; pencere orani degistiginde uzamaz veya kirpilmaz.
    /// Kenar cubugu tahtanin saginda kaldigi icin hicbir denetim oyun alanini kapatmaz.
    /// </para>
    /// <para>
    /// <see cref="AspectRatioFitter"/> yerine bu bilesen kullanilir: boylece boyut hesabi
    /// acik ve test edilebilir kalir, ayrica <see cref="BoardView"/> ile ayni kare icinde
    /// guncellenir.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(RectTransform))]
    [DefaultExecutionOrder(-200)]
    public sealed class BoardFitter : MonoBehaviour
    {
        /// <summary>Tahta en-boy orani (genislik / yukseklik).</summary>
        public float Aspect = UiLayout.BoardAspect;

        private RectTransform _rect;
        private RectTransform _parent;
        private Vector2 _appliedSize = new Vector2(-1f, -1f);

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _parent = _rect.parent as RectTransform;
            Apply();
        }

        private void LateUpdate()
        {
            Apply();
        }

        private void Apply()
        {
            if (_rect == null || _parent == null || Aspect <= 0f)
            {
                return;
            }

            Vector2 available = _parent.rect.size;
            if (available.x <= 0f || available.y <= 0f)
            {
                return;
            }

            float width = Mathf.Min(available.x, available.y * Aspect);
            var size = new Vector2(Mathf.Floor(width), Mathf.Floor(width / Aspect));
            if (size == _appliedSize)
            {
                return;
            }

            _appliedSize = size;
            _rect.anchorMin = new Vector2(0.5f, 0.5f);
            _rect.anchorMax = new Vector2(0.5f, 0.5f);
            _rect.pivot = new Vector2(0.5f, 0.5f);
            _rect.anchoredPosition = Vector2.zero;
            _rect.sizeDelta = size;
        }
    }
}
