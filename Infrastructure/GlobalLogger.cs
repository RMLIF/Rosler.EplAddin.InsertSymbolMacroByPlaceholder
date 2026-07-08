using System;
using System.Diagnostics;
using System.IO;
using Eplan.EplApi.Base;
using Eplan.EplApi.Base.Enums;

namespace Rosler.EplAddin.InsertSymbolMacroByPlaceholder.Infrastructure
{
    public static class GlobalLogger
    {
        private static readonly object _lock = new object();

        public static bool EnableDebugOutput = true;
        public static bool EnableFileLogging = false;
        public static bool ShowInfoMessages = false;

        public static string LogFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EplanAddin",
            "log.txt");

        public static void Info(string message)
        {
            Write("[INFO] " + message, MessageLevel.Message);
        }

        public static void Warn(string message)
        {
            Write("[WARN] " + message, MessageLevel.Warning);
        }

        public static void Error(string message)
        {
            Write("[ERROR] " + message, MessageLevel.Error);

            // optional (empfohlen)
            throw new BaseException(message, MessageLevel.Error);
        }

        public static void DebugLog(string message)
        {
            if (EnableDebugOutput)
            {
                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                              + " [DEBUG] " + message;
                Debug.WriteLine(line);
            }
        }

        private static void Write(string message, MessageLevel level)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string final = timestamp + " " + message;

            // Visual Studio Output
            if (EnableDebugOutput)
            {
                Debug.WriteLine(final);
            }

            // EPLAN Meldung
            if (level != MessageLevel.Message || ShowInfoMessages)
            {
                new BaseException(final, level);
            }

            // Datei
            if (EnableFileLogging)
            {
                lock (_lock)
                {
                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(LogFilePath));
                        File.AppendAllText(LogFilePath, final + Environment.NewLine);
                    }
                    catch { }
                }
            }
        }
    }
}

//using System;
//using System.Diagnostics;
//using System.IO;
//using Eplan.EplApi.Base;

//namespace Rosler.EplAddin.InsertSymbolMacroByPlaceholder.Infrastructure
//{
//    public static class GlobalLogger
//    {
//        private static readonly object _lock = new object();

//        // Steuerung
//        public static bool EnableDebugOutput = true;
//        public static bool EnableFileLogging = false;
//        public static bool ShowInfoMessages = false;

//        public static string LogFilePath =
//            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "EplanAddinLog.txt");

//        public static void Info(string message)
//        {
//            Write("[INFO] " + message, MessageLevel.Message);
//        }

//        public static void Warn(string message)
//        {
//            Write("[WARN] " + message, MessageLevel.Warning);
//        }

//        public static void Error(string message)
//        {
//            Write("[ERROR] " + message, MessageLevel.Error);
//        }

//        private static void Write(string message, MessageLevel level)
//        {
//            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
//            string final = timestamp + " " + message;

//            // Debug (Visual Studio Output)
//            if (EnableDebugOutput)
//            {
//                Debug.WriteLine(final);
//            }

//            // EPLAN Meldungen
//            if (level != MessageLevel.Message || ShowInfoMessages)
//            {
//                new BaseException(final, level);
//            }

//            // Datei
//            if (EnableFileLogging)
//            {
//                lock (_lock)
//                {
//                    try
//                    {
//                        File.AppendAllText(LogFilePath, final + Environment.NewLine);
//                    }
//                    catch { }
//                }
//            }
//        }
//    }
//}
