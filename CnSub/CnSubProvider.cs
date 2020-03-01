using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Security;
using MediaBrowser.Controller.Subtitles;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Serialization;

namespace CnSub
{
    public class CnSubProvider : ISubtitleProvider, IDisposable
    {
        public const string NAME = "CnSub";

        private readonly ILogger _logger;
        private readonly IHttpClient _httpClient;
        private readonly IServerConfigurationManager _config;
        private readonly IEncryptionManager _encryption;
        private readonly IJsonSerializer _json;
        private readonly IFileSystem _fileSystem;
        private readonly ILocalizationManager _localizationManager;

        private readonly List<BaseSubProvider> providers = new List<BaseSubProvider>();

        public CnSubProvider(ILogger logger, IHttpClient httpClient, IServerConfigurationManager config, IEncryptionManager encryption, IJsonSerializer json, IFileSystem fileSystem, ILocalizationManager localizationManager)
        {
            _logger = logger;
            _httpClient = httpClient;
            _config = config;
            _encryption = encryption;
            _json = json;
            _fileSystem = fileSystem;
            _localizationManager = localizationManager;
            if (_config != null)
            {
                _config.NamedConfigurationUpdating += _config_NamedConfigurationUpdating;
            }

            providers.Add(new SubhdProvider(logger, httpClient, config, encryption, json, fileSystem, localizationManager));
        }

        private const string PasswordHashPrefix = "h:";
        void _config_NamedConfigurationUpdating(object sender, ConfigurationUpdateEventArgs e)
        {
            if (!string.Equals(e.Key, "cn_sub", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var options = (CnSubOptions)e.NewConfiguration;

            if (options != null &&
                !string.IsNullOrWhiteSpace(options.SubhdPasswordHash) &&
                !options.SubhdPasswordHash.StartsWith(PasswordHashPrefix, StringComparison.OrdinalIgnoreCase))
            {
                options.SubhdPasswordHash = EncryptPassword(options.SubhdPasswordHash);
            }
        }

        private string EncryptPassword(string password)
        {
            return PasswordHashPrefix + _encryption.EncryptString(password);
        }

        private string DecryptPassword(string password)
        {
            if (password == null ||
                !password.StartsWith(PasswordHashPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            return _encryption.DecryptString(password.Substring(2));
        }

        public string Name
        {
            get { return NAME; }
        }

        private CnSubOptions GetOptions()
        {
            return _config.GetCnSubConfiguration();
        }

        public IEnumerable<VideoContentType> SupportedMediaTypes
        {
            get
            {
                return new[] { VideoContentType.Episode, VideoContentType.Movie };
            }
        }

        private string NormalizeLanguage(string language)
        {
            if (language != null)
            {
                var culture = _localizationManager.FindLanguageInfo(language.AsSpan());
                if (culture != null)
                {
                    return culture.ThreeLetterISOLanguageName;
                }
            }

            return language;
        }

        public async Task<IEnumerable<RemoteSubtitleInfo>> Search(SubtitleSearchRequest request, CancellationToken cancellationToken)
        {
            if (request.IsForced == true)
            {
                return Array.Empty<RemoteSubtitleInfo>();
            }
            else if (request.ContentType.Equals(VideoContentType.Episode) || request.ContentType.Equals(VideoContentType.Movie))
            {
                List<RemoteSubtitleInfo> result = new List<RemoteSubtitleInfo>();
                foreach (BaseSubProvider provider in providers)
                {
                    var infos = await provider.Search(request, cancellationToken).ConfigureAwait(false);
                    if (infos != null && infos.Count() > 0)
                    {
                        result.AddRange(infos);
                    }
                }
                return result;
            }
            else
            {
                return Array.Empty<RemoteSubtitleInfo>();
            }
        }

        public async Task<SubtitleResponse> GetSubtitles(string id, CancellationToken cancellationToken)
        {
            foreach (BaseSubProvider provider in providers)
            {
                var task = provider.GetSubtitles(id, cancellationToken);
                if (task != null)
                {
                    return await task.ConfigureAwait(false);
                }
            }
            return new SubtitleResponse();
        }

        public void Dispose()
        {
            _config.NamedConfigurationUpdating -= _config_NamedConfigurationUpdating;
        }
    }
}
