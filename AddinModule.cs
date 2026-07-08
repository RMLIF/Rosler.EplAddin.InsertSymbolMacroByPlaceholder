using Eplan.EplApi.ApplicationFramework;
using Rosler.EplAddin.InsertSymbolMacroByPlaceholder.Infrastructure;

namespace Rosler.EplAddin.InsertSymbolMacroByPlaceholder
{
    public class AddinModule : IEplAddIn
    {
        public bool OnInit()
        {
            // ✅ Zentrale Logging-Konfiguration

            GlobalLogger.EnableDebugOutput = true;   // Visual Studio Output
            GlobalLogger.EnableFileLogging = false;  // später auf true setzen wenn nötig
            GlobalLogger.ShowInfoMessages = false;   // verhindert Popup-Spam

            GlobalLogger.Info("Add-In wurde initialisiert");

            return true;
        }

        public bool OnInitGui()
        {
            return true;
        }

        public bool OnRegister(ref bool bLoadOnStart)
        {
            bLoadOnStart = true;

            GlobalLogger.Info("Add-In wurde registriert");

            return true;
        }

        public bool OnUnregister()
        {
            GlobalLogger.Info("Add-In wurde de-registriert");
            return true;
        }

        public bool OnExit()
        {
            GlobalLogger.Info("Add-In wird beendet");
            return true;
        }
    }
}