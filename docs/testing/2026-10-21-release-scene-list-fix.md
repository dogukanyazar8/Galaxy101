# Release masa açılışı — sahne listesi düzeltmesi

21 Ekim 2026. Kullanıcının son Player.log kaydı `GalaxyTable scene is missing from the Windows build.` hatasını doğruladı. Menü artık GalaxyTable açarken özel Windows derleyicisi hâlâ yalnız Bootstrap/Table paketliyordu; Editor paylaşılan sahne listesinde GalaxyTable zaten bulunuyordu.

`Assets/Okey101/Editor/BuildWindows.cs` artık Bootstrap, eski Table ve `TableAssetBuilder.ScenePath` sahnelerini tek listeden paketler. Üç dosyanın varlığını derlemeden önce doğrular ve kullanılan yolları build-report.json içine kaydeder. Release/Development/Acceptance aynı listeyi kullanır.

Doğrulama:

- Önceki davranışla regresyon testi başarısız oldu: `artifacts/test-results/20261021T194033599Z-cb01ffc5/results.xml` (1 geçti / 1 başarısız; GalaxyTable eksik).
- Düzeltme sonrası iki sahne sözleşmesi testi geçti: `artifacts/test-results/20261021T194113464Z-b4d1267a/results.xml`.
- Editor PlayMode menüden GalaxyTable açılışı, dağıtım ve menüye dönüş testi geçti: `artifacts/test-results/20261021T194129459Z-2d46dcc7/results.xml` (1/1).
- Yeni Release: `artifacts/builds/20261021T194205401Z-b8f0e64c/Galaxy101.exe`; Succeeded, 0 hata / 0 derleme uyarısı. Release tanılama temizliği geçti, fixture sayısı sıfır.
- Eski ve yeni gerçek player `globalgamemanagers` sahne yolları karşılaştırıldı: eskide iki, yenide GalaxyTable dahil üç yol var. Yeni paketin level0/level1/level2 dosyaları ve build raporu eşleşti. Kanıt: `artifacts/galaxy101/release-scene-fix/evidence.json`; hata logunun kopyası aynı klasörde.
- Release üzerinde fiziksel etkileşimli oyun testi yapılmadı; doğrulama Editor geçiş testi ve gerçek Release paket içeriği kapsamındadır. Masaüstüne müdahale edilmedi.

## Additional observations

- **medium — Assets/Galaxy101/Scripts/Editor/MenuAssetBuilder.cs:64, InstallMenu:** bu yeniden kurulum aracı paylaşılan sahne listesini yalnız Bootstrap/Table ile değiştiriyor. Araç çalıştırılırsa elle Build Profiles derlemesinde GalaxyTable listeden çıkabilir. Mevcut paylaşılan listede GalaxyTable açık; bu görev yalnız özel Windows derleme listesini düzeltti, yeniden kurulum aracını çalıştırmadı veya değiştirmedi.
