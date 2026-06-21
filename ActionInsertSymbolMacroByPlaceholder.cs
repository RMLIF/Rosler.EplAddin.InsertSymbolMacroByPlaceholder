using Eplan.EplApi.ApplicationFramework;
using Eplan.EplApi.Base;
using Eplan.EplApi.HEServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eplan.EplApi.DataModel;
using Eplan.EplApi.DataModel.Graphics;
using System.Windows.Forms;

namespace Rosler.EplAddin.InsertSymbolMacroByPlaceholder
{
    public class ActionInsertSymbolMacroByPlaceholder : IEplAction
    {
        public bool Execute(ActionCallingContext oActionCallingContext)
        {
            // 1. Parameter definieren
            string placeholderName = "";

            // Über Action-Parameter auslesen
            oActionCallingContext.GetParameter("PlaceholderName", ref placeholderName);

            // 2. Alle aktuell im EPLAN selektierten Seiten ermitteln
            SelectionSet selectionSet = new SelectionSet();
            Page[] selectedPages = selectionSet.GetSelectedPages();

            // Prüfen, ob mindestens eine Seite ausgewählt wurde
            if (selectedPages != null && selectedPages.Length > 0)
            {
                // Instanz Ihrer Logik-Klasse erstellen
                PlaceMacroAtPlaceholder logic = new PlaceMacroAtPlaceholder();

                // Schleife entfernt: Nur die erste/aktuell markierte Seite verarbeiten
                Page targetPage = selectedPages[0];

                // Die Methode für die einzelne Seite aufrufen
                logic.PlaceMacroPlaceholder(targetPage, placeholderName);
            }
            else
            {
                // Fehlerbehandlung: Keine Seite im Seiten-Navigator markiert
                MessageBox.Show("Bitte wählen Sie zuerst eine Schaltplanseite im Navigator aus.");
            }

            return true;
        }

        public void GetActionProperties(ref ActionProperties actionProperties)
        {
            // Optional: Hier können Sie Beschreibungen für die Parameter hinterlegen
        }

        public bool OnRegister(ref string Name, ref int Ordinal)
        {
            Name = "RoslerInsertSymbolMacroByPlaceholder";
            Ordinal = 20;
            return true;
        }
    }
}

//using Eplan.EplApi.ApplicationFramework;
//using Eplan.EplApi.Base;
//using Eplan.EplApi.HEServices;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using Eplan.EplApi.DataModel;
//using Eplan.EplApi.DataModel.Graphics;
//using Eplan.EplApi.HEServices;
//using Eplan.EplApi.Base;
//using System.Windows.Forms;

//namespace Rosler.EplAddin.InsertSymbolMacroByPlaceholder
//{
//    public class ActionInsertSymbolMacroByPlaceholder : IEplAction
//    {
//        public bool Execute(ActionCallingContext oActionCallingContext)
//        {
//            // 1. Parameter definieren
//            string placeholderName = "";
//            //string macroPath = @"C:\EPLAN_Data\Makros\MeinSymbolmakro.ems";

//            // Alternativ über Action-Parameter auslesen, falls gewünscht:
//            oActionCallingContext.GetParameter("PlaceholderName", ref placeholderName);
//            // oActionCallingContext.GetParameter("MacroPath", ref macroPath);

//            // 2. Alle aktuell im EPLAN selektierten Seiten ermitteln
//            SelectionSet selectionSet = new SelectionSet();
//            Page[] selectedPages = selectionSet.GetSelectedPages();

//            // Prüfen, ob überhaupt Seiten ausgewählt wurden
//            if (selectedPages != null && selectedPages.Length > 0)
//            {
//                // Instanz Ihrer Logik-Klasse erstellen
//                PlaceMacroAtPlaceholder logic = new PlaceMacroAtPlaceholder();

//                // Schleife über JEDE ausgewählte Seite
//                foreach (Page targetPage in selectedPages)
//                {
//                    // Die Methode aufrufen. Ihre Logik prüft bereits intern, 
//                    // ob der Platzhalter existiert, und bricht fehlerfrei ab, wenn nicht.
//                    logic.PlaceMacroPlaceholder(targetPage, placeholderName);
//                }
//            }
//            else
//            {
//                // Fehlerbehandlung: Keine Seite im Seiten-Navigator markiert
//                MessageBox.Show("Bitte wählen Sie zuerst mindestens eine Schaltplanseite im Navigator aus.");
//            }

//            return true;
//        }


//        public void GetActionProperties(ref ActionProperties actionProperties)
//        {

//        }

//        public bool OnRegister(ref string Name, ref int Ordinal)
//        {
//            Name = "RoslerInsertSymbolMacroByPlaceholder";
//            Ordinal = 20;
//            return true;
//        }

//    }
//}
