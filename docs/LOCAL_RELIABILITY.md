# Yerel güvenilirlik doğrulaması · 26 Ekim 2026

Railway kullanıcı kararıyla askıda. Bu çalışmada push, deploy veya uzak veritabanı değişikliği yok. Çalışma alanı `C:/GD/Okey 101`; eski Railway worktree'si değiştirilmedi.

## Değişiklikler

- Parola sıfırlama kalıcı `accounts.session_version` değerini artırır; eski JWT, refresh token, WebSocket ticket ve socket yeni oturumu kullanamaz. Token üretimi/yenileme/sıfırlama hesap kilidiyle aynı sürümü izler. Yeni oturum eski reset bildirimiyle kapatılmaz.
- Ayrılan oyuncunun katılım kimliği ve işlem anahtarı sabittir. Commit başarılı olup yanıt kaybolduğunda tekrar deneme istatistik/LP/XP'yi ikinci kez yazmaz. Silinmiş hesap için anonim ayrılma kaydı tutulur.
- Maç sonucu ve ilk hesaplanan ödül makbuzu aynı transaction'da kalır; bonus XP gerçek istatistiğe yazılır. El XP'si defter anahtarıyla tekilleşir. Bekleyen iade/ayrılma/el XP'si sonuçlanmadan maç tamamlanmaz; okuma hatası koltuğu yarım değiştirmez.
- Engellenen göndericinin sohbeti izleyiciye de gitmez. Engel verisi okunamıyorsa gönderim başarılı görünmez; mevcut yeniden deneme yolu toparlar. Tamamlanmış çevrimdışı oyuncular bellekten temizlenir.
- İstemci HTTP yanıtları hesap nesline bağlıdır; eski hesap yeni profili/cüzdanı ezemez. Profil kaydetmeleri mevcut oturumdaki ortak kapıdan sırayla geçer. Socket kurulumu tek uçuş; `Dispose` terminal; `replaced` açıkça yeniden giriş ister.
- Yükleme iptali sunucudan gerçekten ayrılmayı bekler. ACK reddi/kopma durumunda masa korunur. Kaçırılan `matchStart`, mevcut snapshot'tan tek oturum açar; oyuncu/izleyici gizli bilgi projeksiyonu korunur. Dereceli yeniden bağlantı `hello` ile insan kontrolünü geri alır.
- Zar duyurusu geldiğinde animasyon bitmeden ikinci atış sunulmaz; HUD ve komut aynı hazır-olma kontrolünü kullanır. İlk ACK beklenirken çift tık ikinci istek üretmez; hata veya başarı sonrasında kilit temizlenir.
- Sıkışık ıstakadaki 9/10 çift açma ve çift bitiş kullanılabilir çiftleri korur. Eski protokolde ayrılıp başka koltuğa dönen hesap aktif koltuğunu kullanır; eski bahis geçmişi korunur.
- Önceden yerelde kalan HUD uyarısı/yerleşimi, senaryo yenilemesi, görünüm karşılaştırma maskesi, build kural sürümü ve `-NoGraphics` test seçeneği gözden geçirilip dahil edildi. Grafik gerektiren canlı URP sahnesi Null grafik aygıtında çalıştırılmaz; grafik açık tekil test kullanılır.

## Şema ve yerelde çalıştırma

`20261026140433_LocalReliability` yalnız eklemeli migration: `accounts.session_version` (varsayılan `0`), `matches.settlement_json`, `match_participants.operation_key` ve nullable benzersiz indeks. API başlangıçta uygular. Mevcut kullanıcı verileri korunur. Oturum sürümü taşımayan eski access token reddedilir; geçerli refresh veya yeniden giriş güncel token üretir.

```powershell
pwsh -NoProfile -File scripts/Start-LocalServer.ps1
# İstemci varsayılanı http://localhost:5173
# Farklı adres için: Galaxy101.exe -galaxyServer http://localhost:5173
# Güncel Windows istemcisi üretmek için (ayrı terminalde, Unity testleri bitince):
pwsh -NoProfile -File scripts/Build-Windows.ps1 -Configuration Release
```

Yerel geliştirme verisini korumak için `-Fresh` kullanma; bu seçenek veritabanını siler. Bu çalışmanın entegrasyon testleri kendi geçici PostgreSQL veritabanlarını kullandı.

## Doğrulama

Test/build süreçleri sıralı; tam ekran yakalama çalıştırılmadı.

| Kontrol | Gözlenen sonuç |
|---|---|
| Sunucu xUnit; eski sınıflar, yeni oturum/kalıcılık/engel/ayrılma/seat ve migration | 189 ayrı test geçti; dokuz küçük eski grup ve odaklı yeni gruplar |
| Saf v4 engine / vektör / Node parity | 14 / 110 / 72 geçti |
| Saf masa modeli, senaryolar, ıstaka/çift düzeni | 46 geçti |
| Native Unity istemci yaşam döngüsü | 17 geçti |
| Native Unity masa/HUD/çıkış + strict lobi | 16 geçti; grafik açık |
| Gerçek yerel API/PG + authored Unity sahnesi | 3/3 geçti: bekleme odası/başlat/ayrıl, eşli/iki katlama seçeneği, doğal tek ellik bot maçı ve lobi sonucu (son koşu: 16 insan eylemi, 5 çekiş, 5 atış) |
| Yakalama korumaları / karşılaştırma maskesi | 5 / 2 geçti; pencere açılmaz |
| EF model/migration uyumu | Bekleyen model değişikliği yok; eski şemadan yükseltme ve veri korunması geçti |
| Değişen PowerShell dosyaları | Parser hatası yok |
| API Release publish | Yerel gerçek-sahne testleri için başarılı |

## Sınırlar

- V4 maç aktörü hâlâ bellektedir. Süreç kapanınca açık maç stake'leri mevcut başlangıç abort/iade politikasıyla geri verilir. Commit edilmemiş el/ayrılma payload'ı için kalıcı outbox veya tam maç devamı yoktur; bu çalışma böyle bir mimari eklemedi.
- Görsel durum tablosu: 63 eşleşti, 7 bilinçli fark, 74 bekliyor, 7 kaldırıldı. Bu çalışma bekleyen görselleri kabul edilmiş saymaz. Eski 160 Hz/FPS ölçümleri güncel performans ölçümü değildir.
- UI Toolkit'in önceki texture hatası ilk başarısız canlı testin temizliğinde görüldü. JSON-null test hatası düzeltildikten sonra grafik açık gerçek akış temiz geçti. Bütün olası filtre/sahne kombinasyonlarında motor hatasının giderildiği iddia edilmez; hata susturma kaldırıldı.
- Bu düzeltme turunda Windows Release paketi yeniden üretilmedi; mevcut eski build güncel kaynak düzeltmelerini içermez. Yukarıdaki build komutunu kullan.
- E-posta/Google/Apple/gerçek ödeme sağlayıcıları, iki fiziksel bilgisayar ve internet/Railway üzerinde yeni doğrulama yapılmadı. Grafik aygıtı kapalı URP denemesi render texture hatasıyla başarısız oldu; gerçek sahne grafik açık yeniden doğrulandı.

## Yerel kayıt

Sunucu `72c3c50`, çift düzeni `661a34e`, istemci/HUD `5424a70`. Son araç/doküman commit'i bu raporu ve önceden kalan yerel v4 kanıtlarını içerir. Ana checkout'taki gözden geçirilen kaynaklar commitlendi; loglar, anahtarlar, test veritabanları ve üretilmiş build dosyaları commit kapsamına alınmadı. `origin/main` yenilenmedi; push yapılmadı.
