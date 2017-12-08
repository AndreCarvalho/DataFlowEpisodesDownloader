using System.Linq;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace RtpDownloader
{
    public static class EpisodeScrapeHelper
    {
        public static Episode ExtractEpisodeDataFromEpisodePage(string html, Regex regex)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var match = regex.Match(html).Groups[1].Value;
            var firstPart = match.Split('/')[0];
            var secondPart = match.Split('=').Last();

            var title = doc.DocumentNode.Descendants("title").Single().InnerText;
            title = title.Split('&').First().Trim();

            var episode = new Episode(
                $"http://cdn-ondemand.rtp.pt/nas2.share/mcm/mp4/{firstPart}/{secondPart}.mp4",
                title);
            return episode;
        }
    }
}