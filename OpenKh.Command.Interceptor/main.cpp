#include <map>
#include <string>
#include <fstream>
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
static string ARGUMENT_FETCH = "launch_args.txt";

int main(int argc, char* argv[])
{
    wchar_t _fetchPath[MAX_PATH];

    auto _fetchLength = GetModuleFileNameW(nullptr, _fetchPath, MAX_PATH);
    auto _fetchPathFS = filesystem::path(_fetchPath).parent_path().string();

    auto _fetchArgsPath = _fetchPathFS + "\\" + ARGUMENT_FETCH;

    if (argc == 0x01)
    {
        auto _makeCommand = "\"" + _fetchPathFS + "\\" + MAIN_LAUNCH + "\"";

        if (filesystem::exists(_fetchArgsPath))
        {
            ifstream _fetchFile(_fetchArgsPath);

            stringstream _fetchBuff;
            _fetchBuff << _fetchFile.rdbuf();

            string _currArg;
            vector<string> _fetchArgs;

            while (getline(_fetchBuff, _currArg, ' '))
            _fetchArgs.push_back(_currArg);
            
            if (_fetchArgs.size() == 0x01)
                _makeCommand = "\"" + _fetchPathFS + "\\" + (MAP_NAMES.contains(_fetchArgs[0]) ? MAP_NAMES[_fetchArgs[0]] : MAIN_LAUNCH) + "\"" + (MAP_NAMES.contains(_fetchArgs[0]) ? ' ' + _fetchArgs[0] : "");

            else if (_fetchArgs.size() >= 0x02)
            {
                _makeCommand = "\"" + _fetchPathFS + "\\" + (MAP_NAMES.contains(_fetchArgs[0]) ? MAP_NAMES[_fetchArgs[0]] : MAIN_LAUNCH) + "\"";

                for (int i = MAP_NAMES.contains(_fetchArgs[0]) ? 1 : 0; i < _fetchArgs.size(); i++)
                {
                    _makeCommand += " ";
                    _makeCommand += _fetchArgs[i];
                }
            }
        }

        system(_makeCommand.c_str());
    }

    else
    {
        if (argc == 0x02)
        {
            auto _makeCommand = "\"" + _fetchPathFS + "\\" + (MAP_NAMES.contains(argv[1]) ? MAP_NAMES[argv[1]] : MAIN_LAUNCH) + "\"" + (MAP_NAMES.contains(argv[1]) ? ' ' + argv[1] : "");
            system(_makeCommand.c_str());
        }

        else
        {
            auto _containsKey = MAP_NAMES.contains(argv[1]);
            auto _makeCommand = "\"" + _fetchPathFS + "\\" + (_containsKey ? MAP_NAMES[argv[1]] : MAIN_LAUNCH) + "\"";

            for (int i = _containsKey ? 2 : 1; i < argc; i++)
            {
                _makeCommand += " ";
                _makeCommand += argv[i];
            }

            system(_makeCommand.c_str());
        }
    }

    if (filesystem::exists(_fetchArgsPath))
        filesystem::remove(_fetchArgsPath);

    return 0;
}
