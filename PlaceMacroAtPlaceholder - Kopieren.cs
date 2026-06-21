using Eplan.EplApi.Base;
using Eplan.EplApi.Base.Enums;
using Eplan.EplApi.DataModel;
using Eplan.EplApi.DataModel.Graphics;
using Eplan.EplApi.DataModel.MasterData;
using Eplan.EplApi.HEServices;
using System;
using System.Collections.Generic;
using System.IO;
using static Eplan.EplApi.HEServices.Renumber.Enums;

public class PlaceMacroAtPlaceholder
{
    public void PlaceMacroPlaceholder(Page targetPage, string placeholderName)
    {
        // 1. Alle Platzhalter-Anker auf dieser Seite sammeln, die dem Suchnamen entsprechen
        Placement[] allPlacements = targetPage.AllPlacements;
        List<PointD> targetLocations = new List<PointD>();

        foreach (Placement placement in allPlacements)
        {
            if (placement is PlaceHolder placeHolder)
            {
                if (placeHolder.Name.Equals(placeholderName, System.StringComparison.OrdinalIgnoreCase))
                {
                    targetLocations.Add(placeHolder.Location);
                }
            }
        }

        // Wenn kein passender Anker auf der Seite ist, können wir abbrechen
        if (targetLocations.Count == 0) return;

        // 2. Alle freien (unplatzierten) Phasenschienen-Artikel im PROJEKT ermitteln
        DMObjectsFinder finder = new DMObjectsFinder(targetPage.Project);

        // Wir holen alle im Projekt eingebuchten Artikelreferenzen
        ArticleReference[] projectArticles = finder.GetArticleReferences(null);
        List<ArticleReference> freePhasenschienen = new List<ArticleReference>();

        string targetPartNumber = "200011637"; // Ihre Teilenummer aus dem Screenshot

        foreach (ArticleReference artRef in projectArticles)
        {
            // Entspricht der Artikel der gesuchten Phasenschiene?
            if (artRef.PartNr.Equals(targetPartNumber, System.StringComparison.OrdinalIgnoreCase))
            {
                // Prüfen, ob der Artikel unplatziert im Baum liegt
                if (!artRef.BelongsToArticlePlacement)
                {
                    freePhasenschienen.Add(artRef);
                }
            }
        }

        // 3. Platzierung durchführen (maximal so viele, wie freie Plätze ODER Anker da sind)
        int placementsToMake = System.Math.Min(targetLocations.Count, freePhasenschienen.Count);

        if (placementsToMake == 0)
        {
            new BaseException($"Keine freien (unplatzierten) Artikel für Teilenummer '{targetPartNumber}' im Projekt gefunden.", MessageLevel.Warning);
            return;
        }

        Insert insertService = new Insert();

        for (int i = 0; i < placementsToMake; i++)
        {
            PointD currentAnchor = targetLocations[i];
            ArticleReference currentArtRef = freePhasenschienen[i];

            // NEU: Belegte Plätze über die übergeordnete Funktion ermitteln
            int variantIndex = 0; // Standard: Variante A

            // Die Funktion holen, an der dieser Artikel hängt
            Function parentFunction = currentArtRef.ParentObject as Function;

            if (parentFunction != null)
            {
                int occupiedSlots = 0;

                // Alle logischen Anschlüsse (L1.1 bis L3.5) durchlaufen
                foreach (Function subFunc in parentFunction.SubFunctions)
                {
                    // Nur die im Schaltplan bereits platzierten Anschlüsse zählen
                    if (subFunc.IsPlaced)
                    {
                        occupiedSlots++;
                    }
                }

                // DYNAMISCHE FORMEL: Berechnet automatisch den Index (0, 1, 2, 3...)
                // Bei 9 platzierten Anschlüssen ergibt 9 / 3 = Index 3 (Variante D)
                variantIndex = occupiedSlots / 3;

                // Sicherheitsprüfung: Falls das Makro nur bis Variante E (Index 4) geht
                if (variantIndex > 4)
                {
                    variantIndex = 4;
                }
            }

            Article articleData = currentArtRef.Article;

            if (articleData != null)
            {
                // Pfad aus den geladenen Artikelstammdaten lesen
                string articleMacroPath = articleData.Properties.ARTICLE_GROUPSYMBOLMACRO;

                if (!string.IsNullOrEmpty(articleMacroPath))
                {
                    articleMacroPath = PathMap.SubstitutePath(articleMacroPath);

                    if (File.Exists(articleMacroPath))
                    {
                        SymbolMacro macro = new SymbolMacro();
                        macro.Open(articleMacroPath);

                        // Das Makro an der Position einfügen
                        StorableObject[] newPlacements = insertService.SymbolMacro(
                            macro,
                            variantIndex,
                            targetPage,
                            currentAnchor,
                            Insert.MoveKind.Absolute,
                            //SymbolMacro.Enums.NumerationMode.None
                            SymbolMacro.Enums.NumerationMode.None
                        );
                        //Identität und BMK der parentFunction übertragen
                        if (parentFunction != null && newPlacements != null)
                        {
                            NameService nameService = new NameService(targetPage);
                            DeviceService deviceService = new DeviceService();

                            foreach (StorableObject newPlacement in newPlacements)
                            {
                                if (newPlacement is Function newFunction)
                                {
                                    newFunction.LockObject();

                                    // FALL A: Es ist KEIN Gerätekasten (also ein Geräteanschluss)
                                    if (newFunction.FunctionCategory != FunctionCategory.DeviceEndTerminal)
                                    {
                                        newFunction.Name = parentFunction.Name;
                                        newFunction.VisibleName = string.Empty; // Anschlüsse bleiben im Plan textfrei
                                    }
                                    // FALL B: Es ist der Gerätekasten selbst
                                    else
                                    {
                                        // 1. Dem Kasten die volle Identität im Hintergrund übergeben
                                        newFunction.Name = parentFunction.Name;

                                        // 2. VOLLKOMMEN DYNAMISCHE KÜRZUNG (Funktioniert in jedem Projekt)
                                        string vollBMK = parentFunction.Name;
                                        string seitenStruktur = targetPage.IdentifyingName;

                                        // Trennung in Strukturanteil (vor dem '-') und Betriebsmittelanteil (nach dem '-')
                                        int trennerIndex = vollBMK.IndexOf('-');
                                        string strukturAnteil = trennerIndex >= 0 ? vollBMK.Substring(0, trennerIndex) : vollBMK;
                                        string bmAnteil = trennerIndex >= 0 ? vollBMK.Substring(trennerIndex) : "";

                                        // Seitenstruktur dynamisch in einzelne Segmente zerlegen (Sucht nach =, +, &, #)
                                        List<string> seitenSegmente = new List<string>();
                                        string aktuellesSegment = "";

                                        for (int d = 0; d < seitenStruktur.Length; d++)
                                        {
                                            char c = seitenStruktur[d];
                                            if (c == '=' || c == '+' || c == '&' || c == '#')
                                            {
                                                // Sobald ein neues Kennzeichen beginnt, das alte Segment speichern
                                                if (aktuellesSegment.Length > 0 && aktuellesSegment[0] != '=' && aktuellesSegment[0] != '+' && aktuellesSegment[0] != '&' && aktuellesSegment[0] != '#')
                                                {
                                                    seitenSegmente.Add(aktuellesSegment);
                                                    aktuellesSegment = "";
                                                }
                                            }
                                            aktuellesSegment += c;
                                        }
                                        if (!string.IsNullOrEmpty(aktuellesSegment)) seitenSegmente.Add(aktuellesSegment);

                                        // Alle Segmente der aktuellen Seite im Strukturanteil des Geräts eliminieren
                                        foreach (string segment in seitenSegmente)
                                        {
                                            if (strukturAnteil.Contains(segment))
                                            {
                                                strukturAnteil = strukturAnteil.Replace(segment, "");
                                            }
                                        }

                                        // Das dynamisch gekürzte BMK zusammensetzen und zuweisen
                                        newFunction.VisibleName = strukturAnteil + bmAnteil;
                                    }

                                    // Visuelle Darstellung und Zuordnung im Projekt aktualisieren
                                    nameService.AdjustFullName(newFunction);
                                    deviceService.UpdateDevice(newFunction, true);
                                }
                            }
                        }

                        ////Identität und BMK der parentFunction übertragen
                        //if (parentFunction != null && newPlacements != null)
                        //{
                        //    NameService nameService = new NameService(targetPage);
                        //    DeviceService deviceService = new DeviceService();

                        //    foreach (StorableObject newPlacement in newPlacements)
                        //    {
                        //        if (newPlacement is Function newFunction)
                        //        {
                        //            newFunction.LockObject();

                        //            // Wir suchen gezielt NUR den Gerätekasten (Box) oder die Hauptfunktion

                        //            if (newFunction.FunctionCategory == FunctionCategory.Blackbox)
                        //            {
                        //                // 1. Dem Kasten die Identität des bestehenden Geräts übergeben
                        //                newFunction.Name = parentFunction.Name;
                        //                newFunction.VisibleName = parentFunction.Name;

                        //                nameService.AdjustFullName(newFunction);

                        //                // 2. DAS GEHEIMNIS: Wir überlagern das gesamte Gerät!
                        //                // Durch "UpdateDevice" mit dem Parameter "true" (oder nachfolgendem Consolidate)
                        //                // nimmt sich EPLAN den Kasten und verschmilzt ALLE im Makro enthaltenen 
                        //                // Unterfunktionen automatisch mit den freien, unplatzierten Anschlüssen des Geräts.
                        //                deviceService.UpdateDevice(newFunction, true);
                        //            }
                        //            if (newFunction.FunctionCategory != FunctionCategory.Blackbox)
                        //            {
                        //                // 1. Dem Kasten die Identität des bestehenden Geräts übergeben
                        //                newFunction.Name = parentFunction.Name;
                        //                newFunction.VisibleName = string.Empty;

                        //                nameService.AdjustFullName(newFunction);

                        //                // 2. DAS GEHEIMNIS: Wir überlagern das gesamte Gerät!
                        //                // Durch "UpdateDevice" mit dem Parameter "true" (oder nachfolgendem Consolidate)
                        //                // nimmt sich EPLAN den Kasten und verschmilzt ALLE im Makro enthaltenen 
                        //                // Unterfunktionen automatisch mit den freien, unplatzierten Anschlüssen des Geräts.
                        //                deviceService.UpdateDevice(newFunction, true);
                        //            }
                        //        }

                        //    }
                        //}
                    }



                    // --- JETZT DIREKT KORREKT AKTUALISIEREN ---
                    Eplan.EplApi.HEServices.Generate generateService = new Eplan.EplApi.HEServices.Generate();
                    generateService.Connections(targetPage.Project);

                    Eplan.EplApi.ApplicationFramework.CommandLineInterpreter interpreter = new Eplan.EplApi.ApplicationFramework.CommandLineInterpreter();
                    interpreter.Execute("GedRedraw");
                }
            }
                
        }
    }
}

