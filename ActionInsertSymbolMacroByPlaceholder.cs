//using Eplan.EplApi.ApplicationFramework;
//using Eplan.EplApi.Base;
//using Eplan.EplApi.DataModel;
//using Eplan.EplApi.DataModel.MasterData;
//using Eplan.EplApi.MasterData;
//using Rosler.EplAddin.InsertSymbolMacroByPlaceholder.Infrastructure;
//using Rosler.EplAddin.InsertSymbolMacroByPlaceholder.Models;
//using Rosler.EplAddin.InsertSymbolMacroByPlaceholder.Orchestrator;
//using System.Collections.Generic;

//public class ActionInsertSymbolMacroByPlaceholder : IEplAction
//{
//    public bool Execute(ActionCallingContext context)
//    {
//        try
//        {
//            GlobalLogger.Info("Starte ActionInsertSymbolMacroByPlaceholder");

//            // ✅ aktuelle Seite holen
//            Project project = new ProjectManager().CurrentProject;
//            Page page = project.Pages[0];

//            // ✅ Dummy / Beispiel – MUSS bei dir ersetzt werden
//            SymbolMacro macro = LoadMacro();

//            // ✅ Platzhalter (Positionsliste)
//            List<PlaceholderLocation> locations = GetPlaceholderLocations(page);

//            // ✅ Artikel für Phasenschienen
//            List<MDPart> parts = LoadParts();

//            int neededSlots = locations.Count;

//            var orchestrator = new PlaceMacroOrchestrator();

//            // ✅ 🔥 HIER war dein Fehler → macro fehlte vorher!
//            orchestrator.Execute(
//                page,
//                locations,
//                macro,       // ✅ DAS hat gefehlt!
//                parts,
//                neededSlots
//            );

//            return true;
//        }
//        catch (System.Exception ex)
//        {
//            GlobalLogger.Error($"Fehler in Action: {ex.Message}");
//            return false;
//        }
//    }

//    public void GetActionProperties(ref ActionProperties actionProperties)
//    {
//    }

//    public bool OnRegister(ref string name, ref int ordinal)
//    {
//        name = "RoslerInsertSymbolMacroByPlaceholder";
//        ordinal = 20;
//        return true;
//    }

//    // =====================================================================
//    // ✅ HILFSMETHODEN (Dummy – ggf. anpassen)
//    // =====================================================================

//    private SymbolMacro LoadMacro()
//    {
//        // TODO: deinen echten Makro-Loader einsetzen
//        return new SymbolMacro();
//    }

//    private List<PlaceholderLocation> GetPlaceholderLocations(Page page)
//    {
//        // TODO: echte Placeholder ermitteln
//        return new List<PlaceholderLocation>();
//    }

//    private List<MDPart> LoadParts()
//    {
//        return new List<MDPart>();
//    }
//}

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
