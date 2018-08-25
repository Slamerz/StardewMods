using System.Reflection;
using System.Xml.Serialization;

using Microsoft.Xna.Framework;

using StardewValley;
using StardewValley.Objects;

namespace Entoarox.AdvancedLocationLoader.Locations
{
    [XmlType("ALLDecoratableLocation")]
    public class DecoratableLocation : StardewValley.Locations.DecoratableLocation
    {
        private class FakeWallpaper : Wallpaper
        {
            public FakeWallpaper(Wallpaper item)
            {
                this.isFloor.Value = item.isFloor.Value;
                this.ParentSheetIndex = item.ParentSheetIndex;
                this.name = this.isFloor.Value ? "Flooring" : "Wallpaper";
                this.Price = 100;
            }
            public override bool canBePlacedHere(GameLocation l, Vector2 tile)
            {
                return false;
            }
            public override bool placementAction(GameLocation location, int x, int y, StardewValley.Farmer who = null)
            {
                return false;
            }
            public Wallpaper Restore()
            {
                return new Wallpaper(this.ParentSheetIndex, this.isFloor.Value);
            }
        }
        private static MethodInfo reset = typeof(GameLocation).GetMethod("resetForPlayerEntry", BindingFlags.Instance | BindingFlags.Public);
        public DecoratableLocation()
        {

        }
        public DecoratableLocation(xTile.Map map, string name) : base(map.ToString(),name)
        {

        }
        public new void resetForPlayerEntry()
        {
            reset.Invoke(this, null);
            foreach (Furniture furniture in this.furniture)
                furniture.resetOnPlayerEntry(this);
            for (int c=0;c<Game1.player.Items.Count;c++)
            {
                Item i = Game1.player.Items[c];
                if (Game1.player.Items[c] is Wallpaper)
                    Game1.player.Items[c] = new FakeWallpaper((Wallpaper)Game1.player.Items[c]);
            }
        }
        public override void cleanupBeforePlayerExit()
        {
            base.cleanupBeforePlayerExit();
            for (int c = 0; c < Game1.player.Items.Count; c++)
            {
                Item i = Game1.player.Items[c];
                if (Game1.player.Items[c] is FakeWallpaper)
                    Game1.player.Items[c] = ((FakeWallpaper)Game1.player.Items[c]).Restore();
            }
        }
        public override void setFloor(int which, int whichRoom = -1, bool persist = false)
        {
            return;
        }
        public new void setWallpaper(int which, int whichRoom = -1, bool persist = false)
        {
            return;
        }
    }
}
