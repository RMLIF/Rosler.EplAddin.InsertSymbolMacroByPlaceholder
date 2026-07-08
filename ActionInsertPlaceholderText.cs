using Eplan.EplApi.ApplicationFramework;
using Eplan.EplApi.DataModel;
using Eplan.EplApi.HEServices;
using Rosler.EplAddin.InsertSymbolMacroByPlaceholder.PlatzhalterErzeugen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Rosler.EplAddin.InsertSymbolMacroByPlaceholder
{
    public class ActionInsertPlaceholderText : IEplAction

    {
        public bool Execute(ActionCallingContext oActionCallingContext)
        {
            // 👉 aktuelle Auswahl (Seiten)
            SelectionSet selectionSet = new SelectionSet();
            Page[] selectedPages = selectionSet.GetSelectedPages();

            if (selectedPages == null || selectedPages.Length == 0)
            {
                MessageBox.Show("Bitte wählen Sie eine Schaltplanseite aus.");
                return false;
            }


            using (SafetyPoint safetyPoint = SafetyPoint.Create())
            {

                using (UndoStep undo = new UndoManager().CreateUndoStep())
                {
                    {

                        CreatePlaceholders createPlaceholders = new CreatePlaceholders();

                        createPlaceholders.CreatePhaseRailPlaceholders(selectedPages);

                        safetyPoint.Commit();
                    }
                }


                return true;
            }
        }

        public void GetActionProperties(ref ActionProperties actionProperties)
        {
           
        }

        public bool OnRegister(ref string Name, ref int Ordinal)
        {
            Name = "RoslerActionInsertPlaceholderText";
            Ordinal = 20;
            return true;
        }
    }
}
