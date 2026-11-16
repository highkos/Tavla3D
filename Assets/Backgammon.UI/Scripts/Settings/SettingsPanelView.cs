using System;
using Backgammon.Core;
using TavlaAI.Infrastructure;
using UnityEngine;
using UnityEngine.UI;

namespace TavlaAI.Settings
{
    /// <summary>
    /// Faz 5 Bolum 6.10: ayarlar paneli. Anahtarlar <c>tavla.</c> onekiyle
    /// <see cref="PlayerPrefs"/>'te saklanir.
    /// </summary>
    public sealed class SettingsPanelView : MonoBehaviour
    {
        /// <summary>Jacoby anahtari.</summary>
        public const string JacobyKey = "tavla.jacoby";

        /// <summary>Olu kup yasagi anahtari.</summary>
        public const string DeadCubeKey = "tavla.deadcube";

        /// <summary>Derinlik anahtari.</summary>
        public const string DepthKey = "tavla.depth";

        /// <summary>Dusunme butcesi anahtari (ms).</summary>
        public const string BudgetKey = "tavla.budgetms";

        /// <summary>Oyuncu rengi anahtari (<c>1</c> = Beyaz, <c>-1</c> = Siyah).</summary>
        public const string ColorKey = "tavla.color";

        /// <summary>Jacoby acik mi?</summary>
        public bool EnableJacoby { get; private set; }

        /// <summary>Olu kup yasagi acik mi?</summary>
        public bool DisallowDeadCube { get; private set; }

        /// <summary>Arama derinligi (0/1/2).</summary>
        public int DepthPly { get; private set; }

        /// <summary>Dusunme butcesi (ms).</summary>
        public int BudgetMilliseconds { get; private set; }

        /// <summary>
        /// Insan oyuncunun rengi: <c>-1</c> = Siyah (varsayilan), <c>1</c> = Beyaz.
        /// </summary>
        /// <remarks>
        /// Varsayilan <b>Siyah</b>'tir: Siyah'in toplama tepsisi boylece <b>sag altta</b> kalir.
        /// Renk degisince yalnizca tepsiler yer degistirir; tahta yerlesimi degismez
        /// (<see cref="TavlaAI.Board.BoardView.HumanTrayBottom"/>).
        /// </remarks>
        public sbyte PlayerColor { get; private set; }

        /// <summary>Ayar degisti.</summary>
        public event Action Changed;

        /// <summary>Panel gorseli.</summary>
        public Image Background { get; private set; }

        /// <summary>Panel gorunur mu?</summary>
        public bool IsVisible
        {
            get { return _content != null && _content.gameObject.activeSelf; }
        }

        private RectTransform _content;
        private Text _summary;

        /// <summary>Gorunumleri kurar ve kayitli ayarlari yukler.</summary>
        public void Initialize()
        {
            var root = (RectTransform)transform;

            // Tum alt ogeler tek bir kapsayicida toplanir; SetVisible yalnizca bu
            // kapsayiciyi kapatir. Aksi halde yalnizca arka plan kapanir, ozet yazisi
            // ve dugmeler ekranda kalirdi.
            _content = UiFactory.CreateRect("Content", root);
            UiFactory.ApplyLayout(_content, 0f, 0f, UiLayout.SidebarWidth, 220f);

            Background = UiFactory.CreateImage("SettingsBackground", _content,
                                               new Color(0.10f, 0.11f, 0.14f, 0.95f));
            UiFactory.ApplyLayout((RectTransform)Background.transform, 0f, 0f,
                                  UiLayout.SidebarWidth, 220f);

            EnableJacoby = PlayerPrefs.GetInt(JacobyKey, 0) != 0;
            DisallowDeadCube = PlayerPrefs.GetInt(DeadCubeKey, 1) != 0;
            DepthPly = Mathf.Clamp(PlayerPrefs.GetInt(DepthKey, 1), 0, 2);
            BudgetMilliseconds = Mathf.Max(0, PlayerPrefs.GetInt(BudgetKey, 2000));
            PlayerColor = PlayerPrefs.GetInt(ColorKey, -1) < 0 ? (sbyte)-1 : (sbyte)1;

            _summary = UiFactory.CreateText("Summary", _content, string.Empty, 18,
                                            TextAnchor.UpperLeft, Color.white);
            UiFactory.ApplyLayout((RectTransform)_summary.transform, 12f, 10f, 328f, 160f);

            // Dort dugme tek satira sigar: 4 * 80 + 3 * 4 = 332 <= 352.
            CreateButton(_content, "ToggleJacoby", "Jacoby", 12f, 178f, ToggleJacoby);
            CreateButton(_content, "ToggleDeadCube", "Olu kup", 96f, 178f, ToggleDeadCube);
            CreateButton(_content, "CycleDepth", "Derinlik", 180f, 178f, CycleDepth);
            CreateButton(_content, "ToggleColor", "Renk", 264f, 178f, ToggleColor);

            RefreshSummary();
            SetVisible(false);
        }

        /// <summary>Paneli acar/kapatir (tum alt ogeler dahil).</summary>
        public void SetVisible(bool visible)
        {
            if (_content != null)
            {
                _content.gameObject.SetActive(visible);
            }
        }

        private void ToggleJacoby()
        {
            EnableJacoby = !EnableJacoby;
            PlayerPrefs.SetInt(JacobyKey, EnableJacoby ? 1 : 0);
            PlayerPrefs.Save();
            RefreshSummary();
            Raise();
        }

        private void ToggleDeadCube()
        {
            DisallowDeadCube = !DisallowDeadCube;
            PlayerPrefs.SetInt(DeadCubeKey, DisallowDeadCube ? 1 : 0);
            PlayerPrefs.Save();
            RefreshSummary();
            Raise();
        }

        private void CycleDepth()
        {
            DepthPly = (DepthPly + 1) % 3;
            PlayerPrefs.SetInt(DepthKey, DepthPly);
            PlayerPrefs.Save();
            RefreshSummary();
            Raise();
        }

        private void ToggleColor()
        {
            PlayerColor = PlayerColor > 0 ? (sbyte)-1 : (sbyte)1;
            PlayerPrefs.SetInt(ColorKey, PlayerColor);
            PlayerPrefs.Save();
            RefreshSummary();
            Raise();
        }

        private void RefreshSummary()
        {
            if (_summary == null)
            {
                return;
            }

            _summary.text =
                "Jacoby            : " + (EnableJacoby ? "acik" : "kapali") + "\n" +
                "Olu kup yasagi    : " + (DisallowDeadCube ? "acik" : "kapali") + "\n" +
                "Derinlik (DepthPly): " + DepthPly + "\n" +
                "Dusunme butcesi   : " + BudgetMilliseconds + " ms\n" +
                "Renk              : " + (PlayerColor > 0 ? "Beyaz" : "Siyah") + "\n" +
                "Dil               : tr-TR";
        }

        private void Raise()
        {
            Action handler = Changed;
            if (handler != null)
            {
                handler();
            }
        }

        private void CreateButton(RectTransform parent, string name, string caption, float x, float y,
                                  Action handler)
        {
            Image image = UiFactory.CreateImage(name, parent, new Color(0.22f, 0.24f, 0.30f, 0.95f));
            UiFactory.ApplyLayout((RectTransform)image.transform, x, y, 80f, 30f);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => handler());

            Text label = UiFactory.CreateText("Label", image.transform, caption, 15,
                                              TextAnchor.MiddleCenter, Color.white);
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.sizeDelta = Vector2.zero;
        }
    }
}
