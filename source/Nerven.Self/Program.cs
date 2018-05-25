using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Nerven.CommandLineParser;

namespace Nerven.Self
{
    public static class Program
    {
        public const string BaseUriOptionKey = "baseUri";
        public const string ClearOutputDirectory = "clearOutputDir";
        public const string GitHubTokenOptionKey = "gitHubToken";
        public const string ExtraDataFilePathOptionKey = "extraDataFile";
        public const string CacheToOptionKey = "cacheTo";
        public const string CacheFromOptionKey = "cacheFrom";
        public const string PngOptExecutable = "pngOptExec";
        public const string GitCommit = "gitCommit";
        public const string GitAuthor = "gitAuthor";
        public const string GitPush = "gitPush";
        
        public static void Main()
        {
            Task.Run(() => MainAsync(Environment.CommandLine)).Wait();
        }

        public static async Task MainAsync(string args)
        {
            var _commandLine = StandardCommandLineParser.Default.ParseCommandLine(CommandLineSplitter.Default.ParseString(args));
            var _outputBaseDirectoryPath = _commandLine.SingleOrDefault(_part => _part.Type == CommandLineItemType.Argument)?.Value;
            var _clearOutputDirectory = _HasCommandLineFlag(_commandLine, ClearOutputDirectory);
            var _baseUri = _GetCommandLineOption(_commandLine, BaseUriOptionKey);
            var _gitHubToken = _GetCommandLineOption(_commandLine, GitHubTokenOptionKey);
            var _extraDataFile = _GetCommandLineOption(_commandLine, ExtraDataFilePathOptionKey);
            var _cacheTo = _GetCommandLineOption(_commandLine, CacheToOptionKey);
            var _cacheFrom = _GetCommandLineOption(_commandLine, CacheFromOptionKey);
            var _pngOptExecutable = _GetCommandLineOption(_commandLine, PngOptExecutable);
            var _gitCommit = _HasCommandLineFlag(_commandLine, GitCommit);
            var _gitAuthor = _GetCommandLineOption(_commandLine, GitAuthor);
            var _gitPush = _HasCommandLineFlag(_commandLine, GitPush);

            await MainAsync(_cacheTo, _cacheFrom, _outputBaseDirectoryPath, _clearOutputDirectory, _baseUri, _gitHubToken, _extraDataFile, _pngOptExecutable, _gitCommit, _gitAuthor, _gitPush).ConfigureAwait(false);
        }

        public static async Task MainAsync(string cacheTo,
            string cacheFrom,
            string outputBaseDirectoryPath,
            bool clearOutputDirectory,
            string baseUri,
            string gitHubToken,
            string extraDataFile,
            string pngOptExecutable,
            bool gitCommit,
            string gitAuthor,
            bool gitPush)
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

            if (clearOutputDirectory)
            {
                foreach (var _fileSystemEntryPath in Directory.EnumerateFileSystemEntries(outputBaseDirectoryPath))
                {
                    if (Directory.Exists(_fileSystemEntryPath))
                        Directory.Delete(_fileSystemEntryPath, true);
                    else
                        File.Delete(_fileSystemEntryPath);
                }
            }

            var _logoBuilder = new LogoBuilder(pngOptExecutable);
            await SiteGenerator.GenerateSiteAsync(baseUri, _logoBuilder, _data, outputBaseDirectoryPath).ConfigureAwait(false);
            await SitePublisher.PublishSiteAsync(outputBaseDirectoryPath, gitCommit, gitAuthor, gitPush).ConfigureAwait(false);
        }

        private static bool _HasCommandLineFlag(IReadOnlyList<CommandLineItem> commandLine, string key)
        {
            return commandLine.Any(_part => _part.Type == CommandLineItemType.Flag && _part.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        }

        private static string _GetCommandLineOption(IReadOnlyList<CommandLineItem> commandLine, string key)
        {
            return commandLine.FirstOrDefault(_part => _part.Type == CommandLineItemType.Option && _part.Key.Equals(key, StringComparison.OrdinalIgnoreCase))?.Value;
        }
    }
}
