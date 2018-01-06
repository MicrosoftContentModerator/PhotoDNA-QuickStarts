#r "Microsoft.WindowsAzure.Storage"


using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.ServiceBus.Messaging;
using System.Threading.Tasks; 

public static async void Run(CloudBlockBlob myBlob, string name, string ext, TraceWriter log)
{
                                                log.Verbose("StartLogging: " + name);
                                		int MaxtryAttempts = 1000;
                                                int tryAttempts = 1;
                                                bool done = false;
                                                while (tryAttempts < MaxtryAttempts && !done)
                                                {
                                                                try
                                                                {
                                                                                var senderFactory = MessagingFactory.CreateFromConnectionString(System.Environment.GetEnvironmentVariable("NamespaceConnectionString"));
                                                                                var sender = await senderFactory.CreateMessageSenderAsync("PDNAMonitoringImageQueue");
                                                                                BrokeredMessage message = new BrokeredMessage();
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
                                                                                                                log.Verbose("Not an imagge " + name);
                                                                                                                return;
                                                                                }
                                                                                message.Properties.Add("URI", myBlob.StorageUri.PrimaryUri.ToString());
                                                                                log.Verbose("Loged blob: " + myBlob.Name);
                                                                                sender.Send(message);
                                                                                sender.Close();
                                                                                return;
                                                                }
                                                                catch (Exception e)
                                                                {
                                                                                
                                                                                if (e.Message.Contains("TCP error code") && tryAttempts <= MaxtryAttempts)
                                                                                {
                                                                                                tryAttempts++;
                                                                                                log.Verbose("For blob " + myBlob.Name + " TCP ports overridden IN BLOB Trigger ____" + e.Message);
                                                                                                await Task.Delay(1000);
                                                                                }
                                                                                else
                                                                                {
                                                                                                log.Verbose("ERROR Met Max attempts or unkwown Error :" + e.Message);
                                                                                                while (e.InnerException != null)
                                                                                                {
                                                                                                                e = e.InnerException;
                                                                                                                log.Verbose("____" + e.Message + "___STACKTRACE:  " + e.StackTrace);
                                                                                                }
                                                                                                done = true;
                                                                                }
                                                                }
                                                }
                                                //REACHED MAX ATTEMPT 
                                                log.Verbose("ERROR 2.. Met Max attempts IN BLOB Trigger :" + myBlob.Name);
                                                await Task.Delay(10000);
}

