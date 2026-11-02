using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using Galaxy101.Table;

namespace Galaxy101
{
    /// <summary>
    /// Settings → Masa (G101-Menu masaVals, showMasa; data g101-settings.js LOOK): five folding sections — Görüş açısı,
    /// Masa teması, Istaka teması, Taş görünümü, Masa bildirimleri — over the table's own settings store (TableSettings),
    /// so the menu and the table read and write the same choices. Only Görüş açısı starts open (mOpen = { view: true }).
    /// </summary>
    public sealed class SettingsMasa
    {
        private readonly HashSet<string> open = new HashSet<string> { "view" };
        private TableSettings table, probe;
        // LOOK.vars: --f1:#2C3790; --f2:#11153F; --rim:#4a5080 (the previews' felt and rim).
        private const string F1 = "#2C3790", F2 = "#11153F", Rim = "#4a5080";
        private static readonly string[] Views = { "seat", "mid", "top", "2d" }, Tables = { "station", "wood", "holo", "andromeda" }, Racks = { "walnut", "sedef", "chrome", "carbon", "neon", "andromeda" }, Tiles = { "classic", "galaxy", "gold", "neon", "marble" };
        private static readonly string[] Notifs = { "bubbles", "giftNotif", "emojiFx", "turnHint", "chatSnd" };

        public void Build(MenuView v, VisualElement list)
        {
            // Read afresh on every build: the table may have changed its settings since (both keep them in PlayerPrefs).
            table = v.TableSettings ?? new TableSettings(v.Model); probe = probe ?? new TableSettings(null, false);
            bool en = v.Loc.Language == "en";
            var names = Names(en);
            string Cur(string[] keys, string[][] labels, string key) => labels[Math.Max(0, Array.IndexOf(keys, key))][0];
            int on = 0; foreach (var k in Notifs) if (Flag(k)) on++;
            var sections = new[]
            {
                ("view", v.L("Görüş açısı", "Camera view"), v.L("Masayı hangi açıdan göreceğin", "How you see the table"), Cur(Views, names.views, table.View)),
                ("table", v.L("Masa teması", "Table theme"), v.L("Çuha ve kenar stili", "Felt and rim style"), Cur(Tables, names.tables, table.TableSkin)),
                ("rack", v.L("Istaka teması", "Rack theme"), v.L("Istakanın malzemesi", "Material of your rack"), Cur(Racks, names.racks, table.RackSkin)),
                ("tile", v.L("Taş görünümü", "Tile style"), v.L("Taş yüzü ve rakam renkleri", "Face and number colours"), Cur(Tiles, names.tiles, table.TileSkin)),
                ("notif", v.L("Masa bildirimleri", "Table notifications"), v.L("Balonlar, ikramlar, emojiler ve ipuçları", "Bubbles, treats, emojis and hints"), en ? on + "/5 on" : on + "/5 açık"),
            };
            // display:flex column, gap 10, padding 2px 0 18px.
            var column = v.Column(); column.style.paddingTop = 2; column.style.paddingBottom = 18; list.Add(column);
            for (var i = 0; i < sections.Length; i++)
            {
                var (key, name, desc, cur) = sections[i]; bool isOpen = open.Contains(key);
                // radius 18, --sd, inset 1px rgba(255,255,255,.08) while open, overflow hidden.
                var card = v.Box(); v.Decorate(card, "sd", 18); card.style.overflow = Overflow.Hidden; card.style.flexShrink = 0; if (i > 0) card.style.marginTop = 10;
                if (isOpen) card.Add(PanelUi.Ring(v, 1, "rgba(255,255,255,.08)", 18));
                var head = PanelUi.CustomButton(v, "", () => { if (!open.Remove(key)) open.Add(key); v.Refresh(); }, "g-masa-head"); head.name = "menu.sec.toggle[" + i + "]";
                head.style.backgroundColor = Color.clear; head.style.unityTextAlign = TextAnchor.MiddleLeft;
                head.style.flexDirection = FlexDirection.Row; head.style.alignItems = Align.Center; head.style.justifyContent = Justify.FlexStart; PanelUi.Pad(head, 14, 16);
                var tile = v.Box(); tile.style.width = tile.style.height = 34; tile.style.flexShrink = 0; tile.style.alignItems = Align.Center; tile.style.justifyContent = Justify.Center; tile.style.marginRight = 14; v.Decorate(tile, "pnH", 11, 3, "pnS");
                tile.Add(PanelUi.Diamond(v, 10, "a1", 3)); head.Add(tile);
                var title = v.Text(name, 17, true); title.name = "menu.sec.name[" + i + "]"; title.style.height = 21.25f;
                var sub = v.Text(desc, 13, false, "mt"); sub.name = "menu.sec.desc[" + i + "]"; sub.style.whiteSpace = WhiteSpace.NoWrap; sub.style.overflow = Overflow.Hidden; sub.style.textOverflow = TextOverflow.Ellipsis;
                var texts = v.Column(title, sub); PanelUi.Grow(texts); texts.style.marginRight = 14; head.Add(texts);
                var pill = v.Text(cur, 13, true, "a1"); pill.name = "menu.sec.cur[" + i + "]"; pill.style.unityFont = v.Font("Montserrat-800"); pill.style.whiteSpace = WhiteSpace.NoWrap; pill.style.flexShrink = 0;
                PanelUi.Pad(pill, 5, 11); v.Decorate(pill, "dp", 9); pill.style.marginRight = 14; head.Add(pill);
                var chevron = v.Box(); chevron.style.width = chevron.style.height = 34; chevron.style.flexShrink = 0; chevron.style.alignItems = Align.Center; chevron.style.justifyContent = Justify.Center; v.Decorate(chevron, "pnH", 11, 3, "pnS");
                var arrow = v.Icon("chevron-down", 14); if (isOpen) arrow.style.rotate = new Rotate(180); chevron.Add(arrow); head.Add(chevron);
                head.RegisterCallback<PointerEnterEvent>(_ => head.style.backgroundColor = v.C("rgba(255,255,255,.04)")); head.RegisterCallback<PointerLeaveEvent>(_ => head.style.backgroundColor = Color.clear);
                card.Add(head);
                if (isOpen)
                {
                    // padding 2px 14px 14px
                    var body = v.Box(); body.style.paddingTop = 2; body.style.paddingLeft = body.style.paddingRight = body.style.paddingBottom = 14; card.Add(body);
                    switch (key)
                    {
                        case "view": ViewCards(v, body, names.views); break;
                        case "table": TableCards(v, body, names.tables); break;
                        case "rack": RackCards(v, body, names.racks); break;
                        case "tile": TileCards(v, body, names.tiles); break;
                        default: NotifRows(v, body, en); break;
                    }
                }
                column.Add(card);
            }
        }

