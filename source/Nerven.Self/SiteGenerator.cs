using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using dotless.Core.configuration;
using Nerven.Htmler;
using Nerven.Htmler.Core;
using static Nerven.Htmler.Core.HtmlBuilder;

namespace Nerven.Self
{
    public class SiteGenerator
    {
        private readonly string _BaseUri;
        private readonly LogoBuilder _LogoBuilder;
        private string _PersonalUri = "http://victorblomberg.se/en/";

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
            var _site = Site(_CreateIndexHtmlDocument(data.Projects));
            _site.Resources.AddRange(data.Projects.Select(_CreateProjectHtmlDocument).ToList());

            await _site.WriteToDirectory(outputBaseDirectoryPath).ConfigureAwait(false);
        }

        private Uri _ResourceUri(string resourcePath)
        {
            return new Uri($"{_BaseUri}{resourcePath}");
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

        private IHtmlDocumentResource _CreateIndexHtmlDocument(IReadOnlyList<ProjectInfo> gitHubRepositories)
        {
            return _CreateHtmlDocument(
                new string[] { },
                null,
                _LogoImage(null),
                divTag(
                    ////sectionTag(
                    ////    classAttr("nerven-info-section"),
                    ////    pTag(
                    ////        Text("wherein "),
                    ////        aTag(
                    ////            hrefAttr(_PersonalUri),
                    ////            Text("I")),
                    ////        Text(" explore how to write software"),
                    ////        brTag(),
                    ////        Text("that makes writing software simpler"))),
                    divTag(
                        classAttr("projects-index"),
                        divTag(gitHubRepositories.Select(_gitHubRepository => sectionTag(
                            aTag(
                                hrefAttr(_DocumentUri(_gitHubRepository)),
                                headerTag(
                                    h2Tag(
                                        _LogoImage(_gitHubRepository),
                                        Text(_gitHubRepository.GitHub.Name))),
                                pTag(Text(_gitHubRepository.GitHub.Description)))
                        )).Cast<IHtmlNode>().ToArray()))
                ));
        }

        private IHtmlDocumentResource _CreateProjectHtmlDocument(ProjectInfo project)
        {
            return _CreateHtmlDocument(
                new[] { project.GitHub.Name.ToLower() },
                project.GitHub.Name,
                _LogoImage(project),
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
                                project.Extra?.DotNetPlatforms?.Count > 0
                                    ? new[] { dtTag(Text(".NET platform")) }
                                        .Concat(project.Extra.DotNetPlatforms
                                            .Select(_dotNetPlatform => ddTag(Text(_dotNetPlatform)))).ToArray()
                                    : null,
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
                            preTag(Text(project.GitHub.LicenseText.Replace("(c)", "©").Replace("(C)", "©"))))));
        }

        private IHtmlNode _LogoImage(ProjectInfo project)
        {
            return _LogoBuilder.GetLogoHtml(project);
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

        private IHtmlDocumentResource _CreateHtmlDocument(IReadOnlyList<string> path, string pageTitle, IHtmlNode logo, IHtmlElement contents)
        {
            var _header = pageTitle == null
                ? h1Tag(
                    aTag(
                        hrefAttr(_DocumentUri()),
                        logo,
                        Text("Nerven")))
                : h1Tag(
                    aTag(
                        hrefAttr(_DocumentUri(path)),
                        logo,
                        smallTag(
                            Text("Nerven")),
                        brTag(),
                        Text(pageTitle)));

            Func<IHtmlNode> _breadcrumbSeparator = () => spanTag(Text(" / "));
            var _breadcrumbs = path.Count == 0
                ? null
                : new[] { spanTag(aTag(hrefAttr(_DocumentUri()), Text("Nerven")), _breadcrumbSeparator()) }
                    .Concat(path.Take(path.Count - 1).Select(_level => spanTag(Text("..."), _breadcrumbSeparator())))
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
                            ////linkTag(relAttr("icon"), typeAttr("image/png"), Attribute("sizes", "16x16"), hrefAttr(_ResourceUri("/external-assets/logo/16x16_png/nerven.png"))),
                            ////linkTag(relAttr("icon"), typeAttr("image/png"), Attribute("sizes", "48x48"), hrefAttr(_ResourceUri("/external-assets/logo/48x48_png/nerven.png"))),
                            ////linkTag(relAttr("icon"), typeAttr("image/png"), Attribute("sizes", "256x256"), hrefAttr(_ResourceUri("/external-assets/logo/256x256_png/nerven.png"))),
                            ////linkTag(relAttr("icon"), typeAttr("image/svg+xml"), Attribute("sizes", "any"), hrefAttr(_ResourceUri("/external-assets/logo/svg_traced/nerven.svg")))),
                            linkTag(relAttr("icon"), typeAttr("image/png"), Attribute("sizes", "16x16"), hrefAttr(_LogoBuilder.GetLogoUri(null, LogoFormat.Png16x16))),
                            linkTag(relAttr("icon"), typeAttr("image/png"), Attribute("sizes", "48x48"), hrefAttr(_LogoBuilder.GetLogoUri(null, LogoFormat.Png48x48))),
                            linkTag(relAttr("icon"), typeAttr("image/png"), Attribute("sizes", "256x256"), hrefAttr(_LogoBuilder.GetLogoUri(null, LogoFormat.Png256x256))),
                            linkTag(relAttr("icon"), typeAttr("image/svg+xml"), Attribute("sizes", "any"), hrefAttr(_LogoBuilder.GetLogoUri(null, LogoFormat.Svg)))),
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
                                                Text("Victor Blomberg"))))))))));
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