//Identität und BMK der parentFunction übertragen
//    if (parentFunction != null && newPlacements != null)
//    {
//        // 1. Alle aktuell unplatzierten Anschlüsse von WC1 sammeln
//        List<Function> unplacedSubFunctions = new List<Function>();
//        foreach (Function subFunc in parentFunction.SubFunctions)
//        {
//            if (!subFunc.IsPlaced)
//            {
//                unplacedSubFunctions.Add(subFunc);
//            }
//        }

//        int unplacedIndex = 0;
//        NameService nameService = new NameService(targetPage);
//        DeviceService deviceService = new DeviceService();

//        foreach (StorableObject newPlacement in newPlacements)
//        {
//            if (newPlacement is Function newFunction)
//            {
//                newFunction.LockObject();

//                // FALL A: Es handelt sich um eine Funktion mit logischen Anschlüssen (z.B. Geräteanschluss)
//                if (newFunction is FunctionConnectionPoint connPoint)
//                {
//                    // Identifizierenden Namen der Hauptfunktion übergeben (Kopplung)
//                    connPoint.Name = parentFunction.Name;

//                    // Mit der unplatzierten Funktion (L1.3, L2.3 etc.) aus der Datenbank verschmelzen
//                    if (unplacedIndex < unplacedSubFunctions.Count)
//                    {
//                        Function targetUnplaced = unplacedSubFunctions[unplacedIndex];

