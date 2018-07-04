using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using dotless.Core.configuration;
using Nerven.Assertion;
using Nerven.Htmler;
using Nerven.Htmler.Build;
using Nerven.Htmler.Core;
using Nerven.Htmler.Fundamentals;
using static Nerven.Htmler.Core.HtmlBuilder;

//// ReSharper disable CoVariantArrayConversion
namespace Nerven.Self
{
    public class SiteGenerator
    {
        private readonly string _BaseUri;
        private readonly LogoBuilder _LogoBuilder;
        private string _PersonalUri = "http://victorblomberg.se/en/";
        private string _NervenSelfUri = "https://github.com/Nerven/Self/";

        private SiteGenerator(string baseUri, LogoBuilder logoBuilder)
        {
            _BaseUri = baseUri;
            _LogoBuilder = logoBuilder;
        }

        public static async Task GenerateSiteAsync(string baseUri, LogoBuilder logoBuilder, SiteData data, string outputBaseDirectoryPath)
        {
            await new SiteGenerator(baseUri, logoBuilder)._GenerateSiteAsync(data, outputBaseDirectoryPath).ConfigureAwait(false);
        }

        private async Task _GenerateSiteAsync(SiteData data, string outputBaseDirectoryPath)
        {
            var _site = Site();
            
            var _logoGeneratorTask = Task.WhenAll(
                new[] { default(ProjectInfo) }.Concat(data.Projects)
                    .Select(_project => _GenerateProjectLogosAsync(_project, _site)));

            _AddFileResourcesToSite(_site, Path.Combine(Environment.CurrentDirectory, "Resources"));

            var _projectPagesTask = Task.WhenAll(data.Projects.Select(async _project =>
            {
                var _projectDocumentResource = await _CreateProjectHtmlDocumentAsync(_project).ConfigureAwait(false);
                _site.Resources.Add(_projectDocumentResource);
            }));

            _site.Resources.Add(await _CreateIndexHtmlDocumentAsync(data.Projects).ConfigureAwait(false));

            await _logoGeneratorTask.ConfigureAwait(false);
            await _projectPagesTask.ConfigureAwait(false);
            await _site.WriteToDirectory(outputBaseDirectoryPath).ConfigureAwait(false);
        }

        private async Task _GenerateProjectLogosAsync(ProjectInfo project, IHtmlSite site)
        {
            var _logo = _LogoBuilder.GetLogo(project);
            var _svgDocument = await _logo.GetSvgDocumentAsync().ConfigureAwait(false);
            var _svgResource = HtmlStreamResourceProperties.CreateStreamResource(null, () =>
            {
                var _stream = new MemoryStream();
                _svgDocument.Save(_stream);
                _stream.Position = 0;
                return _stream;
            });
            _svgResource.Name = new[] { "external-assets", "logo", "svg", _logo.Key + ".svg" };
            site.Resources.Add(_svgResource);

            foreach (var _pixelSize in new[] { 16, 32, 48, 256, 512, 4096 })
            {
                var _sizeString = _pixelSize.ToString(CultureInfo.InvariantCulture);

                var _pngResource = HtmlStreamResourceProperties.CreateStreamResource("image/png", await _logo.GetPngDataAsync(_pixelSize).ConfigureAwait(false));
                _pngResource.Name = new[] { "external-assets", "logo", _sizeString + "x" + _sizeString + "_png", _logo.Key + ".png" };
                site.Resources.Add(_pngResource);
            }
        }

