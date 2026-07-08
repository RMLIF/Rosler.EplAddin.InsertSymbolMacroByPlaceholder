using Eplan.EplApi.ApplicationFramework;
using Eplan.EplApi.Base;
using Eplan.EplApi.Base.Enums;
using Eplan.EplApi.DataModel;
using Eplan.EplApi.DataModel.Graphics;
using Eplan.EplApi.DataModel.MasterData;
using Eplan.EplApi.HEServices;
using Eplan.EplApi.MasterData;
using Rosler.EplAddin.InsertSymbolMacroByPlaceholder.Infrastructure;
using Rosler.EplAddin.InsertSymbolMacroByPlaceholder.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Contexts;
using static Eplan.EplApi.HEServices.PartsService;
using static Eplan.EplApi.HEServices.Renumber.Enums;
using DMProps = Eplan.EplApi.DataModel.Properties;


namespace Rosler.EplAddin.InsertSymbolMacroByPlaceholder.Services
{
    public class PhasenschienenService
    {
        private const int MAX_VARIANT_INDEX = 4;



        public SchienenErgebnis GetOrPreparePhasenschiene(Page page, int requiredSlots)
        {
            GlobalLogger.Info("Starte Phasenschienen-Ermittlung");

            BaseContext context = FindBasePhasenschiene(page);

            Function existingFunction = null;
           

            if (context.Reference != null)
                existingFunction = context.Reference.ParentObject as Function;

            int occupiedPins = GetCurrentOccupiedPins(existingFunction);


            GlobalLogger.Info("Bereits belegte Pins: " + occupiedPins);
            GlobalLogger.Info("Neue benötigte Pins: " + requiredSlots);

            int totalNeededSlots = occupiedPins + requiredSlots;

            GlobalLogger.Info("Gesamt benötigte Pins: " + totalNeededSlots);

            List<PartCandidate> parts = LoadMatchingParts(context);

            GlobalLogger.Info("Gefundene Artikel: " + parts.Count);

            PartCandidate best = SelectBestPart(parts, totalNeededSlots);

            if (best == null)
            {
                GlobalLogger.Error("Kein passender Artikel gefunden");
            }

            GlobalLogger.Info("Ausgewählter Artikel: " + best.Part.PartNr);

            Function function = ApplySelection(page.Project, best, context);

            int variant = CalculateVariantIndex(function);

            GlobalLogger.Info("VariantIndex: " + variant);

            return new SchienenErgebnis
            {
                ParentFunction = function,
                VariantIndex = variant
            };
        }



        private BaseContext FindBasePhasenschiene(Page page)
        {
            DMObjectsFinder finder = new DMObjectsFinder(page.Project);
            var articles = finder.GetArticleReferences(null);

            foreach (var artRef in articles)
            {
                if (!artRef.IdentifyingName.Contains("WC"))
                    continue;

                var func = artRef.ParentObject as Function;

                if (func != null && func.IsMainFunction && artRef.Article != null)
                {

                    GlobalLogger.Info(
                            $"WC gefunden: {artRef.PartNr}");

                    return new BaseContext
                    {
                        Manufacturer = artRef.Article.Properties.ARTICLE_MANUFACTURER.ToString(),
                        ProductGroup = artRef.Article.Properties.ARTICLE_PRODUCTGROUP.ToString(),
                        Reference = artRef
                    };
                }
            }

            return new BaseContext();
        }
        public List<PartCandidate> LoadMatchingParts(BaseContext ctx)
        {
            List<PartCandidate> result = new List<PartCandidate>();

            using (var db = new MDPartsManagement().OpenDatabase())
            {
                MDObjectFilter filter = new MDObjectFilter();

                filter.AddPropertyCondition(22041, MDObjectFilter.CompareOperator.OperatorEqual, ctx.ProductGroup ?? "129");
                filter.AddPropertyCondition(22007, MDObjectFilter.CompareOperator.OperatorEqual, ctx.Manufacturer);

                var parts = db.GetParts(filter);

                foreach (var part in parts)
                {
                    int slots = part.FunctionTemplatePositions.Count(p =>
                        !p.FunctionDefinitionCategory.ToString()
                        .Equals("Blackbox", StringComparison.OrdinalIgnoreCase));

                    result.Add(new PartCandidate
                    {
                        Part = part,
                        Slots = slots
                    });
                }
            }

            return result;
        }

        private PartCandidate SelectBestPart(List<PartCandidate> parts, int requiredSlots)
        {
            var best = parts
                .Where(p => p.Slots >= requiredSlots)
                .OrderBy(p => p.Slots)
                .FirstOrDefault();

            if (best != null)
                return best;

            return parts.OrderByDescending(p => p.Slots).FirstOrDefault();
        }