//                        // Korrekte Zuweisung der Anschlussbezeichnung auf der Anschluss-Klasse
//                        connPoint.ConnectionPointDesignation = targetUnplaced.ConnectionPointDesignation;
//                        unplacedIndex++;
//                    }

//                    // Sichtbares BMK beim Anschluss LEEREN, damit die Grafik kurz bleibt
//                    connPoint.VisibleName = string.Empty;
//                }
//                // FALL B: Es handelt sich um den Gerätekasten oder andere Funktionen ohne Anschlüsse
//                else
//                {
//                    newFunction.Name = parentFunction.Name;

//                    // Gerätekasten-Anzeige wie von Ihnen gewünscht steuern
//                    newFunction.VisibleName = parentFunction.Name;
//                }

//                // 2. Visuelle Darstellung (Kürzung/Verschachtelung) für beide Typen anwenden
//                nameService.AdjustFullName(newFunction);

//                // 3. Logik in der EPLAN-Datenbank verschmelzen und aktivieren
//                deviceService.UpdateDevice(newFunction, false);
//            }
//        }
//    }

//    ////Identität und BMK der parentFunction übertragen
//    //if (parentFunction != null && newPlacements != null)
//    //{
//    //    // 1. NameService instanziieren für die visuelle Struktur-Kürzung
//    //    NameService nameService = new NameService(targetPage);

