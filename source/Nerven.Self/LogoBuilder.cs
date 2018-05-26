using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using CliWrap;
using Nerven.Assertion;
using Nerven.Htmler.Core;
using Nerven.Htmler.Fundamentals;
using Nito.AsyncEx;
using static Nerven.Htmler.Core.HtmlBuilder;

namespace Nerven.Self
{
    public sealed class LogoBuilder
    {
        private readonly string _PngOptExecutable;
        private readonly ConcurrentDictionary<string, Logo> _Logos;

        public LogoBuilder(string pngOptExecutable)
        {
            _PngOptExecutable = pngOptExecutable;
            _Logos = new ConcurrentDictionary<string, Logo>();
        }
        
        public Logo GetLogo(ProjectInfo project)
        {
            return _Logos.GetOrAdd(_GetKey(project), _ => _CreateLogo(project));
        }

        private Logo _CreateLogo(ProjectInfo project)
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

            return new Logo(this, project, _key, _svgWithTextDocument);
        }

        private static XName _SvgElementName(string localName)
        {
            return XName.Get(localName, "http://www.w3.org/2000/svg");
        }

        private static string _GetKey(ProjectInfo project)
        {
            return project == null ? "nerven" : $"nerven-{project.GitHub.Name.ToLower()}";
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

        public sealed class Logo
        {
            private readonly LogoBuilder _LogoBuilder;
            private readonly ProjectInfo _Project;
            private readonly XDocument _SvgWithTextDocument;
            private readonly AsyncLock _Lock;
            private readonly Dictionary<int, byte[]> _PngBitmaps;
            private XDocument _PlainSvg;
            private IHtmlChildNode _SvgNode;

            public Logo(LogoBuilder logoBuilder, ProjectInfo project, string key, XDocument svgWithTextDocument)
            {
                _LogoBuilder = logoBuilder;
                _Project = project;
                Key = key;
                _SvgWithTextDocument = svgWithTextDocument;
                _Lock = new AsyncLock();
                _PngBitmaps = new Dictionary<int, byte[]>();
            }

            public string Key { get; }

            public async Task<XDocument> GetSvgDocumentAsync()
            {
                using (await _Lock.LockAsync().ConfigureAwait(false))
                {
                    if (_PlainSvg == null)
                        _PlainSvg = await _GeneratePlainSvg().ConfigureAwait(false);
                }

                return _PlainSvg;
            }

            public async Task<string> GetSvgDocumentDataUriAsync()
            {
                var _svgDocument = await GetSvgDocumentAsync().ConfigureAwait(false);
                using (var _stream = new MemoryStream())
                {
                    _svgDocument.Save(_stream);
                    _stream.Position = 0;
                    return _GetDataUri("image/svg+xml", _stream.ToArray());
                }
            }

            public async Task<IHtmlChildNode> GetSvgNodeAsync()
            {
                using (await _Lock.LockAsync().ConfigureAwait(false))
                {
                    if (_SvgNode == null)
                        _SvgNode = await _GenerateSvgNode().ConfigureAwait(false);
                }

                return _SvgNode.CloneChildNode();
            }

            public async Task<byte[]> GetPngDataAsync(int size)
            {
                using (await _Lock.LockAsync().ConfigureAwait(false))
                {
                    if (!_PngBitmaps.TryGetValue(size, out var _data))
                    {
                        _data = await _GeneratePngBitmap(size).ConfigureAwait(false);
                        _PngBitmaps[size] = _data;
                    }

                    return _data;
                }
            }

            public async Task<string> GetPngDataUriAsync(int size)
            {
                var _bitmapData = await GetPngDataAsync(size).ConfigureAwait(false);
                return _GetDataUri("image/png", _bitmapData);
            }

            private static string _GetDataUri(string mimeType, byte[] data)
            {
                return $"data:{mimeType};base64,{Convert.ToBase64String(data)}";
            }

            private async Task<XDocument> _GeneratePlainSvg()
            {
                var _key = _GetKey(_Project);

                XDocument _svgDocumentWithPath;
                XDocument _svgDocument;

                var _tempDirectoryPath = Path.Combine(Path.GetTempPath(), $"{typeof(LogoBuilder).FullName}_{Guid.NewGuid()}");
                try
                {
                    var _svgWithTextFilePath = Path.Combine(_tempDirectoryPath, $"{Guid.NewGuid()}.svg");
                    _CreateFileDirectoryIfMissing(_svgWithTextFilePath);
                    _SvgWithTextDocument.Save(_svgWithTextFilePath);

                    var _svgFilePath = Path.Combine(_tempDirectoryPath, $"{Guid.NewGuid()}.svg");
                    await _RunInkscape(_svgWithTextFilePath, $@"--export-text-to-path --export-plain-svg ""{_svgFilePath}""").ConfigureAwait(false);
                    _svgDocumentWithPath = XDocument.Load(_svgFilePath);
                    _svgDocument = XDocument.Load(_svgWithTextFilePath);
                }
                finally
                {
                    if (Directory.Exists(_tempDirectoryPath))
                        Directory.Delete(_tempDirectoryPath, true);
                }

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

                return _svgDocument;
            }

            private async Task<IHtmlChildNode> _GenerateSvgNode()
            {
                var _svgDocument = _PlainSvg ?? await _GeneratePlainSvg().ConfigureAwait(false);
                var _logoHtml = spanTag(classAttr("logo"), _svgDocument.Root.ToHtmlNode());
                return _logoHtml;
            }
            
            private async Task<byte[]> _GeneratePngBitmap(int pixelSize)
            {
                var _tempDirectoryPath = Path.Combine(Path.GetTempPath(), $"{typeof(LogoBuilder).FullName}_{Guid.NewGuid()}");
                try
                {
                    var _svgWithTextFilePath = Path.Combine(_tempDirectoryPath, $"{Guid.NewGuid()}.svg");
                    _CreateFileDirectoryIfMissing(_svgWithTextFilePath);
                    _SvgWithTextDocument.Save(_svgWithTextFilePath);

                    var _pngFilePath = Path.Combine(_tempDirectoryPath, $"{Guid.NewGuid()}.png");
                    await _RunInkscape(_svgWithTextFilePath, $@"--export-width {pixelSize} --export-height {pixelSize} --export-png ""{_pngFilePath}""").ConfigureAwait(false);

                    if (!string.IsNullOrEmpty(_LogoBuilder._PngOptExecutable))
                    {
                        using (var _pngOptCli = new Cli(_LogoBuilder._PngOptExecutable))
                        {
                            var _pngOptResult = await _pngOptCli.ExecuteAsync($"\"{_pngFilePath}\"").ConfigureAwait(false);
                            _pngOptResult.ThrowIfError();
                        }
                    }

                    return File.ReadAllBytes(_pngFilePath);
                }
                finally
                {
                    if (Directory.Exists(_tempDirectoryPath))
                        Directory.Delete(_tempDirectoryPath, true);
                }
            }

            private static async Task _RunInkscape(string sourceFilePath, string command)
            {
                using (var _inkscapeProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = @"c:\Program Files\Inkscape\inkscape.exe",
                        Arguments = $@"--file ""{sourceFilePath}"" {command}",
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
            }

            private static void _CreateFileDirectoryIfMissing(string filePath)
            {
                var _directoryPath = Path.GetDirectoryName(filePath);
                if (_directoryPath != null && !Directory.Exists(_directoryPath))
                    Directory.CreateDirectory(_directoryPath);
            }
        }
    }
}

