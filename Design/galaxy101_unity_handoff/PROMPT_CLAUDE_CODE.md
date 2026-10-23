# Claude Code için görev metni — Galaxy 101 (tam oyun, v3)

Bu metni olduğu gibi Claude Code'a verin. Paket Unity projesinin kökünde `Design/galaxy101_unity_handoff/` altında olmalıdır. Blender kuruluysa yolu `BLENDER_PATH` ortam değişkeniyle verin (ya da Blender MCP sunucusunu bağlayın).

---

## GÖREV
Sen kıdemli bir Unity oyun istemcisi geliştiricisisin. **Galaxy 101** (101 Okey, 4 oyunculu) oyununun **tüm ekranlarını ve tüm oyun mantığını** Unity'de üretim kalitesinde kuracaksın: açılış/yükleme, giriş (misafir, e-posta, kayıt, Google), ana menü ve tüm pencereleri, ayarlar, lobi (Klasik), masa açma/katılma/şifre/kod, bekleme odası ve arkadaş daveti, 3D oyun masası (dağıtım töreni, oyun, el açma, işleme, okey alma, geri toplama, cezalar, elden bitiş, el/maç sonu), ikram/hediye, sohbet, masa ayarları ve lobiye dönüş.

## ÖNCE OKU (sırayla, tamamen)
1. `README.md`
2. `spec/00_GUNCEL_DURUM_V3.md` — **belge önceliği**; çelişkide en yeni belge geçerli.
3. `spec/18`–`spec/25` (v3: akış, ayarlar, lobi, dağıtım, kural motoru, masa düzeni, Blender, testler)
4. `spec/08`–`spec/17` (3D masa, etkileşim, animasyon, hediye/sohbet/ayarlar, ek durumlar)
5. `spec/01`–`spec/07` (tasarım sistemi, ana menü, pencereler)
6. `spec/data/*.json|csv`
7. `screenshots/v3/INDEX.md` + tüm `screenshots/v3/*`, ardından `screenshots/INDEX.md`, `screenshots/table/INDEX.md`
8. `reference/game/` — **birincil kaynak**. Spec ile HTML çelişirse `reference/game` içindeki değer geçerlidir. Eski `reference/*.dc.html` ve `reference/table/` yalnızca tarihçedir.

Prototipi çalıştırmak: `cd reference/game && npx serve .` (ya da `python -m http.server`), tarayıcıda `Galaxy 101 - Oyun.dc.html`. Masa tek başına: `G101-Masa.dc.html` (üstteki akış düğmeleri ve Tweaks → Test senaryosu).

## TEKNİK KARARLAR
- Unity 6 LTS, URP. UI: **UI Toolkit** (UXML/USS/C#), referans 1280×720, Scale With Screen Size, match 0.5.
- Fontlar Montserrat (logo/başlık/düğme) + Baloo 2 (gövde), Türkçe glifli.
- 5 gezegen teması USS değişkenleriyle (`themes.json`). TR/EN: Unity Localization (`strings_tr_en.csv`, `table_strings_*.csv`).
- Gezegen HLSL shader, yıldız alanı partikül (`spec/04`).
- 3D masa: URP, ACES tonemapping, exposure 1.05, PCF gölge; kamera görüşleri seat/over/top/**deal** (`spec/09`, `spec/21`).
- Mimari: `GameFlow` durum makinesi (Splash → Auth → Menu → Lobby → Loading → Table → Lobby); **`Galaxy101.Rules` saf C# kural motoru** (`spec/22`) + NUnit testleri; `TableView` (3D) yalnızca durumdan (state) pozları türetir (prototipteki `syncTable → sync(list)` deseni: her taşın kimliği + `at` konumu → hedef poz → tween).
- Servisler arayüz + mock: Auth, Economy, Lobby, Friends/Invite, Leaderboard, Rewards, Profile, Settings, Match (botlar).
- Kalıcılık: PlayerPrefs/JSON — `spec/18` anahtarları.
- Görüntü: `FullScreenMode`, monitöre göre çözünürlük listesi, `targetFrameRate` (`spec/19`).

## BLENDER
`spec/24`'e göre: önce `reference/game/Galaxy 101 - 3D Varlik Disa Aktarma.html`'den GLB'leri al, sonra Blender'ı `"$BLENDER_PATH" -b -P Tools/Blender/<script>.py` ile çalıştırarak taş, zar, ıstaka (6), masa (4 tema), koltuk ve 10 ikramı üretim kalitesine getir; glb/fbx olarak `Assets/Art/Models/` altına çıkar. Betikleri `Tools/Blender/` altında sakla ki tekrar üretilebilsin. Blender yoksa prototipteki prosedürel geometriyi C# ile üret (ölçüler aynı).

## AŞAMALAR (her aşama sonunda derle, çalıştır, ekran görüntüsüyle karşılaştır)
1. İskelet, token'lar, fontlar, temalar, yerelleştirme.
2. Açılış + giriş ekranları + hesap/bakiye/kalıcılık (`spec/18`).
3. Ana menü 1a ve tüm pencereler (`spec/02`, `03`, `19`).
4. Lobi, masa aç, şifre/kod, bekleme odası, davet (`spec/20`).
5. Kural motoru + birim testleri (`spec/22`; özellikle: havuz 2 kopya sınırı, okey farkında per bilgisi, okeyin iki uca işlenmesi, okey alma, hatalı açma, işlek taş, geri topla, elden bitiş puanı).
6. 3D masa sahnesi ve varlıklar (`spec/09`, `16`, `24`).
7. Dağıtım töreni (`spec/21`) — zar fiziği deterministik, gösterge/okey HUD.
8. Oyun etkileşimi: sürükle-bırak, atma/alma bölgeleri, işleme göstergeleri, orta tahta, deste sola kayma, taş izi (`spec/11`, `23`).
9. Animasyonlar ve efektler: nükleer bitiş, kutlama, ikramlar (`spec/12`, `23`).
10. Hediye, sohbet, oyuncu kartı, masa ayarları, ek durumlar, el/maç sonu, lobiye dönüş (`spec/13`, `17`).
11. Geliştirici test menüsü (`spec/25`).
12. `spec/07` ve `spec/15` kontrol listeleri + v3 ekran görüntüleriyle yan yana karşılaştırma.

## KABUL
- Tüm `screenshots/v3` durumları Unity'de üretilebilir ve görsel olarak eşleşir.
- Hiçbir elde bir taş 2 kopyadan fazla görünmez; okey yüzü boş; masada yatan taşlar net (Unlit yüz).
- Perler orta tahtada üst üste binmez, eklenince eski perler yerinde kalır.
- 1080p'de 60 FPS (orta seviye GPU).
