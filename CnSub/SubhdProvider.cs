using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

namespace CnSub
{
    public class SubhdProvider : BaseSubProvider
    {
        private readonly String _siteUrl = "http://subhd.la";
        private readonly String _searchUrl = "http://subhd.la/search/";

        public SubhdProvider(ILogger logger, IHttpClient httpClient, IServerConfigurationManager config, IEncryptionManager encryption, IJsonSerializer json, IFileSystem fileSystem, ILocalizationManager localizationManager)
            : base(logger, httpClient, config, encryption, json, fileSystem, localizationManager)
        {
        }

        protected async override Task<IEnumerable<RemoteSubtitleInfo>> OnSearch(SubtitleSearchRequest request, CancellationToken cancellationToken)
        {
            _logger.Info($"SubhdProvider, OnSearch, name: {request.Name}, lang: {request.Language}, path: {request.MediaPath}, year: {request.ProductionYear}");

            string keyword = request.Name;

            using var res = await _httpClient.GetResponse(GetOptions($"{_searchUrl}{keyword}", cancellationToken)).ConfigureAwait(false);

            var docNode = (await GetResponseDoc(res.Content).ConfigureAwait(false)).DocumentNode;
            var smallNode = docNode.SelectSingleNode("//small");
            if (smallNode == null) return Array.Empty<RemoteSubtitleInfo>();

            if (smallNode.InnerText.Contains("总共 0 条") == true)
            {
                return Array.Empty<RemoteSubtitleInfo>();
            }
            else
            {
                List<RemoteSubtitleInfo> result = new List<RemoteSubtitleInfo>();

                HtmlNodeCollection nodes = docNode.SelectNodes("//div[@class='mb-4 bg-white rounded shadow-sm']");

                foreach (HtmlNode itemNode in nodes)
                {
                    #region check movie tag
                    //if (request.ContentType == VideoContentType.Movie)
                    //{
                    //    var movieTag = itemNode.SelectSingleNode(".//div[@class='px-1 rounded-sm bg-danger text-white']");
                    //    if (movieTag == null)
                    //    {
                    //        continue;
                    //    }
                    //}
                    #endregion

                    var itemText = itemNode.InnerText.Trim();

                    // language
                    var lang = NormailizeLang(request.Language);
                    if (MatchesLang(lang, itemText) <= 0) continue;

                    // title and href
                    var a = itemNode.SelectSingleNode(".//div[@class='f12 pt-1']/a");
                    var subUrl = a?.GetAttributeValue("href", "");
                    var subName = a?.InnerText?.Trim();
                    if (a == null || subUrl == null || subUrl.Length == 0 || subName == null || subName.Length == 0)
                    {
                        continue;
                    }

                    // download count
                    int? downloadCount = null;
                    var downloadStr = itemNode.SelectSingleNode(".//i[contains(@class,'fa-download')]")?.NextSibling?.InnerText?.Trim();
                    if (downloadStr != null && downloadStr.Length > 0)
                    {
                        downloadCount = int.Parse(new string(downloadStr.Where(Char.IsDigit).ToArray()));
                    }

                    // author
                    var author = itemNode.SelectSingleNode(".//a[@class='text-dark font-weight-bold']")?.InnerText?.Trim();
                    if (author == null) author = "";

                    // time
                    DateTimeOffset? createTime = null;
                    var time = itemNode.SelectSingleNode(".//i[contains(@class,'fa-clock')]")?.NextSibling?.InnerText?.Trim();
                    if (time != null)
                    {
                        if (time.Contains("天前"))
                        {
                            int days = -int.Parse(new string(time.Where(char.IsDigit).ToArray()));
                            createTime = new DateTimeOffset(DateTime.Now.AddDays(days));
                        }
                        else if (time.Contains("分钟前") || time.Contains("小时前"))
                        {
                            createTime = new DateTimeOffset(DateTime.Now);
                        }
                        else if (time.Contains("月") && time.Contains("日"))
                        {
                            var splits = time.Split(new string[] { "月", "日" }, StringSplitOptions.RemoveEmptyEntries);
                            createTime = new DateTimeOffset(DateTime.Now.Year, int.Parse(splits[0]), int.Parse(splits[1]), 0, 0, 0, new TimeSpan());
                        }
                        else
                        {
                            var splits = time.Split('.');
                            if (splits.Count() == 3)
                            {
                                try
                                {
                                    createTime = new DateTimeOffset(int.Parse(splits[0]), int.Parse(splits[1]), int.Parse(splits[2]), 0, 0, 0, new TimeSpan());
                                }
                                catch (Exception)
                                {
                                    createTime = null;
                                }
                            }
                        }
                    }

                    result.Add(new RemoteSubtitleInfo
                    {
                        Id = $"subhd:{lang}:{Base64Encode(subUrl)}:{Base64Encode(request.MediaPath)}",
                        Name = $"[SUBHD] {subName} - {author} - {time}",
                        ThreeLetterISOLanguageName = lang,
                        Format = ExtractFormat(itemText),
                        DownloadCount = downloadCount,
                        Author = author,
                        DateCreated = createTime,
                        ProviderName = CnSubProvider.NAME
                    });
                }

                result.Sort(CompareSubInfo);

                _logger?.Info($"SubhdProvider, result count: {result.Count()}");
                int idx = 0;
                result.ForEach((i) =>
                {
                    _logger?.Info($"SubhdProvider, \t{idx++}, \tid: {i.Id}, name: {i.Name}, lang: {i.ThreeLetterISOLanguageName}, format: {i.Format}, downloadCount: {i.DownloadCount}, author: {i.Author}, time: {i.DateCreated}");
                });

                return result;
            }
        }

