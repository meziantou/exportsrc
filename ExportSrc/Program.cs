using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace ExportSrc
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        private static void Main(string[] args)
        {
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
            if (Debugger.IsAttached)
            {
                SafeMain(args);
            }
            else
            {
                try
                {
                    SafeMain(args);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]  
        private static void SafeMain(string[] args)
        {
            if (args.Length == 0)
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
                return;
            }

            CodeFluent.Runtime.Diagnostics.ConsoleListener.EnsureConsole();

            if (args.Any(s => s == "/?" || s == "-?"))
            {
                PrintUsage();
                return;
            }

            if (Trace.Listeners.Count == 0 || (Trace.Listeners.Count == 1 && Trace.Listeners[0].GetType() == typeof(DefaultTraceListener)))
            {
                Trace.Listeners.Add(new ConsoleTraceListener());
            }

            Settings settings;
            if (args.Length > 2)
            {
                settings = Settings.Deserialize(args[2]);
            }
            else
            {
                settings = Settings.GetDefault();
            }

            //XmlSerializer serializer = new XmlSerializer(typeof(Settings));
            //using (Stream s = File.OpenWrite("settings.xml"))
            //    serializer.Serialize(s, settings);

            Exporter exporter = new Exporter(args[0], settings);
            ExportResult result = exporter.Export(args[1]);

            Logger.Current.Log(LogCategory.Summary, "Directories: " + result.Directories);
            Logger.Current.Log(LogCategory.Summary, "Files:       " + result.Files);
        }

        private static void PrintUsage()
        {
            Console.WriteLine("ExportSrc.exe <source> <destination> [config]");
            Console.WriteLine("\tsource              Source directory");
            Console.WriteLine("\tdestination         Target ");
            Console.WriteLine("\tconfig              Configuration file");
        }

        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            string baseResourceName = Assembly.GetExecutingAssembly().GetName().Name + ".External." + new AssemblyName(args.Name).Name;
            byte[] assemblyData = null;
            byte[] symbolsData = null;
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(baseResourceName + ".dll"))
            {
                if (stream == null)
                    return null;
                assemblyData = new Byte[stream.Length];
                stream.Read(assemblyData, 0, assemblyData.Length);
            }
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(baseResourceName + ".pdb"))
            {
                if (stream != null)
                {
                    symbolsData = new Byte[stream.Length];
                    stream.Read(symbolsData, 0, symbolsData.Length);
                }
            }
            return Assembly.Load(assemblyData, symbolsData);
        }
    }
}