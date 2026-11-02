using System;
using UnityEngine;

namespace Galaxy101
{
    /// <summary>Loads original cursor SVGs rasterized with the selected theme's two accent colors.</summary>
    public static class ThemedCursor
    {
        private static readonly string[] names={"default","pointer","grab","grabbing","drop","text","no","wait","progress","ew","ns"};
        private static readonly PlanetDefinition[] planets={PlanetDefinition.For("saturn"),PlanetDefinition.For("mars"),PlanetDefinition.For("neptune"),PlanetDefinition.For("jupiter"),PlanetDefinition.For("venus")};
        // Five known palettes plus the base fallback. Resource-owned textures need no runtime copies or disposal.
        private static readonly Texture2D[,] textures=new Texture2D[6,names.Length];

        public static Texture2D Load(string cursorName,string planet)
        {
            int cursor=Array.IndexOf(names,cursorName);
            if(cursor<0)return null;
            int palette=planets.Length;
            for(int i=0;i<planets.Length;i++)
                if(string.Equals(planet,planets[i].Resource,StringComparison.OrdinalIgnoreCase)||string.Equals(planet,planets[i].Name,StringComparison.OrdinalIgnoreCase))
                {palette=i;break;}
            var texture=textures[palette,cursor];
            if(texture!=null)return texture;
            if(palette<planets.Length)
                texture=Resources.Load<Texture2D>("Galaxy101/Cursors/Themes/"+planets[palette].Resource+"/"+cursorName+"@2x");
            if(texture==null)texture=Resources.Load<Texture2D>("Galaxy101/Cursors/"+cursorName+"@2x")??Resources.Load<Texture2D>("Galaxy101/Cursors/"+cursorName);
            textures[palette,cursor]=texture;
            return texture;
        }
    }
}
