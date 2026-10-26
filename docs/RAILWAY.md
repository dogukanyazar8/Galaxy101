# Galaxy 101 · Railway

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
RAILWAY_DOCKERFILE_PATH=Server/Dockerfile
DATABASE_URL=${{Postgres.DATABASE_URL}}
Auth__SigningKey=<Railway'de saklanan rastgele base64 anahtar>
Game__PublicBaseUrl=https://<üretilen-domain>
```

`Postgres`, DB servisinin gerçek adıdır. `DATABASE_URL`, DB'den API servisine ayrıca referanslanır. İmzalama anahtarı en az32 rastgele bayt; her deploy'da değiştirilmez. Şema migration'ları API başlangıcında otomatik çalışır. Veritabanı için public TCP proxy gerekmez.

## Oyuncular nasıl bağlanır?

Aynı güncel Windows build klasörü iki bilgisayara da verilir; yalnız `.exe` gönderilmez. Oyunun yanında bir `.cmd` dosyası:

```bat
@echo off
start "Galaxy 101" "%~dp0Galaxy101.exe" -galaxyServer https://<üretilen-domain>
```

İstemci HTTPS adresinden WSS adresini otomatik çıkarır. Misafir girişi, lobi ve masa testi bu kurulumla yapılabilir. Google/Apple girişi, SMTP şifre sıfırlama ve gerçek mağaza ödemeleri kendi hizmet anahtarları olmadan etkin değildir.

## Kontrol ve güncelleme

1. `https://<domain>/health`: HTTP200, `status=ok`.
2. `https://<domain>/v1/info`: `version=0.4.0`, `ruleVersion=101-v4`.
3. İki ayrı hesapla aynı özel masaya gir; çek/at, sohbet, kopma/dönme ve çıkışı dene.
4. Sunucuya etki eden `main` push'ları GitHub autodeploy ile yayımlanır. Başarısız deploy'da build/runtime loglarına bakılır.

V4 masaları bellektedir: yeniden deploy/restart devam eden masaları geri kurmaz; açık maçlar iptal edilip net bahisler iade edilir. İlk arkadaş testi için tek replika korunur; aktif maç sırasında deploy yapılmaz.

Canlı domain ve doğrulama sonuçları ilk başarılı deploy sonrasında bu dosyaya kaydedilecek.
