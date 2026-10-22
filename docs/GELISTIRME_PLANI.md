# Galaxy 101 — Geliştirme Planı

Son güncelleme: 21 Ekim 2026. Durum ayrıntıları: `IMPLEMENTATION_NOTES.md`.

## Neredeyiz

| Alan | Durum |
|---|---|
| Ana menü görseli | Bitti: 7 panel, 5 dialog, 5 tema, 54 durum HTML ile denetlendi |
| Ana menü verisi | Mock: `IEconomyService`, `IFriendsService`, `ILeaderboardService`, `IProfileService`, `IRewardsService`, `ISettingsService`, `IShareService` |
| 3D masa | Faz 0–3 bitti: modeller, sahne, arka plan, HUD, motor tabanlı akış, botlar, ıstaka etkileşimi, menü bağlantısı |
| Kural motoru | `com.okey101.core` (motor bağımsız, `noEngineReferences`); sunucuda da aynen kullanılabilir |
| Sürüm kontrolü | **Yok.** Proje git deposu değil |

## İki hat, bir kural

İş iki bağımsız hatta yürür. Hatlar dosya sahipliğiyle ayrılır; ortak dosyalar yalnızca entegrasyon adımlarında değişir.

| | Masa hattı (Unity) | Sunucu hattı (çoğunlukla Unity dışı) |
|---|---|---|
| Dosyalar | `Assets/Galaxy101/Table/**`, `Assets/Galaxy101/Scripts/Editor/Table/**`, masa testleri | `Server/**`, `Shared/` (DTO), `Assets/Galaxy101/Scripts/Services/Http*` |
| Test | `scripts/Test-Unity.ps1` | `dotnet test` |
| Ortak / dokunma | `Bootstrap.unity`, `MainMenuController` (`PlayRequested` sınırı), ses ayarı, `IMPLEMENTATION_NOTES.md`, URP asset'leri | aynı liste |

Kurallar:
- Aynı anda yalnızca bir Unity batch süreci (proje kilidi). Editör açıkken batch test koşulmaz.
- Her faz kendi dalında; faz bitince testler yeşil + referans görüntü karşılaştırması + commit.
- `IMPLEMENTATION_NOTES.md` her hattın kendi bölümüne yazılır; birleştirmede çakışma tek yerde çözülür.

## Adım 0 — hemen (her şeyin önünde)

