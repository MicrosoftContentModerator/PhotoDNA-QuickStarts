using Microsoft.Azure.WebJobs.Host;
using Microsoft.ServiceBus.Messaging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using System;
using System.Threading.Tasks;

namespace Microsoft.Ops.BlobMonitor
{
	public static class BlobToQueue
	{
		public static async void Run(CloudBlockBlob myBlob, string name, string ext, TraceWriter logger)
		{
			var log = new OptionalLogger();
			log.logs = logger;

			bool logsetup = true;
			if (System.Environment.GetEnvironmentVariable("logVerbose").ToLower() == "false") logsetup = false;

			log.logging = logsetup;

			log.Verbose("BlobToQueue: StartLogging: " + name);
			int MaxtryAttempts = 1000;
			int tryAttempts = 1;
			bool done = false;
			while (tryAttempts < MaxtryAttempts && !done)
			{
				try
				{
					var storageAccount = CloudStorageAccount.Parse(System.Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
					var client = storageAccount.CreateCloudQueueClient();
					var queue = client.GetQueueReference("pdnamonitoringimagequeue");
					queue.CreateIfNotExists();

					switch (ext)
					{
						case "png":
							break;
						case "gif":
							break;
						case "jpeg":
							break;
						case "jpg":
							break;
						case "tiff":
							break;
						case "bmp":
							break;
						default:
							log.Verbose("Not an image " + name);
							return;
					}
					
					CloudQueueMessage message = new CloudQueueMessage(myBlob.StorageUri.PrimaryUri.ToString());
					log.Verbose("BlobToQueue: Logged blob: " + myBlob.Name);
					queue.AddMessage(message);
					
					return;
				}
				catch (Exception e)
				{

					if (e.Message.Contains("TCP error code") && tryAttempts <= MaxtryAttempts)
					{
						tryAttempts++;
						log.Verbose("BlobToQueue: For blob " + myBlob.Name + " TCP ports overridden in blob to queue trigger ____" + e.Message);
						await Task.Delay(1000);
					}
					else
					{
						log.Verbose("BlobToQueue: ERROR Met max retry attempts blob to queue trigger or unkwown error :" + e.Message);
						while (e.InnerException != null)
						{
							e = e.InnerException;
							log.Verbose("____" + e.Message + "___STACKTRACE:  " + e.StackTrace);
						}

						done = true;
					}
				}
			}

			// reached max retry attempts
			log.Verbose("BlobToQueue: ERROR 2.. Met max retry attempts blob to queue trigger :" + myBlob.Name);
		}

		public class OptionalLogger
		{
			public TraceWriter logs { get; set; }
			public bool logging { get; set; }

			public void Verbose(string input)
			{
				if (logging)
				{
					logs.Verbose(input);
				}
			}
		}
	}
}
