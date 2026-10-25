# 16 · Masa varlıkları: GLB → Blender → Unity

Bu belge, 3D modellerin indirilmesinden Claude Code'a teslimine kadar olan adımları anlatır. Adım A–C'yi siz yaparsınız; D'den sonrası Claude Code'un işidir.

## A. Modelleri indirin

1. Paket klasöründe terminal açın: `npx serve reference/table` (ya da `python -m http.server` → `reference/table`).
2. Tarayıcıda `Galaxy101_3D_Varlik_Disa_Aktarma.html` açın. Gezegen teması **Satürn** kalsın.
3. **Tümünü GLB olarak indir** → 28 dosya. Tarayıcı "birden çok dosya indirme" izni isterse onaylayın.
4. Sayıyı kontrol edin: masa 4 · ıstaka 6 · taş seti 5 + tek taş 1 · koltuk 1 · oda 1 · ikram 6 · hediye 4.
5. Dosyaları paket içinde `assets_src/glb/` klasörüne taşıyın (dosya adlarını değiştirmeyin).

Diğer gezegenler için ayrıca indirmeye gerek yok. Gezegen renkleri (LED, eloksal, dikiş, neon tüp, masa kenarı, deri, çuha) Unity'de malzeme adına göre çalışma anında atanır (`spec/09` §8, `table_content.json → planets`).

## B. Blender'da cilalayın (isteğe bağlı ama önerilir)

Blender 4.x · File → Import → glTF 2.0. Her dosya için:

| Kural | Ayrıntı |
|---|---|
| İsimler | Nesne ve malzeme adlarını **değiştirmeyin** (`istaka_govde`, `istaka_uc_sol`, `tas_kenar_neon` …). Unity kodu bunlara bağlanır. |
| Ölçü / orijin | Metre, +Y yukarı. Orijin ve ölçek aynı kalır (Apply Transform yapmayın; değerler zaten doğru). |
| Kenarlar | Bevel modifier (0.5–1 mm, 2 segment) ya da Weighted Normal; profil dışarıdan değişmesin. Istaka raf ve dudak yükseklikleri ±0.5 mm içinde kalmalı (taşların oturduğu yüzeyler). |
| UV | Smart UV Project; ahşap/karbon/fırçalanmış metalde damar yönü ıstaka boyunca (x). |
| Dokular | Prosedürel kanvas dokuları 2K PBR'a pişirin: Base Color, Roughness, Normal, (gerekirse) Metallic ve Emission. Doku adları malzeme adıyla başlasın: `istaka_ceviz_BaseColor.png`. |
| Taşlar | Taş gövdesine dokunmayın (ölçü 50 × 70 × 10 mm sabit). Yüz dokuları Unity'de atlas olarak yeniden üretilecek. |
| LOD | Istaka, masa, koltuk ve hediyeler için LOD1 (%50 Decimate). Adlandırma `_LOD0`, `_LOD1`. |
| Dışa aktarma | File → Export → glTF 2.0 (.glb), "Apply Modifiers" açık, Materials: Export, Images: Automatic. Aynı dosya adıyla `assets_src/blender_out/` klasörüne kaydedin. |

Blender adımını atlarsanız Claude Code `assets_src/glb/` içindeki ham GLB'leri kullanır; sonuç prototiple aynı görünür, yalnızca doku çözünürlüğü daha düşük olur.

## C. Klasör yapısı (Claude Code'a vermeden önce)

```
<UnityProjesi>/
  Design/galaxy101_unity_handoff/      ← bu paketin tamamı
    README.md, PROMPT_CLAUDE_CODE.md
    spec/ (01–16 + data/)
    screenshots/ + screenshots/table/
    reference/ + reference/table/     ← prototipler ve 3D kaynak kodu
    assets_src/
      glb/                            ← A adımındaki 28 ham GLB
      blender_out/                    ← B adımındaki cilalı GLB'ler (varsa)
```

## D. Unity tarafı (Claude Code yapar)

1. glTFast paketini kurar; `blender_out/` varsa onu, yoksa `glb/` klasörünü `Assets/Galaxy101/Models/` altına alır.
2. Her GLB için prefab: `Table_{station|wood|holo|andromeda}`, `Rack_{walnut|sedef|chrome|carbon|neon|andromeda}`, `Chair`, `Room`, `Gift_{id}`.
3. Istaka prefab'larına 30 `Slot` transform ekler (`slotLocal` formülü, `spec/09` ve `table_scene.json`).
4. Taş: tek taş GLB'sinden gövde mesh'i; 2 malzeme yuvası (`tas_<görünüm>`, `tas_kenar_<görünüm>`). Yüzler `tiles3d.js → GLYPHS` SVG yollarından editör aracıyla pişirilen **atlas** (5 görünüm × 53 hücre, hücre 128×180 px, 2× çözünürlük önerilir).
5. Shader Graph: iridescence (Andromeda, sedef), clearcoat (URP Complex Lit), neon cam (Transparent + Fresnel), emissive LED'ler Bloom ile.
6. `SkinService`: masa / ıstaka / taş görünümü × gezegen renklerini malzeme adlarına göre uygular; Ayarlar → Görünüm kartlarıyla anında değişir.

## E. Kontrol

- Prototipte Ayarlar → Görünüm'den her ıstaka ve taş görünümünü seçip Unity'deki karşılığıyla yan yana karşılaştırın.
- `spec/15_MASA_KABUL_LISTESI.md` maddelerinin hepsi işaretlenmeden teslim alınmaz.
