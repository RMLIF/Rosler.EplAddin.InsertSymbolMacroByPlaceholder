using Eplan.EplApi.Base;
using Eplan.EplApi.Base.Enums;
using Eplan.EplApi.DataModel;
using Eplan.EplApi.HEServices;
using Eplan.EplApi.MasterData;
using System;
using System.Collections.Generic;

public class PhasenschienenService
{
    public class SchienenErgebnis
    {
        public Function ParentFunction { get; set; }
        public int VariantIndex { get; set; }
    }

    public SchienenErgebnis GetOrPreparePhasenschiene(Page targetPage, int neededSlotsForThisPlacement)
    {
        System.Diagnostics.Debug.WriteLine($"[START] GetOrPreparePhasenschiene aufgerufen für Seite: '{targetPage.Name}' | Benötigte Anschlüsse: {neededSlotsForThisPlacement}");

        DMObjectsFinder finder = new DMObjectsFinder(targetPage.Project);

        System.Diagnostics.Debug.WriteLine("[INFO] Lese alle Artikelreferenzen aus dem aktuellen EPLAN-Projektpool...");
        ArticleReference[] projectArticles = finder.GetArticleReferences(null);
        System.Diagnostics.Debug.WriteLine($"[INFO] {projectArticles.Length} Artikelreferenzen im Projekt gefunden.");

        ArticleReference baseArtRef = null;
        string targetDescription1 = "";
        string targetManufacturer = "";
        string targetProductGroup = "";

        System.Diagnostics.Debug.WriteLine("[SCHRITT 1] Starte zielgerichtete Suche nach der platzierten Phasenschiene (-WC)...");

        // 1. Platziertes Basis-Gerät gezielt über das reale Betriebsmittel suchen
        foreach (ArticleReference artRef in projectArticles)
        {
            // DYNAMISCHER VORFILTER: Wenn das BMK kein "WC" enthält, sofort überspringen
            if (!artRef.IdentifyingName.Contains("-WC") && !artRef.IdentifyingName.Contains("WC"))
            {
                // Sehr schlanke Logik: Überspringt z.B. Motorschutzschalter ohne Logik-Overhead
                continue;
            }

            System.Diagnostics.Debug.WriteLine($"[PRÜFUNG] Passendes BMK identifiziert: '{artRef.IdentifyingName}'. Untersuche logische Funktion...");

            // Das logische EPLAN-Objekt (die Funktion) ermitteln
            Function parentFunc = artRef.ParentObject as Function;

            if (parentFunc != null)
            {
                System.Diagnostics.Debug.WriteLine($"[DETAILS] Gerät: '{parentFunc.Name}' | IstHauptfunktion: {parentFunc.IsMainFunction} | BesitztArtikeldaten: {(artRef.Article != null ? "Ja" : "Nein")}");

                // WICHTIGE ERGÄNZUNG FÜR PHASENSCHIENEN: 
                // Wir lesen die Daten NUR aus, wenn wir auf der HAUPTFUNKTION (dem Gerätekasten) stehen.
                // Die einzelnen Geräteanschlüsse (L1.1, L2.1 etc.) werden übersprungen, da sie keine Artikeltexte enthalten.
                if (parentFunc.IsMainFunction && artRef.Article != null)
                {
                    System.Diagnostics.Debug.WriteLine("[LOGIK] Hauptfunktion mit Artikelverknüpfung erreicht. Extrahiere Stammdaten...");

                    var propDesc1 = artRef.Article.Properties.ARTICLE_DESCR1;
                    var propManuf = artRef.Article.Properties.ARTICLE_MANUFACTURER;
                    var propProdG = artRef.Article.Properties.ARTICLE_PRODUCTGROUP;

                    string desc1 = propDesc1 != null ? propDesc1.ToString() : string.Empty;
                    string manuf = propManuf != null ? propManuf.ToString() : string.Empty;
                    string prodg = propProdG != null ? propProdG.ToString() : string.Empty;

                    // ERGÄNZUNG: Produktgruppe (prodg) in die Doku-Ausgabe mit aufgenommen
                    System.Diagnostics.Debug.WriteLine($"[STAMMDATEN] Gelesen -> Bezeichnung 1: '{desc1}' | Hersteller: '{manuf}' | Produktgruppe: '{prodg}'");

                    if (!string.IsNullOrEmpty(desc1))
                    {
                        baseArtRef = artRef;
                        targetDescription1 = desc1; // Holt jetzt garantiert "Phasenschiene" aus dem Kasten
                        targetManufacturer = manuf;
                        targetProductGroup = prodg; // Wird fehlerfrei an Schritt 2 übergeben

                        System.Diagnostics.Debug.WriteLine($"[ERFOLG] Ziel-Phasenschiene eindeutig identifiziert: '{parentFunc.Name}'. Suche wird beendet.");
                        break; // Phasenschiene erfolgreich identifiziert -> Schleife beenden
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[WARNUNG] Artikelbescheinigung 1 war leer. Suche läuft weiter...");
                    }
                }
            }
        }

        // Sicherheitsprüfung, falls die Schleife ohne Fund durchlief
        if (string.IsNullOrEmpty(targetDescription1))
        {
            System.Diagnostics.Debug.WriteLine("[ABBRUCH] Es konnte keine aktive, platzierte Phasenschiene im Projekt ermittelt werden.");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[INFO] Parameter für Folgeschritte fixiert -> Filter-Suchbegriff: '{targetDescription1}' | Ziel-Hersteller: '{targetManufacturer}'");
        }

        System.Diagnostics.Debug.WriteLine("[SCHRITT 2] Starte Suche nach unplatzierten Tausch-Schienen in den Stammdaten...");



        // =========================================================================
        // SCHRITT 2: DYNAMISCHE FILTERUNG DIREKT IN DER ARTIKELDATENBANK
        // =========================================================================
        System.Diagnostics.Debug.WriteLine($"[SCHRITT 2] Nutze MDObjectFilter für direkten Datenbank-Scan (Hersteller: '{targetManufacturer}' | Produktgruppe-ID: '{targetProductGroup}')...");

        ArticleReference selectedArtRef = null;
        string fallbackPartNumber = "";
        int highestTemplateCount = 0;

        List<string> gueltigeSchienenTeilenummern = new List<string>();
        MDPart[] filteredParts = null;
        // WICHTIG: Das PartsManagement ebenfalls in ein using packen, um RAM-Abstürze (Heap-Corruption) zu verhindern
        MDPartsManagement partsManagement = new MDPartsManagement();
        {
            using (MDPartsDatabase partsDatabase = partsManagement.OpenDatabase())
            {
                if (partsDatabase != null)
                {
                    MDObjectFilter mdFilter = new MDObjectFilter();

                    // ERGÄNZUNG: Wir prüfen nun sauber die Variable 'targetProductGroup' (die '129' aus Schritt 1)
                    string produktGruppenIdText = "129"; // Sicherer Standard-Wert für Phasenschienen
                    if (!string.IsNullOrEmpty(targetProductGroup))
                    {
                        produktGruppenIdText = targetProductGroup; // Nutzt die live gelesene Produktgruppe
                    }

                    // KORREKTUR: Typ-Cast auf EPLAN-Enum angewendet und ID als geforderten String übergeben
                    mdFilter.AddPropertyCondition(22041, MDObjectFilter.CompareOperator.OperatorEqual, produktGruppenIdText);


                    // KORREKTUR: Zweite Bedingung (Hersteller, ID 22007) ebenfalls mit Typ-Cast absichern
                    mdFilter.AddPropertyCondition(22007, MDObjectFilter.CompareOperator.OperatorEqual, targetManufacturer);

                    // Gefilterte Artikel direkt hochperformant aus der Teiledatenbank abrufen
                    filteredParts = partsDatabase.GetParts(mdFilter);
                    System.Diagnostics.Debug.WriteLine($" -> Datenbank-Filter aktiv. Es wurden {filteredParts.Length} exakt passende Schienen-Stammdaten geladen.");

                    foreach (MDPart part in filteredParts)
                    {
                        gueltigeSchienenTeilenummern.Add(part.PartNr);

                        // Wir zählen direkt in den geladenen Schablonen die echten Pins
                        int totalPins = 0;
                        foreach (MDFunctionTemplatePosition templatePosition in part.FunctionTemplatePositions)
                        {
                            string categoryText = templatePosition.FunctionDefinitionCategory.ToString();
                            if (!categoryText.Equals("Blackbox", StringComparison.OrdinalIgnoreCase))
                            {
                                totalPins++;
                            }
                        }

                        System.Diagnostics.Debug.WriteLine($"   -> Schiene verifiziert: '{part.PartNr}' | Kapazität: {totalPins} Pins");

                        if (totalPins > highestTemplateCount)
                        {
                            highestTemplateCount = totalPins;
                            fallbackPartNumber = part.PartNr;
                        }
                    }
                }
            }
        }

        // Map-Abgleich gegen die unplatzierten Referenzen im Projektpool
        foreach (ArticleReference artRef in projectArticles)
        {
            if (!artRef.BelongsToArticlePlacement)
            {
                if (gueltigeSchienenTeilenummern.Contains(artRef.PartNr))
                {
                    selectedArtRef = artRef;
                }
            }
        }

        Function parentFunction = null;
        int currentOccupiedSlots = 0;

        if (selectedArtRef != null) parentFunction = selectedArtRef.ParentObject as Function;

        if (parentFunction != null)
        {
            foreach (Function subFunc in parentFunction.SubFunctions)
            {
                if (subFunc.IsPlaced) currentOccupiedSlots++;
            }
        }

        int totalNeededSlots = currentOccupiedSlots + neededSlotsForThisPlacement;
        int currentArticleMaxSlots = selectedArtRef != null ? GetMaxSlotsFromPartsDatabase(selectedArtRef.PartNr, "1") : 0;



        System.Diagnostics.Debug.WriteLine("[BERECHNUNG-ERGEBNIS]");
        System.Diagnostics.Debug.WriteLine($" -> Bereits im Schaltplan platzierte Pins der Schiene: {currentOccupiedSlots}");
        System.Diagnostics.Debug.WriteLine($" -> Neu hinzukommende Pins durch dieses Makro:        {neededSlotsForThisPlacement}");
        System.Diagnostics.Debug.WriteLine($" -> Zukünftig benötigte Gesamt-Pin-Anzahl:            {totalNeededSlots}");
        System.Diagnostics.Debug.WriteLine($" -> Maximale Kapazität der größten bekannten Schiene: {highestTemplateCount}");



        System.Diagnostics.Debug.WriteLine($"[INFO] Aktueller Artikel '{(selectedArtRef != null ? selectedArtRef.PartNr : "Keiner")}' bietet maximal {currentArticleMaxSlots} Pins.");

        System.Diagnostics.Debug.WriteLine("[SCHRITT 3] Starte Validierung: Artikeltausch (Skalierung) oder Neuanlage/Kaskadierung prüfen...");



        // =========================================================================
        // SCHRITT 3: AUSWAHL DES RICHTIGEN ARTIKELS AUS DER GEFILTERTEN DATENBANK-LISTE
        // =========================================================================
        System.Diagnostics.Debug.WriteLine("[SCHRITT 3] Starte Artikeltausch direkt basierend auf der verifizierten Teile-Liste...");

        if (parentFunction != null && selectedArtRef != null)
        {
            System.Diagnostics.Debug.WriteLine($"[LOGIK] Bestehendes Gerät '{parentFunction.Name}' wird auf Größenänderung (Skalierung) geprüft...");

            // Wenn die aktuelle Größe nicht exakt zu den benötigten Anschlüssen passt
            if (totalNeededSlots != currentArticleMaxSlots && totalNeededSlots <= highestTemplateCount)
            {
                System.Diagnostics.Debug.WriteLine($"[MISMÄTCH] Größe passt nicht exakt! Benötigt: {totalNeededSlots} Pins | Aktuell: {currentArticleMaxSlots} Pins. Suche optimales Up- oder Downgrade...");

                string optimalPartNumber = selectedArtRef.PartNr;
                string optimalVariantNumber = selectedArtRef.VariantNr; // Standardmäßig bestehende Variante beibehalten
                int optimalSlots = highestTemplateCount;

                // ABSOLUTER PERFORMANCE-BOOST: Wir durchlaufen NUR NOCH die 7 verifizierten 
                // Datenbank-Treffer (filteredParts) aus Schritt 2, anstatt das riesige Projekt-Array!
                foreach (Eplan.EplApi.MasterData.MDPart part in filteredParts)
                {
                    // Die Pins lesen wir direkt aus der bereits im RAM liegenden Schablone ab
                    int slots = 0;
                    foreach (Eplan.EplApi.MasterData.MDFunctionTemplatePosition templatePosition in part.FunctionTemplatePositions)
                    {
                        string categoryText = templatePosition.FunctionDefinitionCategory.ToString();
                        if (!categoryText.Equals("Blackbox", StringComparison.OrdinalIgnoreCase))
                        {
                            slots++;
                        }
                    }

                    System.Diagnostics.Debug.WriteLine($" -> [RAM-EVALUIERUNG] Prüfe Schiene: '{part.PartNr}' (Var: {part.Variant}) mit {slots} Pins.");

                    // Wir suchen die kleinste Schiene, die groß genug für alle Anschlüsse ist (Automatischer Verkleinerungseffekt)
                    if (slots >= totalNeededSlots && slots < optimalSlots)
                    {
                        optimalSlots = slots;
                        optimalPartNumber = part.PartNr;
                        optimalVariantNumber = part.Variant; // Variante dynamisch aus der Datenbank übernehmen
                    }
                }

                // Wenn ein optimaler Artikel (Up- oder Downgrade) gefunden wurde, tauschen wir ihn aus
                if (optimalPartNumber != selectedArtRef.PartNr || optimalVariantNumber != selectedArtRef.VariantNr)
                {
                    System.Diagnostics.Debug.WriteLine($"[ARTIKELTAUSCH] Führe Skalierung durch: Wechsel zu '{optimalPartNumber}' (Variante: '{optimalVariantNumber}', {optimalSlots} Pins).");

                    // 1. Automatischen Sicherheits- und Sperrkontext für das EPLAN-Datenmodell öffnen
                    // 🟢 NEU: Automatischen Sicherheits- und Sperrkontext öffnen
                    using (SafetyPoint safetyPoint = SafetyPoint.Create())
                    {
                        ArticleReference[] aktuelleReferenzen = parentFunction.ArticleReferences;
                        if (aktuelleReferenzen != null && aktuelleReferenzen.Length > 0)
                        {
                            // 🛠️ KORREKTUR: Greift gezielt das erste Element ab (Behebt Typenfehler)
                            ArticleReference editierbareArtRef = aktuelleReferenzen[0];

                            // 🛠️ KORREKTUR: Schreibt die tatsächliche NEUE Artikelnummer & Variante
                            editierbareArtRef.PartNr = optimalPartNumber;
                            editierbareArtRef.VariantNr = optimalVariantNumber;
                            editierbareArtRef.Count = 1;

                            // 🟢 NEU: Änderungen fest in die Projektdatenbank schreiben
                            editierbareArtRef.StoreToObject();
                            safetyPoint.Commit();
                        }
                        else
                        {
                            // 🛠️ KORREKTUR: Parameter für Anzahl (1) und Clean (false) explizit ergänzt
                            parentFunction.AddArticleReference(optimalPartNumber, optimalVariantNumber, 1, false);
                            safetyPoint.Commit();
                        }
                    } // Beim Verlassen des using-Blocks wird die automatische Sperre sofort wieder aufgehoben

                    // 6. Das Gerät aktualisieren, um die neuen Schablonen im Schaltplan/Navigator zu synchronisieren
                    new DeviceService().UpdateDevice(parentFunction);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[INFO] Der aktuell zugewiesene Artikel '{selectedArtRef.PartNr}' ist bereits die bestmögliche Schienengröße.");
                }
            }
            else if (totalNeededSlots > highestTemplateCount)
            {
                // KASKADIERUNG: Wenn selbst die größte Schiene zu klein ist -> Folgeschiene im Navigator eröffnen (z.B. -WC2)
                System.Diagnostics.Debug.WriteLine($"[KASKADIERUNG] Limit überschritten! Benötigt: {totalNeededSlots} Pins | Maximal verfügbare Größe: {highestTemplateCount} Pins.");
                System.Diagnostics.Debug.WriteLine($"[KASKADIERUNG] Erzeuge ein zusätzliches, unplatziertes Folgegerät mit dem Artikel '{fallbackPartNumber}' via CreateNewUnplacedDevice...");

                parentFunction = CreateNewUnplacedDevice(targetPage.Project, fallbackPartNumber);
                currentOccupiedSlots = 0; // Der Platzierungs-Zähler fängt bei der neuen Schiene wieder ganz vorne an

                // Nach Neuanlage sicherstellen, dass Variante und Anzahl am neuen Gerät stimmen
                if (parentFunction != null && parentFunction.ArticleReferences.Length > 0)
                {
                    Eplan.EplApi.DataModel.ArticleReference newArtRef = parentFunction.ArticleReferences[0];
                    newArtRef.LockObject();
                    newArtRef.VariantNr = "1"; // Standardvariante für Neuanlage
                    newArtRef.Count = 1;       // Anzahl auf 1 setzen
                    newArtRef.StoreToObject();

                    new DeviceService().UpdateDevice(parentFunction);
                }

                System.Diagnostics.Debug.WriteLine($"[KASKADIERUNG ERFOLG] Neues Folgegerät angelegt: '{(parentFunction != null ? parentFunction.Name : "Fehler/Null")}'.");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[INFO] Die aktuelle Schienengröße ist bereits optimal dimensioniert. Kein Tausch notwendig.");
            }
        }
        else
        {
            // ERST-NEUANLAGE: Wenn im gesamten Projekt noch gar keine unplatzierte Schiene vorhanden war
            System.Diagnostics.Debug.WriteLine("[LOGIK] Kein aktives Gerät im Projekt gefunden. Leite Erst-Neuanlage ein...");

            if (!string.IsNullOrEmpty(fallbackPartNumber))
            {
                System.Diagnostics.Debug.WriteLine($"[ERST-NEUANLAGE] Erzeuge das erste unplatzierte Basisgerät mit Artikel '{fallbackPartNumber}' via CreateNewUnplacedDevice...");

                parentFunction = CreateNewUnplacedDevice(targetPage.Project, fallbackPartNumber);
                currentOccupiedSlots = 0;

                // Nach Erst-Neuanlage ebenfalls Variante und Anzahl initialisieren
                if (parentFunction != null && parentFunction.ArticleReferences.Length > 0)
                {
                    Eplan.EplApi.DataModel.ArticleReference newArtRef = parentFunction.ArticleReferences[0];
                    newArtRef.LockObject();
                    newArtRef.VariantNr = "1"; // Standardvariante für Erst-Neuanlage
                    newArtRef.Count = 1;       // Anzahl auf 1 setzen
                    newArtRef.StoreToObject();

                    new DeviceService().UpdateDevice(parentFunction);
                }

                System.Diagnostics.Debug.WriteLine($"[ERST-NEUANLAGE ERFOLG] Basisgerät angelegt: '{(parentFunction != null ? parentFunction.Name : "Fehler/Null")}'.");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[CRITICAL] Abbruch: Keine valide Artikelnummer aus dem MDObjectFilter zur Erst-Neuanlage vorhanden.");
                throw new BaseException("Kein passender Artikel in der Liste zur Neuanlage vorhanden.", MessageLevel.Warning);
            }
        }

        // Berechnung des VariantenIndexes für das Makro (Variante A, B, C...)
        int variantIndex = currentOccupiedSlots / 3;
        if (variantIndex > 4) variantIndex = 4; // Sicherheitsbegrenzung auf Variante E (maximal 15 Pins auf einer physikalischen Schiene)

        System.Diagnostics.Debug.WriteLine($"[ENDE] GetOrPreparePhasenschiene abgeschlossen. Berechneter Makro-VariantenIndex: {variantIndex} (Variante {(char)('A' + variantIndex)})\n");

        return new SchienenErgebnis
        {
            ParentFunction = parentFunction,
            VariantIndex = variantIndex
        };
    }








