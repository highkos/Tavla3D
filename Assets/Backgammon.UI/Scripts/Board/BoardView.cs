using System;
using System.Collections.Generic;
using Backgammon.UI;
using TavlaAI.Infrastructure;
using UnityEngine;
using UnityEngine.UI;

namespace TavlaAI.Board
{
    /// <summary>
    /// Faz 5 Bolum 6.4: tahtayi <see cref="BoardViewModel"/>'den cizer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Hane dikdortgenleri <see cref="BoardLayout.Calculate"/> ile <b>hesaplanir</b>; sahne
    /// dosyasinda elle yerlestirilmis hane yoktur. Boylece yerlesim Unity'siz test edilebilir
    /// (Bolum 8, Test 38).
    /// </para>
    /// <para>
    /// Taslar <see cref="CheckerPool"/>'dan gelir; kare icinde <c>Instantiate</c> cagrilmaz
    /// (Bolum 8, Test 40).
    /// </para>
    /// </remarks>
    public sealed class BoardView : MonoBehaviour
    {
        /// <summary>Beyaz tas rengi.</summary>
        public static readonly Color WhiteColor = new Color(0.96f, 0.95f, 0.90f, 1f);

        /// <summary>Siyah tas rengi.</summary>
        public static readonly Color BlackColor = new Color(0.16f, 0.17f, 0.20f, 1f);

        private static readonly Color PointLight = new Color(0.96f, 0.92f, 0.80f, 0.98f);

        /// <summary>
        /// Koyu hane yuzeyi.
        /// </summary>
        /// <remarks>
        /// Once <c>0.33, 0.11, 0.10</c> idi; parlakligi (~0,16) Siyah pulun govdesiyle (~0,17)
        /// neredeyse ayniydi ve koyu hanede vuran Siyah pul <b>gorunmuyordu</b>. Ton biraz
        /// koyultuldu: hem acik hane ile kontrast artti, hem de Siyah pulun govdesi ayrildi.
        /// </remarks>
        private static readonly Color PointDark = new Color(0.26f, 0.08f, 0.07f, 0.98f);

        /// <summary>
        /// Bar (orta serit) rengi: opak, acik sicak ahsap (tahta cercevesi ailesi).
        /// </summary>
        /// <remarks>
        /// <para>
        /// Once neredeyse siyahti (<c>0.11, 0.08, 0.06</c>); oyun alanindan ayriliyordu ama
        /// <b>Siyah pul bar uzerinde kayboluyordu</b>. Tahta zemini koyu ahsap
        /// (parlaklik ~0,35) oldugu icin orta ton da yeterli degildi. Acik ahsap tonu hem
        /// Beyaz hem Siyah puldan net ayrilir, hem de koyu zeminden ayrilir.
        /// </para>
        /// </remarks>
        private static readonly Color BarColor = new Color(0.70f, 0.56f, 0.40f, 0.98f);

        /// <summary>
        /// Toplama tepsisi rengi: bar ile <b>ayni</b> acik ahsap tonu.
        /// </summary>
        /// <remarks>
        /// Kullanici toplama alaninin da kirik taslarin durdugu orta alan (bar) gibi
        /// gorunmesini istedi; bu yuzden renk ve doku <see cref="BarColor"/> ile birebir
        /// aynidir. Tepsinin genisligi ve yuksekligi degismez.
        /// </remarks>
        private static readonly Color TrayColor = BarColor;

        private readonly BoardPointView[] _points = new BoardPointView[BoardLayout.PointCount];
        private readonly List<int> _legalTargets = new List<int>(8);
        private readonly List<int> _captures = new List<int>(4);
        private BoardLayout _layout;
        private RectTransform _root;
        private CheckerPool _pool;
        private BoardViewModel _model;
        private int _selectedPoint = -1;
        private float _width;
        private float _height;
        private bool _humanTrayBottom;
        private bool _layoutTrayBottom;
        private bool _flipPoints;
        private bool _layoutFlip;
        private LayoutRect _whiteBarRect;
        private LayoutRect _blackBarRect;