        private static void _AddFileResourcesToSite(IHtmlSite site, string resourcesDirectoryPath)
        {
            var _cleanResourcesDirectoryPath = resourcesDirectoryPath.EndsWith("\\", StringComparison.Ordinal)
                ? resourcesDirectoryPath.Substring(0, resourcesDirectoryPath.Length - 1)
                : resourcesDirectoryPath;
            foreach (var _resourceFilePath in Directory.GetFiles(_cleanResourcesDirectoryPath, "*", SearchOption.AllDirectories))
            {
                Must.Assertion
                    .Assert(_resourceFilePath.StartsWith(_cleanResourcesDirectoryPath, StringComparison.Ordinal));

                var _nameParts = _resourceFilePath.Substring(_cleanResourcesDirectoryPath.Length + 1).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var _resource = HtmlStreamResourceProperties.CreateStreamResource(null, _ => new FileStream(_resourceFilePath, FileMode.Open, FileAccess.Read));
                _resource.Name = new[] { "assets" }.Concat(_nameParts).ToArray();

                site.Resources.Add(_resource);
            }
        }
        
        private Uri _DocumentUri(IReadOnlyList<string> path)
        {
            var _baseUriUri = new Uri(_BaseUri, UriKind.RelativeOrAbsolute);
            return new Uri($"{_BaseUri}/{string.Join("/", path.Select(Uri.EscapeDataString))}{(path.Count != 0 ? "/" : string.Empty)}{(_baseUriUri.IsFile ? "index.html" : string.Empty)}");
        }

        private Uri _DocumentUri(params string[] path)
        {
            IReadOnlyList<string> _path = path;
            return _DocumentUri(_path);
        }

        private Uri _DocumentUri(ProjectInfo gitHubRepository)
        {
            return _DocumentUri(gitHubRepository.GitHub.Name.ToLower());
        }

        private async Task<IHtmlDocumentResource> _CreateIndexHtmlDocumentAsync(IReadOnlyList<ProjectInfo> projects)
        {
            var _projectsNode = divTag();
            foreach (var _project in projects
                .OrderByDescending(_p => _p.Extra?.Recommended != false)
                .ThenBy(_p => _p.GitHub.Name))
            {
                _projectsNode.Children.Add(
                    sectionTag(
                        classAttr(_project.Extra?.Recommended == false ? "obsolete-project" : null),
                        aTag(
                            hrefAttr(_DocumentUri(_project)),
                            headerTag(
                                h2Tag(
                                    await _LogoImageAsync(_project).ConfigureAwait(false),
                                    Text(_project.GitHub.Name))),
                            pTag(Text(_project.GitHub.Description)))));
            }

            return await _CreateHtmlDocumentAsync(
                new string[] { },
                null,
                _LogoBuilder.GetLogo(null),
                divTag(
                    divTag(
                        classAttr("projects-index"),
                        _projectsNode
                    )
                )).ConfigureAwait(false);
        }

