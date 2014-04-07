using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NDesk.Options;
using Renci.SshNet;
using Renci.SshNet.Common;
using Renci.SshNet.Sftp;

namespace OdjfsScraper.SyncDatabaseBackups
{
    internal class Program
    {
        private const string SearchPattern = @"OdjfsScraper_\d{4}-\d{2}-\d{2}.bak";
        private const string LatestLinkName = @"OdjfsScraper_Current.bak";
        private const string TemporaryFileName = @"OdjfsScraper_Previous.bak";

        private static int Main(string[] args)
        {
            // get the command line arguments
            string host = null;
            string username = null;
            string password = null;
            string localDirectory = null;
            string remoteDirectory = null;
            bool caseInsensitive = false;
            bool displayUsage = false;

            var p = new OptionSet
            {
                {"o=", "the remote host", v => host = v},
                {"u=", "the SFTP username", v => username = v},
                {"p=", "the SFTP password", v => password = v},
                {"l=", "the local source directory of database backups", v => localDirectory = v},
                {"r=", "the remote destination directory for the database backups", v => remoteDirectory = v},
                {"i", "use case-insensitive file name comparison, default: false", v => caseInsensitive = true},
                {"h|help", "display this help message", v => displayUsage = true}
            };

            p.Parse(args);

            if (displayUsage)
            {
                DisplayUsage(p);
                return 0;
            }

            // validate command line arguments
            if (host == null)
            {
                return DisplayArgumentError("-o");
            }
            if (username == null)
            {
                return DisplayArgumentError("-u");
            }
            if (password == null)
            {
                return DisplayArgumentError("-p");
            }
            if (localDirectory == null)
            {
                return DisplayArgumentError("-l");
            }
            if (remoteDirectory == null)
            {
                return DisplayArgumentError("-r");
            }

            // get all local files that could be upload
            string[] allLocalFiles = Directory
                .GetFiles(localDirectory)
                .Where(File.Exists)
                .Select(Path.GetFileName)
                .Where(f => Regex.IsMatch(f, SearchPattern))
                .ToArray();

            using (var sftp = new SftpClient(host, username, password))
            {
                sftp.Connect();
                sftp.ChangeDirectory(remoteDirectory);

                // delete the previous symlink
                if (sftp.Exists(LatestLinkName))
                {
                    SftpFile linkDestination = sftp.Get(LatestLinkName);
                    sftp.RenameFile(linkDestination.Name, TemporaryFileName);
                    sftp.DeleteFile(LatestLinkName);
                    sftp.RenameFile(TemporaryFileName, linkDestination.Name);
                }
                else
                {
                    try
                    {
                        sftp.DeleteFile(LatestLinkName);
                    }
                    catch (SftpPathNotFoundException)
                    {
                    }
                }

                // get all remote files
                ISet<string> allRemoteNodes = new HashSet<string>(sftp
                    .ListDirectory(".")
                    .Select(s => s.Name)
                    .Select(f => caseInsensitive ? f.ToUpper() : f));

                // upload missing files
                foreach (string localFile in allLocalFiles)
                {
                    if (!allRemoteNodes.Contains(caseInsensitive ? localFile.ToUpper() : localFile))
                    {
                        Console.WriteLine("Uploading {0}.", localFile);
                        string localPath = Path.Combine(localDirectory, localFile);
                        using (var stream = new FileStream(localPath, FileMode.Open, FileAccess.Read))
                        {
                            sftp.UploadFile(stream, localFile);
                        }
                    }
                    else
                    {
                        Console.WriteLine("Skipping {0}.", localFile);
                    }
                }

                // make a link to the 
                SftpFile latestFile = sftp
                    .ListDirectory(".")
                    .Where(s => Regex.IsMatch(s.Name, SearchPattern))
                    .OrderByDescending(s => s.Name)
                    .FirstOrDefault();
                if (latestFile != null)
                {
                    Console.WriteLine("Making link from {0} to {1}.", latestFile.Name, LatestLinkName);
                    sftp.SymbolicLink(latestFile.Name, LatestLinkName);
                }
            }

            return 0;
        }

        private static void DisplayUsage(OptionSet p)
        {
            Console.WriteLine("Usage: SyncDatabaseBackups [OPTIONS]");
            Console.WriteLine();
            Console.WriteLine("  Uploads all OdjfsScraper database backups to the specified remote host,");
            Console.WriteLine("  via SFTP. Only database backups with filenames matching the following");
            Console.WriteLine("  pattern will be included:");
            Console.WriteLine();
            Console.WriteLine("  OdjfsScraper_####-##-##.bak (# must be a digit, 0-9)");
            Console.WriteLine();
            Console.WriteLine("  A symbolic link 'OdjfsScraper_Current.bak' will be created in the");
            Console.WriteLine("  remote directory pointing to the last database backup in the remote");
            Console.WriteLine("  directory, by lex order.");
            Console.WriteLine();
            Console.WriteLine("Options:");
            p.WriteOptionDescriptions(Console.Out);
        }

        private static int DisplayArgumentError(string argument)
        {
            Console.Error.WriteLine("The {0} argument is required.", argument);
            Console.Error.WriteLine("Use the --help command for more information.");
            return 1;
        }
    }
}