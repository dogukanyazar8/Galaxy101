# Fark matrisi · animasyon

Üretildi: `python tools/v4/anim_matrix.py`. "Unity'de ad" sütunu ad eşleşmesidir (ipucu); değer karşılaştırması Faz 2'de durum durum yapılır (`DURUMLAR.md`).

## css.json · 67 keyframe, 157 kullanım, kullanılmayan 11 (taşınmaz)

| Kimlik | Pakette (kullanım: öğe · süre · eğri · tekrar) | Unity'de ad | Durum |
|---|---|---|---|
| css.fadeIn | app.splash.progress · 500 ms · ease · 1; app.splash.tipBox · 500 ms · ease · 1; auth.side · 600 ms · ease · 1 (+11) | Assets/Galaxy101/Scripts/Screens/MenuView.Main.cs, Assets/Galaxy101/Scripts/Screens/MenuView.Modals.cs | ad var · değer Faz 2 |
| css.panelIn | auth.panel · 450 ms · cubic-bezier(.2,1,.4,1) · 1; form.panel · 400 ms · cubic-bezier(.2,1,.4,1) · 1 | — | eksik (ad yok) · Faz 2 |
| css.modalIn | load.fail · 300 ms · ease · 1; menu.closeSettings · 280 ms · cubic-bezier(.2,1.2,.4,1) · 1; menu.closePanel · 280 ms · cubic-bezier(.2,1.2,.4,1) · 1 (+12) | Assets/Galaxy101/Scripts/Screens/MenuView.Modals.cs, Assets/Galaxy101/Table/Scripts/Hud/TableHud.cs | ad var · değer Faz 2 |
| css.floatA | table.t.discHere · 1400 ms · ease-in-out · infinite; table.dh.rollN · 2000 ms · ease-in-out · infinite | Assets/Galaxy101/Scripts/Screens/MenuView.Main.cs, Assets/Galaxy101/Table/Scripts/Hud/TableHud.cs | ad var · değer Faz 2 |
| css.pulseG | auth.guest · 2200 ms · ease · infinite; menu.play · 2200 ms · ease · infinite; table.nextHand · 2000 ms · ease · infinite | Assets/Galaxy101/Scripts/Screens/AuthFlow.cs, Assets/Galaxy101/Scripts/Screens/MenuView.Main.cs | ad var · değer Faz 2 |
| css.shine | auth.guest · 3000 ms · ease · infinite; menu.play · 2800 ms · ease · infinite; menu.h.when[{{ $index }}] · 3600 ms · ease · infinite | Assets/Galaxy101/Scripts/Components/SkewBand.cs, Assets/Galaxy101/Scripts/Dialogs/PanelUi.cs | ad var · değer Faz 2 |
| css.spin | auth.google · 800 ms · linear · infinite; form.submit · 800 ms · linear · infinite; table.dh.rollN · 2400 ms · linear · infinite (+2) | Assets/Galaxy101/Scripts/Screens/LobbyScreen.Room.cs, Assets/Galaxy101/Table/Scripts/Game/TableBoard.cs | ad var · değer Faz 2 |
| css.shake | form.error · 400 ms · ease · 1 | Assets/Galaxy101/Scripts/Screens/LobbyScreen.cs, Assets/Galaxy101/Scripts/Screens/LobbyScreen.Room.cs | ad var · değer Faz 2 |
| css.toastIn | app.toast · 350 ms · ease · 1; ? · 300 ms · ease · 1; table.thank · 300 ms · ease · 1 | Assets/Galaxy101/Scripts/Core/MenuView.cs, Assets/Galaxy101/Table/Scripts/Hud/TableHud.cs | ad var · değer Faz 2 |
| css.g101Float | app.splash.tile[0] · 6000 ms · cubic-bezier(.45,0,.55,1) · infinite; app.splash.tile[1] · 6000 ms · cubic-bezier(.45,0,.55,1) · infinite; app.splash.tile[2] · 6000 ms · cubic-bezier(.45,0,.55,1) · infinite (+6) | Assets/Galaxy101/Scripts/Screens/AuthFlow.cs | ad var · değer Faz 2 |
| css.g101Glint | app.splash.tile[0] · 7000 ms · ease-in-out · infinite; app.splash.tile[1] · 7000 ms · ease-in-out · infinite; app.splash.tile[2] · 7000 ms · ease-in-out · infinite | — | eksik (ad yok) · Faz 2 |
| css.g101TileIn | auth.side.logo · 1100 ms · cubic-bezier(.16,1,.3,1) · 1; auth.side.logo · 1100 ms · cubic-bezier(.16,1,.3,1) · 1; ? · 1100 ms · cubic-bezier(.16,1,.3,1) · 1 (+3) | Assets/Galaxy101/Scripts/Screens/AuthFlow.cs | ad var · değer Faz 2 |
| css.tileIn | app.splash.tile[0] · 1150 ms · cubic-bezier(.16,1,.3,1) · 1; app.splash.tile[1] · 1150 ms · cubic-bezier(.16,1,.3,1) · 1; app.splash.tile[2] · 1150 ms · cubic-bezier(.16,1,.3,1) · 1 | Assets/Galaxy101/Scripts/Screens/AuthFlow.cs | ad var · değer Faz 2 |
| css.logoIn | app.splash.logo · 1100 ms · cubic-bezier(.16,1,.3,1) · 1 | — | eksik (ad yok) · Faz 2 |
| css.splashOut | — | Assets/Galaxy101/Scripts/Screens/AuthFlow.cs | ad var · değer Faz 2 |
| css.tilesOut | — | — | eksik (ad yok) · Faz 2 |
| css.menuIn | — | Assets/Galaxy101/Scripts/Screens/AuthFlow.cs, Assets/Galaxy101/Scripts/Screens/MenuView.Main.cs | ad var · değer Faz 2 |
| css.planetFloat | app.stage · 14000 ms · ease-in-out · infinite; menu.starsA · 14000 ms · ease-in-out · infinite | — | eksik (ad yok) · Faz 2 |
| css.bob | menu.openDaily · 1600 ms · ease · infinite | Assets/Galaxy101/Scripts/Dialogs/PanelUi.cs, Assets/Galaxy101/Scripts/Screens/AuthFlow.cs | ad var · değer Faz 2 |
| css.iconShine | menu.t.rank · 3200 ms · ease-in-out · infinite; menu.buyChips · 3200 ms · ease-in-out · infinite; table.deckCount · 3200 ms · ease-in-out · infinite | Assets/Galaxy101/Scripts/Screens/MenuView.Main.cs | ad var · değer Faz 2 |
| css.lbFade | lobby.g.label[{{ $index }}] · 300 ms · ease · 1; lobby.openCreate#2 · 300 ms · ease · 1; lobby.w.start · 200 ms · ease · 1 (+3) | — | eksik (ad yok) · Faz 2 |
| css.lbCard | — | Assets/Galaxy101/Scripts/Screens/LobbyScreen.cs | ad var · değer Faz 2 |
| css.lbModal | lobby.iv.close · 320 ms · cubic-bezier(.22,1,.36,1) · 1; lobby.closeCreate · 300 ms · cubic-bezier(.2,1.2,.4,1) · 1; lobby.closeDlg · 250 ms · ease · 1 (+4) | Assets/Galaxy101/Scripts/Screens/LobbyScreen.Create.cs | ad var · değer Faz 2 |
| css.lbSpin | lobby.w.start · 800 ms · linear · infinite; lobby.r.go[{{ $index }}] · 800 ms · linear · infinite; lobby.closeDlg · 800 ms · linear · infinite | — | eksik (ad yok) · Faz 2 |
| css.lbShake | lobby.b.label[{{ $index }}] · 400 ms · ease · 1 | Assets/Galaxy101/Scripts/Screens/LobbyScreen.Room.cs | ad var · değer Faz 2 |
| css.lbPop | lobby.w.status · 400 ms · ease · 1 | — | eksik (ad yok) · Faz 2 |
| css.lbPulse | lobby.openCreate · 2400 ms · ease · infinite | Assets/Galaxy101/Scripts/Screens/LobbyScreen.cs | ad var · değer Faz 2 |
| css.lbToast | lobby.d.toRoom · 300 ms · ease · 1 | Assets/Galaxy101/Scripts/Screens/LobbyScreen.cs | ad var · değer Faz 2 |
| css.lbDots | lobby.z.team[{{ $index }}] · 1200 ms · ease · infinite; lobby.z.team[{{ $index }}] · 1200 ms · ease · infinite; lobby.z.team[{{ $index }}] · 1200 ms · ease · infinite | — | eksik (ad yok) · Faz 2 |
| css.rkFade | ranked.v.pick[{{ $index }}] · 250 ms · ease · 1; ranked.cancel · 250 ms · ease · 1; ranked.text.noHist · 200 ms · ease · 1 (+2) | — | eksik (ad yok) · Faz 2 |
| css.rkModal | ranked.closeInvite · 280 ms · cubic-bezier(.2,1.2,.4,1) · 1; ranked.f.go[{{ $index }}] · 300 ms · cubic-bezier(.2,1.2,.4,1) · 1; ranked.found.acceptLabel · 300 ms · cubic-bezier(.2,1.2,.4,1) · 1 | — | eksik (ad yok) · Faz 2 |
| css.rkSpin | ranked.z.ini[{{ $index }}] · 900 ms · linear · infinite; ranked.text.pending[{{ $index }}] · 1100 ms · linear · infinite; ranked.cta.sub · 800 ms · linear · infinite (+1) | — | eksik (ad yok) · Faz 2 |
| css.rkSeat | ranked.z.kick[{{ $index }}] · 450 ms · cubic-bezier(.2,1.3,.4,1) · 1; ranked.p.ini[{{ $index }}] · 300 ms · ease · 1 | — | eksik (ad yok) · Faz 2 |
| css.rkPulse | — | — | eksik (ad yok) · Faz 2 |
| css.rkRays | ranked.res.again · 24000 ms · linear · infinite | — | eksik (ad yok) · Faz 2 |
| css.rkPop | ranked.res.pKicker · 700 ms · cubic-bezier(.2,1.3,.4,1) · 1 | — | eksik (ad yok) · Faz 2 |
| css.rkUp | ranked.res.pKicker · 400 ms · ease-out · 1; ranked.res.pTitle · 400 ms · ease-out · 1; ranked.res.pSub · 400 ms · ease-out · 1 (+1) | — | eksik (ad yok) · Faz 2 |
| css.rkFloat | ranked.text.seasonTag · 4000 ms · ease-in-out · infinite; ranked.res.pKicker · 3000 ms · ease-in-out · infinite | — | eksik (ad yok) · Faz 2 |
| css.rkToast | ranked.res.close#2 · 350 ms · ease · 1 | — | eksik (ad yok) · Faz 2 |
| css.ring | table.p.open[{{ $index }}] · 4000 ms · linear · 1 | Assets/Galaxy101/Resources/Galaxy101/UI/Base.uss, Assets/Galaxy101/Scripts/Components/GalaxyFilters.cs | ad var · değer Faz 2 |
| css.drawerIn | table.mo.sub · 350 ms · cubic-bezier(.2,1.2,.4,1) · 1; table.closeGift · 300 ms · cubic-bezier(.2,1.2,.4,1) · 1 | Assets/Galaxy101/Table/Scripts/Hud/TableHud.Gifts.cs, Assets/Galaxy101/Table/Scripts/Hud/TableHud.Social.cs | ad var · değer Faz 2 |
| css.drawerInL | table.closeChat · 300 ms · cubic-bezier(.2,1.2,.4,1) · 1 | Assets/Galaxy101/Table/Scripts/Hud/TableHud.Social.cs | ad var · değer Faz 2 |
| css.popIn | table.p.open[{{ $index }}] · 500 ms · ease · 1; table.dh.rollN · 550 ms · cubic-bezier(.2,1.3,.4,1) · 1 | Assets/Galaxy101/Table/Scripts/Hud/TableHud.cs | ad var · değer Faz 2 |
| css.bubble | table.p.bubble[{{ $index }}] · 2800 ms · ease · 1; table.me.bubble · 2800 ms · ease · 1 | Assets/Galaxy101/Table/Scripts/Hud/TableHud.cs, Assets/Galaxy101/Table/Scripts/Hud/TableHud.Gifts.cs | ad var · değer Faz 2 |
| css.dhIn | table.countEl · 550 ms · cubic-bezier(.22,1,.36,1) · 1; table.dh.roll · 400 ms · cubic-bezier(.22,1,.36,1) · 1 | Assets/Galaxy101/Table/Scripts/Hud/TableHud.Deal.cs | ad var · değer Faz 2 |
| css.dhSub | table.dh.sub · 400 ms · ease · 1 | Assets/Galaxy101/Table/Scripts/Hud/TableHud.Deal.cs | ad var · değer Faz 2 |
| css.dhTile | table.dh.ind.num · 600 ms · cubic-bezier(.22,1,.36,1) · 1; table.dh.ok.num · 600 ms · cubic-bezier(.22,1,.36,1) · 1 | Assets/Galaxy101/Table/Scripts/Hud/TableHud.Deal.cs | ad var · değer Faz 2 |
| css.rollBob | table.dh.roll · 2200 ms · ease-in-out · infinite | Assets/Galaxy101/Table/Scripts/Hud/TableHud.Deal.cs | ad var · değer Faz 2 |
| css.rollIcon | table.dh.roll · 2200 ms · ease-in-out · infinite | Assets/Galaxy101/Table/Scripts/Hud/TableHud.Deal.cs | ad var · değer Faz 2 |
| css.nukeFlash | — | — | eksik (ad yok) · Faz 2 |
| css.bombPulse | table.doBomb · 1400 ms · ease · infinite | — | eksik (ad yok) · Faz 2 |
| css.bombWiggle | table.doBomb · 1600 ms · ease-in-out · infinite | — | eksik (ad yok) · Faz 2 |
| css.barFill | table.nextHand · 15000 ms · linear · 1 | Assets/Galaxy101/Table/Scripts/Hud/TableHud.cs | ad var · değer Faz 2 |
| css.wifi | table.closeSettings#3 · 1600 ms · ease · infinite; table.closeSettings#3 · 1600 ms · ease · infinite; ? · 1600 ms · ease · infinite | Assets/Galaxy101/Scripts/Screens/LobbyScreen.Room.cs, Assets/Galaxy101/Scripts/Screens/TableLoadingOverlay.cs | ad var · değer Faz 2 |
| css.countPop | — | Assets/Galaxy101/Table/Scripts/Hud/TableHud.cs | ad var · değer Faz 2 |
| css.dots | table.p.status[{{ $index }}] · 1200 ms · ease · infinite | Assets/Galaxy101/Scripts/Screens/LobbyScreen.Room.cs, Assets/Galaxy101/Table/Scripts/Hud/TableHud.cs | ad var · değer Faz 2 |

