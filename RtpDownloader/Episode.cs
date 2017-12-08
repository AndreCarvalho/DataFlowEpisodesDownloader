namespace RtpDownloader
{
    public class Episode
    {
        public string FileUrl { get; }
        public string Name { get; }

        public Episode(string fileUrl, string name)
        {
            FileUrl = fileUrl;
            Name = name;
        }
    }
}