using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace ExportSrc.MSBuild
{
    public class ExportSrcTask : Task
    {
        public string Source { get; set; }
        public string ConfigurationFile { get; set; }

        [Required]
        public string Destination { get; set; }

        public override bool Execute()
        {
            if (string.IsNullOrEmpty(Source))
            {
                Source = Path.GetDirectoryName(BuildEngine2.ProjectFileOfTaskNode);
            }

            Log.LogMessage(MessageImportance.Normal, "Exporting source from {0} to {1}", Source, Destination);
            Settings settings;
            if (!string.IsNullOrEmpty(ConfigurationFile))
            {
                settings = Settings.ReadSettings(ConfigurationFile);
            }
            else
            {
                settings = Settings.GetDefault();
            }

            Exporter exporter = new Exporter(Source, settings);
            exporter.Export(Destination);
            return true;
        }
    }
}