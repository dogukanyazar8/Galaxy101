# Galaxy 101 · Railway

> 26 Ekim 2026: Railway çalışması kullanıcı kararıyla askıda. Aşağıdaki ayarlar önceki dağıtımın tarihsel kaydıdır; bu düzeltme çalışmasında push/deploy veya uzak veritabanı değişikliği yapılmadı. Güncel yerel doğrulama: [LOCAL_RELIABILITY.md](LOCAL_RELIABILITY.md).

Tek API servisi + tek PostgreSQL yeterli. Unity Windows oyunu oyuncunun bilgisayarında çalışır; Railway API ve WebSocket sunucusunu barındırır.

## Servis ayarları

| Ayar | Değer |
|---|---|
| GitHub | `dogukanyazar8/Galaxy101`, branch `main` |
| Root Directory | Depo kökü `/`; `Server` seçilmez |
| Builder / Dockerfile | Docker / `Server/Dockerfile` |
| Build / Start Command | Boş; Dockerfile kullanılır |
| Healthcheck | `/health`,60s |
| Port / domain target | `8080` |
| Replika / serverless | `1` / kapalı |

Railway'nin yeni servislerinde eski `railway.json` / `railway.toml` okunmaz. Bu ayarlar doğrudan Railway servisine uygulanır; depodaki `railway.json` eski servisler için uyumluluk kaydıdır (legacy okuma desteği2026-12-01'de sona erer). Kaynak: [Config as Code / IaC](https://docs.railway.com/infrastructure-as-code).

## API servisinin değişkenleri

```dotenv
PORT=8080
ASPNETCORE_ENVIRONMENT=Production
RAILWAY_DOCKERFILE_PATH=Server/Dockerfile
DATABASE_URL=${{Postgres.DATABASE_URL}}
Auth__SigningKey=<Railway'de saklanan rastgele base64 anahtar>
Game__PublicBaseUrl=https://galaxy101-api-production.up.railway.app
```

`Postgres`, DB servisinin gerçek adıdır. `DATABASE_URL`, DB'den API servisine ayrıca referanslanır. İmzalama anahtarı en az32 rastgele bayt; her deploy'da değiştirilmez. Şema migration'ları API başlangıcında otomatik çalışır. Veritabanı için public TCP proxy gerekmez.

## Oyuncular nasıl bağlanır?

Aynı güncel Windows build klasörü iki bilgisayara da verilir; yalnız `.exe` gönderilmez. Oyunun yanında bir `.cmd` dosyası:

```bat
@echo off
start "Galaxy 101" "%~dp0Galaxy101.exe" -galaxyServer https://galaxy101-api-production.up.railway.app
```

İstemci HTTPS adresinden WSS adresini otomatik çıkarır. Misafir girişi, lobi ve masa testi bu kurulumla yapılabilir. Google/Apple girişi, SMTP şifre sıfırlama ve gerçek mağaza ödemeleri kendi hizmet anahtarları olmadan etkin değildir.

## Kontrol ve güncelleme

1. [Canlı sağlık kontrolü](https://galaxy101-api-production.up.railway.app/health): HTTP200, `status=ok`.
2. [Canlı sürüm](https://galaxy101-api-production.up.railway.app/v1/info): `version=0.4.0`, `ruleVersion=101-v4`.
3. İki ayrı hesapla aynı özel masaya gir; çek/at, sohbet, kopma/dönme ve çıkışı dene.
4. Sunucuya etki eden `main` push'ları GitHub autodeploy ile yayımlanır. Başarısız deploy'da build/runtime loglarına bakılır.

V4 masaları bellektedir: yeniden deploy/restart devam eden masaları geri kurmaz; açık maçlar iptal edilip net bahisler iade edilir. İlk arkadaş testi için tek replika korunur; aktif maç sırasında deploy yapılmaz.

Railway projesi: [Galaxy101](https://railway.com/project/feb4f602-69f1-4f69-9117-1a6f06c80a52), ortam `production`, servisler `galaxy101-api` ve `Postgres` (kalıcı volume, PostgreSQL18), bölge EU West.

## Doğrulama · 26 Ekim 2026

- GitHub kaynak commit'i `6877433`, ilk başarılı API deployment'ı `9457d23f-2022-45da-928f-259a9ea9aa3e`. Sonraki talimat commit'leri sunucu kodunu değiştirmez.
- Yerel production Docker image build ve ayrı PostgreSQL17 ile başlangıç smoke testi geçti.
- Canlı PostgreSQL18 ile otomatik migration ve production başlangıcı geçti; API ve DB birer çalışan replika.
- Canlı HTTPS: `/health`200/ok; `/v1/info`0.4.0/101-v4; misafir girişi; cüzdan10.000 çip/20 gem.
- Gerçek HTTPS/WSS üzerindeki iki `TableLiveServerProtocolTests` testi geçti: dağıtım, botlara karşı çek/at, geçersiz hamle reddi, durum eşitleme, masadan çıkış ve kopan soketle masaya dönüş. Sonuç:2 başarılı,0 başarısız,0 atlanan;36 saniye, sıralı.
- Unity görüntülü istemcisi ve iki insanın aynı masada uçtan uca arkadaş testi bu deploy doğrulamasında çalıştırılmadı. Windows build ayrıca alınır; yukarıdaki `.cmd` adresiyle başlatılır. Yerel commit edilmemiş v4 UI çalışmaları bu sunucu yayınına eklenmedi.
- Ham kanıtlar yerel `artifacts/railway/` altında (`docker-build.log`, `docker-smoke.json`, `live-http.json`, `railway-live.trx`, `live-test.log`). Bu klasör Git'e gönderilmez.

Railway CLI5.26.0 ile `environment edit --service-config` sessizce `No changes to apply` döndü. Ayarlar stdin üzerinden JSON patch ile uygulandı, deployment manifestinden doğrulandı. Aynı sorunla karşılaşırsan Dashboard ayarlarını kullan veya [CLI hata kaydındaki JSON yöntemini](https://github.com/railwayapp/cli/issues/1119) uygula; çıkış kodu0 tek başına ayarın uygulandığını göstermez.