//    //    foreach (StorableObject newPlacement in newPlacements)
//    //    {
//    //        if (newPlacement is Function newFunction)
//    //        {
//    //            // Das Objekt vor dem Schreibzugriff sperren
//    //            newFunction.LockObject();

//    //            // 2. Den logischen Namen der Hauptfunktion übergeben (Kopplung)
//    //            newFunction.Name = parentFunction.Name;

//    //            // 3. Das sichtbare BMK bewusst leeren. 
//    //            // Dadurch ermittelt EPLAN das BMK dynamisch über die Seite/Strukturkästen.
//    //            newFunction.VisibleName = parentFunction.Name;//string.Empty;

//    //            // 4. Online-Nummerierung erzwingen, falls der Zähler schon vergeben ist
//    //            // Hierzu nutzen wir den NameService, um das BMK projektweit eindeutig zu machen.
//    //            nameService.AdjustFullName(newFunction);

//    //            // 5. Vererbung und Logik final aktualisieren
//    //            // Statt dem nicht existierenden "Parse" nutzen wir die offizielle API-Methode,
//    //            // um das Betriebsmittel mit den Artikellogiken abzugleichen.
//    //            DeviceService deviceService = new DeviceService();
//    //            deviceService.UpdateDevice(newFunction, false);
//    //        }
//    //    }
//    //}

//using Eplan.EplApi.Base;
//using Eplan.EplApi.DataModel;
//using Eplan.EplApi.DataModel.Graphics;
//using Eplan.EplApi.DataModel.MasterData;

//using Eplan.EplApi.HEServices;
//using System.Collections.Generic;
//using System.IO;

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

//        // 2. Alle freien (unplatzierten) Phasenschienen-Artikel im PROJEKT ermitteln
//        DMObjectsFinder finder = new DMObjectsFinder(targetPage.Project);

//        // Wir holen alle im Projekt eingebuchten Artikelreferenzen
//        ArticleReference[] projectArticles = finder.GetArticleReferences(null);
//        List<ArticleReference> freePhasenschienen = new List<ArticleReference>();

//        string targetPartNumber = "200011637"; // Ihre Teilenummer aus dem Screenshot

//        foreach (ArticleReference artRef in projectArticles)
//        {
//            // Entspricht der Artikel der gesuchten Phasenschiene?
//            if (artRef.PartNr.Equals(targetPartNumber, System.StringComparison.OrdinalIgnoreCase))
//            {
//                // CRITICAL: Prüfen, ob der Artikel bereits im Schaltplan/3D platziert wurde
//                // BelongsToArticlePlacement ist false, wenn das Gerät unplatziert im Baum liegt
//                if (!artRef.BelongsToArticlePlacement)
//                {
//                    freePhasenschienen.Add(artRef);
//                }
//            }
//        }

//        // 3. Platzierung durchführen (maximal so viele, wie freie Plätze ODER Anker da sind)
//        int placementsToMake = System.Math.Min(targetLocations.Count, freePhasenschienen.Count);

//        if (placementsToMake == 0)
//        {
//            new BaseException($"Keine freien (unplatzierten) Artikel für Teilenummer '{targetPartNumber}' im Projekt gefunden.", MessageLevel.Warning);
//            return;
//        }

//        Insert insertService = new Insert();

