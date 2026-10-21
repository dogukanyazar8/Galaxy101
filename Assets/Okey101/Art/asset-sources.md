# A3 kaynak kaydı

Modeller, dokular, rakam çizgileri ve sesler bu proje için sıfırdan üretildi.
İndirilen veya satın alınan üçüncü taraf varlık yok. Kullanım: proje sahibine ait özgün üretim.

- Üretici: `scripts/generate-table-art.py`; Blender 5.2.2 LTS, yerleşik `bpy`, NumPy ve Python standart kitaplığı.
- `Sources/okey-table.blend`: üç modelin düzenlenebilir ana sahnesi. Metre, Z yukarı kaynak; FBX dışa aktarımı Y yukarı, -Z ileri. Animasyon, ışık, kamera, eklenti veya simülasyon dışa aktarılmaz.
- `Models/Tile.fbx`: ortak `.72 × .16 × .92` Unity birimlik pahlı gövde. Tek ortak gövde materyali, basit kutu çarpışması; Rigidbody yok.
- `Models/Rack.fbx`: iki sıra, 24 yuva; en fazla 22 fiziksel taş. Yuvaların çarpışmaları koddan üretilir.
- `Models/Table.fbx`: ceviz çerçeve, ince pirinç şerit, koyu yeşil keçe. Prosedürel özgün geometri.
- `Textures/TileFaces.png`: 4096×1024 RGBA atlas, 16 sütun × 4 renk. 1–13 rakamları özgün çizgi yolları; kalan hücrelerde 101 arka yüz. Renk başına daire, baklava, üçgen, kare işaretleri. Font dosyası kullanılmadı.
- `Textures/FeltWeave.png`, `WoodGrain.png`: sabit tohumlu özgün NumPy dokuları.
- `Audio/draw.wav`, `place.wav`, `discard.wav`: 44.1kHz, 16 bit mono; özgün sönümlü sinüsler ve sabit tohumlu filtreli gürültü. Kayıt veya örnek ses yok.
- Unity `LegacyRuntime.ttf`: mevcut Unity yerleşik yazı tipi, yalnız arayüz ve O/S okey işaretleri; dosya projeye kopyalanmaz. Atlas bu fontu kullanmaz.

Tekrar üretim (proje kökünde):

```powershell
& 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe' --background --python scripts/generate-table-art.py
& .\scripts\Build-Windows.ps1 -Configuration Development
```

Build adımı ortak Unity materyallerini, FBX prefablarını ve Table sahnesini günceller. Taş başına materyal kopyalanmaz; atlas hücresi `MaterialPropertyBlock` ile seçilir. Kaynak `.blend` build sahnesi tarafından referanslanmaz.