        /// <summary>
        /// Insan oyuncunun toplama tepsisi alt yarida mi?
        /// </summary>
        /// <remarks>
        /// <para>
        /// Insan Siyah oynarken <c>true</c> yapilir; boylece oyuncunun toplama tepsisi her zaman
        /// <b>sag altta</b> kalir (Bolum 6.4).
        /// </para>
        /// <para>
        /// <b>Hane yerlesimi degismez.</b> Yalnizca tepsi yarimlari takas edilir; tahtayi dikey
        /// aynalamak renk degisiminde tahtayi taninmaz hale getiriyordu.
        /// </para>
        /// </remarks>
        public bool HumanTrayBottom
        {
            get { return _humanTrayBottom; }
            set
            {
                if (_humanTrayBottom == value)
                {
                    return;
                }

                _humanTrayBottom = value;
                _width = 0f;
                _height = 0f;
            }
        }

        /// <summary>
        /// Hane numaralari oyuncunun bakisina gore 180 derece dondurulsun mu?
        /// </summary>
        /// <remarks>
        /// <para>
        /// Insan <b>Beyaz</b> oynarken <c>true</c> yapilir. Motor Beyaz'i <c>1 -&gt; 24</c>
        /// yonunde hareket ettirir; tahta dondurulmezse oyuncunun taslari Siyah oynarken
        /// gittigi yonun <b>tersine</b> gidiyor gibi gorunur.
        /// </para>
        /// <para>
        /// <b>Degismez:</b> tahta cercevesi, bar ve tepsi seridi yer degistirmez; yalnizca
        /// hane numaralari dondurulur. Ucgen rengi <b>gorsel yuvaya</b> gore secildigi icin
        /// acik/koyu desen de ayni kalir.
        /// </para>
        /// </remarks>
        public bool FlipPoints
        {
            get { return _flipPoints; }
            set
            {
                if (_flipPoints == value)
                {
                    return;
                }

                _flipPoints = value;
                _width = 0f;
                _height = 0f;
            }
        }

        /// <summary>Havuz.</summary>
        public CheckerPool Pool
        {
            get { return _pool; }
        }

        /// <summary>Hane gorunumleri; <c>Points[i]</c> = hane <c>i + 1</c>.</summary>
        public BoardPointView[] Points
        {
            get { return _points; }
        }

        /// <summary>Beyaz bar.</summary>
        public BarView WhiteBar { get; private set; }

        /// <summary>Siyah bar.</summary>
        public BarView BlackBar { get; private set; }

        /// <summary>Beyaz toplama tepsisi.</summary>
        public TrayView WhiteTray { get; private set; }

        /// <summary>Siyah toplama tepsisi.</summary>
        public TrayView BlackTray { get; private set; }

        /// <summary>Hane tiklandi (1..24).</summary>
        public event Action<int> PointClicked;

        /// <summary>Hane uzerinde surukleme basladi.</summary>
        public event Action<int> PointDragStarted;

        /// <summary>Hane uzerinde surukleniyor.</summary>
        public event Action<int, Vector2> PointDragged;

        /// <summary>Hane uzerinde birakildi.</summary>
        public event Action<int, Vector2> PointDragEnded;

        /// <summary>Tepsi tiklandi (toplama hedefi: 0 veya 25).</summary>
        public event Action<int> TrayClicked;

