using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Octokit;

namespace Nerven.Self
{
    public static class DataFetcher
    {
        private const string _GithubOrganizationName = "Nerven";
        private const string _GithubApiUserAgent = "Nerven.Self";

        public static async Task<SiteData> FetchDataAsync(string gitHubToken, string extraDataFile)
        {
            var _gitHubRepositories = await _FetchGitHubData(gitHubToken).ConfigureAwait(false);

            var _extraData = extraDataFile == null ? null : _GetExtraData(extraDataFile);

            var _projects = _gitHubRepositories
                .Select(_gitHubRepository => new ProjectInfo(_extraData?.Projects.SingleOrDefault(_projectExtra => _projectExtra.Name == _gitHubRepository.Name), _gitHubRepository))
                .ToList();

            return new SiteData(_projects);
        }

        private static ExtraData _GetExtraData(string extraDataFile)
        {
            return Helpers.DeserializeData<ExtraData>(extraDataFile);
        }

        private static async Task<IReadOnlyList<GitHubRepositoryInfo>> _FetchGitHubData(string gitHubToken)
        {
            var _github = new GitHubClient(new ProductHeaderValue(_GithubApiUserAgent))
            {
                Credentials = new Credentials(gitHubToken)
            };

            var _repositoryTasks = (await _github.Repository.GetAllForOrg(_GithubOrganizationName).ConfigureAwait(false))
                .Where(_repository => !_repository.Private)
                .Select(async _repository =>
                    {
                        Readme _readme;
                        try
                        {
                            _readme = await _github.Repository.Content.GetReadme(_repository.Id).ConfigureAwait(false);
                        }
                        catch (NotFoundException)
                        {
                            _readme = null;
                        }

                        string _readmeHtml;
                        try
                        {
                            _readmeHtml = await _github.Repository.Content.GetReadmeHtml(_repository.Id).ConfigureAwait(false);
                        }
                        catch (NotFoundException)
                        {
                            _readmeHtml = null;
                        }

                        string _licenseText;
                        try
                        {
                            var _licenseFiles = await _github.Repository.Content.GetAllContents(_repository.Id, "LICENSE.txt").ConfigureAwait(false);
                            _licenseText = _licenseFiles.SingleOrDefault()?.Content;
                        }
                        catch (NotFoundException)
                        {
                            _licenseText = null;
                        }

                        var _licenseName = _licenseText != null && _licenseText.StartsWith("﻿The MIT License (MIT)\n") ? "MIT License" : null;

                        return new GitHubRepositoryInfo(
                            _repository.Owner.Login,
                            _repository.FullName,
                            _repository.Name,
                            _repository.Description,
                            _repository.HtmlUrl,
                            _repository.CloneUrl,
                            _repository.GitUrl,
                            _repository.SshUrl,
                            _readme?.Content,
                            _readmeHtml,
                            _licenseText,
                            _licenseName);
                    })
                .ToList();

            var _reporitories = (await Task.WhenAll(_repositoryTasks).ConfigureAwait(false))
                .Where(_repository => _repository.Description != null)
                .OrderBy(_repository => _repository.Name)
                .ToList();

            return _reporitories;
        }
    }
}
