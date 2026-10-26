# Online dağıtım hedefi

Karar tarihi: 2026-10-21. Kullanıcı Railway'i online sunucu için hedef platform olarak seçti.

- Unity Windows/Android/iOS uygulaması oyuncunun cihazında çalışır ve ayrıca dağıtılır.
- Online oda ve oyun sunucusu Railway'de barındırılacak şekilde tasarlanacak; kurallar, sıra ve gizli eller merkezi sunucunun sorumluluğunda olacak.
- C# sunucusu için Docker ile dağıtım ve güvenli WebSocket bağlantısı aday yaklaşım. Bağlantı kütüphanesi, mobil uyumluluk ve yük testi sonrasında kesinleştirilecek.
- Yeniden bağlanma, sunucu yeniden başlatıldığında oyun kurtarma, kalıcı kayıtlar ve oda sahipliği ayrıca uygulanacak.
- 26 Ekim 2026: kullanıcı Railway'e gerçek deploy yapılmasını istedi. Mevcut .NET 10 Docker API + PostgreSQL kullanılacak; Unity istemcisi ayrı dağıtılacak. Hazırlık ve doğrulama: [RAILWAY.md](RAILWAY.md).
- V4 runtime masaları restart'ta geri kurulmaz; açık maçlar iptal edilip net bahisler iade edilir. İlk dağıtım tek replika ile yapılır.

Resmî başvurular: [ASP.NET Core dağıtımı](https://docs.railway.com/guides/aspnet-core), [WebSocket desteği](https://docs.railway.com/networking/public-networking/specs-and-limits).
