# DECISIONS.md · kullanıcıya sorulmadan seçilen değerler · v0.4.0

Her satır geri alınabilir; değişirse RULES.md, rules.js ve ilgili test vektörü birlikte güncellenir.

| Kimlik | Karar | Neden | Kural / test |
|---|---|---|---|
| D-01 | Taş kimlikleri string: `R5a`, `R5b`, `Ja`, `Jb`. Kanonik sıra R, B, Y, K; 1–13; a, b; sonra Ja, Jb. | 2 kopya sınırı yapısal olur; testler okunur kalır. | R-01 |
| D-02 | RNG: mulberry32 (uint32), `rngInt(n) = next() % n`. Seed maç başında verilir; dağıtan, karıştırma ve zarlar aynı akıştan, bu sırayla. | C#'ta `uint` ile birebir çevrilir. | R-02, DEAL-01 |
| D-03 | Gösterge seviyesinde ve üstünde iki sahte okey varsa alttaki ilk taş gösterge olur. | Prototip bu durumda sahte okeyi gösterge yapıyordu (hata). | R-04, DEAL-03 |
| D-04 | Deste çekiş sırası: `deck[0]` = gösterge balyasının en alt taşı, sonra büyük indeksli balya, sonra küçük. | Prototipteki orta konum sırası (c0…c19). | R-05 |
| D-05 | ~~geçersiz~~ → kullanıcı kararı: sarma yok (R-12). | — | — |
| D-06 | Okey gruptaysa eksik renklerden R, B, Y, K sırasıyla ilkini temsil eder (açtıran taş değeri ve rozet için). | Puan değişmez; deterministik olmalı. | R-13 |
| D-07 | ~~geçersiz~~ → kullanıcı kararı: elden bitişte eşik yok (R-53). | — | — |
| D-08 | Çift açan oyuncu masadaki seri/gruplara işleyebilir, yalnız çift olarak yere açabilir. | spec'te tanımsızdı. | R-24, R-25 |
| D-09 | Okey alma, alınan okeyi aynı turda kullanma zorunluluğu getirmez. | spec'te tanımsızdı. | R-26 |
| D-10 | Geri bırak, turdaki tüm işlemleri de geri alır. | Açılış yapılıp taş geri bırakılırsa durum tutarsız kalıyordu. | R-32 |
| D-11 | Bahis, maç başlamadan bekleme odasından çıkılırsa iade edilir. | Kullanıcı kararı "oturunca düşülür"; iade tanımsızdı. | R-90 |
| D-12 | Eşitlikte havuz kazananlar arasında eşit bölünür; eşlide takım payı masada kalan insan ortaklara (R-91). | Tanımsızdı. | R-91 |
| D-13 | ~~geçersiz~~ → kullanıcı kararı R-59 (aynı sonuç: sıra paylaşılır; paylaşım D-40). | — | — |
| D-14 | ~~geçersiz~~ → kullanıcı kararı R-55 (taş bittiğinde ıstakalar yazılır). | — | — |
| D-15 | Okey atma cezası alan atışta ayrıca işlek cezası yazılmaz. | Çifte ceza istenmedi. | R-42 |
| D-16 | Açtıran taş cezası yalnız açmamış oyuncunun soldan aldığı taşla açmasında doğar; değer, taşın perdeki değeridir (okeyse temsil ettiği sayı). | spec/17 tanımı + okey belirsizliği. | R-43 |
| D-17 | Okeyi alınan cezası, oyuncu kendi koyduğu okeyi geri alırsa yazılmaz; eşlide ortağın okeyi alınırsa yazılır. | spec/17 "başka oyuncu". | R-44 |
| D-18 | Bekleyen cezalar (açtıran taş, okeyi alınan) turun sonunda (atış/bitiş) yazılır; geri topla onları siler. | Geri toplanan bir hamleye ceza yazılmamalı. | R-34 |
| D-19 | Zaman aşımında yapılmış geçerli açılış/işleme korunur; yalnız soldan alınıp açılamamış taş geri bırakılır. | spec'te "çek + en kötü taşı at" deniyordu. | R-70 |
| D-20 | Bot zamanlaması prototipten: çek 600 ms, per/işle başına 430 ms, atış 650 ms (per sonrası) / 730 ms; sohbet olasılıkları %16 / %60. | Prototipte sabitti; rastgele gecikme eklenmedi. | bots.TIMING, bots.CHAT |
| D-21 | Botlar açabildikleri ilk turda açar; seriyi çifte tercih eder, yalnız çiftle elden bitebiliyorsa çift açar; ortağın okeyini almaz; okeyi yalnız bitişe bir taş kala işler. | Basit, deterministik, gizli bilgi kullanmaz. | R-80 |
| D-22 | Sohbet seçeneklerinin etkisi (R-63) ve izleyici seçeneği. | Tanımsızdı. | R-63 |
| D-23 | İnsan dağıtıcı her zar için en fazla 15 sn bekler; atmazsa sunucu atar (ikinci zar dahil). | Prototipte ikinci zarın süresi yoktu (sonsuza dek bekliyordu). | timings §1 |
| D-24 | Dağıtım töreni sunucuda adım adım zamanlanır; toplam 21 686 ms (bot dağıtıcı). | Prototip zamanlamalarından hesaplandı; önceki pakette 22 sn tahmindi. | SRV-02 |
| D-25 | "Sonraki el" düğmesi (gReady): masadaki bütün insan oyuncular hazırsa 15 sn beklenmez. | Prototipte düğme tek oyunculuydu. | handWait |
| D-26 | Hediye "herkese" = fiyat × alıcı sayısı (masadaki diğer oyuncular). | Prototipte tutar gösterimi buna göreydi; sunucu tanımı yoktu. | giftSend |
| D-27 | Mağaza paketlerindeki bonus yüzdesi miktara eklenir (250.000 +%10 = 275.000). | Kartta "+%10" rozeti vardı; verilen miktar tanımsızdı. | ECON.shop |
| D-28 | ~~Kilit oturum boyunca~~ → kullanıcı kararı R-98 ile değişti (5 dk). | — | — |
| D-29 | Dereceli eşleşmede Duo: oyuncu ve arkadaşı 0 ve 2. koltukta (eşli takım). | spec/27 "karşıda bir çift". | rankedCheck |
| D-30 | Dereceli lig koruması: lige yeni çıkışta 3 maç; sonraki her maçta 1 azalır; korumadayken lig sınırında LP 0'da kalır. | spec/27 "3 maç boyunca alt lige düşmez"; sayım kuralı tanımsızdı. | rankApply |
| D-31 | Masadan kalkan oyuncunun yerine geçen bot onun adıyla oynar; bot ödeme ya da LP almaz. | Kullanıcı kararı "botun puanı kimseye yazılmaz". | R-60, SRV-07 |
| D-32 | Sohbette serbest yazı en çok 120 karakter. | Tanımsızdı. | chatSend |
| D-33 | ~~Koltuk süresiz, zaman aşımı otomatik~~ → kullanıcı kararı R-66 ile değişti. | — | — |
| D-34 | Devralmada havuz = ödenen bahislerin toplamı (kalkanın kaybettiği bahis havuzda kalır, devralanın bahsi eklenir). | R-91 "4 × bahis" devralma durumunu kapsamıyordu. | R-91, SRV-18 |
| D-35 | "Koltuğu sunucu oynar" yalnız bağlantısı kopuk oyuncuya uygulanır; bağlı oyuncu zaman aşımına uğrasa da her turda yalnız R-70 otomatik hamle yapılır. | Cevap kopma bağlamındaydı. | R-66 |
| D-36 | Sunucunun oynaması, oyuncu döndüğünde (hello) ya da bir hamle gönderdiğinde biter; sıra ondaysa tur süresi baştan başlar. | Dönen oyuncuya yarım kalmış süre verilmemeli. | R-66, SRV-16 |
| D-37 | Haftalık puanı yalnız koltuktaki insan oyuncu alır; kalkan oyuncu ve yerine oynayan bot almaz; devralan oyuncu maç sonundaki sıranın puanını alır. | Tanımsızdı. | R-95 |
| D-38 | Şifremi unuttum: istek her zaman başarılı döner (hesap var/yok sızdırılmaz); kod + yeni şifre ile tamamlanır. | Güvenlik varsayılanı. | passwordReset |
| D-39 | ~~"En az 15 taş"~~ → kullanıcı düzeltmesi: şart kaldırıldı, elden bitişte eşik aranır (R-53). | — | — |
| D-40 | Paylaşılan sıra: beraber oyuncular, kapladıkları sıra konumlarındaki değerlerin ortalamasını alır, en yakın tama yuvarlanır (LP, haftalık puan, XP sıra bonusu). Ödül havuzu eşit bölünür. | "Paylaşılır" ifadesinin sayısal karşılığı. | R-59 |
| D-41 | Eşli maçta kazanan takım 1. ve 2. konumları, kaybeden 3. ve 4. konumları kaplar; her oyuncu ortalamayı alır (LP +20 / −15, haftalık 23 / 3, XP 350 / 40). | Eşli sıralamada 4 konumun takımlara dağılımı tanımsızdı. | R-59, R-95, R-93 |
| D-42 | El XP'si yalnız bitirene +340; eşli masada bitirenin ortağı +60 alır. XP botlara karşı ve dereceli maçlarda da verilir. | Cevap "El kazanan" diyordu; eşli ve mod ayrımı tanımsızdı. | R-93 |
| D-43 | Katlamalıda yükselmiş eşiğin altında kalan açılış eksik açılış sayılır (+101), klasik R-23 ile aynı. | Tanımsızdı. | R-23, KAT-01 |

