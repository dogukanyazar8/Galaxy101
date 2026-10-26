# Hat B · Sunucu · rapor

Dal `v4/server` (worktree `C:/GD/Okey101-v4/server`, taban `753287e`). Kararlar K-04 (protokol) ve K-05 (sunucu akışları). Hat A'nın motoru (`v4/rules`, `Okey101.Core.V4`) iki kez birleştirildi.

## Ne yapıldı

1. **Sunucu v4 motorunda, adım protokolüyle.** `Server/src/Galaxy101.Server/V4/` — `Hub` (tek döngülü aktör: lobi masaları, bekleme odası, davetler, botlara karşı, dereceli kuyruk, sohbet, hediye, yeniden bağlanma) ve `Store` (bahis/iade/ödeme defteri, XP, LP, haftalık puan, görevler, katılımcı kayıtları, yeniden başlatmada iade). Hub mantıksal saatle çalışır: zamanlayıcı işi kendi vadesini "şimdi" görür, böylece olay zamanları paketin takvimine milisaniyesi milisaniyesine uyar ve `FakeTimeProvider` ile test edilir.
2. **Maç akışı taşıma bağımsız paket (orkestratör isteği).** `Packages/com.galaxy101.table/` (Unity paketi, C# 9, `.meta`'lı; sunucu `Server/src/Galaxy101.Table/Galaxy101.Table.csproj` ile derler): `TableRuntime` mockServer.js'in masa kısmıdır — dağıtım takvimi (D-24, 21 686 ms), insan dağıtıcı zar beklemesi (D-23), tur sayacı ve R-70, bot turları (D-20, bots.js), kopuk koltuğu sunucunun oynaması (R-66, D-35, D-36), el sonu bekleme ve `gReady` (D-25), kalkma (R-60), devralma (R-65), her koltuğa gizli bilgi süzgeçli olaylar. `TableSettlement` maç sonu aritmetiği (R-91, D-12, D-40, D-41), `TableTimings`/`TableEconomy` paketin sabitleri. İstemci aynı kodu çevrimdışı koşar (ISTEMCI_NOTU §6).
3. **Protokol.** Sözleşmeye 53 C2S + 45 S2C adı ve 94 hata kodu (`V4Messages`, `V4Errors`) ile yük sınıfları (`V4Payloads.cs`). Yönlendiricide v4 mesajları için ack `{id, ok, code, detail, at, …dönüş}`; `ping` ack'i `serverTime`. Hesap/ekonomi/sosyal mesajları mevcut HTTP uçlarında; hata gövdelerine paket kodu (`error`) ve `detail` eklendi. Eşleme: `docs/v4/PROTOKOL_ESLEME.md`.
4. **Ekonomi ve kararlar.** Bahis oturunca (R-90), bekleme odasından çıkana/çıkarılana/kopana iade (D-11), havuz = ödenen bahisler, kesinti 0 (R-91, D-34; devralmada büyür), eşitlikte eşit bölüşüm ve eşlide masadaki ortaklara (D-12), hediye "herkese" fiyat × alıcı (D-26), mağaza bonusu (D-27), haftalık puan yalnız koltuktaki insana, botlara karşı/özel hariç (R-95, D-37), XP el başına anında (R-93, D-42), dereceli 5 el/30 sn/bahissiz (R-94), duo 0+2 (D-29), koruma (D-30). Paylaşılan sıra en yakın tama ve eşli dört konum (D-40, D-41) eski yolda da uygulandı; eski yolun dağıtım beklemesi 21 686 ms, sohbeti 120 karakter.
5. **Testler.** `Server/tests/Galaxy101.Server.Tests/V4/`: 26 paket akışı (SRV-SC1, SRV-01…25) + SRV-02b (insan dağıtıcı) gerçek sunucuya karşı, sanal saatle; 4 `TableRuntime` birim testi (sunucusuz, sahte host). Test altyapısı paketteki `env()/client()/until()` karşılığı: 500 ms adım, hub boşalana dek çalışır, istemci soketine kuyruklanan her çerçeveyi alana dek bekler (`Connection.Queued`).

## Commitler

| Commit | İçerik |
|---|---|
| `7b85e48` | Protokol hub'ı, taşıma bağımsız masa çalışma zamanı, sözleşmeler, yönlendirici ack'i |
| `9a93d5d` | `v4/rules` birleştirmesi (motor) |
| `d169302` | Test altyapısı + ilk 17 akış |
| `849b231` | 26 akışın tamamı; HTTP hata gövdesinde paket kodu; D-27 |
| `d3669da` | Eski yol: D-40/D-41 paylaşım, D-24 süre, D-32 sohbet |
| `7d1e2a5` | Silinen/yasaklanan hesap v4 masasından çıkar; dereceli hazırlıkta kopma koltuğu korur; çalışma zamanı testleri |
| `db24e5a` | Kopma testleri sunucunun soketi bırakmasını bekler |
| `b5c9541` | `v4/rules` birleştirmesi (Hat A belgeleri) |
| (son) | Bu belgeler: PROTOKOL_ESLEME, matris/hat-b, RAPOR, EKSIKLER, ISTEMCI_NOTU |

## Komutlar ve çıktılar

```
dotnet build Server/tests/Galaxy101.Server.Tests/Galaxy101.Server.Tests.csproj -p:OutDir=<scratch>/b-out/
dotnet test <scratch>/b-out/Galaxy101.Server.Tests.dll
  Başarılı!  - Başarısız: 0, Başarılı: 130, Atlanan: 0, Toplam: 130, Süre: 28 s
dotnet test <scratch>/b-out/Galaxy101.Server.Tests.dll --filter "FullyQualifiedName~V4"
  Başarılı!  - Başarısız: 0, Başarılı: 31, Atlanan: 0, Toplam: 31
node tools/v4/run_package_tests.js
  vectors 106/106 · server flows 26/26
```

130 = 99 mevcut test (taban `753287e`: 99/99) + 27 v4 akış testi (26 paket akışı + SRV-02b) + 4 çalışma zamanı testi. Tam paket art arda üç kez yeşil koştu.

Değişen mevcut testler (gerekçesi commit mesajlarında):
- `StoreTests`: `gems_1` 550 → 605 elmas (D-27 bonus eklenir).
- `SettlementTests`: berabere haftalık puan 22 → 23 (D-40 en yakın tama); eşli için 23/3 haftalık ve 350/40 XP eklendi (D-41).
- `MatchTests`, `RankedTests`: XP bonusu ve LP beklentisi beraberlikte paylaşılan değeri hesaplar (D-40; önce rastgele tohumla beraberlik çıkarsa düşebilirdi).

## Kararlar

- **Taşıma korundu (K-04).** Her mesaj eşlendi; protocol.json taşımasına geçmeyi gerektiren bir eşleme çıkmadı. Giriş HTTP'de kaldığı için soket her zaman girişli açılır; `hello` bilet sonrası ilk mesajdır ve `token`'ı yok sayar.
- **Yeni hub, eski yol yerinde.** v4 mesajları (`tableCreate`, `g*` …) yeni `Hub`'a gider; eski `table.*` / `match.*` / `queue.*` / `room.*` eski istemci ve eski kayıtlar için kalır (pakette yok, silinmedi). Yeni maçlar v4 motorunda; eski kayıtlar okunur.
- **Orkestratörün "taşıma bağımsız masa çalışma zamanı" isteği** maç akışını yazmadan önce geldi; akış doğrudan `Packages/com.galaxy101.table` olarak kuruldu ve hub onu sarar (yeniden yazım gerekmedi). Paket `Galaxy101.Contracts`'a da başvurur (olay yükleri sözleşme sınıfları).
- **Mantıksal saat.** Hub zamanlayıcıları kendi vadeleriyle koşar; bu, sanal saatte de gerçek saatte de olay zamanlarını paketle aynı yapar (SRV-02 tablosu birebir).
- **K-12 / EK-211 (26 Ekim 2026 kullanıcı kararı).** Bekleme odası sahibi açıkça ayrıldığında veya bağlantısı koptuğunda sahiplik koltuk sırasındaki ilk kalan insana geçer. Botlar ve izleyiciler sahip olamaz; insan kalmazsa masa kapanır. Ayrılanın bahsi bir kez iade edilir; diğer bahisler kalır, geri sayım iptal edilir, oda/lobi yeni sahibi alır. İstemcinin ayrılma tostu "Bekleme odasından ayrıldın"; masa kapandı iddiası yok. Eski protokol değişmedi.
- **Demo kısayolları gerçek sunucuya girmedi** (DEMO-01…18): SRV-04 gerçek PIN'li masa, SRV-08 gerçek ikinci hesap, SRV-09/10 gerçek dört hesaplı kuyruk; ilk el sabitleme (DEMO-17) yalnız test kancası `Hub.NextRig`.
- **Yeniden başlatma**: v4 masaları geri kurulmaz; açılışta tuttukları bahis iade edilir (EK-210). Eski motorun maç geri kurulumu v4 kayıtlarını atlar.

## Açık konular

- EK-205 (ad benzersizliği) kullanıcı onayı bekler. EK-211, K-12 ile kapandı.
- EK-201 (`gAdd.side` L/R ↔ pre/post) ve EK-202 (SRV-25 metnindeki 4000) paket sahibine.
- `skinSet`, `friendRemoved`, `E_REQUEST_PENDING`, Galaksi çerçevesi seviye kuralı sunucuda yok (EK-206…209).
- Hub tek döngü; çok masalı yükte masa başına aktöre bölünebilir (EK-221).
- İstemci (Faz 2) henüz eski protokolle konuşuyor; geçiş ISTEMCI_NOTU'nda.

Ayrıntı: `docs/v4/PROTOKOL_ESLEME.md`, `docs/v4/matris/hat-b.md`, `docs/v4/hat-b/EKSIKLER.md`, `docs/v4/hat-b/ISTEMCI_NOTU.md`.

## K-12 doğrulaması · 26 Ekim 2026

`V4/OwnerTransferTests` altı entegrasyon vakası: açık ayrılma/kopma için ilk insanın seçimi (daha önce katılan yüksek koltuğa ve bota karşı), oda/lobi yayını, ayrılanın tek iadesi ve kalan bahisler, yeni sahibin yetkileri; son insanın bot/izleyici bırakıp ayrılması; geri sayım iptali, eski zamanlayıcının maçı başlatmaması ve yeni sahibin tekrar başlatması.

Derleme, etkin sunucunun çıktısını kullanmadan ayrı geçici `OutDir` altında yapıldı; test DLL'i doğrudan çalıştırıldı. Unity ve yerel sunucu başlatılmadı.

```
dotnet build Server/tests/Galaxy101.Server.Tests/Galaxy101.Server.Tests.csproj -p:OutDir=<temp>/g101-owner-transfer-tests-1792993413331/ --no-restore -v:minimal
  0 Uyarı, 0 Hata
dotnet test <out>/Galaxy101.Server.Tests.dll --filter FullyQualifiedName~OwnerTransferTests
  RED: Başarısız 4, Başarılı 2, Atlanan 0, Toplam 6 (masa silindiği için Assert.NotNull başarısız)
  GREEN: Başarısız 0, Başarılı 6, Atlanan 0, Toplam 6
dotnet test <out>/Galaxy101.Server.Tests.dll
  Başarısız 0, Başarılı 136, Atlanan 0, Toplam 136
python tools/v4/unity_compile.py --repo C:/GD/Okey101-v4/server --csproj-from "C:/GD/Okey 101" --scratch <temp>/g101-owner-client-compile --project Galaxy101.Runtime
  Galaxy101.Runtime: OK (0 errors)
```

İstemci tostu yalnız C# derlemesiyle doğrulandı; Unity PlayMode/görsel doğrulama bu değişiklikte yapılmadı.
