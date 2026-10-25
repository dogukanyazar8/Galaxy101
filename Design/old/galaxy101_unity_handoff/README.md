# Galaxy 101 — Unity Teslim Paketi (TAM OYUN · v3)

> **v3 güncellemesi:** Paket artık oyunun tamamını kapsar: açılış/yükleme, giriş ve hesap, ana menü ve pencereler, ayarlar, Klasik lobi, masa açma/katılma, bekleme odası ve davet, 3D masa (gerçek dağıtım töreni, zar, gösterge/okey, orta tahta, işleme göstergeleri, okey alma, geri topla, cezalar, elden bitiş), el/maç sonu ve lobiye dönüş.
> Önce **`spec/00_GUNCEL_DURUM_V3.md`** (belge önceliği), sonra **`PROMPT_CLAUDE_CODE.md`**.
> Birincil çalışan prototip: **`reference/game/Galaxy 101 - Oyun.dc.html`** (yerel sunucuyla açın). Yeni ekran görüntüleri: **`screenshots/v3/`** (49 görüntü, `INDEX.md`).
> Blender ile varlık üretimi: **`spec/24_BLENDER_VARLIK_HATTI.md`**. Test senaryoları: **`spec/25`**.

## v3 dosyaları
```
spec/00_GUNCEL_DURUM_V3.md     ← neyin değiştiği, hangi belgenin geçerli olduğu
spec/18_ACILIS_GIRIS_HESAP.md  ← açılış animasyonları, giriş/kayıt/misafir, bakiye, kalıcı kayıt
spec/19_AYARLAR_GORUNTU.md     ← görüntü modu, monitöre göre çözünürlük, kare hızı
spec/20_LOBI.md                ← Klasik lobi, filtreler, masa aç, şifre/kod, bekleme odası, davet, masadan kalk
spec/21_DAGITIM_TORENI.md      ← dağıtan, 15×7 balya, iki zar, gösterge/okey, balya dağıtımı, zamanlamalar, HUD
spec/22_KURAL_MOTORU.md        ← taş havuzu, per bilgisi, işleme, okey alma, açma, geri topla, cezalar, elden bitiş, puan
spec/23_MASA_DUZENI_V3.md      ← orta tahta algoritması, deste sola, işleme göstergeleri, taş izi, yüzler, orta halkalar
spec/24_BLENDER_VARLIK_HATTI.md← GLB → Blender (Python/MCP) → Unity
spec/25_TEST_SENARYOLARI.md    ← geliştirici test menüsü
reference/game/                ← GÜNCEL prototip: Galaxy 101 - Oyun.dc.html, G101-Menu, G101-Lobi, G101-Masa + 3D modüller
screenshots/v3/                ← S01–S15 (giriş/menü/lobi), G01–G34 (masa)
PROMPT_CLAUDE_CODE_v1_menu.md  ← eski (yalnız ana menü) görev metni
```

---

## (Önceki sürümün açıklaması — hâlâ geçerli ayrıntılar için)


Bu paket, **Galaxy 101** (101 Okey) oyununun iki ekranını Unity'de birebir yeniden oluşturmak için gereken her şeyi içerir:

1. **Ana menü (1a)** ve açılan tüm pencereler — `spec/01`–`07`, 54 ekran görüntüsü, `reference/Galaxy101_AnaMenu_1a.dc.html`
2. **3D oyun masası** — tam oyun akışı (bekleme, dağıtım, turlar, el açma, işleme, bitiş, el sonu), ıstaka etkileşimi (tek taş ve per toplu taşıma), ikram/hediye sistemi (10 3D model), sohbet, oyuncu kartı, masa ayarları, 4 masa + 6 ıstaka teması, 5 taş görünümü (sahte okey dahil), 2 görüş açısı — `spec/08`–`15`, 27 ekran görüntüsü (`screenshots/table/`), `reference/table/`

Her ikisi için tasarım kuralları, ölçüler, renkler, 5 gezegen teması, animasyonlar, efektler, davranışlar, TR/EN metinler, örnek veriler ve çalışan HTML prototipler vardır. Masa ekranının tüm 3D modelleri GLB olarak dışa aktarılabilir.

Geliştirici (Claude Code / Codex) bu README'den başlamalı, sonra `PROMPT_CLAUDE_CODE.md` içindeki görevi uygulamalıdır.

---

## 1. Tasarım dosyaları hakkında

`reference/` klasöründeki HTML dosyaları **tasarım referansıdır**, üretim kodu değildir. Görünümü ve davranışı göstermek için HTML/CSS/JS ile yazılmış prototiplerdir. Görev, bu tasarımı **Unity'de (UI Toolkit + C#)** Unity'nin kendi yöntemleriyle yeniden kurmaktır. HTML kodu doğrudan taşınmaz; içindeki değerler (renk, ölçü, süre, metin) birebir kullanılır.