| D-60 | Ortak saat: `g101-clock.js` oyun sayfasında ilk betik. `Math.random` seed'li mulberry32 ile değiştirilir (`?seed`, yoksa seed bir kez tarayıcıdan çekilir ve `G101Clock.seed`'de görünür). `performance.now`, `Date.now`, `requestAnimationFrame` zamanı sanal saatten gelir. | Bütün prototip rastgeleliği tek akıştan; Unity: `GameClock` + `System.Random(seed)` | — |
| D-61 | `?ft` (donma zamanı; host kendi `?t` belirtecini kullandığı için `t` değil) anlamı: durum yoluna varıldıktan sonra ft ms beklenir, sonra kare **kanonik zamanda** (100000 + ft ms) dondurulur: bütün CSS/Web animasyonları `currentTime = 100000 + ft`, JS saati aynı değere sabitlenir, bekleyen zamanlayıcılar kuyruğa alınır. Böylece açılış animasyonları bitmiş, sonsuz döngüler (gezegen, nabız) her açılışta aynı fazda olur; yükleme süresi sonucu etkilemez. | Gerçek "sayfa açılışından t ms" yükleme süresine bağlı olduğu için tekrarlanamıyordu | — |
| D-62 | Öğe kimlikleri (`data-g101-id`) şablondan türetildi: tıklanabilir öğe → işleyici adı (`menu.openDaily`), metin yaprağı → metin anahtarı (`lobby.text.summary`), döngü içinde `[index]` (`table.p.name[0]`). Önek: app / menu / lobby / ranked / table. Aynı ad tekrarlanırsa `#2`. | Unity eleman adları bunlarla aynı olacak | — |


