using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenKh.Tools.ModManager.Models
{
    public class ModModel
    {
        public string? ModTitle { get; set; }
        public string? ModAuthor { get; set; }
        public string? ModDescription { get; set; }
        public string? ModFilesList { get; set; }
        public string? ModIconSource { get; set; }
        public string? ModYamlPath { get; set; }
        public bool? ModActive { get; set; }

        public Uri? ModSource { get; set; }
        public Uri? ModIssues { get; set; }

    }
}
