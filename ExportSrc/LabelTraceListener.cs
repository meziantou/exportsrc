using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace ExportSrc
{
    public class LabelTraceListener : TraceListener
    {
        private readonly Label _label;

        public LabelTraceListener(Label label)
        {
            _label = label;
        }

        public override void Write(string message)
        {
            _label.BeginInvoke((Action)(() => { _label.Text = message; }));
        }

        public override void WriteLine(string message)
        {
            _label.BeginInvoke((Action)(() => { _label.Text = message; }));
        }
    }
}
