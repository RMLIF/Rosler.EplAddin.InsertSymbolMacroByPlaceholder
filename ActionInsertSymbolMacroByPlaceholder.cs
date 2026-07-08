using Eplan.EplApi.ApplicationFramework;
using Eplan.EplApi.DataModel;
using Eplan.EplApi.HEServices;
using Rosler.EplAddin.InsertSymbolMacroByPlaceholder.Infrastructure;
using Rosler.EplAddin.InsertSymbolMacroByPlaceholder.Orchestrator;
using System.Windows.Forms;

namespace Rosler.EplAddin.InsertSymbolMacroByPlaceholder
{
    public class ActionInsertSymbolMacroByPlaceholder : IEplAction
    {
        public bool Execute(ActionCallingContext context)
        {
            string placeholderName = string.Empty;

            // 👉 Parameter auslesen
            context.GetParameter("PlaceholderName", ref placeholderName);

            if (string.IsNullOrWhiteSpace(placeholderName))
            {
                GlobalLogger.Warn("Kein Platzhalter-Name übergeben");
                return false;
            }

            // 👉 aktuelle Auswahl (Seiten)
            SelectionSet selectionSet = new SelectionSet();
            Page[] selectedPages = selectionSet.GetSelectedPages();

            if (selectedPages == null || selectedPages.Length == 0)
            {
                MessageBox.Show("Bitte wählen Sie eine Schaltplanseite aus.");
                return false;
            }

            Eplan.EplApi.DataModel.Page targetPage = selectedPages[0];

            try
            {
                // ✅ NEUER Einstiegspunkt: Orchestrator
                var orchestrator = new PlaceMacroOrchestrator();
                orchestrator.Execute(targetPage, placeholderName);

                return true;
            }
            catch (System.Exception ex)
            {
                GlobalLogger.Error("Fehler in Action: " + ex.Message);
                return false;
            }
        }

        public void GetActionProperties(ref ActionProperties actionProperties)
        {
            // Optional: Beschreibung/Hilfe für Parameter
        }

        public bool OnRegister(ref string Name, ref int Ordinal)
        {
            Name = "RoslerInsertSymbolMacroByPlaceholder";
            Ordinal = 20;
            return true;
        }
    }
}
