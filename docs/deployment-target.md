# Online dağıtım hedefi

Karar tarihi: 2026-10-21. Kullanıcı Railway'i online sunucu için hedef platform olarak seçti.

- Unity Windows/Android/iOS uygulaması oyuncunun cihazında çalışır ve ayrıca dağıtılır.
- Online oda ve oyun sunucusu Railway'de barındırılacak şekilde tasarlanacak; kurallar, sıra ve gizli eller merkezi sunucunun sorumluluğunda olacak.
- C# sunucusu için Docker ile dağıtım ve güvenli WebSocket bağlantısı aday yaklaşım. Bağlantı kütüphanesi, mobil uyumluluk ve yük testi sonrasında kesinleştirilecek.
- Yeniden bağlanma, sunucu yeniden başlatıldığında oyun kurtarma, kalıcı kayıtlar ve oda sahipliği ayrıca uygulanacak.
- Bu karar bir dağıtım yapıldığı anlamına gelmez. Mevcut Windows sürümü yerel bir insan ve üç botla çalışır; Railway hesabı/kaynağı oluşturulmadı ve ücretli hizmet başlatılmadı.

Resmî başvurular: [ASP.NET Core dağıtımı](https://docs.railway.com/guides/aspnet-core), [WebSocket desteği](https://docs.railway.com/networking/public-networking/specs-and-limits).
