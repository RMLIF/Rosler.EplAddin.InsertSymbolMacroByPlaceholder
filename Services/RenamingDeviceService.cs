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

               

                nameService.RenameDevice(
                    function,
                    newName,
                    true,   // CDPs mit umbenennen
                    true);  // beschreibende Eigenschaften beibehalten

                


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


           

            var blackboxes = new List<BoxedDevice>();

            if (boxedDevice.FunctionCategory == FunctionCategory.Blackbox)
            {
                blackboxes.Add(boxedDevice);
            }


            Function renameFunction = null;

            if (boxedDevice.FunctionCategory == FunctionCategory.Blackbox)
            {
                renameFunction = boxedDevice;
            }


           

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



            bool result = RenameSelectedDevice(data);

            if (result)
            {
                try
                {
                    DeviceService ds = new DeviceService();

                  
                    ds.UpdateDevice(renameFunction);
                    ds.UpdateDevice(parentFunction);

                }
                catch (Exception ex)
                {
                    GlobalLogger.Warn(
                        $"UPDATEDEVICE AFTER RENAME ERROR={ex.Message}");
                }
            }


            return result;


        }

    }
}