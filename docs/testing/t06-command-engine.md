# T06 — Komut motoru, sıra ve elin sonuçlandırılması

Durum: Tamamlandı. Tarih: 2026-10-20.

## Uygulanan davranış

`GameEngine.Apply` değiştirilemez bir otorite durumuyla DrawCommand veya CommitTurnCommand alır. Başarı yeni RoundState döndürür; hata kaynak durumu değiştirmez. Unity/Application bağımlılığı yoktur.

- RoundId, aktif oyuncu, artan oyuncu sequence'i, ExpectedVersion ve evre kontrol edilir. Yinelenen/eski sequence DuplicateCommand; farklı sürüm StaleVersion döndürür. Tekrar gönderimde ikisi birden yanlışsa DuplicateCommand önceliklidir.
- Sequence, son kabul edilenden büyük olmalıdır; aradaki sayıları kullanmak zorunlu değildir. Yalnız başarılı komutun oyuncusuna ait kayıt değişir. Reddedilen aynı sequence, düzeltilmiş hamlede kullanılabilir.
- Her başarılı komutta Version tam bir artar. Version/tur sayacı veya sonuç puanı taşması InvalidState ile atomik reddedilir.
- İlk 22 taşlık tur doğrudan planlamadır; tekrar çekiş yapılamaz. Normal çekiş yalnız AwaitingDraw evresinde mümkündür. Kapalı destenin ilk taşı veya önceki oyuncunun son atığı alınır; aynı tur ikinci çekiş reddedilir.
- Yandan çekiş T05'in PlayerView tabanlı kesin aramasından geçer. NoLegalUse veya Cancelled durumunda çekiş, sürüm ve sequence değişmez. Tanık bulunması çekişi kesinleştirir; gerçek masa/atış planı sonraki CommitTurnCommand ile ayrıca doğrulanır.
- Çekişte PendingDraw ve son kapalı taş bilgisi kaydedilir. Son kapalı taşı çekmek eli hemen kapatmaz; oyuncu turunu ve atışını tamamlar.
- Normal tur onayında oyuncu `(actor + 1) % 4` olur; TurnNumber bir artar. PendingDraw ve işleme bütçesi temizlenir; OpeningAtTurnStart sıradaki oyuncunun açılışına ayarlanır.
- Kazanma, dört çift nedeniyle iptal, deste sonu, sonraki tur sırasıyla değerlendirilir. Kazanma aynı anda oluşan iptal/deste sonuna üstün gelir. Bitiş atışına ceza uygulanmaz.
- Terminal sonuçta bitiren/son oynayan oyuncu ve tur numarası korunur. Draw/bütçe temizlenir; sonuç dört oyuncunun fiziksel ellerinden T03 ile hesaplanır. Sahte okey N'ye normal değeriyle, gerçek okey yalnız J'ye girer.
- Dört çiftte bütün HasScore alanları false, Total alanları null olur. P tanısal olarak kalabilir; puan tablosuna yazılmaz. Terminal durumda yeni oyun komutu WrongPhase ile reddedilir.
- İptal token'ı işlem başında, yandan aramada ve kabul sınırında denetlenir. Kaynak durumun dışına yan etki yapılmaz. Oturum kuyruğuna bağlama ve UI iptal yaşam döngüsü T07/T09 kapsamındadır.

## İki farklı doğrulama sınırı

Mevcut `StateInvariant.Validate` fiziksel sahiplik ve karşılık kontrolü olarak korundu. T04/T05'in geçici taslakları ve dar kural fixture'ları bu metodu kullanabilir.

Yeni `StateInvariant.ValidateAuthority` motorun kabul ettiği/kabul sonunda ürettiği durumları denetler:

- 106 benzersiz fiziksel konum; doğru grup yapısı ve grup sahibinin açılış hakkı.
- İlk dağıtımda 22/21/21/21 el, 20 deste, boş masa/atıklar ve sıfır başlangıç sayaçları.
- Sonuçlanmamış ellerde boş oyuncu eli olmaması; çekiş beklenirken en fazla 21, planlarken aktif oyuncuda en fazla 22 taş.
- İlk tur dışında planlama durumunda elde bulunan çekilmiş taş ve doğru WasLastStock bilgisi. Masa taslağından kalmış bütçe/açılış uyuşmazlığı otorite sayılmaz.
- Terminal evre/FinishInfo uyumu, boş kazanan eli, son atış, bitiş bayrakları ve iptal/deste sonu koşulları.
- Bütün ScoreBreakdown alanlarının fiziksel el ve mevcut P üzerinden yeniden hesaplanan sonuçla uyuşması.

