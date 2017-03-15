using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.XPath;
using Nerven.Assertion;
using Nerven.Htmler;
using Nerven.Htmler.Core;
using static Nerven.Htmler.Core.HtmlBuilder;

namespace Nerven.Self
{
    public sealed class LogoBuilder
    {
        private readonly string _BaseUri;
        private readonly string _OutputBaseDirectoryPath;
        private readonly string[] _LogoDirectoryPath;
        private readonly Dictionary<string, IHtmlNode> _LogoHtmlNodes;

        public LogoBuilder(string baseUri, string outputBaseDirectoryPath, string[] logoDirectoryPath)
        {
            _BaseUri = baseUri;
            _OutputBaseDirectoryPath = outputBaseDirectoryPath;
            _LogoDirectoryPath = logoDirectoryPath;
            _LogoHtmlNodes = new Dictionary<string, IHtmlNode>();
        }

        public async Task BuildLogo(ProjectInfo project)
        {
            var _key = _GetKey(project);
            var _text = _GetText(project);

            int _fontSize;
            int _textY;
            switch (_text.Length)
            {
                case 1:
                case 2:
                    _fontSize = 1400;
                    _textY = 1550;
                    break;
                case 3:
                    _fontSize = 1000;
                    _textY = 1400;
                    break;
                default:
                    _fontSize = 800;
                    _textY = 1300;
                    break;
            }

            var _svgString = $@"<?xml version=""1.0"" encoding=""utf-8"" standalone=""no""?>
<svg
    xmlns:dc=""http://purl.org/dc/elements/1.1/""
    xmlns:rdf=""http://www.w3.org/1999/02/22-rdf-syntax-ns#""
    xmlns:svg=""http://www.w3.org/2000/svg""
    xmlns=""http://www.w3.org/2000/svg""
    viewBox=""0 0 2000 2000""
    id=""{_key}-logo""
    version=""1.1""
    width=""100%""
    height=""100%"">
    <defs>
        <style type=""text/css"">
<![CDATA[
@font-face {{
    font-family: 'Asap';
    src: url('./font-asap/Asap-Bold-webfont.eot');
    src: url('./font-asap/Asap-Bold-webfont.eot?#iefix') format('embedded-opentype'),
         url('./font-asap/Asap-Bold-webfont.woff') format('woff'),
         url('./font-asap/Asap-Bold-webfont.ttf') format('truetype'),
         url('./font-asap/Asap-Bold-webfont.svg#AsapBold') format('svg');
    font-weight: bold;
    font-style: normal;
}}
]]>
        </style>
    </defs>
    <g>
        <rect
            x=""0""
            y=""0""
            width=""2000""
            height=""2000""
            style=""fill:#248400"" />
        <polygon
            points=""0,0 185,0 1495,2000 0,2000""
            style=""fill:#ffffff;fill-opacity:0.2"" />
        <g
            transform=""translate(1000,{_textY})"">
            <text
                id=""{_key}-logo-text""
                font-family=""Asap""
                font-size=""{_fontSize}""
                font-weight=""bold""
                text-anchor=""middle""
                style=""fill:#fff"">
                <tspan>{_text}</tspan>
            </text>
        </g>
    </g>
</svg>
";

            var _svgWithTextDocument = XDocument.Parse(_svgString);

            var _svgWithTextFilePath = GetLogoFilePath(project, LogoFormat.SvgWithText);
            _CreateFileDirectoryIfMissing(_svgWithTextFilePath);
            var _svgFilePath = GetLogoFilePath(project, LogoFormat.Svg);
            _CreateFileDirectoryIfMissing(_svgFilePath);

            _svgWithTextDocument.Save(_svgWithTextFilePath);

            using (var _inkscapeProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = @"c:\Program Files\Inkscape\inkscape.exe",
                    Arguments = $@"--file ""{_svgWithTextFilePath}"" --export-text-to-path --export-plain-svg ""{_svgFilePath}""",
                },
                EnableRaisingEvents = true,
            })
            {
                var _exitSource = new TaskCompletionSource<int>();
                _inkscapeProcess.Exited += (_sender, _v) => _exitSource.SetResult(0);

                Must.Assertion
                    .Assert(_inkscapeProcess.Start());

                await _exitSource.Task.ConfigureAwait(false);

                Must.Assertion
                    .Assert(_inkscapeProcess.ExitCode == 0);
            }

