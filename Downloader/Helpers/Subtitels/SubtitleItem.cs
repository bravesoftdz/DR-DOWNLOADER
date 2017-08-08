﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Downloader.Helpers.Subtitels
{
    public class SubtitleItem
    {
        public int StartTime { get; set; }
        public int EndTime { get; set; }
        public List<string> Lines { get; set; }

        public SubtitleItem() => Lines = new List<string>();
        public bool IsValid() => StartTime * EndTime >= 0 && Lines.Any();
        public override string ToString()
        {
            var startTs = new TimeSpan(0, 0, 0, 0, StartTime);
            var endTs = new TimeSpan(0, 0, 0, 0, EndTime);
            return $"{startTs:G} --> {endTs:G}{Environment.NewLine}{string.Join(Environment.NewLine, Lines)}{Environment.NewLine}";
        }
    }
}