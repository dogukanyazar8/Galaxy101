# T03 — Puan hesabı ve özel bitişler

Durum: Tamamlandı. Tarih: 2026-10-20.

## Uygulanan davranış

- Saf `ScoreCalculator.Calculate`, R12–R16'ya göre bir oyuncunun el sonucunu üretir; Unity bağımlılığı yoktur.
- Temel puan B: kazanan −101; seri kaybeden N + 101J; çift kaybeden 2 × (N + 101J); açmamış oyuncu 202.
- Gerçek okeyle bitiş, kazananın çift açması ve elden bitiş bağımsız ×2 çarpanlarıdır. Sekiz birleşimde M = 1/2/4/8; kazanan dahil herkese uygulanır.
- Sonuç `(B + P) × M`. Kaybedenin çift açması P'yi ayrıca ikiye katlamaz. Kazananın birikmiş cezaları da son çarpana dahildir.
- Deste biterse kazanan yoktur; M = 1, kaybeden hesabı kullanılır.
- Dört çift iptalinde `HasScore=false`, `Total=null`. Tanısal N/J/P korunur; B = 0 ve M = 1. Önceki el kayıtlarına bu hesaplayıcı erişmez.
- Negatif N/P, 0–2 dışı J, geçersiz açılış, eksik bitiş bilgisi ve kazanan/bitiş uyuşmazlığı programlama hatası olarak reddedilir. Tamsayı taşması `OverflowException` üretir; puan sessizce ters işarete dönmez.

## Dosyalar

- [Hesaplayıcı](../../Packages/com.okey101.core/Runtime/Scoring/ScoreCalculator.cs)
- [Testler](../../Packages/com.okey101.core/Tests/Editor/ScoreCalculatorTests.cs)

## Gözlenen testler

```powershell
& .\scripts\Test-Unity.ps1 -Platform EditMode -Filter 'Okey101.Core.Tests.ScoreCalculatorTests'
& .\scripts\Test-Unity.ps1 -Platform EditMode
```

- İlk RED: 43 test, 37 başarısız. Derlenen minimum API hatalı puan dönüyor; hesaplama ve girdi denetimi assertion'ları başarısız oluyordu. Mevcut FinishInfo denetimlerini sınayan dört test ve sıfır sonucu bekleyen iki test zaten geçiyordu.
- Uygulama sonrası dar kontrol: **43/43 geçti**.
- Tam EditMode: **97/97 geçti; 0 başarısız, 0 atlanan, 0 belirsiz.** Önceki 54 test de çalıştı.
- Kapsam: onaylı sayısal örnekler; sekiz çarpan birleşiminde kazanan/kaybeden; kazananın P = 101/202 durumları; iki gerçek okey; sıfır normal toplam; açmamış oyuncuda eldeki okeyin eklenmemesi; deste sonu; puan yazılmayan iptal; geçersiz girdiler; temel toplam, çift hesabı, ceza ekleme ve son çarpmada taşma.

[RED NUnit sonucu](../../artifacts/test-results/20261020T164736379Z-2e29221b/results.xml)

[Dar GREEN sonucu](../../artifacts/test-results/20261020T164800341Z-db8dad6d/results.xml)

[Tam NUnit sonucu](../../artifacts/test-results/20261020T164817252Z-88d6da80/results.xml)

[Tam Unity günlüğü](../../artifacts/test-results/20261020T164817252Z-88d6da80/editor.log)

## Çağıran sözleşmesi ve sınırlar

N/J hesabını çağıran yapar: sahte okey normal yüz değeriyle N'ye girer; gerçek okey yalnız J'ye girer. Son atışın gerçek okey olup olmadığını FinishInfo bildirir. Hesaplayıcı fiziksel taşları veya atışı incelemez.

`isWinner`, çağıran tarafından oyuncu kimliğiyle FinishInfo.Winner karşılaştırılarak seçilir. Seçilen kazananın açılışı FinishInfo.WinnerOpening ile eşleşmelidir. Elin bitmesi, tur cezasının oluşması ve dört oyuncunun sonucunun birlikte kaydedilmesi T05/T06 kapsamındadır.

İptal kayıtlarındaki P yalnız tanısaldır; puan tablosuna HasScore/Total dikkate alınarak yazılmalıdır. Açmamış veya iptal edilmiş elde kullanılmayan N/J toplamı hesaplanmaz; gereksiz taşma oluşmaz.

PlayMode ve Windows build çalıştırılmadı. Görsel masa veya oynanabilir sürüm kabulü yapılmadı. Yeni bağımlılık veya Git işlemi yok.
