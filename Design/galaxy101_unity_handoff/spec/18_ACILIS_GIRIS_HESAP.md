# 18 — Açılış (yükleme), giriş ekranları, hesap ve kalıcı kayıt

Görseller: `screenshots/v3/S01`–`S06`. Kaynak: `reference/game/Galaxy 101 - Oyun.dc.html` (template + `class Component`).

## Ekran akışı
`splash` → (kayıtlı hesap varsa) `menu` / (yoksa) `auth` → `login` | `register` | `guest` | Google → `menu` → `lobby` → `loading` → `game` → `lobby`.
URL `?to=lobby` ile açılırsa ve hesap kayıtlıysa doğrudan lobiye gidilir (masadan kalkınca kullanılır).

## Açılış / yükleme (S01)
- Arka plan: radyal degrade `radial-gradient(120% 110% at 75% 30%, --bg1, --bg2 50%, --bg3)` + canlı yıldız alanı + prosedürel gezegen (`planet-renderer.js`). Gezegen splash konumu x 640, y 330, ölçek 1.
- **GALAXY** logosu: Montserrat 700 66px, letter-spacing 8px, dikey altın degrade (`--lg1` → `--a1` 45% → `--a2`). Giriş animasyonu `logoIn`: 1.1s, gecikme .85s, `cubic-bezier(.16,1,.3,1)`, opacity 0→1, translateY 14→0, scale .94→1, letter-spacing 22px→8px.
- **1 0 1 taşları** (78×108, 88×122, 78×108; köşe 12/13; sabit eğim −10°, 2°, 10° — eğim `rotate` özelliğinde sabit tutulur, animasyon sadece `translate` kullanır):
  - Giriş `tileIn` 1.15s, gecikmeler .1/.3/.5s, `cubic-bezier(.16,1,.3,1)`: translateY 80→0, rotateX −80°→0, scale .78→1, perspective 900px, origin alt orta.
  - Süzülme `g101Float`: 6s sonsuz, `cubic-bezier(.45,0,.55,1)`, translate 0 → −7px → 0, taş başına faz −0s / −2s / −4s (dalga). **Gecikmeli başlatma yok** (eski sürümdeki sağa-sola sıçrama buydu).
  - Parıltı `g101Glint`: 7s, taşın %38 genişliğinde beyaz şerit, %62–86 arasında soldan sağa geçer, opaklık 0→1→0.
- İlerleme çubuğu + ipucu metni; %100 olunca `afterSplash`.
- **Çıkış geçişi**: `splashOut` .75s `cubic-bezier(.7,0,.3,1)`: opacity 1→0, scale 1→.96, blur 0→10px. Taşlar aynı anda `tilesOut` (translateY −36px, scale 1.14). Gezegen x 640, y 340, ölçek 1.5, opaklık 0'a kayar. 720 ms sonra hedef ekrana geçilir.
- **Ana menü girişi** (yalnızca splash'tan sonra): `menuIn` 1.2s `cubic-bezier(.16,1,.3,1)`: opacity 0→1, scale 1.05→1, blur 12px→0 (%60'ta 0). Diğer geçişlerde .35s fade.

## Giriş seçimi (S02)
Sağda panel: "Hoş geldin" başlığı, **MİSAFİR OLARAK OYNA** (birincil, mavi), **Giriş yap**, **Kayıt ol**, **Google ile devam et**, dil seçici TR/EN (sağ üst). Gezegen auth konumu x 310, y 340, ölçek 1.05.

## Giriş yap (S03)
Alanlar: e-posta, şifre (göster/gizle), "Beni hatırla", "Şifremi unuttum". Hatalar: hesap yok; yanlış şifre "N deneme hakkın kaldı"; 3 yanlışta kilit. 900 ms sahte gecikme.
## Kayıt ol (S04)
Alanlar: ad, e-posta, şifre, şifre tekrar, şartlar onayı (zorunlu). 1100 ms gecikme. Başarıda hesap kaydedilir ve giriş yapılır.
## Misafir (S05)
Takma ad (en çok 16, varsayılan `Misafir####`), 8 avatar rengi seçimi, "misafir ilerlemesi kaydedilmez" uyarısı. 700 ms gecikme.

## Başlangıç bakiyesi
| Yöntem | Çip |
|---|---|
| Misafir | **10.000** |
| Kayıtlı (e-posta) / Google | **25.000** |
Lobide bakiyenin yetmediği bahisler kilitli görünür ("Yetersiz bakiye").

## Kalıcı kayıt (Unity'de PlayerPrefs / dosya)
| Anahtar | İçerik |
|---|---|
| `galaxy101.save.v1` | `{ acct, accounts{email:{name,pass(hash),color}}, planet, lang, wallet{chips,gems} }` |
| `galaxy101.fps` | `"auto"` ya da sayı |
| `galaxy101.gres` | seçili çözünürlük "2560×1440" |
| `galaxy101.wmode` | "Tam ekran" / "Çerçevesiz" / "Pencere" |
Tema ve dil tüm ekranlarda anında uygulanır ve kaydedilir.
