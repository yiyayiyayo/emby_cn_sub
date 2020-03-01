using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CnSub;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Security;
using MediaBrowser.Controller.Subtitles;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;

namespace CNSubtitles
{
    class Program
    {
        static void Main(string[] args)
        {
            ISubtitleProvider downloader = new CnSubProvider(new Logger(), new HttpClientImpl(), null, new EncryptionManager(), new JsonSerializer(), null, new LocalizationManager());

            //SearchMovie, request: ContentType= Movie, DisabledSubtitleFetchers= 0: , IndexNumber= , IndexNumberEnd= , IsForced= , IsPerfectMatch= False, Language= eng,
            // MediaPath= D:\Video\Movie\Knives.Out.2019.720p.BluRay.x264.DD5.1-HDChina\Knives.Out.2019.720p.BluRay.x264.DD5.1-HDChina.mkv, Name= Knives Out, ParentIndexNumber= ,
            // ProductionYear= 2019, Tmdb=546554;Imdb=tt8946378, RuntimeTicks= 78138900000, SearchAllProviders= True, SeriesName= , SubtitleFetcherOrder= 0: , TwoLetterISOLanguageName= en

            //SubtitleSearchRequest request = new SubtitleSearchRequest
            //{
            //    ContentType = VideoContentType.Movie,
            //    DisabledSubtitleFetchers = new string[0],
            //    IsPerfectMatch = false,
            //    Language = "eng", // chi
            //    MediaPath = "D:\\Video\\Movie\\Knives.Out.2019.720p.BluRay.x264.DD5.1-HDChina\\Knives.Out.2019.720p.BluRay.x264.DD5.1-HDChina.mkv",
            //    Name = "Knives Out",
            //    ProductionYear = 2019,
            //    ProviderIds = new Dictionary<string, string>()
            //};
            //request.ProviderIds.Add("Tmdb", "546554");
            //request.ProviderIds.Add("Imdb", "tt8946378");
            //request.RuntimeTicks = 78138900000;
            //request.SearchAllProviders = true;
            //request.TwoLetterISOLanguageName = "en"; // zh-CN
            //Task<IEnumerable<RemoteSubtitleInfo>> task = downloader.Search(request, new CancellationToken());
            //task.Wait();

            //IEnumerable<RemoteSubtitleInfo> infos = task.Result;
            ////infos.GetEnumerator().MoveNext
            //foreach (RemoteSubtitleInfo info in infos)
            //{
            //    Console.WriteLine($"info: {info}");
            //}

            var tasksub = downloader.GetSubtitles("subhd:chi:L2EvMzEwNjQz:RDpcVmlkZW9cTW92aWVcTW91bGluLlJvdWdlLjIwMDEuMTA4MHAuQmx1UmF5LngyNjQtTUVMaVRFXE1vdWxpbi5Sb3VnZS4yMDAxLjEwODBwLkJsdVJheS54MjY0LU1FTGlURS5ta3Y=", new CancellationToken());
            tasksub.Wait();
            Console.WriteLine(tasksub.Result);
        }
    }

    class Logger : ILogger
    {
        public void Debug(string message, params object[] paramList) { }
        public void Debug(ReadOnlyMemory<char> message) { }
        public void Error(string message, params object[] paramList) { }
        public void Error(ReadOnlyMemory<char> message) { }
        public void ErrorException(string message, Exception exception, params object[] paramList) { }
        public void Fatal(string message, params object[] paramList) { }
        public void FatalException(string message, Exception exception, params object[] paramList) { }
        public void Info(string message, params object[] paramList) { }
        public void Info(ReadOnlyMemory<char> message) { }
        public void Log(LogSeverity severity, string message, params object[] paramList) { }
        public void Log(LogSeverity severity, ReadOnlyMemory<char> message) { }
        public void LogMultiline(string message, LogSeverity severity, StringBuilder additionalContent) { }
        public void Warn(string message, params object[] paramList) { }
        public void Warn(ReadOnlyMemory<char> message) { }
    }
    class HttpClientImpl : IHttpClient
    {
        public Task<Stream> Get(HttpRequestOptions options) { return null; }
        public IDisposable GetConnectionContext(HttpRequestOptions options) { return null; }
        public Task<HttpResponseInfo> GetResponse(HttpRequestOptions options)
        {
            return Task.Factory.StartNew<HttpResponseInfo>(() => { return Execute("GET", options); });
        }

