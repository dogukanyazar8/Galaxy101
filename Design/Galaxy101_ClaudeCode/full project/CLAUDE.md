# Galaxy 101 — kalıcı kurallar

- Kullanıcıya her zaman Türkçe cevap ver.
- Tek oynanabilir oyun: `Galaxy 101 - Gerçek Ana Oyun.dc.html`. Her güncelleme (kural, arayüz, metin, sürüm, hata düzeltme) bu sayfada, oyunun içinde çalışır halde olmalı ve burada kontrol edilir. Başka sayfada kalan değişiklik "yapıldı" sayılmaz. Teslimde bu sayfayı aç ve doğrula.
- Oyunun parçaları (oyun sayfası bunları içe aktarır; değişiklikler bunlarda yapılır): `G101-Menu`, `G101-Lobi`, `G101-Dereceli`, `G101-Masa` (.dc.html) + `g101-cursor.js`, `g101-rank.js`, `g101-settings.js`, `table3d.js`, `tiles3d.js`, `racks3d.js`, `gifts3d.js`, `planet-renderer.js`, `starfield.js`. Bu dosyaların adını değiştirme (oyun adlarıyla yükler).
- Dosya adı önekleri:
  - `Eski - *` — v1 tasarımları, oyunda kullanılmaz; düzenleme.
  - `Referans - *` — katalog/belge sayfaları (masa ek durumları, dereceli sistem, imleç seti); oyunda değil. Yalnız bunlara yapılan değişiklik yeterli değildir.
  - `Araç - *` — 3D önizleme ve dışa aktarma araçları.
  - `galaxy101_unity_handoff/`, `galaxy101_ek_durumlar/` eski teslim paketleri; `galaxy101_handoff_v4/` güncel paket.
- `galaxy101_handoff_v4/logic/*` (rules.js, bots.js, mockServer.js) oyuna bağlandığında oyun sayfası bu modülleri kullanmalı; kural değişikliği hem motora hem oyuna yansımalı.
- Sürüm: 0.5.0 — tüm ekranlarda görünür.
- Tasarımı değiştirme; istenen dışındaki görünümü elleme.
