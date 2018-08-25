using System.Xml;
using System.Xml.Serialization;

namespace Entoarox.AdvancedLocationLoader.Locations
{
    [XmlType("ALLSewer")]
    public class Sewer : StardewValley.Locations.Sewer
    {
        public Sewer()
        {

        }
        public Sewer(xTile.Map map, string name) : base(map.ToString(),name)
        {

        }
        public new void resetForPlayerEntry()
        {
            base.resetForPlayerEntry();
            this.characters.Clear();
        }
    }
}
