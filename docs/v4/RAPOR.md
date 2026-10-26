# Galaxy 101 · v4 · rapor

> Tarihsel v4 raporu. Sonraki Railway denemesinin ardından kullanıcı yerel geliştirmeye döndü; Railway askıda. Güncel düzeltmeler ve doğrulama sınırları: [LOCAL_RELIABILITY.md](../LOCAL_RELIABILITY.md).

Görev: `Design/PROMPT_v4_OTONOM_GELISTIRME.md` (Faz 0 → Faz 4). Paket: `Design/Galaxy101_ClaudeCode/` v0.4.0. Çalışma 25–26 Ekim 2026; bütün commit'ler yerel (`main`), push / deploy / Railway yok. Ayrıntılı günlük: `ILERLEME.md`; kararlar `KARARLAR.md`; eksikler `EKSIKLER.md` (+ hat dosyaları); Unity farkları `UNITY_FARKLARI.md` (+ hat dosyaları); durum tablosu `DURUMLAR.md`; fark matrisi `FARK_MATRISI.md`; uçtan uca `FAZ3.md`.

## 1 · Ne yapıldı

| Faz | Ne | Commit'ler |
|---|---|---|
| 0 | Taban testler + taban commit; paket Node'da (106/106, 26/26); 992 referans karesi (144 durum × ana/tekrar/EN + 4 tema) + yerleşim ve metin dökümleri; fark matrisi; mimari kararlar | `39bed5b` taban, `753287e`, `fdbb251` … |
| 1-A | `rules.js` + `bots.js` C#'a satır satır (`Okey101.Core.V4`), vektör koşucusu, mulberry32 + sürümlü kayıt (`101-v4` / `mulberry32-v4`; eski kayıtlar eski motorla açılır), 72 maçlık JS eşdeğerlik testi | birleşme `9c42c73` |
| 1-B | Sunucu v4: protokol merkezi (53 C2S, 45 S2C, 94 hata kodu), adım adım oyun mesajları, gizli bilgi, D-24 takvimi, ekonomi R-90…R-98, dereceli, sohbet, kopma/dönme; maç akışı taşımadan bağımsız paket `com.galaxy101.table` (sunucu ve Unity aynı kodu koşar, K-09); 26 paket akışı xUnit | birleşme `4b7e976`, `d949edc` |
| 1-C | 103 GLB `Models/v4/` altında, malzeme/ışık/yerleşim eşlemesi, envanter araçları | birleşme `e372d47` |
| 1-D | 1017 metin anahtarı TR/EN, temalar, fontlar (Nunito-700 dahil), 69 ikon, 75 lig rozeti, 11 imleç, 28 efekt + 2 müzik, ses olay kataloğu | birleşme `321d118`, Unity içe aktarma `3daf284` |
| 2 | Unity durum yönlendiricisi (`-g101State/Seed/Theme/Lang/Ft/Size/Fps`, D-60/61/62), ortak saat, yerleşim/metin dökümü, karşılaştırma araçları; menü pencereleri, giriş, lobi (v4 istemci), Dereceli ekranları (v4 dereceli mesajları), ses + müzik + imleç, masa v4 çalışma zamanında (istemci modeli, çevrimdışı ev sahibi, ağ oturumu, yeniden bağlanma kartı, kaldırılan durumlar) | birleşmeler `448426f`, `9606a80`, `d7eb810`, `88b9a35`, `0a86a4b`, `32c5b04`, `1f23036`, `3683cfa`, `f6ca9dd`, `2313f50` + düzeltmeler |
| 3 | Docker imajı + PostgreSQL, yerel sunucu, canlı sunucu testleri, çözünürlükler ve kare hızı | `451b53d`, `a319068`, `9148e1f` |

Paralel çalışma: Faz 1'de dört hat (A/B/C/D), Faz 2'de beş kod hattı (menü, Dereceli, lobi, ses+imleç, masa) ayrı git worktree'lerinde ve dallarında (`v4/*`); Unity yalnız ana kopyada ve sırayla çalıştırıldı (K-11).

## 2 · Testler

