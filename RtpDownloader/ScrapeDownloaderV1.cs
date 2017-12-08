using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using HtmlAgilityPack;
#pragma warning disable CS4014

namespace RtpDownloader
{
    /// <summary>
    ///     1st implementation of the scrape/downloader (with basic data flow usage for consumer-producer)
    /// </summary>
    public class ScrapeDownloaderV1
    {
        public async Task Work()
        {
            const string saveDirectory = @"D:\Jardim da celeste\";

            var downloader = new HttpDownloaderWithLimitsDecorator(new HttpDownloader(new HttpClient()));

            var searchPagesUrls = Enumerable.Range(1, 12) // TODO: add another block to figure out how many result pages there are
                .Select(pageNumber => $"https://arquivos.rtp.pt/page/{pageNumber}/?advanced=1&s=celeste");

            var episodesUrls = new BufferBlock<string>();
            var videoFileUrl = new BufferBlock<Episode>();

            ProduceLinksToIndividualEpisodes(searchPagesUrls, episodesUrls, downloader);

            await Task.WhenAll(
                ExtractEpisodeDataFromEpisodePage(episodesUrls, videoFileUrl, downloader),
                DownloadEpisodes(videoFileUrl, saveDirectory, downloader)).ConfigureAwait(false);
        }

        private async Task ProduceLinksToIndividualEpisodes(IEnumerable<string> searchPagesUrls,
            ITargetBlock<string> episodesUrls, IHttpDownloader downloader)
        {
            var searchPageDownloadTasks = new List<Task>();

            foreach (var searchPageUrl in searchPagesUrls)
                searchPageDownloadTasks.Add(Task.Run(async () =>
                {
                    Console.WriteLine($"Parsing {searchPageUrl}");

                    var html = await downloader.DownloadHtmlPageAsync(searchPageUrl).ConfigureAwait(false);
                    var document = new HtmlDocument();
                    document.LoadHtml(html);

                    var allArticles = document.DocumentNode.Descendants("article");
                    var links = allArticles.SelectMany(x => x.Descendants("a"))
                        .Where(x => x.Attributes["title"].Value.Contains("Jardim da Celeste"))
                        .Select(x => x.Attributes["href"].Value);

                    foreach (var link in links)
                        episodesUrls.Post(link);
                }));

            await Task.WhenAll(searchPageDownloadTasks).ConfigureAwait(false);

            episodesUrls.Complete();
        }

        private async Task ExtractEpisodeDataFromEpisodePage(IReceivableSourceBlock<string> episodesUrls,
            ITargetBlock<Episode> videoFileUrlTargetBlock, IHttpDownloader downloader)
        {
            var tasks = new List<Task>();

            var regex = new Regex("/mp4/(.+?).mp4.");

            while (await episodesUrls.OutputAvailableAsync().ConfigureAwait(false))
            while (episodesUrls.TryReceive(out var url))
            {
                var url1 = url;
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var html = await downloader.DownloadHtmlPageAsync(url1).ConfigureAwait(false);
                        var episode = EpisodeScrapeHelper.ExtractEpisodeDataFromEpisodePage(html, regex);
                        videoFileUrlTargetBlock.Post(episode);

                        Console.WriteLine($"Added episode: {episode.Name}");
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Error processing url {url1}");
                    }
                }));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);

            videoFileUrlTargetBlock.Complete();
        }

        

        private async Task DownloadEpisodes(IReceivableSourceBlock<Episode> episodes, string outputDirectory,
            IHttpDownloader downloader)
        {
            var tasks = new List<Task>();

            while (await episodes.OutputAvailableAsync().ConfigureAwait(false))
            while (episodes.TryReceive(out var episode))
            {
                var episode1 = episode;
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var data = await downloader.DownloadFileAsync(episode1.FileUrl).ConfigureAwait(false);
                        var filePath = Path.Combine(outputDirectory, episode1.Name + ".mp4");
                        File.WriteAllBytes(filePath, data);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Error downloading {episode1.FileUrl} {e.Message}");
                    }
                }));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
    }
}