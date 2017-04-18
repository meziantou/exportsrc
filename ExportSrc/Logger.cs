using System;
using System.Diagnostics;

namespace ExportSrc
{
    public class Logger
    {
        public long Minimum { get; set; }
        public long Maximum { get; set; }

        private static Logger _current;

        public static Logger Current
        {
            get
            {
                if (_current == null)
                {
                    _current = new Logger();
                }

                return _current;
            }
        }

        private bool MustLog(Enum level)
        {
            long n = Convert.ToInt64(level);

            return n >= Minimum && n <= Maximum;
        }

        public Logger()
        {
            Minimum = long.MinValue;
            Maximum = long.MaxValue;
        }

        public void Log(Enum category, object value)
        {
            if (!MustLog(category))
                return;

            Trace.WriteLine(value, category.ToString());
        }

        public void Log(string category, object value)
        {
            Trace.WriteLine(value, category);
        }
    }
}