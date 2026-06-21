using Eplan.EplApi.ApplicationFramework;
using Eplan.EplApi.Base;
using Eplan.EplApi.Base.Enums;
using Eplan.EplApi.DataModel;
using Eplan.EplApi.DataModel.Graphics;
using Eplan.EplApi.DataModel.MasterData;
using Eplan.EplApi.HEServices;
using System;
using System.Collections.Generic;
using System.IO;

public class PlaceMacroAtPlaceholder
{
    public void PlaceMacroPlaceholder(Page targetPage, string placeholderName)
    {
        System.Diagnostics.Debug.WriteLine($"\n[HAUPTCODE PROJEKT-START] BATCH-PROZESS GESTARTET für Such-Platzhalter: '{placeholderName}'");

        Project currentProject = targetPage.Project;
        Insert insertService = new Insert();
        PhasenschienenService schienenService = new PhasenschienenService();

        List<KeyValuePair<Page, PointD>> projectTargetLocations = new List<KeyValuePair<Page, PointD>>();

        try
        {
            System.Diagnostics.Debug.WriteLine("[SCHRITT 1] Nutze DMObjectsFinder für ultraschnelle, indizierte Platzhaltersuche im Projekt...");

            DMObjectsFinder finder = new DMObjectsFinder(currentProject);
            PlacementsFilter placementFilter = new PlacementsFilter();

            Placement[] globalPlacements = finder.GetPlacements(placementFilter);
            System.Diagnostics.Debug.WriteLine($"[INFO] {globalPlacements.Length} allgemeine Platzhalter-Objekte im Projekt gefunden. Filtere nach Name...");

            foreach (Placement placement in globalPlacements)
            {
                if (placement is PlaceHolder placeHolder)
                {
                    if (placeHolder.Name.Equals(placeholderName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        Page parentPage = placeHolder.Page;

                        if (parentPage != null && (parentPage.PageType == DocumentTypeManager.DocumentType.Circuit))
                        {
                            projectTargetLocations.Add(new KeyValuePair<Page, PointD>(parentPage, placeHolder.Location));
                            System.Diagnostics.Debug.WriteLine($" -> ZIEL-SEITE GEFUNDEN! Seite: '{parentPage.Name}' | Position: X={placeHolder.Location.X} / Y={placeHolder.Location.Y}");
                        }
                    }
                }
            }

            if (projectTargetLocations.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine($"[INFO] Der Platzhalter '{placeholderName}' existiert im gesamten Projekt nicht. Vorgang abgebrochen.");
                return;
            }

            int totalProjectPins = projectTargetLocations.Count * 3;
            System.Diagnostics.Debug.WriteLine($"[LOGIK] Starte EINMALIGE Artikelprüfung im Service für {totalProjectPins} Gesamt-Pins...");

            // 🟢 NUTZT DEN BEREINIGTEN SERVICE: Führt Artikeltausch UND das Löschen alter Grafiken sauber im Service aus!
            var ergebnis = schienenService.GetOrPreparePhasenschiene(targetPage, totalProjectPins);

            if (ergebnis == null || ergebnis.ParentFunction == null)
            {
                System.Diagnostics.Debug.WriteLine("[HAUPTCODE CRITICAL] Logisches Gerät oder Artikelreferenz konnte nicht ermittelt werden.");
                return;
            }

            Function parentFunction = ergebnis.ParentFunction;
            int startVariantIndex = ergebnis.VariantIndex;

            ArticleReference currentArtRef = null;
            if (parentFunction.ArticleReferences != null && parentFunction.ArticleReferences.Length > 0)
            {
                currentArtRef = parentFunction.ArticleReferences[0];
            }

            // 🔴 ENTFERNT: Der fehlerhafte "[DIREKT-TAUSCH]"-Block wurde hier restlos gelöscht!
            // Da der PhasenschienenService das Löschen der alten Grafiken und das Up-/Downgrade bereits 
            // im SafetyPoint erledigt hat, darf dieser Block hier die Daten nicht noch einmal manipulieren.

            System.Diagnostics.Debug.WriteLine("[HAUPTCODE] Bereite Platzierung der neuen Makrosegmente vor...");
            // =========================================================================
            // SCHRITT 2: REGULÄRE MAKRO-PLATZIERUNG
            // =========================================================================
            if (currentArtRef != null)
            {
                Article articleData = currentArtRef.Article;

                if (articleData != null)
                {
                    string articleMacroPath = articleData.Properties.ARTICLE_GROUPSYMBOLMACRO;
                    articleMacroPath = PathMap.SubstitutePath(articleMacroPath);

                    if (File.Exists(articleMacroPath))
                    {
                        SymbolMacro macro = new SymbolMacro();
                        macro.Open(articleMacroPath);

                        System.Diagnostics.Debug.WriteLine($"[SCHRITT 2] Zeichne {projectTargetLocations.Count} Makros im ununterbrochenen Batch-Lauf...");

                        for (int i = 0; i < projectTargetLocations.Count; i++)
                        {
                            Page currentPage = projectTargetLocations[i].Key;
                            PointD currentAnchor = projectTargetLocations[i].Value;

                            int currentVariantIndex = startVariantIndex + i;
                            if (currentVariantIndex > 4) currentVariantIndex = 4; // Begrenzung auf Variante E

                            System.Diagnostics.Debug.WriteLine($" -> [PLATZIERUNG {i + 1}/{projectTargetLocations.Count}] Seite: '{currentPage.Name}' | Variante: {(char)('A' + currentVariantIndex)}");

                            // Grafik auf der gereinigten Zielseite einfügen
                            StorableObject[] newPlacements = insertService.SymbolMacro(
                                macro,
                                currentVariantIndex,
                                currentPage,
                                currentAnchor,
                                Insert.MoveKind.Absolute,
                                SymbolMacro.Enums.NumerationMode.None
                            );

                            // BMK relativ zur Zielseite über die Service-Methode kürzen
                            schienenService.UpdateMacroPlacements(currentPage, parentFunction, newPlacements);
                        }

                        System.Diagnostics.Debug.WriteLine("[SCHRITT 3] Platzierung beendet. Führe EINMALIGES, projektweites Verbindungs-Update aus...");

                        // EPLAN-Projektkontext aktualisieren
                        new DeviceService().UpdateDevice(parentFunction);
                        new Generate().Connections(currentProject);

                        CommandLineInterpreter interpreter = new CommandLineInterpreter();
                        interpreter.Execute("GedRedraw");

                        System.Diagnostics.Debug.WriteLine("[HAUPTCODE ERFOLG] Das gesamte Projekt wurde in einem einzigen Durchlauf fehlerfrei automatisiert.");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[CRITICAL] Makrodatei existiert nicht: {articleMacroPath}");
                    }
                }
            }
        }
        catch (BaseException ex)
        {
            System.Diagnostics.Debug.WriteLine("[HAUPTCODE CRITICAL EXCEPTION] " + ex.Message);
        }
    }
}






//using Eplan.EplApi.Base.Enums;
//using Eplan.EplApi.Base;
//using Eplan.EplApi.DataModel;
//using Eplan.EplApi.DataModel.Graphics;
//using Eplan.EplApi.DataModel.MasterData;
//using Eplan.EplApi.HEServices;
//using Eplan.EplApi.ApplicationFramework;
//using System;
//using System.Collections.Generic;
//using System.IO;

//public class PlaceMacroAtPlaceholder
//{
//    public void PlaceMacroPlaceholder(Page targetPage, string placeholderName)
//    {
//        System.Diagnostics.Debug.WriteLine($"\n[HAUPTCODE PROJEKT-START] BATCH-PROZESS GESTARTET für Such-Platzhalter: '{placeholderName}'");

//        Project currentProject = targetPage.Project;
//        Insert insertService = new Insert();
//        PhasenschienenService schienenService = new PhasenschienenService();

//        // Lokale Liste für diesen einen, sauberen Durchlauf
//        List<KeyValuePair<Page, PointD>> projectTargetLocations = new List<KeyValuePair<Page, PointD>>();

//        try
//        {
//            System.Diagnostics.Debug.WriteLine("[SCHRITT 1] Nutze DMObjectsFinder für ultraschnelle, indizierte Platzhaltersuche im Projekt...");

//            // =========================================================================
//            // DATABASE-SCAN: Holt NUR die echten Platzhalter-Objekte aus der Datenbank
//            // =========================================================================
//            DMObjectsFinder finder = new DMObjectsFinder(currentProject);
//            PlacementsFilter placementFilter = new PlacementsFilter();

//            // KORREKTUR: GetPlacements liefert ein 'Placement[]' zurück (Fehler CS0029 behoben)
//            Placement[] globalPlacements = finder.GetPlacements(placementFilter);
//            System.Diagnostics.Debug.WriteLine($"[INFO] {globalPlacements.Length} allgemeine Platzhalter-Objekte im Projekt gefunden. Filtere nach Name...");

//            foreach (Placement placement in globalPlacements)
//            {
//                if (placement is PlaceHolder placeHolder)
//                {
//                    // Filter auf den exakten Namen Ihres Ankers
//                    if (placeHolder.Name.Equals(placeholderName, System.StringComparison.OrdinalIgnoreCase))
//                    {
//                        Page parentPage = placeHolder.Page;

//                        // Nur verarbeiten, wenn es sich um eine gültige Schaltplanseite handelt
//                        if (parentPage != null &&
//                           (parentPage.PageType == DocumentTypeManager.DocumentType.Circuit))
//                        {
//                            projectTargetLocations.Add(new KeyValuePair<Page, PointD>(parentPage, placeHolder.Location));
//                            System.Diagnostics.Debug.WriteLine($" -> ZIEL-SEITE GEFUNDEN! Seite: '{parentPage.Name}' | Position: X={placeHolder.Location.X} / Y={placeHolder.Location.Y}");
//                        }
//                    }
//                }
//            }

//            if (projectTargetLocations.Count == 0)
//            {
//                System.Diagnostics.Debug.WriteLine($"[INFO] Der Platzhalter '{placeholderName}' existiert im gesamten Projekt nicht. Vorgang abgebrochen.");
//                return;
//            }

//            // Berechnet die Pins für alle gefundenen Anker im gesamten Projekt auf einmal
//            int totalProjectPins = projectTargetLocations.Count * 3;
//            System.Diagnostics.Debug.WriteLine($"[LOGIK] Starte EINMALIGE Artikelprüfung im Service für {totalProjectPins} Gesamt-Pins...");

//            // Holt die optimale Schienengröße live aus der Datenbank (Läuft nur EINMAL!)
//            var ergebnis = schienenService.GetOrPreparePhasenschiene(targetPage, totalProjectPins);
//            Function parentFunction = ergebnis.ParentFunction;
//            int startVariantIndex = ergebnis.VariantIndex;

//            if (parentFunction == null || parentFunction.ArticleReferences.Length == 0)
//            {
//                System.Diagnostics.Debug.WriteLine("[HAUPTCODE CRITICAL] Logisches Gerät oder Artikelreferenz konnte nicht ermittelt werden.");
//                return;
//            }

//            ArticleReference currentArtRef = null;
//            if (parentFunction.ArticleReferences != null && parentFunction.ArticleReferences.Length > 0)
//            {
//                currentArtRef = parentFunction.ArticleReferences[0];
//            }

//            // =========================================================================
//            // GENAU HIER (ZWISCHEN BERECHNUNG UND SCHRITT 2) STEHT DIESER BLOCK!
//            // =========================================================================
//            if (parentFunction.IsPlaced)
//            {
//                System.Diagnostics.Debug.WriteLine($"[DIREKT-TAUSCH] Führe Artikelwechsel direkt am Bauteil aus...");

//                if (currentArtRef != null)
//                {
//                    currentArtRef.LockObject();
//                    currentArtRef.PartNr = currentArtRef.PartNr;
//                }

//                // KORREKTUR: DeviceService verwenden statt Direktaufruf an der Function
//                Eplan.EplApi.HEServices.DeviceService devService = new Eplan.EplApi.HEServices.DeviceService();
//                devService.UpdateDevice(parentFunction); // Drückt die Änderung ins Projekt

//                // Verbindungen aktualisieren
//                Generate generateService = new Generate();
//                generateService.Connections(currentProject);

//                CommandLineInterpreter interpreter = new CommandLineInterpreter();
//                interpreter.Execute("GedRedraw");

//                System.Diagnostics.Debug.WriteLine("[HAUPTCODE ERFOLG] Direkt-Tausch erfolgreich durchgeführt. Makro-Platzierung übersprungen.");
//                //return; // WICHTIG: Verhindert, dass Schritt 2 (Grafik zeichnen) gestartet wird!
//            }

//            // =========================================================================
//            // SCHRITT 2: REGULÄRE MAKRO-PLATZIERUNG (Wird nur ausgeführt, wenn nicht platziert)
//            // =========================================================================
//            if (currentArtRef != null)
//            {
//                Article articleData = currentArtRef.Article;

//                if (articleData != null)
//                {
//                    // ... Ihr nachfolgender Code bleibt exakt gleich ...
//                }

//                string articleMacroPath = articleData.Properties.ARTICLE_GROUPSYMBOLMACRO;
//                articleMacroPath = PathMap.SubstitutePath(articleMacroPath);

//                if (File.Exists(articleMacroPath))
//                {
//                    SymbolMacro macro = new SymbolMacro();
//                    macro.Open(articleMacroPath);

//                    System.Diagnostics.Debug.WriteLine($"[SCHRITT 2] Zeichne {projectTargetLocations.Count} Makros im ununterbrochenen Batch-Lauf...");

//                    // 2. REINE PLATZIERUNGSSCHLEIFE (Kein ständiges Neustarten mehr!)
//                    for (int i = 0; i < projectTargetLocations.Count; i++)
//                    {
//                        Page currentPage = projectTargetLocations[i].Key;
//                        PointD currentAnchor = projectTargetLocations[i].Value;

//                        // Variantenindex zählt sauber projektweit von der Startvariante hoch
//                        int currentVariantIndex = startVariantIndex + i;
//                        if (currentVariantIndex > 4) currentVariantIndex = 4; // Begrenzung auf Variante E

//                        System.Diagnostics.Debug.WriteLine($" -> [PLATZIERUNG {i + 1}/{projectTargetLocations.Count}] Seite: '{currentPage.Name}' | Variante: {(char)('A' + currentVariantIndex)}");

//                        // Grafik direkt auf der Zielseite einfügen
//                        StorableObject[] newPlacements = insertService.SymbolMacro(
//                            macro,
//                            currentVariantIndex,
//                            currentPage,
//                            currentAnchor,
//                            Insert.MoveKind.Absolute,
//                            SymbolMacro.Enums.NumerationMode.None
//                        );

//                        // BMK relativ zur Zielseite kürzen
//                        schienenService.UpdateMacroPlacements(currentPage, parentFunction, newPlacements);
//                    }

//                    System.Diagnostics.Debug.WriteLine("[SCHRITT 3] Platzierung beendet. Führe EINMALIGES, projektweites Verbindungs-Update aus...");

//                    // 3. SEITEN-UPDATE (Einmalig am Ende des gesamten Vorgangs!)
//                    Generate generateService = new Generate();
//                    generateService.Connections(currentProject);

//                    CommandLineInterpreter interpreter = new CommandLineInterpreter();
//                    interpreter.Execute("GedRedraw");

//                    System.Diagnostics.Debug.WriteLine("[HAUPTCODE ERFOLG] Das gesamte Projekt wurde in einem einzigen Durchlauf fehlerfrei automatisiert.");
//                }
//            }
//        }
//        catch (BaseException ex)
//        {
//            System.Diagnostics.Debug.WriteLine("[HAUPTCODE CRITICAL EXCEPTION] " + ex.Message);
//        }
//    }



//}
// HINWEIS: Keine statischen Flags mehr nötig! Die Methode läuft nur noch EINMAL an.
//    public void PlaceMacroPlaceholder(Page targetPage, string placeholderName)
//    {
//        System.Diagnostics.Debug.WriteLine($"\n[HAUPTCODE PROJEKT-START] BATCH-PROZESS GESTARTET für Such-Platzhalter: '{placeholderName}'");

//        Project currentProject = targetPage.Project;
//        Insert insertService = new Insert();
//        PhasenschienenService schienenService = new PhasenschienenService();

//        // Lokale Liste für diesen einen, sauberen Durchlauf
//        List<KeyValuePair<Page, PointD>> projectTargetLocations = new List<KeyValuePair<Page, PointD>>();

//        try
//        {
//            System.Diagnostics.Debug.WriteLine("[SCHRITT 1] Nutze DMObjectsFinder für ultraschnelle, indizierte Platzhaltersuche im Projekt...");

//            // =========================================================================
//            // DATABASE-SCAN: Holt NUR die echten Platzhalter-Objekte aus der Datenbank
//            // =========================================================================
//            DMObjectsFinder finder = new DMObjectsFinder(currentProject);
//            PlacementsFilter placementFilter = new PlacementsFilter();



//            // KORREKTUR: GetPlacements liefert ein 'Placement[]' zurück (Fehler CS0029 behoben)
//            Placement[] globalPlacements = finder.GetPlacements(placementFilter);
//            System.Diagnostics.Debug.WriteLine($"[INFO] {globalPlacements.Length} allgemeine Platzhalter-Objekte im Projekt gefunden. Filtere nach Name...");

//            foreach (Placement placement in globalPlacements)
//            {
//                if (placement is PlaceHolder placeHolder)
//                {
//                    // Filter auf den exakten Namen Ihres Ankers
//                    if (placeHolder.Name.Equals(placeholderName, System.StringComparison.OrdinalIgnoreCase))
//                    {
//                        Page parentPage = placeHolder.Page;

//                        // Nur verarbeiten, wenn es sich um eine gültige Schaltplanseite handelt
//                        if (parentPage != null &&
//                           (parentPage.PageType == DocumentTypeManager.DocumentType.Circuit))
//                        {
//                            projectTargetLocations.Add(new KeyValuePair<Page, PointD>(parentPage, placeHolder.Location));
//                            System.Diagnostics.Debug.WriteLine($" -> ZIEL-SEITE GEFUNDEN! Seite: '{parentPage.Name}' | Position: X={placeHolder.Location.X} / Y={placeHolder.Location.Y}");
//                        }
//                    }
//                }
//            }

//            if (projectTargetLocations.Count == 0)
//            {
//                System.Diagnostics.Debug.WriteLine($"[INFO] Der Platzhalter '{placeholderName}' existiert im gesamten Projekt nicht. Vorgang abgebrochen.");
//                return;
//            }

//            // Berechnet die Pins für alle gefundenen Anker im gesamten Projekt auf einmal
//            int totalProjectPins = projectTargetLocations.Count * 3;
//            System.Diagnostics.Debug.WriteLine($"[LOGIK] Starte EINMALIGE Artikelprüfung im Service für {totalProjectPins} Gesamt-Pins...");

//            // Holt die optimale Schienengröße live aus der Datenbank (Läuft nur EINMAL!)
//            var ergebnis = schienenService.GetOrPreparePhasenschiene(targetPage, totalProjectPins);
//            Function parentFunction = ergebnis.ParentFunction;
//            int startVariantIndex = ergebnis.VariantIndex;

//            if (parentFunction == null || parentFunction.ArticleReferences.Length == 0)
//            {
//                System.Diagnostics.Debug.WriteLine("[HAUPTCODE CRITICAL] Logisches Gerät oder Artikelreferenz konnte nicht ermittelt werden.");
//                return;
//            }

//            ArticleReference currentArtRef = null;
//            if (parentFunction.ArticleReferences != null && parentFunction.ArticleReferences.Length > 0)
//            {
//                currentArtRef = parentFunction.ArticleReferences[0];
//            }

//            if (currentArtRef != null)
//            {
//                Article articleData = currentArtRef.Article;

//                if (articleData != null)
//                {
//                    // ... Ihr nachfolgender Code bleibt exakt gleich ...
//                }

//                string articleMacroPath = articleData.Properties.ARTICLE_GROUPSYMBOLMACRO;
//                articleMacroPath = PathMap.SubstitutePath(articleMacroPath);

//                if (File.Exists(articleMacroPath))
//                {
//                    SymbolMacro macro = new SymbolMacro();
//                    macro.Open(articleMacroPath);

//                    System.Diagnostics.Debug.WriteLine($"[SCHRITT 2] Zeichne {projectTargetLocations.Count} Makros im ununterbrochenen Batch-Lauf...");

//                    // 2. REINE PLATZIERUNGSSCHLEIFE (Kein ständiges Neustarten mehr!)
//                    for (int i = 0; i < projectTargetLocations.Count; i++)
//                    {
//                        Page currentPage = projectTargetLocations[i].Key;
//                        PointD currentAnchor = projectTargetLocations[i].Value;

//                        // Variantenindex zählt sauber projektweit von der Startvariante hoch
//                        int currentVariantIndex = startVariantIndex + i;
//                        if (currentVariantIndex > 4) currentVariantIndex = 4; // Begrenzung auf Variante E

//                        System.Diagnostics.Debug.WriteLine($" -> [PLATZIERUNG {i + 1}/{projectTargetLocations.Count}] Seite: '{currentPage.Name}' | Variante: {(char)('A' + currentVariantIndex)}");

//                        // Grafik direkt auf der Zielseite einfügen
//                        StorableObject[] newPlacements = insertService.SymbolMacro(
//                            macro,
//                            currentVariantIndex,
//                            currentPage,
//                            currentAnchor,
//                            Insert.MoveKind.Absolute,
//                            SymbolMacro.Enums.NumerationMode.None
//                        );

//                        // BMK relativ zur Zielseite kürzen
//                        schienenService.UpdateMacroPlacements(currentPage, parentFunction, newPlacements);
//                    }

//                    System.Diagnostics.Debug.WriteLine("[SCHRITT 3] Platzierung beendet. Führe EINMALIGES, projektweites Verbindungs-Update aus...");

//                    // 3. SEITEN-UPDATE (Einmalig am Ende des gesamten Vorgangs!)
//                    Generate generateService = new Generate();
//                    generateService.Connections(currentProject);

//                    CommandLineInterpreter interpreter = new CommandLineInterpreter();
//                    interpreter.Execute("GedRedraw");

//                    System.Diagnostics.Debug.WriteLine("[HAUPTCODE ERFOLG] Das gesamte Projekt wurde in einem einzigen Durchlauf fehlerfrei automatisiert.");
//                }
//            }
//        }
//        catch (BaseException ex)
//        {
//            System.Diagnostics.Debug.WriteLine("[HAUPTCODE CRITICAL EXCEPTION] " + ex.Message);
//        }
//    }
//}



//using Eplan.EplApi.Base.Enums;
//using Eplan.EplApi.Base;
//using Eplan.EplApi.DataModel;
//using Eplan.EplApi.DataModel.Graphics;
//using Eplan.EplApi.DataModel.MasterData;
//using Eplan.EplApi.HEServices;
//using Eplan.EplApi.ApplicationFramework;
//using System;
//using System.Collections.Generic;
//using System.IO;

//public class PlaceMacroAtPlaceholder
//{
//    // =========================================================================
//    // STATISCHE KLASSENVARIABLEN: Bleiben über alle Seitenaufrufe hinweg erhalten
//    // =========================================================================
//    private static bool istArtikelGeprüft = false;
//    private static Function globalParentFunction = null;
//    private static int globalStartVariantIndex = 0;
//    private static SymbolMacro globalMacro = null;
//    private static int globalPlacementsCounter = 0; // Zählt projektweit die platzierten Makros hoch

//    public void PlaceMacroPlaceholder(Page targetPage, string placeholderName)
//    {
//        System.Diagnostics.Debug.WriteLine($"\n[HAUPTCODE SEITEN-AUFRUF] Methode gestartet für Seite: '{targetPage.Name}' | Such-Platzhalter: '{placeholderName}'");

//        Project currentProject = targetPage.Project;
//        List<KeyValuePair<Page, PointD>> globalTargetLocations = new List<KeyValuePair<Page, PointD>>();

//        // 1. Platzhalter-Anker sammeln (Wir können uns hier auf die aktuelle targetPage beschränken, 
//        // da die Methode ohnehin für jede Seite einzeln triggert)
//        Placement[] pagePlacements = targetPage.AllPlacements;
//        foreach (Placement placement in pagePlacements)
//        {
//            if (placement is PlaceHolder placeHolder)
//            {
//                if (placeHolder.Name.Equals(placeholderName, System.StringComparison.OrdinalIgnoreCase))
//                {
//                    globalTargetLocations.Add(new KeyValuePair<Page, PointD>(targetPage, placeHolder.Location));
//                }
//            }
//        }

//        if (globalTargetLocations.Count == 0) return;

//        Insert insertService = new Insert();
//        PhasenschienenService schienenService = new PhasenschienenService();

//        try
//        {
//            // Da wir projektweit prüfen wollen, ermitteln wir die Gesamt-Ankeranzahl im Projekt 
//            // EINMALIG beim allerersten Aufruf der ersten Seite
//            if (!istArtikelGeprüft)
//            {
//                int totalProjectPins = 0;
//                foreach (Page p in currentProject.Pages)
//                {
//                    if (p.PageType == DocumentTypeManager.DocumentType.Circuit)
//                    {
//                        foreach (Placement pl in p.AllPlacements)
//                        {
//                            if (pl is PlaceHolder ph && ph.Name.Equals(placeholderName, StringComparison.OrdinalIgnoreCase))
//                            {
//                                totalProjectPins += 3; // 3 Pins pro Platzhalter im gesamten Projekt
//                            }
//                        }
//                    }
//                }

//                System.Diagnostics.Debug.WriteLine($"[GLOBALES PROJEKT-INIT] Führe die ABSOLUT EINMALIGE Artikelprüfung für das Gesamtprojekt aus ({totalProjectPins} Gesamt-Pins)...");

//                var ergebnis = schienenService.GetOrPreparePhasenschiene(targetPage, totalProjectPins);
//                globalParentFunction = ergebnis.ParentFunction;
//                globalStartVariantIndex = ergebnis.VariantIndex;

//                if (globalParentFunction != null && globalParentFunction.ArticleReferences.Length > 0)
//                {
//                    ArticleReference currentArtRef = globalParentFunction.ArticleReferences[0];
//                    Article articleData = currentArtRef.Article;

//                    if (articleData != null)
//                    {
//                        string articleMacroPath = articleData.Properties.ARTICLE_GROUPSYMBOLMACRO;
//                        articleMacroPath = PathMap.SubstitutePath(articleMacroPath);

//                        if (File.Exists(articleMacroPath))
//                        {
//                            globalMacro = new SymbolMacro();
//                            globalMacro.Open(articleMacroPath);
//                        }
//                    }
//                }

//                istArtikelGeprüft = true;
//                System.Diagnostics.Debug.WriteLine("[GLOBALES PROJEKT-INIT] Artikelprüfung erfolgreich abgeschlossen und für alle weiteren Seiten gesperrt.");
//            }

//            // Sicherheits-Abbruch, falls Initialisierung fehlschlug
//            if (globalMacro == null || globalParentFunction == null) return;

//            // 2. REINE PLATZIERUNGSSCHLEIFE FÜR DIE AKTUELLE SEITE
//            for (int i = 0; i < globalTargetLocations.Count; i++)
//            {
//                Page currentPage = globalTargetLocations[i].Key;
//                PointD currentAnchor = globalTargetLocations[i].Value;

//                // Der Variantenindex errechnet sich nun aus dem globalen Gesamtzähler aller Seiten
//                int currentVariantIndex = globalStartVariantIndex + globalPlacementsCounter;
//                if (currentVariantIndex > 4) currentVariantIndex = 4;

//                System.Diagnostics.Debug.WriteLine($"[PLATZIERUNG] Seite '{currentPage.Name}' | Makro {i + 1}/{globalTargetLocations.Count} | Globale Platzierung Nr. {globalPlacementsCounter + 1} | Variante {(char)('A' + currentVariantIndex)}");

//                // Grafik einfügen
//                StorableObject[] newPlacements = insertService.SymbolMacro(
//                    globalMacro,
//                    currentVariantIndex,
//                    currentPage,
//                    currentAnchor,
//                    Insert.MoveKind.Absolute,
//                    SymbolMacro.Enums.NumerationMode.None
//                );

//                // BMK kürzen
//                schienenService.UpdateMacroPlacements(currentPage, globalParentFunction, newPlacements);

//                // Globalen Zähler erhöhen, damit die Variante auf der nächsten Seite richtig weiterschiebt
//                globalPlacementsCounter++;
//            }

//            // 3. AKTUALISIEREN (Am Ende jeder Seite)
//            Generate generateService = new Generate();
//            generateService.Connections(targetPage.Project);

//            CommandLineInterpreter interpreter = new CommandLineInterpreter();
//            interpreter.Execute("GedRedraw");
//        }
//        catch (BaseException ex)
//        {
//            System.Diagnostics.Debug.WriteLine("[HAUPTCODE CRITICAL EXCEPTION] " + ex.Message);
//        }
//    }

//    // Optional: Methode um den Status zurückzusetzen (z.B. vor dem Start eines komplett neuen Laufs)
//    public static void ResetServiceState()
//    {
//        istArtikelGeprüft = false;
//        globalParentFunction = null;
//        globalStartVariantIndex = 0;
//        globalMacro = null;
//        globalPlacementsCounter = 0;
//    }
//}





//using Eplan.EplApi.Base.Enums;
//using Eplan.EplApi.Base;
//using Eplan.EplApi.DataModel;
//using Eplan.EplApi.DataModel.Graphics;
//using Eplan.EplApi.DataModel.MasterData;
//using Eplan.EplApi.HEServices;
//using Eplan.EplApi.ApplicationFramework;
//using System;
//using System.Collections.Generic;
//using System.IO;

//public class PlaceMacroAtPlaceholder
//{
//    public void PlaceMacroPlaceholder(Page targetPage, string placeholderName)
//    {
//        System.Diagnostics.Debug.WriteLine($"\n[HAUPTCODE START] Batch-Suche gestartet für Such-Platzhalter: '{placeholderName}'");

//        Project currentProject = targetPage.Project;
//        List<KeyValuePair<Page, PointD>> globalTargetLocations = new List<KeyValuePair<Page, PointD>>();

//        // 1. Alle Platzhalter-Anker projektweit sammeln
//        foreach (Page page in currentProject.Pages)
//        {
//            if (page.PageType == DocumentTypeManager.DocumentType.Circuit)
//            {
//                Placement[] pagePlacements = page.AllPlacements;
//                foreach (Placement placement in pagePlacements)
//                {
//                    if (placement is PlaceHolder placeHolder)
//                    {
//                        if (placeHolder.Name.Equals(placeholderName, System.StringComparison.OrdinalIgnoreCase))
//                        {
//                            globalTargetLocations.Add(new KeyValuePair<Page, PointD>(page, placeHolder.Location));
//                        }
//                    }
//                }
//            }
//        }

//        if (globalTargetLocations.Count == 0) return;

//        Insert insertService = new Insert();
//        PhasenschienenService schienenService = new PhasenschienenService();

//        // Variablendeklaration für die Wiederverwendung in der Schleife
//        Function parentFunction = null;
//        int startVariantIndex = 0;
//        SymbolMacro macro = null;

//        // WICHTIG: Flag zur Steuerung der EINMALIGEN Prüfung IN der Schleife
//        bool istArtikelGeprüft = false;

//        try
//        {
//            // Die Pins werden auf Basis der global gefundenen Anker berechnet
//            int totalPinsToPlace = globalTargetLocations.Count * 3;

//            // 2. DIE HAUPTSCHLEIFE
//            for (int i = 0; i < globalTargetLocations.Count; i++)
//            {
//                Page currentPage = globalTargetLocations[i].Key;
//                PointD currentAnchor = globalTargetLocations[i].Value;

//                // =========================================================================
//                // LOGIK: ARTIKELPRÜFUNG FINDET GENAU EINMAL BEIM ERSTEN DURCHLAUF STATT
//                // =========================================================================
//                if (!istArtikelGeprüft)
//                {
//                    System.Diagnostics.Debug.WriteLine($"[SCHLEIFE - DURCHLAUF {i + 1}] Führe die einmalige Artikelprüfung und Vorbereitung aus...");

//                    // Ruft die Datenbank- und Tauschlogik auf
//                    var ergebnis = schienenService.GetOrPreparePhasenschiene(targetPage, totalPinsToPlace);
//                    parentFunction = ergebnis.ParentFunction;
//                    startVariantIndex = ergebnis.VariantIndex;

//                    if (parentFunction != null && parentFunction.ArticleReferences.Length > 0)
//                    {
//                        ArticleReference currentArtRef = parentFunction.ArticleReferences[0];
//                        Article articleData = currentArtRef.Article;

//                        if (articleData != null)
//                        {
//                            string articleMacroPath = articleData.Properties.ARTICLE_GROUPSYMBOLMACRO;
//                            articleMacroPath = PathMap.SubstitutePath(articleMacroPath);

//                            if (File.Exists(articleMacroPath))
//                            {
//                                macro = new SymbolMacro();
//                                macro.Open(articleMacroPath);
//                            }
//                        }
//                    }

//                    // Flag auf true setzen, damit dieser Block ab jetzt ignoriert wird
//                    istArtikelGeprüft = true;
//                    System.Diagnostics.Debug.WriteLine("[SCHLEIFE] Artikelprüfung erfolgreich abgeschlossen und für Folgedurchläufe gesperrt.");
//                }

//                // Falls das Makro oder die Funktion nicht geladen werden konnte, Schleife abbrechen
//                if (macro == null || parentFunction == null)
//                {
//                    System.Diagnostics.Debug.WriteLine("[SCHLEIFE ERROR] Makro oder Hauptfunktion konnte nicht initialisiert werden.");
//                    break;
//                }

//                // Variantenindex berechnen und weiterschieben
//                int currentVariantIndex = startVariantIndex + i;
//                if (currentVariantIndex > 4) currentVariantIndex = 4; // Begrenzung auf Variante E

//                System.Diagnostics.Debug.WriteLine($"[PLATZIERUNG {i + 1}/{globalTargetLocations.Count}] Platziere auf Seite '{currentPage.Name}' mit Variante {(char)('A' + currentVariantIndex)}");

//                // Grafik auf der jeweiligen Seite platzieren
//                StorableObject[] newPlacements = insertService.SymbolMacro(
//                    macro,
//                    currentVariantIndex,
//                    currentPage,
//                    currentAnchor,
//                    Insert.MoveKind.Absolute,
//                    SymbolMacro.Enums.NumerationMode.None
//                );

//                // Identität übertragen und BMK dynamisch relativ zur Schaltplanseite kürzen
//                schienenService.UpdateMacroPlacements(currentPage, parentFunction, newPlacements);
//            }

//            // 3. FINALES GLOBAL UPDATE (Einmalig nach der Schleife)
//            System.Diagnostics.Debug.WriteLine("[ENDE] Alle Makros platziert. Starte abschließendes Projekt-Update...");
//            Generate generateService = new Generate();
//            generateService.Connections(currentProject);

//            CommandLineInterpreter interpreter = new CommandLineInterpreter();
//            interpreter.Execute("GedRedraw");
//        }
//        catch (BaseException ex)
//        {
//            System.Diagnostics.Debug.WriteLine("[HAUPTCODE CRITICAL EXCEPTION] " + ex.Message);
//        }
//    }
//}




//using Eplan.EplApi.Base.Enums;
//using Eplan.EplApi.Base;
//using Eplan.EplApi.DataModel;
//using Eplan.EplApi.DataModel.Graphics;
//using Eplan.EplApi.DataModel.MasterData;
//using Eplan.EplApi.HEServices;
//using Eplan.EplApi.ApplicationFramework;
//using System;
//using System.Collections.Generic;
//using System.IO;

//public class PlaceMacroAtPlaceholder
//{
//    public void PlaceMacroPlaceholder(Page targetPage, string placeholderName)
//    {
//        System.Diagnostics.Debug.WriteLine($"\n[HAUPTCODE START] Projektweite Batch-Suche gestartet für Such-Platzhalter: '{placeholderName}'");

//        Project currentProject = targetPage.Project;
//        List<KeyValuePair<Page, PointD>> globalTargetLocations = new List<KeyValuePair<Page, PointD>>();

//        System.Diagnostics.Debug.WriteLine("[SCHRITT 1] Durchsuche das GESAMTE PROJEKT nach passenden Platzhalter-Ankern...");

//        // Iteration über alle Seiten des gesamten Projekts
//        foreach (Page page in currentProject.Pages)
//        {
//            // Nur logische Schaltplanseiten untersuchen (z.B. Schaltplan allpolig)
//            if ((page.PageType == DocumentTypeManager.DocumentType.Circuit ))
//            {

//               Placement[] pagePlacements = page.AllPlacements;
//                foreach (Placement placement in pagePlacements)
//                {
//                    if (placement is PlaceHolder placeHolder)
//                    {
//                        if (placeHolder.Name.Equals(placeholderName, System.StringComparison.OrdinalIgnoreCase))
//                        {
//                            // Merkt sich die Kombination aus spezifischer Seite und Koordinaten-Anker
//                            globalTargetLocations.Add(new KeyValuePair<Page, PointD>(page, placeHolder.Location));
//                            System.Diagnostics.Debug.WriteLine($" -> Anker im Projekt gefunden! Seite: '{page.Name}' | Position: X={placeHolder.Location.X} / Y={placeHolder.Location.Y}");
//                        }
//                    }
//                }
//            }
//        }

//        if (globalTargetLocations.Count == 0)
//        {
//            System.Diagnostics.Debug.WriteLine($"[HAUPTCODE ABBRUCH] Der Platzhalter '{placeholderName}' wurde im gesamten Projekt auf keiner Seite gefunden.");
//            return;
//        }

//        System.Diagnostics.Debug.WriteLine($"[HAUPTCODE INFO] Projektweit insgesamt {globalTargetLocations.Count} Ankerpunkte für die Platzierung gesammelt.");

//        Insert insertService = new Insert();
//        PhasenschienenService schienenService = new PhasenschienenService();

//        try
//        {
//            // Berechnet die benötigten Pins im Voraus für ALLE projektweiten Ankerpunkte auf einmal!
//            int totalPinsToPlace = globalTargetLocations.Count * 3;

//            System.Diagnostics.Debug.WriteLine($"[BATCH INITIIRUNG] Starte globale Vorabprüfung im Service für {totalPinsToPlace} Gesamt-Pins...");

//            // Führt den Artikeltausch, das Up-/Downgrade oder die Neuanlage EINMALIG für das Gesamtprojekt aus
//            var ergebnis = schienenService.GetOrPreparePhasenschiene(targetPage, totalPinsToPlace);
//            Function parentFunction = ergebnis.ParentFunction;
//            int startVariantIndex = ergebnis.VariantIndex;

//            if (parentFunction == null)
//            {
//                System.Diagnostics.Debug.WriteLine("[HAUPTCODE CRITICAL] Die Serviceklasse lieferte kein gültiges logisches Gerät zurück.");
//                return;
//            }

//            if (parentFunction.ArticleReferences != null && parentFunction.ArticleReferences.Length > 0)
//            {
//                ArticleReference currentArtRef = parentFunction.ArticleReferences[0];
//                Article articleData = currentArtRef.Article;

//                if (articleData != null)
//                {
//                    string articleMacroPath = articleData.Properties.ARTICLE_GROUPSYMBOLMACRO;

//                    if (!string.IsNullOrEmpty(articleMacroPath))
//                    {
//                        articleMacroPath = PathMap.SubstitutePath(articleMacroPath);

//                        if (File.Exists(articleMacroPath))
//                        {
//                            SymbolMacro macro = new SymbolMacro();
//                            macro.Open(articleMacroPath);

//                            System.Diagnostics.Debug.WriteLine("[SCHRITT 2] Starte projektweite, blitzschnelle Platzierungsschleife...");

//                            // 2. REINE PLATZIERUNGSSCHLEIFE ÜBER DAS GESAMTE PROJEKT
//                            for (int i = 0; i < globalTargetLocations.Count; i++)
//                            {
//                                Page currentPage = globalTargetLocations[i].Key;
//                                PointD currentAnchor = globalTargetLocations[i].Value;

//                                int currentVariantIndex = startVariantIndex + i;
//                                if (currentVariantIndex > 4) currentVariantIndex = 4; // Sicherheitsbegrenzung auf Variante E

//                                System.Diagnostics.Debug.WriteLine($" -> [GRAFIK {i + 1}/{globalTargetLocations.Count}] Zeichne auf Seite '{currentPage.Name}' | Variante: {(char)('A' + currentVariantIndex)}");

//                                // Das Makro an den lückenlosen Projektkoordinaten einfügen
//                                StorableObject[] newPlacements = insertService.SymbolMacro(
//                                    macro,
//                                    currentVariantIndex,
//                                    currentPage,
//                                    currentAnchor,
//                                    Insert.MoveKind.Absolute,
//                                    SymbolMacro.Enums.NumerationMode.None
//                                );

//                                // Identität übertragen und BMK dynamisch relativ zur jeweiligen Zielseite kürzen
//                                schienenService.UpdateMacroPlacements(currentPage, parentFunction, newPlacements);
//                            }

//                            System.Diagnostics.Debug.WriteLine("[SCHRITT 3] Projektweite Schleife beendet. Starte finales, globales Verbindungs-Update...");

//                            Generate generateService = new Generate();
//                            generateService.Connections(currentProject);

//                            CommandLineInterpreter interpreter = new CommandLineInterpreter();
//                            interpreter.Execute("GedRedraw");

//                            System.Diagnostics.Debug.WriteLine("[HAUPTCODE ERFOLG] Die projektweite Batch-Platzierung wurde fehlerfrei beendet.");
//                        }
//                    }
//                }
//            }
//        }
//        catch (BaseException ex)
//        {
//            System.Diagnostics.Debug.WriteLine("[HAUPTCODE CRITICAL EXCEPTION] Fehler im globalen Ablauf: " + ex.Message);
//        }
//    }
//}


//using Eplan.EplApi.Base.Enums;
//using Eplan.EplApi.Base;
//using Eplan.EplApi.DataModel;
//using Eplan.EplApi.DataModel.Graphics;
//using Eplan.EplApi.DataModel.MasterData;
//using Eplan.EplApi.HEServices;
//using Eplan.EplApi.ApplicationFramework;
//using System;
//using System.Collections.Generic;
//using System.IO;

//public class PlaceMacroAtPlaceholder
//{
//    public void PlaceMacroPlaceholder(Page targetPage, string placeholderName)
//    {
//        System.Diagnostics.Debug.WriteLine($"\n[HAUPTCODE START] PlaceMacroPlaceholder gestartet für Such-Platzhalter: '{placeholderName}'");

//        // 1. Alle Platzhalter-Anker auf dieser Seite sammeln
//        Placement[] allPlacements = targetPage.AllPlacements;
//        List<PointD> targetLocations = new List<PointD>();

//        System.Diagnostics.Debug.WriteLine($"[HAUPTCODE PRÜFUNG] Durchsuche {allPlacements.Length} Placements auf Seite nach Name '{placeholderName}'...");

//        foreach (Placement placement in allPlacements)
//        {
//            if (placement is PlaceHolder placeHolder)
//            {
//                if (placeHolder.Name.Equals(placeholderName, System.StringComparison.OrdinalIgnoreCase))
//                {
//                    targetLocations.Add(placeHolder.Location);
//                    System.Diagnostics.Debug.WriteLine($" -> Anker gefunden an Position: X={placeHolder.Location.X} / Y={placeHolder.Location.Y}");
//                }
//            }
//        }

//        // Wenn kein passender Anker auf der Seite ist, können wir abbrechen
//        if (targetLocations.Count == 0)
//        {
//            System.Diagnostics.Debug.WriteLine($"[HAUPTCODE ABBRUCH] Kein Platzhalter mit dem Namen '{placeholderName}' auf der Seite vorhanden.");
//            return;
//        }

//        System.Diagnostics.Debug.WriteLine($"[HAUPTCODE INFO] Insgesamt {targetLocations.Count} passende(r) Ankerpunkt(e) für die Platzierung ermittelt.");

//        Insert insertService = new Insert();
//        PhasenschienenService schienenService = new PhasenschienenService();

//        try
//        {
//            // =========================================================================
//            // PERFORMANCE-OPTIMIERUNG: PRÜFUNG GANZ AM ANFANG (VOR DER SCHLEIFE)
//            // =========================================================================
//            // Wir berechnen die benötigten Pins im Voraus für ALLE Ankerpunkte auf einmal!
//            int totalPinsToPlace = targetLocations.Count * 3;

//            System.Diagnostics.Debug.WriteLine($"[BATCH INITIIRUNG] Starte EINMALIGE Vorabprüfung im Service für {totalPinsToPlace} Gesamt-Pins...");

//            // Führt den Artikeltausch, das Up- oder Downgrade oder die Neuanlage EINMALIG vorab aus
//            var ergebnis = schienenService.GetOrPreparePhasenschiene(targetPage, totalPinsToPlace);
//            Function parentFunction = ergebnis.ParentFunction;
//            int startVariantIndex = ergebnis.VariantIndex;

//            if (parentFunction == null)
//            {
//                System.Diagnostics.Debug.WriteLine("[HAUPTCODE CRITICAL] Die Serviceklasse lieferte kein gültiges logisches Gerät (parentFunction) zurück.");
//                return;
//            }

//            System.Diagnostics.Debug.WriteLine($"[BATCH LOGIK] Zugeordnetes Ziel-Gerät fixiert: '{parentFunction.Name}'.");

//            if (parentFunction.ArticleReferences != null && parentFunction.ArticleReferences.Length > 0)
//            {
//                ArticleReference currentArtRef = parentFunction.ArticleReferences[0];
//                Article articleData = currentArtRef.Article;

//                if (articleData != null)
//                {
//                    string articleMacroPath = articleData.Properties.ARTICLE_GROUPSYMBOLMACRO;
//                    System.Diagnostics.Debug.WriteLine($"[BATCH STAMMDATEN] Gelesener Makropfad aus Artikel '{currentArtRef.PartNr}': '{articleMacroPath}'");

//                    if (!string.IsNullOrEmpty(articleMacroPath))
//                    {
//                        articleMacroPath = PathMap.SubstitutePath(articleMacroPath);
//                        System.Diagnostics.Debug.WriteLine($"[BATCH STAMMDATEN] Pfad nach EPLAN-Substituierung: '{articleMacroPath}'");

//                        if (File.Exists(articleMacroPath))
//                        {
//                            System.Diagnostics.Debug.WriteLine("[BATCH INITIALISIERUNG] Makro-Datei existiert. Öffne Symbol-Makro...");
//                            SymbolMacro macro = new SymbolMacro();
//                            macro.Open(articleMacroPath);

//                            System.Diagnostics.Debug.WriteLine("[SCHRITT 2] Starte blitzschnelle Platzierungsschleife (Reine Grafik-Generierung)...");

//                            // 2. REINE PLATZIERUNGSSCHLEIFE (Läuft ohne jegliche Artikeldatenbankzugriffe)
//                            for (int i = 0; i < targetLocations.Count; i++)
//                            {
//                                PointD currentAnchor = targetLocations[i];

//                                // Der Varianten-Index wandert mit jedem gesetzten Teil dynamisch weiter
//                                int currentVariantIndex = startVariantIndex + i;
//                                if (currentVariantIndex > 4) currentVariantIndex = 4; // Sicherheitsbegrenzung auf Variante E

//                                System.Diagnostics.Debug.WriteLine($" -> [GRAFIK {i + 1}/{targetLocations.Count}] Zeichne Makro an Anker {i}. Berechneter VariantenIndex: {currentVariantIndex} (Variante {(char)('A' + currentVariantIndex)})");

//                                // Das Makro an der Position einfügen
//                                StorableObject[] newPlacements = insertService.SymbolMacro(
//                                    macro,
//                                    currentVariantIndex,
//                                    targetPage,
//                                    currentAnchor,
//                                    Insert.MoveKind.Absolute,
//                                    SymbolMacro.Enums.NumerationMode.None
//                                );

//                                System.Diagnostics.Debug.WriteLine($"    -> Grafik erzeugt (Elemente: {newPlacements?.Length ?? 0}). Rufe UpdateMacroPlacements zur BMK-Kürzung auf...");

//                                // Identität übertragen und BMK dynamisch kürzen
//                                schienenService.UpdateMacroPlacements(targetPage, parentFunction, newPlacements);
//                            }

//                            System.Diagnostics.Debug.WriteLine("[SCHRITT 3] Platzierungsschleife beendet. Starte einmaliges, globales Projektdaten-Update...");

//                            // --- JETZT DIREKT KORREKT AKTUALISIEREN (Einmalig am Ende des Batches) ---
//                            Generate generateService = new Generate();
//                            generateService.Connections(targetPage.Project);
//                            System.Diagnostics.Debug.WriteLine(" -> Verbindungen im aktuellen Projekt erfolgreich neu generiert.");

//                            CommandLineInterpreter interpreter = new CommandLineInterpreter();
//                            interpreter.Execute("GedRedraw");
//                            System.Diagnostics.Debug.WriteLine(" -> EPLAN Grafischer Editor aktualisiert (GedRedraw).");

//                            System.Diagnostics.Debug.WriteLine("[HAUPTCODE ERFOLG] Die Batch-Platzierung wurde fehlerfrei abgeschlossen.");
//                        }
//                        else
//                        {
//                            System.Diagnostics.Debug.WriteLine($"[HAUPTCODE ERROR] Die physische Makro-Datei existiert nicht unter dem Pfad: '{articleMacroPath}'");
//                        }
//                    }
//                    else
//                    {
//                        System.Diagnostics.Debug.WriteLine($"[HAUPTCODE ERROR] Der Artikel '{currentArtRef.PartNr}' besitzt kein eingetragenes Symbolmakro (ARTICLE_GROUPSYMBOLMACRO ist leer).");
//                    }
//                }
//            }
//            else
//            {
//                System.Diagnostics.Debug.WriteLine($"[HAUPTCODE ERROR] Das Gerät '{parentFunction.Name}' besitzt keine zugewiesenen Artikelreferenzen.");
//            }
//        }
//        catch (BaseException ex)
//        {
//            System.Diagnostics.Debug.WriteLine("[HAUPTCODE CRITICAL EXCEPTION] Fehler im globalen Ablauf: " + ex.Message);
//        }
//    }
//}



//using Eplan.EplApi.Base.Enums;
//using Eplan.EplApi.Base; // WICHTIG: Für BaseException und PathMap benötigt!
//using Eplan.EplApi.DataModel;
//using Eplan.EplApi.DataModel.Graphics;
//using Eplan.EplApi.DataModel.MasterData;
//using Eplan.EplApi.HEServices;
//using Eplan.EplApi.ApplicationFramework; // WICHTIG: Für CommandLineInterpreter benötigt!
//using Rosler.EplAddin.InsertSymbolMacroByPlaceholder;
//using System;
//using System.Collections.Generic;
//using System.IO;
//using static Eplan.EplApi.HEServices.Renumber.Enums;

//public class PlaceMacroAtPlaceholder
//{
//    public void PlaceMacroPlaceholder(Page targetPage, string placeholderName)
//    {
//        // 1. Alle Platzhalter-Anker auf dieser Seite sammeln, die dem Suchnamen entsprechen
//        Placement[] allPlacements = targetPage.AllPlacements;
//        List<PointD> targetLocations = new List<PointD>();

//        foreach (Placement placement in allPlacements)
//        {
//            if (placement is PlaceHolder placeHolder)
//            {
//                if (placeHolder.Name.Equals(placeholderName, System.StringComparison.OrdinalIgnoreCase))
//                {
//                    targetLocations.Add(placeHolder.Location);
//                }
//            }
//        }

//        // Wenn kein passender Anker auf der Seite ist, können wir abbrechen
//        if (targetLocations.Count == 0) return;

//        Insert insertService = new Insert();
//        PhasenschienenService schienenService = new PhasenschienenService(); // Service aufrufen

//        // KORREKTUR 1: 'targetLocations.Count' statt dem nicht deklarierten 'placementsToMake' verwenden
//        for (int i = 0; i < targetLocations.Count; i++)
//        {
//            PointD currentAnchor = targetLocations[i];

//            try
//            {
//                // SCHRITT 2: Dynamische Artikelermittlung und Tausch komplett ausgelagert
//                var ergebnis = schienenService.GetOrPreparePhasenschiene(targetPage, 3);
//                Function parentFunction = ergebnis.ParentFunction;
//                int variantIndex = ergebnis.VariantIndex;

//                // KORREKTUR 2: Über das Array der ArticleReferences die erste Referenz typsicher herausholen
//                ArticleReference currentArtRef = null;
//                if (parentFunction.ArticleReferences != null && parentFunction.ArticleReferences.Length > 0)
//                {
//                    currentArtRef = parentFunction.ArticleReferences[0];
//                }

//                if (currentArtRef != null)
//                {
//                    Article articleData = currentArtRef.Article;

//                    if (articleData != null)
//                    {
//                        string articleMacroPath = articleData.Properties.ARTICLE_GROUPSYMBOLMACRO;

//                        if (!string.IsNullOrEmpty(articleMacroPath))
//                        {
//                            articleMacroPath = PathMap.SubstitutePath(articleMacroPath);

//                            if (File.Exists(articleMacroPath))
//                            {
//                                SymbolMacro macro = new SymbolMacro();
//                                macro.Open(articleMacroPath);

//                                // Das Makro an der Position einfügen
//                                StorableObject[] newPlacements = insertService.SymbolMacro(
//                                    macro,
//                                    variantIndex,
//                                    targetPage,
//                                    currentAnchor,
//                                    Insert.MoveKind.Absolute,
//                                    SymbolMacro.Enums.NumerationMode.None
//                                );

//                                // SCHRITT 3: Identität übertragen und BMK dynamisch kürzen ausgelagert
//                                schienenService.UpdateMacroPlacements(targetPage, parentFunction, newPlacements);

//                                // --- JETZT DIREKT KORREKT AKTUALISIEREN ---
//                                Generate generateService = new Generate();
//                                generateService.Connections(targetPage.Project);

//                                CommandLineInterpreter interpreter = new CommandLineInterpreter();
//                                interpreter.Execute("GedRedraw");
//                            }
//                        }
//                    }
//                }
//            }
//            catch (BaseException ex)
//            {
//                // Fehlerbehandlung falls keine Schiene im Projekt aktiv ist
//            }
//        }
//    }
//}