- Prototipi açmak için: `reference/Galaxy101_AnaMenu_1a.dc.html` dosyasını bir tarayıcıda açın (aynı klasördeki `support.js`, `planet-renderer.js`, `starfield.js` gerekir; internet bağlantısı Google Fonts için gerekir).
- Dosyada **1a, 1b, 1c** olmak üzere üç yön vardır. **Sadece 1a uygulanacak.** 1b ve 1c'yi yok sayın.
- Prototipteki "Tweaks" paneli tasarım sürecine aittir. Kesinleşmiş seçimler aşağıdadır; oyunda kullanıcıya font seçtirilmez.

## 2. Doğruluk seviyesi

**Yüksek doğruluk (hi-fi).** Renkler, yazı tipleri, ölçüler, köşe yuvarlaklıkları, gölgeler, animasyon süreleri ve metinler kesindir. Piksel düzeyinde aynı sonuç beklenir.

## 3. Kesinleşmiş ayarlar

| Ayar | Değer |
|---|---|
| Varsayılan gezegen teması | **Satürn** |
| Gezegen dönüş hızı | **Hızlı** = bir tam tur **16 sn** |
| Logo yazı tipi (GALAXY) | **Montserrat** 700 |
| Başlık / buton yazı tipi | **Montserrat** (600–900) |
| Gövde yazı tipi | **Baloo 2** (600–800) |
| Referans çözünürlük | **1280×720** tasarım birimi (1080p = ×1.5, 2K = ×2) |
| Varsayılan dil | Türkçe (İngilizce tam destekli) |

## 4. Paket içeriği

```
README.md                       ← bu dosya
PROMPT_CLAUDE_CODE.md           ← Claude Code / Codex'e verilecek görev metni
spec/
  01_TASARIM_SISTEMI.md         ← renk token'ları, 5 tema, tipografi, gölge/köşe ölçekleri, ikonlar
  02_ANA_EKRAN_1a.md            ← 1a ekranının her öğesi: konum, ölçü, renk, durumlar
  03_PENCERELER.md              ← Ayarlar, Mağaza, Arkadaşlar, Liderlik, Günlük ödül, Kurallar,
                                   Profilim, Paylaş, Profili düzenle, Çıkış, Hesap sil, Arkadaş ekle
  04_ANIMASYON_VE_EFEKTLER.md   ← tüm animasyonlar, gezegen render algoritması, yıldız alanı
  05_DAVRANIS_VE_STATE.md       ← tıklama akışları, doğrulama kuralları, state modeli, Esc/kapatma
  06_UNITY_UYGULAMA_REHBERI.md  ← Unity mimarisi, klasör yapısı, UI Toolkit eşlemesi, shader/partikül
  07_KABUL_KONTROL_LISTESI.md   ← teslim öncesi kontrol listesi
  08_OYUN_MASASI_GENEL.md       ← masa: kesinleşmiş ayarlar, ekran yerleşimi, toast metinleri
  09_MASA_3D_SAHNE.md           ← masa: koordinatlar, kamera, ışık, oda/masa/ıstaka/taş ölçüleri, temalar, GLB hattı
  10_MASA_OYUN_AKISI.md         ← masa: durum makinesi, kurallar, puanlama, tüm zamanlamalar
  11_MASA_ETKILESIM.md          ← masa: tıklama, sürükleme, per toplu taşıma algoritmaları
  12_MASA_ANIMASYONLAR.md       ← masa: taş, dağıtım, ikram, roket, bitiş, arayüz animasyonları
  13_MASA_HEDIYE_SOHBET_AYARLAR.md ← masa: ikram kataloğu, panel, sohbet, oyuncu kartı, ayarlar
  14_MASA_UNITY_REHBERI.md      ← masa: Unity mimarisi, sahne hiyerarşisi, sınıflar, shader'lar
  15_MASA_KABUL_LISTESI.md      ← masa: kontrol listesi
  16_MASA_VARLIK_TESLIMI.md     ← masa: GLB indirme → Blender → Unity adım adım, klasör yapısı
  data/
    themes.json                 ← 5 gezegen temasının tüm renk değişkenleri (birebir)
    content.json                ← modlar, kurallar, arkadaşlar, liderlik, görevler, varsayılan ayarlar
    strings_tr_en.csv           ← tüm TR/EN metinler (Unity Localization'a aktarılabilir)
    table_content.json          ← masa: gezegen masa renkleri, oyuncular, ikram kataloğu, demo eller, ayar varsayılanları, temalar
    table_scene.json            ← masa: 3D sabitler, kamera, ışık, yuvalar, çapalar, zamanlamalar, tween formülleri
    table_strings_tr_en.csv     ← masa: tüm TR/EN metinler (183 satır)
screenshots/
  table/INDEX.md + T01–T27      ← masa ekran görüntüleri
  table/T28–T51 (_saturn/_mars) ← masa ek durumları (spec/17): bot masası, okey seçici, ceza, yerden alma, mesajlar, el sonu v2, maç sonu, okeyi alma, yükleme
  INDEX.md                      ← her görüntünün açıklaması
  *.png                         ← 54 ekran görüntüsü (1280×720 tasarımın 0.72 ölçekli hali)
reference/
  Galaxy101_AnaMenu_1a.dc.html  ← çalışan prototip (şablon + mantık tek dosyada)
  planet-renderer.js            ← prosedürel gezegen render kodu (shader'a çevrilecek)
  starfield.js                  ← canlı yıldız alanı kodu (partikül sistemine çevrilecek)
  support.js                    ← prototip çalışma zamanı (Unity için gerekmez)
  table/
    Galaxy101_OyunMasasi_3D.dc.html     ← çalışan 3D masa prototipi (üstteki 10 düğmeyle her aşamaya geçilir)
    table3d.js                          ← 3D sahne, etkileşim, animasyonlar (birincil 3D kaynak)
    gifts3d.js                          ← 10 ikram/hediye modeli
    racks3d.js                          ← 6 ıstaka modeli (profil + uç parçaları)
    tiles3d.js                          ← 5 taş görünümü, Galaxy 101 Numerals rakam yolları, sahte okey
    Galaxy101_3D_Varlik_Disa_Aktarma.html ← tüm modelleri GLB olarak indiren sayfa
    three-d-stage.js, planet-renderer.js, starfield.js, support.js
```

