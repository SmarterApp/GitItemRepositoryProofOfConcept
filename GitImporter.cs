using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using System.IO.Compression;
using System.Diagnostics;


namespace GitItemRepositoryProofOfConcept
{
    class GitImporter
    {
        const string c_workingFolderName = "GitItemRepositoryProofOfConcept";
        const string c_LogFileName = "PocLog.csv";

        // For expediency, this and the other environment strings are specific to the Git installation
        // on Brandt's computer. They must be fixed for a functional general solution.
        const string c_gitPathAdditions =
@"C:\Users\Brandt\AppData\Local\GitHub\PortableGit_c2ba306e536fdf878271f7fe636a147ff37326ad\cmd;C:\Users\Brandt\AppData\Local\GitHub\PortableGit_c2ba306e536fdf878271f7fe636a147ff37326ad\bin;C:\Users\Brandt\AppData\Local\Apps\2.0\KDELGVRH.A2Y\HQBYN6C9.KHV\gith..tion_317444273a93ac29_0003.0000_3d9282105f72a5d4;C:\Users\Brandt\AppData\Local\GitHub\lfs-amd64_0.5.4;C:\Program Files (x86)\MSBuild\12.0\bin;C:\Users\Brandt\AppData\Local\Apps\2.0\KDELGVRH.A2Y\HQBYN6C9.KHV\gith..tion_317444273a93ac29_0003.0000_3d9282105f72a5d4\NativeBinaries/x86";

        string m_gitLabUrl;
        string m_userId;
        string m_password;
        string m_workingFolder;
        GitLabSession m_session;
        TextWriter m_Log;
        int m_currentItemIndex = 0;

