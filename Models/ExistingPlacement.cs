
using Eplan.EplApi.Base;
using Eplan.EplApi.DataModel;
using Eplan.EplApi.DataModel.Graphics;

namespace Rosler.EplAddin.InsertSymbolMacroByPlaceholder.Models
{
    public class ExistingPlacement
    {
        public Page Page { get; set; }
        public PointD Location { get; set; }
        public int Variant { get; set; }
    }
}
