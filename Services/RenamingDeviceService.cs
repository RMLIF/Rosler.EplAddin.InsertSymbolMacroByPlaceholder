using Eplan.EplApi.Base;
using Eplan.EplApi.Base.Enums;
using Eplan.EplApi.DataModel;
using Eplan.EplApi.HEServices;
using Rosler.EplAddin.InsertSymbolMacroByPlaceholder.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Rosler.EplAddin.InsertSymbolMacroByPlaceholder.Services
{

    /// <summary>
    /// Enthält alle Informationen, die zum Umbenennen eines Betriebsmittels
    /// in EPLAN benötigt werden.
    ///
    /// Es werden die Strukturkennzeichen sowie die BMK-Bestandteile der
    /// selektierten Funktion verwaltet.
    /// </summary>
    public class RenameDeviceData
    {
        /// <summary>
        /// Die selektierte EPLAN-Funktion bzw. Hauptfunktion des Betriebsmittels.
        /// Das Objekt stellt das zu bearbeitende Betriebsmittel dar.
        /// </summary>
        public Function Function { get; set; }

        /// <summary>
        /// Funktionskennzeichen (+).
        /// Entspricht der Eigenschaft DESIGNATION_PLANT (1100).
        /// Beispiel: "MOTOR".
        /// </summary>
        public string Plant { get; set; }

        /// <summary>
        /// Ortskennzeichen (=).
        /// Entspricht der Eigenschaft DESIGNATION_LOCATION (1200).
        /// Beispiel: "A1".
        /// </summary>
        public string Location { get; set; }

        /// <summary>
        /// Funktionale Zuordnung (&amp;).
        /// Entspricht der Eigenschaft
        /// DESIGNATION_FUNCTIONALASSIGNMENT (1300).
        /// Beispiel: "SAFETY".
        /// </summary>
        public string FunctionalAssignment { get; set; }

        /// <summary>
        /// BMK-Kennbuchstabe.
        /// Entspricht der Eigenschaft FUNC_CODE (20013).
        /// Beispiel: "K".
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// BMK-Zähler.
        /// Entspricht der Eigenschaft FUNC_COUNTER (20014).
        /// Beispiel: "100".
        /// </summary>
        public string Counter { get; set; }
    }

    internal class RenamingDeviceService
    {

        public bool RenameSelectedDevice(RenameDeviceData data)
        {
            if (data == null || data.Function == null)
                return false;

            RenameFunction(
                data.Function,
                data.Plant,
                data.Location,
                data.FunctionalAssignment,
                data.Code,
                data.Counter);

            return true;
        }



        private void RenameFunction(
            Function function,
            string plant,
            string location,
            string functionalAssignment,
            string code,
            string counter)
        {
            if (function == null)
                throw new ArgumentNullException(nameof(function));

            try
            {
                //if (!function.IsDeviceLocked(false))
                //{
                //    function.LockDevice(false);
                //}

                NameService nameService = new NameService();

                FunctionBasePropertyList newName =
                    new FunctionBasePropertyList();

                // Funktionskennzeichen (+)
                if (!string.IsNullOrWhiteSpace(plant))
                {
                    newName.DESIGNATION_PLANT = plant;
                }

                // Ortskennzeichen (=)
                if (!string.IsNullOrWhiteSpace(location))
                {
                    newName.DESIGNATION_LOCATION = location;
                }

                // Funktionale Zuordnung (&)
                if (!string.IsNullOrWhiteSpace(functionalAssignment))
                {
                    newName.DESIGNATION_FUNCTIONALASSIGNMENT =
                        functionalAssignment;
                }

                // BMK Kennbuchstabe
                if (!string.IsNullOrWhiteSpace(code))
                {
                    newName.FUNC_CODE = code;
                }

                // BMK Zähler
                if (!string.IsNullOrWhiteSpace(counter))
                {
                    newName.FUNC_COUNTER = counter;
                }

                GlobalLogger.Info("===== VOR RENAME DEVICE =====");

                foreach (var f in function.AllSubFunctions)
                {
                    GlobalLogger.Info(
                        $"SUB={f.Name} PARENT={f.ParentFunction?.Name}");
                }

                nameService.RenameDevice(
                    function,
                    newName,
                    true,   // CDPs mit umbenennen
                    true);  // beschreibende Eigenschaften beibehalten

                GlobalLogger.Info("===== NACH RENAME DEVICE =====");

                foreach (var f in function.AllSubFunctions)
                {
                    GlobalLogger.Info(
                        $"SUB={f.Name} PARENT={f.ParentFunction?.Name}");
                }


            }
            catch (Exception ex)
            {
                throw new Exception(
                    $"Fehler beim Umbenennen von '{function.Name}'.",
                    ex);
            }
        }

        public bool RenameBoxedDeviceFromParent(
    BoxedDevice boxedDevice,
    Function parentFunction)
        {
            if (boxedDevice == null || parentFunction == null)
                return false;

           


            GlobalLogger.Info("===== BOXEDDEVICE DEBUG =====");

            GlobalLogger.Info(
                $"BOX TYPE={boxedDevice.GetType().FullName}");

            GlobalLogger.Info(
                $"BOX ID={boxedDevice.ObjectIdentifier}");

            GlobalLogger.Info(
                $"BOX NAME={boxedDevice.Name}");

            GlobalLogger.Info(
                $"BOX PAGE={boxedDevice.Page?.Name}");


            GlobalLogger.Info(
                $"BOX CATEGORY={boxedDevice.FunctionCategory}");

            GlobalLogger.Info(
                $"BOX NAME={boxedDevice.Name}");

            GlobalLogger.Info(
                $"BOX ID={boxedDevice.ObjectIdentifier}");


            

            foreach (var f in boxedDevice.FunctionsInside)
            {
                GlobalLogger.Info(
                    $"FUNC={f.Name}");

                GlobalLogger.Info(
                    $"CAT={f.FunctionCategory}");

                GlobalLogger.Info(
                    $"MAIN={f.IsMainFunction}");
            }

           

            var blackboxes = new List<BoxedDevice>();

            if (boxedDevice.FunctionCategory == FunctionCategory.Blackbox)
            {
                blackboxes.Add(boxedDevice);
            }

            GlobalLogger.Info(
                $"BLACKBOX COUNT={blackboxes.Count}");


            GlobalLogger.Info(
                $"BLACKBOX COUNT={blackboxes.Count}");


            foreach (var f in blackboxes)
            {
                GlobalLogger.Info(
                    $"BLACKBOX ID={f.ObjectIdentifier} " +
                    $"NAME={f.Name} " +
                    $"MAIN={f.IsMainFunction}");

                GlobalLogger.Info(
                    $"ARTICLE_REF_COUNT={f.ArticleReferences.Length}");

                foreach (var ar in f.ArticleReferences)
                {
                    GlobalLogger.Info(
                        $"BLACKBOX ARTICLE={ar.PartNr} VARIANT={ar.VariantNr}");
                }


                GlobalLogger.Info(
                        $"BLACKBOX ID={f.ObjectIdentifier}");

                GlobalLogger.Info(
                    $"NAME={f.Name}");

                GlobalLogger.Info(
                    $"CAT={f.FunctionCategory}");

                GlobalLogger.Info(
                    $"MAIN={f.IsMainFunction}");

            }


            


            GlobalLogger.Info(
                $"BOXEDDEVICE NAME={boxedDevice.Name}");

            foreach (var f in boxedDevice.FunctionsInside)
            {
                GlobalLogger.Info(
                    $"CANDIDATE={f.Name}");
            }


            


            Function renameFunction = null;

            if (boxedDevice.FunctionCategory == FunctionCategory.Blackbox)
            {
                renameFunction = boxedDevice;
            }


            GlobalLogger.Info(
                $"RENAME FUNCTION={renameFunction?.Name}");


            if (renameFunction == null)
                return false;

            RenameDeviceData data = new RenameDeviceData
            {
                Function = renameFunction,
                Plant = parentFunction.Properties.DESIGNATION_PLANT,
                Location = parentFunction.Properties.DESIGNATION_LOCATION,
                FunctionalAssignment =
                    parentFunction.Properties.DESIGNATION_FUNCTIONALASSIGNMENT,
                Code = parentFunction.Properties.FUNC_CODE,
                Counter = parentFunction.Properties.FUNC_COUNTER
            };


            GlobalLogger.Info(
                $"ParentFunction={parentFunction.Name}");

            GlobalLogger.Info(
                $"RenameFunction={renameFunction?.Name}");

            GlobalLogger.Info(
                $"PARENT ARTICLE_REF_COUNT={parentFunction.ArticleReferences.Length}");

            foreach (var ar in parentFunction.ArticleReferences)
            {
                GlobalLogger.Info(
                    $"PARENT ARTICLE={ar.PartNr} VARIANT={ar.VariantNr}");
            }




            bool result = RenameSelectedDevice(data);

            GlobalLogger.Info(
                $"RENAME RESULT: {result}");

            if (result)
            {
                try
                {
                    DeviceService ds = new DeviceService();

                    GlobalLogger.Info(
                        $"UPDATEDEVICE AFTER RENAME BEGIN");
                    ds.UpdateDevice(renameFunction);
                    ds.UpdateDevice(parentFunction);

                   


                    GlobalLogger.Info(
                        $"UPDATEDEVICE AFTER RENAME END");
                }
                catch (Exception ex)
                {
                    GlobalLogger.Info(
                        $"UPDATEDEVICE AFTER RENAME ERROR={ex.Message}");
                }
            }


            GlobalLogger.Info(
                $"BOX ID={boxedDevice.ObjectIdentifier}");

            GlobalLogger.Info(
                $"BOX NAME={boxedDevice.Name}");

            GlobalLogger.Info(
                $"BOX MAIN={boxedDevice.IsMainFunction}");

            GlobalLogger.Info(
                $"BOX PARENT={boxedDevice.ParentFunction?.Name}");

            foreach (var f in boxedDevice.FunctionsInside)
            {
                GlobalLogger.Info(
                    $"FUNC={f.Name}");

                GlobalLogger.Info(
                    $"FUNC ID={f.ObjectIdentifier}");

                GlobalLogger.Info(
                    $"FUNC PARENT={f.ParentFunction?.Name}");

                GlobalLogger.Info(
                    $"FUNC MAIN={f.IsMainFunction}");
            }



            GlobalLogger.Info(
                $"PARENT ID={parentFunction.ObjectIdentifier}");

            GlobalLogger.Info(
                $"RENAME ID={renameFunction.ObjectIdentifier}");

            GlobalLogger.Info(
                $"PARENT == RENAME = {parentFunction.ObjectIdentifier == renameFunction.ObjectIdentifier}");



            return result;


        }

    }
}