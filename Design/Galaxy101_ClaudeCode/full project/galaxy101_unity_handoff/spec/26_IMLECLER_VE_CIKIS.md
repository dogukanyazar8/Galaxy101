# 26 · İmleç seti ve Oyundan çık

Kaynak: `g101-cursor.js` (çizim + davranış), `Galaxy 101 - İmleç Seti.dc.html` (canlı katalog), `assets/cursors/`.

## A. Oyundan çık (Ayarlar sol alt)

- Eski "Galaxy 101 · v0.2.0 / © CDX Games" metni kaldırıldı.
- Yerine sekme satırıyla aynı ölçüde buton: padding 12/14, köşe 14, gap 12, güç simgesi 20 px + "Oyundan çık" Montserrat 700 17.
  - Normal: zemin `#FF5E6C` %10, iç kenar 1 px `#FF5E6C` %32, yazı `#FF8A94`.
  - Hover: zemin `#FF5E6C`, yazı beyaz, gölge `0 4 0 #C23E4A`. Basılı: 3 px aşağı.
- Tık → onay penceresi (çıkış/silme penceresiyle aynı kalıp): kırmızı karo + güç simgesi, "Oyundan çıkmak istiyor musun?", "Galaxy 101 kapanacak. Çiplerin, ilerlemen ve ayarların kayıtlı kalır.", [Vazgeç] [Oyundan çık].
- Onay → "Görüşmek üzere / Galaxy 101 kapatılıyor…" (1.1 sn) → Unity'de `PlayerPrefs.Save()` + `Application.Quit()`. Prototipte siyah "Oyun kapatıldı" ekranı gösterilir.
- EN: "Quit game", "Quit the game?", "See you soon".
- Masa içinde çıkış yine masadan kalk akışıyla yapılır (bahis kaybı uyarısı); bu buton yalnızca menü ayarlarında.

## B. İmleç durumları

32 px taban, 48 ve 64 px karşılıkları hazır. Hotspot 32 px tabanına göredir (48 için ×1.5, 64 için ×2). `assets/cursors/hotspots.json`.

| Dosya | CSS karşılığı | Hotspot | Nerede |
|---|---|---|---|
| default | default | 4,3 | Boş alan, keçe, arka plan |
| pointer | pointer | 4,3 | Buton, sekme, deste, atık bölgesi |
| grab | grab | 16,15 | Istakadaki taş, soldan alınabilir taş |
| grabbing | grabbing | 16,16 | Taş/per sürüklerken |
| drop | copy | 16,16 | Sürüklenen taş geçerli hedef üstünde |
| text | text | 16,16 | Tüm yazı alanları |
| no | not-allowed | 4,3 | Pasif buton, YAKINDA, sıra sende değil |
| wait | wait | 16,16 | Tam kilit (giriş, ödeme) — 12 kare animasyon |
| progress | progress | 4,3 | Yükleme ekranı — 12 kare animasyon |
| ew | ew-resize | 16,16 | Kaydırıcılar |
| ns | ns-resize | 16,16 | Dikey liste tutamağı |

Görsel dil: koyu mürekkep kontur `#0A0F2C` 2 px, yumuşak gölge (dy 1.6, blur 1.3, %45), oyunun 45° elmas motifi. Beyaz gövde `#FFFFFF→#DCE2F3`.

Tema: vurgu rengi (`a1`,`a2`) seçili gezegenden gelir. PNG'ler Satürn (`#FFC94A→#F5A623`). Unity'de 5 tema × 11 durum PNG üretmek yerine iki yol:
1. **Donanım imleci** (`Cursor.SetCursor`, en düşük gecikme): Satürn PNG'leri; tema değişince ilgili tema setine geç (SVG'lerden aynı betikle `c1/c2` verip üret).
2. **Yazılım imleci** (önerilen, prototipteki davranış): `Cursor.visible = false`, en üst Canvas'ta `Image`; vurgu katmanı ayrı sprite + tema rengi tint. Konum her karede `Input.mousePosition` (yumuşatma YOK — gecikme hissi olmasın).

## C. Davranış

- Durum değişimi çapraz geçiş: opaklık 120 ms ease-out, ölçek 0.6→1, 220 ms `cubic-bezier(.2,1.5,.4,1)`.
- pointer hover: ölçek 1.06, −6° eğim (hotspot çevresinde). Basılı: 0.84, −8°.
- default basılı: 0.86. Tık halkası: 38 px, 2 px tema rengi + parlama, 450 ms büyüyüp söner.
- grab üstünde basılı tutunca otomatik grabbing.
- 3D masa: taş üstü `grab`, deste üstü `pointer`, sürükleme `grabbing`, geçerli hedef (atık bölgesi / işlenebilir per) `drop`.
- wait/progress: ay 1.1 sn'de bir tur (Unity: 12 kare, ~11 fps).
- Pencereden çıkınca imleç gizlenir; dokunmatikte devre dışı.

## D. Ayar

Ayarlar › Görüntü › **İmleç**: Normal · Büyük (×1.3) · Sistem. Kalıcı (`galaxy101.cursor`). Sistem = işletim sistemi imleci.
