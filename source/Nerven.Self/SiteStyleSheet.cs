using System;
using System.IO;
using System.Linq;
using System.Text;
using Nerven.Assertion;

namespace Nerven.Self
{
    public static class SiteStyleSheet
    {
        private static readonly Lazy<string> _Value = new Lazy<string>(_GetValue);

        public static string Value => _Value.Value;

        private static string _GetValue()
        {
            var _assembly = typeof(SiteStyleSheet).Assembly;
            var _resourceName = _assembly.GetManifestResourceNames()
                .Single(_name => _name.EndsWith($".{nameof(SiteStyleSheet)}.less"));
            using (var _resourceStream = _assembly.GetManifestResourceStream(_resourceName))
            {
                Must.Assertion
                    .Assert(_resourceStream != null);

                using (var _resourceReader = new StreamReader(_resourceStream, Encoding.UTF8))
                {
                    return _resourceReader.ReadToEnd();
                }
            }
        }
    }
}
