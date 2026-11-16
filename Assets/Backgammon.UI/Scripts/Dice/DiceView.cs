using Backgammon.UI;
using TavlaAI.Infrastructure;
using UnityEngine;
using UnityEngine.UI;

namespace TavlaAI.Dice
{
    /// <summary>
    /// Faz 5 Bolum 6.5: iki zari gosterir.
    /// </summary>
    /// <remarks>
    /// <b>Dublede gosterim secimi:</b> tek zar + <c>"x2"</c> etiketi kullanilir. Dort ayri zar
    /// gorseli yerine bu secim, <see cref="BoardViewModel.RemainingCount"/> ile <b>tek</b> kaynaktan
    /// tutarlidir ve kalan zar sayisini (duble sonrasi 4, tek adim sonrasi 3/2/1) dogrudan yansitir.
    /// </remarks>
    public sealed class DiceView : MonoBehaviour
    {
        /// <summary>Birinci zar yuzu.</summary>
        public Image Die1Image;

        /// <summary>Ikinci zar yuzu.</summary>
        public Image Die2Image;

        /// <summary>Birinci zar etiketi.</summary>
        public Text Die1Label;

        /// <summary>Ikinci zar etiketi.</summary>
        public Text Die2Label;

        /// <summary>Duble carpani etiketi (<c>"x2"</c>).</summary>
        public Text MultiplierLabel;

        /// <summary>Kalan zar sayisi etiketi.</summary>
        public Text RemainingLabel;

        private byte _overrideDie1;
        private byte _overrideDie2;
        private string _overrideCaption;
        private string _caption;

        private bool _openingActive;
        private byte _openingDie1;
        private byte _openingDie2;
        private string _openingCaption;
        private Text _die1Caption;
        private Text _die2Caption;

        /// <summary>
        /// Acilis zarini <b>kimin hangi zari attigi</b> belli olacak sekilde gosterir.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Acilis zarinda iki taraf birer zar atar; yuksek atan baslar. Motor iki zari
        /// <c>rolledDice</c> icinde sirasiz tutar, bu yuzden hangi zarin kime ait oldugu
        /// <b>burada</b> belirlenir: kazananin zari daima <b>buyuk</b> olandir.
        /// </para>
        /// <para>
        /// Gosterim normal zar gosteriminden <b>once</b> gelir; acilis bitince
        /// <see cref="ClearOpeningRoll"/> cagrilir ve panel normal moda doner.
        /// </para>
        /// </remarks>
        public void SetOpeningRoll(byte humanDie, byte aiDie, bool humanStarts)
        {
            _openingActive = true;
            _openingDie1 = humanDie;
            _openingDie2 = aiDie;
            _openingCaption = "acilis: sen " + humanDie + " - rakip " + aiDie +
                              (humanStarts ? " (sen basliyorsun)" : " (rakip basliyor)");

            // Acilis gosterimi gecici durum mesajini ve rakip sabitlemesini gecersiz kilar.
            _caption = null;
            _overrideDie1 = 0;
            _overrideDie2 = 0;
            _overrideCaption = null;
        }

        /// <summary>Acilis zar gosterimini kaldirir.</summary>
        public void ClearOpeningRoll()
        {
            _openingActive = false;
            _openingDie1 = 0;
            _openingDie2 = 0;
            _openingCaption = null;
        }

        /// <summary>Acilis zar gosterimi etkin mi?</summary>
        public bool IsOpeningRollActive
        {
            get { return _openingActive; }
        }

        /// <summary>
        /// Kalan zar etiketinin yerine gecici bir aciklama yazar (acilis zarinda kimin
        /// baslayacagi gibi). <see cref="ClearCaption"/> ile kaldirilir.
        /// </summary>
        public void SetCaption(string caption)
        {
            // Gecici mesaj acilis gosteriminden sonra gelir; acilis gosterimi kapanir.
            ClearOpeningRoll();
            _caption = caption;
        }

        /// <summary>Gecici aciklamayi kaldirir.</summary>
        public void ClearCaption()
        {
            _caption = null;
        }

        /// <summary>
        /// Zarlari gecici olarak sabitler.
        /// </summary>
        /// <remarks>
        /// Rakip zar atip hamlesini oynadiginda <c>rolledDice</c> sifirlanir ve zar paneli
        /// bosalir; oyuncu rakibin ne attigini goremez. Bu yontem, tahtada zar yokken
        /// (<c>Die1 == 0</c>) son atilan zarlarin gosterilmesini saglar.
        /// </remarks>
        public void SetOverride(byte die1, byte die2, string caption)
        {
            _overrideDie1 = die1;
            _overrideDie2 = die2;
            _overrideCaption = caption;

            // Sabitleme bilincli bir gosterimdir; acilis gosterimini ve gecici durum
            // mesajini gecersiz kilar.
            ClearOpeningRoll();
            _caption = null;
        }

        /// <summary>Sabitlemeyi kaldirir.</summary>
        public void ClearOverride()
        {
            _overrideDie1 = 0;
            _overrideDie2 = 0;
            _overrideCaption = null;
        }