//        for (int i = 0; i < placementsToMake; i++)
//        {
//            PointD currentAnchor = targetLocations[i];
//            ArticleReference currentArtRef = freePhasenschienen[i];
//            Article articleData = currentArtRef.Article;

//            if (articleData != null)
//            {
//                // Pfad aus den geladenen Artikelstammdaten lesen
//                string articleMacroPath = articleData.Properties.ARTICLE_GROUPSYMBOLMACRO;

//                if (!string.IsNullOrEmpty(articleMacroPath))
//                {
//                    articleMacroPath = PathMap.SubstitutePath(articleMacroPath);

//                    if (File.Exists(articleMacroPath))
//                    {
//                        SymbolMacro macro = new SymbolMacro();
//                        macro.Open(articleMacroPath);

//                        // Das Makro an der Position des i-ten Platzhalters einfügen
//                        insertService.SymbolMacro(
//                            macro,
//                            0, // Variante A
//                            targetPage,
//                            currentAnchor,
//                            Insert.MoveKind.Absolute,
//                            SymbolMacro.Enums.NumerationMode.None
//                        );

//                        // HINWEIS: Um das Gerät im Projektbaum von "unplatziert" auf "platziert" umzuschalten,
//                        // müsste die neu erstellte Funktion im Schaltplan mit dieser ArticleReference verknüpft werden.
//                    }
//                }
//            }
//        }
//    }
//}

//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using Eplan.EplApi.DataModel;
//using Eplan.EplApi.DataModel.Graphics;
//using Eplan.EplApi.HEServices;
//using Eplan.EplApi.Base;
//using Eplan.EplApi.DataModel.MasterData;

//namespace Rosler.EplAddin.InsertSymbolMacroByPlaceholder
//{
//    public class PlaceMacroAtPlaceholder
//    {
//        // HINWEIS: macroPath wurde aus den Parametern entfernt, da er intern ermittelt wird
//        public void PlaceMacroPlaceholder(Page targetPage, string placeholderName)
//        {
//            Placement[] allPlacements = targetPage.AllPlacements;
//            PointD? insertionPoint = null;
//            string dynamicMacroPath = string.Empty;

//            // 1. Platzhalter suchen und Eigenschaften auslesen
//            foreach (Placement placement in allPlacements)
//            {
//                if (placement is PlaceHolder placeHolder)
//                {
//                    if (placeHolder.Name.Equals(placeholderName, System.StringComparison.OrdinalIgnoreCase))
//                    {
//                        insertionPoint = placeHolder.Location;

//                        // Hier wird der Pfad aus der Beschreibung des Platzhalters gelesen
//                        dynamicMacroPath = placeHolder.Properties.PLACEHOLDER_DESCRIPTION;
//                        break;
//                    }
//                }
//            }

//            // 2. Makro platzieren, wenn der Platzhalter existiert und ein Pfad hinterlegt ist
//            if (insertionPoint != null && !string.IsNullOrEmpty(dynamicMacroPath))
//            {
//                try
//                {
//                    Insert insertService = new Insert();

//                    // Symbolmakro-Objekt erstellen und Datei laden
//                    SymbolMacro macro = new SymbolMacro();
//                    macro.Open(dynamicMacroPath);

//                    // Aufruf des EPLAN-Einfügeservices
//                    insertService.SymbolMacro(
//                        macro,
//                        0, // Variante A
//                        targetPage,
//                        insertionPoint.Value,
//                        Insert.MoveKind.Absolute,
//                        SymbolMacro.Enums.NumerationMode.None
//                    );
//                }
//                catch (System.Exception ex)
//                {
//                    // Fehler abfangen, falls der Pfad in der Beschreibung ungültig ist (z.B. Datei existiert nicht)
//                    System.Diagnostics.Debug.WriteLine($"Fehler beim Laden des Makros aus der Beschreibung: {ex.Message}");
//                }
//            }
//            else if (insertionPoint != null && string.IsNullOrEmpty(dynamicMacroPath))
//            {
//                // Der Platzhalter existiert, aber das Beschreibungsfeld ist leer
//                System.Diagnostics.Debug.WriteLine($"Der Platzhalter '{placeholderName}' auf Seite '{targetPage.Name}' besitzt keinen Makropfad in der Beschreibung.");
//            }
//        }
//    }
//}