        private async Task<IHtmlDocumentResource> _CreateProjectHtmlDocumentAsync(ProjectInfo project)
        {
            return await _CreateHtmlDocumentAsync(
                new[] { project.GitHub.Name.ToLower() },
                project.GitHub.Name,
                _LogoBuilder.GetLogo(project),
                articleTag(
                    classAttr("project-article"),
                    sectionTag(
                        classAttr("project-info-section"),
                        pTag(Text(project.GitHub.Description)),
                        dlTag(new IHtmlNode[][]
                            {
                                new[] { dtTag(Text("GitHub")), ddTag(aTag(hrefAttr(project.GitHub.HtmlUrl), Text($"github.com/{project.GitHub.OwnerName}/{project.GitHub.Name}"))) },
                                project.Extra?.NugetPackages?.Count > 0
                                    ? new[] { dtTag(Text("NuGet")) }
                                        .Concat(project.Extra.NugetPackages
                                            .Select(_nugetPackage => ddTag(
                                                kbdTag(
                                                    Text("Install-Package "),
                                                    Text(_nugetPackage)),
                                                Text(" ("),
                                                aTag(
                                                    hrefAttr($"https://www.nuget.org/packages/{_nugetPackage}/"),
                                                    Text(_nugetPackage)),
                                                Text(")")))).ToArray()
                                    : null,
                                project.Extra?.DotNetPlatforms?.Count > 0
                                    ? new[] { dtTag(Text(".NET platform")) }
                                        .Concat(project.Extra.DotNetPlatforms
                                            .Select(_dotNetPlatform => ddTag(Text(_dotNetPlatform)))).ToArray()
                                    : null,
                                project.Extra?.DevelopmentStatus.HasValue == true
                                    ? new[]
                                    {
                                        dtTag(Text("Development")),
                                        ddTag(Text(project.Extra.DevelopmentStatus.ToString())),
                                    }
                                    : null,
                                project.GitHub.LicenseName != null
                                    ? new[]
                                        {
                                            dtTag(Text("License")),
                                            ddTag(Text(project.GitHub.LicenseName)),
                                        }
                                    : null,
                                new[]
                                    {
                                        dtTag(Text("Source (git)")),
                                        ddTag(kbdTag(Text(project.GitHub.SshUrl))),
                                        ddTag(kbdTag(Text(project.GitHub.CloneUrl))),
                                    },
                                new[]
                                    {
                                        dtTag(Text("Source (archive)")),
                                        ddTag(aTag(hrefAttr($"{project.GitHub.HtmlUrl}/archive/master.tar.gz"), Text(".tar.gz"))),
                                        ddTag(aTag(hrefAttr($"{project.GitHub.HtmlUrl}/archive/master.zip"), Text(".zip"))),
                                    },
                            }.Where(_childNodes => _childNodes != null).SelectMany(_childNodes => _childNodes).ToArray())),
                    project.GitHub.ReadmeHtml == null
                        ? null
                        : sectionTag(
                            classAttr("project-readme-section"),
                            headerTag(
                                h2Tag(Text("Readme"))),
                            divTag(_FixReadmeHtml(project))),
                    project.GitHub.LicenseText == null
                        ? null
                        : sectionTag(
                            classAttr("project-license-section"),
                            headerTag(
                                h2Tag(Text("License"))),
                            preTag(Text(project.GitHub.LicenseText.Replace("(c)", "©").Replace("(C)", "©")))))).ConfigureAwait(false);
        }

        private Task<IHtmlChildNode> _LogoImageAsync(ProjectInfo project)
        {
            return _LogoBuilder.GetLogo(project).GetSvgNodeAsync();
        }

        private IHtmlNode _FixReadmeHtml(ProjectInfo project)
        {
            var _readmeHtml = project.GitHub.ReadmeHtml;

            var _readmeElement = XElement.Parse(_readmeHtml, LoadOptions.PreserveWhitespace);

            var _anchors = _readmeElement.Descendants("a")
                .Where(_descendantAElement => _descendantAElement.Attribute("class")?.Value == "anchor")
                .ToList();
            foreach (var _anchor in _anchors)
            {
                _anchor.Elements("svg").Remove();
                _anchor.ReplaceWith(_anchor.Nodes());
            }

            foreach (var _element in _readmeElement.DescendantsAndSelf().ToList())
            {
                if (_element.Name.LocalName.Length == 2 && _element.Name.LocalName[0] == 'h' && _element.Value == project.GitHub.Name)
                {
                    _element.Remove();
                }

                _element.Attributes().Where(_attribute => _attribute.Name.ToString().StartsWith("data-")).Remove();
                _element.Attributes("id").Remove();
                _element.Attributes("itemprop").Remove();
            }

            foreach (var _element in _readmeElement.DescendantsAndSelf().Where(_element => _element.Name.LocalName == "pre").ToList())
            {
                var _codeLineStart = "<div class=\"code-line\">";
                var _codeLineEnd = "</div>";
                _element.RemoveAttributes();
                var _html = _element.ToString();
                var _newHtml = _html
                    .Replace("<pre>\r\n  ", "<pre>")
                    .Replace("<pre>", "<pre>" + _codeLineStart)
                    .Replace("</pre>", _codeLineEnd + "</pre>")
                    .Replace("\r\n", _codeLineEnd + _codeLineStart)
                    .Replace("\n", _codeLineEnd + _codeLineStart)
                    .Replace("\r", string.Empty);
                var _newElement = XElement.Parse(_newHtml, LoadOptions.PreserveWhitespace);
                _newElement.Add(new XAttribute("class", "code"));

                _element.ReplaceWith(_newElement);
            }

            return _readmeElement.ToHtmlNode();
        }