        private bool Flag(string key) { try { return table.Bool(key); } catch (Exception) { return true; } } // st[k] !== false

        /// <summary>LOOK.tr / LOOK.en names and descriptions.</summary>
        private static (string[][] views, string[][] tables, string[][] racks, string[][] tiles) Names(bool en) => en
            ? (new[] { new[] { "Player view", "Low angle, rack up front" }, new[] { "Mid view", "Medium angle, rack and table balanced" }, new[] { "Top view", "High angle, whole table visible" }, new[] { "2D table", "Flat top-down view, large rack" } },
               new[] { new[] { "Station", "Metal rim, light strip" }, new[] { "Walnut lounge", "Polished walnut, brass trim" }, new[] { "Hologram", "Glass rim, glowing grid" }, new[] { "Andromeda", "Spiral galaxy, iridescent rim" } },
               new[] { new[] { "Classic walnut", "Solid walnut, maple inlay" }, new[] { "Mother-of-pearl", "Rosewood, pearl inlay" }, new[] { "Aluminium", "Anodised metal, smoked glass" }, new[] { "Carbon", "Carbon fibre, glowing seams" }, new[] { "Neon glass", "Smoked glass, neon tubes" }, new[] { "Andromeda", "Galaxy panel, iridescent" } },
               new[] { new[] { "Classic", "Ivory, classic colours" }, new[] { "Galaxy", "Nebula texture, glowing numbers" }, new[] { "Gold", "Black onyx, gold frame" }, new[] { "Neon", "Smoked body, glowing edge" }, new[] { "Marble", "White marble, gold veins" } })
            : (new[] { new[] { "Oyuncu gözü", "Alçak açı, ıstaka önde" }, new[] { "Yarı yüksek", "Orta açı, ıstaka ve masa dengeli" }, new[] { "Kuşbakışı", "Yüksek açı, tüm masa görünür" }, new[] { "2D masa", "Tepeden düz görünüm, büyük ıstaka" } },
               new[] { new[] { "İstasyon", "Metal kenar, ışık şeridi" }, new[] { "Ceviz salon", "Cilalı ceviz, pirinç detay" }, new[] { "Hologram", "Cam kenar, ışıklı ızgara" }, new[] { "Andromeda", "Sarmal galaksi, yanardöner kenar" } },
               new[] { new[] { "Klasik ceviz", "Masif ceviz, akçaağaç kakma" }, new[] { "Sedef kakma", "Gül ağacı, sedef desen" }, new[] { "Alüminyum", "Eloksal metal, füme cam" }, new[] { "Karbon", "Karbon fiber, ışıklı dikiş" }, new[] { "Neon cam", "Füme cam, neon tüpler" }, new[] { "Andromeda", "Galaksi panel, yanardöner" } },
               new[] { new[] { "Klasik", "Fildişi, klasik renkler" }, new[] { "Galaksi", "Nebula doku, parlayan rakam" }, new[] { "Altın", "Siyah oniks, altın çerçeve" }, new[] { "Neon", "Füme gövde, ışıklı kenar" }, new[] { "Mermer", "Beyaz mermer, altın damar" } });

