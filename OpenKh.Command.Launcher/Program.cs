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
            {"bbs", "KINGDOM HEARTS Birth by Sleep FINAL MIX.exe" }
        };

        static string MAIN_LAUNCH = "KINGDOM HEARTS HD 1.5+2.5 Launcher.exe";

        public static void Main(string[] args)
        {
            var _fetchArguments = String.Join(" ", args.Skip(1));

            if (args.Length == 0)
                Process.Start(MAIN_LAUNCH, _fetchArguments);

            else if (!KEY_LAUNCH.ContainsKey(args[0]))
                Process.Start(MAIN_LAUNCH, _fetchArguments);

            else
                Process.Start(KEY_LAUNCH[args[0]], _fetchArguments);
        }
    }
}
