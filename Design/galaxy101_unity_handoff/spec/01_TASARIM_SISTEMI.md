# 01 — Tasarım Sistemi

Tüm ölçüler **1280×720 tasarım birimi** (px) cinsindendir. Unity'de PanelSettings referans çözünürlüğü 1280×720 olduğunda 1 px = 1 UI Toolkit birimi.

## 1. Tema değişkenleri

Arayüzün tüm renkleri temaya bağlı **değişkenlerden** gelir. Gezegen değişince bütün arayüz (zemin, paneller, vurgu, butonlar, logo, taşlar, yıldız rengi, gezegen) birlikte değişir. Mod renkleri ve alt menü buton renkleri (Arkadaşlar mavi, Liderlik mor, Günlük ödül yeşil, Kurallar camgöbeği) **temadan bağımsızdır**; Mağaza butonu temanın vurgu rengini kullanır.

| Değişken | Anlamı | Satürn (varsayılan) |
|---|---|---|
| `bg1` | Zemin degradesi iç renk | `#2A2F6E` |
| `bg2` | Zemin degradesi orta | `#161A45` |
| `bg3` | Zemin degradesi dış / en koyu | `#0A0C28` |
| `pn` | Panel / pencere zemini | `#232A66` |
| `pnH` | Panel hover / ikincil buton | `#2D3680` |
| `pnS` | Panel alt gölgesi (3D kenar) | `#0B0D2B` |
| `dp` | Çukur alan (input, pill, track) | `#0B0E2E` |
| `sd` | Pencere içi kart / yan menü | `#10133A` |
| `bd` | Kenarlık / scrollbar | `#2E3680` |
| `a1` | Vurgu açık (ana) | `#FFD964` |
| `a2` | Vurgu koyu | `#F5A623` |
| `aS` | Vurgu buton alt gölgesi | `#B86E0F` |
| `aT` | Vurgu üstü metin | `#3A1F00` |
| `lS` | Logo alt gölgesi | `#8A4E06` |
| `gl` | Vurgu parlaması (glow) | `rgba(255,201,74,.45)` |
| `mt` | İkincil (soluk) metin | `#A9AEDD` |
| `ic` | İkon rengi | `#D3D6FF` |
| `st` | Yıldız tonu | `#FFE7A3` |
| `pg` | Gezegen hale rengi | `rgba(255,214,140,.35)` |
| `atm` | Gezegen atmosfer kenarı | `rgba(255,226,175,.55)` |
| `lg1` | Logo degradesi üst renk | `#FFF4C8` |
| `tf1`,`tf2` | Logo taşı yüzey degradesi | `#FFFDF6` → `#EEE2C6` |
| `te`,`te2` | Logo taşı iç kenar / alt kenar | `#D9C9A2` / `#A8925F` |
| `t1`,`t2`,`t3` | 1-0-1 rakam renkleri | `#C8462E` / `#2B5DA8` / `#D9951A` |

**5 temanın tüm değerleri:** `data/themes.json` (Satürn, Mars, Neptün, Jüpiter, Venüs). Unity'de her tema bir USS dosyası (`theme-saturn.uss` …) veya bir `ThemeSO` ScriptableObject olmalı.

Gezegen seçicideki küre renkleri (`pickerOrb`, iç→orta→kenar radyal degrade) de `themes.json`'dadır.

## 2. Sabit renkler (temadan bağımsız)

| Kullanım | Renk |
|---|---|
| Ana metin | `#F4F1E8` |
| Başarı / ekle / toggle açık | `#3CCB6E`, alt gölge `#22914A`, açık metin `#8EF0B0` |
| Uyarı / rozet / sil | `#FF5E6C`, alt gölge `#C23E4A`, açık metin `#FF8A94` |
| Altın derece | `#FFC94A`, elmas `#FFE08A → #F5A623` |
| Elmas (para birimi) | `#7FE3FF → #3D7BFF`, açık `#C8F6FF` |
| Coin | radial `#FFE9A8 → #F5A623 (60%) → #B86E0F`, 2px kesikli kenar `#FFF3C8` |
| Mod: Tekli | `#3D7BFF` / gölge `#2148B0` |
| Mod: Eşli | `#FF6B6B` / `#C23E4A` |
| Mod: Katlamalı | `#8B5CF6` / `#5B34B8` |
| Mod: Dereceli | `#16B6A8` / `#0C7F75` |
| Mod: Botlara karşı | `#3CCB6E` / `#22914A` |
| Mod: Özel oda | `pnH` / `pnS` (tema) |
| Alt menü: Arkadaşlar | 160° `#7AA6FF → #3D6BF0`, gölge `#2448B8`, glow `rgba(80,130,255,.4)`, etiket `#A9C4FF` |
| Alt menü: Liderlik | 160° `#C2A4FF → #7C4DEB`, gölge `#5530B0`, glow `rgba(150,110,255,.4)`, etiket `#D4C2FF` |
| Alt menü: Günlük ödül | 160° `#4FDB80 → #25A857`, gölge `#18793F`, glow `rgba(60,203,110,.4)`, etiket `#8EF0B0` |
| Alt menü: Kurallar | 160° `#5BE6D6 → #14A596`, gölge `#0C746A`, glow `rgba(40,210,190,.4)`, etiket `#9AF2E8` |
| Alt menü: Mağaza | 160° `a1 → a2`, gölge `aS`, glow `gl`, etiket `a1` |
| Profilim pencere ikonu | 160° `#FFA27A → #F0683A`, gölge `#B2441E` |
| Taş renkleri (kurallar) | Kırmızı `#C8302E`, Sarı `#D9951A`, Mavi `#2B5DA8`, Siyah `#1E1E24` |
| Avatar renkleri (seçilebilir) | `#FF8A5B #FF5E6C #E85D9A #8B5CF6 #3D7BFF #16B6A8 #3CCB6E #F5A623` |

