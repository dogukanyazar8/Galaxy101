# v4 · eksikler

Kaynağı pakette bulunamayan, ya da pakette olup projede yapılamayan her şey. Değer uydurulmadı; "geçici durum" sütunu şu an ne yapıldığını söyler.

Gruplar: **T** tasarımda yok · **K** kullanıcı kararı gerekiyor · **D** dış hizmet gerekiyor · **U** Unity kısıtı · **P** paket içi tutarsızlık

| Kimlik | Grup | Ne eksik | Nerelere bakıldı | Etkisi | Şu anki geçici durum |
|---|---|---|---|---|---|
| EK-001 | P | Aynı durum adresi iki açılışta aynı kareyi vermiyor: arka plandaki yıldız tuvali (parıltı, kayan yıldız) ve gezegen dönüşü donma anında farklı fazda. Menü öğeleri ve 3D masa birebir. | `g101-clock.js` (freeze yalnız CSS/Web animasyonlarını ve JS saatini sabitliyor), `starfield.js`, `planet-renderer.js`; `artifacts/v4/ref/capture_report.json` `repeat` → `diffPixels` | Piksel karşılaştırmasında arka plan farkı gürültü olur | Karşılaştırmada arka plan tuvali maskelenir (KARARLAR K-08); fark sayıları `capture_report.json`'da |
| EK-002 | P | Görev metnindeki URL şablonu `theme=saturn&lang=tr` 02–05 (kendi teması olan) ve 90–97, T27 (EN) durumlarını ezer; durum kataloğuyla çelişir | `g101-state.js` `run()`, `states.json` | Şablon aynen uygulanırsa bu 13 durum yanlış tema/dille çekilir | Bu durumlar kendi tema/dilleriyle çekildi (KARARLAR K-07) |