        private Function ApplySelection(Project project, PartCandidate best, BaseContext ctx)
        {
            if (best == null)

                throw new BaseException(
                    "Kein Artikel gefunden.",
                    MessageLevel.Error
                );


            Function function = null;

            if (ctx.Reference != null)
                function = ctx.Reference.ParentObject as Function;

            if (function == null)
                return CreateNewDevice(project, best.Part.PartNr);

            var refs = function.ArticleReferences;
            ArticleReference currentRef = null;

            if (refs != null && refs.Length > 0)
                currentRef = refs[0];

            if (currentRef == null || currentRef.PartNr != best.Part.PartNr)
            {

                using (var sp = SafetyPoint.Create())
                {
                    GlobalLogger.Info(
                        "SET ARTICLE START");

                    GlobalLogger.Info(
                        "FUNCTION=" + function.Name);

                    GlobalLogger.Info(
                        "FUNCTION ID=" + function.ObjectIdentifier);

                    GlobalLogger.Info(
                        "FUNCTION CATEGORY=" + function.FunctionCategory);

                    GlobalLogger.Info(
                        "FUNCTION MAIN=" + function.IsMainFunction);

                    GlobalLogger.Info(
                        "NEW ARTICLE=" + best.Part.PartNr);

                    GlobalLogger.Info(
                        "NEW VARIANT=" + best.Part.Variant);

                    if (currentRef != null)
                    {
                        GlobalLogger.Info(
                            "CURRENT ARTICLE=" + currentRef.PartNr);

                        currentRef.PartNr = best.Part.PartNr;
                        currentRef.VariantNr = best.Part.Variant;
                        currentRef.Count = 1;
                        currentRef.StoreToObject();

                        GlobalLogger.Info(
                            "ARTICLE UPDATED");
                    }
                    else
                    {
                        GlobalLogger.Info(
                            "NO ARTICLE REFERENCE -> ADD ARTICLE");

                        function.AddArticleReference(
                            best.Part.PartNr,
                            best.Part.Variant,
                            1,
                            false);

                        GlobalLogger.Info(
                            "ARTICLE ADDED");
                    }

                    GlobalLogger.Info(
                        "SET ARTICLE END");

                    sp.Commit();
                }



                GlobalLogger.Info("==== ARTIKEL VOR UPDATEDEVICE ====");

                foreach (var ar in function.ArticleReferences)
                {
                    GlobalLogger.Info(
                        $"PartNr={ar.PartNr} Variant={ar.VariantNr}");
                }


                GlobalLogger.Info(
                    $"Ausgewählter Artikel: {best.Part.PartNr}");

                GlobalLogger.Info(
                    $"Typnummer: {best.Part.Properties.ARTICLE_TYPENR}");

                GlobalLogger.Info(
                    $"Hersteller: {best.Part.Properties.ARTICLE_MANUFACTURER}");



                new DeviceService().UpdateDevice(function);


                GlobalLogger.Info("NACH UPDATEDEVICE");

                foreach (var ar in function.ArticleReferences)
                {
                    GlobalLogger.Info($"ARTREF={ar.PartNr}");

                }
            }

                return function;
        }

        private Function CreateNewDevice(Project project, string partNumber)
        {
            DeviceService devService = new DeviceService();

            var created = devService.CreateDevice(project, partNumber, "1", new FunctionPropertyList());

            if (created != null && created.Length > 0)
                return created[0];

            return null;
        }

        private int CalculateVariantIndex(Function function)
        {
            if (function == null) return 0;

            int occupied = function.SubFunctions.Count(f => f.IsPlaced);

            int variant = occupied / 3;

            if (variant > MAX_VARIANT_INDEX)
                variant = MAX_VARIANT_INDEX;

            return variant;
        }

        



        private int GetCurrentOccupiedPins(Function function)
        {
            if (function == null)
                return 0;

            int count = 0;

            foreach (Function sub in function.SubFunctions)
            {
                if (!sub.IsPlaced)
                    continue;

                // ✅ KEINE Blackbox zählen
                if (sub.FunctionCategory == FunctionCategory.Blackbox)
                    continue;

                count++;
            }

            return count;
        }

       

