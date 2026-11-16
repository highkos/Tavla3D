using System;
using System.Collections.Generic;
using UnityEngine;

namespace TavlaAI.Infrastructure
{
    /// <summary>
    /// Faz 5 Bolum 6.1: arka plan is parcacigindan gelen istekleri ana is parcacigina tasir.
    /// </summary>
    /// <remarks>
    /// <c>GameOrchestrator.ThinkAsync</c> bir <see cref="System.Threading.Tasks.Task"/> dondurur;
    /// devam (continuation) Unity API'sine <b>yalnizca</b> bu kopru uzerinden dokunmalidir.
    /// </remarks>
    public sealed class UnityMainThreadDispatcher : MonoBehaviour
    {
        private static readonly Queue<Action> Pending = new Queue<Action>(64);
        private static readonly object Gate = new object();
        private static UnityMainThreadDispatcher _instance;

        /// <summary>Etkin kopru; sahne yuklenmeden once <c>null</c>.</summary>
        public static UnityMainThreadDispatcher Instance
        {
            get { return _instance; }
        }

        /// <summary>Kopru yoksa olusturur ve sahneler arasi korur.</summary>
        public static void EnsureExists()
        {
            if (_instance != null)
            {
                return;
            }

            var go = new GameObject("UnityMainThreadDispatcher");
            _instance = go.AddComponent<UnityMainThreadDispatcher>();
            DontDestroyOnLoad(go);
        }

        /// <summary>Islemi ana is parcaciginda calistirilmak uzere kuyruga alir.</summary>
        public static void Enqueue(Action action)
        {
            if (action == null)
            {
                return;
            }

            lock (Gate)
            {
                Pending.Enqueue(action);
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
        }

        private void Update()
        {
            while (true)
            {
                Action action;
                lock (Gate)
                {
                    if (Pending.Count == 0)
                    {
                        return;
                    }

                    action = Pending.Dequeue();
                }

                try
                {
                    action();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }
    }
}
