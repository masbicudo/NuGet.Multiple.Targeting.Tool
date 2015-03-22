using System.Diagnostics;

namespace Masb.NuGet.Multiple.Targeting.Tool.Helpers
{
    public static class PathHelperTests
    {
        public static void GetRelativePathTest()
        {
            Debug.Assert(@"file" == PathHelper.GetRelativePath(@"C:\Dir\", @"C:\Dir\file"));
            Debug.Assert(@"" == PathHelper.GetRelativePath(@"C:\Dir\", @"C:\Dir\"));
            Debug.Assert(@"..\Dir" == PathHelper.GetRelativePath(@"C:\Dir\", @"C:\Dir"));
            Debug.Assert(@"..\" == PathHelper.GetRelativePath(@"C:\Dir\", @"C:\"));
            Debug.Assert(@"..\" == PathHelper.GetRelativePath(@"C:\Dir\", @"C:"));
            Debug.Assert(@"..\OtherDir\file" == PathHelper.GetRelativePath(@"C:\Dir\", @"C:\OtherDir\file"));
            Debug.Assert(@"..\OtherDir\" == PathHelper.GetRelativePath(@"C:\Dir\", @"C:\OtherDir\"));
            Debug.Assert(@"..\OtherDir" == PathHelper.GetRelativePath(@"C:\Dir\", @"C:\OtherDir"));

            Debug.Assert(@"Dir\file" == PathHelper.GetRelativePath(@"C:\Dir", @"C:\Dir\file"));
            Debug.Assert(@"Dir\" == PathHelper.GetRelativePath(@"C:\Dir", @"C:\Dir\"));
            Debug.Assert(@"Dir" == PathHelper.GetRelativePath(@"C:\Dir", @"C:\Dir"));
            Debug.Assert(@"" == PathHelper.GetRelativePath(@"C:\Dir", @"C:\"));
            Debug.Assert(@"" == PathHelper.GetRelativePath(@"C:\Dir", @"C:"));
            Debug.Assert(@"OtherDir\file" == PathHelper.GetRelativePath(@"C:\Dir", @"C:\OtherDir\file"));
            Debug.Assert(@"OtherDir\" == PathHelper.GetRelativePath(@"C:\Dir", @"C:\OtherDir\"));
            Debug.Assert(@"OtherDir" == PathHelper.GetRelativePath(@"C:\Dir", @"C:\OtherDir"));

            Debug.Assert(@"Dir\file" == PathHelper.GetRelativePath(@"C:\", @"C:\Dir\file"));
            Debug.Assert(@"Dir\" == PathHelper.GetRelativePath(@"C:\", @"C:\Dir\"));
            Debug.Assert(@"Dir" == PathHelper.GetRelativePath(@"C:\", @"C:\Dir"));
            Debug.Assert(@"" == PathHelper.GetRelativePath(@"C:\", @"C:\"));
            Debug.Assert(@"" == PathHelper.GetRelativePath(@"C:\", @"C:"));
            Debug.Assert(@"OtherDir\file" == PathHelper.GetRelativePath(@"C:\", @"C:\OtherDir\file"));
            Debug.Assert(@"OtherDir\" == PathHelper.GetRelativePath(@"C:\", @"C:\OtherDir\"));
            Debug.Assert(@"OtherDir" == PathHelper.GetRelativePath(@"C:\", @"C:\OtherDir"));

            Debug.Assert(@"Dir\file" == PathHelper.GetRelativePath(@"C:", @"C:\Dir\file"));
            Debug.Assert(@"Dir\" == PathHelper.GetRelativePath(@"C:", @"C:\Dir\"));
            Debug.Assert(@"Dir" == PathHelper.GetRelativePath(@"C:", @"C:\Dir"));
            Debug.Assert(@"" == PathHelper.GetRelativePath(@"C:", @"C:\"));
            Debug.Assert(@"" == PathHelper.GetRelativePath(@"C:", @"C:"));
            Debug.Assert(@"OtherDir\file" == PathHelper.GetRelativePath(@"C:", @"C:\OtherDir\file"));
            Debug.Assert(@"OtherDir\" == PathHelper.GetRelativePath(@"C:", @"C:\OtherDir\"));
            Debug.Assert(@"OtherDir" == PathHelper.GetRelativePath(@"C:", @"C:\OtherDir"));

            Debug.Assert(@"..\Dir\file" == PathHelper.GetRelativePath(@"C:\OtherDir\", @"C:\Dir\file"));
            Debug.Assert(@"..\Dir\" == PathHelper.GetRelativePath(@"C:\OtherDir\", @"C:\Dir\"));
            Debug.Assert(@"..\Dir" == PathHelper.GetRelativePath(@"C:\OtherDir\", @"C:\Dir"));
            Debug.Assert(@"..\" == PathHelper.GetRelativePath(@"C:\OtherDir\", @"C:\"));
            Debug.Assert(@"..\" == PathHelper.GetRelativePath(@"C:\OtherDir\", @"C:"));
            Debug.Assert(@"file" == PathHelper.GetRelativePath(@"C:\OtherDir\", @"C:\OtherDir\file"));
            Debug.Assert(@"" == PathHelper.GetRelativePath(@"C:\OtherDir\", @"C:\OtherDir\"));
            Debug.Assert(@"..\OtherDir" == PathHelper.GetRelativePath(@"C:\OtherDir\", @"C:\OtherDir"));

            Debug.Assert(@"Dir\file" == PathHelper.GetRelativePath(@"C:\OtherDir", @"C:\Dir\file"));
            Debug.Assert(@"Dir\" == PathHelper.GetRelativePath(@"C:\OtherDir", @"C:\Dir\"));
            Debug.Assert(@"Dir" == PathHelper.GetRelativePath(@"C:\OtherDir", @"C:\Dir"));
            Debug.Assert(@"" == PathHelper.GetRelativePath(@"C:\OtherDir", @"C:\"));
            Debug.Assert(@"" == PathHelper.GetRelativePath(@"C:\OtherDir", @"C:"));
            Debug.Assert(@"OtherDir\file" == PathHelper.GetRelativePath(@"C:\OtherDir", @"C:\OtherDir\file"));
            Debug.Assert(@"OtherDir\" == PathHelper.GetRelativePath(@"C:\OtherDir", @"C:\OtherDir\"));
            Debug.Assert(@"OtherDir" == PathHelper.GetRelativePath(@"C:\OtherDir", @"C:\OtherDir"));
        }
    }
}