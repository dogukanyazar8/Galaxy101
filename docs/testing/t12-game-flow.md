# T12 — Yerel oyun akışı ve ayarlar

2026-10-20; Unity 6000.6.6f1; Windows x64, Mono/DX11. T12 kapsamı: ana menü, yerel masa, duraklatma, ayrılma onayı, sonuç/yeni el ve yerel ayarlar. Online/mobile ve T13 nihai kabulü bu raporun kapsamında değildir.

## Davranış

- Bootstrap sahnesi düzenlenebilir MainMenu prefabını açar. Yerel Oyna, Table sahnesinde tek oturum ve insan + üç bot başlatır. Çift tıklama ikinci yükleme oluşturmaz. Her yeni masa bağımsız seed alır.
- Odak kaybı yerel oturumu, görüntü hareketini ve sesi duraklatır. Odak geri gelince kullanıcı Devam et seçer. İptal edilmiş komut/önizleme/bot işi yeni komut gibi tekrar gönderilmez.
- Menüye dönüş önce duraklatır. Vazgeç aynı oturumu, otorite hash'ini ve ıstaka yerleşimini korur; oyun duraklatılmış kalır. Onay eski sahipliği kapatır, token'ları iptal eder, bot/komut işini bekler ve ardından Bootstrap yükler. Oyun kaydet/devam et eklenmedi.
- Sonuçta N/J/P/B/M ve toplam puan görünür. AllPairs iptalinde “Puan yazılmadı” gösterilir. Sonraki el, önceki başlangıç koltuğunu bir ilerletir; AllPairs yeniden dağıtımı aynı koltukla başlar. LocalSession.StartNextRound yalnız terminal oturumda kullanılabilir.
- Ayarlar ana menü ve duraklatma ekranında ortaktır: ana ses 0–1, pencere/tam ekran, azaltılmış hareket. Kaydet önce diske yazar, başarılıysa uygular. Vazgeç taslağı uygulamaz. Yazma hatasında alanlar korunur ve yeniden deneme mesajı gösterilir.
- Üretim ayar yolu: `%USERPROFILE%/AppData/LocalLow/Okey101/Okey 101/settings.json` (`Application.persistentDataPath`). Saf C# LocalSettings'in parametresiz yolu LocalApplicationData/Okey101/settings.json; sunum katmanı Unity yolunu açıkça geçirir. Varsayılanlar: ses 1, pencere modu, normal hareket. Dosya yokken dosya oluşturulmaz. Bozuk/uyumsuz/aşırı büyük dosyada iç log + güvenli varsayılan; bozuk dosya silinmez. Gerekli alanlar, sürüm 1, sonlu ses değeri ve 64KiB sınırı doğrulanır. Aynı klasörde benzersiz geçici dosya, Flush(true), atomik replace/move kullanılır; başarısız değiştirmede eski dosya korunur.
- T12 modal pencereleri CanvasGroup ile arka UI etkileşimini engeller, odağı içeri taşır ve kapanınca geri yükler. Çıkış/ayarlar açıkken GameBootstrap devam etmeyi ayrıca reddeder. Tekrar Ayarlar çağrısı düzenlenen taslağı sıfırlamaz.

## Otomatik doğrulama

| Kontrol | Sonuç | Kanıt |
|---|---|---|
| İlk GameFlow RED | 7/7 başarısız | artifacts/test-results/20261020T203557359Z-c1adfcc6/results.xml |
| İlk ayarlar RED | 10 başarısız, 1 geçti | artifacts/test-results/20261020T203625631Z-3f3d5585/results.xml |
| Modal regresyon RED | 2/2 başarısız | artifacts/test-results/20261020T205127187Z-44fa5723/results.xml |
| GameFlow GREEN | 11/11 geçti | artifacts/test-results/20261020T205240539Z-18bb3089/results.xml |
| Son tüm PlayMode | **54/54 geçti** | artifacts/test-results/20261020T205352043Z-e0032856/results.xml |
| Son tüm EditMode, Simulation hariç | **321/321 geçti** | artifacts/test-results/20261020T205427717Z-856b777c/results.xml |

