# 15 · Oyun Masası — Kabul Kontrol Listesi

Her maddeyi `screenshots/table/` görüntüleri ve `reference/table/Galaxy101_OyunMasasi_3D.dc.html` prototipiyle yan yana doğrulayın.

## Sahne
- [ ] Kamera "Oyuncu gözü" konum/hedef/FOV birebir; ıstakam altta tam görünüyor, üst oyuncu ve pencere görünüyor (T05)
- [ ] Kuşbakışı görünüm ve 1.3 sn geçiş; Ayşe avatarı perlerin üstüne binmiyor (T22)
- [ ] Gezegen pencerenin ötesinde, sol üstte; yıldızlar akıyor
- [ ] 4 masa × 6 ıstaka teması, 5 gezegen rengiyle doğru (T19–T21)
- [ ] 5 taş görünümü (Klasik, Galaksi, Altın, Neon, Mermer) mağaza önizlemesiyle aynı yüz/kenar renkleri; rakamlar Galaxy 101 Numerals çizimleri
- [ ] Her taş setinde sahte okey taşı mevcut (atlas + prefab)
- [ ] Taş kalınlığı değişse bile orta dudak üst sıra taşların önünde
- [ ] Andromeda ıstakası masa galaksisiyle çakışmıyor (takımyıldız paneli)
- [ ] Taş kalınlığı 10 mm, yüz rakamı ve nokta net (1080p ve 2K'da)
- [ ] Rakip koltuk sırtında yatay ışık yok; üst bilgi şeridi temiz okunuyor

## Akış
- [ ] Bekleme: tek parça çubuk n/4 sırayla doluyor; davet et; 3-2-1 + kamera inişi (T01, T02)
- [ ] Dağıtım: karma efekti, girdap yolu, 24 ms aralık, gösterge açılışı (T03, T04)
- [ ] 22 taşla ilk hamle atma ile başlıyor
- [ ] Tur süresi halkası ve uyarı rengi; süre bitince otomatik oyun
- [ ] Rakip turları zamanlamaları; rakip ıstakasında taşlar zıplamıyor
- [ ] Aç (≥101), Çift aç (≥5), Yere aç, İşle; "AÇILDI · …" bilgisi; sürükleyerek işleme ve kırmızı uyarı (T14)
- [ ] Açmış rakip işliyor ve bitirebiliyor; bitiş anı (spot, konfeti, kamera, şerit) 3.8 sn (T23)
- [ ] Taş bitti şeridi 2.4 sn
- [ ] El sonu tablosu, satır animasyonları, aldığın ikramlar, "Sonraki el · 15" dolum + otomatik yeni el (T24)
- [ ] Masadan kalk onayı ve lobiye dönüş (T25)
- [ ] Bağlantı koptu: deneme sayacı, dolan çubuk, şimdi dene, lobiye dön (T26)

## Istaka etkileşimi
- [ ] Hover 0.8 cm, seçim 2.2 cm yükselme
- [ ] Tek taş: dolu yuvaya bırakınca swap yok; en yakın boşluğa kayma; kenarda diğer yöne; canlı önizleme
- [ ] Per: 360 ms basılı tut veya Shift; birlikte kalkma; iki yanda 1 boşluk (kenar hariç); diğer taşlar sırasını koruyarak itiliyor; üst üste binme yok; önizleme = sonuç
- [ ] Istaka içi kaymalarda parıltı/zıplama yok
- [ ] Desteden/yerden sürükleyerek çekme, hayalet taş
- [ ] Çift tık ile atma, seç + halkaya tıkla ile atma
- [ ] Seri diz / Çift diz

## İkram, sohbet, ayarlar
- [ ] 10 model birebir (GLB'lerden), panel görselleri görünüyor (T08–T10)
- [ ] Uçuş, iniş, idle animasyonları; alıcının köşesinde en çok 3, en eski değişiyor
- [ ] Roket 5.6 sn ekran turu, duman/kıvılcım, iniş patlaması, kamera sarsıntısı (T11, T12)
- [ ] Herkes ×3 fiyat; bakiye; yetersiz çip durumu
- [ ] İkram bildirimi + Teşekkür et (T06)
- [ ] Oyuncu kartı, hızlı ikram, arkadaş ekle (T07)
- [ ] Sohbet: hızlı cümleler, giriş, balonlar, okunmamış sayacı (T13)
- [ ] Ayarlar: 4 sekme, tüm seçenekler işlevli ve kalıcı, Varsayılana dön (T15–T18)
- [ ] İngilizce tüm metinler (T27); sayı biçimi TR `125.400`, EN `125,400`

## Teknik
- [ ] 60 FPS (orta PC), arka planda render durur
- [ ] 1280×720 / 1920×1080 / 2560×1440'da arayüz keskin, 3D kırpılma yok
- [ ] Model/görünüm ayrımı: taş id'leri, tek sync mekanizması
- [ ] `IMPLEMENTATION_NOTES.md` güncel
