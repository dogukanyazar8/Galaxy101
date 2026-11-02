# RULES.md · Galaxy 101 kural karar tablosu · v0.5.0

Tek kaynak budur. Kodda karşılığı `logic/rules.js` (kural kimlikleri yorumlarda), testleri `tests/rules/*.json`. Eski spec/17 ve spec/22 bu tabloyla çelişirse bu tablo geçerlidir. Kaldırılanlar `REMOVED.md`'de.

Gösterim: renkler R kırmızı, B mavi, Y sarı, K siyah. Taş kimliği `R5a` / `R5b` (iki kopya), sahte okey `Ja` / `Jb`. Örneklerde gösterge B6 → okey B7.

## 1 · Taşlar ve dağıtım

| Kimlik | Kural | Sayılı örnek | Test |
|---|---|---|---|
| R-01 | 106 taş: 4 renk × 13 sayı × 2 kopya + 2 sahte okey. Her kimlik oyunda tam bir kez bulunur, bu yüzden bir renk-sayı en çok 2 kez görünür. | `R5a` iki yerde olamaz | COPY-01, COPY-02 |
| R-02 | Dağıtan: ilk elde seed'li RNG ile 0–3, sonraki ellerde bir sonraki koltuk `(D+1)%4`. Karıştırma Fisher–Yates, `j = rng % (i+1)`. | seed 42 → dağıtan 0 | DEAL-01, TURN-02 |
| R-03 | 15 balya × 7 taş (karışık dizinin 0–104'ü), 106. taş fazla taştır. İki zar a, b. Gösterge balyası `a−1`, seviyesi `b−1` (0 alt). Fazla taş gösterge balyasının yanındaki balyaya `(a−1+1)%15` eklenir. | zar 3·4 → balya 2, seviye 3, fazla taş balya 3'e | DEAL-02, DEAL-04 |
| R-04 | Gösterge sahte okey olamaz: zarın gösterdiği taş sahte okeyse üstündeki ilk sahte olmayan taş, üstte yoksa altındaki ilk taş gösterge olur. Okey = göstergenin rengi, sayısı `n%13+1` (13 → 1). | seviye 3 sahte → seviye 4 R10 gösterge, okey R11 | DEAL-02, DEAL-03 |
| R-05 | Balya dağıtımı: `S(k)=(a−1+k)%15`. j=0..3 için koltuk `(D+1+j)%4` önce `S(1+j)`, `S(5+j)`, sonra `S(9+j)` balyalarını alır (her balya üstten alta). `S(1)` fazla taşlı olduğundan dağıtanın sağındaki 22, diğerleri 21 taş alır. Deste = gösterge balyasının kalan 6 taşı + büyük indeksli kalan balya + küçük indeksli kalan balya, toplam 20; çekiş `deck[0]`'dan. | DEAL-04: `deck[0]=R8a` | DEAL-01, DEAL-04 |
| R-06 | 22 taşlı oyuncu ilk turda çekmez, doğrudan oynar ve atar. Sıra `(k+1)%4` yönünde ilerler. | çek → `E_PHASE` | DEAL-05 |

## 2 · Perler

| Kimlik | Kural | Sayılı örnek | Test |
|---|---|---|---|
| R-10 | Seri: aynı renk, ardışık, 3–13 taş. Değer = sayıların toplamı. | R3-R4-R5 = 12 | MELD-01, MELD-10, MELD-18 |
| R-11 | Grup: aynı sayı, farklı renk, 3–4 taş. Değer = sayı × taş adedi. | R7-Y7-K7 = 21 | MELD-04, MELD-06, MELD-07 |
| R-12 | Sarma yoktur: 13'ten sonra 1 gelmez (12-13-1, 13-1-2 geçersiz); okey de 13'ün ardına konamaz. 1 her zaman 1 puandır. Tahtada 1 her zaman 1. sütundadır. | 12-13-1 geçersiz · okey-12-13 = 11-12-13 = 36 | MELD-08, MELD-09, MELD-11, MELD-19, MELD-20, ISLE-07 |
| R-13 | Okey (gösterge+1'in iki kopyası) her taşın yerine geçer; değeri perdeki konumundan çıkar (seride sırası, grupta eksik renk — R,B,Y,K sırasıyla ilk eksik). Sahte okey joker değildir, okeyin renk ve sayısında sıradan bir taştır. | R3-okey-R5 → okey R4; B5-B6-sahte = 18 | MELD-02, MELD-03, MELD-05, MELD-12, MELD-19 |
| R-14 | Perde en az bir gerçek (okey olmayan) taş bulunmalı. | okey-okey geçersiz | MELD-17 |
| R-15 | Çift: aynı renk ve sayıda iki taş; okey herhangi bir taşla, iki okey ya da iki sahte okey birbiriyle çift olur. | R5-okey çift | MELD-13…16 |
| R-16 | Perin türü (seri/grup/çift) istemciden gelir ve sunucu doğrular. Tek gerçek taşlı belirsiz perde (okey-okey-R5) tür istemcinin seçimidir. Sıralama ve botlar en yüksek toplamlı kümeyi seçer; okey en yüksek boşluğu doldurur. | R11-R12-okey + 9'lar = 63 | MELD-20 |

## 3 · El açma, yere açma, işleme

| Kimlik | Kural | Sayılı örnek | Test |
|---|---|---|---|
| R-20 | Seri açma: seçilen seri/grupların toplamı ≥ eşik. Klasik eşik 101; Katlamalı masada R-64. Açılış puanı sabit kalır. | 46 + 33 + 26 = 105 | OPEN-01, KAT-02 |
| R-21 | Çift açma: ≥ eşik çift. Klasik eşik 5; Katlamalı masada R-64. | 5 çift | OPEN-03, KAT-04 |
| R-22 | Bir açılışta seri ve çift karışmaz (`E_MELD_MIX`). Geçersiz per reddedilir, ceza yoktur (`E_MELD_INVALID`). | — | OPEN-05, OPEN-06 |
| R-23 | Eksik açılış (seri ya da çift eşiğin altında, elden bitiş R-53 dışında) kabul edilir: oyuncuya +101 ceza yazılır, taşlar ıstakaya döner, açılmamış sayılır, aynı turda yeniden deneyebilir. Katlamalıda yükselmiş eşik geçerlidir. | 79/101 → +101 · katlamalı 104/106 → +101 | OPEN-02, OPEN-04, KAT-01, FIN-11, FIN-13 |
| R-24 | Yere aç: açmış oyuncu sonradan ek per açar. Seri açan seri/grup ve çift açabilir; çiftleri açtığı turda (açılıştan sonra ayrı hamleyle) ya da sonraki turlarda yere koyar. Açılış türü seri kalır (çift çarpanı yok). Çift açan yalnız çift açar. Açılış puanı artmaz. | çift açan seri açamaz → `E_OPEN_MODE` | OPEN-03, OPEN-09, OPEN-10, OPEN-11 |
| R-25 | İşle: açmış oyuncu, kendi sırasında, çektikten sonra, masadaki herhangi bir seri ya da gruba (çift hariç, ortak ya da rakip fark etmez) taş ekler. Okey serinin iki ucuna da eklenebilir; uç istemciden gelir. | okey R4-R5-R6'nın başına R3, sonuna R7 olarak | ISLE-01…10 |
| R-26 | Okey alma: açmış oyuncu masadaki bir okeyin temsil ettiği gerçek taşı okeyin yerine koyar, okey ıstakasına gelir. Okeyi masaya koyan oyuncu kendisi değilse ona +101 (R-44). | K3-okey-K5 + K4 → okey ıstakaya | OKEY-01…03 |

## 4 · Tur

| Kimlik | Kural | Sayılı örnek | Test |
|---|---|---|---|
| R-30 | Tur: çek (desteden ya da soldaki oyuncunun — bir önceki koltuk `(k+3)%4` — son attığı taş), oyna (aç / yere aç / işle / okey al / geri topla), at. | — | TURN-01, SIDE-05 |
| R-31 | Açmamış oyuncu soldan taş alırsa aynı turda o taşı içeren bir açılış yapmak zorundadır. Açamıyorsa taşı geri bırakır; o taş açılmadan atış yapılamaz (`E_SIDE_MUST_OPEN`). Açmış oyuncu soldan alınan taşı kullanmak zorunda değildir. | K9 alındı, 97 ile açma reddedilir, K9 dahil 106 kabul | SIDE-01, SIDE-03, SIDE-04 |
| R-32 | Soldan alınan taş aynı turda atılamaz (`E_SIDE_DISCARD`). Geri bırak: turdaki tüm işlemler geri alınır, taş yığının üstüne döner, oyuncu çekme aşamasına döner. | — | SIDE-01, SIDE-02 |
| R-33 | Geri bırakılan taş aynı turda yeniden alınamaz; oyuncu desteden çeker (`E_SIDE_RETURNED`). | — | SIDE-02 |
| R-34 | Geri topla: turun ilk değiştirici işleminden (aç, yere aç, işle, okey al) önceki durum saklanır; atıştan önce geri yüklenebilir. Bekleyen cezalar (R-43, R-44) da silinir. Eksik açılış cezası (R-23) geri alınmaz. | okey alma geri toplanınca +101 yazılmaz | OPEN-08, OKEY-03, SIDE-04 |
| R-35 | Açma, yere açma ve işlemeden sonra ıstakada en az 1 taş kalmalıdır; el her zaman bir atışla biter (`E_LAST_TILE`). | — | OPEN-07, ISLE-08 |
| R-36 | Bitiş: açmış oyuncunun son taşını atması. Aynı turda işleyip bitirmek serbesttir. | işle R7 + at Y1 → bitti | FIN-06, FIN-09 |
| R-37 | Bitiş taşı masanın ortasına konur, yığına girmez; işlek/okey cezası doğurmaz. | — | FIN-02 |

## 5 · Cezalar

Hepsi el puanına sabit eklenir, çarpanla çarpılmaz (R-58). Masa seçeneği "Ceza kuralları: kapalı" ise hiçbiri yazılmaz (R-62).

| Kimlik | Ceza | Tutar | Kime / ne zaman | Test |
|---|---|---|---|---|
| R-40 | Eksik açılış (R-23) | +101 | açana, anında | OPEN-02, OPEN-04 |
| R-41 | İşlek taş atma: atılan taş masadaki bir seri/gruba eklenebiliyor ya da bir okeyin yerine geçebiliyor | +101 | atana, atışta · masa seçeneği "İşlek taş atma cezası" kapalıysa yazılmaz | PEN-01, PEN-02, PEN-07 |
| R-42 | Okey atma (bitiş atışı hariç). Okey atışında ayrıca işlek cezası yazılmaz. Sahte okey okey değildir. | +101 | atana, atışta · masa seçeneği "Okey atma cezası" kapalıysa yazılmaz | PEN-03, PEN-04, FIN-02, PEN-08 |
| R-43 | Açtıran taş: açmamış oyuncu soldan aldığı taşla açarsa, taşı atan oyuncuya taşın perdeki değeri × 10 | 9 → +90, 13 → +130 | atana, açanın turu bitince · masa seçeneği "Açtıran taş cezası" (varsayılan açık) kapalıysa yazılmaz | SIDE-01, PEN-09 |
| R-44 | Masadaki okeyi alınan: okeyi masaya koyan oyuncuya (alan kendisi değilse) | +101 | koyana, alanın turu bitince | OKEY-01, OKEY-03 |

## 6 · El sonu puanı

`M = (elden ? 2 : 1) × (okeyle ? 2 : 1) × (çiftle ? 2 : 1)` (en fazla ×8)

| Kimlik | Kural | Sayılı örnek | Test |
|---|---|---|---|
| R-50 | El puanı = temel × M + ceza (cezalar çarpılmaz). M çarpanların çarpımıdır: elden bitiş ×2, okey atarak bitiş (okey kafa) ×2, çift açarak bitiş ×2 (en çok ×8). Bitirenin temeli −101. | ×1 → −101, ×2 → −202, ×4 → −404, ×8 → −808 | FIN-01…05, FIN-10, FIN-12 |
| R-51 | Açmamış rakip: `202 × M + ceza` | ×2 → 404 | FIN-01, FIN-02 |
| R-52 | Açmış rakip: `kalan taş toplamı × M × (çift açtıysa 2) + ceza`. Istakada kalan okey 101, sahte okey okeyin sayısı (okey B7 ise 7), diğerleri kendi sayısı. | okey + sahte = 108 | FIN-01 |
| R-53 | Elden bitiş: hiç açmamış oyuncu aynı turda elini açıp son taşı atarak biter. Açılış eşiği aranır: seri ≥ 101, çift ≥ 5 (katlamalıda o anki yükselmiş eşik, R-64); eşik karşılanmıyorsa "Elden bit" düğmesi görünmez. ×2. | elden: bitiren −202, açmamış 404, açmış kalan × 2 · elden + okey kafa: −404, 808, kalan × 4 | FIN-03, FIN-05, FIN-10, FIN-11, FIN-12, FIN-13 |
| R-54 | Okeyle bitiş: son atılan taş gerçek okeyse ×2. Çiftle bitiş: bitiren çift açmışsa ×2. | — | FIN-02, FIN-04 |
| R-55 | Taş bitti: deste boşken atış yapılır ve el bitmezse el biter. Herkes ıstakasını yazar: seri açmış = kalan taş toplamı (okey 101, sahte okey okeyin sayısı); çift açmış = bunun 2 katı; açmamış = 202. Cezalar eklenir; kimse −101 almaz; çarpan uygulanmaz. | [2, (101+5)×2+101, 202, 7] = [2, 313, 202, 7] | FIN-07, FIN-09 |
| R-56 | Çift açmış, bitirmemiş oyuncunun kendi puanı ayrıca ×2. | 10 × 2 × 2 = 40 | FIN-04, FIN-05 |
| R-57 | Eşli: ortaklar 0–2 ve 1–3. Ortak bitirirse diğer ortak 0 (+ kendi cezası) alır. Takım puanı = iki ortağın toplamı. Tahta ortaktır; ortağın ya da rakibin perine işlenebilir. | [−101, 202, 101, 2] → takım [0, 204] | FIN-08, MATCH-03 |
| R-58 | Cezalar çarpılmaz. | elden ×2: 15 × 2 + 101 = 131 | FIN-03 |
| R-59 | Maç N el (3 / 5 / 7). En düşük toplam kazanır. Eşitlik bozucu yoktur, maç berabere biter: eşit toplamlar aynı sırayı paylaşır (1, 1, 3, 4). Ödül, LP, haftalık puan ve XP sıra bonusu paylaşılır: beraber oyuncular kapladıkları sıraların değerlerinin ortalamasını alır (D-40). Eşlide sıralama takım toplamıyla; takım iki sıra konumunu kaplar (D-41). | 1.lik berabere: LP (30+10)/2 = 20, haftalık (30+15)/2 = 23 | MATCH-01…03, SRV-21 |

## 7 · Masa, ayrılma, zaman aşımı

| Kimlik | Kural | Sayılı örnek | Test |
|---|---|---|---|
| R-60 | Masadan kalkan oyuncu bahsini kaybeder, ödül ve XP sıra bonusu almaz; yerine bot oturur, botun puanı kimseye yazılmaz. Tekli masada kalkan maçta son sayılır (birden çok kalkan varsa önce kalkan daha geride). Eşli masada takım son sayılmaz: ortak bot ile oynamaya devam eder; takım kazanırsa takımın ödülünün tamamını masada kalan ortak alır. Koltuk oyun sürerken devralınabilir (R-65). | — | LEAVE-01, LEAVE-02, SRV-07, SRV-25 |
| R-61 | Masa seçeneği "İşleme: kapalı": işle ve okey alma yapılamaz (`E_ISLE_DISABLED`), işlek cezası yoktur. | — | ISLE-10, PEN-06 |
| R-62 | Masa seçeneği "Ceza kuralları: kapalı": R-40…R-44 cezalarının hiçbiri yazılmaz; eksik açılış yine geri alınır. | — | PEN-05 |
| R-63 | "İzleyici: açık" yalnız izleme girişine izin verir, kuralları değiştirmez. "Sohbet": açık = serbest yazı + hızlı mesaj; hızlı = yalnız hazır mesajlar; kapalı = sohbet paneli ve balonlar gizli, sistem satırları görünür. | — | (protokol, faz 2) |
| R-64 | Katlamalı (Klasik 101 varyantı): yalnız açılış eşiği değişir. Bir el içinde sonradan açan, kendinden önce aynı türde açanın değerini geçmek zorundadır: seri ilk ≥101, sonraki = önceki seri açılışın toplamı + 1 (101 → 102 → 103 …); çift ilk ≥5, sonraki = önceki çift sayısı + 1 (5 → 6 → 7 …, üst sınır yok). Seri ve çift eşikleri bağımsızdır. El bitince eşikler 101 / 5'e döner. Puan katlanmaz, bahis değişmez. Eşli masada "Ortağın açılışı eşiği yükseltsin" seçeneği (varsayılan AÇIK): kapalıysa yalnız rakip takımın açılışları eşiği yükseltir. Seçenek masa oluşturma formunda, lobi kartında ve masa bilgisinde görünür. | 105 açılmış → 106 gerekir; 7 çift açılmış → seri eşiği yine 101 | KAT-01…11, SIM-03, SRV-23 |
| R-65 | Oyuna katıl (devralma): oynanan masada botun ya da kalkan oyuncunun koltuğu yeni oyuncuya geçer. Bahis o an ödenir; koltuğun toplam puanıyla devam edilir; "kalktı" işareti kalkar, sıralamada normal oyuncu sayılır. Dereceli ve botlara karşı masalarda yok. | LEAVE-01 toplamları [100,50,300,200], 1 kalkar, devralınır → sıralama 0,1,3,2 | TAKE-01, SRV-18 |
| R-66 | Bağlantı kopması kalkma değildir: tur süresi normal işler; üst üste 2 zaman aşımından sonra koltuğu sunucu (bot mantığı) oynar; oyuncu maç bitene dek dönebilir, döndüğünde sunucu bırakır. Bekleme odasında kopan oyuncu hemen çıkarılır, bahsi iade edilir. | — | SRV-16, SRV-17 |
| R-70 | Süre dolunca otomatik oyun: çekme aşamasındaysa desteden çek; soldan alınmış ve açılamamış taş varsa geri bırak, desteden çek; sonra R-71'deki taşı at. Yapılmış geçerli açılış/işleme korunur. | — | AUTO-01, AUTO-02, DEAL-05 |
| R-71 | Otomatik atılacak taş: soldan alınan taş atılamaz; öncelik okey olmayan ve işlek olmayan taş; ıstakadaki en az bağlantılı (aynı renk ±1/±2 ya da aynı sayı başka renk); eşitse sayısı büyük; eşitse kimliği alfabetik küçük. | R1-R2-R3-K9-Y13 → Y13 | AUTO-01, AUTO-03 |
| R-80 | Botlar aynı kural motorunu kullanır, yalnız açık bilgi ve kendi ıstakasını görür (`logic/bots.js`). | seed 42 maç toplamları | SIM-01, SIM-02 |

## 8 · Ekonomi

| Kimlik | Kural | Durum |
|---|---|---|
| R-90 | Bahis oyuncu masaya oturduğunda bakiyeden düşülür. Maç başlamadan bekleme odasından çıkan oyuncunun bahsi iade edilir. | karar (kullanıcı) + iade: DECISIONS D-11 |
| R-91 | Havuz = maçta ödenen bahislerin toplamı (normalde 4 × bahis; devralmada devralanın bahsi eklenir, D-34). Kesinti yok (%0): kazanan havuzun tamamını alır. Beraberlikte kazananlar eşit böler. Eşlide kazanan takımın payı masada kalan insan ortaklar arasında bölünür (biri kalktıysa diğeri tamamını alır). | karar (kullanıcı) · SRV-25 |
| R-92 | Seviye n'den n+1'e geçmek için gereken XP = 375 · n. | karar (kullanıcı) · SRV-24 |
| R-93 | XP: her elde bitiren +340, diğer oyuncular +60 (taş bittiyse herkes +60). Maç sonu sıra bonusu 500 / 200 / 80 / 0 (R-59 paylaşımı). Masadan kalkan sıra bonusu almaz. Botlara karşı ve dereceli dahil her modda verilir (D-42). | karar (kullanıcı) · SRV-24 |
| R-94 | Dereceli: bahis yok, katlamasız, 5 el, 30 sn; LP kuralları `spec/27` (Dereceli sistem). Botlara karşı: çip yok. | mevcut |
| R-95 | Liderlik: haftalık puan maç sonu sırasına göre 30 / 15 / 5 / 1 (çevrimiçi maçlar; özel odalar ve botlara karşı hariç; eşlide takımın sırası), pazartesi 00:00 İstanbul sıfırlanır. Sezon tablosu dereceli LP (lig × 100 + LP). Arkadaşlar sekmesi: arkadaşlar arasında sezon LP. Eşit puan aynı sırayı paylaşır. | karar (kullanıcı) · SRV-21 |
| R-96 | Başlangıç bakiyesi: misafir 10.000 çip, e-posta / Google hesabı 25.000 çip; herkese 20 elmas. | karar (kullanıcı) · SRV-20 |
| R-97 | Günlük ödül: gün değişimi sunucu saatiyle Europe/Istanbul. Bir gün kaçırılırsa seri 1. güne döner; 7. günden sonra baştan başlar. | karar (kullanıcı) · SRV-22 |
| R-98 | Giriş: 3 yanlış şifreden sonra 5 dakika kilit; "Şifremi unuttum" ile şifre değiştirilince kilit kalkar. | karar (kullanıcı) · SRV-19 |

## Hata kodları (motor)

`E_NOT_YOUR_TURN`, `E_PHASE`, `E_HAND_OVER`, `E_TILE_NOT_IN_RACK`, `E_SIDE_EMPTY`, `E_SIDE_RETURNED`, `E_SIDE_DISCARD`, `E_SIDE_MUST_OPEN`, `E_RETURN_INVALID`, `E_DECK_EMPTY`, `E_MELD_INVALID`, `E_MELD_MIX`, `E_MELD_NOT_FOUND`, `E_ALREADY_OPENED`, `E_NOT_OPENED`, `E_OPEN_MODE`, `E_LAST_TILE`, `E_ISLE_INVALID`, `E_ISLE_DISABLED`, `E_SWAP_INVALID`, `E_NO_UNDO`, `E_LEAVE_INVALID`, `E_ACTION`, `E_STATUS`. Arayüzdeki karşılıkları (durum kimliği ve metin anahtarı) faz 2'de `protocol/protocol.json`'da.

Ceza kodları: `P_OPEN_SHORT`, `P_ISLEK`, `P_OKEY_DISCARD`, `P_OPENED_WITH`, `P_OKEY_TAKEN`.
