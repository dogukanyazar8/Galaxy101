# v4 · Unity farkları

Prototipten bilinçli olarak farklı kalan noktalar: UI Toolkit / Unity kısıtı ya da yakalama koşulu, seçilen yöntem ve etkisi. Kalan her fark ya burada ya `EKSIKLER.md`'de.

| Kimlik | Fark | Neden (kısıt) | Seçilen yöntem | Etkilenen durumlar |
|---|---|---|---|---|
| UF-01 | Unity karelerinde sağ altta "Development Build" filigranı | Yönlendirici yalnız `UNITY_EDITOR || DEVELOPMENT_BUILD` derlemesinde (görev §0.5, KARARLAR K-06); Unity Development oyuncusu bu filigranı her zaman çizer | Karşılaştırmada 1150–1280 × 696–720 bölgesi maskelenir (`tools/v4/compare_states.py`) | hepsi |
