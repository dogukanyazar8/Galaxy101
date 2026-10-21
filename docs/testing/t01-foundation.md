# T01 — Proje ve dağıtım temeli

Durum: Tamamlandı. Tarih: 2026-10-20.

## Çıktı

- Resmi URP şablonundan oluşturulmuş Unity 6000.6.6f1 projesi.
- URP 17.6.0, Input System 1.20.0, uGUI 2.6.0, editörün desteklediği Test Framework 1.8.0; manifest ve lock sabit.
- UnityEngine referansı olmayan `Packages/com.okey101.core` paketi.
- 106 ayrı taş kimliği; gerçek/sahte okey karşılıkları; kopyalanarak korunan durum, grup, komut ve oyuncu görünümü sözleşmeleri.
- Tekrarlanabilir karıştırma; ayrı gösterge; 22/21/21/21 dağıtım ve 20 kapalı taş.
- Mevcut projeye yazmayı reddeden başlangıç betiği ve gerçek NUnit sonucunu doğrulayan test betiği.

## Gözlenen doğrulama

```powershell
& .\scripts\Test-Unity.ps1 -Platform EditMode
```

Sonuç: **21 test, 21 geçti; 0 başarısız, 0 atlanan, 0 belirsiz.**

- [NUnit sonuç dosyası](../../artifacts/test-results/20261020T162838694Z-1f8247b5/results.xml)
- [Unity çalışma günlüğü](../../artifacts/test-results/20261020T162838694Z-1f8247b5/editor.log)

Kimlikler, dağıtım/korunum, gösterge 13 sarması, karıştırma sabit vektörü, sınır reddetme, koleksiyon izolasyonu, eksik/çift taş konumu ve UnityEngine bağımlılığı test edildi.

Rastgelelik sınır regresyonu önce `Expected: 0 / But was: 1` ile başarısız oldu; düzeltme sonrası aynı test ve tüm EditMode grubu geçti. İlk API testi ise model henüz yokken derleme hatası verdi; bu, çalışan bir assertion başarısızlığı olarak raporlanmıyor.

İşletim kontrolleri: mevcut proje üzerine başlangıç reddedildi; aktif dosya kilidinde test başlatılmadı; boş filtre total=0 sonucunda başarı sayılmadı. Stale kilit dosyası varlığı tek başına açık Editor sayılmıyor; dosya sahipliği deneniyor, süreç öldürülmüyor.

## Görsel üretim ve sınırlar

Blender 5.2 kurulu. Onaylı tasarımda 3D masa/ıstaka/taş varlıkları Blender ile; materyal, ışık, animasyon ve 2D uGUI arayüzü Unity'de hazırlanacak. T01'de özel Blender varlığı üretilmedi; grafik üretimi T11'de.

Oynanabilir masa, botlar, Windows exe, PlayMode/cihaz/görsel/performans kabulü bu aşamada yoktur. Git deposu oluşturulmadı; commit/push yapılmadı. Tekrar testinde Editor'ün bu projeyi kapatmış olması gerekir; test betikleri PowerShell 7 kullanır.

[Uygulama planı](../superpowers/plans/2026-10-20-windows-first-playable.md)
