using Eplan.EplApi.DataModel;

namespace Rosler.EplAddin.InsertSymbolMacroByPlaceholder.Models
{
    public class BaseContext
    {
        public string Manufacturer { get; set; }
        public string ProductGroup { get; set; }
        public ArticleReference Reference { get; set; }
    }
}
