using Eplan.EplApi.DataModel;
using Eplan.EplApi.DataModel.Graphics;
using Rosler.EplAddin.InsertSymbolMacroByPlaceholder.Infrastructure;
using Rosler.EplAddin.InsertSymbolMacroByPlaceholder.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Rosler.EplAddin.InsertSymbolMacroByPlaceholder.Services
{
    public class PlaceholderSearchService
    {

        public List<PlaceholderLocation> Find(Project project, string name)
        {
            GlobalLogger.Info("Suche Platzhalter: " + name);

            List<PlaceholderLocation> result = new List<PlaceholderLocation>();

            var finder = new DMObjectsFinder(project);
            var placements = finder.GetPlacements(null);

            GlobalLogger.DebugLog("Gesamt Placements im Projekt: " + placements.Length);

            foreach (var placement in placements)
            {
                var ph = placement as PlaceHolder;

                if (ph == null)
                    continue;

                if (!ph.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    continue;

                result.Add(new PlaceholderLocation(ph.Page, ph.Location));
            }

            GlobalLogger.Info("Gefundene Platzhalter: " + result.Count);

            return result;
        }

    }
}