        /// <summary>Tahtayi kurar (bir kez).</summary>
        public void Initialize(BoardViewModel model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            _model = model;
            _root = (RectTransform)transform;

            var background = GetComponent<Image>();
            if (background != null)
            {
                background.sprite = SpriteLibrary.Board;
                background.type = Image.Type.Simple;
                background.color = Color.white;
            }

            for (int point = 1; point <= BoardLayout.PointCount; point++)
            {
                RectTransform rect = UiFactory.CreateRect("Point_" + point.ToString("D2"), _root);
                var hit = rect.gameObject.AddComponent<Image>();
                hit.color = new Color(0f, 0f, 0f, 0f);
                hit.raycastTarget = true;

                Image face = UiFactory.CreateImage("Face", rect, PointColor(point), SpriteLibrary.Point);
                Stretch((RectTransform)face.transform);

                Image highlight = UiFactory.CreateImage("Highlight", rect, new Color(0f, 0f, 0f, 0f));
                highlight.enabled = false;
                Stretch((RectTransform)highlight.transform);

                Text label = UiFactory.CreateText("Count", rect, string.Empty, 18,
                                                  TextAnchor.MiddleCenter, Color.white);
                label.enabled = false;
                Stretch((RectTransform)label.transform);

                var view = rect.gameObject.AddComponent<BoardPointView>();
                view.Initialize(point, rect, face, highlight, label);
                view.Clicked += OnPointClicked;
                view.DragStarted += OnPointDragStarted;
                view.Dragged += OnPointDragged;
                view.DragEnded += OnPointDragEnded;
                _points[point - 1] = view;
            }

            WhiteBar = CreateBar("BarWhite", 0);
            BlackBar = CreateBar("BarBlack", 25);
            WhiteTray = CreateTray("TrayWhite", 25);
            BlackTray = CreateTray("TrayBlack", 0);

            // Havuz en son kurulur: uGUI'de son kardes ustte cizilir, pullar hane
            // ucgenlerinin ve bar/tepsinin uzerinde kalmali.
            _pool = new CheckerPool();
            _pool.Initialize(_root, SpriteLibrary.CheckerWhite, SpriteLibrary.CheckerBlack);

            // Tas sayisi etiketleri havuzdan SONRA cizilmelidir. Hane dikdortgeninin
            // cocugu kaldiklari surece pullarin altinda kaliyor ve yigin sayisi
            // okunamadigi icin "tas eksildi" izlenimi doguyordu.
            for (int point = 1; point <= BoardLayout.PointCount; point++)
            {
                Text label = _points[point - 1].CountLabel;
                if (label == null)
                {
                    continue;
                }

                label.transform.SetParent(_root, false);
                var outline = label.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
                outline.effectDistance = new Vector2(1.5f, -1.5f);
            }

            Recalculate();
            Refresh();
        }

        /// <summary>Secili haneyi ve yasal hedefleri vurgular.</summary>
        public void SetSelection(int selectedPoint, IReadOnlyList<int> legalTargets,
                                 IReadOnlyList<int> captures)
        {
            _selectedPoint = selectedPoint;

            _legalTargets.Clear();
            if (legalTargets != null)
            {
                for (int i = 0; i < legalTargets.Count; i++)
                {
                    _legalTargets.Add(legalTargets[i]);
                }
            }

            _captures.Clear();
            if (captures != null)
            {
                for (int i = 0; i < captures.Count; i++)
                {
                    _captures.Add(captures[i]);
                }
            }

            ApplyHighlights();
        }

        /// <summary>
        /// Tahta boyutu degistiginde (pencere yeniden boyutlandirma) yerlesimi tazeler.
        /// </summary>
        /// <remarks>
        /// <see cref="BoardFitter"/> tahtayi en-boy oranini koruyarak sigdirir; boyut
        /// degistiginde nokta/cubuk/tepsi dikdortgenleri yeniden hesaplanmalidir.
        /// </remarks>
        private void LateUpdate()
        {
            if (_model == null || _root == null)
            {
                return;
            }

            if (!Mathf.Approximately(_root.rect.width, _width) ||
                !Mathf.Approximately(_root.rect.height, _height))
            {
                Refresh();
            }
        }

