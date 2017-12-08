using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace RtpDownloader
{
    public interface IHttpDownloader
    {
        Task<string> DownloadHtmlPageAsync(string url);
        Task<byte[]> DownloadFileAsync(string url);
    }

    public class HttpDownloader : IHttpDownloader
    {
        private readonly HttpClient _httpClient;

        public HttpDownloader(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<string> DownloadHtmlPageAsync(string url)
        {
            var page = await _httpClient.GetAsync(url).ConfigureAwait(false);
            return await page.Content.ReadAsStringAsync().ConfigureAwait(false);
        }

        public async Task<byte[]> DownloadFileAsync(string url)
        {
            return await _httpClient.GetByteArrayAsync(url).ConfigureAwait(false);
        }
    }

    public class HttpDownloaderWithLimitsDecorator : IHttpDownloader
    {
        private const int MaxHtmlPageConcurrentDownloads = 5;
        private const int MaxFileConcurrentDownloads = 2;
        private readonly IHttpDownloader _decorated;
        private readonly SemaphoreSlim _semaphoreSlimFile;
        private readonly SemaphoreSlim _semaphoreSlimHtml;

        public HttpDownloaderWithLimitsDecorator(IHttpDownloader decorated)
        {
            _decorated = decorated;
            _semaphoreSlimHtml = new SemaphoreSlim(MaxHtmlPageConcurrentDownloads);
            _semaphoreSlimFile = new SemaphoreSlim(MaxFileConcurrentDownloads);
        }

        public async Task<string> DownloadHtmlPageAsync(string url)
        {
            try
            {
                await _semaphoreSlimHtml.WaitAsync();
                return await _decorated.DownloadHtmlPageAsync(url).ConfigureAwait(false);
            }
            finally
            {
                _semaphoreSlimHtml.Release();
            }
        }

        public async Task<byte[]> DownloadFileAsync(string url)
        {
            try
            {
                await _semaphoreSlimFile.WaitAsync();
                return await _decorated.DownloadFileAsync(url).ConfigureAwait(false);
            }
            finally
            {
                _semaphoreSlimFile.Release();
            }
        }
    }
}