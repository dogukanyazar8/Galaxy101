# Faz 2 · Lobi · rapor

Dal `v4/lobby` (worktree `C:/GD/Okey101-v4/lobby`, taban `main` 59aa81c). Hedef: Unity Klasik lobisi (masa listesi, Masa aç formu, PIN ve kod pencereleri, bekleme odası, davet paneli, İzle, Oyuna katıl) v4 prototipiyle (`G101-Lobi.dc.html`, S09–S15 ve kataloğu olmayan lobi görünümleri) birebir. Unity çalıştırılmadı; derleme `tools/v4/unity_compile.py` ile, Unity'de çekim ve karşılaştırma orkestratörde.

## 1. Özet

- **Birleştirmeler:** `v4/data` (df74ade: v4 metinleri, ikonlar, Nunito-700), `v4/server` (bad019d: Hat B bitti, `docs/v4/hat-b/RAPOR.md` var), `main` 6565e6a (008d395: sunucu, veri, 3D, ses, dereceli, menü hatları; ses hattının `LobbyScreen.Flash` → `GalaxyAudio.Toast` ve `PressSink` → imleç kancaları korundu).
- **Ekranlar** (`Assets/Galaxy101/Scripts/Screens/LobbyScreen*.cs`): öğe adları prototipin `data-g101-id`'si (D-62; döngüdeki öğe prototipin sırasını taşır, aynı ad prototipte olduğu gibi tekrar eder), bütün metinler `strings_v4.json` `lobby.STR.*` anahtarından (`S("sit")`), hata metinleri `errors[kod].key`'den, ikonlar `Icons/v4`'ten (geometriyle eşlendi).
- **Sunucu:** `LobbyClient` v4 paket protokolünü konuşuyor (Hat B hub'ı); `ILobbyBackend` dikiş olarak kaldı ve v4 için genişledi (`InviteAnswered`, `InviteAsync(seat, recent)`, `CancelInviteAsync`, `RecentPlayer.PlayedAt`).
- **Referans:** paketin S14 / S15 kareleri davet panelini göstermiyor (EK-801); doğru yolla çekilen S14 / S15 ve 20 ek lobi görünümü `docs/v4/lobby/ref/` (png + layout.json + text.json, `compare_states.py` biçimi) ve çekim betiği `capture_lobby_refs.py`.
- **Derleme:** `Galaxy101.PlayMode.Tests: OK (0 errors)` (her commit öncesi).

Yeni dosyalar: `LobbyScreen.Strings.cs` (v4 metin anahtarları, hata kodu eşlemesi, genTR, süre biçimleri), `LobbyScreen.Dialogs.cs` (pencereler; `Room.cs`'ten ayrıldı). `.meta`'lar `make_meta.py` ile.

## 2. Commit'ler

| Commit | İçerik |
|---|---|
| `df74ade` | `v4/data` birleştirmesi |
| `5ec76f3` | Liste, Masa aç, pencereler, bekleme odası, davet paneli v4 prototipine; D-62 adları, `strings_v4` metinleri, v4 ikonları; `ILobbyBackend` genişlemesi; demo arka uç (prototipin SEED / FRIENDS verisi) ve testler yeni adlara |
| `6a16222` | Bakiye değişince açık form da yeniden çizilir; bekleyen davetin sayacı `30 − geçen tam saniye` |
| `bad019d` | `v4/server` birleştirmesi |
| `008d395` | `main` birleştirmesi (6565e6a) |
| `dd24b37` | `LobbyClient` v4 protokolü; canlı sunucu testi `tableJoinCode` ve Başlat ile |
| `f451720` | Bu belgeler ve referans kareleri (main'deki kırmızı Hemen oyna testinin gerekçesi commit mesajında) |
| `3e0ceb4` | Commit tablosu |
| (main fd64806 birleştirmesi) | run8 sonrası `main` |
| `71deb03` | S09 süzgeç satırı kayması (tam metin genişlikleri); gelen davet (`inviteIn` / `inviteRespond`) |
| (son) | Bu rapor: run8 sonucu |

## 2a. run8 sonucu ve sonrası

Orkestratörün Unity koşusu run8 (`artifacts/v4/unity/run8`, main fd64806): S10, S11, S12, S13 eşleşti; S14 / S15 bilinçli fark olarak kaydedildi (ana liste EK-007 = EK-801). S09: 148/155 metin, 7 metin 3 px'ten fazla kaymış (Katlamalı +4, Tümü +4, 1K +5, 5K +5, 10K +6, 50K +6, 100K +7), UI pikseli %12,74 (sınır %12).

- **Neden:** UI Toolkit ölçülen her metin kutusunu tam piksele yukarı yuvarlıyor (tarayıcı kesirli genişliği tutuyor); süzgeç satırındaki 13 kısa etiket satır boyunca yarım piksel kadar birikiyor (sekme kutuları 103,41 → 104, 95,33 → 96 …).
- **Düzeltme (71deb03):** segment seçenekleri (tip ve bahis sekmeleri; formdaki, kurallardaki ve davet panelindeki segmentler de), süzgeç çipleri (101↑ işaretiyle) ve üst çubuktaki Kodla katıl / Masa aç etiketleri genişliklerini aynı fontla on kat büyük ölçen gizli bir yoklama etiketinden alıyor (yuvarlama hatası onda bire iner; font + boyut + metin başına önbellekte). Ölçüm lobi açıldıktan sonraki ilk panel güncellemesinde, giriş solmasının (.3 s) içinde; sonraki yeniden çizimler önbellekten aynı karede.
- **Beklenen:** süzgeç satırı ±1 px; UI pikselindeki kalan payın bir kısmı arka plandaki gezegen (Unity'de daha büyük / parlak; menü-arka plan hattı) ve yazı kenar yumuşatması — bu hattın dosyalarında değil. Unity'de yeniden çekimle doğrulanmalı.

## 3. Durum ve görünüm başına değişiklikler

### S09 · Lobi masa listesi
- Üst çubuk: geri (v4 `form-back` ikonu), "101" rozetinin üst ışığı (`inset 0 1px 0`), Klasik başlığı `26px/1.1`, bakiye kutusunun iç gölgesi (`inset 0 2px 0`); adlar `lobby.back`, `lobby.text.classic`, `lobby.chipsLabel`, `lobby.buy`, `lobby.openCode`, `lobby.openCreate`.
- Süzgeçler: Katlamalı çipinin işareti "101↑" (eskiden "×2"); seçili çipin halkası artık genişliği büyütmüyor (dolgudan geri alınıyor, referansta çipler 7 px kayıyordu); sekme sayıları `lobby.o.n[i]`.
- Kartlar: "Katlamalı" rozeti ("×2 Katlamalı" değil) ve Eşli + Katlamalı masada ikinci mor rozet "Ortak eşiği yükseltir" / "Yalnız rakip yükseltir" (R-64, `TableSummary.PartnerRaises`); kart düğmeleri arası 6 px; durum "Oyunda · El h/n" `sPlay`.
- Sıralama ve "Boş yer var": oynanan masada boş koltuk da alınabilir sayılır (`renderVals` `free = players.includes(null)`), sunucu bot koltuklarını `OpenSeats` ile verir (R-65). main'de kırmızı olan `HemenOynaOpensTheListWithFiltersSearchAndBalanceRules` ("Dolu Masa" beklenirken "Kadıköy") bununla geçer: Kadıköy (oynanıyor, boş koltuk) "oyunda + yer var" sırasına çıkar, dolu masa en sona kalır; test prototipin kuralını zaten bekliyordu, değiştirilmedi.
- Özel masalar (kendi masan da) listede yok (`vis = tables.filter(t => t.vis !== 'private')`).
- Bakiye yetmeyince tost `lowB` ("5.000 bahis için 4.000 çip daha gerekiyor."); eski "Yetersiz bakiye · X çip eksik" metni prototipte yok.
- Demo arka uçtaki tip hatası düzeldi (Tekli masalar `solo` yazılıyordu, "Tekli 0" görünüyordu).

### S10 · Masa aç
- Açılış değerleri `openCreate()`: ad `genTR(ilk ad) + " masası"` ("Doğukan’ın masası"; eskiden "Doğukan Y.’in masası"), bahis 5K karşılanabiliyorsa 5K, değilse karşılanabilen en yüksek, hiçbiri değilse 1K + uyarı; tip süzgeçteki tipten.
- Katlamalı açıklaması `rKatD` ("Sonradan açan önceki açılışı geçmeli: 101 → 102 → 103, 5 → 6 → 7 çift"); Eşli + Katlamalı'da yeni satır `lobby.create.katlamaPartner` "Ortağın açılışı eşiği yükseltsin mi?" (varsayılan açık, 18 px girintili, 15 px başlık) → `TableOptions.PartnerRaises`.
- Bahis başlığı taban çizgisinde hizalı (bakiye satırı 2 px aşağıda; katmanlar referansa göre 2 px yukarıdaydı); seçili katmana `0 0 18px --gl` parıltısı; katman üzerine gelince `--pnH`.
- Kaydırma çubuğu yer kaplamıyor (UF-802): satırlar 138–818 arasında (eskiden 11 px sola kaymıştı).
- Özet sütunu yeni düzen: ad + mod satırı (özelde kilit), kimlik rozetleri (Tekli/Eşli, Özel, Şifreli), dört hücreli istatistik şeridi, "MASA KURALLARI" listesi (yeşil/sönük nokta, sağda değer; Katlamalı "101 → 102 → 103", eşlide ortak kuralı Evet/Hayır), bakiye satırı (`#F4F1E8`, yetmezse yalnız bakiye `#FF8A94`), CTA "Masayı oluştur · 5.000" ya da "Yetersiz bakiye", Vazgeç. Eski 2 × 2 kutular ve rozet bulutu kalktı.
- Anahtarlar düğmesi .22 s `cubic-bezier(.3,1.5,.5,1)` ile kayıyor; kurallar kutusu lbFade; arka plan bulanık (6 px).
- "Masan açıldı" tostu kalktı (prototipte yok); özel masada paylaşım penceresi açılır.

### S11 · Şifreli masa PIN
- Kutu halkaları prototipin dört hâli (hata kırmızı, sıradaki altın + `0 0 14px --gl`, dolu `--bd`, boş iç gölge); dört rakam girilmeden "Gir" soluk ve çalışmaz, otomatik gönderim yok (Enter ya da Gir).
- Yanlış PIN: kutular kırmızı, PIN temizlenir, lbShake, "Yanlış şifre. Tekrar dene." Sunucu PIN'i kabul edince "Masaya bağlanılıyor…" (UF-805).

### S12 · Kodla katıl
- Giriş büyük harfe çevrilir, yalnız A–Z 0–9 ve "-" , en çok 7 karakter; gönderimde tiresiz 6 karakter şartı ("Kod 6 karakter olmalı"); kod `GX-XXXX` olarak gider. Sunucu bilmiyorsa satır içi "Bu koda ait açık bir masa bulunamadı." (eskiden 4 karakterlik kod da kabul ediliyordu).
- Kod penceresi sunucu yanıtına kadar açık kalır (eskiden önce "bağlanılıyor"a geçip hata ile geri dönüyordu).

### S13 · Bekleme odası (kurucu)
- Özet paneli prototipteki gibi: başlıkta ÖZET + kimlik rozetleri, dört hücreli istatistik şeridi, kalan yükseklikte MASA KURALLARI, ödül kutusu (eşlide "kişi başı"), ipucu, Başlat. Eski 2 × 2 kutular ve rozetler kalktı.
- Koltuk yüzünün `0 0 0 3px --pnS` halkası, v4 tacı (26 × 20, üstten −14), lbPop .4 s koltuk dolunca; koltuk adları ekrandaki konuma göre (`lobby.z.ini[0]` altta).
- Ayrılırken tost: kurucuya "Masan kapatıldı", misafire "Bekleme odasından ayrıldın" (eskiden hep ikincisi). Bot eklenince "Bot koltuğa oturdu", oyuncu oturunca "X masaya oturdu" (yeşil), çıkarılınca "X masadan çıkarıldı" (botta da adıyla).
- Durum yolu: demo arka uç prototipin `enterWait($tbl:mine, true)` sonucunu kurar; sahibi koltukta olmayan odada taç 0. konumda (UF-813).

### S14 / S15 · Davet (Arkadaşlar / Son oynadıkların)
- Panel ölçüleri ve satırlar referansa göre; sekme sayısı "çevrimiçi/toplam" (eskiden oyundakiler de sayılıyordu, "6/8" → "4/8"); satırlar yalnız duruma göre sıralı (sunucunun sırası korunur; eskiden ada göre de sıralanıyordu).
- Başlık ikonu prototipteki gibi kırpılıyor (artı çizgisi görünüm kutusu dışında); sekme kabının iç gölgesi; satır halkası beklemede altın, masada yeşil.
- Düğme hâlleri: Davet et, Bekleniyor n (döner halka + kısalan çubuk, tıklayınca iptal), Masada (yeşil zemin + onay), Reddetti, Tekrar davet, Oyunda, Çevrimdışı, Masa dolu.
- Son oynadıkların alt satırına "· bugün / dün / 3 gün önce" (veri varsa, EK-804); "Son görülme 2 sa / dün / 3 gün".
- Davet, panelin açıldığı koltuğa gider (`inviteSend.seat`); reddetme / süresi dolma `inviteResult` ile satıra ve tosta yansır.
- Durum yönlendirici S14 / S15'te paneli açar (paketin yolu açmıyor, EK-801).

### Kataloğu olmayan görünümler
| Görünüm | Değişiklik | Referans |
|---|---|---|
| Boş liste | `lobby.text.emptyT/emptyB`, `lobby.resetFilters`, `lobby.openCreate#2`, lbFade .3 s | `ref/Xempty` |
| Süzgeçler açık | çip halkası dolgu dengeli | `ref/XfilterOn` |
| Masaya bağlanılıyor | gövde yalnız masa adı (eskiden "ad · bahis"); oda 850 ms dolmadan açılmaz (eskiden oda durumu gelir gelmez açılıyordu) | `ref/Xconnect` |
| Masa doldu / kapandı | ikonlar v4; masa oynanıyor ve izleyiciye açıksa ikinci düğme "İzle"; "Benzer masa bul" aynı tipte, karşılanabilen, bahsi en yakın masa (eskiden yalnız aynı bahis) | `ref/Xfull`, `ref/XfullWatch`, `ref/Xclosed` |
| Paylaşım (özel masa) | kopyala / paylaş düğmelerinde ikon, "Paylaş" sonrası "Davet metni kopyalandı" etiketi (tost yerine), şifre kutucuğu formdaki PIN'i gösterir | `ref/Xshare` |
| Özel masanın odası | kod kutusu (kesik kenar, "Kodu kopyala", paylaş) | `ref/XwaitPriv`, `ref/XinvPriv` |
| Eşli + katlamalı oda | Takım A/B rozetleri, kurallarda ortak satırı | `ref/XwaitPairs` |
| Misafirin odası | Başlat yerine dönen halka + "Oda sahibi bekleniyor", Bot ekle yok | `ref/XwaitGuest` |
| Bekleyen davet | Bekleniyor 29 + çubuk, koltuk çipi seçili, "· 1 davet bekliyor" | `ref/X14pend` |
| Masa aç varyantları | eşli + katlamalı, şifre hatası + boş ad, uyarı, kurallar açık | `ref/XCpairs`, `ref/XCpairsScroll`, `ref/XCwarn`, `ref/XCrules` |
| İzle | kart düğmesi göz ikonu; şifreli masada önce PIN; sunucu masayı `snapshot{spectator}` ile verir → `MatchStarted` | — |
| Oyuna katıl (R-65) | yeşil "Katıl"; bahis o an; `snapshot{screen: "table"}` → `MatchStarted` | — |
| Tost | `lobby-toast`, ses hattının `GalaxyAudio.Toast` kancası korundu | `ref/Xtoast` |

## 4. Sunucu bağlantısı (v4)

`LobbyClient` (`Assets/Galaxy101/Scripts/Online/LobbyClient.cs`) Hat B'nin hub'ıyla konuşur; eski `table.*` / `lobby.*` mesajları artık kullanılmıyor (sunucuda eski istemci için duruyorlar).

| Ekranın isteği (`ILobbyBackend`) | v4 mesajı | Not |
|---|---|---|
| `SubscribeAsync` / `Unsubscribe` | `lobbySubscribe` / `lobbyUnsubscribe` | yeniden bağlanınca yeniden abone |
| `Tables` | `lobby{tables}`, `lobbyTable{table}`, `lobbyTableRemoved{id}` | `V4Table` → `TableSummary`: `mode` solo/pairs → single/pairs, `bet`, `katlama`, `katlamaPartner ?? true`, `rules` (`chat` open → on, `penalties` → iki ceza anahtarı, `isle` → İşleme), `status` (countdown = bekleyen), `handNo`, oynanırken bot koltukları `OpenSeats` (dereceli / botlara karşı değilse) |
| `CreateAsync` | `tableCreate{name, mode, private, pin, hands, bet, katlama, turnSec, rules, katlamaPartner?}` | `katlamaPartner` yalnız eşli + katlamalı; ceza eşlemesi EK-811 |
| `JoinAsync` | `tableJoin{tableId, pin?, seat?}` ya da `tableJoinCode{code, pin?}` | oynanan masada devralma (R-65) → `snapshot{screen: "table"}` |
| `WatchAsync` | `tableSpectate{tableId}` | PIN alanı yok (EK-807) |
| `LeaveAsync`, `AddBotAsync`, `KickAsync`, `StartAsync` | `tableLeave`, `seatAddBot{seat}`, `seatKick{seat}`, `tableStart` | kurucu koltuktayken masa kendiliğinden saymaz: Başlat gerekir |
| `Room` | `tableState{table}` (benim koltuğum varsa), `countdown{n, cancelled}` | `YourSeat` = koltuklarda benim kimliğim; `Code` yalnız özel masada |
| `RoomChanged(Closed)` | `leftTable{reason}` → "left"/"disconnect", `kicked` → "kicked", `tableClosed{reason: "ownerLeft"}` → "Masa kapandı" penceresi, soket kopması → "offline" | bahis iadesi `balance{reason: "betRefund"}` (R-90, D-11) |
| `RoomChanged(Closed + MatchId)` | `matchStart{table}`, `snapshot{screen: "table", spectator?}` | `MatchId` = masa kimliği; menü `MatchStarted` ile masayı açar |
| `Chips` / `ChipsChanged` | `balance{chips, gems}` → `GalaxyOnline.Apply` | bahis oturunca düşer (R-90) |
| `InviteAsync` | `inviteSend{userId, seat?, recent?}` | 30 sn |
| `InviteAnswered` | `inviteResult{userId, result}` | declined → Reddetti, expired → Tekrar davet |
| `CancelInviteAsync` | (`inviteCancel{inviteId}`) | `inviteId` ack'te, `AckMessage` taşımıyor: yalnız yerel (EK-812) |
| `FriendsAsync`, `RecentAsync` | HTTP `/v1/friends`, `/v1/players/recent` | "birlikte oynama zamanı" yok (EK-804) |

Hata kodları: ekran v4 kodlarını (`E_PIN_WRONG`, `E_TABLE_FULL`, `E_TABLE_CLOSED`, `E_CODE_FORMAT`, `E_CODE_UNKNOWN`, `E_NO_CHIPS`, `E_ALREADY_SEATED`, `E_TABLE_PLAYING`, `E_SPECTATE_OFF`, `E_SEAT_TAKEN`, sessizler `E_NOT_OWNER`, `E_TABLE_NOT_FULL`, `E_STATUS`, …) `strings_v4.json` `errors`'taki türe göre gösterir (pencere / satır içi / sallama / tost / sessiz); sahte arka uç ve eski sunucu kodları `LobbyScreen.V4Code` ile aynı yere düşer.

Gelen davet (71deb03): `inviteIn` → `ILobbyBackend.InviteReceived` → lobi açıkken davet penceresi ("X seni masaya çağırıyor", `passBody` masa · bahis); Katıl → `inviteRespond{accept: true}` "Masaya bağlanılıyor…" üzerinden bekleme odasına, Sonra → `inviteRespond{accept: false}`; süresi geçmişse `E_INVITE_EXPIRED` tostu.

Bu hattın dosyaları dışında kalan işler: `AckMessage`'ın dönüş alanları (EK-812), lobi kapalıyken menünün gelen daveti açması (EK-813), `tableSpectate`'e PIN (EK-807), son oyuncularda zaman alanı (EK-804, sunucu).

## 5. Testler

`Assets/Galaxy101/Tests/PlayMode/LobbyTests.cs` yeni adlara ve akışlara geçti (Unity'de koşulmadı; derleniyor):
- Kimliği tekrar eden öğeler kapsayıcı adıyla tıklanır (`Click(root, "lobby-types", "lobby.o.pick[2]")`); kart düğmesi `Click(root, "lobby-card-<id>", "lobby.b.go[0]")`.
- Otur → önce "bağlanılıyor" ve liste; oda bekleme süresinden sonra. Koltuk adları konuma göre (koltuk 1'de oturunca koltuk 2/3 = konum 1/2).
- PIN: dört rakamdan önce Gir bir şey yapmaz; yanlış PIN kutuyu temizler. Kod: "7q2m" artık geçersiz (6 karakter şartı).
- Masa aç: "Doğukan’ın masası", 5K varsayılan, eşli + katlamalı'da ortak satırı ve `PartnerRaises = false` gönderimi, paylaşımda PIN.
- Davet: sekme sayısı "1/3", davet açılan koltuğa (`(mert, 1)`), `inviteResult` declined → "no", oturunca beklemeden düşer.
- Canlı sunucu testi (`LiveServer`, sunucu yoksa Inconclusive): ikinci misafir `tableJoinCode` ile (yanlış PIN `E_PIN_WRONG`), kurucu Başlat'a basar.
- main'de kırmızı olan `HemenOynaOpensTheListWithFiltersSearchAndBalanceRules` §3 S09'daki kuralla geçer (beklenti değişmedi).
- Yeni `IncomingInviteJoinsThroughInviteRespond`: `inviteIn` davet penceresini açar, Katıl `inviteRespond(accept)` gönderir ve bekleme odası gelir.

## 6. Referans kareleri

`docs/v4/lobby/ref/` — `capture_lobby_refs.py` prototipin kendi durum yönlendiricisini, `states.json`'a eklenen yollarla sürer (Playwright, Chrome, seed 42, Satürn, TR):
- `S14.*`, `S15.*`: davet paneli açık (`deep {wait: {inv: {tab: 'fr' | 'recent', q: '', seat: null}}}`), paketin S14 / S15'inin yerine kullanılabilir: `uv run --with pillow python tools/v4/compare_states.py <unity> --ref docs/v4/lobby/ref`.
- `X*`: kataloğu olmayan 20 görünüm (§3 tablosu). EN için: `… capture_lobby_refs.py <out> en`.

## 7. Kalan farklar ve Unity'de bakılacaklar

- Bilinçli farklar `UNITY_FARKLARI.md` UF-801…813 (gradyan yazı, gizli kaydırma çubukları, box-shadow taklitleri, ikon kalınlıkları, harf aralığı, satır kırma); kaynağı olmayanlar `EKSIKLER.md` EK-801…813.
- Unity'de çekilmeden doğrulanamayanlar (orkestratör karşılaştırmasında bakılacak): kart / form satırlarının 1–2 px'lik yükseklikleri (`MenuView.CssLineHeight` değerleri), PIN kutusu parıltısının ikiz öğeyle görünümü, özet kural listesinin (ScrollView, gizli çubuk) yüksekliği (referans 235,7 / 258,6 px), davet satırında üç noktalı kısaltma, lbPop ve anahtar kayma eğrileri.
- Masa aç ve davet listesi ScrollView'leri çubuksuz: fareyle kaydırma çalışır, sürükleme çubuğu yok (UF-802).

## 8. Komutlar

```
git merge v4/data; git merge v4/server; git merge main
python "C:/GD/Okey 101/tools/v4/unity_compile.py" --repo C:/GD/Okey101-v4/lobby --csproj-from "C:/GD/Okey 101" --scratch <scratch> --project Galaxy101.PlayMode.Tests
  Galaxy101.PlayMode.Tests: OK (0 errors)
uv run --with playwright --with pillow python docs/v4/lobby/ref/capture_lobby_refs.py <out>
  24 durum, hepsi router error=None (22'si ref/'e kopyalandı: S14, S15 ve §3'tekiler)
```