        /// <summary>Gorunumleri kurar.</summary>
        public void Initialize()
        {
            var root = (RectTransform)transform;

            // Zar yuzleri 72 px: altlarinda "Sen"/"Rakip" etiketine yer kalsin diye
            // 80 px'ten kucultuldu. Panel yuksekligi (112) degismedi.
            Die1Image = UiFactory.CreateImage("Die1Face", root, Color.white, SpriteLibrary.Die(1));
            UiFactory.ApplyLayout((RectTransform)Die1Image.transform, 0f, 0f, 72f, 72f);
            Die1Image.enabled = false;

            Die2Image = UiFactory.CreateImage("Die2Face", root, Color.white, SpriteLibrary.Die(1));
            UiFactory.ApplyLayout((RectTransform)Die2Image.transform, 80f, 0f, 72f, 72f);
            Die2Image.enabled = false;

            Die1Label = UiFactory.CreateText("Die1", root, "-", 36, TextAnchor.MiddleCenter, Color.white);
            UiFactory.ApplyLayout((RectTransform)Die1Label.transform, 0f, 0f, 72f, 72f);

            Die2Label = UiFactory.CreateText("Die2", root, "-", 36, TextAnchor.MiddleCenter, Color.white);
            UiFactory.ApplyLayout((RectTransform)Die2Label.transform, 80f, 0f, 72f, 72f);

            MultiplierLabel = UiFactory.CreateText("Multiplier", root, string.Empty, 24,
                                                   TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.2f));
            UiFactory.ApplyLayout((RectTransform)MultiplierLabel.transform, 160f, 0f, 60f, 72f);

            // Acilis zarinda hangi zarin kime ait oldugunu gosteren etiketler.
            _die1Caption = UiFactory.CreateText("Die1Owner", root, string.Empty, 15,
                                                TextAnchor.MiddleCenter, new Color(0.75f, 0.85f, 1f));
            UiFactory.ApplyLayout((RectTransform)_die1Caption.transform, 0f, 74f, 72f, 18f);

            _die2Caption = UiFactory.CreateText("Die2Owner", root, string.Empty, 15,
                                                TextAnchor.MiddleCenter, new Color(1f, 0.78f, 0.72f));
            UiFactory.ApplyLayout((RectTransform)_die2Caption.transform, 80f, 74f, 72f, 18f);

            RemainingLabel = UiFactory.CreateText("Remaining", root, string.Empty, 16,
                                                  TextAnchor.MiddleCenter, new Color(0.8f, 0.8f, 0.8f));
            UiFactory.ApplyLayout((RectTransform)RemainingLabel.transform, 0f, 92f, 240f, 20f);
        }

        /// <summary>View-model'den yeniler.</summary>
        public void Refresh(BoardViewModel model)
        {
            if (model == null)
            {
                return;
            }

            byte die1 = model.Die1;
            byte die2 = model.Die2;

            // Acilis gosterimi her seyin onunde gelir: motor iki zari sirasiz tuttugu icin
            // "sen/rakip" atfi yalnizca burada bilinir.
            if (_openingActive)
            {
                die1 = _openingDie1;
                die2 = _openingDie2;
            }

            bool overrideActive = !_openingActive && die1 == 0 && _overrideDie1 > 0 && _overrideDie2 > 0;
            if (overrideActive)
            {
                die1 = _overrideDie1;
                die2 = _overrideDie2;
            }

            bool hasDice = die1 > 0 && die2 > 0;

            if (Die1Image != null)
            {
                Die1Image.enabled = hasDice;
                if (hasDice)
                {
                    Die1Image.sprite = SpriteLibrary.Die(die1);
                }
            }

            if (Die2Image != null)
            {
                Die2Image.enabled = hasDice;
                if (hasDice)
                {
                    Die2Image.sprite = SpriteLibrary.Die(die2);
                }
            }

            if (Die1Label != null)
            {
                Die1Label.enabled = !hasDice;
                Die1Label.text = hasDice ? die1.ToString() : "-";
            }

            if (Die2Label != null)
            {
                Die2Label.enabled = !hasDice;
                Die2Label.text = hasDice ? die2.ToString() : "-";
            }

            if (_die1Caption != null)
            {
                _die1Caption.text = _openingActive ? "Sen" : string.Empty;
            }

            if (_die2Caption != null)
            {
                _die2Caption.text = _openingActive ? "Rakip" : string.Empty;
            }

            bool isDouble = hasDice && die1 == die2;
            if (MultiplierLabel != null)
            {
                MultiplierLabel.text = isDouble ? "x2" : string.Empty;
            }

            if (RemainingLabel != null)
            {
                if (_openingActive)
                {
                    RemainingLabel.text = _openingCaption ?? string.Empty;
                }
                else if (_caption != null)
                {
                    RemainingLabel.text = _caption;
                }
                else if (overrideActive)
                {
                    RemainingLabel.text = _overrideCaption ?? string.Empty;
                }
                else
                {
                    RemainingLabel.text = model.RemainingCount > 0
                        ? "kalan zar: " + model.RemainingCount
                        : string.Empty;
                }
            }
        }
    }
}