        /// <summary>View-model'den yeniden cizer.</summary>
        public void Refresh()
        {
            if (_model == null)
            {
                return;
            }

            Recalculate();
            _pool.HideAll();

            for (int point = 1; point <= BoardLayout.PointCount; point++)
            {
                BoardPointView view = _points[point - 1];
                Backgammon.UI.PointView data = _model.PointAt(point);
                view.SetCount(data.Count);
                StackCheckers(point, data.Owner, data.Count);
            }

            StackBar(_whiteBarRect, _model.WhiteBar.Count, SpriteLibrary.CheckerWhite);
            StackBar(_blackBarRect, _model.BlackBar.Count, SpriteLibrary.CheckerBlack);

            if (WhiteBar != null)
            {
                WhiteBar.SetCount(_model.WhiteBar.Count);
            }

            if (BlackBar != null)
            {
                BlackBar.SetCount(_model.BlackBar.Count);
            }

            if (WhiteTray != null)
            {
                WhiteTray.SetCount(_model.WhiteBorneOff);
            }

            if (BlackTray != null)
            {
                BlackTray.SetCount(_model.BlackBorneOff);
            }

            StackTray(_layout.WhiteTray, _model.WhiteBorneOff, SpriteLibrary.CheckerWhite);
            StackTray(_layout.BlackTray, _model.BlackBorneOff, SpriteLibrary.CheckerBlack);

            ApplyHighlights();
        }

        private void Recalculate()
        {
            float width = _root.rect.width;
            float height = _root.rect.height;
            if (width <= 0f || height <= 0f)
            {
                width = 1920f;
                height = 1080f;
            }

            if (Mathf.Approximately(width, _width) && Mathf.Approximately(height, _height) &&
                _layoutTrayBottom == _humanTrayBottom && _layoutFlip == _flipPoints)
            {
                return;
            }

            _width = width;
            _height = height;
            _layoutTrayBottom = _humanTrayBottom;
            _layoutFlip = _flipPoints;
            _layout = BoardLayout.Calculate(width, height, _humanTrayBottom, _flipPoints);

            for (int point = 1; point <= BoardLayout.PointCount; point++)
            {
                LayoutRect r = _layout.PointAt(point);
                UiFactory.ApplyLayout(_points[point - 1].Rect, r.X, r.Y, r.Width, r.Height);

                // Ucgen yonu satirdan turetilir: ust satir asagi, alt satir yukari bakar.
                bool topRow = r.Y < _layout.Board.Height * 0.5f;
                _points[point - 1].SetFaceFlipped(topRow);

                // Renk gorsel yuvaya gore: aynalamada acik/koyu desen bozulmaz.
                _points[point - 1].SetFaceColor(PointColor(_layout.VisualSlot(point)));

                Text label = _points[point - 1].CountLabel;
                if (label != null)
                {
                    UiFactory.ApplyLayout((RectTransform)label.transform, r.X, r.Y, r.Width, r.Height);
                }
            }

            // Bar yarimlari ev bolgelerini izler: her oyuncunun kirik taslari kendi ev
            // bolgesine yakin yarimda birikir. Ev bolgesi 19..24 olan Beyaz icin yarim,
            // aynalamaya gore ust veya alt olur.
            float halfBar = _layout.Bar.Height * 0.5f;
            bool whiteBarBottom = _layout.PointAt(19).Y >= _layout.Board.Height * 0.5f;
            _whiteBarRect = new LayoutRect(_layout.Bar.X,
                                           whiteBarBottom ? _layout.Bar.Y + halfBar : _layout.Bar.Y,
                                           _layout.Bar.Width, halfBar);
            _blackBarRect = new LayoutRect(_layout.Bar.X,
                                           whiteBarBottom ? _layout.Bar.Y : _layout.Bar.Y + halfBar,
                                           _layout.Bar.Width, halfBar);
            ApplyBarLayout(WhiteBar, _whiteBarRect);
            ApplyBarLayout(BlackBar, _blackBarRect);
            ApplyTrayLayout(WhiteTray, _layout.WhiteTray);
            ApplyTrayLayout(BlackTray, _layout.BlackTray);
        }

