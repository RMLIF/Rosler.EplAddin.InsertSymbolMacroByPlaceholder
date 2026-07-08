using Eplan.EplApi.DataModel;
using System.Collections.Generic;

namespace Rosler.EplAddin.InsertSymbolMacroByPlaceholder.Models
{
    public class SchienenPlan
    {
        public List<SchienenEintrag> Schienen
            = new List<SchienenEintrag>();
    }

    public class SchienenEintrag
    {
        public Function ParentFunction { get; set; }

        public int GesamtPlaetze { get; set; }

        public int BelegtePlaetze { get; set; }

        public int FreiePlaetze { get; set; }

        public bool IstNeueSchiene { get; set; }

        public List<PlaceholderLocation> Targets
            = new List<PlaceholderLocation>();
    }
}