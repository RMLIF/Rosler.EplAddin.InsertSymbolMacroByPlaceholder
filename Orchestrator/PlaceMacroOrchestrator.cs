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

            GlobalLogger.Info("Gesamt Pins Bedarf: " + totalPins);

            // ✅ NEU: freie Targets separat berechnen
            var freeTargets = targets
                .Where(t => !HasPlacement(t))
                .ToList();

            GlobalLogger.Info("Freie Targets: " + freeTargets.Count);



            SchienenPlan plan =
                _phasenschiene.PreparePhasenschienen(
                    page,
                    freeTargets);

            GlobalLogger.Info(
                $"PLAN SCHIENEN={plan.Schienen.Count}");

            foreach (var schiene in plan.Schienen)
            {
                GlobalLogger.Info(
                    $"PLAN SCHIENE={schiene.ParentFunction?.Name}");

                GlobalLogger.Info(
                    $"GESAMT={schiene.GesamtPlaetze}");

                GlobalLogger.Info(
                    $"BELEGT={schiene.BelegtePlaetze}");

                GlobalLogger.Info(
                    $"FREI={schiene.FreiePlaetze}");

                GlobalLogger.Info(
                    $"TARGETS={schiene.Targets.Count}");
            }

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


                // DEBUG HIER


                foreach (var target in t)
                {
                    foreach (Placement placement in target.Page.AllPlacements)
                    {
                        if (placement is BoxedDevice bd)
                        {
                            GlobalLogger.Info(
                                $"BOX: {bd.Name}");

                            GlobalLogger.Info(
                                $"ParentFunction: {bd.ParentFunction?.Name}");

                            foreach (Function f in bd.FunctionsInside)
                            {
                                GlobalLogger.Info(
                                    $"DUMP ID={f.ObjectIdentifier} NAME={f.Name}");

                                GlobalLogger.Info(
                                    $"FUNC Parent: {f.ParentFunction?.Name}");
                            }

                            // Nur RV-BoxedDevices analysieren
                            if (bd.Name.StartsWith("==RV"))
                            {
                                GlobalLogger.Info(
                                    $"*** RV BOX GEFUNDEN: {bd.Name} ***");

                                Function rootFunction =
                                    bd.FunctionsInside
                                      .FirstOrDefault(f => f.ParentFunction == null);

                                GlobalLogger.Info(
                                    $"ROOT FUNCTION: {rootFunction?.Name}");

                                if (rootFunction != null)
                                {
                                    GlobalLogger.Info(
                                        $"ROOT ID: {rootFunction.ObjectIdentifier}");
                                }

                                foreach (Function f in bd.FunctionsInside)
                                {
                                    GlobalLogger.Info(
                                        $"BOXFUNC ID={f.ObjectIdentifier} " +
                                        $"NAME={f.Name} " +
                                        $"PARENT={f.ParentFunction?.Name}");
                                }
                            }
                        }
                    }
                }


                try
                {

                    GlobalLogger.Info("VOR SafeFinalizeDevice");

                    foreach (var target in t)
                    {
                        foreach (Placement p in target.Page.AllPlacements)
                        {
                            if (p is BoxedDevice bd)
                            {
                                if (bd.Name.Contains("RV01"))
                                {
                                    GlobalLogger.Info(
                                        $"BEFORE FINALIZE BOX={bd.Name}");
                                }
                            }
                        }
                    }


                    GlobalLogger.Info("NACH SafeFinalizeDevice");

                    foreach (var target in t)
                    {
                        foreach (Placement p in target.Page.AllPlacements)
                        {
                            if (p is BoxedDevice bd)
                            {
                                if (bd.Name.Contains("RV01"))
                                {
                                    GlobalLogger.Info(
                                        $"AFTER FINALIZE BOX={bd.Name}");
                                }
                            }
                        }
                    }


                    GlobalLogger.Info("NACH SafeFinalizeDevice");

                    foreach (Function sub in func.AllSubFunctions)
                    {
                        GlobalLogger.Info(
                            $"SUB: {sub.Name}  Main={sub.IsMainFunction}");
                    }


                }
                catch
                {
                    GlobalLogger.Warn("UpdateDevice für Schiene fehlgeschlagen");
                }


            }




            // ✅🔥 HIER IST DER FIX
            GlobalLogger.Info("Korrigiere Hauptfunktionen...");
     
            GlobalLogger.Info("=== Workflow abgeschlossen ===");
        }

        private void ProcessSchiene(
            Function func,
            int variant,
            List<PlaceholderLocation> targets,
            string name)
        {
            GlobalLogger.Info("Platzierung " + name + " Targets=" + targets.Count);

            var macroLocal = LoadMacro(func);

            if (macroLocal == null)
            {
                GlobalLogger.Warn("Makro konnte nicht geladen werden");
                return;
            }

            _placement.PlaceAll(
                targets,
                new SchienenErgebnis
                {
                    ParentFunction = func,
                    VariantIndex = variant
                },
                macroLocal,
                _phasenschiene);

            // ✅ optional: Stabilisierung nach kompletter Schiene
            new DeviceService().UpdateDevice(func);
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

        private void Finalize(Project project, Function function)
        {
            new DeviceService().UpdateDevice(function);
            new Generate().Connections(project);
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
                GlobalLogger.Info(
                    $"FOUND EXISTING GROUP={g}");

                result.Add(new ExistingPlacement
                {
                    Variant = g - 1
                });
            }

            GlobalLogger.Info(
                $"BESTEHENDE GRUPPEN={groups.Count}");

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


        private List<List<PlaceholderLocation>> SplitTargets(List<PlaceholderLocation> targets, List<int> schienen)
        {
            List<List<PlaceholderLocation>> result = new List<List<PlaceholderLocation>>();

            int index = 0;

            foreach (int slots in schienen)
            {
                int maxTargets = slots / 3;

                List<PlaceholderLocation> list = new List<PlaceholderLocation>();

                for (int i = 0; i < maxTargets && index < targets.Count; i++)
                {
                    list.Add(targets[index]);
                    index++;
                }

                result.Add(list);
            }

            return result;
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


        private void FixMainFunctions(Function parentFunction)
        {

            GlobalLogger.Info("=== FIX MAIN START ===");

            foreach (var f in parentFunction.AllSubFunctions)
            {
                GlobalLogger.Info(
                    $"FIXMAIN {f.Name} " +
                    $"Cat={f.FunctionCategory} " +
                    $"Main={f.IsMainFunction}");
            }


            GlobalLogger.Info(
                $"PARENT NAME={parentFunction.Name}");

            GlobalLogger.Info(
                $"PARENT MAIN={parentFunction.IsMainFunction}");

            GlobalLogger.Info(
                $"PARENT CATEGORY={parentFunction.FunctionCategory}");

            GlobalLogger.Info(
                $"SUB COUNT={parentFunction.AllSubFunctions.Count()}");


            var allBlackboxes = parentFunction.AllSubFunctions
                .Where(f => f.FunctionCategory == FunctionCategory.Blackbox && f.IsPlaced)
                .ToList();

            if (allBlackboxes.Count == 0)
                return;

            // ✅ eine auswählen (z. B. erste)
            var main = allBlackboxes.First();

            foreach (var f in allBlackboxes)
            {
                f.LockObject();

                if (f == main)
                    f.IsMainFunction = true;
                else
                    f.IsMainFunction = false;
            }


            GlobalLogger.Info("=== FIX MAIN ENDE ===");

            foreach (var f in parentFunction.AllSubFunctions)
            {
                GlobalLogger.Info(
                    $"FIXMAIN END {f.Name} " +
                    $"Main={f.IsMainFunction}");
            }

        }


        public void SafeFinalizeDevice(Function parentFunction)
        {

            
                GlobalLogger.Info("SAFEFINALIZE ENTER");

                if (parentFunction == null)
                {
                    GlobalLogger.Info("SAFEFINALIZE NULL");
                    return;
                }

                GlobalLogger.Info("SAFEFINALIZE BEFORE EXISTINGMAIN");


            // -------------------------------------------------
            // ✅ 1. sicherstellen: mindestens 1 MainFunction
            // -------------------------------------------------

            GlobalLogger.Info("SAFEFINALIZE BEFORE EXISTINGMAIN");

            GlobalLogger.Info(
                $"PARENT NAME={parentFunction.Name}");

            GlobalLogger.Info(
                $"PARENT MAIN={parentFunction.IsMainFunction}");

            GlobalLogger.Info(
                $"PARENT PLACED={parentFunction.IsPlaced}");

            try
            {
                var subs = parentFunction.AllSubFunctions.ToArray();

                GlobalLogger.Info(
                    $"SUBS COUNT={subs.Length}");
            }
            catch (Exception ex)
            {
                GlobalLogger.Warn(
                    $"ALLSUBFUNCTIONS FAILED: {ex}");

                throw;
            }


            Function existingMain = null;


            foreach (var f in parentFunction.AllSubFunctions)
            {
                try
                {
                    GlobalLogger.Info($"CHECK MAIN {f.Name}");

                    bool isMain = f.IsMainFunction;

                    GlobalLogger.Info($"MAIN={isMain}");

                    bool isPlaced = f.IsPlaced;

                    GlobalLogger.Info($"PLACED={isPlaced}");

                    if (isMain && isPlaced)
                    {
                        existingMain = f;
                        break;
                    }
                }
                catch (Exception ex)
                {
                    GlobalLogger.Warn(
                        $"FAILED ON {f.Name}: {ex}");

                    throw;
                }
            }



           

            if (existingMain == null)
            {
                GlobalLogger.Info(
                    "NO MAIN SUBFUNCTION FOUND");

                GlobalLogger.Info(
                    $"PARENT IS MAIN={parentFunction.IsMainFunction}");
            }

            GlobalLogger.Info("SAFEFINALIZE STEP1");
            // -------------------------------------------------
            // ✅ 2. EINMAL synchronisieren (Gerät aufbauen)
            // -------------------------------------------------
            DeviceService ds = new DeviceService();
            ds.UpdateDevice(parentFunction);
            GlobalLogger.Info("SAFEFINALIZE STEP2");
            // -------------------------------------------------
            // ✅ 3. Namen & Struktur sauber berechnen
            // -------------------------------------------------
            NameService ns = new NameService(parentFunction.Page);

            foreach (Function f in parentFunction.AllSubFunctions)
            {
                if (!f.IsPlaced)
                    continue;

                // 🔥 jetzt kennt EPLAN das Gerät → korrekt berechnen!
                ns.AdjustFullName(f);
            }
            GlobalLogger.Info("SAFEFINALIZE STEP3");
           
        }

    }
}

