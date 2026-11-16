# Tavla3D

Unity 6.3 (URP) ile yazılmış 2.5D tavla oyunu. Tek kişilik çevrimdışı oyun ve
sunucu yetkili (server-authoritative) çevrimiçi maçlar aynı istemcide çalışır.

## Özellikler

- **Çevrimdışı mod** — zar, sıra ve yapay zeka tamamen istemcide. ONNX tabanlı
  politika/değer ağı ile hamle analizi ve öneri.
- **Çevrimiçi mod** — zar sunucuda atılır, hamle sunucuda doğrulanır. Rakip
  bulunamazsa sunucu tarafı bot devreye girer.
- **Kural kodu tek kopya** — yasal hamle üretimi hem istemcide hem sunucuda
  **aynı** `Backgammon.Core` / `Backgammon.Rules.TavlaAI` derlemeleriyle çalışır.
  Kurallar hiçbir dile çevrilmez, ikinci kopya tutulmaz.
- **Kup (doubling cube)**, Crawford kuralı, maç skoru, açılış zarı.
- **Analiz paneli** — pip farkı, kazanma olasılığı, en iyi hamle önerisi.

## Gereksinimler

| Bileşen | Sürüm |
| --- | --- |
| Unity | **6000.3.8f1** (Unity 6.3) veya uyumlu |
| .NET SDK | 8.0 veya üzeri (MagicOnion kaynak üreticisi için) |
| Platform | Windows 10/11 (geliştirme), Android (hedef) |

## Kurulum

1. Depoyu klonlayın ve Unity Hub ile açın.
2. Unity ilk açılışta UPM paketlerini indirir (`Packages/manifest.json`):
   - `com.cysharp.magiconion.client.unity` 7.11.0
   - `com.cysharp.yetanotherhttphandler` 1.10.0
   - `com.cysharp.unitask` 2.5.11
   - `com.github-glitchenzo.nugetforunity`
3. NuGet paketleri `Assets/Packages/` altında hazır gelir (`Assets/packages.config`).
   Eksik olan olursa **NuGet → Restore Packages** menüsünü kullanın.
4. `Assets/Scenes/Main.unity` sahnesini açıp **Play**'e basın.

## Çevrimiçi mod

Oyun açıldığında sol altta çevrimiçi panel görünür:

```
Cevrimdisi
[Cevrimici oyna]  [Vazgec]  [Cevrimdisi]
```

- **Cevrimici oyna** → sunucuya bağlanır, eşleşme kuyruğuna girer.
- Rakip bulunamazsa (varsayılan 10 sn) sunucu tarafı bot ile maç başlar.
- **Cevrimdisi** → yerel moda döner.

Varsayılan sunucu adresi `OnlineGameBridge.DefaultServerAddress` içindedir
(`https://game.koseli.com.tr:5002`). Kendi sunucunuzu kullanmak için bu değeri
veya Inspector'daki **Server Address** alanını değiştirin.

> **Android'de TLS zorunludur.** Android düz HTTP/2'ye (cleartext) izin vermez;
> `http://` adresleri yalnızca masaüstü geliştirmede çalışır.

## Proje yapısı

```
Assets/
  Backgammon.UI/          Çevrimdışı oyun: tahta, zar, kup, HUD, analiz, girdi
	Scripts/
	  Bootstrap/          GameBootstrap — sahnenin tek giriş noktası
	  Board/ Dice/ Cube/ Hud/ Analysis/ Settings/ Input/
	  Infrastructure/     UnityMainThreadDispatcher, UiFactory, ONNX arka ucu
	Editor/               SceneBuilder, BuildScript
	Tests/                EditMode + PlayMode testleri
  Backgammon.Online/      Çevrimiçi mod köprüsü (asmdef: TavlaAI.Online)
	Scripts/              MagicOnion kurulumu, IOnlineGame uygulaması, DTO ↔ motor eşlemesi
	Tests/                EditMode + PlayMode testleri
  Plugins/                Backgammon.Core / .UI / .Client / .Rules.TavlaAI + onnxruntime
  StreamingAssets/        ONNX modeli ve bearoff tabloları
  Packages/               NuGetForUnity ile indirilen paketler
```

## Testler

```powershell
# EditMode
Unity.exe -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults results.xml

# PlayMode
Unity.exe -batchmode -nographics -projectPath . -runTests -testPlatform PlayMode -testResults results.xml
```

Mevcut durum: **20/20 EditMode**, **46/46 PlayMode**.

## Lisans

Bu depo için henüz bir lisans dosyası eklenmemiştir. Aksi belirtilene kadar tüm
haklar saklıdır.
