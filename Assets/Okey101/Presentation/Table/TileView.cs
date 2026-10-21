using Okey101.Core.Model;
using UnityEngine;

namespace Okey101.Presentation
{
    public enum TileRole { Hand, Stock, Indicator, Discard, Meld }

    public sealed class TileView : MonoBehaviour
    {
        [SerializeField] private MeshRenderer body;
        [SerializeField] private TextMesh number;
        [SerializeField] private TextMesh badge;
        [SerializeField] private GameObject selection;
        [SerializeField] private MeshRenderer faceRenderer;
        [SerializeField] private Collider hitTarget;
        private MaterialPropertyBlock properties;
        private MaterialPropertyBlock faceProperties;
        public TileId Id { get; private set; }
        public TileRole Role { get; private set; }
        public int? MeldId { get; private set; }

        public static TileView CreatePrimitive(Transform parent, Material material, Font font)
        {
            var root = new GameObject("Tile"); root.transform.SetParent(parent, false);
            var view = root.AddComponent<TileView>();
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Ivory body"; body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(.72f, .16f, .92f);
            view.body = body.GetComponent<MeshRenderer>(); view.body.sharedMaterial = material;
            view.hitTarget = body.GetComponent<Collider>();
            view.number = Label(root.transform, font, "Number", new Vector3(0, .18f, 0), 96, .06f);
            view.badge = Label(root.transform, font, "Color mark", new Vector3(-.23f, .18f, .30f), 38, .045f);
            view.selection = GameObject.CreatePrimitive(PrimitiveType.Cube);
            view.selection.name = "Selection"; view.selection.transform.SetParent(root.transform, false);
            view.selection.transform.localPosition = new Vector3(0, -.03f, 0);
            view.selection.transform.localScale = new Vector3(.79f, .13f, .99f);
            view.selection.GetComponent<MeshRenderer>().sharedMaterial = material;
            var selectionProperties = new MaterialPropertyBlock();
            selectionProperties.SetColor("_BaseColor", new Color(.94f, .72f, .30f));
            view.selection.GetComponent<MeshRenderer>().SetPropertyBlock(selectionProperties);
            view.selection.GetComponent<Collider>().enabled = false;
            view.selection.SetActive(false);
            return view;
        }

        public static TileView CreateArt(GameObject model, Material material, Material atlas, Font font)
        {
            var view = CreatePrimitive(null, material, font);
            Object.DestroyImmediate(view.body.gameObject);
            var mesh = Object.Instantiate(model, view.transform); mesh.name = "Bevelled ivory body";
            view.body = mesh.GetComponentInChildren<MeshRenderer>();
            foreach (var renderer in mesh.GetComponentsInChildren<MeshRenderer>()) renderer.sharedMaterial = material;
            var collider = view.gameObject.AddComponent<BoxCollider>(); collider.size = new Vector3(.72f, .16f, .92f);
            view.hitTarget = collider;
            var face = GameObject.CreatePrimitive(PrimitiveType.Quad); face.name = "Shared atlas face";
            face.transform.SetParent(view.transform, false); face.transform.localPosition = new Vector3(0, .25f, 0);
            face.transform.localScale = new Vector3(.66f, .92f, 1);
            Object.DestroyImmediate(face.GetComponent<Collider>());
            view.faceRenderer = face.GetComponent<MeshRenderer>(); view.faceRenderer.sharedMaterial = atlas;
            view.faceRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            view.faceRenderer.receiveShadows = false;
            view.number.GetComponent<Renderer>().enabled = false;
            return view;
        }

        private static TextMesh Label(Transform parent, Font font, string name, Vector3 position, int size, float scale)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = position;
            var text = go.AddComponent<TextMesh>(); text.font = font; text.fontSize = size; text.characterSize = scale;
            text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            return text;
        }