Bu API'ler birbirinin yerine kullanılmamalıdır. T07 otoritesi ve ilerideki otorite senaryosu yüklemeleri ValidateAuthority kullanmalı; masa düzenleme önizlemesi tek başına otoriteye çevrilmemelidir.

## Dosyalar

- [GameEngine](../../Packages/com.okey101.core/Runtime/Turns/GameEngine.cs)
- [StateInvariant](../../Packages/com.okey101.core/Runtime/Turns/StateInvariant.cs)
- [Komut/sıra testleri ve fixture yardımcıları](../../Packages/com.okey101.core/Tests/Editor/GameEngineTests.cs)
- [Bitiş ve puan entegrasyon testleri](../../Packages/com.okey101.core/Tests/Editor/RoundEndTests.cs)

Üç yeni C# dosyasının .meta dosyaları Unity tarafından üretildi. Var olan StateInvariant genişletildi; T01–T05 uygulamaları, paketler ve test betikleri değiştirilmedi.

## Gözlenen doğrulama

```powershell
& .\scripts\Test-Unity.ps1 -Platform EditMode -Filter 'Okey101.Core.Tests.GameEngineTests;Okey101.Core.Tests.RoundEndTests'
& .\scripts\Test-Unity.ps1 -Platform EditMode
```

- İlk RED: **40 test, 38 başarısız**. Derlenen minimum motor InvalidState dönüyordu; otorite doğrulaması yalnız fiziksel kontrol yapıyordu. Sayaç ve sonuç taşması testleri bu minimum API'de zaten geçiyordu.
- İlk uygulama GREEN: **40/40**. İlk turda bitiş, sequence sıçraması, bozuk grup/otorite durumları ve iptal puanı kontrolleriyle **50/50**.
- Ek regresyon RED: **1/1 başarısız**. Son oyuncunun atığını kaldırıp taşı desteye taşıyan, fiziksel olarak tutarlı iptal kaydı ValidateAuthority tarafından kabul ediliyordu. Motor zaten atışı ekliyordu; eksik olan terminal doğrulamasıydı. Her terminal sonuçta tamamlanmış atış kontrolü eklenince dar kontrol **51/51** geçti.
- Son tam EditMode: **272/272 geçti; 0 başarısız, 0 atlanan, 0 belirsiz.** GameEngineTests 34, RoundEndTests 17; önceki 221 test de çalıştı. Son Unity günlüğünde C# derleyici hata/uyarısı bulunmadı.

[İlk RED](../../artifacts/test-results/20261020T173801859Z-e926bd49/results.xml)

[İlk GREEN](../../artifacts/test-results/20261020T173955846Z-6c156dcf/results.xml)

[Terminal atış kontrolü RED](../../artifacts/test-results/20261020T174245240Z-e81a49fb/results.xml)

[Son dar GREEN](../../artifacts/test-results/20261020T174301020Z-624c85d1/results.xml)

[Son tam NUnit sonucu](../../artifacts/test-results/20261020T174318482Z-3ccb990f/results.xml)

[Son Unity günlüğü](../../artifacts/test-results/20261020T174318482Z-3ccb990f/editor.log)

Dört başlangıç koltuğunun her birinde açılış yapmadan kapalıdan çekme/atma döngüsü deste sonuna kadar yürütüldü: 1 ilk atış + 20 çekiş + 20 atış = **41 kabul edilen komut**. Her adımda 106 kimlik, kaynak değişmezliği, sürüm ve yalnız ilgili sequence'in değişimi doğrulandı. Bu, bot stratejisi veya T10'un 1.000 dağıtımlık simülasyonu değildir.

Ek örnekler: aynı tur çift açıp gerçek okeyle bitiş −808; sahte okeyle aynı bitiş −404; kazanan P = 202 ise M = 8 ile +808. Red/7 göstergesinde eldeki sahte okey N = 8, gerçek okey J = 1; seri 109, çift 218. Dört çift iptali son kapalı taş turunda da puansızdır.

## Sonraki adım ve sınırlar

Motor iptalde otomatik dağıtım yapmaz; terminal sonucu korur. Yeni el çağıranı RoundFactory'ye yeni RoundId/seed ve aynı StartingPlayer verir. Bu kullanım testi geçti; menü/yeni el akışına bağlanması T12 kapsamındadır.

İlk etkileşimli masa, yerel komut kuyruğu ve Development Windows paketi T07 kapsamındadır. Bu görevde PlayMode, Windows build veya görsel/oynanabilir masa kabulü çalıştırılmadı. Git init/commit/push ve yeni bağımlılık yok.
