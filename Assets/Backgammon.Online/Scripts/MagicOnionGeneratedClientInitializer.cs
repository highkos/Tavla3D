using Backgammon.Contracts.Hubs;
using MagicOnion.Client;

namespace TavlaAI.Online
{
    /// <summary>
    /// MagicOnion kaynak ureticisini tetikler.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>IL2CPP/AOT icin zorunludur.</b> MessagePack formatter'lari ve istemci
    /// proxy'leri calisma zamaninda yansima ile uretilemez; bu nedenle derleme
    /// sirasinda uretilmeleri gerekir. Ozellik, <see cref="IBackgammonHub"/>'un
    /// bulundugu derlemeyi (<c>Backgammon.Contracts</c>) tarar ve tum sozlesme
    /// tipleri icin formatter uretir.
    /// </para>
    /// <para>
    /// Uretilen <c>Resolver</c> Unity'de otomatik olarak
    /// <c>MessagePackSerializer.DefaultOptions</c>'a kaydedilir; ek kayit gerekmez.
    /// </para>
    /// </remarks>
    [MagicOnionClientGeneration(typeof(IBackgammonHub))]
    public partial class MagicOnionGeneratedClientInitializer
    {
    }
}