Kullanılmayanlar (taşınmaz): dealIn, popScale, tileFloat, tileShine, twinkle, spinS, tableSpin, planetSpin, rkCard, rkDots, rise

## tween3d.json

| Kimlik | Pakette | Unity'de ad | Durum |
|---|---|---|---|
| tween3d.dist.p0 ile hedef arasındaki 3D uzaklık | "p0 ile hedef arasındaki 3D uzaklık" | — | Faz 2 |
| tween3d.clamp.cl(v,a,b) = min(max(v,a),b) | "cl(v,a,b) = min(max(v,a),b)" | — | Faz 2 |
| tween3d.tileTween.common | "konum: lerp(p0,p1,e2) + y += sin(π·min(1,u))·arc; dönüş: slerp(q0,q1, easeInOutCubic(min(1,u·1.15))); e = easeOutCubic( | Assets/Galaxy101/Scripts/Screens/MenuView.Main.cs, Assets/Galaxy101/Table/Scripts/Core/ThreeBlend.cs | ad var · değer Faz 2 |
| tween3d.tileTween.modes | {"travel": {"use": "çekme, atma, okey alma, geri topla/bırak (varsayılan)", "dur": "cl(0.42 + dist·0.38, 0.42, 0.85)", " | Assets/Galaxy101/Scripts/Background/Starfield.cs, Assets/Galaxy101/Scripts/Core/GameDisplay.cs | ad var · değer Faz 2 |
| tween3d.dice.pre | "0.42 (ikinci zar; masadaki yerinden atış noktasına easeInOutCubic, y += sin(π·u)·0.05)" | Assets/Galaxy101/Scripts/Core/MenuModel.cs, Assets/Galaxy101/Scripts/Diagnostics/GalaxyCaptureDriver.cs | ad var · değer Faz 2 |
| tween3d.dice.dur | 1.45 | — | Faz 2 |
| tween3d.dice.fall | "lerp(start, land, 1 − (1−u)^2.4)" | — | Faz 2 |
| tween3d.dice.bounces | 3 | Assets/Galaxy101/Table/Scripts/Game/TableCeremony.cs | ad var · değer Faz 2 |
| tween3d.dice.hide | "0.4 s ölçek 1 → 0 (easeInOutCubic), y += 0.0015/kare" | Assets/Galaxy101/Scripts/Diagnostics/GalaxyCaptureDriver.cs, Assets/Galaxy101/Scripts/Editor/Table/TableAssetBuilder.cs | ad var · değer Faz 2 |
| tween3d.dice.returnsMs | "round((pre + 1.45)·1000)" | — | Faz 2 |
| tween3d.camera.blend | "lerp(a,b, easeInOutCubic(u)), varsayılan 1.6 s" | Assets/Galaxy101/Scripts/Editor/Table/GiftThumbnailBaker.cs, Assets/Galaxy101/Scripts/Editor/Table/TableMaterialConverter.cs | ad var · değer Faz 2 |
| tween3d.camera.idleSway | "x += sin(t·0.21)·0.006; y += sin(t·0.33)·0.004" | — | Faz 2 |
| tween3d.camera.shake | "shakeUntil − now kadar; genlik (kalan)·0.03 rastgele" | Assets/Galaxy101/Scripts/Screens/LobbyScreen.cs, Assets/Galaxy101/Scripts/Screens/LobbyScreen.Room.cs | ad var · değer Faz 2 |
| tween3d.camera.fov | 40 | Assets/Galaxy101/Scripts/Editor/Table/GiftThumbnailBaker.cs, Assets/Galaxy101/Scripts/Editor/Table/TableAssetBuilder.cs | ad var · değer Faz 2 |
| tween3d.camera.views | {"deal": {"pos": [0, 2.05, 1.62], "target": [0, 0.66, -0.12]}, "seat": {"pos": [0, 1.55, 1.62], "target": [0, 0.62, -0.2 | Assets/Galaxy101/Table/Scripts/Core/TableSettings.cs, Assets/Galaxy101/Table/Scripts/Game/TableBoard.cs | ad var · değer Faz 2 |
| tween3d.camera.cues | [{"at": "bekleme odası", "view": "over", "dur": 0}, {"at": "geri sayım", "view": "player (ayar)", "dur": 3}, {"at": "dea | — | Faz 2 |
| tween3d.gifts.fly | {"dur": "1.35 (roket 5.6)", "path": "lerp(start,end,easeInOutCubic(u)) ya da eğri üzerinde k = smoothstepMix(u)", "scale | Assets/Galaxy101/Table/Scripts/Game/TableBoard.cs, Assets/Galaxy101/Table/Scripts/Game/TableCeremony.cs | ad var · değer Faz 2 |
| tween3d.gifts.land | {"dur": 0.5, "squash": "sin(π·v)·(1 − v)", "fx": "burst + wave (roket #FFB347, diğer #FFE08A)"} | Assets/Galaxy101/Scripts/Core/GameDisplay.cs, Assets/Galaxy101/Table/Scripts/Game/TableCeremony.cs | ad var · değer Faz 2 |
| tween3d.gifts.scale | "GIFT_SCALE[kind] // 1.4" | Assets/Galaxy101/Resources/Galaxy101/UI/Base.uss, Assets/Galaxy101/Resources/Galaxy101/UI/Panels.uss | ad var · değer Faz 2 |
| tween3d.fx.burst | {"life": 0.9, "particles": "n (vars. 28)", "speed": "spd (vars. 0.55)", "gravity": "−1.4 /s²"} | Assets/Galaxy101/Table/Scripts/Game/TableCeremony.cs, Assets/Galaxy101/Table/Scripts/Game/TableFx.cs | ad var · değer Faz 2 |
| tween3d.fx.wave | {"life": 0.7, "ring": "iç 0.85 / dış 1, 56 parça, additive", "scale": "0.03 + easeOutCubic(u)·0.16", "opacity": "0.9·(1  | Assets/Galaxy101/Scripts/Background/GalaxyBackground.cs, Assets/Galaxy101/Table/Scripts/Game/TableCeremony.cs | ad var · değer Faz 2 |
| tween3d.fx.shock | {"life": "0.9 (verilen)", "ring": "iç 0.9 / dış 1, 72 parça", "scale": "0.02 + easeOutCubic(u)·max", "opacity": "0.95·(1 | Assets/Galaxy101/Table/Scripts/Game/TableFx.cs | ad var · değer Faz 2 |
| tween3d.fx.fire | {"scale": "0.04 + easeOutQuart(u)·max", "opacity": "op·(1 − u)^1.6"} | Assets/Galaxy101/Scripts/Services/OnlineMenu.cs | ad var · değer Faz 2 |
| tween3d.fx.smoke | {"scale": "0.02 + easeOutCubic(u)·0.11", "opacity": "0.32·(1 − u)", "rise": "0.03 /s"} | Assets/Galaxy101/Table/Scripts/Game/TableFx.cs, Assets/Galaxy101/Table/Scripts/Game/TableGifts.cs | ad var · değer Faz 2 |
| tween3d.fx.trail | {"scale": "s0·(0.35 + 0.65·(1 − u))", "opacity": "op·(1 − u)²"} | Assets/Galaxy101/Table/Scripts/Game/TableFx.cs, Assets/Galaxy101/Table/Scripts/Game/TileView.cs | ad var · değer Faz 2 |
| tween3d.fx.plume | {"count": "34 (14 gövde + 20 başlık)", "life": "2.6 + rnd·0.9", "drag": "gövde 0.975 / başlık 0.982 kare başına"} | — | Faz 2 |
| tween3d.fx.dealFx | {"at": "dealStart", "seq": ["wave #FFD964 @0", "wave tema a1 @170 ms", "wave #FFFFFF @340 ms", "burst #FFE08A ×50 hız 0. | Assets/Galaxy101/Table/Scripts/Game/TableFx.cs, Assets/Galaxy101/Table/Scripts/Game/TableGame.cs | ad var · değer Faz 2 |
| tween3d.fx.nukeFx | {"light": "PointLight #FFB25A, 90·e^(−3t), 2 s, menzil 4.5", "shake": "1.2 s", "glows": ["#FFFFFF 0.55/0.9 s", "#FFD58A  | — | Faz 2 |
| tween3d.fx.celebrate | {"winSpot": "SpotLight #FFE7A3, 3.4 s; şiddet 40·sin(π·min(1, u·1.6)), u > 0.6 ise ·(1 − u)/0.4", "bursts": "8 patlama,  | Assets/Galaxy101/Table/Scripts/Game/TableFx.cs, Assets/Galaxy101/Table/Scripts/Game/TableGame.cs | ad var · değer Faz 2 |
| tween3d.ambient.led | "emissive 2.2 + sin(t·1.2)·0.25" | Assets/Galaxy101/Table/Scripts/Core/SkinService.cs, Assets/Galaxy101/Table/Scripts/Game/TableFx.cs | ad var · değer Faz 2 |
| tween3d.ambient.holo | "Y dönüş 0.12 rad/s (+ boost·1.6), opaklık 0.34 + sin(t·0.9)·0.05" | Assets/Galaxy101/Scripts/Editor/Table/TableAssetBuilder.cs, Assets/Galaxy101/Scripts/Editor/Table/TableMaterialConverter.cs | ad var · değer Faz 2 |

## easings.json

| Kimlik | Pakette | Unity'de ad | Durum |
|---|---|---|---|
| ease.easeOutCubic | {"formula": "1 - (1-u)^3", "source": "table3d.js ease", "bezier": [0.33, 1, 0.68, 1]} | Assets/Galaxy101/Table/Scripts/Core/TableConstants.cs, Assets/Galaxy101/Table/Scripts/Game/TableFx.cs | ad var · değer Faz 2 |
| ease.easeInOutCubic | {"formula": "u<0.5 ? 4u^3 : 1 - (-2u+2)^3/2", "source": "table3d.js easeIO", "bezier": [0.65, 0, 0.35, 1]} | Assets/Galaxy101/Table/Scripts/Core/TableConstants.cs, Assets/Galaxy101/Table/Scripts/Game/TableCeremony.cs | ad var · değer Faz 2 |
| ease.easeOutQuad24 | {"formula": "1 - (1-u)^2.4", "source": "zar düşüşü", "bezier": [0.3, 0.7, 0.55, 1]} | — | Faz 2 |
| ease.easeOutQuart | {"formula": "1 - (1-u)^4", "source": "fx fire"} | — | Faz 2 |
| ease.easeInCubic | {"formula": "u^3", "source": "nuke iniş fazı", "bezier": [0.32, 0, 0.67, 0]} | — | Faz 2 |
| ease.smoothstepMix | {"formula": "k = (u²(3-2u))·0.35 + u·0.65", "source": "hediye eğri yolu"} | — | Faz 2 |
| ease.linear | {"formula": "u", "bezier": [0, 0, 1, 1]} | Assets/Galaxy101/Scripts/Background/MenuGammaPresenter.cs, Assets/Galaxy101/Scripts/Components/GalaxySlider.cs | ad var · değer Faz 2 |
| ease.cssOutExpo | {"bezier": [0.16, 1, 0.3, 1], "source": "logo/taş girişleri, menuIn"} | — | Faz 2 |
| ease.cssPanel | {"bezier": [0.2, 1, 0.4, 1], "source": "panelIn"} | — | Faz 2 |
| ease.cssInOutStrong | {"bezier": [0.7, 0, 0.3, 1], "source": "splashOut, tilesOut"} | — | Faz 2 |
| ease.cssFloat | {"bezier": [0.45, 0, 0.55, 1], "source": "g101Float"} | — | Faz 2 |
| ease.cssPlanetMove | {"bezier": [0.6, 0, 0.2, 1], "source": "gezegen konum geçişi (1 s)"} | — | Faz 2 |
| ease.cssToggle | {"bezier": [0.3, 1.5, 0.5, 1], "source": "anahtar düğmesi topuzu (aşma)"} | — | Faz 2 |
| ease.ease | {"bezier": [0.25, 0.1, 0.25, 1]} | Assets/Galaxy101/Resources/Galaxy101/UI/Base.uss, Assets/Galaxy101/Resources/Galaxy101/UI/Panels.uss | ad var · değer Faz 2 |
| ease.ease-in-out | {"bezier": [0.42, 0, 0.58, 1]} | Assets/Galaxy101/Scripts/Background/GalaxyBackground.cs, Assets/Galaxy101/Scripts/Screens/MenuView.Main.cs | ad var · değer Faz 2 |
| ease.ease-out | {"bezier": [0, 0, 0.58, 1]} | Assets/Galaxy101/Resources/Galaxy101/UI/Base.uss, Assets/Galaxy101/Resources/Galaxy101/UI/Panels.uss | ad var · değer Faz 2 |
| ease.ease-in | {"bezier": [0.42, 0, 1, 1]} | Assets/Galaxy101/Scripts/Background/GalaxyBackground.cs, Assets/Galaxy101/Scripts/Screens/MenuView.Main.cs | ad var · değer Faz 2 |
