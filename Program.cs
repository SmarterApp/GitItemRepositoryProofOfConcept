using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;


namespace GitItemRepositoryProofOfConcept
{
    class Program
    {
        const string c_syntax =
@"Syntax:
    To import a content package into a GitLab server
        GitItemRepositoryProofOfConcept <contentpackage.zip> <groupname>
    To estimate the storage required
        GitItemRepositoryProofOfConcept -space <contentpackage.zip>";

        public static string sGitLabUrl = "http://newgitlab.smarterbalanced.org";
        public static string sUserId;
        public static string sPassword;

        static void Main(string[] args)
        {
            try
            {
                if (args.Length != 2 || string.Equals(args[0], "-h", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine(c_syntax);
                    if (Win32Interop.ConsoleHelper.IsSoleConsoleOwner)
                    {
                        Console.WriteLine("Press any key to exit.");
                        Console.ReadKey(true);
                    }
                    return;
                }

                if (string.Equals(args[0], "-space", StringComparison.OrdinalIgnoreCase))
                {
                    LoadCredentials();
                    GitImporter importer = new GitImporter(sGitLabUrl, sUserId, sPassword);
                    importer.CalculateSize(args[1]);
                }
                else
                {
                    LoadCredentials();
                    GitImporter importer = new GitImporter(sGitLabUrl, sUserId, sPassword);
                    importer.ImportContentPackageToGit(args[0], args[1]);
                }
            }
            catch(Exception err)
            {
                Console.WriteLine(err.ToString());
            }

            if (Win32Interop.ConsoleHelper.IsSoleConsoleOwner)
            {
                Console.WriteLine();
                Console.WriteLine("Press any key to exit.");
                Console.ReadKey(true);
            }
        }

        const string cCredentialsFilename = "credentials.txt";
        static void LoadCredentials()
        {
            // Look in the same directory as the application
            string path = AppDomain.CurrentDomain.BaseDirectory;
            path = path.Replace('/', '\\').TrimEnd('\\');
            if (path.EndsWith(@"\bin\debug", StringComparison.OrdinalIgnoreCase)) path = path.Substring(0, path.Length - 10);
            else if (path.EndsWith(@"\bin\release", StringComparison.OrdinalIgnoreCase)) path = path.Substring(0, path.Length - 12);

            using (TextReader reader = new StreamReader(Path.Combine(path, cCredentialsFilename)))
            {
                sUserId = reader.ReadLine();
                sPassword = reader.ReadLine();
            }
        }
    }
}