    public void UpdateMacroPlacements(Page targetPage, Function parentFunction, StorableObject[] newPlacements)
    {
        if (parentFunction == null || newPlacements == null) return;

        NameService nameService = new NameService(targetPage);
        DeviceService deviceService = new DeviceService();

        foreach (StorableObject newPlacement in newPlacements)
        {
            if (newPlacement is Function newFunction)
            {
                newFunction.LockObject();

                if (newFunction.FunctionCategory == FunctionCategory.Blackbox)
                {
                    newFunction.Name = parentFunction.Name;

                    string vollBMK = parentFunction.Name;
                    string seitenStruktur = targetPage.IdentifyingName;

                    int trennerIndex = vollBMK.IndexOf('-');
                    string strukturAnteil = trennerIndex >= 0 ? vollBMK.Substring(0, trennerIndex) : vollBMK;
                    string bmAnteil = trennerIndex >= 0 ? vollBMK.Substring(trennerIndex) : "";

                    List<string> seitenSegmente = new List<string>();
                    string aktuellesSegment = "";

                    for (int k = 0; k < seitenStruktur.Length; k++)
                    {
                        char c = seitenStruktur[k];
                        if (c == '=' || c == '+' || c == '&' || c == '#')
                        {
                            if (aktuellesSegment.Length > 0 && aktuellesSegment != "=" && aktuellesSegment != "+" && aktuellesSegment != "&" && aktuellesSegment != "#")
                            {
                                seitenSegmente.Add(aktuellesSegment);
                                aktuellesSegment = "";
                            }
                        }
                        aktuellesSegment += c;
                    }
                    if (!string.IsNullOrEmpty(aktuellesSegment)) seitenSegmente.Add(aktuellesSegment);

                    foreach (string segment in seitenSegmente)
                    {
                        if (strukturAnteil.Contains(segment))
                        {
                            strukturAnteil = strukturAnteil.Replace(segment, "");
                        }
                    }

                    newFunction.VisibleName = strukturAnteil + bmAnteil;
                    nameService.AdjustFullName(newFunction);
                    deviceService.UpdateDevice(newFunction, true);
                }
                if (newFunction.FunctionCategory != FunctionCategory.Blackbox)
                {
                    newFunction.Name = parentFunction.Name;
                    newFunction.VisibleName = string.Empty;
                    nameService.AdjustFullName(newFunction);
                    deviceService.UpdateDevice(newFunction, true);
                }
            }
        }
    }

