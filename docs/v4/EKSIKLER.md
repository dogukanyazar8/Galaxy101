# v4 · eksikler

Kaynağı pakette bulunamayan, ya da pakette olup projede yapılamayan her şey. Değer uydurulmadı; "geçici durum" sütunu şu an ne yapıldığını söyler.

Gruplar: **T** tasarımda yok · **K** kullanıcı kararı gerekiyor · **D** dış hizmet gerekiyor · **U** Unity kısıtı · **P** paket içi tutarsızlık

| Kimlik | Grup | Ne eksik | Nerelere bakıldı | Etkisi | Şu anki geçici durum |
|---|---|---|---|---|---|
| EK-001 | P | Aynı durum adresi iki açılışta aynı kareyi vermiyor: arka plandaki yıldız tuvali (parıltı, kayan yıldız) ve gezegen dönüşü donma anında farklı fazda. Menü öğeleri ve 3D masa birebir. | `g101-clock.js` (freeze yalnız CSS/Web animasyonlarını ve JS saatini sabitliyor), `starfield.js`, `planet-renderer.js`; `artifacts/v4/ref/capture_report.json` `repeat` → `diffPixels` | Piksel karşılaştırmasında arka plan farkı gürültü olur | Karşılaştırmada arka plan tuvali maskelenir (KARARLAR K-08); fark sayıları `capture_report.json`'da |
| EK-002 | P | Görev metnindeki URL şablonu `theme=saturn&lang=tr` 02–05 (kendi teması olan) ve 90–97, T27 (EN) durumlarını ezer; durum kataloğuyla çelişir | `g101-state.js` `run()`, `states.json` | Şablon aynen uygulanırsa bu 13 durum yanlış tema/dille çekilir | Bu durumlar kendi tema/dilleriyle çekildi (KARARLAR K-07) |
| EK-003 | P | Geri sayım metinleri (Mağaza "Hafta sonu fırsatı" süresi, günlük ödül/görev yenilenmesi) aynı adreste her açılışta farklı: prototip saati `Date.now = gerçek yükleme anı + sanal süre` (`g101-clock.js` `d0`), yani duvar saatine bağlı | `g101-clock.js` (`Date.now` sarmalı), `artifacts/v4/ref/30.png` ↔ `_repeat/30.png` (01:54:22 ↔ 01:49:51) | Bu metinler hiçbir zaman piksel/metin eşleşmez | Unity'de de sunucu/duvar saatinden hesaplanır; karşılaştırmada süre metinleri "biçim aynı" diye kabul edilir (ss:dd:sn) |
| EK-004 | P | Açılış ilerlemesi (S01) yükleme olaylarına (font hazır 46, gezegen 62, `table3d.js` 84) ve rastgele artışa bağlı; donma anındaki yüzde ve mesaj yükleme hızına göre değişir. İpucu metni seed'li ana akıştan (`Math.floor(Math.random()*4)`) seçilir; hangi değeri alacağı o ana dek yapılan bütün `Math.random` çağrılarının sırasına bağlı | `Galaxy 101 - Gerçek Ana Oyun.dc.html` `runSplash()` (satır 447–458), `_tip` (satır 613) | S01 karesinde ilerleme yüzdesi, mesajı ve ipucu Unity ile birebir tutmaz | Unity aynı algoritmayı kullanır; S01 "bilinçli fark" (UNITY_FARKLARI UF-02) |

## Hatların eksik kayıtları

Her hat kendi kayıtlarını tutar; kimlik aralıkları: 0xx ana, 1xx kural, 2xx sunucu, 3xx 3D, 4xx veri, 5xx masa, 6xx menü, 7xx dereceli, 8xx lobi, 9xx ses. Grup: T tasarımda yok · K kullanıcı kararı · D dış hizmet · U Unity kısıtı · P paket tutarsızlığı.

| Dosya | Kayıt | T | K | D | U | P |
|---|---|---|---|---|---|---|
| `EKSIKLER.md` | 4 (EK-001…EK-004) | 0 | 0 | 0 | 0 | 4 |
| `hat-a/EKSIKLER.md` | 8 (EK-101…EK-108) | 1 | 0 | 0 | 2 | 5 |
| `hat-b/EKSIKLER.md` | 25 (EK-201…EK-225) | 16 | 1 | 1 | 1 | 6 |
| `hat-c/EKSIKLER.md` | 11 (EK-301…EK-311) | 0 | 1 | 0 | 4 | 6 |
| `hat-d/EKSIKLER.md` | 13 (EK-401…EK-413) | 0 | 1 | 0 | 7 | 5 |
