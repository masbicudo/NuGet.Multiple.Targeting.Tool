using System;
using System.IO;

namespace Masb.NuGet.Multiple.Targeting.Tool
{
    public class AppDataBlobStorageManager : FileSystemBlobStorageManager
    {
        public AppDataBlobStorageManager()
            : base(Path.Combine(Environment.CurrentDirectory, "AppData"))
        {
        }
    }
}