        public Task<string> GetTempFile(HttpRequestOptions options) { return null; }
        public Task<HttpResponseInfo> GetTempFileResponse(HttpRequestOptions options) { return null; }
        public Task<HttpResponseInfo> Post(HttpRequestOptions options)
        {
            return Task.Factory.StartNew<HttpResponseInfo>(() => { return Execute("POST", options); });
        }

        public Task<HttpResponseInfo> SendAsync(HttpRequestOptions options, string httpMethod) { return null; }

        private HttpResponseInfo Execute(String method, HttpRequestOptions options)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(options.Url);
            request.Method = method;
            request.ContentType = options.RequestContentType;
            request.UserAgent = options.UserAgent;
            request.Timeout = options.TimeoutMs;
            request.Accept = options.AcceptHeader;
            request.Referer = options.Referer;
            if (method.Equals("POST"))
            {
                try
                {
                    using (var stream = request.GetRequestStream())
                    {
                        byte[] byteArray = Encoding.UTF8.GetBytes(options.RequestContent.ToString());
                        request.ContentLength = byteArray.Length;
                        stream.Write(byteArray, 0, byteArray.Length);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
            //if (options.RequestHeaders != null && options.RequestHeaders.Count > 0)
            //{
            //    foreach (KeyValuePair<string, string> header in options.RequestHeaders)
            //    {
            //        request.Headers.Add(header.Key, header.Value);
            //    }
            //}
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            //Stream myResponseStream = response.GetResponseStream();
            //StreamReader myStreamReader = new StreamReader(myResponseStream, Encoding.GetEncoding("utf-8"));
            //string retString = myStreamReader.ReadToEnd();
            //myStreamReader.Close();
            //myResponseStream.Close();

            HttpResponseInfo responseInfo = new HttpResponseInfo();
            Dictionary<string, string> responseHeaders = new Dictionary<string, string>();
            responseInfo.Headers = responseHeaders;
            for (int i = 0; i < response.Headers.Keys.Count; i++)
            {
                //Console.WriteLine(String.Format("{0,-20}{1}", response.Headers.Keys[i], response.Headers.Get(i)));
                responseHeaders.Add(response.Headers.Keys[i], response.Headers.Get(i));
            }
            responseInfo.Content = response.GetResponseStream();
            return responseInfo;
        }
    }
    //class ConfigManager : IServerConfigurationManager
    //{
    //    public IServerApplicationPaths ApplicationPaths => throw new NotImplementedException();
    //    public ServerConfiguration Configuration => throw new NotImplementedException();
    //    public IApplicationPaths CommonApplicationPaths => throw new NotImplementedException();
    //    public BaseApplicationConfiguration CommonConfiguration => throw new NotImplementedException();
    //    public event EventHandler<ConfigurationUpdateEventArgs> NamedConfigurationUpdating;
    //    public event EventHandler<EventArgs> ConfigurationUpdated;
    //    public event EventHandler<ConfigurationUpdateEventArgs> NamedConfigurationUpdated;
    //    public void AddParts(IEnumerable<IConfigurationFactory> factories) { }
    //    public object GetConfiguration(string key) { }
    //    public Type GetConfigurationType(string key) { }
    //    public void ReplaceConfiguration(BaseApplicationConfiguration newConfiguration) { }
    //    public void SaveConfiguration() { }
    //    public void SaveConfiguration(string key, object configuration) { }
    //    public bool SetOptimalValues() { }
    //}
    class EncryptionManager : IEncryptionManager
    {
        public string DecryptString(string value) { return value; }
        public string EncryptString(string value) { return value; }
    }
    class JsonSerializer : IJsonSerializer
    {
        public object DeserializeFromBytes(ReadOnlySpan<byte> bytes, Type type) { throw new NotImplementedException(); }
        public T DeserializeFromBytes<T>(ReadOnlySpan<byte> bytes) { throw new NotImplementedException(); }
        public object DeserializeFromFile(Type type, string file) { throw new NotImplementedException(); }
        public T DeserializeFromFile<T>(string file) where T : class { throw new NotImplementedException(); }
        public T DeserializeFromSpan<T>(ReadOnlySpan<char> text) { throw new NotImplementedException(); }
        public object DeserializeFromSpan(ReadOnlySpan<char> json, Type type) { throw new NotImplementedException(); }
        public T DeserializeFromStream<T>(Stream stream) { throw new NotImplementedException(); }
        public object DeserializeFromStream(Stream stream, Type type) { throw new NotImplementedException(); }
        public Task<T> DeserializeFromStreamAsync<T>(Stream stream) { throw new NotImplementedException(); }
        public Task<object> DeserializeFromStreamAsync(Stream stream, Type type) { throw new NotImplementedException(); }
        public T DeserializeFromString<T>(string text) { return System.Text.Json.JsonSerializer.Deserialize<T>(text); }
        public object DeserializeFromString(string json, Type type) { throw new NotImplementedException(); }
        public void SerializeToFile(object obj, string file) { throw new NotImplementedException(); }
        public ReadOnlySpan<char> SerializeToSpan(object obj) { throw new NotImplementedException(); }
        public void SerializeToStream(object obj, Stream stream) { throw new NotImplementedException(); }
        public string SerializeToString(object obj) { throw new NotImplementedException(); }
    }
    //class FileSystem : IFileSystem
    //{
    //    public string DefaultDirectory => throw new NotImplementedException();
    //    public IEnumerable<FileSystemMetadata> CommonFolders => throw new NotImplementedException();
    //    public char DirectorySeparatorChar => throw new NotImplementedException();
    //    public void AddShortcutHandler(IShortcutHandler handler) { }
    //    public bool AreEqual(ReadOnlySpan<char> path1, ReadOnlySpan<char> path2) { }
    //    public bool ContainsSubPath(ReadOnlySpan<char> parentPath, ReadOnlySpan<char> path) { }
    //    public void CopyFile(string source, string target, bool overwrite) { }
    //    public void CreateDirectory(string path) { }
    //    public void CreateShortcut(string shortcutPath, string target) { }
    //    public void DeleteDirectory(string path, bool recursive) { }
    //    public void DeleteDirectory(string path, bool recursive, bool sendToRecycleBin) { }
    //    public void DeleteFile(string path) { }
    //    public void DeleteFile(string path, bool sendToRecycleBin) { }
    //    public bool DirectoryExists(string path) { }
    //    public bool FileExists(string path) { }
    //    public DateTimeOffset GetCreationTimeUtc(FileSystemMetadata info) { }
    //    public DateTimeOffset GetCreationTimeUtc(string path) { }
    //    public IEnumerable<FileSystemMetadata> GetDirectories(string path, bool recursive = false) { }
    //    public FileSystemMetadata GetDirectoryInfo(string path) { }
    //    public string GetDirectoryName(string path) { }
    //    public ReadOnlySpan<char> GetDirectoryName(ReadOnlySpan<char> path) { }
    //    public IEnumerable<string> GetDirectoryPaths(string path, bool recursive = false) { }
    //    public List<FileSystemMetadata> GetDrives() { }
    //    public FileSystemMetadata GetFileInfo(string path) { }
    //    public string GetFileNameWithoutExtension(FileSystemMetadata info) { }
    //    public ReadOnlySpan<char> GetFileNameWithoutExtension(ReadOnlySpan<char> path) { }
    //    public string GetFileNameWithoutExtension(string path) { }
    //    public IEnumerable<string> GetFilePaths(string path, bool recursive = false) { }
    //    public IEnumerable<string> GetFilePaths(string path, string[] extensions, bool enableCaseSensitiveExtensions, bool recursive) { }
    //    public IEnumerable<FileSystemMetadata> GetFiles(string path, bool recursive = false) { }
    //    public IEnumerable<FileSystemMetadata> GetFiles(string path, string[] extensions, bool enableCaseSensitiveExtensions, bool recursive) { }
    //    public Stream GetFileStream(string path, FileOpenMode mode, FileAccessMode access, FileShareMode share, bool isAsync = false) { }
    //    public Stream GetFileStream(string path, FileOpenMode mode, FileAccessMode access, FileShareMode share, FileOpenOptions fileOpenOptions) { }
    //    public IEnumerable<FileSystemMetadata> GetFileSystemEntries(string path, bool recursive = false) { }
    //    public IEnumerable<string> GetFileSystemEntryPaths(string path, bool recursive = false) { }
    //    public FileSystemMetadata GetFileSystemInfo(string path) { }
    //    public string GetFullPath(string path) { }
    //    public DateTimeOffset GetLastWriteTimeUtc(FileSystemMetadata info) { }
    //    public DateTimeOffset GetLastWriteTimeUtc(string path) { }
    //    public DateTimeOffset GetLastWriteTimeUtc(string path, bool fileExists) { }
    //    public string GetValidFilename(string filename) { }
    //    public bool IsPathFile(ReadOnlySpan<char> path) { }
    //    public bool IsRootPath(ReadOnlySpan<char> path) { }
    //    public ReadOnlySpan<char> MakeAbsolutePath(ReadOnlySpan<char> folderPath, ReadOnlySpan<char> filePath) { }
    //    public void MoveDirectory(string source, string target) { }
    //    public void MoveFile(string source, string target) { }
    //    public ReadOnlySpan<char> NormalizePath(ReadOnlySpan<char> path) { }
    //    public Stream OpenRead(string path) { }
    //    public Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default) { }
    //    public Task<string[]> ReadAllLinesAsync(string path, CancellationToken cancellationToken = default) { }
    //    public string ReadAllText(string path) { }
    //    public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default) { }
    //    public Task<string> ReadAllTextAsync(string path, Encoding encoding, CancellationToken cancellationToken = default) { }
    //    public ReadOnlySpan<char> ResolveShortcut(ReadOnlySpan<char> filename) { }
    //    public void SetAttributes(string path, bool isHidden, bool readOnly) { }
    //    public void SetExecutable(string path) { }
    //    public void SetHidden(string path, bool isHidden) { }
    //    public void SetReadOnly(string path, bool readOnly) { }
    //    public void SwapFiles(string file1, string file2) { }
    //    public void WriteAllBytes(string path, byte[] bytes) { }
    //    public void WriteAllLines(string path, IEnumerable<string> lines) { }
    //    public void WriteAllText(string path, string text) { }
    //    public void WriteAllText(string path, string text, Encoding encoding) { }
    //}
    class LocalizationManager : ILocalizationManager
    {
        public CountryInfo FindCountryInfo(ReadOnlySpan<char> country) { return null; }
        public CultureDto FindLanguageInfo(ReadOnlySpan<char> language)
        {
            if (string.Equals("English", language.ToString(), StringComparison.OrdinalIgnoreCase) ||
                string.Equals("En", language.ToString(), StringComparison.OrdinalIgnoreCase) ||
                string.Equals("Eng", language.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return new CultureDto { ThreeLetterISOLanguageNames = new string[] { "eng" } };
            }
            else if (string.Equals("Chinese", language.ToString(), StringComparison.OrdinalIgnoreCase) ||
              string.Equals("Chs", language.ToString(), StringComparison.OrdinalIgnoreCase) ||
              string.Equals("chi", language.ToString(), StringComparison.OrdinalIgnoreCase) ||
              string.Equals("zh", language.ToString(), StringComparison.OrdinalIgnoreCase) ||
              string.Equals("cn", language.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return new CultureDto { ThreeLetterISOLanguageNames = new string[] { "chi" } };
            }
            else
            {
                return new CultureDto { ThreeLetterISOLanguageNames = new string[] { "eng" } };
            }
        }
        public CountryInfo[] GetCountries() { return null; }
        public CultureDto[] GetCultures() { return null; }
        public LocalizatonOption[] GetLocalizationOptions() { return null; }
        public string GetLocalizedString(string phrase, string culture) { return null; }
        public string GetLocalizedString(string phrase) { return null; }
        public ParentalRating[] GetParentalRatings() { return null; }
        public int? GetRatingLevel(ReadOnlySpan<char> rating) { return null; }
        public string RemoveDiacritics(string text) { return null; }
    }
}
