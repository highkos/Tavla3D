namespace TavlaAI.Infrastructure
{
    /// <summary>
    /// Faz 5: sahne yerlesim sabitleri (1920 x 1080 referans cozunurluk).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Yerlesim kurali:</b> tahta solda, <b>tum</b> denetimler (mac skoru, Crawford rozeti,
    /// mac uzunlugu, zarlar, kup, analiz, ayarlar ve dugmeler) sagdaki kenar cubugunda durur.
    /// Kenar cubugu tahtanin saginda kaldigi icin hicbir denetim oyun alanini kapatmaz.
    /// </para>
    /// <para>
    /// <b>Uyarlanabilirlik:</b> kenar cubugu <b>sag</b> kenara, tahta alani ise iki yana
    /// yayilan bir kapsayiciya baglanir. Boylece pencere orani degistiginde cubuk ekran
    /// disina tasmaz ve tahta en-boy oranini korur.
    /// </para>
    /// <para>
    /// Tek kaynak: hem <c>SceneBuilder</c> (Editor) hem <c>GameBootstrap</c> (calisma zamani)
    /// bu sabitleri kullanir; boylece sahne ile calisma zamani yerlesimi ayrisamaz.
    /// </para>
    /// </remarks>
    public static class UiLayout
    {
        /// <summary>Referans genislik.</summary>
        public const float ReferenceWidth = 1920f;

        /// <summary>Referans yukseklik.</summary>
        public const float ReferenceHeight = 1080f;

        /// <summary>Tahtanin baslangic genisligi (fitter gercek boyutu hesaplar).</summary>
        public const float BoardWidth = 1500f;

        /// <summary>Tahtanin baslangic yuksekligi.</summary>
        public const float BoardHeight = 938f;

        /// <summary>Tahta en-boy orani (genislik / yukseklik).</summary>
        public const float BoardAspect = BoardWidth / BoardHeight;

        /// <summary>Tahta alaninin sol boslugu.</summary>
        public const float BoardLeftMargin = 20f;

        /// <summary>Tahta alaninin ust boslugu.</summary>
        public const float BoardTopMargin = 20f;

        /// <summary>Tahta alaninin alt boslugu.</summary>
        public const float BoardBottomMargin = 20f;

        /// <summary>Tahta alani ile kenar cubugu arasindaki bosluk.</summary>
        public const float BoardRightGap = 20f;

        /// <summary>Sag kenar cubugunun genisligi.</summary>
        public const float SidebarWidth = 352f;

        /// <summary>Sag kenar cubugunun sag boslugu.</summary>
        public const float SidebarRightMargin = 20f;

        /// <summary>Kenar cubugunun toplam kapladigi alan (bosluklar dahil).</summary>
        public const float SidebarReserved = SidebarWidth + SidebarRightMargin + BoardRightGap;

        /// <summary>Dugme cubugunun ust kenari.</summary>
        public const float ControlBarY = 1002f;

        /// <summary>Dugme cubugunun yuksekligi (iki satir).</summary>
        public const float ControlBarHeight = 66f;
    }
}
