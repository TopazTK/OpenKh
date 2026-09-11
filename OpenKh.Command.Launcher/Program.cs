using System.Diagnostics;

namespace OpenKh.Command.Launcher
{
    public partial class Program
    {
        static Dictionary<string, string> KEY_LAUNCH = new Dictionary<string, string>()
        {
            {"kh1", "KINGDOM HEARTS FINAL MIX.exe" },
            {"kh2", "KINGDOM HEARTS II FINAL MIX.exe" },
            {"recom", "KINGDOM HEARTS Re_Chain of Memories.exe" },
            {"bbs", "KINGDOM HEARTS Birth by Sleep FINAL MIX.exe" },
            {"ddd", "KINGDOM HEARTS Dream Drop Distance.exe" },
        };

        static string MAIN_LAUNCH = "KINGDOM HEARTS HD 1.5+2.5 Launcher.exe";

        public static void Main(string[] args)
        {
            var _fetchArguments = KEY_LAUNCH.FirstOrDefault(x => x.Key == args[0]).Key != "" ? String.Join(" ", args.Skip(1)) : String.Join(" ", args);
            var _launchPath = args.Length == 0 || !KEY_LAUNCH.ContainsKey(args[0]) ? Path.Combine(AppContext.BaseDirectory, MAIN_LAUNCH) : Path.Combine(AppContext.BaseDirectory, KEY_LAUNCH[args[0]]);

            Process.Start(_launchPath, _fetchArguments);
        }
    }
}
