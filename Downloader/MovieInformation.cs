﻿using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using Downloader.Helpers;
using Downloader.JSON_Objects;


using HtmlAgilityPack;
using Newtonsoft.Json;
using static Downloader.Helpers.TaskList;
// ReSharper disable RedundantStringInterpolation
namespace Downloader
{
    internal class MovieInformation
    {
        #region Fields

        private readonly M3U8File streamInformations;

        #endregion

        #region Properties

        public string Description { get; }

        public string FileName { get; set; }

        public List<string> Formats { get; }

        private string ImageFilename { get; }

        public string ProductionCountry { get; }

        public int ProductionYear { get; }

        public string SubTitle { get; }

        public string Title { get; }

        public MemoryStream TitleImage { get; }

        public string Url { get; }

        private bool Valid { get; }

        #endregion

        #region Constructors
        public MovieInformation(string url)
        {
            Valid = false;

            if (string.IsNullOrEmpty(url))
                return;

            var htmlDocument = new HtmlWeb().Load(url);
            var broadcastInformation =
            (
                from script in htmlDocument.DocumentNode.Descendants("script")
                select script.InnerText into s
                where s.Contains("window.DR")
                select s.Replace("window.DR = {", "{").Replace("};", "}") into json
                select JsonConvert.DeserializeObject<BroadcastInformation>(json)
            ).FirstOrDefault();

            var programCard = broadcastInformation?.Tv.ProgramCard;

            if (programCard == null)
                return;

            var primaryBroadcast = programCard.PrimaryBroadcast;

            Title = programCard.Title;
            Url = programCard.PrimaryAsset?.Uri;

            if (string.IsNullOrEmpty(Url))
                return;

            Description = programCard.Description;
            ProductionYear = primaryBroadcast?.ProductionYear ?? 0;
            ProductionCountry = primaryBroadcast?.ProductionCountry;

            var baseFileName = RemoveInvalidChars(Title);
            using (var webClient = new WebClient())
            {
                TitleImage = new MemoryStream(webClient.DownloadData(programCard.PrimaryImageUri))
                {
                    Position = 0
                };

                ImageFilename = baseFileName + ".jpg";
                FileName = baseFileName + ".ts";

                var m3U8Object = JsonConvert.DeserializeObject<M3u8Object>(webClient.DownloadString(Url));
                url = m3U8Object.Links?.FirstOrDefault(x => x.Uri.Contains("m3u8"))?.Uri;
                SubTitle = m3U8Object.SubtitlesList?.FirstOrDefault(x => !string.IsNullOrEmpty(x.Language))?.Language;

                if (url == null)
                    return;

                var tmp = webClient.DownloadString(url);

                if (string.IsNullOrEmpty(tmp))
                    return;

                streamInformations = new M3U8File(tmp);
                Formats = new List<string>(streamInformations.Streams.GroupBy(s => s.Resolution).Select(s => s.Key));
            }

            Valid = true;
        }

        #endregion

        #region Members

        private static string RemoveInvalidChars(string userInput) => Path.GetInvalidFileNameChars().Aggregate(userInput, (current, nameChar) => current.Replace(nameChar, ' '));

        public void Save(string destination)
        {
            if (Valid)
                Save(Formats.Last(), destination, false);
        }

        private void SaveNfoFile(string destination)
        {

            var strs = new List<string>
                       {
                           $"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\" ?>",
                           $"<movie>",
                           $"    <title>{Title}</title>",
                           $"    <originaltitle>{Title}</originaltitle>",
                           $"    <year>{ProductionYear}</year>",
                           $"    <outline>{SubTitle}</outline>",
                           $"    <plot>{Description}</plot>",
                           $"    <thumb aspect=\"poster\" preview=\"{ImageFilename}\">{ImageFilename}</thumb>",
                           $"  <fanart>",
                           $"    <thumb preview=\"{ImageFilename}\">{ImageFilename}</thumb>",
                           $"  </fanart>",
                           $"</movie>"
                       };

            destination = string.Concat(destination, RemoveInvalidChars(Title), ".nfo");
            File.WriteAllLines(destination, strs);
        }

        #endregion

        #region Interface Implementations

        public void Save(string resolution, string destination, bool createDirectory)
        {
            if (!destination.EndsWith("\\"))
                destination += "\\";

            if (createDirectory)
            {
                destination = string.Concat(destination, RemoveInvalidChars(Title), "\\");
                Directory.CreateDirectory(destination);
            }

            SaveNfoFile(destination);
            var streamInformation = streamInformations.InformationFromResolution(resolution);
            var str = string.Concat(destination, FileName);
            var processStartInfo = new ProcessStartInfo(FFMPEG_FILENAME, $" -i \"{streamInformation.Uri}\" -hide_banner -v quiet -stats -c copy \"{str}\"")
            {
                WindowStyle = ProcessWindowStyle.Hidden
            };

            str = string.Concat(destination, ImageFilename);

            File.Delete(str);

            using (var fileStream = new FileStream(str, FileMode.CreateNew))
            {
                TitleImage.WriteTo(fileStream);
                fileStream.Close();
            }

            Process.Start(processStartInfo)?.WaitForExit();
        }
        #endregion
    }
}