        public void Show(TileId id, TileFace indicator, TileRole role, Camera camera)
        {
            Id = id; Role = role; MeldId = null; name = role + " " + id.Value;
            var face = TileCatalog.Create()[id.Value].PrintedFace ?? TileCatalog.JokerFace(indicator);
            var color = FaceColor(face.Color);
            number.text = face.Number.ToString(); number.color = color;
            badge.text = id.Value >= 104 ? "S" : TileCatalog.IsRealJoker(id, indicator) ? "O" : ColorMark(face.Color);
            badge.color = color;
            if (faceRenderer != null) { SetFace(face); if (id.Value < 104 && !TileCatalog.IsRealJoker(id, indicator)) badge.text = ""; }
            Orient(camera); Tint(new Color(.96f, .94f, .85f)); SetSelected(false);
        }

        public void ShowBack(Camera camera)
        {
            Role = TileRole.Stock; name = "Kapalı deste";
            number.text = "101"; number.color = new Color(.85f, .73f, .43f); badge.text = "";
            if (faceRenderer != null) SetAtlas(15, 0);
            Orient(camera); Tint(new Color(.035f, .19f, .17f)); SetSelected(false);
        }

        public void ShowPlaced(PlacedTile tile, int meldId, TileFace indicator, Camera camera)
        {
            Show(tile.Id, indicator, TileRole.Meld, camera); MeldId = meldId;
            number.text = tile.RepresentedFace.Number.ToString(); number.color = FaceColor(tile.RepresentedFace.Color);
            badge.color = number.color;
            if (faceRenderer != null) SetFace(tile.RepresentedFace);
        }

        private void Orient(Camera camera)
        {
            number.transform.rotation = camera.transform.rotation;
            badge.transform.rotation = camera.transform.rotation;
            if (faceRenderer != null) faceRenderer.transform.rotation = camera.transform.rotation;
        }
        private void SetFace(TileFace face) { SetAtlas(face.Number - 1, (int)face.Color); }
        private void SetAtlas(int column, int row)
        {
            if (faceProperties == null) faceProperties = new MaterialPropertyBlock();
            faceProperties.SetVector("_BaseMap_ST", new Vector4(1f / 16, 1f / 4, column / 16f, (3 - row) / 4f));
            faceProperties.SetColor("_BaseColor", Color.white); faceRenderer.SetPropertyBlock(faceProperties);
        }
        public float NumberPixelHeight(Camera camera)
        {
            if (faceRenderer == null)
            {
                var bounds = number.GetComponent<MeshRenderer>().localBounds;
                return camera.WorldToScreenPoint(number.transform.TransformPoint(bounds.center + Vector3.up * bounds.extents.y)).y
                    - camera.WorldToScreenPoint(number.transform.TransformPoint(bounds.center - Vector3.up * bounds.extents.y)).y;
            }
            // Original atlas numerals occupy at least 152 of each 256-pixel cell's rows.
            var top = faceRenderer.transform.TransformPoint(new Vector3(0, .34f, 0));
            var bottom = faceRenderer.transform.TransformPoint(new Vector3(0, -.254f, 0));
            return camera.WorldToScreenPoint(top).y - camera.WorldToScreenPoint(bottom).y;
        }
        private void Tint(Color color)
        {
            if (properties == null) properties = new MaterialPropertyBlock();
            properties.SetColor("_BaseColor", color); body.SetPropertyBlock(properties);
        }
        public void SetSelected(bool value)
        {
            // Property blocks are not serialized into prefab assets.
            if (value)
            {
                var highlight = new MaterialPropertyBlock();
                highlight.SetColor("_BaseColor", new Color(.94f, .72f, .30f));
                selection.GetComponent<MeshRenderer>().SetPropertyBlock(highlight);
            }
            selection.SetActive(value);
        }
        public void SetInteractive(bool value) { hitTarget.enabled = value; }
        public void SetReserved(bool value)
        { Tint(value ? new Color(.42f, .49f, .42f) : new Color(.96f, .94f, .85f)); SetInteractive(!value); }
        public static Color FaceColor(TileColor color)
        {
            switch (color)
            {
                case TileColor.Red: return new Color(.65f, .08f, .07f);
                case TileColor.Yellow: return new Color(.58f, .33f, .015f);
                case TileColor.Blue: return new Color(.04f, .23f, .63f);
                default: return new Color(.06f, .09f, .09f);
            }
        }
        private static string ColorMark(TileColor color)
            => color == TileColor.Red ? "K" : color == TileColor.Yellow ? "S" : color == TileColor.Blue ? "M" : "N";
    }
}
