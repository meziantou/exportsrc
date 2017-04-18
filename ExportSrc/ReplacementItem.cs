using System.Xml.Serialization;

namespace ExportSrc
{
    public class ReplacementItem
    {
        [XmlAttribute("text")]
        public string SearchText { get; set; }
        [XmlAttribute("by")]
        public string ReplacementText { get; set; }
    }
}