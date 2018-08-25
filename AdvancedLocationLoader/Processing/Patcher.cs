using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Entoarox.AdvancedLocationLoader.Configs;
using Entoarox.Framework;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using Warp = Entoarox.AdvancedLocationLoader.Configs.Warp;

namespace Entoarox.AdvancedLocationLoader.Processing
{
    /// <summary>Applies content pack data.</summary>
    internal class Patcher
    {
        /*********
        ** Properties
        *********/
        /// <summary>Writes messages to the log.</summary>
        private readonly IMonitor Monitor;

        /// <summary>The content pack data to apply.</summary>
        private readonly ContentPackData[] Data;

        /// <summary>The content helper to use when interacting with the game's content files.</summary>
        private readonly IContentHelper CoreContentHelper;

        //private List<string> Conditionals = new List<string>();
        //private SecondaryLocationManifest1_2 Compound = new SecondaryLocationManifest1_2();


        /*********
        ** Public methods
        *********/
        /// <summary>Construct an instance.</summary>
        /// <param name="monitor">Writes messages to the log.</param>
        /// <param name="contentHelper">The content helper to use when interacting with the game's content files.</param>
        /// <param name="data">The content pack data to apply.</param>
        public Patcher(IMonitor monitor, IContentHelper contentHelper, IEnumerable<ContentPackData> data)
        {
            this.Monitor = monitor;
            this.Data = data.ToArray();
            this.CoreContentHelper = contentHelper;
        }

