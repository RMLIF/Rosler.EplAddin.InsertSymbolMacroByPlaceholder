using Eplan.EplApi.Base;
using Eplan.EplApi.DataModel;
using Eplan.EplApi.DataModel.Graphics;

namespace Rosler.EplAddin.InsertSymbolMacroByPlaceholder.Models
{
    public class PlaceholderLocation
    {
        public Page Page { get; set; }
        public PointD Location { get; set; }

        public PlaceholderLocation() { }

        public PlaceholderLocation(Page page, PointD location)
        {
            Page = page;
            Location = location;
        }
    }
}
