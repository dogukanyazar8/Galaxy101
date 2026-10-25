# 08 · Oyun Masası (3D) — Genel Bakış

Bu bölüm **3D oyun masası ekranının** tamamını tanımlar. Ana menüden "HEMEN OYNA" ile girilen ekrandır. Ana menüyle aynı tasarım dili kullanılır: aynı 5 gezegen teması, aynı fontlar (Montserrat / Baloo 2), aynı avatar çerçeveleri (Klasik / Altın), aynı panel, buton ve gölge kuralları (`01_TASARIM_SISTEMI.md`).

Birincil kaynaklar:
- **3D değerler** (ölçü, kamera, ışık, malzeme, animasyon): `reference/table/table3d.js`, `reference/table/gifts3d.js`
- **Arayüz, akış ve metin**: `reference/table/Galaxy101_OyunMasasi_3D.dc.html` (template + `class Component` mantığı)
- **Veri**: `spec/data/table_content.json`, `spec/data/table_scene.json`, `spec/data/table_strings_tr_en.csv`
- **Görseller**: `screenshots/table/` (27 görüntü, `INDEX.md`)
- **3D modeller**: `reference/table/Galaxy101_3D_Varlik_Disa_Aktarma.html` sayfası tüm modelleri GLB olarak indirir (bkz. `09_MASA_3D_SAHNE.md` §10)

Spec ile HTML/JS çelişirse **JS'deki değer geçerlidir**.

---

## 1. Kesinleşmiş ayarlar (prototipteki Tweaks'in son hali)

| Ayar | Varsayılan | Kullanıcı değiştirebilir mi |
|---|---|---|
| Gezegen teması | Satürn (ana menüde seçilen tema masaya taşınır) | Ana menüden |
| Görüş açısı | **Oyuncu gözü** | Ayarlar → Görünüm (Oyuncu gözü / Kuşbakışı) |
| Masa teması | **İstasyon** | Ayarlar → Görünüm (İstasyon, Ceviz salon, Hologram, Andromeda) |
| Istaka teması | **Klasik ceviz** | Ayarlar → Görünüm (Klasik ceviz, Sedef kakma, Alüminyum, Karbon, Neon cam, Andromeda) |
| Taş görünümü | **Klasik** | Ayarlar → Görünüm (Klasik, Galaksi, Altın, Neon, Mermer); mağazada satın alınanlar açık |
| Taş kalınlığı | **10 mm** (ıstaka profilleri bu kalınlığa göre modellendi) | Hayır |
| Tur süresi | **30 sn** | Hayır (sunucu kuralı) |
| Bot hediyeleri | Demo içindir; üretimde gerçek oyuncu davranışı | — |
| Dil | Türkçe (İngilizce tam) | Ayarlar → Oyun → Dil |
| Çözünürlük | 1280×720 referans; 1080p = ×1.5, 2K = ×2 | — |

## 2. Ekran yerleşimi (1280×720 tasarım birimi)

3D sahne tam ekran çizilir; arayüz üstte UI Toolkit katmanıdır. Arka plan katmanları (alttan üste):
1. Tema degradesi: `radial-gradient(ellipse 90% 75% at 28% 12%, --bg1, --bg2 55%, --bg3)`
2. Yıldız alanı (ana menüdeki starfield, 1280×720)
3. Gezegen (ana menüdeki renderer) — merkez **(300, 118)**, ölçek **0.82**, −16° dönük; arkasında 600×600 `--pg` hâlesi
4. 3D sahne (WebGL; şeffaf temizleme — pencerenin ötesi boş bırakılır, gezegen ve yıldızlar oradan görünür)
5. Kenar karartması: `radial-gradient(ellipse 80% 70% at 50% 58%, transparent 60%, rgba(0,0,0,.42))`
6. Üst karartma: yükseklik 120, `rgba(3,5,14,.78) → .5 (45%) → transparent`

