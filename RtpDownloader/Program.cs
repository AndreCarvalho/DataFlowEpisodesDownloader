using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using HtmlAgilityPack;
#pragma warning disable 4014

namespace RtpDownloader
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var sw = Stopwatch.StartNew();

            //var worker = new ScrapeDownloaderV1();
            var worker = new ScrapeDownloaderV2();

            await worker.Work();

            sw.Stop();

            Console.WriteLine($"Files downloaded. It took {sw.Elapsed.TotalSeconds} seconds");
            Console.ReadKey();
        }
    }
}