        private async Task<IHtmlDocumentResource> _CreateHtmlDocumentAsync(IReadOnlyList<string> path, string pageTitle, LogoBuilder.Logo logo, IHtmlElement contents)
        {
            var _logoNode = await logo.GetSvgNodeAsync().ConfigureAwait(false);

            var _header = pageTitle == null
                ? h1Tag(
                    aTag(
                        hrefAttr(_DocumentUri()),
                        _logoNode,
                        Text("Nerven")))
                : h1Tag(
                    aTag(
                        hrefAttr(_DocumentUri(path)),
                        _logoNode,
                        smallTag(
                            Text("Nerven")),
                        brTag(),
                        Text(pageTitle)));

            IHtmlNode BreadcrumbSeparator() => spanTag(Text(" / "));
            var _breadcrumbs = path.Count == 0
                ? null
                : new[] { spanTag(aTag(hrefAttr(_DocumentUri()), await _LogoImageAsync(null).ConfigureAwait(false), Text("Nerven")), BreadcrumbSeparator()) }
                    .Concat(path.Take(path.Count - 1).Select(_level => spanTag(Text("..."), BreadcrumbSeparator())))
                    .Concat(new[] { spanTag(aTag(hrefAttr(_DocumentUri(path)), Text(pageTitle))) })
                    .ToArray<IHtmlNode>();

            return DocumentResource(
                path,
                Document(
                    htmlTag(
                        langAttr("en"),
                        headTag(
                            metaTag(charsetAttr(Encoding.UTF8)),
                            metaTag(nameAttr("viewport"), contentAttr("width=device-width, initial-scale=1.0")),
                            titleTag(Text(pageTitle == null ? "Nerven" : $"Nerven {pageTitle}")),
                            styleTag(
                                typeAttr("text/css"),
                                _GetStyleSheet()),
                            linkTag(relAttr("icon"), typeAttr("image/png"), Attribute("sizes", "16x16"), hrefAttr(await logo.GetPngDataUriAsync(16).ConfigureAwait(false))),
                            linkTag(relAttr("icon"), typeAttr("image/svg+xml"), Attribute("sizes", "any"), hrefAttr(await logo.GetSvgDocumentDataUriAsync().ConfigureAwait(false)))),
                        bodyTag(
                            divTag(
                                _breadcrumbs == null
                                    ? null
                                    : navTag(
                                        classAttr("site-nav"),
                                        divTag(_breadcrumbs)),
                                headerTag(
                                    classAttr("site-header"),
                                    _header),
                                divTag(
                                    classAttr("site-contents"),
                                    contents),
                                footerTag(
                                    classAttr("site-footer"),
                                    divTag(
                                        spanTag(
                                            aTag(
                                                hrefAttr(_DocumentUri()),
                                                Text("Nerven"))),
                                        spanTag(
                                            Text(" by ")),
                                        spanTag(
                                            aTag(
                                                hrefAttr(_PersonalUri),
                                                Text("Victor Blomberg")))),
                                    divTag(
                                        spanTag(
                                            Text("This website, including the Nerven logo and all Nerven project logos, was automagically generated by ")),
                                        spanTag(
                                            aTag(
                                                hrefAttr(_NervenSelfUri),
                                                Text("Nerven.Self"))))))))));
        }

        private IHtmlRaw _GetStyleSheet()
        {
            var _dotlessConfig = new DotlessConfiguration
            {
                MinifyOutput = true
            };

            var _less = SiteStyleSheet.Value
                .Replace("'__BASE_URI'", $"'{_BaseUri}'");
            var _css = dotless.Core.Less.Parse(_less, _dotlessConfig)
                .Replace("\r\n", " ")
                .Replace('\n', ' ');

            return Raw(_css);
        }
    }
}
