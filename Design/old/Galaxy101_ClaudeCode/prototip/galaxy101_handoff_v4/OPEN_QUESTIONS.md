# OPEN_QUESTIONS.md · kullanıcı kararı gerekenler · v0.4.0

Cevaplanana dek motor DECISIONS.md'deki geçici değerle çalışır (varsa). Cevap gelince RULES.md, rules.js ve test vektörü birlikte güncellenir.

| Kimlik | Soru | Şu anki durum | Etkilenen |
|---|---|---|---|
| — | Şu an açık soru yok. | — | — |

## Cevaplananlar

| Kimlik | Soru | Cevap (kullanıcı) |
|---|---|---|
| Q-01 | Katlamalı masa oyunda tam olarak neyi değiştirir? (ör. her elin puanı el sırasıyla katlanır ×1, ×2, ×4…; ya da bahis her elde ikiye katlanır) | Katlamalı yalnız açılış eşiğini yükseltir: seri önceki + 1, çift önceki + 1; bağımsız; el sonunda sıfırlanır; eşlide "ortağın açılışı yükseltsin" seçeneği (vars. açık) (R-64). |
| Q-02 | XP miktarları: el / maç başına kaç XP? Sıra bonusu var mı? Erken kalkan ne alır? (eski öneri: el kazanma +340, kaybetme +60; maç 1. +500, 2. +200, 3. +80) | El bitiren +340, diğerleri +60; sıra bonusu 500/200/80/0; kalkan bonus almaz; seviye eğrisi 375·n (R-92, R-93). |
| Q-03 | Sarmada (12-13-1) 1 kaç puan sayılır: 1 mi, 14 mü? | Sarma geçersiz; 1 her zaman 1; 12-13-1 ve tahtada 1'i 13. sütuna koyma prototipten kaldırıldı (R-12). |
| Q-04 | Elden bitişte 101 / 5 çift eşiği aranmalı mı? | Kullanıcı düzeltmesi: eşik aranır (seri ≥101, çift ≥5; katlamalıda yükselmiş eşik), "en az 15 taş" yok; puan = temel × çarpan + ceza (R-50, R-53). |
| Q-05 | Maç sonu eşitliğinde eşitlik bozucu olsun mu (ör. son elin puanı, daha az ceza)? | Eşitlik bozucu yok; sıra, ödül, LP ve haftalık puan paylaşılır (R-59). |
| Q-06 | Taş bitince el puanlanmalı mı? | Herkes ıstakasını yazar: seri N + okey 101, çift ×2, açmamış 202; cezalar eklenir; −101 ve çarpan yok (R-55). |
| Q-07 | Eşli masada bir oyuncu kalkarsa takım mı son sayılır, yalnız o oyuncu mu? Ortağın bahsi ne olur? | Takım son sayılmaz; yerine bot; kalkan bahsini kaybeder, ödül/XP almaz; takım kazanırsa ödülün tamamı ortağa (R-60). |
| Q-09 | Ekonomide %5 kesinti çip havuzundan mı yakılır, yoksa bir hesaba mı gider? Günlük ödül ve görev miktarları prototipteki değerler mi kalsın? | Kesinti %0; günlük seri 1.000…15.000 ve görevler prototip değerleriyle (R-91). |
| Q-08 | Ardışık kaç zaman aşımında oyuncu otomatik oyuna (bota) devredilir ve bu kalkma sayılır mı? | Q-10 cevabıyla kapandı: bağlantısı kopuk oyuncuda üst üste 2 zaman aşımı → koltuğu sunucu oynar; kalkma sayılmaz (R-66). |
| Q-10 | Bağlantı kopma toleransı: kaç sn sonra oyuncu "kalktı" sayılır (bahis kaybı, bot devralır)? Hiç sayılmamalı mı? | Kalktı sayılmaz; normal tur süresi; 2 zaman aşımından sonra sunucu oynar; maç bitene dek dönebilir. Bekleme odasında hemen çıkar. İstemci "Deneme n/5", 3 sn, sonra "Lobiye dön" (R-66). |
| Q-11 | Liderlik puanı nasıl hesaplanır (haftalık / sezon)? Dereceli LP mi, kazanılan çip mi, el kazanma mı? | Haftalık 30/15/5/1 (çevrimiçi; özel oda hariç); sezon = dereceli LP; arkadaşlar = arkadaşlar arası sezon LP (R-95). |
| Q-12 | Maç bittiğinde masa ne olur: kapanır mı, aynı oyuncularla bekleme odasına mı döner ("Tekrar oyna")? | Maç bitince masa kapanır, herkes lobiye döner. |
| Q-13 | "Oyuna katıl": oynanan masada boş koltuk (kalkanın yerindeki bot) yeni oyuncuya verilir mi? Verilirse bahis ve puanı ne olur? | Evet; botun ya da kalkanın koltuğu devralınır, bahis o an ödenir, koltuğun toplamıyla devam (R-65). |
| Q-14 | 3 yanlış şifreden sonra kilit süresi ve açma yolu (şifremi unuttum)? | 5 dk kilit; "Şifremi unuttum" ile şifre değişince kilit kalkar (R-98). |
| Q-15 | Misafir ve yeni hesabın başlangıç çip ve elmas miktarı? | Misafir 10.000 çip, e-posta/Google 25.000 çip; herkese 20 elmas (R-96). |
| Q-16 | Günlük ödül serisi bir gün atlanırsa sıfırlanır mı, 7. günden sonra başa mı döner? | Kaçırılırsa 1. güne döner; 7. günden sonra baştan; gün değişimi Europe/Istanbul (R-97). |