        private void ApplyBarLayout(BarView bar, LayoutRect rect)
        {
            if (bar == null)
            {
                return;
            }

            UiFactory.ApplyLayout((RectTransform)bar.transform, rect.X, rect.Y, rect.Width, rect.Height);
        }

        private void ApplyTrayLayout(TrayView tray, LayoutRect rect)
        {
            if (tray == null)
            {
                return;
            }

            UiFactory.ApplyLayout((RectTransform)tray.transform, rect.X, rect.Y, rect.Width, rect.Height);
        }

        private void StackCheckers(int point, sbyte owner, int count)
        {
            if (count <= 0 || owner == 0)
            {
                return;
            }

            LayoutRect rect = _layout.PointAt(point);

            // Satir, hanenin gercek dikdortgeninden turetilir. Hane numarasindan turetmek
            // (point <= 12) dikey aynalamada yanlistir: aynalanmis tahtada alt satir 13..24'tur
            // ve pullar hanenin ters kenarindan buyuyerek tasardi.
            bool bottomRow = rect.Y >= _layout.Board.Height * 0.5f;
            float size = Mathf.Min(rect.Width, rect.Height / 6f) * 0.92f;
            float step = count > 1
                ? Mathf.Min(size, (rect.Height - size) / (count - 1))
                : 0f;

            Sprite sprite = owner > 0 ? SpriteLibrary.CheckerWhite : SpriteLibrary.CheckerBlack;
            float centerX = rect.X + (rect.Width * 0.5f);

            for (int i = 0; i < count; i++)
            {
                float centerY = bottomRow
                    ? rect.Y + rect.Height - (size * 0.5f) - (i * step)
                    : rect.Y + (size * 0.5f) + (i * step);
                _pool.Place(sprite, Color.white, centerX, centerY, size);
            }
        }

        /// <summary>
        /// Barda bekleyen taslari cizer.
        /// </summary>
        /// <param name="rect">Bar'in <b>kendi</b> yarim dikdortgeni (ust veya alt yari).</param>
        /// <param name="count">Tas sayisi.</param>
        /// <param name="sprite">Tas gorseli.</param>
        /// <remarks>
        /// Buyume yonu <b>dikdortgenin kendi konumundan</b> turetilir; renge gore sabitlenirse
        /// dikey aynalamada taslar bar'in ters kenarindan buyur.
        /// </remarks>
        private void StackBar(LayoutRect rect, int count, Sprite sprite)
        {
            if (count <= 0 || rect.Width <= 0f || rect.Height <= 0f)
            {
                return;
            }

            bool fromBottom = rect.Y >= _layout.Board.Height * 0.5f;
            float size = Mathf.Min(rect.Width, rect.Height / 6f) * 0.92f;
            float step = count > 1 ? Mathf.Min(size, (rect.Height - size) / (count - 1)) : 0f;
            float centerX = rect.X + (rect.Width * 0.5f);

            for (int i = 0; i < count; i++)
            {
                float centerY = fromBottom
                    ? rect.Y + rect.Height - (size * 0.5f) - (i * step)
                    : rect.Y + (size * 0.5f) + (i * step);
                _pool.Place(sprite, Color.white, centerX, centerY, size);
            }
        }

