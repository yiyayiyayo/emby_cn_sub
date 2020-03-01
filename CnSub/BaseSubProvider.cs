using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using HtmlAgilityPack;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Security;
using MediaBrowser.Controller.Subtitles;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Serialization;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace CnSub
{
    public abstract class BaseSubProvider
    {
        public const string CHS = "chi";
        public const string CHT = "cht";
        public const string ENG = "eng";

        public const string ASS = "ass";
        public const string SSA = "ssa";
        public const string SRT = "srt";

        protected readonly ILogger _logger;
        protected readonly IHttpClient _httpClient;
        protected readonly IServerConfigurationManager _config;
        protected readonly IEncryptionManager _encryption;
        protected readonly IJsonSerializer _json;
        protected readonly IFileSystem _fileSystem;
        protected readonly ILocalizationManager _localizationManager;

        public BaseSubProvider(ILogger logger, IHttpClient httpClient, IServerConfigurationManager config, IEncryptionManager encryption, IJsonSerializer json, IFileSystem fileSystem, ILocalizationManager localizationManager)
        {
            _logger = logger;
            _httpClient = httpClient;
            _config = config;
            _encryption = encryption;
            _json = json;
            _fileSystem = fileSystem;
            _localizationManager = localizationManager;
        }

        public async Task<IEnumerable<RemoteSubtitleInfo>> Search(SubtitleSearchRequest request, CancellationToken cancellationToken)
        {
            return await OnSearch(request, cancellationToken);
        }

        protected abstract Task<IEnumerable<RemoteSubtitleInfo>> OnSearch(SubtitleSearchRequest request, CancellationToken cancellationToken);

        public async Task<SubtitleResponse> GetSubtitles(string id, CancellationToken cancellationToken)
        {
            if (HandlesSubtitle(id))
            {
                return await OnGetSubtitles(id, cancellationToken);
            }
            else
            {
                return null;
            }
        }

        protected abstract Boolean HandlesSubtitle(string id);

        protected abstract Task<SubtitleResponse> OnGetSubtitles(string id, CancellationToken cancellationToken);

        protected Dictionary<string, string> EscapeDictionary(Dictionary<string, string> dic)
        {
            return dic.ToDictionary(item => item.Key, item => Uri.EscapeDataString(item.Value));
        }

        protected async Task<HtmlDocument> GetResponseDoc(Stream stream)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(await GetResponseContent(stream).ConfigureAwait(false));
            return doc;
        }

        protected async Task<string> GetResponseContent(Stream stream)
        {
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return HttpUtility.HtmlDecode(await reader.ReadToEndAsync().ConfigureAwait(false));
        }

        protected static int CompareSubInfo(RemoteSubtitleInfo x, RemoteSubtitleInfo y)
        {
            // Download count
            if (x.DownloadCount == null)
            {
                if (y.DownloadCount == null) return 0;
                else return 1;
            }
            else if (y.DownloadCount == null) return -1;
            else return (int)(y.DownloadCount - x.DownloadCount);
        }

        /**
         * -1: not match
         * 0: do not known
         * 1: match and exactly
         * 2: match 
         */
        protected int MatchesLang(string lang, string text)
        {
            _logger?.Info($"MatchesLang, lang: {lang}, text: {text}");
            int result = 0;

            if (text == null) result = -1;
            else
            {
                lang = NormailizeLang(lang);
                text = text.ToLower();

                if (text.Contains("双语") || text.Contains("chs&eng")) result = 2;
                else if (CHS.Equals(lang))
                {
                    if (text.Contains("chs") || text.Contains("zh") || text.Contains("简体") || text.Contains("中文")) result = 1;
                    else if (text.Contains("cht") || text.Contains("繁体") || text.Contains("en") || text.Contains("英文")) result = -1;
                }
                else if (CHT.Equals(lang))
                {
                    if (text.Contains("cht") || text.Contains("繁体")) result = 1;
                    else if (text.Contains("chs") || text.Contains("中文") || text.Contains("简体") || text.Contains("en") || text.Contains("英文")) result = -1;
                }
                else if (ENG.Equals(lang))
                {
                    if (text.Contains("en") || text.Contains("eng") || text.Contains("英文")) result = 1;
                    else if (text.Contains("chs") || text.Contains("中文") || text.Contains("简体") || text.Contains("cht") || text.Contains("繁体")) result = -1;
                }
            }

            _logger?.Info($"MatchesLang, result: {result}");
            return result;
        }

        protected bool MatchesFormat(string format, string text)
        {
            bool result;

            if (format == null || text == null) result = false;
            else if (!SRT.Equals(format) && !ASS.Equals(format) && !SSA.Equals(format)) result = false;
            else result = text.ToLower().Contains(format.ToLower());

            _logger?.Info($"MatchesFormat, lang: {format}, text: {text}, result: {result}");
            return result;
        }

        protected string ExtractFormat(string text)
        {
            string result = null;

            if (text != null)
            {
                text = text.ToLower();
                if (text.Contains(ASS)) result = ASS;
                else if (text.Contains(SSA)) result = SSA;
                else if (text.Contains(SRT)) result = SRT;
                else result = null;
            }

            _logger?.Info($"SubhdProvider, ExtractFormat text: {text}, result: {result}");
            return result;
        }

        protected IArchiveEntry SelectSubtitle(string lang, string mediaPath, Stream stream, out string selectedFormat)
        {
            _logger?.Info($"SubhdProvider, SelectSubtitle lang: {lang}, mediaPath: {mediaPath}");

            selectedFormat = "";
            lang = NormailizeLang(lang);

            IArchiveEntry currentEntry = null;
            int currentScore = 0;
            List<IArchiveEntry> entriesToSave = new List<IArchiveEntry>();

            using var archive = ArchiveFactory.Open(stream);
            try
            {
                foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
                {
                    string key = entry.Key;

                    _logger?.Info($"SubhdProvider, \t process entry: {key}");

                    int score = 0;
                    int matches = MatchesLang(lang, key);
                    var format = ExtractFormat(key);

                    if (ASS.Equals(format) || SSA.Equals(format)) score += 0xF;
                    else if (SRT.Equals(format)) score += 0x8;
                    else continue;

                    entriesToSave.Add(entry);

                    // lang:
                    //  exactly: 0xF0, matches: 0x80, don't known: 0x00
                    // format:
                    //  ass/ssa: 0xF, srt: 0x8 
                    if (matches == 1) score = 0xF0;
                    else if (matches == 2) score = 0x80;
                    else if (matches == 0) score = 0;
                    else continue;

                    _logger?.Info($"SubhdProvider, \t score: {score}");

                    if (score > currentScore)
                    {
                        currentScore = score;
                        currentEntry = entry;
                        selectedFormat = format;
                    }
                }

                TrySaveSubtitles(mediaPath, entriesToSave.Where(entry => entry != currentEntry));
            }
            catch (InvalidFormatException)
            {
                throw;
            }
            catch (IndexOutOfRangeException)
            {
                throw;
            }

            _logger?.Info($"SubhdProvider, SelectSubtitle selectedEntry: {currentEntry.Key}, format: {selectedFormat}");
            return currentEntry;
        }

        protected string NormailizeLang(string lang)
        {
            string result = CHS;

            if (lang != null)
            {
                lang = lang.ToLower();
                if (CHS.Equals(lang) || CHT.Equals(lang) || ENG.Equals(lang))
                {
                    result = lang;
                }
            }

            _logger?.Info($"SubhdProvider, NormailizeLang lang: {lang}, result: {result}");
            return result;
        }

        public static string Base64Encode(string plainText)
        {
            var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            return Convert.ToBase64String(plainTextBytes);
        }

        public static string Base64Decode(string base64EncodedData)
        {
            var base64EncodedBytes = Convert.FromBase64String(base64EncodedData);
            return Encoding.UTF8.GetString(base64EncodedBytes);
        }

        protected bool IsLangPart(string text)
        {
            text = text.ToLower();
            return text.Contains("chs") ||
                text.Contains("cht") ||
                text.Contains("中文") ||
                text.Contains("简体") ||
                text.Contains("繁体") ||
                text.Contains("英文") ||
                (text.Contains("中") && text.Length <= 5) ||
                (text.Contains("简") && text.Length <= 5) ||
                (text.Contains("繁") && text.Length <= 5) ||
                (text.Contains("英") && text.Length <= 5) ||
                (text.Contains("zh") && text.Length <= 5);
        }

        protected void TrySaveSubtitles(string mediaPath, IEnumerable<IArchiveEntry> entries)
        {
            _logger?.Info($"SubhdProvider, TrySaveSubtitles, mediaPath: {mediaPath}");
            if ( entries.Count() > 0)
            {
                try
                {
                    var directory = Path.GetDirectoryName(mediaPath);
                    var mediaName = Path.GetFileNameWithoutExtension(mediaPath);

                    _logger?.Info($"SubhdProvider, TrySaveSubtitles, \t directory: {directory}, mediaName: {mediaName}");
                    foreach (var entry in entries)
                    {
                        // guess media name
                        string postFix;
                        var spilits = entry.Key.Split(new string[] { "." }, StringSplitOptions.RemoveEmptyEntries).ToList();
                        int spilitsCnt = spilits.Count();
                        if (spilitsCnt >= 3 && IsLangPart(spilits[spilitsCnt - 2]))
                        {
                            postFix = String.Join<string>(".", spilits.GetRange(spilitsCnt - 2, 2));
                        }
                        else
                        {
                            postFix = spilits.ElementAt(spilitsCnt - 1);
                        }

                        var target1 = $"{directory}\\{mediaName}";

                        var target = $"{target1}.{postFix}";
                        if (_fileSystem?.FileExists(target) == true)
                        {
                            int idx = 1;
                            do
                            {
                                target = $"{target1}.{idx++}.{postFix}";
                            } while (_fileSystem?.FileExists(target) == true);
                        }

                        _logger?.Info($"SubhdProvider, \t write {entry.Key} to {directory}, path: {target}");
                        entry.WriteToFile(target);
                    }
                }
                catch (Exception) { }
            }
        }

        public class Result
        {
            public string Season { get; set; }
            public string Episode { get; set; }
            public string Title { get; set; }
            public string Language { get; set; }
            public string Version { get; set; }
            public string Completed { get; set; }
            public string HearingImpaired { get; set; }
            public string Corrected { get; set; }
            public string HD { get; set; }
            public string Download { get; set; }
            public string Multi { get; set; }
            public string Id { get; set; }
        }
    }
}