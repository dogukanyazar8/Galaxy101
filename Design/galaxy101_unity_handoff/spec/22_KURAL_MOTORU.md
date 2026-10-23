# 22 — Kural motoru (101 Okey) — Unity'de saf C# olarak yazılmalı

Kaynak: `G101-Masa.dc.html` içindeki model fonksiyonları. Unity'de `Galaxy101.Rules` (MonoBehaviour'suz, test edilebilir) olarak kurulmalı.

## Veri
- `Tile { id, c: 'R'|'B'|'Y'|'K'|'J', n: 1..13 (J için 0) }`. J = sahte okey.
- `okey = { c: gösterge.c, n: gösterge.n % 13 + 1 }`. `isJok(t)`: t okey ile aynı renk-sayı (gerçek okey, joker). `real(t)`: J ise okeyin değeri, değilse kendisi.
- Istaka: 2 satır × 15 yuva. Perler: oyuncu başına `Tile[][]`.

## Görülmemiş taş havuzu (`poolTake`)
Havuz = 106'lık set − (ıstakam + tüm atılan yığınları + tüm perler + gösterge + bitiş taşı + `gone` listesi). `gone`: görünümden düşen taşlar (atılan yığın 4'ü aşınca alttakiler, botun soldan aldığı taşlar). Desteden çekiş, bot atışları ve bot işlemeleri havuzdan gelir; havuz boşsa el "Taş bitti" ile biter. **Hiçbir renk-sayı 2 kopyayı, sahte okey 2'yi geçemez.**

## Per bilgisi (`meldInfo`) — okey farkında
Jokerler dışındaki taşlar gerçek değerleriyle alınır:
- 2 taş ve aynı → `pair`.
- Hepsi aynı renk (ve aynı sayı değil) → `run`: `start = ilkGerçek.n − ilkGerçek.index`, `end = start + uzunluk − 1`.
- Hepsi aynı sayı → `group`: `n`, kullanılan renkler.

## İşleme (`extendSides`, `extendSide`, `jokerFits`)
- Seri: taş aynı renkte ve `start−1` (≥1) → sol; `end+1` (≤13) ya da end=13 iken 1 → sağ.
- **Joker (okey) seri**: start>1 ise **sol**, end<13 ise **sağ** — iki uç birden geçerli, oyuncu hangi uca bırakırsa oraya.
- Grup: <4 taş, aynı sayı, kullanılmamış renk → ekle. Joker gruba eklenebilir.
- Çift perine işleme yok.
- **Okey alma** (`trySwapOkey`): yerdeki bir perdeki okeyin temsil ettiği gerçek taşı (serideyse `start + index`, gruptaysa eksik renklerden biri) okeyin üstüne bırakınca gerçek taş pere girer, okey ıstakaya gelir. Tost "Okeyi aldın!".
- İşleme ve okey alma yalnızca **el açmış** oyuncu, kendi sırasında, taş çektikten sonra (atma aşaması).

## El açma
- Seri açma: ıstakadaki en yüksek toplamlı geçerli seri/grup kümesi (`evalHand`), toplam ≥ **101**.
- Çift açma: ≥ **5 çift**.
- Açılış puanı **sabit** kalır; sonradan *Yere aç* (ek per) veya işleme puanı artırmaz.
- **Hatalı açma**: toplam <101 (ya da <5 çift) iken Aç'a basılabilir (düğme turuncu-kırmızı, ipucu "Sayı yetmiyor · açarsan hatalı açma cezası alırsın"). Taşlar 1.5 sn masada kalır, sonra ıstakaya döner, **+101 ceza**, süre baştan başlar.

## Geri topla (`snapTurn`, `undoTurn`)
Sıradaki ilk değiştirici işlemden (aç, çift aç, yere aç, işle, okey al) önce anlık görüntü alınır: ıstaka, tüm perler, açılış puanları, açılış modu, tahta yerleşim kayıtları. **Geri topla** düğmesi atma aşamasında görünür; basınca hepsi geri yüklenir (doğru açılışı geri toplamanın cezası yok). Taş atınca görüntü silinir.

## Soldan taş alma ve geri bırakma
Soldaki oyuncunun son attığı taş alınabilir. Atmadan önce **Geri bırak** düğmesi ya da taşı sol halkaya sürükleme (95 px yakınlık) ile taş yığına geri döner ve aşama tekrar *çek* olur.

## Cezalar (her biri +101, el sonu puanına eklenir)
| Durum | Tetik |
|---|---|
| Hatalı el açma | <101 seri / <5 çift ile açma |
| İşlek taş atma | Atılan taş yerdeki herhangi bir pere işlenebiliyor ya da bir okeyin yerine geçebiliyorsa (oyuncu ve botlar) |
Gösterim: kırmızı tost "{ad} +101 ceza · {neden}", sohbet sistem satırı, oyuncu etiketinde kırmızı "Ceza +101".

## Elden bitiş (bomba)
Koşul: sıra bende, atma aşaması, **hiç açmamışım**, 15+ taş, tek taş dışında hepsi geçerli perlerde (ya da 7 çift + 1 taş). **Elden bit** düğmesi (kırmızı, sallanan bomba). Son taş nükleer animasyonla ortaya çakılır, 14 taş yere açılır. Puan: bitiren −202 (×2), rakiplerin cezası **×2**. Normal bitiş −101.

## Sıra ve süre
- Varsayılan tur süresi 30 sn (masa ayarı). Uyarı: süre bitmeden `warn` sn önce kırmızı.
- **Aç, Çift aç, Yere aç, İşle, okey alma süreyi baştan başlatır.**
- Süre dolunca otomatik oyun (çek + en kötü taşı at).

## El sonu puanı
`hand[k] = (bitiren ? −101×mul : açtıysa kalan taş değeri : 202) × mul + ceza[k]`; mul = 2 elden bitişte. Maç N el (3/5/7); en düşük toplam kazanır; kazanan bahis × 4.