        /// <summary>
        /// A choice card: background --pnH when picked (else --dp), "inset 0 0 0 2px var(--a1), 0 0 18px var(--gl)" when picked,
        /// hover --pnH. A value the table cannot take yet stays inert.
        /// </summary>
        private Button Card(MenuView v, string id, bool picked, string setting, string value)
        {
            bool usable = true; try { probe.Set(setting, value); } catch (ArgumentException) { usable = false; }
            var card = PanelUi.CustomButton(v, "", () => { if (!usable) return; table.Set(setting, value); v.Refresh(); }, "g-masa-card"); card.name = id;
            card.style.flexBasis = 0; card.style.flexGrow = 1; card.style.minWidth = 0; card.style.alignItems = Align.Center; PanelUi.Pad(card, 10, 10);
            v.Decorate(card, picked ? "pnH" : "dp", 14); if (picked) { card.Add(PanelUi.Ring(v, 2, "a1", 14)); GalaxyFilters.Shadow(card, 0, 0, 9, v.C("gl")); }
            else { card.RegisterCallback<PointerEnterEvent>(_ => v.Decorate(card, "pnH", 14)); card.RegisterCallback<PointerLeaveEvent>(_ => v.Decorate(card, "dp", 14)); }
            if (!usable) card.SetEnabled(false);
            return card;
        }

        private static VisualElement Grid(MenuView v, VisualElement parent, IList<VisualElement> cells, int columns, int gap)
        {
            VisualElement row = null;
            for (var i = 0; i < cells.Count; i++)
            {
                if (i % columns == 0) { row = v.Row(); row.style.alignItems = Align.Stretch; if (i > 0) row.style.marginTop = gap; parent.Add(row); }
                if (i % columns < columns - 1) cells[i].style.marginRight = gap; row.Add(cells[i]);
            }
            for (var k = cells.Count % columns; k > 0 && k < columns; k++) { var pad = v.Box(); pad.style.flexBasis = 0; pad.style.flexGrow = 1; if (k < columns - 1) pad.style.marginRight = gap; row.Add(pad); }
            return row;
        }

        private void ViewCards(MenuView v, VisualElement body, string[][] names)
        {
            var cells = new List<VisualElement>();
            for (var i = 0; i < Views.Length; i++)
            {
                var card = Card(v, "menu.v.pick[" + i + "]", table.View == Views[i], "view", Views[i]);
                // 96×62 preview, radius 11, --dp, the view's sketch; gap 8; name 700 15px, desc 700 12px/1.3 centred.
                var frame = v.Box(); frame.style.width = 96; frame.style.height = 62; frame.style.overflow = Overflow.Hidden; v.Decorate(frame, "dp", 11);
                frame.Add(new ViewSketch(Views[i], v.C("a1"), v.C("var(--tf1)"))); card.Add(frame);
                var name = v.Text(names[i][0], 15, true); name.name = "menu.v.name[" + i + "]"; name.style.marginTop = 8; name.style.whiteSpace = WhiteSpace.NoWrap;
                var desc = PanelUi.Wrap(v, names[i][1], 12); desc.name = "menu.v.desc[" + i + "]"; desc.style.unityTextAlign = TextAnchor.UpperCenter; MenuView.CssParagraph(desc, 12 * 1.3f, false);
                card.Add(name); card.Add(desc); cells.Add(card);
            }
            Grid(v, body, cells, 3, 10);
        }

