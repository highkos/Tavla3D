using System;
using Backgammon.Client;
using TavlaAI.Infrastructure;

namespace TavlaAI.Online
{
    /// <summary>
    /// <see cref="IDispatcher"/> uygulamasi: geri cagrilari Unity ana is parcacigina tasir.
    /// </summary>
    /// <remarks>
    /// <c>Backgammon.Client</c> kitapligi Unity tipi icermez; ana is parcacigina gecis
    /// yalnizca bu ince sarmalayici uzerinden yapilir.
    /// </remarks>
    public sealed class UnityDispatcher : IDispatcher
    {
        /// <summary>Paylasilan ornek.</summary>
        public static readonly UnityDispatcher Instance = new UnityDispatcher();

        /// <inheritdoc />
        public void Post(Action action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            UnityMainThreadDispatcher.Enqueue(action);
        }
    }
}