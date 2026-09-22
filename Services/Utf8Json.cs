using System.IO;
using System.Text;

namespace RusakbGate.Services;

public static class Utf8Json
{
    public static void WriteFile(string path, string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}