        private void TableCards(MenuView v, VisualElement body, string[][] names)
        {
            // TABLE_PREV / TABLE_PREV_A: rim, line (inset ring of the felt), glow; LOOK.felt(k).
            var rims = new Dictionary<string, (string rim, string line, float lineWidth, string ring, float ringWidth, string glow, float ledge)>
            {
                ["station"] = ("linear-gradient(160deg,#6a7396,#2c3352)", "a1", 2, "a1", 2, "gl", 0),
                ["wood"] = ("linear-gradient(160deg,#7A4528,#3A2014)", "#D9B15A", 2, null, 0, null, 3),
                ["holo"] = ("linear-gradient(160deg,rgba(120,160,255,.35),rgba(20,30,60,.6))", "a1", 1, "a1", 1, "gl", 0),
                ["andromeda"] = ("linear-gradient(135deg,#1b1f4a,#6a4fc0 30%,#2f7ad6 55%,#c56fd0 80%,#1b1f4a)", "#B79CFF", 1, null, 0, "rgba(143,180,255,.55)", 0),
            };
            var cells = new List<VisualElement>();
            for (var i = 0; i < Tables.Length; i++)
            {
                var key = Tables[i]; var look = rims[key];
                var card = Card(v, "menu.c.pick[" + i + "]", table.TableSkin == key, "tableSkin", key);
                var well = v.Box(); well.style.alignSelf = Align.Stretch; well.style.height = 66; well.style.alignItems = Align.Center; well.style.justifyContent = Justify.Center; v.Decorate(well, "dp", 11);
                var rim = v.Box(); rim.style.width = 86; rim.style.height = 52; PanelUi.Pad(rim, 5, 5); v.Decorate(rim, look.rim, 14, (int)look.ledge, "#22120a");
                if (look.ring != null) { var outer = PanelUi.Ring(v, look.ringWidth, look.ring, 14 + look.ringWidth); outer.style.left = outer.style.right = outer.style.top = outer.style.bottom = -look.ringWidth; rim.Add(outer); }
                if (look.glow != null) GalaxyFilters.Shadow(rim, 0, 0, key == "andromeda" ? 11 : key == "holo" ? 12 : 9, v.C(look.glow));
                var felt = v.Box(); felt.style.flexGrow = 1; felt.style.overflow = Overflow.Hidden; Felt(v, felt, key); felt.Add(PanelUi.Ring(v, look.lineWidth, look.line, 10)); rim.Add(felt); well.Add(rim); card.Add(well);
                var name = v.Text(names[i][0], 14, true); name.name = "menu.c.name[" + i + "]"; name.style.marginTop = 8; name.style.whiteSpace = WhiteSpace.NoWrap; card.Add(name);
                cells.Add(card);
            }
            Grid(v, body, cells, 4, 10);
        }

        /// <summary>LOOK.felt(k): stacked gradient layers (the hologram's 9 px grid lines are left out, UNITY_FARKLARI).</summary>
        private static void Felt(MenuView v, VisualElement felt, string key)
        {
            string[] layers = key == "andromeda"
                ? new[] { "radial-gradient(#141a46,#04050f)", "radial-gradient(ellipse 60% 30% at 50% 50%,rgba(183,156,255,.35),transparent 70%)", "radial-gradient(ellipse 34% 14% at 50% 50%,#FFF6E0,#F5C98A 30%,rgba(143,176,255,.75) 62%,transparent 80%)" }
                : key == "holo" ? new[] { "radial-gradient(" + F2 + ",#03050d)" }
                : key == "wood" ? new[] { "radial-gradient(" + F1 + "," + F2 + " 120%)" } : new[] { "radial-gradient(" + F1 + "," + F2 + ")" };
            v.Decorate(felt, layers[0], 10);
            for (var i = 1; i < layers.Length; i++) { var layer = v.Box(); layer.pickingMode = PickingMode.Ignore; layer.style.position = Position.Absolute; layer.style.left = layer.style.right = layer.style.top = layer.style.bottom = 0; v.Decorate(layer, layers[i], 10); felt.Add(layer); }
        }

