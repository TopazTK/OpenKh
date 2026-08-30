using Avalonia.Media.Imaging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace OpenKh.Tools.ModManager.Models
{
    public class ModModel
    {
        public string? ModTitle { get; set; }
        public string? ModAuthor { get; set; }
        public string? ModPlatform { get; set; }
        public string? ModDescription { get; set; }
        public string[]? ModFilesList { get; set; }
        public Bitmap? ModIcon { get; set; }
        public string? ModPath { get; set; }
        public bool ModActive { get; set; }
        public bool ModValid { get; set; }
        public Uri? ModSource { get; set; }
        public Uri? ModIssues { get; set; }
        public int ModBehindBy { get; set; }
    }
}