        /// <summary>
        /// Toplanan taslari tepsiye cizer.
        /// </summary>
        /// <remarks>
        /// Tepsi seridi dardir (<c>genislik * 0.08</c>); 15 tasi tek kolonda ust uste
        /// dizmek yerine iki kolonlu izgara kullanilir ki hepsi gorunur kalsin.
        /// </remarks>
        private void StackTray(LayoutRect rect, int count, Sprite sprite)
        {
            if (count <= 0 || rect.Width <= 0f || rect.Height <= 0f)
            {
                return;
            }

            const int Columns = 2;
            int rows = (count + Columns - 1) / Columns;
            float size = Mathf.Min(rect.Width / Columns, rect.Height / rows) * 0.88f;
            float cellWidth = rect.Width / Columns;
            float cellHeight = rect.Height / rows;

            for (int i = 0; i < count; i++)
            {
                int column = i % Columns;
                int row = i / Columns;
                float centerX = rect.X + (cellWidth * (column + 0.5f));
                float centerY = rect.Y + (cellHeight * (row + 0.5f));
                _pool.Place(sprite, Color.white, centerX, centerY, size);
            }
        }

        private void ApplyHighlights()
        {
            for (int point = 1; point <= BoardLayout.PointCount; point++)
            {
                BoardPointView view = _points[point - 1];
                if (point == _selectedPoint)
                {
                    view.SetHighlight(PointHighlight.Selected);
                }
                else if (_captures.Contains(point))
                {
                    view.SetHighlight(PointHighlight.Capture);
                }
                else if (_legalTargets.Contains(point))
                {
                    view.SetHighlight(PointHighlight.LegalTarget);
                }
                else
                {
                    view.SetHighlight(PointHighlight.None);
                }
            }

            if (WhiteBar != null)
            {
                WhiteBar.SetHighlight(_selectedPoint == 0);
            }

            if (BlackBar != null)
            {
                BlackBar.SetHighlight(_selectedPoint == 25);
            }

            if (WhiteTray != null)
            {
                WhiteTray.SetHighlight(_legalTargets.Contains(25));
            }

            if (BlackTray != null)
            {
                BlackTray.SetHighlight(_legalTargets.Contains(0));
            }
        }

        private BarView CreateBar(string name, int point)
        {
            Image image = UiFactory.CreateImage(name, _root, BarColor, SpriteLibrary.Bar);
            image.raycastTarget = true;
            var bar = image.gameObject.AddComponent<BarView>();
            bar.Background = image;
            bar.CountLabel = UiFactory.CreateText("Count", image.transform, string.Empty, 20,
                                                  TextAnchor.MiddleCenter, Color.white);
            Stretch((RectTransform)bar.CountLabel.transform);
            bar.Initialize(point);
            bar.Clicked += OnPointClicked;
            return bar;
        }

        private TrayView CreateTray(string name, int point)
        {
            Image image = UiFactory.CreateImage(name, _root, TrayColor, SpriteLibrary.Tray);
            image.raycastTarget = true;
            var tray = image.gameObject.AddComponent<TrayView>();
            tray.Background = image;
            tray.CountLabel = UiFactory.CreateText("Count", image.transform, string.Empty, 20,
                                                   TextAnchor.MiddleCenter, Color.white);
            Stretch((RectTransform)tray.CountLabel.transform);
            tray.Initialize(point);
            tray.Clicked += OnTrayClicked;
            return tray;
        }

        private static Color PointColor(int point)
        {
            return (point % 2 == 1) ? PointLight : PointDark;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }

        private void OnPointClicked(int point)
        {
            Action<int> handler = PointClicked;
            if (handler != null)
            {
                handler(point);
            }
        }

        private void OnPointDragStarted(int point)
        {
            Action<int> handler = PointDragStarted;
            if (handler != null)
            {
                handler(point);
            }
        }

        private void OnPointDragged(int point, Vector2 position)
        {
            Action<int, Vector2> handler = PointDragged;
            if (handler != null)
            {
                handler(point, position);
            }
        }

        private void OnPointDragEnded(int point, Vector2 position)
        {
            Action<int, Vector2> handler = PointDragEnded;
            if (handler != null)
            {
                handler(point, position);
            }
        }

        private void OnTrayClicked(int point)
        {
            Action<int> handler = TrayClicked;
            if (handler != null)
            {
                handler(point);
            }
        }
    }
}