        private void RackCards(MenuView v, VisualElement body, string[][] names)
        {
            // RACK_PREV / RACK_PREV_A: back, ledge, glow (inset 0 2px 0 top line; neon / andromeda glow outside).
            var looks = new Dictionary<string, (string back, string ledge, string topLine, string glow)>
            {
                ["walnut"] = ("linear-gradient(#7a4527,#4a2614)", "#8a5230", "#EBD5A8", null),
                ["sedef"] = ("linear-gradient(#4A1E12,#2a0f08)", "#E4BE62", "#F6F0E6", null),
                ["chrome"] = ("linear-gradient(#c3c8d2,#7d8494)", "#e4e7ee", "a1", null),
                ["carbon"] = ("#14161d", "#14161d", "a1", null), // repeating-linear-gradient(45deg,#14161d 0 4px,#2c2f38 4px 8px): stripes left out
                ["neon"] = ("linear-gradient(#0b0c14,#05060c)", "a1", "#fff", "gl"),
                ["andromeda"] = ("linear-gradient(90deg,#0c0f2e,#3a2a6e 35%,#FFF1D0 50%,#3a6fd6 65%,#0c0f2e)", "#B79CFF", "#CFE0FF", "rgba(143,180,255,.6)"),
            };
            var cells = new List<VisualElement>();
            string[] inks = { "#D7263D", "#1F5FBF", "#E39B12" };
            for (var i = 0; i < Racks.Length; i++)
            {
                var key = Racks[i]; var look = looks[key];
                var card = Card(v, "menu.c.pick[" + i + "]", table.RackSkin == key, "rackSkin", key); card.style.flexDirection = FlexDirection.Row;
                // padding 5px 6px 4px, radius 7; the tiles row: gap 3, padding-bottom 3, a 3px ledge line.
                var rack = v.Box(); rack.style.flexShrink = 0; rack.style.paddingTop = 5; rack.style.paddingLeft = rack.style.paddingRight = 6; rack.style.paddingBottom = 4; v.Decorate(rack, look.back, 7);
                var top = v.Box(); top.pickingMode = PickingMode.Ignore; top.style.position = Position.Absolute; top.style.left = top.style.right = top.style.top = 0; top.style.height = 2; top.style.backgroundColor = v.C(look.topLine); top.style.borderTopLeftRadius = top.style.borderTopRightRadius = 7; rack.Add(top);
                if (look.glow != null) GalaxyFilters.Shadow(rack, 0, 0, 7, v.C(look.glow));
                var row = v.Row(); row.style.paddingBottom = 3; row.style.borderBottomWidth = 3; row.style.borderBottomColor = v.C(look.ledge);
                for (var t = 0; t < 3; t++)
                {
                    var face = v.Box(); face.style.width = 12; face.style.height = 17; face.style.alignItems = Align.Center; face.style.justifyContent = Justify.Center; if (t < 2) face.style.marginRight = 3; v.Decorate(face, "linear-gradient(var(--tf1),var(--tf2))", 3);
                    var digit = v.Text((7 + t).ToString(), 9, true, inks[t]); digit.style.unityFont = v.Font("Montserrat-800"); digit.style.unityTextAlign = TextAnchor.MiddleCenter; face.Add(digit); row.Add(face);
                }
                rack.Add(row); card.Add(rack);
                var name = v.Text(names[i][0], 14, true); name.name = "menu.c.name[" + i + "]"; name.style.whiteSpace = WhiteSpace.NoWrap;
                var desc = PanelUi.Wrap(v, names[i][1], 11); desc.name = "menu.c.desc[" + i + "]"; desc.style.fontSize = 11.5f; MenuView.CssParagraph(desc, 11.5f * 1.3f, false);
                var texts = v.Column(name, desc); texts.style.minWidth = 0; texts.style.flexShrink = 1; texts.style.marginLeft = 12; card.Add(texts);
                cells.Add(card);
            }
            Grid(v, body, cells, 3, 10);
        }

