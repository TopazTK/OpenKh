#include <map>
#include <string>
#include <iostream>
#include <filesystem>
#include <Windows.h>

using namespace std;

static map<string, string> MAP_NAMES =
{
    {"kh1", "KINGDOM HEARTS FINAL MIX.exe" },
    {"kh2", "KINGDOM HEARTS II FINAL MIX.exe" },
    {"recom", "KINGDOM HEARTS Re_Chain of Memories.exe" },
    {"bbs", "KINGDOM HEARTS Birth by Sleep FINAL MIX.exe" },
    {"ddd", "KINGDOM HEARTS Dream Drop Distance.exe" },
};

static string MAIN_LAUNCH = "KINGDOM HEARTS HD 1.5+2.5 Launcher.exe";

int main(int argc, char* argv[])
{
    wchar_t _fetchPath[MAX_PATH];

    auto _fetchLength = GetModuleFileNameW(nullptr, _fetchPath, MAX_PATH);
    auto _fetchPathFS = filesystem::path(_fetchPath).parent_path();

    if (argc == 0x01)
    {
        auto _makeCommand = "\"" + _fetchPathFS.string() + "\\" + MAIN_LAUNCH + "\"";
        system(_makeCommand.c_str());
    }

    else
    {
        if (argc == 0x02)
        {
            auto _makeCommand = "\"" + _fetchPathFS.string() + "\\" + (MAP_NAMES.contains(argv[1]) ? MAP_NAMES[argv[1]] : MAIN_LAUNCH + " " + argv[1]) + "\"";
            system(_makeCommand.c_str());
        }

        else
        {
            auto _containsKey = MAP_NAMES.contains(argv[1]);
            auto _makeCommand = "\"" + _fetchPathFS.string() + "\\" + (_containsKey ? MAP_NAMES[argv[1]] : MAIN_LAUNCH) + "\"";

            for (int i = _containsKey ? 2 : 1; i < argc; i++)
            {
                _makeCommand += " ";
                _makeCommand += argv[i];
            }

            system(_makeCommand.c_str());
        }
    }

    return 0;
}