        protected override bool HandlesSubtitle(string id)
        {
            return id.Contains("subhd");
        }

        protected async override Task<SubtitleResponse> OnGetSubtitles(string id, CancellationToken cancellationToken)
        {
            _logger?.Info($"SubhdProvider, OnGetSubtitles id: {id}");

            var idParts = id.Split(new[] { ':' }, 4);
            var language = NormailizeLang(idParts[1]);
            var url = Base64Decode(idParts[2]);
            var mediaPath = Base64Decode(idParts[3]);

            _logger?.Info($"SubhdProvider, OnGetSubtitles id: {id}, url: {url}, mediaPath: {mediaPath}, lang: {language}");

            using var res = await _httpClient.GetResponse(GetOptions($"{_siteUrl}{url}", cancellationToken)).ConfigureAwait(false);

            var docNode = (await GetResponseDoc(res.Content).ConfigureAwait(false)).DocumentNode;
            var dtoken = docNode.SelectSingleNode("//@dtoken")?.GetAttributeValue("dtoken", null);
            var sid = docNode.SelectSingleNode("//@dtoken")?.GetAttributeValue("sid", null);
            if (sid == null)
            {
                sid = url.Substring(url.LastIndexOf("/"));
            }
            _logger?.Info($"SubhdProvider, \t sub_id: {sid}, dtoken: {dtoken}");
            if (dtoken == null || dtoken.Length == 0 || sid == null || sid.Length == 0)
            {
                return new SubtitleResponse();
            }

            // {"success":true,"url":"http:\/\/dl.subhd.la\/shooter\/21\/[\u9633\u5149\u5c0f\u7f8e\u5973].Little.Miss.Sunshine.HDTV.RE.a1080.X264.DD51.F@Silu.rar"}
            var options = GetOptions($"{_siteUrl}/ajax/down_ajax", cancellationToken);
            options.SetPostData(EscapeDictionary(new Dictionary<string, string>
            {
                { "dtoken", dtoken },
                { "sub_id", sid}
            }));

            using var ajaxRes = await _httpClient.Post(options).ConfigureAwait(false);
            var ajaxContent = await GetResponseContent(ajaxRes.Content);

            var ajaxJson = _json.DeserializeFromString<Dictionary<string, Object>>(ajaxContent);
            var fileUrl = ajaxJson["url"]?.ToString();
            _logger?.Info($"SubhdProvider, \t ajaxContent: {ajaxContent}, fileUrl: {fileUrl}, success: {ajaxJson["success"]}");

            if ((true.Equals(ajaxJson["success"]) || "true".Equals(ajaxJson["success"])) && fileUrl != null && fileUrl.Length > 0)
            {
                using var stream = await _httpClient.GetResponse(GetOptions(fileUrl, cancellationToken)).ConfigureAwait(false);
                var fileMs = new MemoryStream();
                await stream.Content.CopyToAsync(fileMs).ConfigureAwait(false);
                fileMs.Position = 0;

                try
                {
                    var subtitleEntry = SelectSubtitle(language, mediaPath, fileMs, out string format);

                    if (subtitleEntry != null)
                    {
                        using var subtitleStream = subtitleEntry.OpenEntryStream();
                        var ms = new MemoryStream();
                        await subtitleStream.CopyToAsync(ms).ConfigureAwait(false);
                        ms.Position = 0;

                        _logger?.Info($"SubhdProvider, \t result, lang: {language}, format: {format}");
                        return new SubtitleResponse()
                        {
                            Language = language,
                            Stream = ms,
                            Format = format
                        };
                    }
                }
                catch (Exception e) { _logger?.Error($"SubhdProvider: {e.Message}"); }
            }
            return new SubtitleResponse();
        }

        private HttpRequestOptions GetOptions(string url, CancellationToken cancellationToken)
        {
            return GetOptions(url, null, cancellationToken);
        }

        private HttpRequestOptions GetOptions(string url, Dictionary<string, string> headers, CancellationToken cancellationToken)
        {
            var options = new HttpRequestOptions
            {
                Url = url,
                CancellationToken = cancellationToken,
                Referer = _siteUrl,
                TimeoutMs = 10000,
                AcceptHeader = "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8,application/json,text/javascript,*/*;q=0.01",
                EnableHttpCompression = false,
                UserAgent = "Mozilla/5.0 (Windows NT 10.0)"
            };
            //options.RequestHeaders.Add("Accept-Language", "en-US,en;q=0.9,zh-CN;q=0.8,zh;q=0.7,zh-TW;q=0.6,ja;q=0.5");
            if (headers != null)
            {
                foreach (KeyValuePair<string, string> header in headers)
                {
                    options.RequestHeaders.Add(header.Key, header.Value);
                }
            }
            return options;
        }
    }
}
