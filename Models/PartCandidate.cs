using Eplan.EplApi.MasterData;

namespace Rosler.EplAddin.InsertSymbolMacroByPlaceholder.Models
{
    public class PartCandidate
    {
        public MDPart Part { get; set; }
        public int Slots { get; set; }
    }
}