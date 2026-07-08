//using Eplan.EplApi.DataModel.Graphics;
//using Eplan.EplApi.DataModel.MasterData;
//using Eplan.EplApi.HEServices;
//using Rosler.EplAddin.InsertSymbolMacroByPlaceholder.Infrastructure;
//using Rosler.EplAddin.InsertSymbolMacroByPlaceholder.Models;
//using System.Collections.Generic;

//namespace Rosler.EplAddin.InsertSymbolMacroByPlaceholder.Services
//{
//    public class MacroPlacementService
//    {


//        //public void PlaceAll(List<PlaceholderLocation> targets,
//        //                     SchienenErgebnis setup,
//        //                     SymbolMacro macro,
//        //                     PhasenschienenService service)
//        //{
//        //    GlobalLogger.Info("Starte Makro-Platzierung");

//        //    Insert insert = new Insert();

//        //    for (int i = 0; i < targets.Count; i++)
//        //    {
//        //        int variant = setup.VariantIndex + i;
//        //        if (variant > 4)
//        //            variant = 4;

//        //        var target = targets[i];

//        //        GlobalLogger.DebugLog(
//        //            "Platzierung " + (i + 1) +
//        //            " | Seite=" + target.Page.Name +
//        //            " | Variant=" + variant);

//        //        var placements = insert.SymbolMacro(
//        //            macro,
//        //            variant,
//        //            target.Page,
//        //            target.Location,
//        //            Insert.MoveKind.Absolute,
//        //            SymbolMacro.Enums.NumerationMode.None);

//        //        service.UpdateMacroPlacements(target.Page, setup.ParentFunction, placements);
//        //    }

//        //    GlobalLogger.Info("Makro-Platzierung abgeschlossen");
//        //}



//    }
//}

using Eplan.EplApi.DataModel;
using Eplan.EplApi.DataModel.Graphics;
using Eplan.EplApi.DataModel.MasterData;
using Eplan.EplApi.HEServices;
using Rosler.EplAddin.InsertSymbolMacroByPlaceholder.Infrastructure;
using Rosler.EplAddin.InsertSymbolMacroByPlaceholder.Models;
using System;
using System.Collections.Generic;

namespace Rosler.EplAddin.InsertSymbolMacroByPlaceholder.Services
{
    public class MacroPlacementService
    {
        //public void PlaceAll(
        //    List<PlaceholderLocation> targets,
        //    SchienenErgebnis setup,
        //    SymbolMacro macro,
        //    PhasenschienenService service)
        //{
        //    GlobalLogger.Info("Starte Makro-Platzierung");

        //    Insert insert = new Insert();

        //    // ✅ NEU: alle Placements sammeln
        //    List<StorableObject> allPlacements = new List<StorableObject>();

        //    for (int i = 0; i < targets.Count; i++)
        //    {
        //        int variant = setup.VariantIndex + i;

        //        if (variant > 4)
        //            variant = 4;

        //        var target = targets[i];

        //        GlobalLogger.DebugLog(
        //            "Platzierung " + (i + 1) +
        //            " | Seite=" + target.Page.Name +
        //            " | Variant=" + variant);

        //        var placements = insert.SymbolMacro(
        //            macro,
        //            variant,
        //            target.Page,
        //            target.Location,
        //            Insert.MoveKind.Absolute,
        //            SymbolMacro.Enums.NumerationMode.None);

        //        // ✅ HIER: alles sammeln statt direkt verarbeiten
        //        if (placements != null)
        //        {
        //            foreach (var p in placements)
        //                allPlacements.Add(p);
        //        }
        //    }

        //    // ✅ GANZ WICHTIG: nur EINMAL ausführen
        //    if (allPlacements.Count > 0)
        //    {
        //        service.UpdateMacroPlacements(
        //            targets[0].Page,
        //            setup.ParentFunction,
        //            allPlacements.ToArray());
        //    }

        //    GlobalLogger.Info("Makro-Platzierung abgeschlossen");
        //}