            var _svgDocumentWithPath = XDocument.Load(_svgFilePath);
            var _svgDocument = XDocument.Load(_svgWithTextFilePath);

            var _textPaths = _svgDocumentWithPath.Root?
                .Element(_SvgElementName("g"))?
                .Element(_SvgElementName("g"))?
                .Element(_SvgElementName("g"))?
                .Elements(_SvgElementName("path"))
                .Select(_pathElement => _pathElement.Attribute("d")?.Value)
                .ToList();

            Must.Assertion
                .Assert(_textPaths != null)
                .Assert(_textPaths.Count != 0)
                .Assert(_svgDocument.Root != null);

            foreach (var _element in _svgDocument.Root.DescendantsAndSelf().ToList())
            {
                if (_element.Name.LocalName == "defs")
                {
                    _element.Remove();
                }
                else if (_element.Attribute("id")?.Value == $"{_key}-logo-text")
                {
                    _element.ReplaceWith(_textPaths.Select(_textPath => new XElement(
                        _SvgElementName("path"),
                        new XAttribute("style", "fill:#ffffff"),
                        new XAttribute("d", _textPath),
                        string.Empty)));
                }
                else if (_element.IsEmpty)
                {
                    _element.Value = string.Empty;
                }
            }

            _svgDocument.Save(_svgFilePath);

            var _logoHtml = spanTag(classAttr("logo"), _svgDocument.Root.ToHtmlNode());
            _LogoHtmlNodes.Add(_key, _logoHtml);
        }

        private static XName _SvgElementName(string localName)
        {
            return XName.Get(localName, "http://www.w3.org/2000/svg");
        }

        public IHtmlNode GetLogoHtml(ProjectInfo project)
        {
            return _LogoHtmlNodes[_GetKey(project)].Clone();
        }

        public string GetLogoFilePath(ProjectInfo project, LogoFormat format)
        {
            return Path.Combine(_OutputBaseDirectoryPath, Path.Combine(_LogoDirectoryPath), _GetFormatKey(format), _GetKey(project) + _GetExtension(format));
        }

        public string GetLogoUri(ProjectInfo project, LogoFormat format)
        {
            return new Uri($"{_BaseUri}/{string.Join("/", _LogoDirectoryPath.Select(Uri.EscapeDataString))}/{_GetFormatKey(format)}/{_GetKey(project)}{_GetExtension(format)}").ToString();
        }

        private static string _GetKey(ProjectInfo project)
        {
            return project == null ? "nerven" : $"nerven-{project.GitHub.Name.ToLower()}";
        }

        private static string _GetFormatKey(LogoFormat format)
        {
            switch (format)
            {
                case LogoFormat.Svg:
                    return "svg";
                case LogoFormat.SvgWithText:
                    return "svg-text";
                case LogoFormat.Png16x16:
                    return "16x16_png";
                case LogoFormat.Png48x48:
                    return "48x48_png";
                case LogoFormat.Png256x256:
                    return "256x256_png";
                case LogoFormat.Png4096x4096:
                    return "4096x4096_png";
                default:
                    throw Must.Assertion.AssertNever();
            }
        }

        private static string _GetExtension(LogoFormat format)
        {
            switch (format)
            {
                case LogoFormat.Svg:
                case LogoFormat.SvgWithText:
                    return ".svg";
                case LogoFormat.Png16x16:
                case LogoFormat.Png48x48:
                case LogoFormat.Png256x256:
                case LogoFormat.Png4096x4096:
                    return ".png";
                default:
                    throw Must.Assertion.AssertNever();
            }
        }

        private static string _GetText(ProjectInfo project)
        {
            if (project == null)
            {
                return "N";
            }

            if (project.Extra?.LogoText != null)
            {
                return project.Extra.LogoText;
            }

            var _chars = project.GitHub.Name.Where(char.IsUpper).Concat(".").ToArray();
            if (_chars.Length <= 2)
            {
                return project.GitHub.Name.Substring(0, 2) + ".";
            }

            return new string(_chars);
        }

        private static void _CreateFileDirectoryIfMissing(string filePath)
        {
            var _directoryPath = Path.GetDirectoryName(filePath);
            if (_directoryPath != null && !Directory.Exists(_directoryPath))
                Directory.CreateDirectory(_directoryPath);
        }
    }
}
