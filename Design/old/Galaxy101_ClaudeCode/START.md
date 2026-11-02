# Galaxy 101 · Claude Code başlangıç dosyası · v0.4.0

Bu arşiv, çalışan Galaxy 101 (101 Okey) oyununun tamamıdır: oyunun kaynak kodu, kural motoru, sunucu provası, testler, protokol, bütün ekranlar, metinler, görseller, 3D modeller için dışa aktarma aracı, ses efektleri, müzik, yazı tipleri ve kütüphaneler. İnternet bağlantısı gerektirmez.

## Klasörler
- `prototip/`: Çalışan oyun. Açılış dosyası `Galaxy 101 - Gerçek Ana Oyun.dc.html`.
  - `vendor/`: three.js r184, JSZip ve yerel yazı tipi tanımı (çevrimdışı çalışma için).
  - `Araç - 3D Varlık Dışa Aktarma.html`: Bütün 3D modelleri GLB olarak tek zip halinde indirir. Kapsam: masa (4 kaplama), ıstaka (6), taş setleri (5), zar, koltuk, oda, 10 ikram/hediye, oyun işaretleri ve tam sahne (ışıklarıyla). 5 gezegen teması seçilebilir.
- `prototip/galaxy101_handoff_v4/`: Teslim paketi. İlk okunacak dosya `README.md`.
  - `RULES.md`, `DECISIONS.md`, `REMOVED.md`, `DEMO_LIST.md`
  - `logic/`: Kural motoru, botlar, sunucu provası ve senaryolar.
  - `protocol/`: Mesajlar, hata kodları ve süreler.
  - `tests/`: 106 kural vektörü ve 26 sunucu akışı; hepsi geçiyor.
  - `states/`: 144 ekran durumu, her biri adresle açılıyor.
  - `export/`: Yerleşim dökümü.
  - `anim/`: Arayüz animasyonları ve 3D hareket formülleri.
  - `strings/`: TR/EN metinler (JSON ve CSV).
  - `assets/`: Temalar, yazı tipleri (TTF), 3D malzemeler, ışıklar, 69 ikon, 75 lig rozeti ve 11 imleç.
  - `sound/`: 28 efekt ve 2 müzik döngüsü (WAV) ile tetikleyicileri.
  - `screenshots/`: 144 ekranın görüntüsü.

## Oyunu çalıştırma
```
cd prototip
npx serve .        # ya da: python -m http.server 8080
```
Tarayıcıda `Galaxy 101 - Gerçek Ana Oyun.dc.html` açılır. Belirli bir ekranı açmak için adrese `?state=G13&seed=42&theme=saturn` eklenir; ekranların listesi `galaxy101_handoff_v4/states/states.json` dosyasında.

## Claude Code'a ilk mesaj
> Bu depo Galaxy 101'in çalışan prototipini ve teslim paketini içeriyor. Önce `prototip/galaxy101_handoff_v4/README.md` dosyasını oku. Sıra şöyle: (1) `logic/rules.js` kural motorunu C#'a taşı; `tests/rules/*.json` vektörlerinin hepsi xUnit testi olarak geçsin. (2) `protocol/` ve `logic/mockServer.js` ile .NET sunucusunu yaz; `tests/server.tests.js` akışlarını entegrasyon testine çevir. (3) Unity istemcisini `states/` ve `screenshots/` ile ekran ekran kur. Yerleşim için `export/`, görseller ve yazı tipleri için `assets/`, hareketler için `anim/`, metinler için `strings/`, sesler için `sound/` klasörünü kullan. 3D modelleri `prototip/Araç - 3D Varlık Dışa Aktarma.html` ile GLB olarak al. Kural kararları `RULES.md` ve `DECISIONS.md` dosyalarında. Tahmin etme; belirsiz bir nokta olursa sor.

## Kurallar
- Tek doğruluk kaynağı: `RULES.md`, `tests/rules/*.json` ve `tests/server.tests.js`.
- `// DEMO:` ile işaretli kod ve `DEMO_LIST.md` dosyasındaki her şey yalnız prototipe aittir; gerçek sunucuda karşılıkları aynı dosyada yazılı.