        /// <summary>Apply content data to the game.</summary>
        /// <param name="compoundData">Compound data loaded from the content packs.</param>
        public void ApplyPatches(out Compound compoundData)
        {
            // track info
            var seasonalTilesheetsByContentPack = new Dictionary<IContentPack, Tilesheet[]>();
            var customLocationNames = new HashSet<string>();
            var mapSizes = new Dictionary<string, Point>();
            var mapCache = new Dictionary<string, xTile.Map>();
            var tilesheetCache = new Dictionary<string, List<string>>();
            var dynamicTiles = new List<Tile>();
            var dynamicProperties = new List<Property>();
            var dynamicWarps = new List<Warp>();
            var conditionals = new List<Conditional>();
            var teleporters = new List<TeleporterList>();
            var shops = new List<ShopConfig>();

            // apply content packs
            foreach (ContentPackData pack in this.Data)
            {
                this.Monitor.Log($"Applying {pack.ContentPack.Manifest.Name}...", LogLevel.Debug);
                string stage = "entry";
                try
                {
                    // apply locations
                    stage = "locations";
                    foreach (Location obj in pack.Locations)
                    {
                        if (Game1.getLocationFromName(obj.MapName) != null)
                        {
                            this.Monitor.Log($"  Can't add location {obj.MapName}, it already exists.", LogLevel.Warn);
                            continue;
                        }

                        try
                        {
                            // cache info
                            xTile.Map map = pack.ContentPack.LoadAsset<xTile.Map>(obj.FileName);
                            mapSizes.Add(obj.MapName, new Point(map.DisplayWidth, map.DisplayHeight));
                            mapCache.Add(obj.MapName, map);
                            customLocationNames.Add(obj.MapName);

                            Processors.ApplyLocation(pack.ContentPack, obj);
                        }
                        catch (Exception err)
                        {
                            this.Monitor.Log($"   Can't add location {obj.MapName}, an error occurred.", LogLevel.Error,
                                err);
                        }
                    }

                    // apply overrides
                    stage = "overrides";
                    foreach (Override obj in pack.Overrides)
                    {
                        if (Game1.getLocationFromName(obj.MapName) == null)
                        {
                            this.Monitor.Log($"   Can't override location {obj.MapName}, it doesn't exist.",
                                LogLevel.Error);
                            continue;
                        }

                        try
                        {
                            xTile.Map map = pack.ContentPack.LoadAsset<xTile.Map>(obj.FileName);
                            mapSizes.Add(obj.MapName, new Point(map.DisplayWidth, map.DisplayHeight));

                            Processors.ApplyOverride(pack.ContentPack, obj);
                        }
                        catch (Exception err)
                        {
                            this.Monitor.Log($"   Can't override location {obj.MapName}, an error occurred.", LogLevel.Error, err);
                        }
                    }

                    // apply redirects
                    stage = "redirects";
                    {
                        HashSet<string> redirCache = new HashSet<string>();
                        foreach (Redirect obj in pack.Redirects)
                        {
                            if (!redirCache.Contains(obj.ToFile))
                            {
                                string toAssetPath = pack.ContentPack.GetRelativePath(fromAbsolutePath: ModEntry.SHelper.DirectoryPath, toLocalPath: obj.ToFile);
                                this.CoreContentHelper.RegisterXnbReplacement(obj.FromFile, toAssetPath);
                                redirCache.Add(obj.ToFile);
                            }
                        }
                    }

                    // apply tilesheets
                    stage = "tilesheets";
                    IList<Tilesheet> seasonalTilesheets = new List<Tilesheet>();
                    foreach (Tilesheet obj in pack.Tilesheets)
                    {
                        if (Game1.getLocationFromName(obj.MapName) == null &&
                            !customLocationNames.Contains(obj.MapName))
                        {
                            this.Monitor.Log($"   Can't apply tilesheet '{obj.SheetId}', location '{obj.MapName}' doesn't exist.", LogLevel.Error);
                            continue;
                        }

                        Processors.ApplyTilesheet(this.CoreContentHelper, pack.ContentPack, obj);
                        if (obj.Seasonal)
                            seasonalTilesheets.Add(obj);
                    }
                    if (seasonalTilesheets.Any())
                        seasonalTilesheetsByContentPack[pack.ContentPack] = seasonalTilesheets.ToArray();

                    // apply tiles
                    stage = "tiles";
                    foreach (Tile obj in pack.Tiles)
                    {
                        if (!this.PreprocessTile(obj, customLocationNames, mapSizes, out string error))
                        {
                            if (error != null)
                                this.Monitor.Log($"   Can't apply tile {obj}: {error}", LogLevel.Error);
                        }

                        else if (obj.SheetId != null && (!tilesheetCache.ContainsKey(obj.MapName) || !tilesheetCache[obj.MapName].Contains(obj.SheetId)))
                        {
                            xTile.Map map = mapCache.ContainsKey(obj.MapName)
                                ? mapCache[obj.MapName]
                                : Game1.getLocationFromName(obj.MapName).map;

                            if (map.GetTileSheet(obj.SheetId) == null && (!tilesheetCache.ContainsKey(map.Id) || !tilesheetCache[map.Id].Contains(obj.SheetId)))
                            {
                                this.Monitor.Log($"   Can't apply tile {obj}, tilesheet doesn't exist.", LogLevel.Error);
                                continue;
                            }
                        }

                        Processors.ApplyTile(obj);
                        if (!string.IsNullOrEmpty(obj.Conditions))
                            dynamicTiles.Add(obj);
                    }

                    // apply properties
                    stage = "properties";
                    foreach (Property obj in pack.Properties)
                    {
                        if (!this.PreprocessTile(obj, customLocationNames, mapSizes, out string error))
                        {
                            if (error != null)
                                this.Monitor.Log($"   Can't apply property patch {obj}: {error}.", LogLevel.Error);
                            continue;
                        }

                        Processors.ApplyProperty(obj);
                        if (!string.IsNullOrEmpty(obj.Conditions))
                            dynamicProperties.Add(obj);
                    }

                    // apply warps
                    stage = "warps";
                    foreach (Warp obj in pack.Warps)
                    {
                        if (!this.PreprocessTile(obj, customLocationNames, mapSizes, out string error))
                        {
                            if (error != null)
                                this.Monitor.Log($"   Can't apply warp {obj}: {error}.", LogLevel.Error);
                            continue;
                        }

                        Processors.ApplyWarp(obj);
                        if (!string.IsNullOrEmpty(obj.Conditions))
                            dynamicWarps.Add(obj);
                    }

                    // save conditionals
                    stage = "conditionals";
                    foreach (Conditional obj in pack.Conditionals)
                        conditionals.Add(obj);

                    // save teleporters
                    stage = "teleporters";
                    foreach (TeleporterList obj in pack.Teleporters)
                        teleporters.Add(obj);

                    // save shops
                    stage = "shops";
                    foreach(ShopConfig obj in pack.Shops)
                        shops.Add(obj);
                }
                catch (Exception ex)
                {
                    this.Monitor.ExitGameImmediately($"Failed applying changes from the {pack.ContentPack.Manifest.Name} content pack ({stage} step).", ex);
                }
            }

            // postprocess
            try
            {
                NPC.populateRoutesFromLocationToLocationList();
                VerifyGameIntegrity();
                this.Monitor.Log("Patches applied!", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                this.Monitor.ExitGameImmediately("Failed post-processing after content pack changes.", ex);
            }

            // create compound info
            compoundData = new Compound(seasonalTilesheetsByContentPack, dynamicTiles, dynamicProperties, dynamicWarps, conditionals, teleporters, shops);
        }


        /*********
        ** Private methods
        *********/
        private bool PreprocessTile(TileInfo info, HashSet<string> customLocationNames, IDictionary<string, Point> mapSizes, out string error)
        {
            error = null;

            if (Game1.getLocationFromName(info.MapName) == null && !customLocationNames.Contains(info.MapName))
            {
                if (!info.Optional)
                    error = "location does not exist";
                return false;
            }

            Point size;
            if (!customLocationNames.Contains(info.MapName))
            {
                xTile.Map map = Game1.getLocationFromName(info.MapName).map;
                size = new Point(map.DisplayWidth, map.DisplayHeight);
                customLocationNames.Add(info.MapName);
                mapSizes[info.MapName] = size;
            }
            else
                size = mapSizes[info.MapName];
            int minX = 0;
            int minY = 0;
            int maxX = size.X;
            int maxY = size.Y;
            if (info is Warp)
            {
                minX--;
                minY--;
                maxX++;
                maxY++;
            }

            if (info.TileX < minX || info.TileY < minY || info.TileX > maxX || info.TileY > maxY)
            {
                error = "placement is outside location bounds";
                return false;
            }

            return true;
        }

        private void VerifyGameIntegrity()
        {
            string[] seasons = { "spring", "summer", "fall", "winter" };
            foreach (GameLocation loc in Game1.locations)
            {
                if (loc.IsOutdoors && !loc.Name.Equals("Desert"))
                {
                    foreach (xTile.Tiles.TileSheet sheet in loc.map.TileSheets)
                    {
                        if (!sheet.ImageSource.Contains("path") && !sheet.ImageSource.Contains("object"))
                        {
                            string[] path = sheet.ImageSource.Split('_');
                            if (path.Length != 2)
                                this.Monitor.ExitGameImmediately("The `" + sheet.Id + "` TileSheet in the `" + loc.Name + "` location is treated as seasonal but does not have proper seasonal formatting, this will cause bugs!");
                            foreach (string season in seasons)
                            {
                                try
                                {
                                    Game1.content.Load<Microsoft.Xna.Framework.Graphics.Texture2D>(Path.Combine("Maps", season + "_" + path[1]));
                                }
                                catch
                                {
                                    this.Monitor.ExitGameImmediately("The `" + sheet.Id + "` TileSheet in the `" + loc.Name + "` location is seasonal but ALL cant find the tilesheet for the `" + season + "` season, this will cause bugs!");
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