        public void UpdateMacroPlacements(
    Page targetPage,
    Function parentFunction,
    StorableObject[] newPlacements)
        {
            GlobalLogger.Info(
                "################ UPDATE MACRO PLACEMENTS ################");

            if (parentFunction == null || newPlacements == null)
                return;

            NameService nameService = new NameService(targetPage);

            bool firstBlackboxHandled = false;

            foreach (StorableObject obj in newPlacements)
            {
                // =====================================================
                // BOXED DEVICE
                // =====================================================

                if (obj is BoxedDevice bd)
                {
                    try
                    {
                        using (var sp = SafetyPoint.Create())
                        {
                            GlobalLogger.Info(
                                $"BOXEDDEVICE ALT: {bd.Name}");



                            foreach (Function func in bd.FunctionsInside)
                            {
                                GlobalLogger.Info(
                                    $"VOR RENAME ID={func.ObjectIdentifier} NAME={func.Name}");
                            }



                            GlobalLogger.Info(
                                $"BOXEDDEVICE ALT: {bd.Name}");

                            foreach (var func in bd.FunctionsInside)
                            {
                                if (func.FunctionCategory != FunctionCategory.Blackbox)
                                    continue;

                                GlobalLogger.Info(
                                    $"BLACKBOX VOR RENAME ID={func.ObjectIdentifier} " +
                                    $"NAME={func.Name} " +
                                    $"MAIN={func.IsMainFunction}");


                                GlobalLogger.Info(
                                    $"ARTICLE_REF_COUNT={func.ArticleReferences.Length}");

                                foreach (var ar in func.ArticleReferences)
                                {
                                    GlobalLogger.Info(
                                        $"ARTIKEL={ar.PartNr} VARIANT={ar.VariantNr}");
                                }


                                try
                                {
                                    GlobalLogger.Info(
                                        $"ARTICLE_REF_COUNT={func.ArticleReferences.Length}");

                                    foreach (var ar in func.ArticleReferences)
                                    {
                                        GlobalLogger.Info(
                                            $"ARTIKEL={ar.PartNr} VARIANT={ar.VariantNr}");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    GlobalLogger.Info(
                                        $"ARTICLE_REFERENCE ERROR={ex.Message}");
                                }
                            }


                            foreach (Function func in bd.FunctionsInside)
                            {
                                GlobalLogger.Info(
                                    $"VOR RENAME ID={func.ObjectIdentifier} NAME={func.Name}");
                            }


                            GlobalLogger.Info(
                                $"BOX NAME={bd.Name}");

                            foreach (var p in newPlacements.OfType<Function>())
                            {
                                GlobalLogger.Info(
                                    $"PLACEMENT FUNC={p.Name}");

                                GlobalLogger.Info(
                                    $"PLACEMENT CAT={p.FunctionCategory}");

                                GlobalLogger.Info(
                                    $"PLACEMENT MAIN={p.IsMainFunction}");
                            }



                            var renamingDeviceService =
                                new RenamingDeviceService();

                            bool result =
                                renamingDeviceService.RenameBoxedDeviceFromParent(
                                    bd,
                                    parentFunction);

                            GlobalLogger.Info(
                                $"RENAME RESULT: {result}");


                            GlobalLogger.Info(
                                $"BOXEDDEVICE NACH RENAME: {bd.Name}");



                            GlobalLogger.Info("===== PARENT CHECK =====");

                            GlobalLogger.Info(
                                $"BOX={bd.Name}");

                            GlobalLogger.Info(
                                $"BOX_PARENT={bd.ParentFunction?.Name}");

                            GlobalLogger.Info(
                                $"SUBCOUNT={bd.AllSubFunctions.Count()}");

                            foreach (var sub in bd.AllSubFunctions)
                            {
                                GlobalLogger.Info(
                                    $"SUB={sub.Name}");

                                GlobalLogger.Info(
                                    $"SUB_PARENT={sub.ParentFunction?.Name}");

                                GlobalLogger.Info(
                                    $"PLACED={sub.IsPlaced}");

                                GlobalLogger.Info(
                                    $"MAIN={sub.IsMainFunction}");
                            }



                            foreach (var func in bd.FunctionsInside)
                            {
                                if (func.FunctionCategory != FunctionCategory.Blackbox)
                                    continue;

                                GlobalLogger.Info(
                                    $"BLACKBOX NACH RENAME ID={func.ObjectIdentifier} " +
                                    $"NAME={func.Name} " +
                                    $"MAIN={func.IsMainFunction}");


                                GlobalLogger.Info(
                                    $"ARTICLE_REF_COUNT={func.ArticleReferences.Length}");

                                foreach (var ar in func.ArticleReferences)
                                {
                                    GlobalLogger.Info(
                                        $"ARTIKEL={ar.PartNr} VARIANT={ar.VariantNr}");
                                }


                                try
                                {
                                    GlobalLogger.Info(
                                        $"ARTICLE_REF_COUNT={func.ArticleReferences.Length}");

                                    foreach (var ar in func.ArticleReferences)
                                    {
                                        GlobalLogger.Info(
                                            $"ARTIKEL={ar.PartNr} VARIANT={ar.VariantNr}");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    GlobalLogger.Info(
                                        $"ARTICLE_REFERENCE ERROR={ex.Message}");
                                }
                            }


                            GlobalLogger.Info(
                                $"BOXEDDEVICE NACH RENAME: {bd.Name}");

                            foreach (var func in bd.FunctionsInside)
                            {
                                if (func.FunctionCategory != FunctionCategory.Blackbox)
                                    continue;

                                GlobalLogger.Info(
                                    $"BLACKBOX NACH RENAME ID={func.ObjectIdentifier} " +
                                    $"NAME={func.Name} " +
                                    $"MAIN={func.IsMainFunction}");

                                foreach (var ar in func.ArticleReferences)
                                {
                                    GlobalLogger.Info(
                                        $"   ARTIKEL={ar.PartNr}");
                                }
                            }

                            foreach (Function func in bd.FunctionsInside)
                            {
                                GlobalLogger.Info(
                                    $"NACH RENAME ID={func.ObjectIdentifier} NAME={func.Name}");
                            }


                            sp.Commit();
                        }
                    }
                    catch (Exception ex)
                    {
                        GlobalLogger.Warn(
                            $"BOXEDDEVICE RENAME FEHLER: {ex}");
                    }

                    try
                    {

                        GlobalLogger.Info("VOR UPDATEDEVICE");
                        GlobalLogger.Info(
                            "BMK=" + parentFunction.Name);

                        GlobalLogger.Info(
                            "MAIN=" + parentFunction.IsMainFunction);

                        new DeviceService().UpdateDevice(parentFunction);

                        GlobalLogger.Info("NACH UPDATEDEVICE");
                        GlobalLogger.Info(
                            "BMK=" + parentFunction.Name);

                        GlobalLogger.Info(
                            "MAIN=" + parentFunction.IsMainFunction);

                    }
                    catch (Exception ex)
                    {
                        GlobalLogger.Warn(
                            $"UPDATEDEVICE FEHLER: {ex.Message}");
                    }

                    continue;
                }

                // =====================================================
                // NORMALE FUNKTIONEN
                // =====================================================

                Function f = obj as Function;

                if (f == null)
                    continue;

                try
                {
                    using (var sp = SafetyPoint.Create())
                    {
                        AssignToDeviceRecursively(
                            f,
                            parentFunction.Name,
                            nameService,
                            ref firstBlackboxHandled);

                        sp.Commit();
                    }
                }
                catch (Exception ex)
                {
                    GlobalLogger.Warn(
                        $"ASSIGN FEHLER: {ex}");
                }

                try
                {

                    GlobalLogger.Info("VOR UPDATEDEVICE");
                    GlobalLogger.Info(
                        "BMK=" + parentFunction.Name);

                    GlobalLogger.Info(
                        "MAIN=" + parentFunction.IsMainFunction);

                    new DeviceService().UpdateDevice(parentFunction);

                    GlobalLogger.Info("NACH UPDATEDEVICE");
                    GlobalLogger.Info(
                        "BMK=" + parentFunction.Name);

                    GlobalLogger.Info(
                        "MAIN=" + parentFunction.IsMainFunction);

                }
                catch
                {
                    GlobalLogger.Warn(
                        "UpdateDevice nach Placement fehlgeschlagen");
                }
            }

            // =====================================================
            // DEBUG CHECK
            // =====================================================

            foreach (var sub in parentFunction.SubFunctions)
            {
                GlobalLogger.Info(
                    $"SUB: {sub.Name} Main={sub.IsMainFunction}");

                if (string.IsNullOrEmpty(sub.Name))
                {
                    GlobalLogger.Warn(
                        "Funktion ohne BMK gefunden!");
                }
            }
        }

      

        public Function CreatePhasenschiene(
    Project project,
    string partNumber,
    string variant,
    Page targetPage,
    BaseContext context)
        {
            try
            {
                //// ✅ Funktionsdefinitionsbibliothek laden

                FunctionDefinitionLibrary funcDefLib = project.FunctionDefinitionLibrary;
                

                // ✅ Blackbox-Definition holen (Standard-Gerätekasten)
                FunctionDefinition blackBoxDef = new FunctionDefinition(
                    funcDefLib,
                    FunctionCategory.Blackbox,
                    1,
                    1
                );

                // ✅ Funktion im Projekt erzeugen (LOGISCH – noch nicht platziert)
                Function mainFunction = Function.Create(blackBoxDef, project);

                mainFunction.LockObject();

                // ----------------------------------------
                // ✅ Struktur übernehmen
                string struktur = "";

                if (context.Reference != null)
                {
                    var existingFunc = context.Reference.ParentObject as Function;

                    if (existingFunc != null && !string.IsNullOrEmpty(existingFunc.Name))
                    {
                        int splitIndex = existingFunc.Name.LastIndexOf('-');

                        if (splitIndex > 0)
                        {
                            struktur = existingFunc.Name.Substring(0, splitIndex);
                        }
                    }
                }

                string baseName = !string.IsNullOrEmpty(struktur)
                    ? struktur + "-WC"
                    : "WC";

                // ✅ Index bestimmen
                int maxIndex = 0;

                DMObjectsFinder finder = new DMObjectsFinder(project);
                Function[] functions = finder.GetFunctions(null);

                foreach (Function f in functions)
                {
                    if (!string.IsNullOrEmpty(f.Name) && f.Name.StartsWith(baseName))
                    {
                        string numberPart = f.Name.Substring(baseName.Length);

                        if (int.TryParse(numberPart, out int num))
                        {
                            if (num > maxIndex)
                                maxIndex = num;
                        }
                    }
                }

                int newIndex = maxIndex + 1;
                string fullName = baseName + newIndex;

                // ✅ BMK setzen
                mainFunction.Name = fullName;
                mainFunction.VisibleName = "";

                // ✅ Artikel zuweisen
                mainFunction.AddArticleReference(
                    partNumber,
                    variant,
                    1,
                    false);

                // ✅ Gerät aus Funktionsschablonen erzeugen
                DeviceService devService = new DeviceService();
                devService.UpdateDevice(mainFunction);

                GlobalLogger.Info("Neue Phasenschiene erzeugt (reine Blackbox + Artikel): " + mainFunction.Name);

                return mainFunction;
            }
            catch (Exception ex)
            {
                GlobalLogger.Error("Fehler beim Erzeugen der Phasenschiene: " + ex.Message);
            }

            return null;
        }


        public List<int> SplitSlotsToSchienen(List<PartCandidate> parts, int totalPins)
        {
            List<int> result = new List<int>();

            if (parts == null || parts.Count == 0)
                return result;

            int maxSlots = parts.Max(p => p.Slots);

            int remaining = totalPins;

            while (remaining > 0)
            {
                // ✅ Verbindung braucht 3 Pins extra (nicht bei erster Schiene)
                if (result.Count > 0)
                {
                    remaining += 3;
                }

                int use = Math.Min(maxSlots, remaining);

                result.Add(use);

                remaining -= use;
            }

            return result;
        }


        public List<PartCandidate> GetParts(BaseContext ctx)
        {
            return LoadMatchingParts(ctx);
        }

        public BaseContext GetBaseContext(Page page)
        {
            return FindBasePhasenschiene(page);
        }



        private void AssignToDeviceRecursively(
    Function f,
    string deviceName,
    NameService nameService,
    ref bool firstBlackboxHandled)
        {
            if (f == null)
                return;

            // ✅ KEIN LockObject!

            // ✅ BMK setzen
            f.Name = deviceName;
            f.VisibleName = "";

            nameService.AdjustFullName(f);

            if (f.FunctionCategory == FunctionCategory.Blackbox)
            {
                if (!firstBlackboxHandled)
                {
                    firstBlackboxHandled = true;
                }
                else
                {
                    f.IsMainFunction = false;
                }
            }

            // ✅ REKURSIV für komplette Makrostruktur
            foreach (Function sub in f.SubFunctions)
            {
                AssignToDeviceRecursively(
                    sub,
                    deviceName,
                    nameService,
                    ref firstBlackboxHandled);
            }
        }



        public SchienenPlan PreparePhasenschienen(
     Page page,
     List<PlaceholderLocation> targets)
        {
            // -------------------------------------------------
            // Schritt 1: Plan anlegen
            // -------------------------------------------------

            SchienenPlan plan = new SchienenPlan();

            List<Function> rails =
                FindExistingPhasenschienen(page.Project);

            GlobalLogger.Info(
                $"RAILS FOUND={rails.Count}");

            foreach (Function rail in rails)
            {
                GlobalLogger.Info(
                    $"RAIL FOUND={rail.Name}");
            }


            int requiredPins =
                CalculateRequiredPins(targets.Count);

            GlobalLogger.Info(
                $"PLACEHOLDER={targets.Count}");

            GlobalLogger.Info(
                $"REQUIRED PINS={requiredPins}");

            // -------------------------------------------------
            // Schritt 2: Vorhandene Schienen analysieren
            // -------------------------------------------------

            foreach (Function rail in rails)
            {
                int slots =
                    GetSlots(rail);

                int occupied =
                    GetOccupiedPins(rail);

                int free =
                    GetFreePins(rail);

                GlobalLogger.Info(
                    $"RAIL={rail.Name}");

                GlobalLogger.Info(
                    $"SLOTS={slots}");

                GlobalLogger.Info(
                    $"OCCUPIED={occupied}");

                GlobalLogger.Info(
                    $"FREE={free}");

                plan.Schienen.Add(
                    new SchienenEintrag
                    {
                        ParentFunction = rail,
                        GesamtPlaetze = slots,
                        BelegtePlaetze = occupied,
                        FreiePlaetze = free
                    });
            }

           

            int occupiedPins =
                plan.Schienen.Sum(
                    s => s.BelegtePlaetze);

            int newPins =
                CalculateRequiredPins(
                    targets.Count);

            int totalRequiredPins =
                occupiedPins + newPins;

            int totalCapacityPins =
                plan.Schienen.Sum(
                    s => s.GesamtPlaetze);

            int totalFreePins =
                plan.Schienen.Sum(
                    s => s.FreiePlaetze);

            GlobalLogger.Info(
                $"OCCUPIED PINS={occupiedPins}");

            GlobalLogger.Info(
                $"NEW PINS={newPins}");

            GlobalLogger.Info(
                $"TOTAL REQUIRED PINS={totalRequiredPins}");

            GlobalLogger.Info(
                $"TOTAL CAPACITY PINS={totalCapacityPins}");

            GlobalLogger.Info(
                $"TOTAL FREE PINS={totalFreePins}");



           


            List<PartCandidate> parts =
                LoadMatchingParts(
                    GetBaseContext(page));

            List<int> railSizes =
                CalculateRailSizes(
                    totalRequiredPins,
                    parts);

            GlobalLogger.Info(
                $"PLANNED RAIL COUNT={railSizes.Count}");

            foreach (int size in railSizes)
            {
                GlobalLogger.Info(
                    $"PLANNED RAIL SIZE={size}");
            }


            if (railSizes.Count == 1)
            {
                GlobalLogger.Info(
                    "EINE SCHIENE AUSREICHEND");


                if (railSizes.Count == 1)
                {
                    int targetSize = railSizes[0];

                    PartCandidate bestPart =
                        parts.FirstOrDefault(
                            p => p.Slots == targetSize);

                    if (bestPart != null &&
                        rails.Count > 0)
                    {
                        UpgradeExistingPhasenschiene(
                            rails[0],
                            bestPart);
                    }
                }

            }

            else
            {
                GlobalLogger.Info(
                    "MEHRERE SCHIENEN ERFORDERLICH");

                Function firstRail =
                    rails.First();

                PartCandidate firstPart =
                    parts.First(
                        p => p.Slots == railSizes[0]);

                UpgradeExistingPhasenschiene(
                    firstRail,
                    firstPart);

                // Anzahl der Targets merken,
                // die bereits auf WC1 liegen
                int consumedTargets =
                    (railSizes[0] - GetOccupiedPins(firstRail)) / 3;

                for (int i = 1; i < railSizes.Count; i++)
                {
                    int size =
                        railSizes[i];

                    PartCandidate part =
                        parts.FirstOrDefault(
                            p => p.Slots == size);

                    if (part == null)
                        continue;

                    // -----------------------------------
                    // Letzten verwendeten Anker der
                    // vorherigen Schiene bestimmen
                    // -----------------------------------

                    PlaceholderLocation lastAnchor =
                        targets[consumedTargets - 1];

                    GlobalLogger.Info(
                        $"LETZTER ANKER={consumedTargets}");


                    // -----------------------------------
                    // Übergangsmakro platzieren
                    // -----------------------------------

                    PlaceholderLocation transitionAnchor =
                        PlaceTransitionMacro(
                            lastAnchor);

                    GlobalLogger.Info(
                        "UEBERGANGSMAKRO PLATZIERT");

                    new CommandLineInterpreter()
                        .Execute("gedRedraw");

                    if (transitionAnchor == null)
                    {
                        GlobalLogger.Error(
                            "KEIN UEBERGANGSANKER GEFUNDEN");

                        continue;
                    }

                   


                    // -----------------------------------
                    // Neue Schiene erzeugen
                    // -----------------------------------

                    Function newRail =
                        CreatePhasenschiene(
                            page.Project,
                            part.Part.PartNr,
                            part.Part.Variant,
                            transitionAnchor.Page,
                            GetBaseContext(page));

                    if (newRail == null)
                        continue;

                    // -----------------------------------
                    // Schieneneintrag erzeugen
                    // -----------------------------------

                    SchienenEintrag newEntry =
                        new SchienenEintrag
                        {
                            ParentFunction = newRail,
                            GesamtPlaetze = part.Slots,
                            BelegtePlaetze = GetBridgePins(),
                            FreiePlaetze =
                                part.Slots - GetBridgePins(),
                            IstNeueSchiene = true
                        };

                    // Übergangsanker als erstes Target merken
                    newEntry.Targets.Add(
                        transitionAnchor);

                    plan.Schienen.Add(
                        newEntry);

                    GlobalLogger.Info(
                        $"UEBERGANGSANKER ZUGEORDNET: {transitionAnchor.Page.Name}");

                    GlobalLogger.Info(
                        $"NEUE SCHIENE={newRail.Name}");

                    // -----------------------------------
                    // Targets dieser Schiene aufaddieren
                    // -----------------------------------

                    int targetsOnThisRail =
                        (size - GetBridgePins()) / 3;

                    consumedTargets +=
                        targetsOnThisRail;



                }
            }


               

                // -------------------------------------------------
                // Schritt 6: Targets auf Schienen verteilen
                // -------------------------------------------------


                DistributeTargets(
                plan,
                targets);


            

            // -------------------------------------------------
            // Fertig
            // -------------------------------------------------

            return plan;
        }


        public List<Function> FindExistingPhasenschienen(
     Project project)
        {
            return project.Pages
                .SelectMany(p => p.AllPlacements)
                .OfType<Function>()
                .Where(f =>
                    f.IsMainFunction &&
                    !string.IsNullOrEmpty(f.Name) &&
                    f.Name.Contains("-WC") &&
                    f.ArticleReferences != null &&
                    f.ArticleReferences.Length > 0)
                .ToList();
        }



        public int GetOccupiedPins(
            Function schiene)
        {
            return schiene.SubFunctions
                .Count(f =>
                    f.IsPlaced &&
                    f.FunctionCategory !=
                        FunctionCategory.Blackbox);
        }


        public int GetSlots(
            Function schiene)
        {

            if (schiene == null)
                return 0;

            if (schiene.ArticleReferences == null)
                return 0;

            if (schiene.ArticleReferences.Length == 0)
                return 0;

            string article =
                schiene.ArticleReferences[0].PartNr;


            var context =
                GetBaseContext(schiene.Page);

            var parts =
                LoadMatchingParts(context);

            var part =
                parts.FirstOrDefault(
                    p => p.Part.PartNr == article);

            return part?.Slots ?? 0;
        }


        public int GetFreePins(
            Function schiene)
        {
            return GetSlots(schiene)
                   - GetOccupiedPins(schiene);
        }


        public int CalculateRequiredPins(
            int placeholderCount)
        {
            return placeholderCount * 3;
        }



        public PartCandidate FindBestPart(
            int requiredPins,
            BaseContext context)
        {
            return LoadMatchingParts(context)
                .Where(p => p.Slots >= requiredPins)
                .OrderBy(p => p.Slots)
                .FirstOrDefault();
        }



        public Function UpgradeExistingPhasenschiene(
    Function function,
    PartCandidate best)
        {
            if (function == null)
                return null;

            var refs = function.ArticleReferences;

            ArticleReference currentRef =
                refs != null && refs.Length > 0
                    ? refs[0]
                    : null;

            using (var sp = SafetyPoint.Create())
            {
                if (currentRef != null)
                {
                    currentRef.PartNr =
                        best.Part.PartNr;

                    currentRef.VariantNr =
                        best.Part.Variant;

                    currentRef.Count = 1;

                    currentRef.StoreToObject();
                }
                else
                {
                    function.AddArticleReference(
                        best.Part.PartNr,
                        best.Part.Variant,
                        1,
                        false);
                }

                sp.Commit();
            }

            new DeviceService()
                .UpdateDevice(function);

            return function;
        }


        public bool CanUpgradeExistingRail(
            Function schiene,
            int requiredPins)
        {
            return GetSlots(schiene)
                   < requiredPins;
        }

       

        public List<int> CalculateRailSizes(
    int totalRequiredPins,
    List<PartCandidate> parts)
        {
            List<int> result = new List<int>();

            int remaining = totalRequiredPins;

            while (remaining > 0)
            {
                var best = parts
                    .Where(p => p.Slots >= remaining)
                    .OrderBy(p => p.Slots)
                    .FirstOrDefault();

                if (best != null)
                {
                    result.Add(best.Slots);
                    break;
                }

                var largest = parts
                    .OrderByDescending(p => p.Slots)
                    .First();

                result.Add(largest.Slots);

                remaining -= largest.Slots;

                // Für jede weitere Schiene müssen die Brückenpins
                // zusätzlich berücksichtigt werden
                if (remaining > 0)
                {
                    remaining += GetBridgePins();
                }
            }

            return result;
        }

        public Dictionary<Function,
        List<PlaceholderLocation>>

        DistributeTargets(
         List<PlaceholderLocation> targets,
        List<Function> schienen)
        {

            Dictionary<Function,
                List<PlaceholderLocation>> result =
                    new Dictionary<Function, List<PlaceholderLocation>>();


            int index = 0;

            foreach (var schiene in schienen)
            {
                result[schiene] =
                    new List<PlaceholderLocation>();

                int freeTargets =
                    GetFreePins(schiene) / 3;

                while (freeTargets > 0 &&
                       index < targets.Count)
                {
                    result[schiene]
                        .Add(targets[index]);

                    index++;
                    freeTargets--;
                }
            }

            return result;
        }


        public int GetBridgePins()
        {
            return 3;
        }

        private PartCandidate SelectOptimalRail(List<PartCandidate> parts, int requiredSlots)
        {
            var best = parts
                .Where(p => p.Slots >= requiredSlots)
                .OrderBy(p => p.Slots)
                .FirstOrDefault();

            if (best != null)
                return best;

            return parts.OrderByDescending(p => p.Slots).FirstOrDefault();
        }


        private void DistributeTargets(
            SchienenPlan plan,
            List<PlaceholderLocation> targets)
        {
            int index = 0;

            foreach (SchienenEintrag schiene
                     in plan.Schienen)
            {
                int freieTargets =
                    schiene.FreiePlaetze / 3;

                while (freieTargets > 0 &&
                       index < targets.Count)
                {
                    schiene.Targets.Add(
                        targets[index]);

                    index++;
                    freieTargets--;
                }

                GlobalLogger.Info(
                    $"TARGETS FUER {schiene.ParentFunction?.Name} = {schiene.Targets.Count}");
            }

            if (index < targets.Count)
            {
                GlobalLogger.Warn(
                    $"NICHT VERTEILTE TARGETS={targets.Count - index}");
            }
        }





        private PlaceholderLocation PlaceTransitionMacro(
            PlaceholderLocation target)
        {

            const string macroPath =
                @"D:\Data\Macros\ROSLER\General\Verbinungsmakros\Phasenschienen.ems";

            SymbolMacro macro =
                new SymbolMacro();

            macro.Open(
                macroPath);

            Insert insert =
                new Insert();

            var inserted =
                insert.SymbolMacro(
                    macro,
                    0,
                    target.Page,
                    target.Location,
                    Insert.MoveKind.Absolute,
                    SymbolMacro.Enums.NumerationMode.None);

            GlobalLogger.Info(
                $"UEBERGANGSMAKRO PLATZIERT AUF {target.Page.Name}");

            GlobalLogger.Info(
                $"INSERTED COUNT={inserted.Length}");

            foreach (var obj in inserted)
            {
                GlobalLogger.Info(
                    $"TYPE={obj.GetType().FullName}");
            }


            PlaceholderLocation transitionAnchor = null;

            foreach (var obj in inserted)
            {
                if (obj is PlaceHolder ph)
                {
                    GlobalLogger.Info(
                        $"PH FOUND={ph.Name}");

                    if (ph.Name == "Anker Phasenschiene")
                    {
                        transitionAnchor =
                            new PlaceholderLocation(
                                ph.Page,
                                ph.Location);

                        GlobalLogger.Info(
                            $"DIRECT TRANSITION ANCHOR={ph.Page.Name}");

                        break;
                    }
                }
            }

            return transitionAnchor;


        }

        public void CleanupDuplicateWCMainFunctions(
     Project project)
        {
            DMObjectsFinder finder =
                new DMObjectsFinder(project);

            var wcFunctions =
                finder.GetFunctions(null)
                      .Where(f =>
                          f.IsMainFunction &&
                          !string.IsNullOrEmpty(f.Name) &&
                          f.Name.Contains("-WC"))
                      .GroupBy(f => f.Name);

            foreach (var group in wcFunctions)
            {
                Function placed =
                    group.FirstOrDefault(f => f.IsPlaced);

                Function unplaced =
                    group.FirstOrDefault(f => !f.IsPlaced);

                if (placed == null || unplaced == null)
                    continue;

                GlobalLogger.Info(
                    $"DELETE DUPLICATE WC MAIN FUNCTION: {unplaced.Name}");

                unplaced.Remove();
            }
        }







    }


}

