# strings/ · metinler · v0.4.0

| Dosya | İçerik |
|---|---|
| `strings.json` | 751 tablo metni (`STR`, `TAGS`, `LG`, `RULES`, `TASKS`, `GIFTS`, `SET_EN`, `G101Settings`, lig adları) · 241 satır içi metin (`en ? '…' : '…'` ifadeleri; aynı metin tek kayıt, `uses` kullanıldığı satırlar) · 49 hata metni (`protocol.json` hata kodları; sessiz ve resync olanlar metinsiz) |
| `strings.csv` | Aynı içerik düz liste (UTF-8 BOM, Excel/Sheets/Unity Localization içe aktarımı için): `key, tr, en, params, source` |

Anahtar: `<ekran>.<tablo>.<yol>` · ekranlar `app` (açılış, giriş, yükleme), `menu`, `lobby`, `ranked`, `table`, `shared`. Satır içi anahtarlar `<ekran>.inline.<alan>@<satır>`; satır numarası yalnız kaynağı bulmak içindir, Unity'de anlamlı adla yeniden adlandırılabilir.

Hata metinleri: `source: "oyun"` → metin oyunda zaten görünen metindir (`key` nereden geldiğini söyler). `source: "yeni"` → arayüzde istemci doğrulamasıyla önceden engellenen kodlar için sunucu yanıtı metni olarak bu pakette yazıldı; ton oyunla aynı (kısa, ikinci tekil, · ayırıcılı). `{0}` = `detail` alanı (`missing`, `until` → dakika).

Denetim: iki dilde aynı anahtar eksiksiz (eksik 0). `notes`: iki dilde ifade sırası/eki farklı satırlar (beklenen dil farkı, hata değil).
Ad, para ve tarih biçimleri `format` alanında.
