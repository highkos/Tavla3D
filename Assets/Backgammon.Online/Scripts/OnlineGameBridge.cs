using System;
using System.Threading;
using System.Threading.Tasks;
using Backgammon.Client;
using Backgammon.Contracts.Dto;
using Backgammon.Contracts.Protocol;
using Backgammon.Core;
using Cysharp.Net.Http;
using TavlaAI.Bootstrap;
using TavlaAI.Infrastructure;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TavlaAI.Online
{
    /// <summary>
    /// Cevrimici modu mevcut cevrimdisi oyuna baglar.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Sunucu yetkili.</b> Zar sunucuda atilir, hamle sunucuda dogrulanir. Bu bilesen
    /// sunucudan gelen <see cref="MatchStateDto"/>'yi <see cref="OnlineBoardAdapter"/> ile
    /// motor pozisyonuna cevirip <see cref="GameBootstrap.ApplyOnlineState"/> uzerinden
    /// tahtaya yazar; oyuncunun bitirdigi hamleyi <see cref="GameBootstrap.OnlineMoveCommitted"/>
    /// ile alip sunucuya gonderir.
    /// </para>
    /// <para>
    /// <b>Kural kodu tek kopya.</b> Yasal hamle uretimi, kismi hamle onizlemesi ve analiz
    /// yerelde <b>ayni</b> <c>Backgammon.Core</c> / <c>Backgammon.Rules.TavlaAI</c>
    /// kutuphaneleriyle calisir; sunucu da ayni kutuphaneyi kullanir.
    /// </para>
    /// <para>
    /// <b>Iki mod.</b> <see cref="GameMode.Local"/> mevcut cevrimdisi oyunu korur;
    /// <see cref="GameMode.Online"/> yalnizca bu bilesen etkinken devreye girer.
    /// </para>
    /// </remarks>
    [DefaultExecutionOrder(-90)]
    public sealed class OnlineGameBridge : MonoBehaviour, IOnlineGame
    {
        /// <summary>Canli sunucu adresi (TLS + HTTP/2).</summary>
        public const string DefaultServerAddress = "https://game.koseli.com.tr:5002";

        private const string DeviceIdPrefsKey = "tavla.online.deviceId";

        [SerializeField]
        [Tooltip("Sunucu adresi. Canlida https:// (TLS + HTTP/2) olmalidir.")]
        private string _serverAddress = DefaultServerAddress;

        private BackgammonClient _client;
        private Stage2Clients _stage2;
        private CancellationTokenSource _cts;
        private GameBootstrap _bootstrap;
        private bool _busy;
        private bool _submitting;
        private string _status = "Cevrimdisi";
        private Text _statusLabel;
        private Button _connectButton;
        private Button _cancelButton;
        private Button _offlineButton;

        /// <summary>Etkin kopru (yoksa <c>null</c>).</summary>
        public static OnlineGameBridge Instance { get; private set; }

        /// <summary>Baglanti durumu.</summary>
        public ConnectionState ConnectionState { get; private set; } = ConnectionState.Disconnected;

        /// <summary>Son bilinen yetkili mac goruntusu.</summary>
        public MatchSnapshotDto Snapshot { get; private set; }

        /// <summary>Kullaniciya gosterilen durum metni.</summary>
        public string Status
        {
            get { return _status; }
        }

        /// <summary>Sunucu adresi.</summary>
        public string ServerAddress
        {
            get { return _serverAddress; }
            set { _serverAddress = value; }
        }

        /// <summary>
        /// Kopruyu her sahne yuklemesinden sonra kendini kuracak sekilde kaydeder.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Neden?</b> Bilesenin sahneye elle eklenmesi kirilgandi: <c>Main.unity</c>
        /// <c>SceneBuilder</c> ile yeniden uretildiginde veya baska bir sahnede acildiginda
        /// cevrimici panel sessizce kayboluyordu. Bu kanca sayesinde <see cref="GameBootstrap"/>
        /// hangi sahnede olursa olsun panel kendini kurar.
        /// </para>
        /// <para>
        /// <b>Neden <c>sceneLoaded</c> ve <c>BeforeSceneLoad</c>?</b> Tek seferlik
        /// <c>AfterSceneLoad</c> yalnizca ilk sahne icin calisir; oyun baska bir sahneye
        /// gectiginde panel bir daha kurulmazdi. <c>sceneLoaded</c> her yuklemede tetiklenir ve
        /// <see cref="GameBootstrap"/> <c>Awake</c> icinde <c>Instance</c>'i atadiktan sonra
        /// cagrilir.
        /// </para>
        /// <para>
        /// <b>Cevrimdisi oyun etkilenmez.</b> <see cref="GameBootstrap"/> yoksa (ornegin sadece
        /// menu sahnesi) hicbir sey yapmayiz; cevrimdisi oyun bu bilesen olmadan da calisir.
        /// </para>
        /// </remarks>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterAutoInstall()
        {
            // Domain reload kapaliyken statik abonelik onceki oturumdan kalabilir.
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureInstalled();
        }

        /// <summary>
        /// Kopru yoksa olusturur. <see cref="GameBootstrap"/> yoksa hicbir sey yapmaz.
        /// </summary>
        /// <returns>Kopru hazirsa <c>true</c>.</returns>
        public static bool EnsureInstalled()
        {
            if (Instance != null)
            {
                return true;
            }

            if (GameBootstrap.Instance == null)
            {
                return false;
            }

            var go = new GameObject("OnlineGameBridge");
            go.AddComponent<OnlineGameBridge>();
            return Instance != null;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            MagicOnionBootstrap.Initialize();

            _bootstrap = GameBootstrap.Instance;
            if (_bootstrap != null)
            {
                _bootstrap.OnlineMoveCommitted += OnOnlineMoveCommitted;
            }

            BuildPanel();
            SetStatus("Cevrimdisi");
            UpdateButtons();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            if (_bootstrap != null)
            {
                _bootstrap.OnlineMoveCommitted -= OnOnlineMoveCommitted;
                _bootstrap = null;
            }

            DisposeClientAsync().GetAwaiter().GetResult();
        }

        // ---------------------------------------------------------------- IOnlineGame

        /// <inheritdoc />
        public void OnConnectionStateChanged(ConnectionState state)
        {
            ConnectionState = state;
            SetStatus(DescribeState(state));
            UpdateButtons();
        }

        /// <inheritdoc />
        public void OnMatchmakingUpdate(MatchmakingTicketDto ticket)
        {
            if (ticket == null)
            {
                return;
            }

            switch (ticket.Status)
            {
                case MatchmakingStatus.Searching:
                    SetStatus("Rakip araniyor... (" + (ticket.WaitedMs / 1000) + " sn)");
                    break;
                case MatchmakingStatus.Matched:
                    SetStatus("Rakip bulundu");
                    break;
                case MatchmakingStatus.MatchedWithBot:
                    SetStatus("Rakip bulunamadi, bot ile oynuyorsun");
                    break;
                case MatchmakingStatus.Cancelled:
                    SetStatus("Eslesme iptal edildi");
                    break;
            }
        }

        /// <inheritdoc />
        public void OnMatchStarted(MatchSnapshotDto snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            Snapshot = snapshot;
            ApplySnapshot(snapshot);
            SetStatus("Mac basladi");
        }

        /// <inheritdoc />
        public void OnMatchEvent(MatchEventDto matchEvent, MatchSnapshotDto snapshot)
        {
            if (matchEvent == null)
            {
                return;
            }

            if (snapshot != null)
            {
                Snapshot = snapshot;
            }

            switch (matchEvent.Kind)
            {
                case MatchEventKind.MatchFinished:
                    SetStatus("Mac bitti");
                    break;
                case MatchEventKind.MatchAborted:
                    SetStatus("Mac iptal edildi");
                    break;
                case MatchEventKind.PlayerDisconnected:
                    SetStatus("Rakibin baglantisi koptu");
                    break;
                case MatchEventKind.PlayerReconnected:
                    SetStatus("Rakip geri dondu");
                    break;
                case MatchEventKind.TurnTimedOut:
                    SetStatus("Sure doldu, sira gecti");
                    break;
            }

            if (snapshot != null)
            {
                ApplySnapshot(snapshot);
            }
            else if (matchEvent.State != null)
            {
                ApplyState(matchEvent.State);
            }
        }

        /// <inheritdoc />
        public void OnQuickChat(QuickChatMessageDto message)
        {
            if (message == null)
            {
                return;
            }

            SetStatus("Hizli mesaj: " + message.QuickChatId);
        }

        /// <inheritdoc />
        public void OnError(ErrorInfo error)
        {
            if (error == null)
            {
                return;
            }

            SetStatus("Hata: " + error.Message);
            Debug.LogWarning("[TavlaAI.Online] " + error.Code + " " + error.Message);
        }

        // ---------------------------------------------------------------- actions

        private void OnConnectClicked()
        {
            _ = ConnectAndPlayAsync();
        }

        private void OnCancelClicked()
        {
            _ = CancelAsync();
        }

        private void OnOfflineClicked()
        {
            _ = GoOfflineAsync();
        }

        private async Task ConnectAndPlayAsync()
        {
            if (_busy)
            {
                return;
            }

            _busy = true;
            UpdateButtons();
            try
            {
                await EnsureClientAsync();
                SetStatus("Eslesme kuyruguna giriliyor...");
                await _client.EnqueueAsync(_cts.Token);
            }
            catch (OperationCanceledException)
            {
                SetStatus("Iptal edildi");
            }
            catch (Exception ex)
            {
                SetStatus("Baglanti hatasi: " + ex.Message);
                Debug.LogWarning("[TavlaAI.Online] " + ex);
            }
            finally
            {
                _busy = false;
                UpdateButtons();
            }
        }

        private async Task CancelAsync()
        {
            if (_busy || _client == null)
            {
                return;
            }

            _busy = true;
            UpdateButtons();
            try
            {
                await _client.CancelMatchmakingAsync(_cts.Token);
                SetStatus("Eslesme iptal edildi");
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[TavlaAI.Online] " + ex);
            }
            finally
            {
                _busy = false;
                UpdateButtons();
            }
        }

        private async Task GoOfflineAsync()
        {
            if (_busy)
            {
                return;
            }

            _busy = true;
            UpdateButtons();
            try
            {
                await DisposeClientAsync();

                if (_bootstrap != null)
                {
                    _bootstrap.SetGameMode(GameMode.Local);
                    _bootstrap.SetOnlineInputLocked(false);
                }

                SetStatus("Cevrimdisi");
            }
            finally
            {
                _busy = false;
                UpdateButtons();
            }
        }

        private async void OnOnlineMoveCommitted(MoveSequence move)
        {
            if (_client == null || Snapshot == null || _submitting || move.IsEmpty)
            {
                return;
            }

            _submitting = true;
            if (_bootstrap != null)
            {
                _bootstrap.SetOnlineInputLocked(true);
            }

            try
            {
                MoveDto[] moves = OnlineBoardAdapter.ToMoves(move);
                TurnResultDto result = await _client.SubmitTurnAsync(moves, _cts.Token);
                if (result != null && result.State != null)
                {
                    ApplyState(result.State);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                SetStatus("Hamle reddedildi: " + ex.Message);
                Debug.LogWarning("[TavlaAI.Online] " + ex);
                await ResyncAsync();
            }
            finally
            {
                _submitting = false;
                if (_bootstrap != null)
                {
                    _bootstrap.SetOnlineInputLocked(false);
                }
            }
        }

        private async Task ResyncAsync()
        {
            if (_client == null)
            {
                return;
            }

            try
            {
                MatchSnapshotDto snapshot = await _client.RequestSnapshotAsync(_cts.Token);
                if (snapshot != null)
                {
                    Snapshot = snapshot;
                    ApplySnapshot(snapshot);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[TavlaAI.Online] yeniden senkronizasyon basarisiz: " + ex.Message);
            }
        }

        // ---------------------------------------------------------------- client lifecycle

        private async Task EnsureClientAsync()
        {
            if (_client != null && ConnectionState == ConnectionState.Connected)
            {
                return;
            }

            await DisposeClientAsync();

            _cts = new CancellationTokenSource();

            var options = new ClientOptions
            {
                ServerAddress = _serverAddress,
                DeviceId = ResolveDeviceId(),
                HttpHandlerFactory = () => new YetAnotherHttpHandler { Http2Only = true },
                Log = message => Debug.Log("[TavlaAI.Online] " + message),
            };

            _client = new BackgammonClient(options, UnityDispatcher.Instance, this);
            _stage2 = new Stage2Clients(_client);

            await _client.ConnectAsync(_cts.Token);
        }

        private async Task DisposeClientAsync()
        {
            if (_cts != null)
            {
                try
                {
                    _cts.Cancel();
                }
                catch (ObjectDisposedException)
                {
                }

                _cts.Dispose();
                _cts = null;
            }

            if (_stage2 != null)
            {
                try
                {
                    await _stage2.DisposeAsync();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[TavlaAI.Online] stage2 kapatma: " + ex.Message);
                }

                _stage2 = null;
            }

            if (_client != null)
            {
                try
                {
                    await _client.DisposeAsync();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[TavlaAI.Online] istemci kapatma: " + ex.Message);
                }

                _client = null;
            }

            Snapshot = null;
            ConnectionState = ConnectionState.Disconnected;
        }

        private static string ResolveDeviceId()
        {
            string id = PlayerPrefs.GetString(DeviceIdPrefsKey, string.Empty);
            if (string.IsNullOrEmpty(id))
            {
                id = Guid.NewGuid().ToString("N");
                PlayerPrefs.SetString(DeviceIdPrefsKey, id);
                PlayerPrefs.Save();
            }

            return id;
        }

        // ---------------------------------------------------------------- state application

        private void ApplySnapshot(MatchSnapshotDto snapshot)
        {
            if (_bootstrap == null || snapshot == null)
            {
                return;
            }

            _bootstrap.SetGameMode(GameMode.Online);
            _bootstrap.SetHumanPlayer(snapshot.YouAreWhite ? (sbyte)1 : (sbyte)-1);
            ApplyState(snapshot.State);
        }

        private void ApplyState(MatchStateDto state)
        {
            if (_bootstrap == null || state == null)
            {
                return;
            }

            BackgammonState engine = OnlineBoardAdapter.FromState(state, _bootstrap.HumanPlayer);
            _bootstrap.SetOnlineInputLocked(_submitting);
            _bootstrap.ApplyOnlineState(engine);
        }

        // ---------------------------------------------------------------- ui

        private void SetStatus(string text)
        {
            _status = text;
            if (_statusLabel != null)
            {
                _statusLabel.text = text;
            }
        }

        private void UpdateButtons()
        {
            bool connected = ConnectionState == ConnectionState.Connected;

            if (_connectButton != null)
            {
                _connectButton.interactable = !_busy && !connected;
            }

            if (_cancelButton != null)
            {
                _cancelButton.interactable = !_busy && connected;
            }

            if (_offlineButton != null)
            {
                _offlineButton.interactable = !_busy;
            }
        }

        private static string DescribeState(ConnectionState state)
        {
            switch (state)
            {
                case ConnectionState.Connecting:
                    return "Baglaniyor...";
                case ConnectionState.Connected:
                    return "Baglandi";
                case ConnectionState.Reconnecting:
                    return "Yeniden baglaniyor...";
                case ConnectionState.Closed:
                    return "Baglanti kapatildi";
                default:
                    return "Cevrimdisi";
            }
        }

        private void BuildPanel()
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[TavlaAI.Online] Canvas bulunamadi; cevrimici panel olusturulamadi.");
                return;
            }

            // Tahtanin altinda kalan bos seritte durur; kenar cubuguna ve tas tahtasina binmez.
            RectTransform root = UiFactory.CreateRect("OnlinePanel", canvas.transform);
            UiFactory.ApplyLayout(root, 20f, 970f, 700f, 92f);

            Image background = UiFactory.CreateImage("OnlinePanelBackground", root,
                                                      new Color(0.10f, 0.11f, 0.15f, 0.92f));
            var backgroundRect = (RectTransform)background.transform;
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;

            _statusLabel = UiFactory.CreateText("OnlineStatus", root, string.Empty, 17,
                                                TextAnchor.MiddleLeft, Color.white);
            UiFactory.ApplyLayout((RectTransform)_statusLabel.transform, 12f, 8f, 676f, 24f);

            _connectButton = AddButton(root, "OnlineConnect", "Cevrimici oyna", 12f, 40f, 180f, OnConnectClicked);
            _cancelButton = AddButton(root, "OnlineCancel", "Vazgec", 200f, 40f, 120f, OnCancelClicked);
            _offlineButton = AddButton(root, "OnlineOffline", "Cevrimdisi", 328f, 40f, 120f, OnOfflineClicked);
        }

        private static Button AddButton(RectTransform parent, string name, string caption, float x, float y,
                                        float width, Action handler)
        {
            Image image = UiFactory.CreateImage(name, parent, new Color(0.22f, 0.24f, 0.30f, 0.95f));
            UiFactory.ApplyLayout((RectTransform)image.transform, x, y, width, 30f);
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
            return button;
        }
    }
}