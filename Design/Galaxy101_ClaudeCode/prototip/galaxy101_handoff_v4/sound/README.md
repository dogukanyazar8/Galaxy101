# sound/ · ses · v0.5.0

| Klasör / dosya | İçerik |
|---|---|
| `sfx/` | 28 efekt (WAV, 44,1 kHz, 16 bit, stereo) |
| `music/` | 2 kesintisiz döngü: `music_menu_loop.wav` (32 sn), `music_table_loop.wav` (40 sn) |
| `events.json` | Her olayın tetikleyicisi, kategorisi, önceliği, eş zamanlı kopya sınırı, süresi ve dosyası; müzik geçişi ve kısma kuralları |

Oyunda: `g101-sound.js` aynı olayları çalar (masa sesleri durum farkından, ikram/hediye, ekran geçişi, ödül ve hata tostu doğrudan, pointer imleçli öğeye basış `ui.tap`). Menü ekranlarında menü müziği, masada masa müziği çalar; tarayıcı izni nedeniyle ilk dokunuşla başlar.
Ayarlar: Ana ses (master), Efektler (sfx), Müzik (music), Taş sesleri (tileSnd), Sohbet/Bildirim sesleri (chatSnd, notifSnd).

Kural motoru olaylarıyla eşleme: drew → tile.draw · discarded → tile.discard · opened/laid → meld.lay · added → meld.add · okeySwapped → okey.swap · penalty → penalty · finished → finish / finish.nuke · handEnd → hand.end · turn(seat=ben) → turn.mine · dealStart/dealDie/dealStep → deal.* / dice.roll · gift → gift.* · chat → chat.message.
