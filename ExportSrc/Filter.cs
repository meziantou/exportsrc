using System;
using System.ComponentModel;
using System.IO;
using System.Net.Mime;
using System.Text.RegularExpressions;
using System.Xml.Serialization;

namespace ExportSrc
{
    [Serializable]
    public class Filter : IEquatable<Filter>
    {
        [XmlIgnore]
        private Regex _regex;
        private string _text;
        private bool _enabled;

        public Filter(string text, FilterType filterType, bool applyToFileName, bool applyToPath, bool applyToDirectory,
                      bool applyToFile)
        {
            Text = text;
            ApplyToPath = applyToPath;
            ApplyToFileName = applyToFileName;
            ApplyToDirectory = applyToDirectory;
            ApplyToFile = applyToFile;
            FilterType = filterType;
            Enabled = true;
        }

        public Filter(string text, FilterType filterType, bool matchDirectory, bool matchFile)
            : this(text, filterType, true, false, matchDirectory, matchFile)
        {
        }

        public Filter(string text, FilterType filterType)
            : this(text, filterType, true, true)
        {
        }

        public Filter()
            : this(null, FilterType.Exclude, true, true, true, true)
        {
        }

        [XmlText]
        public string Text
        {
            get { return _text; }
            set
            {
                if (_text == value)
                    return;

                _text = value;
                _regex = null;
            }
        }

        [XmlAttribute]
        [DefaultValue(true)]
        public bool Enabled
        {
            get
            {
                return !string.IsNullOrEmpty(Text) && _enabled;
            }
            set { _enabled = value; }
        }

        [XmlAttribute]
        [DefaultValue(typeof(FilterType), "Exclude")]
        public FilterType FilterType { get; set; }

        [XmlAttribute]
        public bool ApplyToFileName { get; set; }

        [XmlAttribute]
        public bool ApplyToPath { get; set; }

        [XmlAttribute]
        public bool ApplyToFile { get; set; }

        [XmlAttribute]
        public bool ApplyToDirectory { get; set; }

        [XmlAttribute]
        [DefaultValue(false)]
        public bool CaseSensitive { get; set; }

        [XmlAttribute]
        [DefaultValue(typeof(FilterExpressionType), "Globbing")]
        public FilterExpressionType ExpressionType { get; set; }

        #region IEquatable<Filter> Members

        /// <summary>
        ///   Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <returns>
        ///   true if the current object is equal to the <paramref name = "other" /> parameter; otherwise, false.
        /// </returns>
        /// <param name = "other">An object to compare with this object.</param>
        public bool Equals(Filter other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(other.FilterType, FilterType) && other.ApplyToFileName.Equals(ApplyToFileName) &&
                   other.ApplyToPath.Equals(ApplyToPath) && other.ApplyToFile.Equals(ApplyToFile) &&
                   other.ApplyToDirectory.Equals(ApplyToDirectory) && other.CaseSensitive.Equals(CaseSensitive) &&
                   Equals(other._text, _text);
        }

        #endregion

        /// <summary>
        ///   Determines whether the specified <see cref = "T:System.Object" /> is equal to the current <see cref = "T:System.Object" />.
        /// </summary>
        /// <returns>
        ///   true if the specified <see cref = "T:System.Object" /> is equal to the current <see cref = "T:System.Object" />; otherwise, false.
        /// </returns>
        /// <param name = "obj">The <see cref = "T:System.Object" /> to compare with the current <see cref = "T:System.Object" />. </param>
        /// <exception cref = "T:System.NullReferenceException">The <paramref name = "obj" /> parameter is null.</exception>
        /// <filterpriority>2</filterpriority>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof(Filter)) return false;
            return Equals((Filter)obj);
        }

        /// <summary>
        ///   Serves as a hash function for a particular type.
        /// </summary>
        /// <returns>
        ///   A hash code for the current <see cref = "T:System.Object" />.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public override int GetHashCode()
        {
            unchecked
            {
                int result = FilterType.GetHashCode();
                result = (result * 397) ^ ApplyToFileName.GetHashCode();
                result = (result * 397) ^ ApplyToPath.GetHashCode();
                result = (result * 397) ^ ApplyToFile.GetHashCode();
                result = (result * 397) ^ ApplyToDirectory.GetHashCode();
                result = (result * 397) ^ CaseSensitive.GetHashCode();
                result = (result * 397) ^ (_text != null ? _text.GetHashCode() : 0);
                return result;
            }
        }

        public static bool operator ==(Filter left, Filter right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Filter left, Filter right)
        {
            return !Equals(left, right);
        }

        public override string ToString()
        {
            return string.Format("FilterType: {1}, Text: {0}, CaseSensitive: {2}", _text, FilterType, CaseSensitive);
        }

        private bool Match(string name)
        {
            BuildRegex();
            return _regex.IsMatch(name);
        }

        public bool Match(string fullPath, string relativePath, string name)
        {
            bool isFile = File.Exists(fullPath);
            bool isDirectory = Directory.Exists(fullPath);

            if (!isFile && !isDirectory)
                return false;

            if (ApplyToFile && !isFile && !ApplyToDirectory)
                return false;

            if (ApplyToDirectory && !isDirectory && !ApplyToFile)
                return false;

            if (ApplyToFileName && Match(name))
                return true;

            if (ApplyToPath && Match(relativePath))
                return true;

            return false;
        }

        private void BuildRegex()
        {
            if (_regex == null)
            {
                RegexOptions options = RegexOptions.Singleline | RegexOptions.Compiled;
                if (!CaseSensitive)
                {
                    options |= RegexOptions.IgnoreCase;
                }

                switch (ExpressionType)
                {
                    case FilterExpressionType.Regex:
                        _regex = new Regex(Text, options);
                        break;
                    case FilterExpressionType.Globbing:
                    default:
                        string escape = Regex.Escape(Text);
                        _regex = new Regex("^" + escape.Replace(@"\*", ".*").Replace(@"\|", "|").Replace(@"\?", ".") + "$", options);
                        break;
                }
            }
        }
    }
}