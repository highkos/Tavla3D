namespace TavlaAI
{
    /// <summary>
    /// Oyunun calisma modu.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Local"/> mevcut cevrimdisi oyundur: zar, sira ve yapay zeka tamamen
    /// istemcide doner.
    /// </para>
    /// <para>
    /// <see cref="Online"/> sunucunun yetkili oldugu moddur. Zar sunucuda atilir, hamle
    /// sunucuda dogrulanir ve tahta yalnizca sunucudan gelen durumla guncellenir. Yerel
    /// yapay zeka dongusu, zar atma ve kup teklifi bu modda devre disidir.
    /// </para>
    /// </remarks>
    public enum GameMode
    {
        /// <summary>Cevrimdisi (yerel) oyun.</summary>
        Local = 0,

        /// <summary>Cevrimici (sunucu yetkili) oyun.</summary>
        Online = 1,
    }
}
