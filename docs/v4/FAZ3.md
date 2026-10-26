# v4 · Faz 3 · uçtan uca doğrulama

Bu makinede (Windows 11, 160 Hz monitör, Docker Desktop) yapıldı. Sunucu: `scripts/Start-LocalServer.ps1` (PostgreSQL 17 konteyneri `galaxy101-dev-pg`, `http://localhost:5173`, `/v1/info` → 0.4.0 / 101-v4). İstemci: `scripts/Build-Windows.ps1 -Configuration Development`.

## 1 · Docker imajı

| Adım | Sonuç |
|---|---|
| `docker build -f Server/Dockerfile -t galaxy101-server:v4 .` | tamam (önce `.dockerignore` paket kaynaklarını dışarıda bırakıyordu → düzeltildi, `451b53d`) |
| imaj + `postgres:17-alpine` aynı ağda, `DATABASE_URL` + `Auth__SigningKey` | `/health` 200 `{"status":"ok"}`, göçler açılışta uygulandı |
| `POST /v1/auth/guest` → `GET /v1/wallet` | 10.000 çip, 20 elmas (R-96) |

## 2 · Akışlar

| Akış | Nasıl doğrulandı | Sonuç |
|---|---|---|
| Misafir → menü | Unity PlayMode `AuthFlowTests.SplashThenGuestSignInLandsInTheMenuWithTheGuestBalance` (canlı yerel sunucu) | geçti |
| Kayıt → yanlış şifre → hatırlanan oturum (R-98 sayaç) | `AuthFlowTests.RegisterThenLoginWithWrongPasswordsAndRememberedResume` (canlı) | geçti |
| Lobi (canlı) | `LobbyTests.LiveServer` | kırmızı: "Attempted to get a texture … not allocated" günlük hatası (lobi hattında, bkz. ILERLEME) |
| Botlara karşı tam maç, dağıtım → turlar → el sonu → maç sonu | sunucu xUnit SRV-01 (gerçek sunucu, sanal saat) | geçti |
| Dağıtım töreni 21 686 ms (bot dağıtıcı), insan dağıtıcı zar beklemesi | SRV-02, SRV-02b | geçti |
| Gizli bilgi | SRV-03 | geçti |
| Masa aç, kodla/şifreyle katıl, bahis oturunca düşer, çıkışta iade | SRV-04, SRV-05, SRV-17 | geçti |
| Bekleme odası: bot ekle, başlat, 3-2-1 | SRV-06 | geçti |
| Maçta masadan kalk (bot devralır), eşlide kalkan | SRV-07, SRV-25 | geçti |
| Davet (30 sn) | SRV-08 (gerçek ikinci hesap) | geçti |
| Dereceli solo, kabul süresi dolar | SRV-09, SRV-10 (dört gerçek hesap) | geçti |
| Kopma ve dönme (snapshot, 2 zaman aşımından sonra sunucu oynar) | SRV-11, SRV-16 | geçti |
| Günlük ödül, gün değişimi İstanbul | SRV-12, SRV-22 | geçti |
| Sohbet seçenekleri, 120 karakter | SRV-13 | geçti |
| Giriş kilidi 5 dk, şifre sıfırlama | SRV-14, SRV-19 | geçti |
| Katlamalı (ortak seçeneği açık/kapalı), XP, haftalık puan, başlangıç bakiyesi, oyuna katıl | SRV-23, SRV-24, SRV-21, SRV-20, SRV-18 | geçti |
| Unity istemcisinin gerçek sunucuyla v4 maçı (adım mesajları, yeniden bağlanma kartı) | masa hattına canlı PlayMode testi istendi | bekliyor |
| Mağaza (gerçek ödeme), Google girişi, e-posta ile şifre sıfırlama | dış hizmet yok; sunucu doğrulama sınırına kadar xUnit (`StoreTests`, `AuthTests`) | sınırına kadar; EKSIKLER D grubu |

## 3 · Çözünürlük ve kare hızı

Durum yönlendiricisi `-g101Size=WxH -g101Fps=5` (`scripts/Capture-G101States.ps1 -Size … -FpsSeconds 5`): 01, 10, S09, S13, S16, G02, G13, T05, T15. Kareler `artifacts/v4/faz3/<WxH>/`.

| Çözünürlük | Ortalama FPS | p95 kare süresi | En kötü kare | Görüntü |
|---|---|---|---|---|
| 1920×1080 | 160,0 (9/9 durum) | 6,26 ms | 6,26 ms | keskin, tam ekran 16:9 |
| 2560×1440 | 159,6–160,0 | 6,26 ms | 12,5 ms (bir kez) | keskin |
| 2560×1080 (21:9) | 159,8–160,0 | 6,26 ms | 12,51 ms | 1280×720 tuval kırpılmadan ortada, yanlarda bant (prototip `k = min(w/1280, h/720)` ile aynı) |

VSync açık (`vSyncCount 1`), monitör 160 Hz; 3D masa durumlarında da (G02 dağıtım, G13, T05, T15) kare hızı monitör yenilemesinde kaldı. 60 FPS hedefi bu makinede karşılandı; daha zayıf GPU'da ölçülmedi.