        private void TileCards(MenuView v, VisualElement body, string[][] names)
        {
            // TILE_PREV: face, edge (inset 0 -3px 0 band · 0 2px 0 ledge · glow), c1, c2, jk; the drop-shadow text filter is left out.
            var looks = new Dictionary<string, (string face, string band, string ledge, string glow, string c1, string c2, string jk)>
            {
                ["classic"] = ("linear-gradient(#FFFDF6,#F1E9D6)", "#E3D7BC", "#B9AB8C", null, "#D7263D", "#1F5FBF", "#127A4A"),
                ["galaxy"] = ("linear-gradient(#3B2F7A,#1C1648)", "#120E30", "#0A0820", "rgba(140,110,255,.5)", "#FF3D5A", "#FFC21A", "#FFD964"),
                ["gold"] = ("linear-gradient(#1d1d24,#050507)", "#D9B25A", "#8A6A20", "rgba(255,201,74,.4)", "#FF3348", "#3D95FF", "#E9C46A"),
                ["neon"] = ("linear-gradient(#14202E,#0A1018)", "#06090E", "#000", "rgba(61,245,224,.5)", "#2E9BFF", "#FFD21F", "#3DF5E0"),
                ["marble"] = ("linear-gradient(#FFFFFF,#E4E6EC)", "#C9CCD6", "#9499A8", null, "#2B3A55", "#C21F36", "#2B3A55"),
            };
            var cells = new List<VisualElement>();
            for (var i = 0; i < Tiles.Length; i++)
            {
                var key = Tiles[i]; var look = looks[key];
                var card = Card(v, "menu.c.pick[" + i + "]", table.TileSkin == key, "tileSkin", key); card.style.paddingLeft = card.style.paddingRight = 6; card.style.paddingTop = card.style.paddingBottom = 8;
                var well = v.Row(); well.style.alignSelf = Align.Stretch; well.style.height = 48; well.style.alignItems = Align.Center; well.style.justifyContent = Justify.Center; v.Decorate(well, "dp", 10);
                string[] glyphs = { "7", "11", "★" }, inks = { look.c1, look.c2, look.jk };
                for (var t = 0; t < 3; t++)
                {
                    var face = v.Box(); face.style.width = 22; face.style.height = 32; face.style.alignItems = Align.Center; face.style.justifyContent = Justify.Center; if (t < 2) face.style.marginRight = 4;
                    v.Decorate(face, look.face, 5, 2, look.ledge); if (look.glow != null) GalaxyFilters.Shadow(face, 0, 0, 5, v.C(look.glow));
                    var band = v.Box(); band.pickingMode = PickingMode.Ignore; band.style.position = Position.Absolute; band.style.left = band.style.right = band.style.bottom = 0; band.style.height = 3; band.style.backgroundColor = v.C(look.band); band.style.borderBottomLeftRadius = band.style.borderBottomRightRadius = 5; face.Add(band);
                    var digit = v.Text(glyphs[t], t == 2 ? 13 : 14, true, inks[t]); digit.style.unityFont = v.Font("Montserrat-800"); digit.style.unityTextAlign = TextAnchor.MiddleCenter; face.Add(digit); well.Add(face);
                }
                card.Add(well);
                var name = v.Text(names[i][0], 13, true); name.name = "menu.c.name[" + i + "]"; name.style.marginTop = 6; name.style.whiteSpace = WhiteSpace.NoWrap; card.Add(name);
                cells.Add(card);
            }
            Grid(v, body, cells, 5, 8);
        }

        private void NotifRows(MenuView v, VisualElement body, bool en)
        {
            string[][] rows =
            {
                new[] { "Sohbet balonları", "Mesajlar oyuncuların yanında balon olarak görünsün", "Chat bubbles", "Messages appear as bubbles next to players" },
                new[] { "İkram bildirimleri", "Sana ikram geldiğinde sağ üstte bildirim çıksın", "Treat notifications", "Show a notice when someone sends you a treat" },
                new[] { "Emoji efektleri", "Gönderilen emojiler masada uçsun", "Emoji effects", "Sent emojis fly across the table" },
                new[] { "Sıra ipuçları", "Sıra sendeyken ne yapacağın üstte yazsın", "Turn hints", "Show what to do when it is your turn" },
                new[] { "Sohbet sesi", "Yeni mesajda kısa ses çalsın", "Chat sound", "Play a short sound on new messages" },
            };
            for (var i = 0; i < Notifs.Length; i++)
            {
                var key = Notifs[i]; bool on = Flag(key);
                // padding 9px 4px, 1px rgba(255,255,255,.06) rule; 700 16px/1.25 over 700 12.5px.
                var name = v.Text(rows[i][en ? 2 : 0], 16, true); name.name = "menu.t.name[" + i + "]"; name.style.height = 20;
                var desc = PanelUi.Wrap(v, rows[i][en ? 3 : 1], 13); desc.name = "menu.t.desc[" + i + "]"; desc.style.fontSize = 12.5f;
                var labels = v.Column(name, desc); PanelUi.Grow(labels); labels.style.marginRight = 16;
                var flip = PanelUi.CustomButton(v, "", () => { table.Set(key, !on); v.Refresh(); }, "g-toggle"); flip.name = "menu.t.flip[" + i + "]"; flip.EnableInClassList("on", on);
                var knob = v.Box("g-toggle-knob"); GalaxyFilters.Shadow(knob, 0, 2, 2, new Color(0, 0, 0, .35f)); flip.Add(knob);
                var row = v.Row(labels, flip); PanelUi.Pad(row, 9, 4); row.style.borderBottomWidth = 1; row.style.borderBottomColor = v.C("rgba(255,255,255,.06)"); body.Add(row);
            }
        }

