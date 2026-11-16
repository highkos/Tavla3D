using System;
using System.Threading;
using System.Threading.Tasks;
using Backgammon.Core;
using Backgammon.Inference;
using Backgammon.UI;
using TavlaAI.Analysis;
using TavlaAI.Board;
using TavlaAI.Cube;
using TavlaAI.Dice;
using TavlaAI.Hud;
using TavlaAI.Infrastructure;
using TavlaAI.Input;
using TavlaAI.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace TavlaAI.Bootstrap
{
    /// <summary>
    /// Faz 5 Bolum 6.3: sahnenin <b>tek</b> <see cref="GameOrchestrator"/> sahibi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Zorunlu kural:</b> sahnede baska hicbir MonoBehaviour <see cref="GameOrchestrator"/>
    /// olusturmaz. Bu bilesen <c>DontDestroyOnLoad</c> ile korunur.
    /// </para>
    /// <para>
    /// Insan oyuncunun rengi ayarlardan secilir (varsayilan <b>Siyah</b>); yapay zeka karsi
    /// rengi oynar. Siyah varsayilandir cunku Siyah'in ev bolgesi ve toplama tepsisi boylece
    /// <b>sag altta</b> kalir.
    /// </para>
    /// </remarks>
    [DefaultExecutionOrder(-100)]
    public sealed class GameBootstrap : MonoBehaviour
    {
        /// <summary>Rakibin zari hamle oynanmadan once en az bu kadar gorunur kalir (saniye).</summary>
        private const float AiRollDisplaySeconds = 0.9f;

        /// <summary>Rakibin zari hamlesinden sonra da bu kadar gorunur kalir (saniye).</summary>
        private const float AiDiceTailSeconds = 1.6f;

        /// <summary>Acilis zari bu kadar sure ekranda kalir (saniye).</summary>
        private const float OpeningRollSeconds = 1.6f;

        /// <summary>Bloke zar bu kadar sure gosterilip sira devredilir (saniye).</summary>
        private const float BlockedPassSeconds = 1.2f;

        private static GameBootstrap _instance;

        private GameOrchestrator _orchestrator;
        private OnnxModelHandle _modelHandle;
        private BearoffDatabase _bearoff;
        private BoardViewModel _boardModel;
        private CubeViewModel _cubeModel;
        private MatchScoreViewModel _matchModel;
        private AnalysisViewModel _analysisModel;
        private MoveInputController _moveInput;
        private bool _aiBusy;
        private bool _aiDoubledThisGame;
        private bool _pendingAiTake;
        private bool _aiRollPending;
        private float _aiRollAt;
        private float _aiDiceVisibleUntil;
        private byte _aiDie1;
        private byte _aiDie2;
        private bool _initialized;
        private string _backendError;
        private bool _inputStateKnown;
        private sbyte _inputTurn;
        private GamePhase _inputPhase;
        private sbyte _humanPlayer = 1;
        private bool _openingRollPending;
        private float _openingRollAt;
        private float _blockedAt;
        private Button _rollButton;
        private Button _doubleButton;
        private GameMode _gameMode = GameMode.Local;
        private bool _onlineInputLocked;

        /// <summary>Insan oyuncunun tarafi (<c>+1</c> Beyaz, <c>-1</c> Siyah).</summary>
        public sbyte HumanPlayer
        {
            get { return _humanPlayer; }
        }

        /// <summary>Yapay zekanin tarafi (insanin karsi rengi).</summary>
        public sbyte AiPlayer
        {
            get { return (sbyte)-_humanPlayer; }
        }

        /// <summary>Etkin oyun modu.</summary>
        public GameMode Mode
        {
            get { return _gameMode; }
        }

        /// <summary>
        /// Cevrimici modda oyuncu hamlesini bitirdiginde tetiklenir. Yerel modda abone
        /// yoktur; hamle dogrudan orkestratora uygulanir.
        /// </summary>
        public event Action<MoveSequence> OnlineMoveCommitted;

        /// <summary>
        /// Oyun modunu degistirir.
        /// </summary>
        /// <remarks>
        /// Cevrimici modda yerel yapay zeka dongusu, zar atma ve kup teklifi devre disi
        /// kalir; tahta yalnizca <see cref="ApplyOnlineState"/> ile guncellenir.
        /// </remarks>
        public void SetGameMode(GameMode mode)
        {
            if (_gameMode == mode)
            {
                return;
            }

            _gameMode = mode;
            _aiBusy = false;
            _aiRollPending = false;
            _openingRollPending = false;
            _pendingAiTake = false;
            _blockedAt = 0f;
            _aiDiceVisibleUntil = 0f;
            _onlineInputLocked = mode == GameMode.Online;

            if (DiceView != null)
            {
                DiceView.ClearOverride();
                DiceView.ClearOpeningRoll();
                DiceView.ClearCaption();
            }

            RefreshViews();
        }

        /// <summary>
        /// Sunucudan gelen yetkili durumu motora yukler ve gorunumleri tazeler.
        /// </summary>
        public void ApplyOnlineState(BackgammonState state)
        {
            if (_orchestrator == null || state == null)
            {
                return;
            }

            _orchestrator.LoadState(state);
            RefreshViews();
        }

        /// <summary>
        /// Cevrimici modda oyuncu girdisini kilitler (sunucu yaniti beklenirken).
        /// </summary>
        public void SetOnlineInputLocked(bool locked)
        {
            _onlineInputLocked = locked;
            if (InputController == null || _orchestrator == null)
            {
                return;
            }

            InputController.IsLocked = locked ||
                                       _orchestrator.State.turn != HumanPlayer ||
                                       _orchestrator.State.phase != GamePhase.Moving;
            InputController.Refresh();
        }

        /// <summary>Cevrimici macda oyuncunun rengini sunucudan gelen bilgiye gore ayarlar.</summary>
        public void SetHumanPlayer(sbyte color)
        {
            ApplyPlayerColor(color, false);
            RefreshViews();
        }

        /// <summary>Etkin on yukleyici.</summary>
        public static GameBootstrap Instance
        {
            get { return _instance; }
        }

        /// <summary>Orkestrator.</summary>
        public GameOrchestrator Orchestrator
        {
            get { return _orchestrator; }
        }

        /// <summary>Tahta gorunumu.</summary>
        public BoardView BoardView { get; private set; }

        /// <summary>Zar gorunumu.</summary>
        public DiceView DiceView { get; private set; }

        /// <summary>Kup gorunumu.</summary>
        public CubeView CubeView { get; private set; }

        /// <summary>Mac skoru gorunumu.</summary>
        public MatchScoreView MatchScoreView { get; private set; }

        /// <summary>Crawford rozeti.</summary>
        public CrawfordBadgeView CrawfordBadgeView { get; private set; }

        /// <summary>Mac uzunlugu secici.</summary>
        public MatchLengthView MatchLengthView { get; private set; }

        /// <summary>Analiz paneli.</summary>
        public AnalysisPanelView AnalysisPanelView { get; private set; }

        /// <summary>Ayarlar paneli.</summary>
        public SettingsPanelView SettingsPanelView { get; private set; }

        /// <summary>Giris denetleyicisi.</summary>
        public BoardInputController InputController { get; private set; }

        /// <summary>Arka uc hatasi (yoksa <c>null</c>).</summary>
        public string BackendError
        {
            get { return _backendError; }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            UnityMainThreadDispatcher.EnsureExists();
            Initialize();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }

            if (_orchestrator != null)
            {
                _orchestrator.Changed -= OnOrchestratorChanged;
                _orchestrator.Dispose();
                _orchestrator = null;
            }

            if (_modelHandle != null)
            {
                _modelHandle.Dispose();
                _modelHandle = null;
            }

            if (_bearoff != null)
            {
                _bearoff.Dispose();
                _bearoff = null;
            }
        }

        /// <summary>Orkestratoru ve gorunumleri kurar.</summary>
        public void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            IPolicyValueEvaluator evaluator = OnnxBackendFactory.CreateEvaluator(out _modelHandle, out _backendError);
            if (_backendError != null)
            {
                Debug.LogWarning("[TavlaAI] ONNX arka ucu kullanilamadi, yedek degerlendirici etkin: " + _backendError);
            }

            string bearoffError;
            _bearoff = OnnxBackendFactory.OpenBearoff(out bearoffError);
            if (bearoffError != null)
            {
                Debug.LogWarning("[TavlaAI] Bearoff veritabani yok: " + bearoffError);
            }

            var options = new GameOrchestratorOptions
            {
                Evaluator = evaluator,
                Bearoff = _bearoff,
                Met = MatchEquityTable.Default,
                Distribution = new HeuristicOutcomeDistributionEvaluator(evaluator),
                SearchSettings = SearchSettings.Default,
                EngineSettings = EngineSettings.Default,
                // Sabit tohum her acilista ayni zar dizisini veriyordu (once 5-2, sonra 6-6).
                // Canli oyunda tohum isletim sisteminden alinir; testler kendi tohumunu enjekte eder.
                Random = DiceRandom.Create(),
                MaxPlies = 2,
            };

            _orchestrator = new GameOrchestrator(options);
            _orchestrator.Changed += OnOrchestratorChanged;

            _boardModel = new BoardViewModel();
            _cubeModel = new CubeViewModel();
            _matchModel = new MatchScoreViewModel();
            _analysisModel = new AnalysisViewModel();
            _moveInput = new MoveInputController();
            _boardModel.Changed += OnBoardModelChanged;

            BoardView = FindOrCreate<BoardView>("BoardRoot");
            DiceView = FindOrCreate<DiceView>("DiceRoot");
            CubeView = FindOrCreate<CubeView>("CubeRoot");
            MatchScoreView = FindOrCreate<MatchScoreView>("MatchScorePanel");
            CrawfordBadgeView = FindOrCreate<CrawfordBadgeView>("CrawfordBadge");
            MatchLengthView = FindOrCreate<MatchLengthView>("MatchLengthPanel");
            AnalysisPanelView = FindOrCreate<AnalysisPanelView>("AnalysisPanel");
            SettingsPanelView = FindOrCreate<SettingsPanelView>("SettingsPanel");
            InputController = FindOrCreate<BoardInputController>("BoardInput");

            BoardView.Initialize(_boardModel);
            DiceView.Initialize();
            CubeView.Initialize();
            MatchScoreView.Initialize();
            CrawfordBadgeView.Initialize();
            MatchLengthView.Initialize();
            AnalysisPanelView.Initialize();
            SettingsPanelView.Initialize();
            SettingsPanelView.SetVisible(false);

            InputController.Initialize(BoardView, _orchestrator, _moveInput, _boardModel);
            InputController.MoveCompleted += OnHumanMoveCompleted;

            CubeView.Accepted += OnCubeAccepted;
            CubeView.Declined += OnCubeDeclined;
            MatchLengthView.LengthSelected += OnMatchLengthSelected;
            SettingsPanelView.Changed += OnSettingsChanged;

            CreateControlBar();

            ApplyPlayerColor(SettingsPanelView.PlayerColor, false);
            _orchestrator.NewMatch(0, false);
            RefreshViews();
        }

        private void Update()
        {
            if (_orchestrator == null)
            {
                return;
            }

            // Bu nesne DontDestroyOnLoad; sahne kaldirildiginda gorunumler yok edilir ama
            // kendisi yasamaya devam eder. Yok edilmis gorunume dokunmak
            // MissingReferenceException firlatir, bu yuzden once hazir mi diye bakilir.
            if (BoardView == null)
            {
                return;
            }

            // Cevrimici modda zar, sira ve yapay zeka tamamen sunucudadir; yerel dongu calismaz.
            if (_gameMode == GameMode.Online)
            {
                return;
            }

            UpdateAiDiceOverride();

            if (_orchestrator.IsGameOver)
            {
                return;
            }

            BackgammonState state = _orchestrator.State;

            if (state.phase == GamePhase.CubeOffered)
            {
                // state.turn hala teklif eden taraftir; karsi taraf yanit verir.
                if (state.turn == HumanPlayer)
                {
                    bool take = _pendingAiTake;
                    _pendingAiTake = false;
                    if (take)
                    {
                        _orchestrator.Take();
                    }
                    else
                    {
                        _orchestrator.Pass();
                    }
                }

                return;
            }

            if (state.phase == GamePhase.DetermineFirstPlayer)
            {
                _orchestrator.RollDice();
                AnnounceOpeningRoll();
                return;
            }

            // Acilis zari bir sure ekranda kalir; boylece oyuncu kimin basladigini gorur.
            if (_openingRollPending)
            {
                if (Time.unscaledTime - _openingRollAt < OpeningRollSeconds)
                {
                    return;
                }

                _openingRollPending = false;
                if (DiceView != null)
                {
                    DiceView.ClearOpeningRoll();
                    DiceView.ClearCaption();
                }

                RefreshViews();
                return;
            }

            // Zar bloke: yasal hamle yoksa kural geregi sira karsiya gecer.
            if (state.phase == GamePhase.Moving && !_orchestrator.HasLegalMoves)
            {
                if (_blockedAt <= 0f)
                {
                    _blockedAt = Time.unscaledTime;
                    if (DiceView != null)
                    {
                        DiceView.SetCaption("hamle yok - sira geciyor");
                    }

                    return;
                }

                if (Time.unscaledTime - _blockedAt < BlockedPassSeconds)
                {
                    return;
                }

                _blockedAt = 0f;
                _aiRollPending = false;
                if (DiceView != null)
                {
                    DiceView.ClearCaption();
                }

                _orchestrator.EndTurn();
                return;
            }

            if (state.turn != AiPlayer || _aiBusy)
            {
                return;
            }

            if (state.phase == GamePhase.PreRoll || state.phase == GamePhase.WaitForRoll)
            {
                if (state.phase == GamePhase.PreRoll && !_aiDoubledThisGame &&
                    BackgammonEngine.CanOffer(state, EngineSettings.Default))
                {
                    CubeDecision decision = _orchestrator.EvaluateCube();
                    if (decision.ShouldDouble)
                    {
                        _aiDoubledThisGame = true;
                        _orchestrator.OfferDouble();
                        return;
                    }
                }

                _orchestrator.RollDice();
                CaptureAiRoll();
                return;
            }

            if (state.phase == GamePhase.Moving)
            {
                // Rakibin zari en az AiRollDisplaySeconds gorunur kalsin; aksi halde
                // zar ayni karede atilip hamle oynanir ve oyuncu ne atildigini goremez.
                if (_aiRollPending)
                {
                    if (Time.unscaledTime - _aiRollAt < AiRollDisplaySeconds)
                    {
                        return;
                    }

                    _aiRollPending = false;
                }

                StartAiThink();
            }
        }

        /// <summary>
        /// Acilis zarini ve kimin basladigini duyurur.
        /// </summary>
        /// <remarks>
        /// <see cref="BackgammonEngine.RollOpening"/> yuksek zari atan tarafi baslatir; ancak
        /// yapay zeka kazanirsa hamle ayni karede oynanip oyuncu acilis zarini hic goremezdi.
        /// Bu yuzden zar <see cref="OpeningRollSeconds"/> boyunca ekranda tutulur.
        /// </remarks>
        private void AnnounceOpeningRoll()
        {
            BackgammonState state = _orchestrator.State;
            byte d1 = state.rolledDice[0];
            byte d2 = state.rolledDice[1];

            _openingRollPending = true;
            _openingRollAt = Time.unscaledTime;

            if (DiceView != null)
            {
                // Acilis zarinda yuksek atan baslar; bu yuzden kazananin zari daima
                // buyuk olandir. Motor iki zari sirasiz tuttugu icin "sen/rakip" atfi
                // burada yapilir ve panelde her zarin altinda etiketlenir.
                bool humanStarts = state.turn == HumanPlayer;
                byte high = d1 > d2 ? d1 : d2;
                byte low = d1 > d2 ? d2 : d1;
                byte humanDie = humanStarts ? high : low;
                byte aiDie = humanStarts ? low : high;

                DiceView.SetOpeningRoll(humanDie, aiDie, humanStarts);

                // RollDice() zaten RefreshViews() cagirdi; o an acilis gosterimi henuz
                // kurulmamisti. Etiketlerin ayni karede gorunmesi icin burada tazelenir.
                DiceView.Refresh(_boardModel);
            }
        }

        /// <summary>Rakibin attigi zarlari saklar ve gosterim sayacini baslatir.</summary>
        private void CaptureAiRoll()
        {
            BackgammonState state = _orchestrator.State;
            _aiDie1 = state.rolledDice[0];
            _aiDie2 = state.rolledDice[1];
            _aiRollPending = true;
            _aiRollAt = Time.unscaledTime;
        }

        /// <summary>Rakibin zarlarini hamlesinden sonra bir sure daha gosterir.</summary>
        private void ShowAiDiceTail()
        {
            if (_aiDie1 == 0 || _aiDie2 == 0 || DiceView == null)
            {
                return;
            }

            DiceView.SetOverride(_aiDie1, _aiDie2, "rakip: " + _aiDie1 + "-" + _aiDie2);
            _aiDiceVisibleUntil = Time.unscaledTime + AiDiceTailSeconds;
        }

        /// <summary>Rakip zari gosterim suresi dolunca sabitlemeyi kaldirir.</summary>
        private void UpdateAiDiceOverride()
        {
            if (_aiDiceVisibleUntil <= 0f || Time.unscaledTime < _aiDiceVisibleUntil)
            {
                return;
            }

            _aiDiceVisibleUntil = 0f;
            if (DiceView != null)
            {
                DiceView.ClearOverride();
            }

            RefreshViews();
        }

        /// <summary>
        /// AI'yi <b>ana is parcaciginda</b> calistirir.
        /// </summary>
        /// <remarks>
        /// Unity/Mono altinda ONNX Runtime'in <c>OrtIoBinding</c> nesnesi is parcacigi guvenli
        /// degildir; arka plan is parcaciginda <c>SynchronizeBoundInputs</c> erisim ihlali
        /// (0xC0000005) uretir. Bu yuzden arama senkron yapilir. 1 katmanli arama onbellekli
        /// toplu degerlendirme ile birkac on milisaniyede biter; kare hizi etkilenmez.
        /// </remarks>
        private void StartAiThink()
        {
            _aiBusy = true;
            InputController.IsLocked = true;

            SearchOutcome outcome;
            try
            {
                outcome = _orchestrator.Think();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[TavlaAI] AI aramasi basarisiz: " + e.GetType().Name + ": " + e.Message);
                _aiBusy = false;
                InputController.IsLocked = false;
                _orchestrator.EndTurn();
                return;
            }

            _aiBusy = false;
            InputController.IsLocked = false;

            if (outcome.IsPass || outcome.BestSequence.IsEmpty)
            {
                ShowAiDiceTail();
                _orchestrator.EndTurn();
                return;
            }

            ShowAiDiceTail();
            _orchestrator.ApplyMove(outcome.BestSequence);
        }

        private void OnHumanMoveCompleted(MoveSequence move)
        {
            if (_orchestrator == null || move.IsEmpty)
            {
                return;
            }

            if (_gameMode == GameMode.Online)
            {
                // Sunucu yetkili: hamle once sunucuya gonderilir, tahta yanitla guncellenir.
                Action<MoveSequence> handler = OnlineMoveCommitted;
                if (handler != null)
                {
                    handler(move);
                }

                return;
            }

            _orchestrator.ApplyMove(move);
        }

        private void OnCubeAccepted()
        {
            if (_orchestrator != null)
            {
                _orchestrator.Take();
            }
        }

        private void OnCubeDeclined()
        {
            if (_orchestrator != null)
            {
                _orchestrator.Pass();
            }
        }

        private void OnMatchLengthSelected(int length)
        {
            if (_orchestrator == null)
            {
                return;
            }

            _aiDoubledThisGame = false;
            _orchestrator.NewMatch(length, SettingsPanelView != null && SettingsPanelView.EnableJacoby);
        }

        private void OnSettingsChanged()
        {
            if (_orchestrator == null || SettingsPanelView == null)
            {
                return;
            }

            _orchestrator.Options.EngineSettings.EnableJacoby = SettingsPanelView.EnableJacoby;
            _orchestrator.Options.EngineSettings.DisallowDeadCube = SettingsPanelView.DisallowDeadCube;
            _orchestrator.Options.SearchSettings.DepthPly = SettingsPanelView.DepthPly;

            ApplyPlayerColor(SettingsPanelView.PlayerColor, true);
        }

        /// <summary>
        /// Insan oyuncunun rengini uygular.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Renk degisince <b>yalnizca toplama tepsileri</b> yer degistirir
        /// (<see cref="BoardView.HumanTrayBottom"/>); boylece oyuncunun tepsisi her zaman
        /// <b>sag altta</b> kalir.
        /// </para>
        /// <para>
        /// Oyuncu <b>Beyaz</b> oynarken hane numaralari da dondurulur
        /// (<see cref="BoardView.FlipPoints"/>). Motor Beyaz'i <c>1 -&gt; 24</c> yonunde
        /// hareket ettirdigi icin, dondurme olmadan oyuncunun taslari Siyah oynarken gittigi
        /// yonun tersine gidiyor gibi gorunuyordu. Dondurme ile oyuncunun ev bolgesi ve
        /// hareket yonu her iki renkte de <b>ayni</b> kalir; tahta cercevesi, bar ve tepsi
        /// seridi yer degistirmez.
        /// </para>
        /// <para>
        /// Renk degisikligi mevcut oyunu anlamsiz kilar (sira ve yon degisir), bu yuzden
        /// <paramref name="restartMatch"/> <c>true</c> ise mac yeniden baslatilir.
        /// </para>
        /// </remarks>
        private void ApplyPlayerColor(sbyte color, bool restartMatch)
        {
            sbyte resolved = color > 0 ? (sbyte)1 : (sbyte)-1;
            bool changed = resolved != _humanPlayer;
            _humanPlayer = resolved;

            if (BoardView != null)
            {
                BoardView.HumanTrayBottom = _humanPlayer < 0;
                BoardView.FlipPoints = _humanPlayer > 0;
            }

            if (MatchScoreView != null)
            {
                MatchScoreView.HumanColor = _humanPlayer;
            }

            if (!changed || !restartMatch || _orchestrator == null)
            {
                return;
            }

            _aiDoubledThisGame = false;
            _pendingAiTake = false;
            _aiRollPending = false;
            _openingRollPending = false;
            _blockedAt = 0f;
            _aiDiceVisibleUntil = 0f;
            if (DiceView != null)
            {
                DiceView.ClearOverride();
                DiceView.ClearOpeningRoll();
                DiceView.ClearCaption();
            }

            _orchestrator.NewMatch(_orchestrator.State.matchLength,
                                   SettingsPanelView != null && SettingsPanelView.EnableJacoby);
        }

        /// <summary>
        /// Tahta gorunumu degisti (kismi hamle onizlemesi dahil) — kalan zar sayisi tazelenir.
        /// </summary>
        private void OnBoardModelChanged()
        {
            if (DiceView != null)
            {
                DiceView.Refresh(_boardModel);
            }
        }

        private void OnOrchestratorChanged()
        {
            if (_orchestrator != null && _orchestrator.State.phase == GamePhase.PreRoll)
            {
                _aiDoubledThisGame = false;
            }

            RefreshViews();
        }

        private void RefreshViews()
        {
            if (_orchestrator == null)
            {
                return;
            }

            // Sahne kaldirildiginda gorunumler yok edilir; bu nesne DontDestroyOnLoad oldugu
            // icin yasamaya devam eder. Yok edilmis gorunume dokunmak istisna firlatir.
            if (BoardView == null)
            {
                return;
            }

            BackgammonState state = _orchestrator.State;
            _boardModel.Update(state);
            _cubeModel.Update(state);
            _matchModel.Update(state);

            if (state.phase != GamePhase.Moving)
            {
                _blockedAt = 0f;
            }

            // Sira/faz degistiginde yarim kalmis hamleyi temizle. Aksi halde onceki
            // turdan kalan adimlar yeni turun zarlariyla birlesip gecersiz hamle
            // uretebiliyor ("kalan zar yok" istisnasi).
            if (!_inputStateKnown || state.turn != _inputTurn || state.phase != _inputPhase)
            {
                _inputStateKnown = true;
                _inputTurn = state.turn;
                _inputPhase = state.phase;
                _moveInput.Reset();
            }

            _moveInput.Update(state);

            BoardView.Refresh();
            DiceView.Refresh(_boardModel);
            CubeView.Refresh(_cubeModel);
            MatchScoreView.Refresh(_matchModel);
            CrawfordBadgeView.Refresh(_matchModel);

            PositionAnalysis analysis = _orchestrator.AnalyzeCurrent();
            _analysisModel.Update(analysis);
            AnalysisPanelView.Refresh(_analysisModel);

            InputController.IsLocked = _onlineInputLocked ||
                                       state.turn != HumanPlayer ||
                                       state.phase != GamePhase.Moving;
            InputController.Refresh();

            // Kural motorda: zar yalnizca sira basinda atilir. Dugme de buna gore pasiflesir;
            // aksi halde oyuncu "Zar"a basarak kural disi yeni zar hakki elde edebilir.
            // Cevrimici modda zar sunucuda atilir, dugme her zaman pasiftir.
            if (_rollButton != null)
            {
                _rollButton.interactable = _gameMode == GameMode.Local && _orchestrator.CanRoll;
            }

            // Kup ust sinirda (64) veya Crawford/olu kup ise teklif edilemez; dugme
            // pasiflesir. Aksi halde oyuncu "Kup"e basip hicbir sey olmamasini hata sanar.
            // Cevrimici protokolde kup yok; dugme cevrimici modda pasiftir.
            if (_doubleButton != null)
            {
                _doubleButton.interactable = _gameMode == GameMode.Local && _cubeModel.CanOffer;
            }
        }

        private void CreateControlBar()
        {
            var canvas = FindOrCreateCanvas();

            // Dugmeler sagdaki kenar cubugunda durur; tahtanin uzerine binmez.
            RectTransform bar = UiFactory.CreateRightAnchored("ControlBar", canvas.transform,
                                                              UiLayout.SidebarRightMargin,
                                                              UiLayout.ControlBarY,
                                                              UiLayout.SidebarWidth,
                                                              UiLayout.ControlBarHeight);

            _rollButton = AddButton(bar, "Roll", "Zar", 0f, 0f, 112f, () =>
            {
                if (DiceView != null)
                {
                    DiceView.ClearOverride();
                }

                _aiDiceVisibleUntil = 0f;
                _orchestrator.RollDice();
            });
            _doubleButton = AddButton(bar, "Double", "Kup", 120f, 0f, 112f, () =>
            {
                if (_orchestrator == null)
                {
                    return;
                }

                // EquityCubeAdvisor yalnizca PreRoll'da karar uretir; yanit tekliften
                // once hesaplanip orkestrator tarafindan saklanir.
                if (_orchestrator.TryOfferDouble())
                {
                    _pendingAiTake = _orchestrator.PendingOpponentTake;
                }
            });
            AddButton(bar, "EndTurn", "Turu bitir", 240f, 0f, 112f, () =>
            {
                if (_orchestrator == null)
                {
                    return;
                }

                // Yarim kalmis hamle varsa once uygula; aksi halde oyuncunun
                // oynadigi taslar kaybolur.
                if (InputController != null)
                {
                    InputController.CommitPartialMove();
                }

                if (_orchestrator.State.phase == GamePhase.Moving)
                {
                    _orchestrator.EndTurn();
                }
            });
            AddButton(bar, "NewGame", "Yeni oyun", 0f, 36f, 112f, () =>
            {
                if (_orchestrator == null)
                {
                    return;
                }

                // NewGame mac skorunu korur; mac bittiyse yeni mac baslatir.
                _aiDoubledThisGame = false;
                _pendingAiTake = false;
                _aiRollPending = false;
                _openingRollPending = false;
                _blockedAt = 0f;
                _aiDiceVisibleUntil = 0f;
                if (DiceView != null)
                {
                    DiceView.ClearOverride();
                    DiceView.ClearOpeningRoll();
                    DiceView.ClearCaption();
                }

                _orchestrator.NewGame();
            });
            AddButton(bar, "Undo", "Geri al", 120f, 36f, 112f, () =>
            {
                if (InputController != null)
                {
                    InputController.UndoStep();
                }
            });
            AddButton(bar, "Settings", "Ayarlar", 240f, 36f, 112f, () =>
            {
                if (SettingsPanelView != null)
                {
                    SettingsPanelView.SetVisible(!SettingsPanelView.IsVisible);
                }
            });
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

        private static Canvas FindOrCreateCanvas()
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null)
            {
                return canvas;
            }

            var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            return canvas;
        }

        private static T FindOrCreate<T>(string name) where T : Component
        {
            T found = FindFirstObjectByType<T>();
            if (found != null)
            {
                return found;
            }

            var go = new GameObject(name, typeof(RectTransform));
            return go.AddComponent<T>();
        }
    }
}
