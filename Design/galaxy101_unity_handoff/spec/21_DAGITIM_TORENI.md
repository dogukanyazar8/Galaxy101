# 21 — Dağıtım töreni (gerçek 101 kuralı)

Görseller: `G02`–`G12`. Kaynak: `G101-Masa.dc.html` → `startDeal`, `dealDice`, `rollDice`, `dealReveal`, `dealExtra`, `dealDistribute`, `dealCenter`, `dealFill`, `dealFinish`, `dealHud`; `table3d.js` → `rollDie`, `markStack`, poz türleri `s`, `h`, `c`, `i`.

## Koltuk ve yön
Koltuk 0 = yerel oyuncu, 1 = sağ, 2 = karşı, 3 = sol. Sıra sağa doğru akar: k → (k+1)%4. Taşı solundakinden alır, sağındakine atar.

## Kurallar
1. İlk elde dağıtan **rastgele**; sonraki her elde **önceki dağıtanın sağındaki** oyuncu.
2. 106 taş: 4 renk (Kırmızı R, Mavi B, Sarı Y, Siyah K) × 1–13 × 2 + 2 sahte okey. Karıştırılır.
3. Dağıtanın önüne **15 balya × 7 taş** (yüzü kapalı, üst üste) + **1 fazla taş** dizilir. Balyalar dağıtanın solundan sağına 1…15.
4. Dağıtan zarı **iki kez** atar: a (balya), b (alttan taş). Yerel oyuncu dağıtansa altın **Zar at** düğmesi (2. atışta **Tekrar at**, rozet 1/2, 2/2); 15 sn basılmazsa otomatik. Bot dağıtıyorsa otomatik.
5. **Gösterge**: a. balyanın alttan b. taşı çekilir, çevrilir (yüzü açık) ve **aynı balyanın üstüne** konur. Üstündeki taşlar bir kat iner. Çıkan **sahte okey** ise bir üstündeki taş gösterge olur.
6. **Okey** = göstergenin bir fazlası, aynı renk; 13'ün okeyi 1.
7. **Fazla taş** a+1. balyanın üstüne konur.
8. Dağıtım (balya numaraları 15'te sarar, `S(k) = (a-1+k) % 15`):
   - 1. tur: alıcı j = 0 sağ, 1 karşı, 2 sol, 3 dağıtan; her biri **S(1+j)** ve **S(5+j)** balyalarını alır (iki balya birlikte).
   - 2. tur: aynı sırayla tek balya **S(9+j)**.
   - Sağdaki oyuncu fazla taşlı balyayı aldığından **22 taşla** başlar; diğerleri 21.
9. Kalan üç balya: S(13), S(14) ve gösterge balyası. Soldaki, sağdakinin üstüne konur (14'lük kule) ve **ortaya** taşınır; gösterge balyası yanına konur ve **en son** çekilir.
10. Oyunu sağdaki oyuncu (22 taşlı) **taş atarak** başlatır (çekmeden).

## 3D yerleşim (dünya birimi m, masa yüzeyi TY = 0.76)
- Balya satırı: dağıtan koltuk çerçevesinde x = (si − 7) × 0.057, z = 0.36; fazla taş si = 15.3 (+0.014). Kat yüksekliği TD = 0.010.
- Teslim yığınları: alıcı çerçevesinde x = (slot − 1) × (TW + 0.016), z = 0.53.
- Orta kuleler: ana yığın x = −0.036 (kat j−6, j=6…19), gösterge yığını x = +0.036 (kat j, j=0…5), gösterge kat 6, yüzü açık. Çekiliş en büyük j'den başlar.
- Zar: kenar 0.03, köşe yarıçapı %17, fildişi + koyu lacivert noktalar (1 kırmızı ve büyük), clearcoat. Zıt yüzler toplamı 7: +x 2, −x 5, +y 1, −y 6, +z 3, −z 4.

## Zamanlama (ms)
| Adım | Süre |
|---|---|
| Balya dizme | başlangıç 450, taş başına 15 ms kademe, uçuş ~0.6–0.9 s (bezier, yukarı kalkıp hedefe süzülme, ¼ tur dönüş, minik zıplama) |
| Zar | atış 1.45 s (tekrar atışta önce 0.42 s ele geri dönüş); 3 sekme (0.32, 0.56, 0.72 anlarında; yükseklikler 0.05/0.018/0.006); dönüş açısı A=10–15 rad, `(1-u)^2.2` ile sönümlenir, sonunda istenen yüz tam üstte; her sekmede toz partikülü, inişte altın şok halkası |
| Gösterge | iki zardan 1000 ms sonra; taş dışarı kayar + 1.15 s takla (bezier), üst taşlar 420 ms'de birer kat iner (45 ms kademe); balyanın altında nabız atan altın işaret |
| Fazla taş | 2300 ms sonra blok kayma 0.65 s |
| Dağıtım | 1300 ms sonra; 1. tur adımları 620 ms, 2. tur 480 ms; blok kayma 0.5–0.8 s, yay 3–7 cm; ikinci balya 110 ms geç |
| Ortaya | soldaki sağdakinin üstüne 0.6 s, 800 ms sonra kuleler ortaya 0.85 s |
| Istakaya | her oyuncunun taşları 48 ms (rakip 40 ms) kademeyle ıstakaya uçar; benim taşlarım uçarken yüzü bana döner |
| Başlangıç | doldurmadan ~2.1 s sonra; kamera 1.4 s'de oyuncu görüşüne |
Kamera tören boyunca `deal` görüşü: konum (0, 2.05, 1.62), hedef (0, 0.66, −0.12).

## Bilgi kartı (HUD, üst orta, top 84px)
Cam kart: dağıtan avatarı + "DAĞITAN" rozeti, başlık "Taşları sen dağıtıyorsun" / "Taşları {ad} dağıtıyor", adım metni (anahtar değişince .4s kayarak gelir): *106 taş 7'li balyalara diziliyor* → *Zarı iki kez at* / *{ad} zar atıyor* / *Zar yuvarlanıyor…* → *{a}. balya · alttan {b}. taş gösterge* (sahte okeyde *Sahte okey çıktı · üstündeki taş gösterge oldu*) → *Fazla taş yandaki balyanın üstüne* → *Balyalar dağıtılıyor* → *Kalan balyalar ortaya* → *22 taşla {ad} başlıyor*. İki 30px zar simgesi (3×3 nokta ızgarası; atılmamış %32 opak; son atılan altın halkalı). Gösterge açıldıktan sonra: mini gösterge taşı → ok → mini okey taşı (altın halkalı), `dhTile` takla girişi.