        public GitImporter(string gitLabUrl, string userId, string password)
        {
            m_gitLabUrl = gitLabUrl;
            m_userId = userId;
            m_password = password;
            m_workingFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), c_workingFolderName);
            if (!Directory.Exists(m_workingFolder)) Directory.CreateDirectory(m_workingFolder);
        }

        const Int64 c_blockSize = 4096;

        public void CalculateSize(string filename)
        {
            Int64 totalItems = 0;
            Int64 totalFiles = 0;
            Int64 totalSize = 0;
            Int64 totalSpace = 0;

            using (ZipArchive zip = ZipFile.Open(filename, ZipArchiveMode.Read))
            {
                List<string> itemFiles = GetSortedItemFileList(zip);

                string currentItemId = null;
                foreach (string fullName in itemFiles)
                {
                    string[] parts = fullName.Split('/', '\\');

                    if (!string.Equals(currentItemId, parts[1], StringComparison.OrdinalIgnoreCase))
                    {
                        ++totalItems;
                        currentItemId = parts[1];
                    }

                    ZipArchiveEntry entry = zip.GetEntry(fullName);

                    ++totalFiles;
                    totalSize += entry.Length;
                    totalSpace += ((entry.Length + c_blockSize - 1) / c_blockSize) * c_blockSize;
                }
            }

            Console.WriteLine("Total items: {0:N0}", totalItems);
            Console.WriteLine("Total files: {0:N0}", totalFiles);
            Console.WriteLine("Total size: {0:N0}", totalSize);
            Console.WriteLine("Total space: {0:N0}", totalSpace);
            Console.WriteLine("Space assumes block size of {0:N0}.", c_blockSize);
        }

        public void ImportContentPackageToGit(string filename, string groupName)
        {
            m_session = new GitLabSession(m_gitLabUrl);
            m_session.Login(m_userId, m_password);
            Console.WriteLine("Login successful.");

            string logFileName = Path.Combine(m_workingFolder, c_LogFileName);
            bool logExists = File.Exists(logFileName);
            using (m_Log = new StreamWriter(logFileName, true))
            {
                // Write the log header (if it doesn't already exist)
                if (!logExists)
                {
                    m_Log.WriteLine("Item,Exists,Create,Push,Total");
                    m_Log.Flush();
                }

                using (ZipArchive zip = ZipFile.Open(filename, ZipArchiveMode.Read))
                {
                    List<string> itemFiles = GetSortedItemFileList(zip);

                    string currentItemId = null;
                    string currentFolderName = null;
                    foreach (string fullName in itemFiles)
                    {
                        string[] parts = fullName.Split('/', '\\');

                        // If this file has a new item ID, save the current one and prep to process the new.
                        if (!string.Equals(currentItemId, parts[1], StringComparison.OrdinalIgnoreCase))
                        {
                            // Save the current item
                            if (currentItemId != null)
                            {
                                ImportItemToGit(currentFolderName, groupName, currentItemId);
                                DeleteItemFolder(currentFolderName);
                                currentItemId = null;
                            }

                            // Prep for the next item
                            currentItemId = parts[1];
                            currentFolderName = Path.Combine(m_workingFolder, currentItemId);
                            if (!Directory.Exists(currentFolderName)) Directory.CreateDirectory(currentFolderName);
                        }

                        // Extract the file
                        zip.GetEntry(fullName).ExtractToFile(Path.Combine(currentFolderName, parts[2]), true);
                    }

                    // Finalize last entry
                    if (currentItemId != null)
                    {
                        ImportItemToGit(currentFolderName, groupName, currentItemId);
                        DeleteItemFolder(currentFolderName);
                        currentItemId = null;
                    }
                }
            }
            m_Log = null;
        }

        private List<string> GetSortedItemFileList(ZipArchive zip)
        {
            List<string> itemFiles = new List<string>();
            foreach (var entry in zip.Entries)
            {
                // Ignore folder names
                if (string.IsNullOrEmpty(entry.Name)) continue;

                // Supported format is <type>/<id>/<filename>
                // type = Items or Stimuli
                // ID is item-12345 or stim-12345
                string[] parts = entry.FullName.Split('/', '\\');
                if (parts.Length != 3) continue;    // Skip unsupported files (e.g. imsmanifest.xml)
                if (!string.Equals(parts[0], "Items", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(parts[0], "Stimuli", StringComparison.OrdinalIgnoreCase)) continue;

                // Suppress media files (to save space on the test server)
                string ext = Path.GetExtension(parts[2]);
                if (string.Equals(ext, ".mp4", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(ext, ".m4a", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(ext, ".ogg", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(ext, ".png", StringComparison.OrdinalIgnoreCase)) continue;

                itemFiles.Add(entry.FullName);
            }
            itemFiles.Sort();

            return itemFiles;
        }

        private void ImportItemToGit(string folderPath, string groupName, string itemName)
        {
            Console.WriteLine("----------------");
            Console.WriteLine("{0}: {1}", m_currentItemIndex, itemName);
            ++m_currentItemIndex;
            Console.WriteLine();

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            long startExists = stopwatch.ElapsedTicks; // Will be near zero but doing this for consistency

            GitLabSession.ProjectStatus projectStatus = m_session.GetProjectStatus(itemName, groupName);
            //Console.WriteLine("ProjectStatus = {0}", projectStatus);
            if (projectStatus == GitLabSession.ProjectStatus.HasContents)
            {
                stopwatch.Stop();
                Console.WriteLine("Item already exists on GitLab. ({0:N3}ms)",
                    ((double)(stopwatch.ElapsedTicks-startExists)) / (((double)Stopwatch.Frequency) / 1000.0));
                return;
            }

            long endExists = stopwatch.ElapsedTicks;

            // Clear out any existing local repository
            if (Directory.Exists(Path.Combine(folderPath, ".git")))
            {
                DeleteItemFolder(Path.Combine(folderPath, ".git"));
                Console.WriteLine("Removed existing repository.");
            }

            // Create a Git repository for the item and add everything to it
            ExecGit(folderPath, "init");
            ExecGit(folderPath, "add -A");
            ExecGit(folderPath, "commit -m 'original'");

            long startCreateProject = stopwatch.ElapsedTicks;

            // Create the corresponding project on GitLab
            if (projectStatus == GitLabSession.ProjectStatus.NonExistent)
            {
                m_session.CreateProject(itemName, groupName);
                Console.WriteLine("Project '{0}/{1}' created on GitLab.", groupName, itemName);
            }
            else
            {
                Console.WriteLine("Empty project '{0}/{1}' already exists on GitLab.", groupName, itemName);
            }

            long endCreateProject = stopwatch.ElapsedTicks;

            // Give it 30 seconds to finish provisioning the project
            if (projectStatus == GitLabSession.ProjectStatus.NonExistent) System.Threading.Thread.Sleep(15000);

            // Add the origin project to local git
            ExecGit(folderPath, string.Concat("remote add origin ", m_gitLabUrl, "/", groupName.ToLowerInvariant(), "/", itemName.ToLowerInvariant(), ".git"));

            long startPush = stopwatch.ElapsedTicks;

            ExecGit(folderPath, "push -u origin master");

            long endPush = stopwatch.ElapsedTicks;

            stopwatch.Stop();

            // "Item,Exists,Create,Push,Total"
            string logLine = string.Format("{0},{1:F3},{2:F3},{3:F3},{4:F3}",
                itemName,
                ((double)(endExists-startExists)) / (((double)Stopwatch.Frequency) / 1000.0),
                ((double)(endCreateProject-startCreateProject)) / (((double)Stopwatch.Frequency) / 1000.0),
                ((double)(endPush-startPush)) / (((double)Stopwatch.Frequency) / 1000.0),
                ((double)stopwatch.ElapsedTicks) / (((double)Stopwatch.Frequency) / 1000.0));
            m_Log.WriteLine(logLine);
            m_Log.Flush();

            Console.Write("Log: ");
            Console.WriteLine(logLine);
            Console.WriteLine("Items = {0}, CallsRequiringRetries = {1}, AverageRetries = {2:N1}", m_currentItemIndex, m_gitRetryCalls, ((double)m_gitRetries) / ((double)m_gitRetryCalls));
            Console.WriteLine();
        }

        const int c_gitMaxAttempts = 5;
        int m_gitRetries = 0;
        int m_gitRetryCalls = 0;

        private void ExecGit(string folderPath, string args)
        {
            int exitCode = 0;
            for (int attempt = 0; attempt < c_gitMaxAttempts; ++attempt)
            {
                Console.WriteLine("git " + args);
                using (Process p = new Process())
                {
                    p.StartInfo.EnvironmentVariables["PATH"] = string.Concat(c_gitPathAdditions, ";", p.StartInfo.EnvironmentVariables["PATH"]);
                    p.StartInfo.EnvironmentVariables["EDITOR"] = "GitPad";
                    p.StartInfo.EnvironmentVariables["github_git"] = @"C:\Users\Brandt\AppData\Local\GitHub\PortableGit_c2ba306e536fdf878271f7fe636a147ff37326ad";
                    p.StartInfo.EnvironmentVariables["github_shell"] = @"true";
                    p.StartInfo.EnvironmentVariables["git_install_root"] = @"C:\Users\Brandt\AppData\Local\GitHub\PortableGit_c2ba306e536fdf878271f7fe636a147ff37326ad";
                    p.StartInfo.EnvironmentVariables["HOME"] = p.StartInfo.EnvironmentVariables["HOMEDRIVE"] + p.StartInfo.EnvironmentVariables["HOMEPATH"];
                    p.StartInfo.UseShellExecute = false;
                    p.StartInfo.FileName = @"C:\Users\Brandt\AppData\Local\GitHub\PortableGit_c2ba306e536fdf878271f7fe636a147ff37326ad\bin\git.exe";
                    p.StartInfo.Arguments = args;
                    p.StartInfo.WorkingDirectory = folderPath;
                    p.Start();
                    p.WaitForExit();
                    Console.WriteLine();

                    if (p.ExitCode == 0) return;
                    exitCode = p.ExitCode;
                }

                ++m_gitRetries;
                if (attempt == 0) ++m_gitRetryCalls;

                System.Threading.Thread.Sleep(1000);
            }
            Console.WriteLine();

            throw new ApplicationException(string.Format("Exit code {0} returned from: git {1} after {2} retries", exitCode, args, c_gitMaxAttempts));
        }

        private static void DeleteFolder(DirectoryInfo di)
        {
            foreach(DirectoryInfo di2 in di.GetDirectories())
            {
                FileAttributes fa = di2.Attributes & ~(FileAttributes.ReadOnly|FileAttributes.Hidden|FileAttributes.System);
                if (di2.Attributes != fa) di2.Attributes = fa;
                DeleteFolder(di2);
            }
            foreach(FileInfo fi in di.GetFiles())
            {
                FileAttributes fa = fi.Attributes & ~(FileAttributes.ReadOnly|FileAttributes.Hidden|FileAttributes.System);
                if (fi.Attributes != fa) fi.Attributes = fa;
                fi.Delete();
            }
            di.Delete();
        }

        private void DeleteItemFolder(string folderPath)
        {
            DeleteFolder(new DirectoryInfo(folderPath));
        }


    }
}
