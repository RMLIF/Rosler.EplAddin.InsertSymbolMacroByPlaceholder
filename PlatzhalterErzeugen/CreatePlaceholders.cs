using Eplan.EplApi.Base;
using Eplan.EplApi.Base.Enums;
using Eplan.EplApi.DataModel;
using Eplan.EplApi.DataModel.Graphics;
using System.Collections.Generic;
using System.Linq;

namespace Rosler.EplAddin.InsertSymbolMacroByPlaceholder.PlatzhalterErzeugen
{
    internal class CreatePlaceholders
    {
        /// <summary>
        /// Erzeugt auf der übergebenen Seite Platzhalter für Motorschutzschalter.
        /// </summary>


        public void CreatePhaseRailPlaceholders(Page[] pages)
        {
             foreach (Page page in pages)
                {
                    foreach (Function function in GetMotorProtectionSwitches(page))
                    {
                        if (!PlaceholderExists(function))
                        {
                            CreatePhaseRailText(function);
                        }
                    }
                }
            
        }



        /// <summary>
        /// Liefert alle Hauptfunktionen mit Kategorie Motorschutzschalter
        /// auf der übergebenen Seite zurück.
        /// </summary>
        private IEnumerable<Function> GetMotorProtectionSwitches(Page page)
        {
            return page.Functions
                       .Where(IsMotorProtectionSwitch);
        }

        /// <summary>
        /// Prüft, ob die Funktion ein Motorschutzschalter ist.
        /// </summary>
        private bool IsMotorProtectionSwitch(Function function)
        {
            if (function == null)
                return false;

            if (!function.IsMainFunction)
                return false;

            FunctionDefinition definition = function.FunctionDefinition;

            if (definition == null)
                return false;

            return definition.FunctionCategory ==
                   FunctionCategory.MotorOverloadSwitch;
        }

        /// <summary>
        /// Ermittelt die Position des Platzhaltertextes.
        /// Bezugspunkt ist der Einfügepunkt der Hauptfunktion.
        /// </summary>
        private PointD GetTextPosition(Function function)
        {
            PointD insertPoint = function.Location;

            return new PointD(
                insertPoint.X - 8.0,
                284.0
            );
        }

        /// <summary>
        /// Prüft, ob bereits ein entsprechender Platzhalter vorhanden ist.
        /// </summary>
        private bool PlaceholderExists(Function function)
        {
            foreach (Placement placement in function.Page.AllPlacements)
            {
                Text text = placement as Text;

                if (text == null)
                    continue;

                if (text.ToString() == "[$place Phasenschiene$]")
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Erzeugt den Platzhaltertext.
        /// </summary>
        private void CreatePhaseRailText(Function function)
        {
            string value = "[$place Phasenschiene$]";
            //string value = "such Text";

            Text text = new Text();

            text.Create(
                function.Page,
                value,
                1.3);

            text.Location = GetTextPosition(function);
            text.IsSetAsVisible = Placement.Visibility.Invisible;
            text.Rotation = 45.0;
            text.IsAutomaticallyTranslated = false;
        }
    }
}