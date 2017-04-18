using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using CodeFluent.Runtime.Utilities;

namespace ExportSrc
{
    public class Exporter
    {
        private static readonly Filter DesignerFilter = new Filter("*.designer.*", FilterType.Exclude);
        private static readonly Filter GeneratedFilter = new Filter("*.g.*", FilterType.Exclude);
        private readonly string _srcPath;
        private int _errorCount;

        public Exporter(string srcPath)
            : this(srcPath, Settings.GetDefault())
        {
        }

        public Exporter(string srcPath, Settings settings)
        {
            if (srcPath == null)
                throw new ArgumentNullException("srcPath");

            if (settings == null)
                throw new ArgumentNullException("settings");

            Settings = settings;

            if (Directory.Exists(srcPath))
            {
                _srcPath = srcPath;
            }
            else
            {
                _srcPath = Path.GetDirectoryName(srcPath);
            }
        }

        public Settings Settings { get; set; }

        public ExportResult Export(string path)
        {
            ExportResult result = new ExportResult();

            Logger.Current.Log(LogCategory.Configuration, Settings);

            if (!Directory.Exists(path))
            {
                Logger.Current.Log(LogCategory.CreateDirectory, path);
                Directory.CreateDirectory(path);
            }

            foreach (string srcPath in GetFiles(_srcPath))
            {
                string relativePath = GetRelativePath(srcPath);
                relativePath = ApplyReplacements(relativePath);
                string dstPath = Path.Combine(path, relativePath);
                if (Directory.Exists(srcPath))
                {
                    result.Directories++;

                    if (ReparsePoint.IsSymbolicLink(srcPath))
                    {
                        if (Settings.KeepSymbolicLinks)
                        {
                            string link = ReparsePoint.GetTargetDir(new DirectoryInfo(srcPath));
                            ReparsePoint.CreateSymbolicLink(dstPath, link, SymbolicLinkType.Directory);
                            continue;
                        }
                    }

                    if (!Directory.Exists(dstPath))
                    {
                        Directory.CreateDirectory(dstPath);
                    }
                }
                else
                {
                    result.Files++;

                    if (ReparsePoint.IsSymbolicLink(srcPath))
                    {
                        if (Settings.KeepSymbolicLinks)
                        {
                            string link = ReparsePoint.GetTargetDir(new FileInfo(srcPath));
                            ReparsePoint.CreateSymbolicLink(dstPath, link, SymbolicLinkType.File);
                            continue;
                        }
                    }

                    CopyFile(srcPath, dstPath);
                }
            }

            return result;
        }

        private bool CanReplaceText
        {
            get
            {
                return Settings.Replacements != null && Settings.Replacements.Count > 0;
            }
        }

        private string ApplyReplacements(string text)
        {
            if (text == null)
                return null;

            if (!CanReplaceText)
                return text;

            foreach (var replacementItem in Settings.Replacements)
            {
                text = text.Replace(replacementItem.SearchText, replacementItem.ReplacementText);
            }

            return text;
        }

        private void CopyFile(string src, string dst)
        {
            Logger.Current.Log(LogCategory.Copy, src);

            IOUtilities.PathCreateDirectory(dst);

            if (Settings.OverrideExistingFile)
            {
                PathDelete(dst, Settings.UnprotectFile);
            }

            if (!CanReplaceText && !Settings.RemoveTfsBinding && !Settings.ConvertRelativeHintPathsToAbsolute)
            {
                CopyBinaryFile(src, dst);
            }
            else
            {
                string tempPath = null;
                string extension = Path.GetExtension(src);
                switch (ToLower(extension))
                {
                    case ".sln":
                        tempPath = Path.GetTempFileName();
                        CopySln(src, tempPath);
                        break;
                    case ".vdproj":
                        tempPath = Path.GetTempFileName();
                        CopyVdproj(src, tempPath);
                        break;
                    case ".csproj":
                    case ".vbproj":
                    case ".dbproj":
                    case ".vcxproj":
                    case ".cfxproj":
                    case ".wixproj":
                        tempPath = Path.GetTempFileName();
                        CopyCsproj(src, tempPath);
                        break;
                    case ".vcproj":
                        tempPath = Path.GetTempFileName();
                        CopyVcproj(src, tempPath);
                        break;
                    default:

                        if (CanReplaceText)
                        {
                            Perceived perceivedType = Perceived.GetPerceivedType(src);
                            if (perceivedType.PerceivedType == PerceivedType.Text)
                            {
                                CopyTextFile(src, dst);
                            }
                            else
                            {
                                CopyBinaryFile(src, dst);
                            }
                        }
                        else
                        {
                            CopyBinaryFile(src, dst);
                        }

                        break;
                }

                if (tempPath != null)
                {
                    CopyTextFile(tempPath, dst);
                    File.Delete(tempPath);
                }
            }


            if (Settings.OutputReadOnly.HasValue)
            {
                SetReadOnly(dst, Settings.OutputReadOnly.Value);
            }
        }

