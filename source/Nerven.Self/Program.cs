using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nerven.CommandLineParser;

namespace Nerven.Self
{
    public static class Program
    {
        public const string BaseUriOptionKey = "baseUri";
        public const string GitHubTokenOptionKey = "gitHubToken";
        public const string ExtraDataFilePathOptionKey = "extraDataFile";
        public const string CacheToOptionKey = "cacheTo";
        public const string CacheFromOptionKey = "cacheFrom";
        
        public static void Main()
        {
            Task.Run(() => MainAsync(Environment.CommandLine)).Wait();
        }

        public static async Task MainAsync(string args)
        {
            var _commandLine = StandardCommandLineParser.Default.ParseCommandLine(CommandLineSplitter.Default.ParseString(args));
            var _outputBaseDirectoryPath = _commandLine.SingleOrDefault(_part => _part.Type == CommandLineItemType.Argument)?.Value;
            var _baseUri = _GetCommandLineOption(_commandLine, BaseUriOptionKey);
            var _gitHubToken = _GetCommandLineOption(_commandLine, GitHubTokenOptionKey);
            var _extraDataFile = _GetCommandLineOption(_commandLine, ExtraDataFilePathOptionKey);
            var _cacheTo = _GetCommandLineOption(_commandLine, CacheToOptionKey);
            var _cacheFrom = _GetCommandLineOption(_commandLine, CacheFromOptionKey);

            await MainAsync(_cacheTo, _cacheFrom, _outputBaseDirectoryPath, _baseUri, _gitHubToken, _extraDataFile).ConfigureAwait(false);
        }

        public static async Task MainAsync(string cacheTo, string cacheFrom, string outputBaseDirectoryPath, string baseUri, string gitHubToken, string extraDataFile)
        {
            SiteData _data;
            if (cacheFrom != null)
            {
                _data = Helpers.DeserializeData<SiteData>(cacheFrom);
            }
            else
            {
                _data = await DataFetcher.FetchDataAsync(gitHubToken, extraDataFile).ConfigureAwait(false);

                if (cacheTo != null)
                {
                    Helpers.SerializeData(cacheTo, _data);
                }
            }

            var _logoBuilder = new LogoBuilder(baseUri, outputBaseDirectoryPath, new []{ "external-assets", "logo" });

            await _logoBuilder.BuildLogo(null).ConfigureAwait(false);
            foreach (var _project in _data.Projects)
            {
                await _logoBuilder.BuildLogo(_project).ConfigureAwait(false);
            }

            await SiteGenerator.GenerateSiteAsync(baseUri, _logoBuilder, _data, outputBaseDirectoryPath).ConfigureAwait(false);
        }

        private static string _GetCommandLineOption(IReadOnlyList<CommandLineItem> commandLine, string key)
        {
            return commandLine.FirstOrDefault(_part => _part.Type == CommandLineItemType.Option && _part.Key.Equals(key, StringComparison.OrdinalIgnoreCase))?.Value;
        }
    }
}