## 3. Tipografi

| Rol | Font | Ağırlık | Boyut |
|---|---|---|---|
| Logo "GALAXY" | Montserrat | 700 | 64 / satır 1.15 / harf aralığı 7 |
| Pencere başlığı | Montserrat | 700 | 26 (alt pencere 22–24) |
| Bölüm başlığı | Montserrat | 700 | 20–24 |
| Büyük buton (HEMEN OYNA) | Montserrat | 700 | 34 / harf aralığı 1 |
| Kart / satır başlığı | Montserrat | 700 | 15–18 |
| Buton metni | Montserrat | 700 | 14–17 |
| Büyük sayı (istatistik) | Montserrat | 700 | 22–28, tabular rakam |
| Gövde | Baloo 2 | 700 | 13–16 |
| Yardımcı metin | Baloo 2 | 700–800 | 11–13, renk `mt` |
| Etiket (BÜYÜK HARF) | Baloo 2 | 900* | 9–11, harf aralığı 1–1.5 |

\* Baloo 2'nin en kalın ağırlığı 800'dür; 900 istenen yerlerde 800 kullanılır (prototipte de tarayıcı 800'e düşer).

Türkçe büyük harf dönüşümü `tr-TR` kültürüyle yapılmalı (i → İ, ı → I).

## 4. Şekil, gölge, köşe

**"Bastırılabilir 3D" stil**: Butonlar ve kartlar düz renk/degrade + alttan **sert gölge** (`0 Npx 0 <koyu renk>`, blur yok). Basılınca (active) eleman `translateY(+3…5px)` iner ve sert gölge küçülür/kaybolur.

| Öğe | Köşe | Sert alt gölge |
|---|---|---|
| HEMEN OYNA | 24 | 7px `aS` |
| Mod kartı | 18 | 5px mod gölgesi |
| Alt menü ikon karesi | 18 (56×56) | 4px |
| Ayarlar butonu | 14 (46×46) | 4px `pnS` |
| Pencere | 28 (alt pencere 26) | 10px `pnS` + `0 40px 90px rgba(0,0,0,.6)` |
| Kart (pencere içi) | 18–22 | yok |
| Buton (pencere içi) | 12–14 | 3–4px |
| Sekme grubu | 14 dış / 10 iç | aktif sekme 3px `aS` |
| Input | 14 | iç gölge `inset 0 2px 0 rgba(0,0,0,.25)` |
| Pill (para birimi) | 999 | iç gölge |
| Toggle | 16 (56×32) | iç gölge |

Unity UI Toolkit'te box-shadow yoktur: sert gölgeler için elemanın arkasına aynı boyutta, `translate`'li koyu bir kardeş eleman (veya 9-slice sprite), yumuşak glow'lar için 9-slice blur sprite kullanın.

## 5. Boşluk ölçeği

4, 6, 8, 10, 12, 14, 16, 18, 20, 22, 24, 28 px. Pencere iç kenar boşluğu 24, kart iç boşluğu 14–18, liste satır aralığı 8.

## 6. Scrollbar

Tüm kaydırılabilir alanlarda scrollbar için **yer her zaman ayrılır** (`scrollbar-gutter: stable`): scrollbar çıkınca/kaybolunca içerik yatayda **kaymaz**. İnce (thin), renk `bd`, zemin şeffaf. Unity: ScrollView `verticalScrollerVisibility = AlwaysVisible` veya sabit sağ padding.

## 7. İkonlar

Tümü 24×24 viewBox, `stroke-linecap/linejoin: round`. Prototipte satır içi SVG; path verileri HTML'de bulunur.

| İkon | Nerede | Stroke |
|---|---|---|
| Dişli (ayarlar) | sağ üst, ayarlar başlığı | 2.2 |
| Dükkan cephesi | Mağaza | 1.9 |
| İki kişi | Arkadaşlar | 1.9 |
| Kupa | Liderlik | 1.9 |
| Hediye kutusu | Günlük ödül | 1.9 |
| Kitap + soru işareti | Kurallar | 1.9 |
| Kişi | Profilim | 1.9 |
| Paylaş (3 düğüm) | Profili paylaş | 2.2 |
| Kalem | Profili düzenle | 2.4 |
| Büyüteç | arama | 2.4 |
| Kişi + artı | Arkadaş ekle | 2.2–2.4 |
| Kopyala (iki kare) | kod/bağlantı kopyala | 2.2–2.4 |
| Bağlantı (zincir) | paylaşım linki | 2.4 |
| Saat | geri sayım | 2.4 |
| Kilit | kilitli çerçeve | 2.6 |
| Çarpı | kapat | 2.8 |
| Onay tiki | seçili / tamamlandı | 3.4–3.6 |
| Çıkış oku | çıkış onayı | 2.2 |
| Çöp kutusu | hesap sil | 2.2 |
| Taç | liderlik 1. | dolgu `#FFD964`, kenar `#B8790F` |