        private static string ToLower(string str)
        {
            if (str == null)
                return null;

            return str.ToLower();
        }

        private void CopyTextFile(string src, string dst)
        {
            IOUtilities.PathCreateDirectory(dst);

            string text = File.ReadAllText(src);
            text = ApplyReplacements(text);
            File.WriteAllText(dst, text);
        }

        private void CopyBinaryFile(string src, string dst)
        {
            IOUtilities.PathCreateDirectory(dst);
            File.Copy(src, dst, true);

            if (Settings.ComputeHash)
            {
                byte[] hashSrc;
                byte[] hashDst;

                using (HashAlgorithm hashAlgorithm = MD5.Create())
                {
                    using (Stream stream = File.OpenRead(src))
                    {
                        hashSrc = hashAlgorithm.ComputeHash(stream);
                    }

                    using (Stream stream = File.OpenRead(dst))
                    {
                        hashDst = hashAlgorithm.ComputeHash(stream);
                    }
                }

                if (!HashEqual(hashSrc, hashDst))
                {
                    if (_errorCount > 5)
                    {
                        throw new Exception(string.Format("Error while copying file \"{0}\" to \"{1}\".", src, dst));
                    }

                    _errorCount += 1;
                    Logger.Current.Log(LogCategory.Verify, "Different hash (" + _errorCount + ")");
                    CopyBinaryFile(src, dst);
                }
                else
                {
                    _errorCount = 0;
                    Logger.Current.Log(LogCategory.Verify, dst);
                }
            }
        }

        private bool HashEqual(byte[] a, byte[] b)
        {
            if (a == b)
                return true;

            if (a == null || b == null)
                return false;

            if (a.Length != b.Length)
                return false;

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                    return false;
            }

            return true;
        }

