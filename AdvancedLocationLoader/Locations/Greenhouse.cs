using System.Xml.Serialization;

namespace Entoarox.AdvancedLocationLoader.Locations
{
    [XmlType("ALLGreenhouse")]
    public class Greenhouse : StardewValley.GameLocation
    {
        public Greenhouse()
        {

        }
        public Greenhouse(xTile.Map map, string name) : base(map.ToString(),name)
        {

        }
        public override void DayUpdate(int dayOfMonth)
        {
            string realName = this.Name;
            this.name.Set("Greenhouse");
            base.DayUpdate(dayOfMonth);
            this.name.Set(realName);
        }
    }
}