    public void GetFunctionTemplatesOfPart(string partNumber, string variantNumber)
    {
        // 1. Artikeldatenbank initialisieren und öffnen
        MDPartsManagement partsManagement = new MDPartsManagement();
        MDPartsDatabase partsDatabase = partsManagement.OpenDatabase();

        // 2. Den spezifischen Artikel anhand von Nummer und Variante abrufen
        MDPart part = partsDatabase.GetPart(partNumber, variantNumber);

        if (part != null)
        {
            // 3. Iteration über alle Positionen der Funktionsschablone
            foreach (MDFunctionTemplatePosition templatePosition in part.FunctionTemplatePositions)
            {
                // Basis-Informationen der Funktionsdefinition auslesen
                string category = templatePosition.FunctionDefinitionCategory.ToString();
                string group = templatePosition.FunctionDefinitionGroup.ToString();

                // Spezifische Eigenschaften über die PropertyList abfragen (z.B. Anschlussbezeichnungen)
                string designation = templatePosition.Properties.FUNC_ALLCONNECTIONDESCRIPTIONS;

                // Ausgabe oder Weiterverarbeitung der Daten
                System.Diagnostics.Debug.WriteLine($"Kategorie: {category}, Gruppe: {group}, Anschluss: {designation}");

                // Optional: Typprüfung für spezialisierte Schablonen (z.B. Klemmen)
                if (templatePosition is MDTerminalTemplatePosition terminalTemplate)
                {
                    string connPointDescr = terminalTemplate.ConnectionPointDescription;
                }
            }
        }
    }