(Son koşu sayıları §7'de; komut çıktıları `ILERLEME.md` günlüğünde.)

## 3 · Durum tablosu özeti

(§7'de güncel sayılarla.)

## 4 · Bilinçli farklar

`UNITY_FARKLARI.md` (UF-01…04) ve hat dosyaları (`menu/`, `lobby/`, `ranked/`, `audio/`, `table/` → toplam 65 kayıt). Başlıcaları:
- UF-01 Development oyuncusu filigranı (yönlendirici yalnız Development/editörde; karşılaştırmada maskelenir).
- UF-02 S01 açılış yüzdesi ve ipucu yükleme olaylarına bağlı.
- UF-03 "GALAXY" yazısı Unity'de tema görseli (CSS degrade/kabartma UI Toolkit'te yok).
- UF-04 UI Toolkit'in filtre render ağacı bir test sırasında motor günlüğü yazıyor (oyuncuda yok).
- Duruma bağlı: 12/S07 gerçek monitör çözünürlüğü, 21/22 prototipteki sabit DEMO bakiye metni yerine gerçek bakiye, S14/S15 paketin yolu davet panelini açmıyor (EK-007), lig rozetleri ve imleçler SVG yerine PNG (SVG filtre/metin içe aktarılamıyor, EK-404).

## 5 · Eksikler (gruplu)

Toplam kayıtlar ve dağılım `EKSIKLER.md` "Hatların eksik kayıtları" tablosunda. Öne çıkanlar:

**Kullanıcının kesinleştirdiği kararlar**
- EK-211: Bekleme odasında sahiplik ilk insan oyuncuya geçer.
- EK-305: Mevcut cilalı modeller; v4 renkleri ve yerleşimi.
- EK-413: Gerçek taş kayıtları.
- K-10: "Botlara karşı" açık kalır.
- EK-501/502/509 eski masa hattı notlarıdır; güncel canlı akış kanıtı ve sınırları yerel doğrulama belgesinde takip edilir.

**Dış hizmet gerekiyor (D)**
- Gerçek ödeme (mağaza makbuz doğrulaması), Google/Apple girişi, e-posta ile şifre sıfırlama: sunucu doğrulama sınırına kadar test edildi; sağlayıcı yok.
- EK-216: Paket akışlarındaki demo kısayolları (sahte arkadaşın kabulü, dereceli demo rakipleri) gerçek hesaplarla kuruldu.
- Dereceli: parti daveti onayı (EK-702), dereceli maç sayısı/geçmişi sunucuda yok (EK-703), kopunca dereceli ekranına dönüş (EK-715).

**Tasarımda / pakette yok (T, P)**
- Paket tutarsızlıkları: D-61 ↔ `g101-state.js` donma anı (EK-005), Masa sekmesi EN çevirisi (EK-006), S14/S15 yolu (EK-007), yinelenen vektör kimlikleri (EK-101), `gAdd.side` L/R ↔ pre/post (EK-201), SRV-25 metnindeki 4000 (EK-202), duvar saatine bağlı geri sayımlar (EK-003), yüklemeye bağlı açılış (EK-004).
- Sunucuda paketin tanımlamadığı uçlar: `skinSet`, `friendRemoved`, `E_REQUEST_PENDING`, Galaksi çerçevesi kuralı (EK-206…209).

**Unity kısıtı (U)**
- SVG filtre/metin içe aktarılamıyor → PNG @1x/@2x (EK-404); glTFast iridescence/sheen desteklemiyor (EK-304); v4 zar modeli aynalı yüzler (EK-306); font atlası yenileme (EK-409, yapıldı).

## 6 · "Pakette yok" sunucu özellikleri (silinmedi)

Hızlı eşleşme kuyruğu `queue.join` (solo/pairs/doubling/ranked), `Game:HandsPerMatch` 4, `Game:Stakes`, `Game:SoloPayout`/`RakePercent`, kodlu oda `room.*`, eski lobi `table.*` ve tur-bütün `match.*` (eski istemci ve eski kayıtlar), dolu eski masanın kendiliğinden sayması, bekleme odasında sahiplik devri (eski yol), `session.replaced`, küfür maskesi/engelleme/şikayet/moderatör, izleyici sınırı ve hız sınırları (eski yol), sezon geçişi/ödülü, gerçek para mağazası, hesap bağlama, Apple girişi, token yenileme, profil istatistik/rozet/geçmiş, bulut ayarları, KVKK onayı, paylaşım bağlantısı, eski maçların yeniden başlatmada geri kurulması. Kaynak: `matris/hat-b.md` "Pakette yok", `PROTOKOL_ESLEME.md`.

## 7 · Son durum

(Son doğrulama koşusundan sonra doldurulur.)

## 8 · Railway'e hazırlık (dağıtım yapılmadı)

| Konu | Durum |
|---|---|
| Docker imajı | `docker build -f Server/Dockerfile .` yerelde tamam; `.dockerignore` ve `railway.json` `watchPatterns` paket kaynaklarını içerecek şekilde düzeltildi (`451b53d`). Bağlam depo kökü. |
| Sağlık ucu | `GET /health` (veritabanına bağlanabiliyorsa 200); `railway.json` `healthcheckPath: /health`, zaman aşımı 60 sn. |
| Replika | 1 (maçlar ve v4 masaları bellekte; v4 masaları yeniden başlatmada geri kurulmaz, tuttukları bahis açılışta iade edilir — EK-210). |
| Veritabanı ve göçler | PostgreSQL; `DATABASE_URL` (Railway) ya da `ConnectionStrings:Game`. EF Core göçleri açılışta uygulanır (yerel imajda doğrulandı). Göçler: `InitialCreate`, `PasswordLogins`, `LobbyTables`, `ModerationAndSearch`, `JoinInProgress`, `RankedSeasons` (+ v4 defter anahtarları `v4:{table}:…` mevcut tablolarda). |
| Gizli anahtarlar | `Auth__SigningKey` (zorunlu, base64 ≥ 32 bayt; `openssl rand -base64 48`); Google/Apple istemci kimlikleri; mağaza (`Store__GooglePackageName`, `Store__GoogleServiceAccountJson`, `Store__AppleBundleId`, `Store__AppleRootCertificate`); e-posta sağlayıcısı (şifre sıfırlama kodu — paket DEMO-15, gerçek gönderim yok). |
| Ortam değişkenleri | `Game__PublicBaseUrl`, `Game__PrivacyPolicyUrl`, `Game__PrivacyPolicyVersion` (önerilir), oyun ayarları `Game__*` varsayılanlarıyla (`Infrastructure/Options.cs`); `ASPNETCORE_HTTP_PORTS=8080` imajda. |
| CORS | İstemci Unity oyuncusu (tarayıcı değil); WebSocket bilet ile (`POST /v1/realtime/ticket` → `/v1/ws?ticket=`). Paylaşım sayfası `/p/{kod}` aynı köken. Tarayıcı istemcisi eklenecekse CORS politikası yazılmalı. |
| İstemci adresi | Unity `GalaxyApi.DefaultServer` / `-galaxyServer <url>` / `GALAXY101_SERVER`; Railway adresi yayından önce derlemeye verilmeli. |
| Sürüm | Uygulama 0.4.0; `/v1/info` → 0.4.0, `ruleVersion` 101-v4. |
