# 05 — Davranış ve State

## 1. Gezinme haritası

```
Ana menü (1a)
├─ Avatar ─────────────► Profilim [Genel | Rozetler | Maç geçmişi]
│                          ├─ Profili düzenle ──► (alt pencere)
│                          └─ Paylaş ───────────► Profili paylaş (alt pencere)
├─ Çip "+" ────────────► Mağaza [Çip]
├─ Elmas "+" ──────────► Mağaza [Elmas]
├─ Dişli ──────────────► Ayarlar [Oyun | Ses | Görüntü | Dil | Hesap]
│                          └─ Hesap: Profili düzenle · Çıkış yap → onay → tamam · Hesabı sil → onay(SİL) → tamam
├─ Mod kartı ──────────► seçili mod değişir (HEMEN OYNA alt yazısı)
├─ HEMEN OYNA ─────────► eşleşme akışı (bu paketin kapsamı dışında; onPlay(mode) olayı yayınla)
├─ Mağaza ─────────────► Mağaza [Çip | Elmas | Görünüm]
├─ Arkadaşlar ─────────► Arkadaşlar [Çevrimiçi | Tümü | İstekler] ── Arkadaş ekle (alt pencere)
├─ Liderlik ───────────► Liderlik [Haftalık | Sezon | Arkadaşlar]
├─ Günlük ödül ────────► Günlük ödül
├─ Kurallar ───────────► Kurallar [6 bölüm]
└─ Gezegen küresi ─────► tema değişir
```

- Aynı anda **bir pencere** + en fazla **bir alt pencere** açık olur. Yeni pencere açılırsa (ör. Profilim'den Mağaza değil; ama çip "+") önceki pencere ve Ayarlar kapanır.
- **Esc**: alt pencere varsa onu, yoksa pencereyi/Ayarlar'ı kapatır. Ana ekranda Esc bir şey yapmaz.
- Arka plana tıklama = Esc.
- Pencere her açıldığında son seçili sekmesini hatırlar (oturum boyunca).

## 2. State modeli (öneri)

```csharp
class MainMenuState {
  int SelectedMode = 0;                 // 0..5 (Tekli varsayılan)
  string Planet = "Satürn";            // Satürn|Mars|Neptün|Jüpiter|Venüs
  string? OpenPanel;                    // null|shop|friends|leader|daily|rules|me
  bool SettingsOpen; int SettingsTab;   // 0..4
  Dictionary<string,int> PanelTab;      // panel → sekme
  int RulesSection;                     // 0..5
  string? Dialog;                       // null|profile|logout|delete|bye|add|share
  string ByeFrom;                       // logout|delete
  Profile Profile;                      // Name="Doğukan Y.", Color="#FF8A5B", Status="", Frame=0 (0 Klasik,1 Altın,2 Galaksi-kilitli)
  Profile? Draft;                       // düzenleme sırasında
  Settings Settings;                    // content.json → settingsDefaults (+ resolution)
  string FriendQuery; bool QueryFocus;
  HashSet<string> Invited, RequestsHandled;
  AddFriendState AddFriend;             // Query, Result(null|none|friend|Player), Sent{code}
  bool DayClaimed; HashSet<int> QuestsClaimed;
  string DeleteTyped;
}
```

Kalıcı olanlar: `Settings`, `Planet`, `Profile`, `SelectedMode`. Oturumluk olanlar: açık pencere, sekmeler, arama sorgusu.

## 3. Kurallar ve doğrulamalar

| Konu | Kural |
|---|---|
| Oyuncu adı | `Trim()` sonrası 3–16 karakter; `^[\p{L}\p{N} ._]+$`; geçersizse Kaydet pasif |
| Durum mesajı | en fazla 40 karakter |
| Baş harfler | boşlukla ayrılmış kelimelerin ilk harfleri, ilk 2, `ToUpper(tr-TR)`; boşsa "?" |
| Hesap silme | girilen metin `Trim().ToUpper(kültür)` == "SİL" (EN "DELETE") |
| Arkadaş arama (liste) | sorgu küçük harf (tr-TR), baştaki `#` atılır; ad *içerir* veya kod (tire yok sayılarak) *içerir* |
| Arkadaş ekle | `#` ve boşluk atılır, büyük harf; "AB1234" → "AB-1234"; önce mevcut arkadaş kodları (→ "zaten arkadaşsınız"), sonra dizinde kod tam eşleşme, sonra ad içerir; yoksa "bulunamadı" |
| Geri sayımlar | yerel saatle gece yarısına; mağaza HH:MM:SS her saniye, görevler "X sa Y dk" |
| Çerçeve | Galaksi seviye 30'da açılır (şu an 24 → kilitli) |
| Günlük seri | gün 1–4 toplandı, gün 5 bugün; "Topla" → toplandı (+5.000 çip), rozet "1" kalkmalı |
| Görevler | `cur ≥ max` → tamamlandı; alınmamışsa "Al" → "Alındı" (+ödül) |
| Davet | "Davet et" → "Gönderildi" (geri alınamaz) |
| İstek | Kabul/Reddet → satır kalkar, sekme sayısı azalır |
| Slider | tamsayı; aralık Tur süresi 1–10 sn, sesler 0–100 % |
| Dil | anında uygulanır; kalıcı |
| Arka plan hareketi | kapalıysa gezegen/yıldızlar durur |
| Çözünürlük | 720p / 1080p / 2K (Windows'ta pencere boyutu/çözünürlük); UI ölçeği referans 1280×720 |

## 4. Sayı ve metin biçimleri

| | TR | EN |
|---|---|---|
| Binlik | 125.400 | 125,400 |
| Para | ₺249,99 | ₺249.99 |
| Yüzde | %41 | 41% |
| Seviye kısaltması | SV 24 | LV 24 |
| Gün | 5. Gün | Day 5 |
| Sıra (geçmiş) | 1. | #1 |
| Zaman | 12 dk önce · 3 sa önce · Dün | 12 min ago · 3 h ago · Yesterday |

## 5. Servisler (mock → gerçek)

| Servis | Metotlar |
|---|---|
| `IProfileService` | Get, Save(name, color, status, frame), GetStats, GetBadges, GetHistory, Logout, RequestDelete |
| `IEconomyService` | Balance (chips, gems), GetOffers(tab), GetDeal, Purchase(id) |
| `IFriendsService` | GetFriends, GetRequests, Invite(id), Accept(id), Decline(id), Search(query), SendRequest(code) |
| `ILeaderboardService` | Get(scope: weekly/season/friends) → podium, rows, me |
| `IRewardsService` | GetStreak, ClaimDay, GetQuests, ClaimQuest(i), ResetTime |
| `ISettingsService` | Load, Save, ResetToDefaults |
| `IShareService` | ProfileUrl, ShareTo(platform), CopyToClipboard |

Mock veriler: `data/content.json`.