    public int GetMaxSlotsFromPartsDatabase(string partNumber, string variantNumber)
    {
        int totalPins = 0;

        // Durch 'using' wird das Parts-Management nach dem Block sauber freigegeben
        MDPartsManagement partsManagement = new MDPartsManagement();
        {
            MDPartsDatabase partsDatabase = null;
            try
            {
                // Datenbank öffnen
                partsDatabase = partsManagement.OpenDatabase();

                if (partsDatabase != null)
                {
                    MDPart part = partsDatabase.GetPart(partNumber, variantNumber);

                    if (part != null)
                    {
                        foreach (MDFunctionTemplatePosition templatePosition in part.FunctionTemplatePositions)
                        {
                            string categoryText = templatePosition.FunctionDefinitionCategory.ToString();

                            // Nur echte Pins zählen (Blackbox-Kasten ignorieren)
                            if (!categoryText.Equals("Blackbox", StringComparison.OrdinalIgnoreCase))
                            {
                                totalPins++;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WARNUNG] Fehler beim Lesen der Schablone für Artikel {partNumber}: {ex.Message}");
                totalPins = 15; // Sicherer Fallback, falls die Datenbank blockiert ist
            }
            finally
            {
                // WICHTIG: Die offene Datenbank explizit schließen, um Heap-Korruption (0xc0000374) zu verhindern
                if (partsDatabase != null)
                {
                    partsDatabase.Close();
                }
            }
        }

        return totalPins;
    }

    public Function CreateNewUnplacedDevice(Project project, string partNumber)
    {
        try
        {
            DeviceService devService = new DeviceService();
            FunctionPropertyList hierarchyList = new FunctionPropertyList(); // Leer lassen für Autonummerierung

            // Erzeugt das Gerät im Hintergrund im Navigator
            Function[] createdDevices = devService.CreateDevice(project, partNumber, "1", hierarchyList);

            if (createdDevices != null && createdDevices.Length > 0)
            {
                // Gibt die Hauptfunktion des erzeugten Artikels zurück
                return createdDevices[0];
            }
        }
        catch (Exception ex)
        {
            throw new BaseException("Fehler beim Erzeugen des unplatzierten Geräts für Artikel " + partNumber + ": " + ex.Message, MessageLevel.Warning);
        }

        return null;
    }
}

