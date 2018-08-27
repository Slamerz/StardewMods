using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

using StardewModdingAPI;
using StardewModdingAPI.Events;

using StardewValley;
using xTile;
using SFarmer = StardewValley.Farmer;

using xTile.Tiles;

namespace Entoarox.DynamicDungeons
{
    public class DynamicDungeonsMod : Mod
    {
        #region fields
        internal static IMonitor SMonitor;
        internal static IModHelper SHelper;
        internal static DynamicDungeon Location;
        private (SFarmer player, string action, string[] arguments, Vector2 position)? ActionInfo;
        private BookMenu InfoBook;
        #endregion
        #region mod
        public override void Entry(IModHelper helper)
        {
            SMonitor = this.Monitor;
            SHelper = this.Helper;
            GameEvents.UpdateTick += this.GameEvents_UpdateTick;
            helper.ConsoleCommands.Add("dd_fromseed", "dd_fromseed <seed> | Generate a dungeon from a specific seed", this.Command_Fromseed);
            this.InfoBook = new BookMenu(new List<Page>() {
                new TitlePage(helper.Translation.Get("Book_Title"),helper.Translation.Get("Book_Subtitle"),helper.Translation.Get("Book_Introduction")),
                new TextPage(helper.Translation.Get("Book_Page1")),
                new TextPage(helper.Translation.Get("Book_Page2")),
                new PaymentPage(),
                new ImagePage(helper.Content.Load<Texture2D>("content/book/doodle1.png"),Game1.textColor,true),
                new TitlePage("DickBut", "Dickius Buttius Maximus", "The Dickius Buttius Maximus, better know as the DickBut is a mighty creature of great majesty."),
                new ImagePage(helper.Content.Load<Texture2D>("content/book/doodle2.png"),Game1.textColor,true),
                new TitlePage("The Lie", "Absolutum Lie-um", "Please turn the page somewhere else, there is nothing to see here."),
                new ImagePage(helper.Content.Load<Texture2D>("content/book/doodle3.png"),Game1.textColor,true),
                new TitlePage("PufferChick", "Pufferium Chickate", "Adorable abomination, dont you just want to cuddle it?"),
            });
        }
        public override object GetApi()
        {
            return new DynamicDungeonsAPI();
        }
        #endregion
        #region methods
        internal static void GenerateDungeon(double difficulty, int? seed = null)
        {
            if (Location != null)
                Game1.locations.Remove(Location);
            var watch = new Stopwatch();
            SMonitor.Log("Generating dungeon...", LogLevel.Alert);
            watch.Start();
            Location = new DynamicDungeon(difficulty, seed);
            watch.Stop();
            SMonitor.Log("Generation completed in ["+watch.ElapsedMilliseconds+"] miliseconds", LogLevel.Alert);
            Game1.locations.Add(Location);
        }
        internal void ResolveAction()
        {
            string action = this.ActionInfo?.action ?? string.Empty;
            if (action.Equals("DDEntrance"))
            {
                GenerateDungeon(10);
                Game1.warpFarmer("DynamicDungeon", Location.EntryPoint.X, Location.EntryPoint.Y, true);
            }
            else if (action.Equals("DDBook"))
            {
                Game1.playSound("shwip");
                Game1.activeClickableMenu = this.InfoBook;
            }
            else if (action.Equals("DDDoor"))
            {
                //TODO inverted for testing
                if (!Game1.player.hasSkullKey)
                {
                    //var warp = Game1.getLocationFromName("DynamicDungeonEntrance").warps[0];
                    //Game1.warpFarmer("DynamicDungeonEntrance", warp.X, warp.Y - 1, false);
                    if(Game1.getLocationFromName("DynamicDungeonEntrance") != null)
                        Game1.warpFarmer("DynamicDungeonEntrance", 1, 1,false);
                    else this.Monitor.Log("Couldn't Find the dungeon");

                }
                else
                    Game1.drawObjectDialogue(SHelper.Translation.Get("SkullKeyNeeded"));
            }
        }
        private void CheckForAction()
        {
            if (Game1.activeClickableMenu == null && !Game1.player.UsingTool && !Game1.pickingTool && !Game1.menuUp && (!Game1.eventUp || Game1.currentLocation.currentEvent.playerControlSequence) && !Game1.nameSelectUp && Game1.numberOfSelectedItems == -1 && !Game1.fadeToBlack)
            {
                this.ActionInfo = null;
                Vector2 grabTile = new Vector2((Game1.getOldMouseX() + Game1.viewport.X), (Game1.getOldMouseY() + Game1.viewport.Y)) / Game1.tileSize;
                if (!Utility.tileWithinRadiusOfPlayer((int)grabTile.X, (int)grabTile.Y, 1, Game1.player))
                    grabTile = Game1.player.GetGrabTile();
                var tile = Game1.currentLocation.map.GetLayer("Buildings").PickTile(new xTile.Dimensions.Location((int)grabTile.X * Game1.tileSize, (int)grabTile.Y * Game1.tileSize), Game1.viewport.Size) ?? Game1.currentLocation.map.GetLayer("Buildings").PickTile(new xTile.Dimensions.Location((int)grabTile.X * Game1.tileSize, (int)(grabTile.Y + 1) * Game1.tileSize), Game1.viewport.Size);
                if (tile != null && tile.Properties.TryGetValue("Action", out var propertyValue) && propertyValue!=null)
                {
                    string[] split = ((string)propertyValue).Split(' ');
                    string[] args = new string[split.Length - 1];
                    Array.Copy(split, 1, args, 0, args.Length);
                    this.ActionInfo = (Game1.player, split[0], args, grabTile);
                }
            }
        }
        #endregion
        #region events
        private void Command_Fromseed(string command, string[] arguments)
        {
            try
            {
                GenerateDungeon(Convert.ToInt32(arguments[0], 16));
            }
            catch
            {
                this.Monitor.Log("Input is not a valid seed!", LogLevel.Error);
            }
        }
        private void GameEvents_UpdateTick(object s, EventArgs e)
        {
            if (!Context.IsWorldReady) return;
            GameEvents.UpdateTick -= this.GameEvents_UpdateTick;
            InputEvents.ButtonReleased += this.InputEvents_ButtonReleased;
            PlayerEvents.Warped += this.PlayerEvents_Warped;
            var loc = Game1.getLocationFromName("WizardHouse");
            var sheet = new TileSheet("Custom",loc.map, this.Helper.Content.GetActualAssetKey("door.png"), new xTile.Dimensions.Size(4, 7), new xTile.Dimensions.Size(16, 16));
            loc.map.AddTileSheet(sheet);
            /*
                loc.setMapTileIndex(5, 13, 112, "Back");
                (5, 13, "Buildings", 1, "Custom").ApplyTo(loc.map);
                (5, 12, "Front", 0, "Custom").ApplyTo(loc.map);
                */
            (4, 11, "Front", 0, "Custom").ApplyTo(loc.map);
            (5, 11, "Front", 1, "Custom").ApplyTo(loc.map);
            (6, 11, "Front", 2, "Custom").ApplyTo(loc.map);

            (4, 12, "Buildings", 4, "Custom").ApplyTo(loc.map);
            (6, 12, "Buildings", 6, "Custom").ApplyTo(loc.map);

            (4, 13, "Buildings", 8, "Custom").ApplyTo(loc.map);
            (6, 13, "Buildings", 10, "Custom").ApplyTo(loc.map);

            (5, 12, "Buildings", new[] { 12, 13, 14, 15, 20, 21, 22, 23 }, "Custom", 250).ApplyTo(loc.map);
            (5, 13, "Buildings", new[] { 16, 17, 18, 19, 24, 25, 26, 27 }, "Custom", 250).ApplyTo(loc.map);

            (5, 13, "Buildings", "Action", "DDDoor").ApplyTo(loc.map);
            if (Game1.getLocationFromName("DynamicDungeonEntrance") != null) return;
            GameLocation l = new GameLocation
            {
                //TODO Still not correctly adding the location to the locations list. Looking into l = new GameLocation(string, string)
                map = this.Helper.Content.Load<Map>("DynamicDungeonsEntrance.tbin"),
            };
            Game1.locations.Add(l);
        }
        private void PlayerEvents_Warped(object s, EventArgsPlayerWarped e)
        {
            if(e.PriorLocation!=null && (e.PriorLocation.Name=="DynamicDungeonEntrance" || e.PriorLocation.Name == "WizardHouse"))
            {
                ControlEvents.ControllerButtonPressed -= this.ControlEvents_ControllerButtonPressed;
                ControlEvents.ControllerButtonReleased -= this.ControlEvents_ControllerButtonReleased;
                ControlEvents.MouseChanged -= this.ControlEvents_MouseChanged;
                if (e.PriorLocation.Name == "DynamicDungeonEntrance")
                    GraphicsEvents.OnPreRenderHudEvent -= this.GraphicsEvents_OnPreRenderHudEvent;
            }
            if(e.NewLocation.Name=="DynamicDungeonEntrance" || e.NewLocation.Name == "WizardHouse")
            {
                ControlEvents.ControllerButtonPressed += this.ControlEvents_ControllerButtonPressed;
                ControlEvents.ControllerButtonReleased += this.ControlEvents_ControllerButtonReleased;
                ControlEvents.MouseChanged += this.ControlEvents_MouseChanged;
                if (e.NewLocation.Name == "DynamicDungeonEntrance")
                    GraphicsEvents.OnPreRenderHudEvent += this.GraphicsEvents_OnPreRenderHudEvent;
            }
        }
        private void GraphicsEvents_OnPreRenderHudEvent(object s, EventArgs e)
        {
            void Glow(float x, float y)
            {
                Game1.spriteBatch.Draw(Game1.mouseCursors, Game1.GlobalToLocal(Game1.viewport, new Vector2((x * Game1.tileSize + Game1.tileSize / 2 + Game1.pixelZoom / 2), (y * Game1.tileSize + Game1.tileSize / 4))), new Rectangle?(new Rectangle(88, 1779, 30, 30)), Color.PaleGoldenrod * (Game1.currentLocation.IsOutdoors ? 0.35f : 0.43f), 0f, new Vector2(15f, 15f), Game1.pixelZoom + (float)(Game1.tileSize * Math.Sin((Game1.currentGameTime.TotalGameTime.TotalMilliseconds + (x * Game1.tileSize * 777) + (y * Game1.tileSize * 9746)) % 3140.0 / 1000.0) / 50.0), SpriteEffects.None, 1f);
            }
            Glow(1.30f, 4.50f);
            Glow(5.00f, 2.50f);
            Glow(8.70f, 4.50f);
            Glow(2.25f, 8.50f);
            Glow(7.75f, 8.50f);
        }
        private void InputEvents_ButtonReleased(object s, EventArgsInput e)
        {
            if (!Context.IsWorldReady || (e.Button != SButton.F5 && e.Button != SButton.F6 && e.Button != SButton.F7))
                return;
            if (e.Button == SButton.F5)
            {
                Game1.warpFarmer("WizardHouse", 5, 14, false);
            }
        }
        private void ControlEvents_ControllerButtonPressed(object sender, EventArgsControllerButtonPressed e)
        {
            if (e.ButtonPressed == Buttons.A)
                CheckForAction();
        }
        private void ControlEvents_ControllerButtonReleased(object sender, EventArgsControllerButtonReleased e)
        {
            if (this.ActionInfo != null && e.ButtonReleased == Buttons.A)
            {
                ResolveAction();
                this.ActionInfo = null;
            }
        }
        private void ControlEvents_MouseChanged(object sender, EventArgsMouseStateChanged e)
        {
            if (e.NewState.RightButton == ButtonState.Pressed && e.PriorState.RightButton != ButtonState.Pressed)
                CheckForAction();
            if (this.ActionInfo != null && e.NewState.RightButton == ButtonState.Released)
            {
                ResolveAction();
                this.ActionInfo = null;
            }
        }
        #endregion
    }
}
