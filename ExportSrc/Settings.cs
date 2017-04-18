using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;
using CodeFluent.Runtime.Utilities;

namespace ExportSrc
{
    public class Settings : Serializable<Settings>
    {
        public Settings()
        {
            Filters = new List<Filter>();
            ExcludedProjects = new List<Project>();
            Replacements = new List<ReplacementItem>();
        }

        [XmlAttribute]
        public bool OverrideExistingFile { get; set; }
        [XmlAttribute]
        public bool UnprotectFile { get; set; }
        [XmlAttribute]
        public bool ExcludeGeneratedFiles { get; set; }
        [XmlAttribute]
        public bool KeepSymbolicLinks { get; set; }
        [XmlAttribute]
        public bool ReplaceLinkFiles { get; set; }
        public bool? OutputReadOnly { get; set; }
        [XmlAttribute]
        public bool RemoveTfsBinding { get; set; }
        [XmlAttribute]
        public bool ComputeHash { get; set; }

        [XmlAttribute]
        public bool ConvertRelativeHintPathsToAbsolute { get; set; }

        public List<Filter> Filters { get; set; }
        public List<Project> ExcludedProjects { get; set; }


        [XmlElement("Replace")]
        public List<ReplacementItem> Replacements { get; set; }

        public static Settings GetDefault()
        {
            Settings result = new Settings();
            result.RemoveTfsBinding = true;
            result.UnprotectFile = true;
            result.OutputReadOnly = false; // Remove readonly attribute
            result.OverrideExistingFile = true;
            result.ExcludeGeneratedFiles = false;
            result.ComputeHash = true;
            result.KeepSymbolicLinks = true;
            result.ReplaceLinkFiles = false;
            result.ConvertRelativeHintPathsToAbsolute = true;

            // Files

            // Nuget packages
            result.Filters.Add(new Filter(@"^(.*\\|)packages\\.*", FilterType.Include, applyToFileName: false, applyToPath: true, applyToDirectory: false, applyToFile: false) { ExpressionType = FilterExpressionType.Regex });

            var excludedFiles = new[]
            {
                "*.cache",
                "_cf_md.config",
                "*.build.xml",
                "*.pdb",
                "*.ilk",
                "*.ncb",
                "*.srb",
                "*.obj",
                "*.exe",
                "*.dll",
                "*.ocx",
                "*.suo",
                "*.bak",
                "*.tmp",
                "*.com",
                "*.swp",
                "*.so",
                "*.o",
                "*.DS_Store*",
                "*thumbs.db*",
                "Desktop.ini",
                "swum-cache.txt",
                "*.class",
                "*.Bindings",
                "*.*log",
                "*.temp",
                "*.tmp",
                "*.orig",
                "*.user",
                "*.vspscc",
                "*.vssscc",
                "*.vshost.*",
                "*.CodeAnalysisLog.xml",
                "*.lastcodeanalysissucceeded",
                ".classpath",
                ".loadpath",
                "*.launch",
                ".buildpath",
                "*.sln.docstates",
                "*_i.c",
                "*_p.c",
                "*.ilk",
                "*.meta",
                "*.pch",
                "*.pgc",
                "*.pgd",
                "*.rsp",
                "*.sbr",
                "*.tlb",
                "*.tli",
                "*.tlh",
                "*.tmp_proj",
                "*.pidb",
                "*.scc",
                "*.psess",
                "*.vsp",
                "*.vspx",
                "*.dotCover",
                "*~",
                "~$*",
                "*.dbmdl",
                "UpgradeLog*.XML",
                "UpgradeLog*.htm",
            };

            foreach (var excludedFile in excludedFiles)
            {
                result.Filters.Add(new Filter(excludedFile, FilterType.Exclude, applyToFileName: true, applyToPath: false, applyToDirectory: false, applyToFile: true));
            }


            // Folders
            result.Filters.Add(new Filter("OBJ", FilterType.Exclude, applyToFileName: true, applyToPath: false, applyToDirectory: true, applyToFile: false));
            result.Filters.Add(new Filter("Debug", FilterType.Exclude, applyToFileName: true, applyToPath: false, applyToDirectory: true, applyToFile: false));
            result.Filters.Add(new Filter("Release", FilterType.Exclude, applyToFileName: true, applyToPath: false, applyToDirectory: true, applyToFile: false));
            result.Filters.Add(new Filter("BIN", FilterType.Exclude, applyToFileName: true, applyToPath: false, applyToDirectory: true, applyToFile: false));
            result.Filters.Add(new Filter("IPCH", FilterType.Exclude, applyToFileName: true, applyToPath: false, applyToDirectory: true, applyToFile: false));
            result.Filters.Add(new Filter("$tf", FilterType.Exclude, applyToFileName: true, applyToPath: false, applyToDirectory: true, applyToFile: false));
            result.Filters.Add(new Filter("publish", FilterType.Exclude, applyToFileName: true, applyToPath: false, applyToDirectory: true, applyToFile: false));
            result.Filters.Add(new Filter("$RECYCLE.BIN", FilterType.Exclude, applyToFileName: true, applyToPath: false, applyToDirectory: true, applyToFile: false));
            result.Filters.Add(new Filter("_UpgradeReport_Files", FilterType.Exclude, applyToFileName: true, applyToPath: false, applyToDirectory: true, applyToFile: false));
            result.Filters.Add(new Filter(".DS_Store", FilterType.Exclude, applyToFileName: true, applyToPath: false, applyToDirectory: true, applyToFile: false));

            // Both
            result.Filters.Add(new Filter("*resharper*", FilterType.Exclude, applyToFileName: true, applyToPath: false, applyToDirectory: true, applyToFile: true));
            result.Filters.Add(new Filter("_TeamCity*", FilterType.Exclude, applyToFileName: true, applyToPath: false, applyToDirectory: true, applyToFile: true));
            //result.Filters.Add(new Filter(".*", FilterType.Exclude, applyToFileName: true, applyToPath: false, applyToDirectory: true, applyToFile: true));

            return result;
        }

        public string Trace()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("Remove Tfs Binding: " + RemoveTfsBinding);
            sb.AppendLine("Compute hash: " + ComputeHash);
            sb.AppendLine("Override Existing Files: " + OverrideExistingFile);
            sb.AppendLine("Unprotect Files: " + UnprotectFile);
            sb.AppendLine("Output Files Read Only: " + (OutputReadOnly.HasValue ? OutputReadOnly.Value.ToString() : "Do not change"));
            sb.AppendLine("Exclude Generated Files: " + ExcludeGeneratedFiles);
            sb.AppendLine("Keep Symbolic Links: " + KeepSymbolicLinks);
            sb.AppendLine("Replace Link Files: " + ReplaceLinkFiles);

            if (Filters != null)
            {
                foreach (Filter filter in Filters)
                {
                    if (filter.FilterType == FilterType.Exclude)
                        sb.AppendLine(filter.ToString());
                }

                foreach (Filter filter in Filters)
                {
                    if (filter.FilterType == FilterType.Include)
                        sb.AppendLine(filter.ToString());
                }
            }

            return sb.ToString();
        }
    }
}