        /// <summary>The 96×62 view sketches of the Masa tab (G101-Menu showMasa, isSeat / isMid / isTop SVGs), drawn as vectors.</summary>
        private sealed class ViewSketch : VisualElement
        {
            private readonly string view; private readonly Color accent, tileFace;
            private static readonly Color Felt = Hex(F1), RimColor = Hex(Rim);
            public ViewSketch(string view, Color accent, Color tileFace)
            {
                this.view = view; this.accent = accent; this.tileFace = tileFace; pickingMode = PickingMode.Ignore;
                style.width = 96; style.height = 62; generateVisualContent += Draw;
            }
            private static Color Hex(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }
            private void Draw(MeshGenerationContext context)
            {
                var p = context.painter2D;
                void Poly(Color fill, Color? stroke, params Vector2[] points)
                {
                    p.BeginPath(); p.MoveTo(points[0]); for (var i = 1; i < points.Length; i++) p.LineTo(points[i]); p.ClosePath();
                    p.fillColor = fill; p.Fill(); if (stroke.HasValue) { p.strokeColor = stroke.Value; p.lineWidth = 1.5f; p.lineJoin = LineJoin.Miter; p.Stroke(); }
                }
                void Rect(float x, float y, float w, float h, float r, Color fill, Color? stroke = null, float alpha = 1)
                {
                    p.BeginPath(); p.MoveTo(new Vector2(x + r, y)); p.ArcTo(new Vector2(x + w, y), new Vector2(x + w, y + h), r); p.ArcTo(new Vector2(x + w, y + h), new Vector2(x, y + h), r);
                    p.ArcTo(new Vector2(x, y + h), new Vector2(x, y), r); p.ArcTo(new Vector2(x, y), new Vector2(x + w, y), r); p.ClosePath();
                    fill.a *= alpha; p.fillColor = fill; p.Fill(); if (stroke.HasValue) { p.strokeColor = stroke.Value; p.lineWidth = 1.5f; p.Stroke(); }
                }
                if (view == "top")
                {
                    Rect(24, 5, 48, 48, 8, Felt, accent); Rect(30, 54, 36, 6, 2, RimColor); Rect(15, 15, 6, 28, 2, RimColor); Rect(75, 15, 6, 28, 2, RimColor); Rect(30, 0, 36, 4, 2, RimColor); Rect(42, 23, 12, 12, 2, tileFace);
                    return;
                }
                if (view == "2d")
                {
                    Rect(22, 3, 52, 34, 7, Felt, accent); Rect(40, 15, 16, 10, 2, RimColor, null, .6f); Rect(12, 42, 72, 17, 3, RimColor);
                    for (int k = 0; k < 7; k++) Rect(15 + k * 10, 44, k == 6 ? 6 : 8, 13, 1.5f, tileFace);
                    return;
                }
                bool mid = view == "mid";
                if (mid) Poly(Felt, accent, new Vector2(22, 9), new Vector2(74, 9), new Vector2(86, 47), new Vector2(10, 47));
                else Poly(Felt, accent, new Vector2(26, 16), new Vector2(70, 16), new Vector2(86, 46), new Vector2(10, 46));
                Rect(16, mid ? 47 : 46, 64, mid ? 9 : 10, 3, RimColor);
                for (var k = 0; k < 7; k++) Rect((mid ? 21 : 20) + 8 * k, mid ? 41 : 40, 6, 8, 1.5f, tileFace);
                if (mid) Rect(40, 22, 16, 10, 2, RimColor, null, .6f);
            }
        }
    }
}