        private void CopyVcproj(string src, string dst)
        {
            if (!Settings.RemoveTfsBinding)
            {
                CopyBinaryFile(src, dst);
                return;
            }

            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(src);

                XmlElement root = doc.DocumentElement;

                root.RemoveAttribute("SccProjectName");
                root.RemoveAttribute("SccLocalPath");
                root.RemoveAttribute("SccAuxPath");
                root.RemoveAttribute("SccProvider");

                doc.Save(dst);
            }
            catch (XmlException ex)
            {
                Logger.Current.Log(LogCategory.Copy, "Invalid vcproj: " + src);
                CopyBinaryFile(src, dst);
            }
        }

        private void CopyVdproj(string src, string dst)
        {
            if (!Settings.RemoveTfsBinding)
            {
                CopyBinaryFile(src, dst);
                return;
            }

            using (StreamReader inputStream = new StreamReader(File.OpenRead(src)))
            {
                using (StreamWriter outputStream = new StreamWriter(File.OpenWrite(dst)))
                {
                    string line;
                    while ((line = inputStream.ReadLine()) != null)
                    {
                        string trimLine = line.Trim();
                        if (!trimLine.StartsWith("\"SccProjectName\"") &&
                            !trimLine.StartsWith("\"SccLocalPath\"") &&
                            !trimLine.StartsWith("\"SccAuxPath\"") &&
                            !trimLine.StartsWith("\"SccProvider\""))
                        {
                            outputStream.WriteLine(line);
                        }
                    }
                }
            }
        }

        private void CopyCsproj(string src, string dst)
        {
            if (!Settings.RemoveTfsBinding && !Settings.ConvertRelativeHintPathsToAbsolute)
            {
                CopyBinaryFile(src, dst);
                return;
            }

            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(src);

                // xmlns has no explicit name but a name s needed by XPath
                XmlNamespaceManager xmlnsmgr = new XmlNamespaceManager(doc.NameTable);
                xmlnsmgr.AddNamespace("msbuild", "http://schemas.microsoft.com/developer/msbuild/2003");
                XmlNode root = doc;

                if (Settings.RemoveTfsBinding)
                {
                    RemoveNode(xmlnsmgr, root, "//msbuild:SccProjectName");
                    RemoveNode(xmlnsmgr, root, "//msbuild:SccLocalPath");
                    RemoveNode(xmlnsmgr, root, "//msbuild:SccAuxPath");
                    RemoveNode(xmlnsmgr, root, "//msbuild:SccProvider");
                }

                if (Settings.ConvertRelativeHintPathsToAbsolute)
                {
                    XmlNodeList nodes = root.SelectNodes("//msbuild:HintPath/text()", xmlnsmgr);
                    if (nodes != null)
                    {
                        foreach (XmlNode node in nodes)
                        {
                            try
                            {
                                string path = Path.Combine(_srcPath, node.Value);
                                if (!IOUtilities.PathIsChildOrEqual(_srcPath, path))
                                {
                                    path = Path.GetFullPath(path);

                                    Environment.SpecialFolder[] folders =
                                    {
                                        Environment.SpecialFolder.AdminTools,
                                        Environment.SpecialFolder.CommonDocuments,
                                        Environment.SpecialFolder.CommonMusic,
                                        Environment.SpecialFolder.CommonOemLinks,
                                        Environment.SpecialFolder.CommonPictures,
                                        Environment.SpecialFolder.CommonProgramFiles,
                                        Environment.SpecialFolder.CommonProgramFilesX86,
                                        Environment.SpecialFolder.CommonPrograms,
                                        Environment.SpecialFolder.CommonStartMenu,
                                        Environment.SpecialFolder.CommonStartup,
                                        Environment.SpecialFolder.CommonTemplates,
                                        Environment.SpecialFolder.CommonVideos,
                                        Environment.SpecialFolder.ProgramFiles,
                                        Environment.SpecialFolder.ProgramFilesX86,
                                        Environment.SpecialFolder.Programs,
                                        Environment.SpecialFolder.System,
                                        Environment.SpecialFolder.SystemX86,
                                        Environment.SpecialFolder.Windows,
                                    };

                                    bool @break = false;
                                    foreach (var specialFolder in folders)
                                    {
                                        try
                                        {
                                            var p = Environment.GetFolderPath(specialFolder);
                                            if(string.IsNullOrEmpty(p))
                                                continue;

                                            if (IOUtilities.PathIsChildOrEqual(p, path))
                                            {
                                                @break = true;
                                                break;
                                            }
                                        }
                                        catch
                                        { }
                                    }

                                    if (@break)
                                    {
                                        node.Value = path;
                                    }
                                }
                            }
                            catch
                            { }
                        }
                    }
                }

                if (Settings.ReplaceLinkFiles)
                {
                    string srcPath = Path.GetDirectoryName(src);
                    string dstPath = Path.GetDirectoryName(dst);
                    ReplaceLinkFiles(srcPath, dstPath, xmlnsmgr, root);
                }

                doc.Save(dst);
            }
            catch (XmlException ex)
            {
                Logger.Current.Log(LogCategory.Copy, "Invalid csproj: " + src);
                CopyBinaryFile(src, dst);
            }
        }

        private void ReplaceLinkFiles(string srcPath, string dstpath, XmlNamespaceManager xmlnsmgr, XmlNode root)
        {
            XmlNodeList nodes = root.SelectNodes("/msbuild:Project/msbuild:ItemGroup/msbuild:Compile[@Include]/msbuild:Link", xmlnsmgr);
            if (nodes == null)
                return;

            foreach (XmlNode node in nodes)
            {
                XmlNode parent = node.ParentNode;
                string src = parent.Attributes["Include"].Value;
                string dst = node.InnerText;

                string copySrc = Path.Combine(srcPath, src);
                string copyDst = Path.Combine(dstpath, dst);
                CopyFile(copySrc, copyDst);

                parent.RemoveChild(node);
                parent.Attributes["Include"].Value = dst;
            }
        }

        private void RemoveNode(XmlNamespaceManager xmlnsmgr, XmlNode root, string xpath)
        {
            XmlNodeList xmlNodeList = root.SelectNodes(xpath, xmlnsmgr);
            if (xmlNodeList != null)
                foreach (XmlNode node in xmlNodeList)
                {
                    if (node.ParentNode != null)
                    {
                        node.ParentNode.RemoveChild(node);
                    }
                }
        }

        private void CopySln(string src, string dst)
        {
            List<string> excludedProjects = new List<string>();
            if (Settings.ExcludedProjects != null)
            {
                excludedProjects.AddRange(Settings.ExcludedProjects.Where(_ => _ != null).Select(_ => _.Id.ToString("B")));
            }

            using (StreamReader inputStream = new StreamReader(File.OpenRead(src)))
            {
                using (StreamWriter outputStream = new StreamWriter(File.OpenWrite(dst)))
                {
                    string line;
                    while ((line = inputStream.ReadLine()) != null)
                    {
                        bool writeLine = true;
                        if (Settings.RemoveTfsBinding)
                        {
                            if (line.Trim().StartsWith("GlobalSection(SourceCodeControl)") ||
                                line.Trim().StartsWith("GlobalSection(TeamFoundationVersionControl)"))
                            {
                                string readLine;
                                while ((readLine = inputStream.ReadLine()) != null && !readLine.Trim().StartsWith("EndGlobalSection"))
                                {
                                }

                                writeLine = false;
                            }
                        }

                        if (excludedProjects.Any(_ => line.IndexOf(_, StringComparison.OrdinalIgnoreCase) >= 0))
                        {
                            writeLine = false;
                        }

                        if (writeLine)
                        {
                            outputStream.WriteLine(line);
                        }
                    }
                }
            }
        }

        private void PathDelete(string filePath, bool unprotect)
        {
            if (filePath == null)
                throw new ArgumentNullException("filePath");

            if (!File.Exists(filePath))
                return;

            FileAttributes attributes = File.GetAttributes(filePath);
            if (((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly) && (unprotect))
            {
                File.SetAttributes(filePath, attributes & ~FileAttributes.ReadOnly);
            }
            File.Delete(filePath);
        }

        private void SetReadOnly(string filePath, bool readOnly)
        {
            if (!File.Exists(filePath))
                return;

            FileAttributes attributes = File.GetAttributes(filePath);
            if (readOnly)
                File.SetAttributes(filePath, attributes & FileAttributes.ReadOnly);
            else
                File.SetAttributes(filePath, attributes & ~FileAttributes.ReadOnly);
        }

        private bool MustExclude(string path)
        {
            string relativePath = GetRelativePath(path);
            string name = Directory.Exists(path) ? new DirectoryInfo(path).Name : Path.GetFileName(path);

            if (Settings.Filters == null)
                return false;

            foreach (Filter filter in Settings.Filters.Where(_ => _.Enabled))
            {
                if (filter.FilterType == FilterType.Exclude)
                    continue;

                if (filter.Match(path, relativePath, name))
                    return false;
            }

            foreach (Filter filter in Settings.Filters.Where(_ => _.Enabled))
            {
                if (filter.FilterType == FilterType.Include)
                    continue;

                if (filter.Match(path, relativePath, name))
                    return true;
            }

            if (Settings.ExcludeGeneratedFiles && IsGeneratedFile(path))
            {
                return true;
            }

            return false;
        }

        private bool IsGeneratedFile(string path)
        {
            if (!File.Exists(path))
                return false;

            string relativePath = GetRelativePath(path);
            string name = Path.GetFileName(path);

            if (DesignerFilter.Match(path, relativePath, name))
                return true;

            if (GeneratedFilter.Match(path, relativePath, name))
                return true;

            using (StreamReader sr = new StreamReader(path))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    if (line.IndexOf("This code was generated by a tool.", StringComparison.CurrentCultureIgnoreCase) >= 0)
                        return true;

                    if (line.IndexOf("<auto-generated", StringComparison.CurrentCultureIgnoreCase) >= 0)
                        return true;

                    if (line.IndexOf("<autogenerated", StringComparison.CurrentCultureIgnoreCase) >= 0)
                        return true;

                    if (line.IndexOf("// $ANTLR", StringComparison.CurrentCultureIgnoreCase) >= 0)
                        return true;

                    if (line.IndexOf("Ce code a \x00e9t\x00e9 g\x00e9n\x00e9r\x00e9 par un outil.", StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
            }

            return false;
        }

        private string GetRelativePath(string path)
        {
            if (path.StartsWith(_srcPath))
                return path.Substring(_srcPath.Length + 1);

            return path;
        }

        private IEnumerable<string> GetFiles(string path)
        {
            if (Directory.Exists(path))
            {
                foreach (string file in Directory.GetFiles(path))
                {
                    if (MustExclude(file))
                    {
                        Logger.Current.Log(LogCategory.Exclude, file);
                    }
                    else
                    {
                        Logger.Current.Log(LogCategory.Include, file);
                        yield return file;
                    }
                }

                foreach (string directory in Directory.GetDirectories(path))
                {
                    if (MustExclude(directory))
                    {
                        Logger.Current.Log(LogCategory.Exclude, directory);
                    }
                    else
                    {
                        Logger.Current.Log(LogCategory.Include, directory);
                        yield return directory;
                        if (Settings.KeepSymbolicLinks && ReparsePoint.IsSymbolicLink(directory))
                            continue;

                        foreach (string item in GetFiles(directory))
                        {
                            yield return item;
                        }
                    }
                }
            }
        }
    }
}