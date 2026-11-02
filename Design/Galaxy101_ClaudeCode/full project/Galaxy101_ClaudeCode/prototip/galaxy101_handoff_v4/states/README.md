# states/ · durum kataloğu

- `states.json`: 151 durum. Kimlikler korunur: 01–97 (ana menü pencereleri), S01–S16 (açılış, giriş, menü, lobi, dereceli), G01–G34 (dağıtım + oyun), T01–T51 (v2 masa).
- Açma: `Galaxy 101 - Gerçek Ana Oyun.dc.html?state=G13&theme=saturn&lang=tr&seed=42&ft=0`
  - `theme`: saturn | mars | neptun | jupiter | venus · `lang`: tr | en · `seed`: tamsayı · `ft`: yola varıştan sonra donma (ms; yoksa `hold`; `t` adı host belirteciyle çakıştığı için `ft`)
  - `dump=1`: `data-g101-id` öğelerinin kutu + hesaplanmış stil dökümü `<ID>.layout.json` olarak iner (`nodl=1`: inmez, `window.G101Dump` + `postMessage`).
- Toplu: `export/layout-all.html` (`?auto=1&only=S10,G13`). "İki kez aç, karşılaştır" kutusu aynı URL'nin aynı yerleşimi verdiğini denetler.
- `removed:true`: 7 durum kaldırıldı (T33, T40, T41, T44, T45, T46, T48); neden `note` alanında. Diğer 144 durumun hepsi adresle açılır.
- `scenario`: T34, T36 sabit dağıtımlı gerçek maçla açılır (`logic/scenarios`); dağıtım töreni sürdüğü için ~45 sn sürer.
- `holdApprox:true`: animasyon anı yaklaşık (dağıtım, patlama, el sonu); `ft` ile ince ayar yapılır.
- Yorumlayıcı: `g101-state.js` (adımlar: acct, logout, go, waitFor, set, call, table{flow, bots, noWait}, waitDeal, masa, wait, click, type, scenario, waitMy, waitPen; masa scSel / scDrag / scSwap / scDiscardCode / scOpen / scDiscard)
  - `scenario`: `logic/scenarios/<id>.json` ilk eli sabitler · `waitMy`: sıra bende ve dağıtım bitti · `waitPen`: koltuğa ceza yazıldı
  - `click`: `data-g101-id` öğesine tıklar · `type`: `data-g101-id` girişine metin yazar (React onInput tetiklenir).

Yolu tanımlı durum: 144 / 144 (kaldırılanlar hariç).
3D sahne nesneleri (DOM dışı): adları, malzemeleri ve ışıkları `assets/materials.json`, `assets/lighting.json`; modelleri `Araç - 3D Varlık Dışa Aktarma.html` (`sahne_tam.glb` dahil); hareketleri `anim/tween3d.json`.

Kayıt güvenliği: `?state`, `?lang` ya da `?theme` varken oyun kaydı (`galaxy101.save.v1`) localStorage'a yazılmaz; durum koşuları oyuncunun gerçek kaydını, dilini, temasını, hesabını ve çipini değiştirmez.
