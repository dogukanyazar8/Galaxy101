# Galaxy 101 · Claude Code başlangıç dosyası · v0.5.0

Bu arşiv, çalışan Galaxy 101 (101 Okey) oyununun tamamıdır: oyunun kaynak kodu, kural motoru, sunucu provası, testler, protokol, bütün ekranlar, metinler, görseller, 3D modeller için dışa aktarma aracı, ses efektleri, müzik, yazı tipleri ve kütüphaneler. three.js, JSZip ve ana yazı tipleri `vendor/` içinde; oyun sayfaları için React `support.js` tarafından CDN'den yüklenir.

## Klasörler
- `prototip/`: Çalışan oyun. Açılış dosyası `Galaxy 101 - Gerçek Ana Oyun.dc.html`.
  - Ekranlar: `G101-Menu` (ana menü, profil, mağaza, sıralama, ayarlar), `G101-Lobi` (oda listesi, masa kurma), `G101-Dereceli` (dereceli eşleşme, lig), `G101-Masa` (oyun masası: dağıtım, oynanış, açma, işleme, bitiş efektleri, skor tablosu, sohbet, ikram/hediye, ayarlar).
  - Modüller: `table3d.js` (3D masa ve etkileşim), `table2d.js` (2D masa görünümü), `tiles3d.js`, `racks3d.js`, `gifts3d.js`, `ships3d.js` (menü ve masa arka planındaki okey gemileri ve avcı kovalamacası), `planet-renderer.js`, `starfield.js`, `g101-*.js` (imleç, rütbe, ayarlar, ses, saat, durum).
  - `vendor/`: three.js r184, JSZip ve yerel yazı tipi tanımı.
  - `Araç - 3D Varlık Dışa Aktarma.html`: Bütün 3D modelleri GLB olarak tek zip halinde indirir. Kapsam: masa (4 kaplama), ıstaka (6), taş setleri (5), zar, koltuk, oda, 10 ikram/hediye, oyun işaretleri, tam sahne ve 5 uzay gemisi (Kervan, Ok, Halka, 2 avcı). 5 gezegen teması seçilebilir.
- `prototip/galaxy101_handoff_v4/`: Teslim paketi. İlk okunacak dosya `README.md`.
  - `RULES.md`, `DECISIONS.md`, `REMOVED.md`, `DEMO_LIST.md`
  - `logic/`: Kural motoru, botlar, sunucu provası ve senaryolar.
  - `protocol/`: Mesajlar, hata kodları ve süreler.
  - `tests/`: 106 kural vektörü ve 26 sunucu akışı.
  - `states/`: Ekran durumları, her biri adresle açılıyor.
  - `export/`: Yerleşim dökümü.
  - `anim/`: Arayüz animasyonları ve 3D hareket formülleri.
  - `strings/`: TR/EN metinler (JSON ve CSV).
  - `assets/`: Temalar, yazı tipleri (TTF), 3D malzemeler, ışıklar, ikonlar, lig rozetleri ve imleçler.
  - `sound/`: Efektler ve müzik döngüleri (WAV) ile tetikleyicileri.
  - `screenshots/`: Ekran görüntüleri + `INDEX.md`.

## Oyunu çalıştırma
```
cd prototip
npx serve .        # ya da: python -m http.server 8080
```
Tarayıcıda `Galaxy 101 - Gerçek Ana Oyun.dc.html` açılır. Belirli bir ekranı açmak için adrese `?state=G13&seed=42&theme=saturn` eklenir; ekranların listesi `galaxy101_handoff_v4/states/states.json` dosyasında.

## Claude Code'a ilk mesaj
> Bu depo Galaxy 101'in çalışan prototipini (v0.5.0) ve teslim paketini içeriyor. Önce `prototip/galaxy101_handoff_v4/README.md` dosyasını oku. Sıra şöyle: (1) `logic/rules.js` kural motorunu C#'a taşı; `tests/rules/*.json` vektörlerinin hepsi xUnit testi olarak geçsin. (2) `protocol/` ve `logic/mockServer.js` ile .NET sunucusunu yaz; `tests/server.tests.js` akışlarını entegrasyon testine çevir. (3) Unity istemcisini `states/` ve `screenshots/` ile ekran ekran kur. Yerleşim için `export/`, görseller ve yazı tipleri için `assets/`, hareketler için `anim/`, metinler için `strings/`, sesler için `sound/` klasörünü kullan. 3D modelleri `prototip/Araç - 3D Varlık Dışa Aktarma.html` ile GLB olarak al. Kural kararları `RULES.md` ve `DECISIONS.md` dosyalarında. Tahmin etme; belirsiz bir nokta olursa sor.

## Kurallar
- Tek doğruluk kaynağı: `RULES.md`, `tests/rules/*.json` ve `tests/server.tests.js`.
- `// DEMO:` ile işaretli kod ve `DEMO_LIST.md` dosyasındaki her şey yalnız prototipe aittir; gerçek sunucuda karşılıkları aynı dosyada yazılı.
- Sürüm 0.5.0 tüm ekranlarda görünür.
