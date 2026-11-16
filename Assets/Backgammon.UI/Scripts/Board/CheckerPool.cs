using System;
using UnityEngine;
using UnityEngine.UI;

namespace TavlaAI.Board
{
    /// <summary>
    /// Faz 5 Bolum 6.4: 30 taslik sabit havuz. Kare icinde <c>Instantiate</c>/<c>Destroy</c>
    /// <b>cagrilmaz</b> (Bolum 8, Test 40).
    /// </summary>
    public sealed class CheckerPool
    {
        /// <summary>Havuz kapasitesi (15 + 15).</summary>
        public const int Capacity = 30;

        private readonly CheckerView[] _checkers = new CheckerView[Capacity];
        private int _visible;

        /// <summary>Havuzun kurulmasindan bu yana yapilan <c>Instantiate</c> cagrisi sayisi.</summary>
        public static int InstantiateCalls { get; private set; }

        /// <summary>Sayaci sifirlar (testler icin).</summary>
        public static void ResetInstantiateCounter()
        {
            InstantiateCalls = 0;
        }

        /// <summary>Havuzu kurar; <b>yalnizca</b> burada <c>Instantiate</c> cagrilir.</summary>
        public void Initialize(RectTransform parent)
        {
            Initialize(parent, null, null);
        }

        /// <summary>Havuzu pul sprite'lariyla kurar.</summary>
        public void Initialize(RectTransform parent, Sprite light, Sprite dark)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            var template = new GameObject("CheckerTemplate", typeof(RectTransform), typeof(Image), typeof(CheckerView));
            var templateRect = (RectTransform)template.transform;
            templateRect.SetParent(parent, false);
            templateRect.sizeDelta = new Vector2(32f, 32f);
            var templateImage = template.GetComponent<Image>();
            templateImage.raycastTarget = false;
            templateImage.color = Color.white;
            templateImage.sprite = light;
            template.SetActive(false);

            for (int i = 0; i < Capacity; i++)
            {
                var go = UnityEngine.Object.Instantiate(template, parent);
                InstantiateCalls++;
                go.name = "Checker_" + i.ToString("D2");
                go.SetActive(true);
                var view = go.GetComponent<CheckerView>();
                view.Image = go.GetComponent<Image>();
                view.Rect = (RectTransform)go.transform;
                view.SetVisible(false);
                _checkers[i] = view;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(template);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(template);
            }
        }

        /// <summary>Gorunur tas sayisi.</summary>
        public int VisibleCount
        {
            get { return _visible; }
        }

        /// <summary>Dizindeki tas.</summary>
        public CheckerView this[int index]
        {
            get { return _checkers[index]; }
        }

        /// <summary>Kare basi: tum taslari gizler.</summary>
        public void HideAll()
        {
            for (int i = 0; i < _visible; i++)
            {
                _checkers[i].SetVisible(false);
            }

            _visible = 0;
        }

        /// <summary>Kare basi: bir tasi yerlestirir ve gorunur yapar.</summary>
        public void Place(Color color, float x, float y, float size)
        {
            Place(null, color, x, y, size);
        }

        /// <summary>Kare basi: sprite'li bir tasi yerlestirir ve gorunur yapar.</summary>
        public void Place(Sprite sprite, Color color, float x, float y, float size)
        {
            if (_visible >= Capacity)
            {
                return;
            }

            CheckerView view = _checkers[_visible];
            view.SetSprite(sprite);
            view.SetColor(color);
            view.SetPlacement(x, y, size);
            view.SetVisible(true);
            _visible++;
        }
    }
}
