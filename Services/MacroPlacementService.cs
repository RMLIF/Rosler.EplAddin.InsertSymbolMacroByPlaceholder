

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
      

        public void PlaceAll(
    List<PlaceholderLocation> targets,
    SchienenErgebnis setup,
    SymbolMacro macro,
    PhasenschienenService service)
        {
            GlobalLogger.Info("Starte Makro-Platzierung");


            Insert insert = new Insert();

            for (int i = 0; i < targets.Count; i++)
            {
                int variant = setup.VariantIndex + i;

                if (variant > 4)
                    variant = 4;


                var target = targets[i];



                var placements = insert.SymbolMacro(
                    macro,
                    variant,
                    target.Page,
                    target.Location,
                    Insert.MoveKind.Absolute,
                    SymbolMacro.Enums.NumerationMode.None);

                // ✅ 🔥 DEBUG-LOG (BMK + Makro + Ziel)
                

                if (placements != null && placements.Length > 0)
                {
                    
                    service.UpdateMacroPlacements(
                        target.Page,
                        setup.ParentFunction,
                        placements);
                }

            }

            GlobalLogger.Info("Makro-Platzierung abgeschlossen");
        }

   

    }
}