Son tam koşularda başarısız/atlanmış/belirsiz test yok. T12 toplam 11 PlayMode + 13 EditMode testi ekler. Menüden gerçek sahne yükleme, çift başlatma, odak kaybı, animasyonun durması, botun tek kez devam etmesi, onay iptalinde hash/yerleşim korunması, eski oturumun dispose edilmesi, sonuçlar, sonraki başlangıç koltuğu ve modal klavye güvenliği kapsanır. Ayarlar testleri bozuk JSON, eksik alan, sürüm, boyut, NaN/Infinity/aralık ve Windows dosya kilidinde başarısız atomik replace'i kapsar.

TenMenuTableCyclesDisposeEveryOwnerAndAudioProducer testi **10 gerçek Bootstrap/Table döngüsü** yapar: her masada tek GameBootstrap/kamera/AudioListener/TileMotion; taş atışıyla tek AudioSource; menüde eski oturum dispose edilmiş ve TileMotion/AudioSource kalmamış. Bu, otomatik Unity nesne sayımıdır; elle paketlenmiş oyun/Profiler kabulüyle aynı kanıt değildir.

Salt okunur son inceleme modal klavye kaçışını buldu. İki regresyon önce başarısız oldu; ortak ModalInputScope ve oyun sahibi korumasıyla düzeltildi. İlk uygulamada Unity'nin sahte-null Component davranışı nedeniyle CanvasGroup oluşturma hatası görüldü; açık `== null` kontrolüyle giderildi. Açık olmayan modal kardeşleri kilitlenmez; yeni açılan onayın kendi düğmeleri etkin kalır. Son tam PlayMode bu son değişikliği içerir.

## Paket ve native kabul

Son paketler ve elle kontrol sonuçları aşağıdaki teslim kaydında tamamlanır. İlk T12 adayı `20261020T204533653Z-5a651be8` modal düzeltmesinden önceydi; nihai teslim olarak kullanılmaz. Bu adayın menüsü Windows'ta gözle görüldü, ardından test penceresi kapatıldı. Kullanıcı önceki T11 Windows Güvenliği pencerelerini kendisi kapattı; bu yanıt işitsel kabul sayılmaz.

T12 son Release: `artifacts/builds/20261020T205436605Z-68bc6dc7/Okey101.exe`, BuildReport0hata/0uyarı,126693690bayt; Release fixture0 ve tanılama kodu dışlama kontrolü geçti. Kullanıcı Computer Use'u fiziksel Escape ile durdurduğu için native kontrol o anda bırakıldı; Development ve elle10döngü kapısı T13'e taşındı. T13'te bu eski kullanıcının oyununa müdahale edilmedi. Son A4 teslim ve kabul durumu [Windows prosedürü](windows-acceptance.md) ve [A4 raporunda](../../artifacts/acceptance/a4-report.md) izlenir.

T10'un ayrı 1000 dağıtım kanıtı korunuyor; T12 saf kural/bot mantığını değiştirmedi. T13'ün son mantık değişikliğinden sonraki simülasyonu ve 10 dakika kare zamanı/30 dakika kararlılık koşuları burada çalıştırılmadı. T08 tam hazırlanmış elin insan kontrolü ve T11 öznel ses kabulü açık.

## Additional observations

- **minor — URP PostProcessUtils:** Kullanılmayan GaussianDepthOfField/BokehDepthOfField/PaniniProjection shader'larına ilişkin önceki çalışma zamanı uyarıları sürüyor. Bu masada efektler kullanılmıyor. BuildReport uyarı sayısı ile çalışma zamanı uyarıları ayrıdır; T12 renderer değişikliği içermez.