| Öğe | Konum / ölçü |
|---|---|
| Üst bar | padding 14×20, gap 12. Sol: **Masadan kalk** butonu 44×44 (r14, `--pnH`, gölge `0 4px 0 --pnS`), logo "GALAXY 101" (Montserrat 800 19px, letter-spacing 3px, logo degradesi), oda hapı "Tekli · 101 • Bahis 5.000" (`--dp`, 12px). Sağ: **Taş** hapı (mini taş 20×27 + "Taş" + kalan sayı), **çip** hapı (coin 26 + tutar 16px), **Ayarlar** butonu 44×44 (dişli ikonu) |
| Sıra bilgisi (toast) | üst orta, top 16, hap, 14px 800; renkler §3 |
| Oyuncu paneli (ben) | sol alt (18,16): avatar 74 (çerçeve + baş harf 22px + "SV 24" rozeti), ad 16px, durum 12px, **Sohbet** (42×42 mavi degrade) ve **İkram/Hediye** (42×42 tema degradesi) butonları |
| Aksiyonlar | sağ alt (18,16): "Seri diz" / "Çift diz" hapları (13px) + gösterge kartı: 62px dairesel açılış ölçeri (conic, `--a1` → ≥101'de `#3CCB6E`) + butonlar (§ 10_MASA_OYUN_AKISI §5) |
| Rakip avatarları | 3D çapalara bağlı (her karede projeksiyon): 62px avatar + ad plakası (ad 14px, durum 11px); aktifken sıra halkası |
| "BURAYA AT" etiketi | atma halkasının 62px üstünde, `--a1` zemin, yüzer animasyon |
| "GÖSTERGE" etiketi | gösterge taşının altında |
| İkram bildirimi | sağ üst (18, 78) |
| Sistem bildirimi | üst orta top 132 (yeşil başarı / kırmızı uyarı) |
| Bitiş şeridi | üst orta top 96 |
| Bekleme kartı | orta top 470; geri sayım rakamı top 250, 140px |

## 3. Sıra bilgisi (toast) metinleri ve renkleri

| Durum | Metin (TR) | Zemin / yazı |
|---|---|---|
| Dağıtım | Taşlar dağıtılıyor… | `rgba(5,8,22,.75)` / `--ic` |
| İlk hamle (22 taş) | İlk hamle senin · ıstakadan bir taş at | `#3CCB6E` / `#062B14` |
| Çekme | Sıra sende · desteden ya da {sol oyuncu}'ın taşından çek | tema degradesi / `--aT` |
| Atma | Atacağın taşı halkaya sürükle ya da çift tıkla | `#3CCB6E` / `#062B14` |
| Açtıktan sonra | Elini açtın · şimdi bir taş at | `#3CCB6E` / `#062B14` |
| Rakip | {Ad} oynuyor… | `rgba(5,8,22,.75)` / `--ic` |
| Süre uyarısı | Süren bitiyor! | `linear-gradient(#FF7A86,#E8455A)` / beyaz |
| Toplu taşıma | Per taşınıyor · ıstakada istediğin yere bırak | `#3CCB6E` / `#062B14` |

"Sıra ipuçları" ayarı kapalıysa dağıtım dışındaki toast'lar ve "BURAYA AT" etiketi gizlenir.

## 4. Oyuncular (demo verisi)

`table_content.json → seats`. Koltuk sırası (saat yönünün tersi): 0 = ben (alt), 1 = Zeynep (sağ), 2 = Ayşe (üst), 3 = Burak (sol). Sıra 0 → 1 → 2 → 3. Ben sağımdakine (1) atarım, solumdakinin (3) attığını alırım.

## 5. Ekran listesi (görüntüler `screenshots/table/`)

Bekleme · geri sayım · dağıtım · ilk hamle · çekme · ikram geldi · oyuncu kartı · ikram/hediye paneli (3 sekme) · roket uçuşu · sohbet · el açma (Yere aç / İşle) · ayarlar (4 sekme) · 4 masa + 4 ıstaka teması · kuşbakışı · bitiş anı · el sonu tablosu · masadan kalk onayı · bağlantı koptu · İngilizce.