## 2026-10-25 · R-24 güncellendi
- Seri açan oyuncu çiftlerini yere açabilir: açtığı turda (açılıştan sonra ayrı `lay` hamlesiyle) ya da sonraki turlarda. Açılış türü seri kalır; çift çarpanı uygulanmaz.
- Çift açan oyuncu seri/grup açamaz → `E_OPEN_MODE`.
- R-22 değişmedi: tek açılış hamlesinde seri ve çift karışmaz (`E_MELD_MIX`).
- Testler: OPEN-10, OPEN-11.


## 2026-10-25 · Faz 3 sonu ve faz 4
- Senaryolar: ilk el `rules.rigFromHands` ile sabitlenir (dağıtım töreni gerçek kalır). R43 (açtıran taş), R44 (okey alma) → T34, T36.
- Hata metinleri: oyunda görünmeyen 22 hata kodu için TR/EN metin yazıldı (`strings.json` `source: "yeni"`); masa kurallarıyla ilgili 3'ü (E_SIDE_RETURNED, E_MELD_MIX, E_ISLE_DISABLED) ve E_OPEN_MODE oyuna eklendi.
- Ses: oyunda 28 ses olayı tetiklenir (`g101-sound.js`); ayarlardaki ses düzeylerine uyar. Menü ve masa müziği çalar. 28 efekt + 2 müzik döngüsü WAV olarak `sound/` içinde (oyundaki sentezin birebir çıktısı).
- Lig rozetleri ve imleçler oyunun canlı üreticilerinden yeniden üretildi (eski `rank/svg` ve `cursors/` kopyaları yerine).
- Durum koşuları her zaman aynı misafirle açılır; test çipi ve hoş geldin tostu bu koşularda gösterilmez.
