using System;
using System.Diagnostics;
using Azure.Identity;
using Azure.Storage.Blobs;

namespace ServerManager
{
    public class ServerManager
    {

        private string? WorldLocation { get; set; }
        private string? ServerScriptLocation { get; set; }
        private string? UnminedLocation { get; set; }
        private string? UnminedOutput {  get; set; }
        private string? StorageAccountUri {  get; set; }
        static Process? MattCraft {  get; set; }

        public ServerManager() 
        {
            WorldLocation = Environment.GetEnvironmentVariable("WorldLocation");
            ServerScriptLocation = Environment.GetEnvironmentVariable("ServerScript");
            UnminedLocation = Environment.GetEnvironmentVariable("UnminedLocation");
            UnminedOutput = Environment.GetEnvironmentVariable("UnminedOutput");
            StorageAccountUri = Environment.GetEnvironmentVariable("StorageAccountUri");
        }

        static void Main(string[] args)
        {
            ServerManager serverManager = new ServerManager();

            if (serverManager.WorldLocation == null)
            {
                Console.WriteLine("World location not found in environment variables. Exiting.");
                return;
            }

            if (serverManager.ServerScriptLocation == null)
            {
                Console.WriteLine("Server start script not found. Exiting."); 
                return;
            }

            if (serverManager.UnminedLocation == null)
            {
                Console.WriteLine("Unmined exe not found. Exiting.");
                return;
            }

            if (serverManager.UnminedOutput == null)
            {
                Console.WriteLine("Unmined output location not found. Exiting.");
                return;
            }          

            if (serverManager.StorageAccountUri == null)
            {
                Console.WriteLine("Storage account URI not found. Exiting.");
                return;
            }

            Console.WriteLine($"Found world location at {serverManager.WorldLocation}.");
            Console.WriteLine($"Found server start script at {serverManager.ServerScriptLocation}.");
            Console.WriteLine($"Found uNmINeD exe at {serverManager.UnminedLocation}.");
            Console.WriteLine($"Found uNmINeD output directory is {serverManager.UnminedOutput}.");

            if (serverManager.RunUnmined())
            {
                Console.WriteLine("Successfully ran Unmined.");
                Console.WriteLine("Attmepting to upload to Azure Storage Account.");

                if (serverManager.UploadToStorage())
                {
                    Console.WriteLine("Successfully uploaded map information to Azure Storage.");
                }
            } 
            else
            {
                Console.WriteLine("Unmined didn't complete successfully. Continuing.");
            }

            /*if (serverManager.StartServer())
            {
                Console.WriteLine("Server started.");
            }*/

        }

        private bool StartServer()
        {
            Console.WriteLine("Starting MattCraft.");

            try
            {
                using (MattCraft = new Process())
                {
                    FileInfo start = new FileInfo(this.ServerScriptLocation);

                    MattCraft.StartInfo = new ProcessStartInfo("cmd.exe", "/c " + start.Name)
                    {
                        CreateNoWindow = true,
                        UseShellExecute = true,
                        WorkingDirectory = start.DirectoryName
                    };

                    MattCraft.Start();

                    if (MattCraft == null)
                    {
                        Console.WriteLine("MattCraft failed to start.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return false;
            }
            return true;
        }

        private bool UploadToStorage()
        {
            BlobServiceClient blobServiceClient = new BlobServiceClient(
                new Uri(this.StorageAccountUri),
                new DefaultAzureCredential());


            return false;
        }

        private bool RunUnmined()
        {
            Console.WriteLine("Cleaning previous uNmINeD map.");

            DirectoryInfo mapDir = new DirectoryInfo(this.UnminedOutput);
            
            try
            {
                mapDir.Delete(true);
            }
            catch (DirectoryNotFoundException)
            {
                Console.WriteLine("No previous map found. Skipping.");
            }

            try
            {
                using (Process unmined = new Process())
                {
                    string command = $"{this.UnminedLocation} web render --players --shadows=true --world=\"{this.WorldLocation}\" --output={this.UnminedOutput}";

                    ProcessStartInfo info = new ProcessStartInfo("cmd.exe", "/c " + command)
                    {
                        CreateNoWindow = true,
                        UseShellExecute = true
                    };

                    unmined.StartInfo = info;
                    unmined.Start();

                    if (unmined != null)
                    {
                        unmined.WaitForExit();
                        unmined.Close();
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            return true;
        }
    }
}
