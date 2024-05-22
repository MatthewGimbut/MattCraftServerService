using System;
using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using HtmlAgilityPack;

namespace ServerManager
{
    public class ServerManager
    {

        private string? WorldLocation { get; set; }
        private string? ServerScriptLocation { get; set; }
        private string? UnminedLocation { get; set; }
        private string? UnminedOutput {  get; set; }
        private string? StorageAccountUri {  get; set; }
        private string? StorageAccountConnectionString { get; set; }
        private string? StorageAccountKey { get; set; }
        private string? ModListLocation { get; set; }

        static Process? MattCraft {  get; set; }

        public ServerManager() 
        {
            WorldLocation = Environment.GetEnvironmentVariable("WorldLocation");
            ServerScriptLocation = Environment.GetEnvironmentVariable("ServerScript");
            UnminedLocation = Environment.GetEnvironmentVariable("UnminedLocation");
            UnminedOutput = Environment.GetEnvironmentVariable("UnminedOutput");
            StorageAccountUri = Environment.GetEnvironmentVariable("StorageAccountUri");
            StorageAccountConnectionString = Environment.GetEnvironmentVariable("StorageAccountConnectionString");
            StorageAccountKey = Environment.GetEnvironmentVariable("StorageAccountKey");
            ModListLocation = Environment.GetEnvironmentVariable("ModListLocation");
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

            if (serverManager.StorageAccountUri == null
                || serverManager.StorageAccountConnectionString == null
                || serverManager.StorageAccountKey == null)
            {
                Console.WriteLine("Storage account information not found. Exiting.");
                return;
            }

            Console.WriteLine($"Found world location at {serverManager.WorldLocation}.");
            Console.WriteLine($"Found server start script at {serverManager.ServerScriptLocation}.");
            Console.WriteLine($"Found uNmINeD exe at {serverManager.UnminedLocation}.");
            Console.WriteLine($"Found uNmINeD output directory is {serverManager.UnminedOutput}.");

            BlobServiceClient blobServiceClient = new BlobServiceClient(serverManager.StorageAccountConnectionString);

            if (serverManager.RunUnmined())
            {
                Console.WriteLine("Successfully ran Unmined.");
                Console.WriteLine("Attmepting to upload to Azure Storage Account.");

                if (serverManager.UploadMapToStorage(blobServiceClient))
                {
                    Console.WriteLine("Successfully uploaded map information to Azure Storage.");
                }
            } 
            else
            {
                Console.WriteLine("Unmined didn't complete successfully. Continuing.");
            }

            if (serverManager.ModListLocation != null)
            {
                Console.WriteLine("Mod list found, attempting to upload to storage.");
                if(serverManager.UploadModListToStorage(blobServiceClient))
                {
                    Console.WriteLine("Uploaded mod list to storage.");
                }
                else
                {
                    Console.WriteLine("Unable to upload the most recent version of the mod list to storage.");
                }
            }

            if (serverManager.StartServer())
            {
                Console.WriteLine("Server started.");
            }
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

        private bool UploadModListToStorage(BlobServiceClient blobServiceClient)
        {
            string blobName = "modlist.json";

            BlobContainerClient container = blobServiceClient.GetBlobContainerClient("modlist");
            Azure.Response<bool> deleteResponse = container.DeleteBlobIfExists("modlist.json");

            if (deleteResponse != null && deleteResponse.Value == true)
            {
                Console.WriteLine("Deleted previous mod list.");
            }

            Dictionary<string, string> modList = GetModList();
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(modList);

            var uploadResponse = container.UploadBlob(blobName, new MemoryStream(Encoding.UTF8.GetBytes(json)));

            if (uploadResponse.Value != null)
            {
                return true;
            }

            return false;
        }

        private Dictionary<string, string> GetModList()
        {
            using StreamReader reader = new(this.ModListLocation);
            string content = reader.ReadToEnd();

            HtmlDocument htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(content);
            List<HtmlNode> nodes = htmlDocument.DocumentNode.SelectNodes("//li/a").ToList();

            Dictionary<string, string> nameUrlPairs = new Dictionary<string, string>();
            foreach (HtmlNode node in nodes)
            {
                nameUrlPairs.Add(node.InnerHtml, node.Attributes["href"].Value);
            }

            return nameUrlPairs;
        }

        private bool UploadMapToStorage(BlobServiceClient blobServiceClient)
        {
            string blobName = "map.jpg";

            BlobContainerClient container = blobServiceClient.GetBlobContainerClient("map");

            Azure.Response<bool> deleteResponse = container.DeleteBlobIfExists("map.jpg");

            if (deleteResponse != null && deleteResponse.Value == true)
            {
                Console.WriteLine("Deleted previous map.");
            }

            var uploadResponse = container.UploadBlob(blobName, File.OpenRead($"{this.UnminedOutput}\\map.jpg"));

            if (uploadResponse.Value != null)
            {
                return true;
            }

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
                    string command = $"{this.UnminedLocation} image render --trim --shadows=true --world=\"{this.WorldLocation}\" --output={this.UnminedOutput}\\map.jpg";

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