1. `git init`, `.gitignore` (Unity: `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `obj/`, `*.csproj`, `*.slnx`, `artifacts/`, `graphify-out/` önbellekleri), büyük ikililer için Git LFS (`*.glb`, `*.png`, `*.ttf`, `*.blend`).
2. İlk commit: mevcut durum (menü + masa faz 0–3).
3. Ana dal `main`; her faz `table/faz-N` ya da `server/faz-N`.

## Masa hattı

Sıra mantığı: **tasarımı hazır olan önce.** T01–T27 tasarlı; kural durumlarının tasarımı (aşağıdaki "Tasarım paketi") bu sırada yaptırılır, hazır olunca Faz 5'e girer.

| Faz | İçerik | Referans | Bağımlılık |
|---|---|---|---|
| 4 · Döngüyü kapat | El sonu tablosu + "Sonraki el · 15" (bot maçı: yalnız XP), bitiş anı şeridi (BİTİRDİN / X BİTİRDİ), TAŞ BİTTİ şeridi, masadan kalk kartı, bekleme + davet + 3-2-1 | T01, T02, T23, T24, T25 | yok |
| 5 · Kural arayüzü ✓ (22 Ekim 2026) | Okey seçici, ceza onayı + rozet, yerden alma kuralı görünümü, kural mesajları (TR/EN), puan dökümü çarpanları, maç sonu | **Tasarım paketi** | tasarım |
| 6 · Efektler ✓ (22 Ekim 2026) | Karma halkaları, deste dönüşü, kıvılcım/dalga, gösterge parlaması, yeni taş + geçerli per çizgileri, halka nabızları, konfeti/spot | spec/12, T03, T04, T23 | Faz 4 |
| 7 · Masa pencereleri | Ayarlar (görünüm, oyun, ses, bildirim) + kuşbakışı kamera + masa/ıstaka/taş görünüm değişimi; rakip kartı; sohbet; ikram/hediye paneli + roket | T07–T22 | ikram fiyatı Faz S3 ekonomisine bağlanır |
| 8 · Ses | Taş, çekme/atma, açma, bitiş, ikram, arayüz | — | ses varlıkları |
| 9 · Kabul | spec/15 listesi, 60 FPS hedef donanım, Windows paketi, mobil kararı | spec/15 | hepsi |

Tasarım paketi (Claude Design'a tek seferde): okey bağlama seçici; ceza onay kartı + ceza rozeti; yerden alınamaz/kullanmalısın durumları; kural mesajları TR/EN; el sonu puan dökümü (okeyle ×2, çiftle ×2, elden ×2, ceza); çiftle bitirme; maç sonu (el sayısı, toplam kazanan, çip/XP, tekrar/lobi); masadaki okeyi alma etkileşimi; menü → masa yükleme ve "masa açılamadı" durumu.

## Sunucu hattı

Yaklaşım: her fazda **sözleşme (OpenAPI) → sunucu + test → Unity `Http*Service` → mock ile yer değiştirme**. Mock'lar test ve çevrimdışı için kalır.

| Faz | İçerik | Notlar |
|---|---|---|
| S1 · Altyapı | ASP.NET Core (.NET 8/9) iskelet, Railway PostgreSQL + EF Core, misafir/cihaz hesabı, JWT + yenileme token'ı, sağlık kontrolü, Docker + Railway dağıtımı | Her şey buna bağlı |
| S2 · Profil + ayarlar | Bulut senkronu, ad/avatar doğrulama, **hesap silme** (mağaza zorunluluğu), KVKK aydınlatma | `IProfileService`, `ISettingsService` |
| S3 · Ekonomi | Değişmez işlem defteri (ledger), idempotent işlem anahtarı; istemci bakiye yazmaz | `IEconomyService`; masa bahsi/ikram ücreti buraya bağlanır |
| S4 · Günlük ödül | Sunucu saati, seri, claim | `IRewardsService` |
| S5 · Mağaza | Katalog sunucudan; önce gem ile alım, sonra Unity IAP + makbuz doğrulama | Simüle kumar sınıfı: nakde çevirme yok, yaş derecelendirmesi |
| S6 · Arkadaşlar | WebSocket ile çevrimiçi durumu, davet kodu, deep link, paylaşım | `IFriendsService`, `IShareService` |
| S7 · Liderlik | Yalnızca sunucu otoriteli maç sonuçları; bot maçları sayılmaz | Masa hattıyla birleşir (S8) |
| S8 · Çevrimiçi masa (+ bağlantı koptu kartı, T26) | Sunucu otoriteli oyun: `com.okey101.core` sunucuda koşar, istemci `LocalSession` yerine ağ oturumu kullanır; eşleştirme; Eşli/Katlamalı/Dereceli/Özel oda kuralları | Masa Faz 4–7 + S1–S6 |

Kimlik sırası: misafir → Google bağlama (Android Credential Manager, Windows OAuth PKCE) → iOS'ta Sign in with Apple zorunlu (App Store 4.8).

## Ürün kararları

- **Bot maçı çevrimdışıdır, yalnızca XP verir** (21 Ekim 2026). Çip kazanılmaz/kaybedilmez, bahis yok. Ekonomi (S3) yalnızca çevrimiçi maçlar için çip işler.
- Açık: maç uzunluğu (el sayısı), XP miktarları. Tasarım brief'i: `docs/TASARIM_BRIEF_MASA.md`.

## Entegrasyon noktaları

1. **Masa ↔ ekonomi (S3 sonrası):** el sonu çip/XP değişimi ve ikram ücretleri sunucu işlemine döner. Bot masası çevrimdışıdır ve yalnızca XP verir.
2. **Masa ↔ çevrimiçi (S8):** `TableGame` bir oturum arayüzüne ayrılır (`LocalSession` / ağ oturumu). Kural motoru iki tarafta aynı olduğu için istemci önizlemesi (ölçer, Aç doğrulaması) sunucuyla tutarlı kalır.
3. **Menü modları:** "Botlara karşı" yerel masa; diğer modlar S8'de eşleştirmeye bağlanır.

## Haftalık akış önerisi

- Masa hattı: Faz 4 → (tasarım paketi gelince) Faz 5 → Faz 6 → Faz 7.
- Sunucu hattı: S1 → S2 → S3 → S4; S3 bitince masa Faz 7'deki ikram ücreti bağlanır.
- Her faz sonu: testler, referans görüntü karşılaştırması, commit, `IMPLEMENTATION_NOTES.md` güncellemesi.
