using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Backgammon.Client;
using Backgammon.Contracts.Dto;
using Backgammon.Contracts.Protocol;
using Backgammon.Core;
using Backgammon.UI;
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

        /// <summary>Status shown while the server waits to auto-pass a blocked turn.</summary>
        private const string NoLegalMovesStatus = "Girecek kapi yok - sira geciyor";

        /// <summary>
        /// Rakibin zarlarinin ekranda kaldigi sure (saniye).
        /// </summary>
        /// <remarks>
        /// Sunucu rakibin turunu neredeyse aninda oynar; <c>TurnStarted</c> ve
        /// <c>TurnApplied</c> olaylari milisaniyeler arayla gelir. Bu bekleme olmadan
        /// oyuncu rakibin ne attigini hic goremez.
        /// </remarks>
        private const float OpponentDiceSeconds = 1.6f;

        /// <summary>Kuyrukta tutulacak en fazla yetkili durum sayisi.</summary>
        private const int MaxQueuedStates = 8;

        /// <summary>Istemci kapatma ve hamle gonderimi icin ust sinir.</summary>
        private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(5);

        /// <summary>Hamle gonderimi bu sureden uzun surerse giris kilidi zorla acilir.</summary>
        private const float SubmitWatchdogSeconds = 25f;

        [SerializeField]
        [Tooltip("Sunucu adresi. Canlida https:// (TLS + HTTP/2) olmalidir.")]
        private string _serverAddress = DefaultServerAddress;

        private readonly Queue<MatchStateDto> _incoming = new Queue<MatchStateDto>(MaxQueuedStates);

        private BackgammonClient _client;
        private Stage2Clients _stage2;
        private CancellationTokenSource _cts;
        private GameBootstrap _bootstrap;
        private bool _busy;
        private bool _submitting;
        private float _submitStartedAt;
        private float _holdUntil;
        private int _appliedVersion = -1;
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

        private void Update()
        {
            PumpStates();
            UpdateSubmitWatchdog();
        }

        /// <summary>
        /// <see cref="GameBootstrap"/> yok edilip yeniden olusturulduysa baglantiyi tazeler.
        /// </summary>
        /// <remarks>
        /// <see cref="GameBootstrap"/> <c>DontDestroyOnLoad</c> oldugu icin normalde yasamaya
        /// devam eder; ancak uygulama kapanip acildiginda veya sahne elle yeniden kuruldugunda
        /// eski referans yok edilmis bir nesneyi gosterir ve tahta <b>sessizce</b> guncellenmez.
        /// </remarks>
        private GameBootstrap Bootstrap
        {
            get
            {
                if (_bootstrap == null)
                {
                    _bootstrap = GameBootstrap.Instance;
                    if (_bootstrap != null)
                    {
                        _bootstrap.OnlineMoveCommitted -= OnOnlineMoveCommitted;
                        _bootstrap.OnlineMoveCommitted += OnOnlineMoveCommitted;
                    }
                }

                return _bootstrap;
            }
        }

        /// <summary>
        /// Hamle gonderimi beklenenden uzun surduyse giris kilidini zorla acar.
        /// </summary>
        /// <remarks>
        /// <c>SubmitTurnAsync</c> kendi zaman asimina sahiptir; yine de baglanti yarida
        /// koparsa <c>async void</c> zinciri tamamlanmayabilir ve oyuncu sonsuza kadar
        /// kilitli kalir. Bu bekci o durumu kurtarir.
        /// </remarks>
        private void UpdateSubmitWatchdog()
        {
            if (!_submitting || _submitStartedAt <= 0f)
            {
                return;
            }

            if (Time.unscaledTime - _submitStartedAt < SubmitWatchdogSeconds)
            {
                return;
            }

            _submitStartedAt = 0f;
            _submitting = false;
            GameBootstrap bootstrap = Bootstrap;
            if (bootstrap != null)
            {
                bootstrap.SetOnlineInputLocked(false);
            }

            SetStatus("Hamle zaman asimi - yeniden deneyin");
            Debug.LogWarning("[TavlaAI.Online] hamle gonderimi zaman asimina ugradi; giris kilidi acildi.");
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

            // Yeni/yeniden kurulan oturumda onceki oturumdan kalan kilitler tasinmamali.
            _submitting = false;
            _submitStartedAt = 0f;
            _busy = false;

            Snapshot = snapshot;
            ApplySnapshot(snapshot);
            SetStatus("Mac basladi");
            UpdateButtons();
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

            // Olayin kendi durumu varsa o kullanilir: anlik goruntu (snapshot) her olayda
            // ayni nesnedir ve olay sirasini korumaz. Boylece rakibin zari once gosterilip
            // ardindan hamlesi uygulanabilir.
            if (matchEvent.State != null)
            {
                EnqueueState(matchEvent.State);
            }
            else if (snapshot != null)
            {
                ApplySnapshot(snapshot);
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
            // Cikis her zaman calismali. Onceki surumde `_busy` iken erken donuluyordu;
            // kapanma takildiginda dugme tamamen olu kaliyor ve oyuncu cevrimici moddan
            // cikamiyordu.
            _busy = true;
            UpdateButtons();
            try
            {
                await DisposeClientAsync();

                GameBootstrap bootstrap = Bootstrap;
                if (bootstrap != null)
                {
                    bootstrap.SetGameMode(GameMode.Local);
                    bootstrap.SetOnlineInputLocked(false);
                }

                SetStatus("Cevrimdisi");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[TavlaAI.Online] cevrimdisi gecisi: " + ex);
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
            if (move.IsEmpty)
            {
                return;
            }

            if (_submitting)
            {
                return;
            }

            CancellationTokenSource cts = _cts;
            if (_client == null || cts == null)
            {
                SetStatus("Baglanti yok - yeniden baglaniyor");
                await ResyncAsync();
                return;
            }

            if (Snapshot == null)
            {
                // Mac goruntusu dusmusse hamleyi sessizce yutmak yerine tazele.
                SetStatus("Durum eskimis - yeniden senkronize ediliyor");
                await ResyncAsync();
                return;
            }

            _submitting = true;
            _submitStartedAt = Time.unscaledTime;
            GameBootstrap bootstrap = Bootstrap;
            if (bootstrap != null)
            {
                bootstrap.SetOnlineInputLocked(true);
            }

            try
            {
                MoveDto[] moves = OnlineBoardAdapter.ToMoves(move);
                TurnResultDto result = await _client.SubmitTurnAsync(moves, cts.Token);
                if (result != null && result.State != null)
                {
                    EnqueueState(result.State);
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
                _submitStartedAt = 0f;
                if (bootstrap != null)
                {
                    bootstrap.SetOnlineInputLocked(false);
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
                MatchSnapshotDto snapshot = await _client.RequestSnapshotAsync(CurrentToken);
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

        /// <summary>Etkin istemcinin iptal belirteci; istemci yoksa <see cref="CancellationToken.None"/>.</summary>
        private CancellationToken CurrentToken
        {
            get
            {
                CancellationTokenSource cts = _cts;
                if (cts == null)
                {
                    return CancellationToken.None;
                }

                try
                {
                    return cts.Token;
                }
                catch (ObjectDisposedException)
                {
                    return CancellationToken.None;
                }
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

            Stage2Clients stage2 = _stage2;
            _stage2 = null;
            if (stage2 != null)
            {
                try
                {
                    await WithTimeoutAsync(stage2.DisposeAsync().AsTask()).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[TavlaAI.Online] stage2 kapatma: " + ex.Message);
                }
            }

            BackgammonClient client = _client;
            _client = null;
            if (client != null)
            {
                try
                {
                    await WithTimeoutAsync(client.DisposeAsync().AsTask()).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // Kapanma takilirsa (olu baglanti, sunucu yanit vermiyor) UI kilitli
                    // kalmamali; istemci referansi zaten birakildi.
                    Debug.LogWarning("[TavlaAI.Online] istemci kapatma: " + ex.Message);
                }
            }

            _incoming.Clear();
            _holdUntil = 0f;
            _appliedVersion = -1;
            _submitting = false;
            _submitStartedAt = 0f;
            Snapshot = null;
            ConnectionState = ConnectionState.Disconnected;
        }

        /// <summary>
        /// <paramref name="task"/> en gec <see cref="ShutdownTimeout"/> icinde tamamlanir;
        /// aksi halde <see cref="TimeoutException"/> firlatir.
        /// </summary>
        /// <remarks>
        /// <c>Task.WaitAsync</c> netstandard2.1'de yok (Unity'nin kullandigi profil).
        /// </remarks>
        private static async Task WithTimeoutAsync(Task task)
        {
            Task completed = await Task.WhenAny(task, Task.Delay(ShutdownTimeout)).ConfigureAwait(false);
            if (!ReferenceEquals(completed, task))
            {
                throw new TimeoutException("Islem " + ShutdownTimeout.TotalSeconds + " sn icinde tamamlanmadi.");
            }

            await task.ConfigureAwait(false);
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
            GameBootstrap bootstrap = Bootstrap;
            if (bootstrap == null || snapshot == null)
            {
                return;
            }

            bootstrap.SetGameMode(GameMode.Online);
            bootstrap.SetHumanPlayer(snapshot.YouAreWhite ? (sbyte)1 : (sbyte)-1);

            // Tam anlik goruntu yetkilidir; kuyrukta bekleyen eski durumlar gecersizdir.
            _incoming.Clear();
            _holdUntil = 0f;
            if (bootstrap.DiceView != null)
            {
                bootstrap.DiceView.ClearCaption();
            }

            ApplyStateNow(snapshot.State);
        }

        /// <summary>
        /// Yetkili durumu kuyruga alir; gercek uygulama <see cref="PumpStates"/> icinde olur.
        /// </summary>
        /// <remarks>
        /// Kuyruk, rakibin zarinin ekranda kalmasini saglar: sunucu rakibin turunu neredeyse
        /// aninda oynadigi icin <c>TurnStarted</c> ve <c>TurnApplied</c> arasinda gorsel fark
        /// olusmuyordu.
        /// </remarks>
        private void EnqueueState(MatchStateDto state)
        {
            if (state == null)
            {
                return;
            }

            if (_incoming.Count >= MaxQueuedStates)
            {
                _incoming.Dequeue();
            }

            _incoming.Enqueue(state);
            PumpStates();
        }

        private void PumpStates()
        {
            GameBootstrap bootstrap = Bootstrap;
            if (bootstrap == null)
            {
                return;
            }

            if (_holdUntil > 0f)
            {
                if (Time.unscaledTime < _holdUntil)
                {
                    return;
                }

                _holdUntil = 0f;
                if (bootstrap.DiceView != null)
                {
                    bootstrap.DiceView.ClearCaption();
                }
            }

            while (_incoming.Count > 0)
            {
                MatchStateDto next = _incoming.Dequeue();
                if (next == null || next.StateVersion < _appliedVersion)
                {
                    continue;
                }

                bool opponentTurn = IsOpponentTurn(next);
                if (opponentTurn && bootstrap.DiceView != null && next.Dice != null)
                {
                    // Baslik durum uygulanmadan once yazilir; RefreshViews zar panelini
                    // bu baslikla birlikte cizer.
                    bootstrap.DiceView.SetCaption("rakip: " + next.Dice.Die1 + "-" + next.Dice.Die2);
                }

                ApplyStateNow(next);

                if (opponentTurn)
                {
                    _holdUntil = Time.unscaledTime + OpponentDiceSeconds;
                    return;
                }
            }
        }

        /// <summary>Durum rakibin turunu mu anlatiyor (zari atilmis, oynanmamis)?</summary>
        private bool IsOpponentTurn(MatchStateDto state)
        {
            GameBootstrap bootstrap = Bootstrap;
            if (bootstrap == null || state == null || state.Phase != MatchPhase.Playing)
            {
                return false;
            }

            if (state.Turn == CheckerColor.None || state.Dice == null)
            {
                return false;
            }

            if (state.Dice.Remaining == null || state.Dice.Remaining.Length == 0)
            {
                // Zar atilmis ama oynanmis: gosterilecek bir sey yok.
                return false;
            }

            return OnlineBoardAdapter.ToEngineColor(state.Turn) != bootstrap.HumanPlayer;
        }

        private void ApplyStateNow(MatchStateDto state)
        {
            GameBootstrap bootstrap = Bootstrap;
            if (bootstrap == null || state == null)
            {
                return;
            }

            _appliedVersion = state.StateVersion;

            BackgammonState engine = OnlineBoardAdapter.FromState(state, bootstrap.HumanPlayer);
            bootstrap.SetOnlineInputLocked(_submitting);
            bootstrap.ApplyOnlineState(engine);

            // The server auto-passes a turn whose player has no legal move at all
            // (e.g. on the bar against a closed home board). The client never
            // submits an empty turn itself; it only explains the waiting.
            GameOrchestrator orchestrator = bootstrap.Orchestrator;
            bool blockedByOpponent = state.Phase == MatchPhase.Playing &&
                                     orchestrator != null &&
                                     orchestrator.State.turn == bootstrap.HumanPlayer &&
                                     orchestrator.State.phase == GamePhase.Moving &&
                                     orchestrator.State.remainingCount > 0 &&
                                     !orchestrator.HasLegalMoves;
            if (blockedByOpponent)
            {
                SetStatus(NoLegalMovesStatus);
            }
            else if (_status == NoLegalMovesStatus)
            {
                SetStatus(string.Empty);
            }
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
                // Cikis her zaman denenebilir olmali: kapanma takildiginda dugme pasif
                // kalirsa oyuncu cevrimici moddan hic cikamaz.
                _offlineButton.interactable = true;
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