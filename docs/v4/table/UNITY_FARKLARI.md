# Faz 2 · Masa · Unity farkları

Prototiple bilinçli ya da Unity'den kaynaklı farklar (`UF-5NN`).

| Kimlik | Durum(lar) | Fark | Neden |
|---|---|---|---|
| UF-501 | G02–G12 | Dağıtan, zarlar ve gösterge seed 42 ile motorun dağıtımıdır; tören adımları sunucu takvimiyle (D-24, `TableTimings`) gelir. Referans kareleri kendi aralarında tutarsız (G02/G04/G06–G09 Zeynep, G03/G05/G10/G11 Burak dağıtıyor). | Prototipte dağıtan `Math.random`; v4'te dağıtım sunucunundur (K-09). |
| UF-502 | G25–G29 | "Elden bit" çıkmaz; kareler "Aç · 89 / 101" ile durağandır (referans da böyle). `masa bomb` adımı motorun reddiyle boşa düşer. | R-53: elden bitişte eşik aranır (HAND_BOMB 89). |
| UF-503 | test akışları (G13–G34, T04–T49) | Deste sayısı kalan gerçek taşlardır: play (ve gift/chat/exit/net/settings) 15, open 14 — referans 20; bomb, end, isle, penIslek, penOpen 20. Rakip taş sayıları referanstaki gibi (21 / 9 / 21; isle 4 / 3 / 5). | Her taş gerçek (106); prototipin sahte sayıları toplamda tutmuyor (EK-504). |
| UF-504 | T23, T24, G34, T42, T43 | Ayşe'nin (Test masasında 2. koltuk "Burak T.") turu hemen başlar; bitiş ≈ 1,5 sn (işle 430 + 430, at 650 ms, D-20) — prototipte 0,5 sn benim turum + 1,9 sn. | Motor sıra atlayamaz; bot zamanlaması paketinki. |
| UF-505 | G21, G22, T35 | Eksik açılış motorda hemen yazılır (openShort + penalty); masa perleri 1,5 sn tahtada "Açtı · 66" gösterip cezayı tost olarak sonra verir. | Prototipin badOpen görüntüsü korunur, kural motorun. |
| UF-506 | G01, T50, T51 | Yükleme ekranı menünün üstünde çekilir, altında masa yok (prototipte masa yükleme katmanının altında da çizilir). Yüzde, prototipin tickLoad'u gibi 50 ms'de %0,6–2,2 (GameClock akışı). | Unity sahne geçişi menüyü kapatır; `noWait` durumunda masa sahnesi yüklenmez. |
| UF-507 | son el | Son elin sonuç kartı 15 sn sayar ya da "Sonraki el" ile kapanır, masa kapanır, lobide tost ("Maçı kazandın! +N çip" / "Maç bitti · N. sıradasın" / "İzlediğin maç bitti"); dereceli → Dereceli ekranı. | REMOVED.md T45/T46; sunucu son elde `handWait` göndermez. |
| UF-508 | T47, T39 | Sürükleme benzetimi taşı hedefe doğru `k` kadar taşır ve hedefleri yakar (TileView.Follow); fare imleci çizilmez. | g101-state.js `scDrag` → table3d debugDrag. |
| UF-509 | tümü | Çevrimdışı maç GameClock ms'siyle akar; yönlendirici dondurunca (100000 ms, EK-005) maç da durur. Tören beklemeleri ve el sonu `Time.time` ile (donunca `timeScale = 0`). | D-60/D-61. |
| UF-510 | T06, T34, T36, T39 | Bot çayı yalnız yönlendirici adımında (gerçek oyunda sunucudan gelmez, EK-506). | DEMO_LIST. |
| UF-511 | T26 | "Bağlantı koptu" kartı çevrimdışı masada `ShowConnectionLost` ile açılır (soket yok); çevrimiçi masada gerçek denemeleri (5 × 3 sn) izler. | Prototipin dropNet'i de benzetim. |
