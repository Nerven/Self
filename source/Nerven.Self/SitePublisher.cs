using System;
using System.Globalization;
using System.Threading.Tasks;
using CliWrap;
using CliWrap.Models;

namespace Nerven.Self
{
    public sealed class SitePublisher
    {
        public static async Task PublishSiteAsync(string outputBaseDirectoryPath, bool gitCommit, string gitAuthor, bool gitPush)
        {
            if (gitCommit)
            {
                using (var _git = new Cli("git", new CliSettings
                {
                    WorkingDirectory = outputBaseDirectoryPath
                }))
                {
                    await _ExecuteCommandAsync(_git, "add .").ConfigureAwait(false);
                    var _statusOutput = await _ExecuteCommandAsync(_git, "status --porcelain").ConfigureAwait(false);
                    if (string.IsNullOrEmpty(_statusOutput.StandardOutput))
                        return;

                    await _ExecuteCommandAsync(_git, $"commit {(string.IsNullOrEmpty(gitAuthor) ? string.Empty : $@"--author ""{gitAuthor}"" ")}-m \"nerven.se regenerated {DateTimeOffset.UtcNow.ToString("u", CultureInfo.InvariantCulture)}\"").ConfigureAwait(false);

                    if (gitPush)
                    {
                        await _ExecuteCommandAsync(_git, "push").ConfigureAwait(false);
                    }
                }
            }
        }

        private static async Task<ExecutionOutput> _ExecuteCommandAsync(ICli cli, string command)
        {
            var _output = await cli.ExecuteAsync(command).ConfigureAwait(false);
            if (_output.ExitCode != 0)
                throw new Exception();

            return _output;
        }
    }
}