        public void PlaceAll(
    List<PlaceholderLocation> targets,
    SchienenErgebnis setup,
    SymbolMacro macro,
    PhasenschienenService service)
        {
            GlobalLogger.Info("Starte Makro-Platzierung");


            GlobalLogger.Info(
                $"PLACEALL TARGET COUNT={targets.Count}");

           


            Insert insert = new Insert();

            for (int i = 0; i < targets.Count; i++)
            {
                int variant = setup.VariantIndex + i;

                if (variant > 4)
                    variant = 4;


                var target = targets[i];

                GlobalLogger.DebugLog(
                    "Platzierung " + (i + 1) +
                    " | Seite=" + target.Page.Name +
                    " | Variant=" + variant);


                GlobalLogger.Info(
                    $"INSERT PAGE={target.Page.Name}");

                GlobalLogger.Info(
                    $"INSERT VARIANT={variant}");

                GlobalLogger.Info(
                    $"INSERT TARGET={i + 1}");


                var placements = insert.SymbolMacro(
                    macro,
                    variant,
                    target.Page,
                    target.Location,
                    Insert.MoveKind.Absolute,
                    SymbolMacro.Enums.NumerationMode.None);

                // ✅ 🔥 DEBUG-LOG (BMK + Makro + Ziel)
                string bmk = setup.ParentFunction != null
                    ? setup.ParentFunction.Name
                    : "UNBEKANNT";


                string macroName = "UNBEKANNT";

                try
                {
                    macroName = macro.ToString();
                }
                
                catch { }


                GlobalLogger.Info(
                    "Makro platziert → BMK=" + bmk +
                    " | Makro=" + macroName +
                    " | Seite=" + target.Page.Name +
                    " | Variant=" + variant +
                    " | Pos=(" + target.Location.X + "," + target.Location.Y + ")");


                // ✅ DIREKT verarbeiten (nicht sammeln!)
                //if (placements != null && placements.Length > 0)
                //{
                //    service.UpdateMacroPlacements(
                //        target.Page,
                //        setup.ParentFunction,
                //        placements);
                //}

                if (placements != null && placements.Length > 0)
                {
                    GlobalLogger.Info(
                        $"UpdateMacroPlacements Parent={setup.ParentFunction?.Name}");

                    foreach (var placement in placements)
                    {
                        GlobalLogger.Info(
                            $"PlacementType={placement.GetType().FullName}");

                        if (placement is BoxedDevice bd)
                        {
                            GlobalLogger.Info(
                                $"BOXEDDEVICE IM UPDATE: {bd.Name}");

                            GlobalLogger.Info(
                                $"FunctionsInside={bd.FunctionsInside.Length}");

                            foreach (Function f in bd.FunctionsInside)
                            {
                                GlobalLogger.Info(
                                    $"   FUNC={f.Name}");

                                GlobalLogger.Info(
                                    $"   PARENT={f.ParentFunction?.Name}");
                            }
                        }

                        if (placement is Function fn)
                        {
                            GlobalLogger.Info(
                                $"FUNCTION IM UPDATE: {fn.Name}");
                        }
                    }

                    service.UpdateMacroPlacements(
                        target.Page,
                        setup.ParentFunction,
                        placements);
                }

            }


            


            GlobalLogger.Info("Makro-Platzierung abgeschlossen");
        }

        private void RemoveUnplacedWCMainFunctions(
   Project project)
        {
            try
            {
                DMObjectsFinder finder =
                    new DMObjectsFinder(project);

                Function[] functions =
                    finder.GetFunctions(null);

                foreach (Function func in functions)
                {
                    try
                    {
                        // Nur WCs betrachten
                        if (string.IsNullOrEmpty(func.Name) ||
                            !func.Name.Contains("-WC"))
                        {
                            continue;
                        }

                        // Nur Hauptfunktionen
                        if (!func.IsMainFunction)
                            continue;

                        // Nur unplatzierte Funktionen
                        if (func.IsPlaced)
                            continue;

                        GlobalLogger.Info(
                            $"DELETE UNUSED WC MAIN FUNCTION: {func.Name}");

                        //func.Remove();
                    }
                    catch (Exception ex)
                    {
                        GlobalLogger.Warn(
                            $"DELETE FAILED {func?.Name}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                GlobalLogger.Error(
                    $"RemoveUnplacedWCMainFunctions ERROR: {ex.Message}");
            }
        }

    }
}

