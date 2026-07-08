using Eplan.EplApi.DataModel;

namespace Rosler.EplAddin.InsertSymbolMacroByPlaceholder.Models
{

    public class SchienenErgebnis
    {
        public Function ParentFunction { get; set; }

        public int VariantIndex { get; set; }

        public int NextVariant { get; set; }
    }

}