### Masa prototipini açmak
`reference/table/Galaxy101_OyunMasasi_3D.dc.html` dosyasını yerel bir sunucuyla açın (`npx serve reference/table` ya da `python -m http.server`), çünkü 3D modüller ES module olarak yüklenir. İnternet bağlantısı three.js (unpkg) ve Google Fonts için gerekir.

### 3D modelleri almak
Aynı sunucuda `Galaxy101_3D_Varlik_Disa_Aktarma.html` → gezegen temasını seçin → **Tümünü GLB olarak indir** (28 dosya) ya da tek tek önizleyip indirin. Dosyaları `assets_src/glb/` klasörüne koyun, Blender'da cilalayıp `assets_src/blender_out/` altına kaydedin (`spec/16`).

## 5. Ekranlar (özet)

1. **Ana menü (1a)**: profil, para birimleri, ayarlar, logo + 1-0-1 taşları, HEMEN OYNA, 6 oyun modu, 5 alt menü butonu, gezegen seçici, sürüm bilgisi; arkada dönen gezegen ve canlı yıldız alanı.
2. **Ayarlar**: Oyun, Ses, Görüntü, Dil, Hesap sekmeleri.
3. **Mağaza**: Çip, Elmas, Görünüm sekmeleri + hafta sonu fırsatı.
4. **Arkadaşlar**: Çevrimiçi, Tümü, İstekler + arama + arkadaş ekle.
5. **Liderlik**: Haftalık, Sezon, Arkadaşlar + podyum.
6. **Günlük ödül**: 7 günlük seri + günlük görevler.
7. **Kurallar**: 6 bölüm + örnek taşlar.
8. **Profilim**: Genel, Rozetler, Maç geçmişi + Paylaş.
9. **Alt pencereler**: Profili düzenle, Çıkış onayı, Hesap silme onayı, İşlem tamam, Arkadaş ekle, Profili paylaş.

Detaylar `spec/` klasöründe.

## 6. Varlıklar (assets)

- **Yazı tipleri**: Montserrat ve Baloo 2, Google Fonts'tan (SIL Open Font License). Türkçe karakterler (ç ğ ı İ ö ş ü Ç Ğ Ö Ş Ü) ve `₺ × · — …` glifleri font asset'e dahil edilmeli.
- **İkonlar**: Tüm ikonlar prototipte satır içi SVG olarak çizildi (24×24 viewBox, stroke tabanlı). `spec/01_TASARIM_SISTEMI.md` → "İkonlar" bölümünde listelenmiştir. Unity'de VectorImage (SVG import) veya 2× PNG olarak kullanılabilir. SVG path verileri HTML dosyasında aranarak alınabilir.
- **Gezegenler ve yıldızlar**: görsel dosya yoktur; prosedürel olarak üretilir (bkz. `04_ANIMASYON_VE_EFEKTLER.md`).
- **Taşlar, coin, elmas, sandık**: CSS degradeleriyle çizildi; Unity'de 9-slice sprite veya UI Toolkit `background-image` degradeleri (Vector API) ile yapılabilir.
- **Ses**: Tasarımda ses dosyası yoktur; ayarlar ekranında ses kategorileri tanımlıdır.

## 7. Dikkat

- Prototipteki satın alma, istek gönderme, ödül toplama gibi işlemler **sahte (mock)** durumdadır; sayfa yenilenince sıfırlanır. Unity tarafında bunlar servis arayüzlerinin (interface) arkasına konmalı, şimdilik mock servisle çalışmalıdır.
- Ekran görüntülerinde gezegen ve yıldızlar animasyonun tek bir karesidir; asıl görünüm hareketlidir.
