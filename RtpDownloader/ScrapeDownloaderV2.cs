using System;
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
    ///     2nd implementation of the scrape/downloader with data flow full leverage
    /// </summary>
    public class ScrapeDownloaderV2
    {
        public async Task Work()
        {
            const string saveDirectory = @"D:\Jardim da celeste\";

            var downloader = new HttpDownloader(new HttpClient());

            var searchPagesUrls = Enumerable.Range(1, 12)// TODO: add another block to figure out how many result pages there are
                .Select(pageNumber => $"https://arquivos.rtp.pt/page/{pageNumber}/?advanced=1&s=celeste");

            //////////////////////////////////////////
            // 1st we configure the individual blocks
            //////////////////////////////////////////

            // each search list page may contain zero or more episodes links
            var scrapeEpisodeSearchPageForIndividualEpisodePage = new TransformManyBlock<string, string>(async url =>
            {
                Console.WriteLine($"Downloading page {url}");

                var html = await downloader.DownloadHtmlPageAsync(url).ConfigureAwait(false);
                var document = new HtmlDocument();
                document.LoadHtml(html);

                var allArticles = document.DocumentNode.Descendants("article");
                var links = allArticles.SelectMany(x => x.Descendants("a"))
                    .Where(x => x.Attributes["title"].Value.Contains("Jardim da Celeste"))
                    .Select(x => x.Attributes["href"].Value);

                return links;
            }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 5 });
            
            // this block downloads the episode page and looks for the video url
            var scrapeIndividualEpisodePageForEpisodeData = new TransformBlock<string, Episode>(async url =>
            {
                var regex = new Regex("/mp4/(.+?).mp4.");

                Console.WriteLine($"Downloading page {url}");
                var html = await downloader.DownloadHtmlPageAsync(url).ConfigureAwait(false);
                return EpisodeScrapeHelper.ExtractEpisodeDataFromEpisodePage(html, regex);
            }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 5 });

            var downloadEpisode = new ActionBlock<Episode>(async episode =>
            {
                try
                {
                    Console.WriteLine($"Downloading file {episode.FileUrl}");
                    var data = await downloader.DownloadFileAsync(episode.FileUrl).ConfigureAwait(false);
                    var filePath = Path.Combine(saveDirectory, episode.Name + ".mp4");
                    File.WriteAllBytes(filePath, data);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error downloading {episode.FileUrl} {e.Message}");
                }
            }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 4 });

            //////////////////////////////////////////
            // 2nd we hook the blocks by linking them 
            //////////////////////////////////////////
            
            scrapeEpisodeSearchPageForIndividualEpisodePage.LinkTo(scrapeIndividualEpisodePageForEpisodeData);
            scrapeIndividualEpisodePageForEpisodeData.LinkTo(downloadEpisode);

            scrapeEpisodeSearchPageForIndividualEpisodePage.Completion.ContinueWith(t =>
            {
                if (t.IsFaulted) ((IDataflowBlock)scrapeIndividualEpisodePageForEpisodeData).Fault(t.Exception);
                scrapeIndividualEpisodePageForEpisodeData.Complete();
            });

            scrapeIndividualEpisodePageForEpisodeData.Completion.ContinueWith(t =>
            {
                if (t.IsFaulted) ((IDataflowBlock)downloadEpisode).Fault(t.Exception);
                downloadEpisode.Complete();
            });


            //////////////////////////////////////////
            // 3rd we feed the starting block with data
            //////////////////////////////////////////

            foreach (var searchPageUrl in searchPagesUrls)
            {
                await scrapeEpisodeSearchPageForIndividualEpisodePage.SendAsync(searchPageUrl).ConfigureAwait(false);
            }

            scrapeEpisodeSearchPageForIndividualEpisodePage.Complete();

            //////////////////////////////////////////
            // Finally we wait for the last block to complete
            //////////////////////////////////////////

            await downloadEpisode.Completion.ConfigureAwait(false);
        }
    }
}