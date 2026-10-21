# T05 — Tam tur, yandan alma araması ve cezalar

Durum: Tamamlandı. Tarih: 2026-10-20.

## Uygulanan davranış

- `TurnEvaluator.Evaluate` önce T04 masa planını, ardından eldeki zorunlu son atışı, R10 kullanımını ve cezayı doğrular. Başarı yeni aday RoundState üretir; hata kaynak durumu değiştirmez. 106 fiziksel kimlik korunur.
- Bütün taşları masaya bırakıp atış yapmama LastTileRequired döndürür. Atılacak taşın aynı fiziksel kimliği elde olmalıdır; açılmamış oyuncunun son tek taşı atarak kazanması kabul edilmez.
- Yandan alınan taş aynı tur sonunda masada bulunmalıdır. Aynı yüzlü diğer kopyayı kullanmak, alınanı geri atmak veya kullanılan gerçek okeyi geri alıp elde tutmak R10'u sağlamaz.
- Oyuncu tur başında açılmamışsa yandan alınan kimlik ilk OpenGroups eylemine dahil olmalıdır. Geçerli 101 açtıktan sonra bu taşı yalnız işleme/değişimde kullanmak yeterli değildir.
- Bitiş dışı gerçek okey atışı veya mevcut hakkıyla açık seri/sayı grubuna doğrudan işlenebilen taşın atışı +101'dir. Aynı atışa iki ceza yazılmaz. Çift alanına yeni grup ve okey değiştirme ihtimalleri işlek taş cezasına girmez.
- Bitiş atışında ceza sıfırdır. Çift açanın bütçesi dolmuşsa normal işlek taş cezası oluşmaz; gerçek okey cezası devam eder. Sahte okey normal yüzüyle değerlendirilir; mevcut okey karşılığı ceza hesabında kaydırılmaz.
- Cezalı geçerli atış AcceptPenalty=false ise PenaltyConfirmationRequired döndürür; true ise P yalnız 101 artar. P taşması InvalidState ile atomik reddedilir.
- `PlayerViewProjector` yalnız ilgili oyuncunun elini/sequence bilgisini ve kamu durumunu aktarır. Başka oyuncunun PendingDraw bilgisi aktarılmaz; rakip ellerinin yüzü ve deste sırası görünümde bulunmaz.

## Kesin arama

`DiscardTurnFinder.Find` yalnız AwaitingDraw evresindeki aktif oyuncunun PlayerView bilgisini alır. Önceki koltuğun `(actor + 3) % 4` son atığını kullanır; eski atıklar veya başka oyuncuların atıkları aday değildir.

Görünmeyen fiziksel kimlikler, yalnız kamuya açık adetleri karşılamak için varsayımsal rakip elleri/desteye yerleştirilir. Gerçek özel RoundState aramaya verilmez. Arama bu varsayımsal ellerin yüzlerine veya deste sırasına göre karar vermez. Testlerde farklı gizli taş dağılımlarından çıkan planlar gerçek çekiş durumlarında çapraz doğrulandı.

Açılmamış oyuncuda bütün fiziksel grup/joker seçenekleri üzerinden çakışmayan açılış alt kümeleri aranır; alınan taş ilk açılışta olmalı ve atış için en az bir taş kalmalıdır. Fiziksel maskeler, kalan taşlardan elde edilebilecek güvenli üst sınırlar ve başarısız alt durum önbelleği kullanılır. Önbellek, kalan maske/harcanan taş/eşik değeri/alınan taş üyeliği/seçilen grup sayısını içerir. P tamsayı sınırına yakınsa farklı açılış karşılıkları birleştirilmez.

Açılmış durumda izinli yeni gruplar, seri/sayı uçlarına tek taş eklemeleri ve eşdeğer taşla okey değişimleri aranır. Çok taşlı geçerli uç ekleme aynı bütçe ve karşılıklarla tek taşlık adımlara ayrılabilir. Aynı türde çok grup ekleme de AddGroup adımlarına ayrılır. Bu ayrıştırma geçerli yolu elemez.

Ziyaret anahtarı elin fiziksel kimlikleri, açılış türü, tur bütçesi ve bütün grupların kimlik/sahip/tür/taş karşılıklarını içerir. Farklı okey karşılıkları aynı durum sayılmaz. Her adım eldeki `2 × joker olmayan taş adedi + gerçek okey adedi` değerini azaltır; 22 taşlık elde masa araması derinliği en fazla 44'tür.

Bulunan her tanık, özgün varsayımsal çekişe karşı TurnEvaluator ile baştan doğrulanır. Tanık AcceptPenalty=true taşıyabilir: bu, cezalı fakat geçerli tur bulunduğunu gösterir; kullanıcının gerçek hamlesindeki ceza onayının yerine geçmez.

Aramada aday/süre/strateji bütçesi sınırı yoktur. NoLegalUse ancak geçerli yollar tükendiğinde; iptal ise Cancelled olarak döner. Mevcut üç parametreli MeldCandidateGenerator API'si korundu; CancellationToken alan ek overload, grup/joker üretimi sırasında da iptali denetler. Sıralama öncesindeki üretim ve sıralama sonrasında iptal kontrolü vardır; sıralama karşılaştırıcısının içinden exception atılmaz.

## Dosyalar

