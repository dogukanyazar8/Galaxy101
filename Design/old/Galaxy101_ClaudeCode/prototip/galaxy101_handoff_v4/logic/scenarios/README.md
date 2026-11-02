# logic/scenarios · senaryolar · v0.4.0

Sunucu senaryosu, ilk elin dağıtımını sabitler; gerisi gerçek motor, gerçek botlar ve gerçek tören ile oynanır.

| Dosya | Kural | Durum | Ne olur |
|---|---|---|---|
| `R43.json` | R-43 açtıran taş | T34 | Attığın K9 ile açmamış koltuk 1 botu açar → sana +90 |
| `R44.json` | R-44 okey alma | T36 | Açtığın serideki okeyi koltuk 1 botu R12 ile alır → sana +101 |
| `client-tests.json` | — | G13–G28 | Motorsuz istemci test akışlarının hazır elleri (yalnız prototip) |

Biçim: `deal = {dealer, dice:[a,b], indicator, racks:[[koltuk 0], [koltuk 1], …], deckTop}`. Eksik ıstakalar ve deste, kalan taşlarla deterministik dolar.
`rules.rigFromHands(deal)` bunu dağıtım sırasına çevirir (`{dealer, dice, order}`); `startHand` aynı `rig` ile çağrılır. Dağıtım töreni, zar ve gösterge bu sıradan çıkar, istenen ıstakalar aynen oluşur.

Kullanım:
- Sunucu: `createServer({ seed, rig })` → yalnız ilk el sabitlenir (`// DEMO:`).
- Testler: `tests/rules/scenarios.json` (`init.scenario`, `botTurn` adımı).
- Oyun: `?state=T34` / `?state=T36` → `g101-state.js` `scenario` adımı, sonra `waitMy`, `masa scOpen / scDiscard`, `waitPen`.
