using Eplan.EplApi.Base;
using Eplan.EplApi.Base.Enums;
using Eplan.EplApi.DataModel;
using Eplan.EplApi.DataModel.MasterData;
using Eplan.EplApi.HEServices;
using Rosler.EplAddin.InsertSymbolMacroByPlaceholder.Infrastructure;
using Rosler.EplAddin.InsertSymbolMacroByPlaceholder.Models;
using Rosler.EplAddin.InsertSymbolMacroByPlaceholder.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Contexts;
using System.Text.RegularExpressions;
using static Eplan.EplApi.HEServices.Renumber.Enums;

namespace Rosler.EplAddin.InsertSymbolMacroByPlaceholder.Orchestrator
{
    public class PlaceMacroOrchestrator
    {
        private readonly PlaceholderSearchService _search = new PlaceholderSearchService();
        private readonly PhasenschienenService _phasenschiene = new PhasenschienenService();
        private readonly MacroPlacementService _placement = new MacroPlacementService();

        public void Execute(Page page, string placeholderName)
        {
            GlobalLogger.Info("=== Start Makro-Workflow ===");


            var targets = _search.Find(page.Project, placeholderName);

            if (targets == null || targets.Count == 0)
            {
                GlobalLogger.Warn("Keine Platzhalter gefunden");
                return;
            }

            // ✅ ALLE Targets behalten (für Berechnung!)
            int totalPins = targets.Count * 3;

           

            // ✅ NEU: freie Targets separat berechnen
            var freeTargets = targets
                .Where(t => !HasPlacement(t))
                .ToList();

            SchienenPlan plan =
                _phasenschiene.PreparePhasenschienen(
                    page,
                    freeTargets);

         

            Function mainFunction =
                plan.Schienen.Count > 0
                    ? plan.Schienen[0].ParentFunction
                    : null;


            var oldPlacements = GetExistingPlacements(mainFunction);

            GlobalLogger.Info("Bestehende Makros: " + oldPlacements.Count);

            DeleteOldPlacements(mainFunction);

            GlobalLogger.Info("Alte Makros gelöscht");

            // alte + neue zusammenführen

            foreach (var old in oldPlacements)
            {
                freeTargets.Insert(
                    0,
                    new PlaceholderLocation(
                        old.Page,
                        old.Location));
            }

            foreach (var schiene in plan.Schienen)
            {

                var func =
                    schiene.ParentFunction;

                var t =
                    schiene.Targets;


                var existingPlacements =
                    GetExistingPlacements(func);

                int nextVariant =
                    existingPlacements.Count;

                var macroLocal = LoadMacro(func);

                if (macroLocal == null)
                {
                    GlobalLogger.Warn("Makro konnte nicht geladen werden");
                    continue;
                }

                _placement.PlaceAll(
                    t,
                                new SchienenErgebnis
                                {
                                    ParentFunction = func,
                                    VariantIndex = nextVariant
                                },
                                macroLocal,
                                _phasenschiene);


                _phasenschiene.CleanupDuplicateWCMainFunctions(
                    page.Project);


            }

           
     
            GlobalLogger.Info("=== Workflow abgeschlossen ===");
        }

        private SymbolMacro LoadMacro(Function function)
        {
            if (function == null)
                return null;

            var refs = function.ArticleReferences;
            if (refs == null || refs.Length == 0)
                return null;

            var article = refs[0].Article;
            if (article == null)
                return null;

            string path = PathMap.SubstitutePath(article.Properties.ARTICLE_GROUPSYMBOLMACRO);

            if (!File.Exists(path))
                return null;

            SymbolMacro macro = new SymbolMacro();
            macro.Open(path);

            return macro;
        }

        private List<ExistingPlacement> GetExistingPlacements(Function parent)
        {
            var result = new List<ExistingPlacement>();

            if (parent == null)
                return result;

            var groups =
                parent.AllSubFunctions
                      .Where(f =>
                            f.IsPlaced &&
                            f.FunctionCategory == FunctionCategory.DeviceEndTerminal)
                      .Select(f => f.Name)
                      .Select(name =>
                      {
                          var match = Regex.Match(
                              name,
                              @"L[123]\.(\d+)$");

                          if (!match.Success)
                              return -1;

                          return int.Parse(match.Groups[1].Value);
                      })
                      .Where(g => g > 0)
                      .Distinct()
                      .OrderBy(g => g)
                      .ToList();

            foreach (var g in groups)
            {
               

                result.Add(new ExistingPlacement
                {
                    Variant = g - 1
                });
            }

        

            return result;
        }


        private void DeleteOldPlacements(Function parent)
        {
            if (parent == null)
                return;

            // Liste bauen, weil wir während Iteration löschen
            List<Function> toDelete = new List<Function>();

            foreach (Function sub in parent.SubFunctions)
            {
                if (!sub.IsPlaced)
                    continue;

                // ✅ NUR Blackbox als Root nehmen
                if (sub.FunctionCategory == FunctionCategory.Blackbox)
                {
                    toDelete.Add(sub);
                }
            }

            foreach (Function bb in toDelete)
            {
                try
                {
                    // ✅ WICHTIG: komplette Platzierung löschen
                    Page page = bb.Page;

                    if (page != null)
                    {
                        var placements = page.AllPlacements;

                        foreach (Placement placement in placements)
                        {
                            Function f = placement as Function;

                            if (f == null)
                                continue;

                            // ✅ Alle Funktionen mit gleichem BMK entfernen
                            if (f.Name == bb.Name)
                            {
                                try
                                {
                                    f.Remove();
                                }
                                catch
                                {
                                    GlobalLogger.Warn("Teilweise Entfernen fehlgeschlagen: " + f.Name);
                                }
                            }
                        }
                    }
                }
                catch
                {
                    GlobalLogger.Warn("Blackbox komplett Löschen fehlgeschlagen");
                }
            }
        }


        private bool HasPlacement(PlaceholderLocation t)
        {
            var placements = t.Page.AllPlacements;

            foreach (Placement p in placements)
            {
                if (p is Function f)
                {
                    var loc = f.Location;
                    var tLoc = t.Location;

                    // ✅ Punktvergleich
                    if (Math.Abs(loc.X - tLoc.X) < 0.01 &&
                        Math.Abs(loc.Y - tLoc.Y) < 0.01)
                    {
                        return true;
                    }
                }
            }

            return false;
        }


    }
}