- [Oyuncu görünümü](../../Packages/com.okey101.core/Runtime/Model/PlayerViewProjector.cs)
- [Tur doğrulaması](../../Packages/com.okey101.core/Runtime/Turns/TurnEvaluator.cs)
- [Cezalar](../../Packages/com.okey101.core/Runtime/Rules/PenaltyEvaluator.cs)
- [Kesin arama](../../Packages/com.okey101.core/Runtime/Rules/DiscardTurnFinder.cs)
- [İptal destekli grup üretimi](../../Packages/com.okey101.core/Runtime/Rules/MeldCandidateGenerator.cs)
- [Tur testleri](../../Packages/com.okey101.core/Tests/Editor/TurnEvaluatorTests.cs)
- [Arama/görünüm testleri](../../Packages/com.okey101.core/Tests/Editor/DiscardTurnFinderTests.cs)
- [Ceza testleri](../../Packages/com.okey101.core/Tests/Editor/PenaltyTests.cs)

Yeni yedi kaynak dosyanın .meta dosyaları Unity tarafından üretildi. Model API'leri, T04, puanlama, paket sürümleri ve test betikleri değiştirilmedi.

## Gözlenen doğrulama

```powershell
& .\scripts\Test-Unity.ps1 -Platform EditMode -Filter 'Okey101.Core.Tests.TurnEvaluatorTests;Okey101.Core.Tests.DiscardTurnFinderTests;Okey101.Core.Tests.PenaltyTests'
& .\scripts\Test-Unity.ps1 -Platform EditMode
```

- İlk RED: **43 test, 38 başarısız**; derlenen minimum API'lerde beklenen durum/hata/ceza assertion'ları başarısız oldu. Zaten sıfır ceza bekleyen dört test ve InvalidState bekleyen taşma testi geçiyordu.
- Tur/ceza uygulaması: **27/27** geçti.
- Üretici iptali ayrı RED: **1 test, 1 başarısız**; iptal edilmiş token üretimi durdurmuyordu. Overload uygulaması sonrasında arama dahil dar kontrol **44/44** geçti.
- Üç adımlı swap/işleme yolu, farklı okey karşılıkları, dört koltuk, yoğun el, iki okeyle çift, bozuk kamu girdileri ve başladıktan sonra iptal: dar kontrol **53/53** geçti. Üretim sırasında iptal testi kontrollü IReadOnlyList okumasında token'ı iptal eder; zamanlayıcı yarışına dayanmaz.
- 100/101 arama sınırı, yandan sahte/gerçek okey ve son kapalı taş bilgisini koruma dahil dar kontrol **58/58** geçti.
- Grup kimliği alanı sınırında ayrı kontrol **2/2** geçti. Önbellek anahtarı grup sayısını da kapsayacak şekilde güçlendirildi. Ayrı bir tanısal Python modelinde 748 normal taş örneğinde yanlış negatif üretilmedi; bu tarama regresyon kanıtı veya Unity kabulü olarak sunulmaz.
- Son tam EditMode: **221/221 geçti; 0 başarısız, 0 atlanan, 0 belirsiz.** T05: DiscardTurnFinderTests 32, TurnEvaluatorTests 14, PenaltyTests 14; önceki 161 test de çalıştı.

[İlk RED](../../artifacts/test-results/20261020T171704396Z-e477a750/results.xml)

[Üretici iptali RED](../../artifacts/test-results/20261020T171856054Z-f4133c7a/results.xml)

[İlk birleşik GREEN](../../artifacts/test-results/20261020T172016379Z-ced19ddc/results.xml)

[58 testlik dar GREEN](../../artifacts/test-results/20261020T172414734Z-7f980972/results.xml)

[Kimlik sınırı kontrolü](../../artifacts/test-results/20261020T172746074Z-7dda64d9/results.xml)

[Son tam NUnit sonucu](../../artifacts/test-results/20261020T172800964Z-8d186892/results.xml)

[Son Unity günlüğü](../../artifacts/test-results/20261020T172800964Z-8d186892/editor.log)

Yoğun 22 taş senaryosu kırmızı 1–10'un iki kopyası ve iki gerçek okeyi kullanır. Önceki tam çalışmada arama ve tanık doğrulaması **672 ms** ölçüldü. Bu tek Editor ölçümüdür; süre testi geçme eşiği değildir ve tüm eller için süre garantisi vermez.

## Sonraki görevler için sınırlar

Kesin arama kombinatoriktir; bazı olumsuz eller daha pahalı olabilir. T07/T09 uygulama kuyruğu bunu görüntü döngüsü dışında çalıştırmalı ve eski el/duraklatma/kapanışta iptal etmelidir. Botun strateji süresi dolması NoLegalUse sayılmamalıdır.

TurnEvaluator sürüm, sequence, aktif oyuncu, tur numarası, PendingDraw, OpeningAtTurnStart ve son kapalı taş bilgisini korur. Son atışın kazanma/iptal/deste sonu önceliğiyle nihai sonuca çevrilmesi, komut yetkisi ve sonraki sıra T06 kapsamındadır. T03 puan hesabı bu görevde çağrılmaz.

PlayMode, Windows build ve oynanabilir/görsel masa kabulü çalıştırılmadı. Git init/commit/push veya yeni bağımlılık yok. Sıradaki görev T06.
