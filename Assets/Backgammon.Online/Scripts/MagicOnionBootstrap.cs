using Cysharp.Net.Http;
using Grpc.Net.Client;
using MagicOnion.Unity;
using UnityEngine;

namespace TavlaAI.Online
{
    /// <summary>
    /// MagicOnion / gRPC kanal altyapisini oyun sahnesi yuklenmeden once kurar.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Neden <c>YetAnotherHttpHandler</c>?</b> Unity'nin varsayilan
    /// <c>HttpClientHandler</c>'i IL2CPP'de HTTP/2 konusamaz; Android duz HTTP/2'ye
    /// (cleartext) izin vermez. Bu yuzden tasima katmani <c>Http2Only = true</c> olan
    /// <c>YetAnotherHttpHandler</c> olarak sabitlenir (ADR-038).
    /// </para>
    /// <para>
    /// <b>Neden <c>BeforeSceneLoad</c>?</b> <see cref="GrpcChannelProviderHost"/> bir
    /// <c>DontDestroyOnLoad</c> nesnesi olusturur; sahne yuklendikten sonra kurulursa
    /// ilk baglanti denemesi kanal saglayicisiz kalabilir.
    /// </para>
    /// </remarks>
    public static class MagicOnionBootstrap
    {
        private static bool _initialized;

        /// <summary>Altyapi kuruldu mu?</summary>
        public static bool IsInitialized
        {
            get { return _initialized; }
        }

        /// <summary>Kanal saglayicisini kurar. Birden fazla cagri guvenlidir.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            GrpcChannelProviderHost.Initialize(new DefaultGrpcChannelProvider(() => new GrpcChannelOptions
            {
                HttpHandler = new YetAnotherHttpHandler { Http2Only = true },
                DisposeHttpClient = true,
            }));
        }
    }
}