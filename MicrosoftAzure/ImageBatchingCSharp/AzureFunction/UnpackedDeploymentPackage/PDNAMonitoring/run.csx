#r "..\bin\Microsoft.Azure.WebJobs.Host.dll"
#r "Microsoft.WindowsAzure.Storage"
#r "..\bin\Newtonsoft.Json.dll"
#r "..\bin\Microsoft.Ops.Common.PreHashClient.CSharp.dll"
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Azure.WebJobs;
using Newtonsoft.Json;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.ServiceBus.Messaging;
using Microsoft.Ops.Common.PreHashClient.CSharp;
using System.Text;
using System.Net;

		
		static HashSet<string> SupportedImageTypes { get; } = new HashSet<string> { ".png", ".gif", ".jpeg", ".jpg", ".tiff", ".bmp" };
		static int timeout = 280;

				public static async void Run(TimerInfo myTimer, TraceWriter log)
		{
			try
			{
				var logger = new OptionalLogger();
				logger.logs = log;

				bool logsetup = true;
				if( System.Environment.GetEnvironmentVariable("logVerbose").ToLower() == "false") logsetup = false;

				logger.logging = logsetup;
				// This is all just setting up the logging for the function, so that if the user elects to not logging the procesdure, nothing will be logged to the tracewriter.


				DateTime invocationTime = DateTime.Now; // this will be used to stop to process after ~4:45 so the next trigger will not spawn an overlapping function
				
				var storageAccount = CloudStorageAccount.Parse(System.Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
				var client = storageAccount.CreateCloudQueueClient();
				var queue = client.GetQueueReference("pdnamonitoringimagequeue");
				queue.CreateIfNotExists();

				while ((DateTime.Now.Subtract(invocationTime)).Seconds < timeout)
				{
					// if the queue doesnt not always return this number, sometime more or less. we make up for this inconsistency with a task.delay of 100 (1/10th of a second) for each group/request at the end of this while loop
					var batch = queue.GetMessages(15);
					if (batch == null)
					{
						logger.Verbose("PDNAMonitor: Queue returned NULL and BREAK function");
						break;
					}

					List<List<CloudQueueMessage>> BatchedSets = new List<List<CloudQueueMessage>>();
					List<CloudQueueMessage> hashBatch = new List<CloudQueueMessage>();

					int i = 0;
					foreach (var mess in batch) //this loop seperates the batch of messages into groups of five called hashBatch
					{
						if (i < 4)
						{
							hashBatch.Add(mess);
							i++;
						}
						else
						{
							hashBatch.Add(mess);
							BatchedSets.Add(hashBatch);

							i = 0;
							hashBatch = new List<CloudQueueMessage>();
						}
					}
                    if (hashBatch.Count > 0) BatchedSets.Add(hashBatch); //finish the loop and cleanup, adds the last incomplete group, checks to see if no groups were created and if so quits
					if (BatchedSets.Count == 0)
					{
						logger.Verbose("PDNAMonitor: Building Batch returned NO BATCHES:: break and stop");
						break;
					}
					
					List<List<HashedImage>> hashedBatches = new List<List<HashedImage>>();  // we'll now take the groups 'BatchedSets' and hash each image. returning a list of groups of 5 hashed images

					int batchCount = 0;
					foreach (var batchedSet in BatchedSets)
					{
						List<HashedImage> hashes = new List<HashedImage>();
						foreach (var mess in batchedSet)
						{
							if ( !String.IsNullOrEmpty(mess.AsString) )
							{
								object url;
								url = mess.AsString;
								System.Uri uri = new Uri(url.ToString());
								string ext = Path.GetExtension(uri.ToString());
								if (!SupportedImageTypes.Contains(ext))           //this may not be nessicary as the BlobToQueue function also checks the uploaded blobs for supported file types.
								{
									var msg = string.Format("PDNAMonitor: Not a supported image type, ignored: {0}", url.ToString());
									log.Verbose(msg);
									await queue.DeleteMessageAsync(mess);
									continue;
								}

								CloudBlockBlob blob = new CloudBlockBlob(uri);
								HashedImage image = new HashedImage(blob.Name);
								image.mess = mess;

								Stream receiveStream = await blob.OpenReadAsync();

								image.value = PdnaClientHash.GenerateHash(receiveStream);

								receiveStream.Close();
								hashes.Add(image);
							}
							else
							{
									await queue.DeleteMessageAsync(mess);
							}
						}

						hashedBatches.Add(hashes);

					}

					var loop = Parallel.ForEach(hashedBatches, hashGroup => MakeRequest(hashGroup, logger, queue));

					await Task.Delay(100 * hashedBatches.Count);
				}

			}
			catch (Exception ex)
			{
				log.Verbose("Failed to run the app: " + GetAllMessage(ex));
			}

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

		public class HashedImage
		{
			public byte[] value;
			public string description = "";
			public string key;
			public string type = "file";
			public CloudQueueMessage mess;

			public HashedImage(string fileName)
			{
				this.key = fileName + "_" + Guid.NewGuid().ToString();
			}
		}

		public class URLObject
		{
			public string DataRepresentation = "URL";
			public string value = "";
		}

		public static async Task MakeRequest(List<HashedImage> imageList, OptionalLogger log, CloudQueue queue)
		{
			try
			{
				var client = new HttpClient();

				log.Verbose("PDNAMonitor: Making PDNA Request for images");
				// Request headers

				//client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", System.Environment.GetEnvironmentVariable("subscriptionKey"));
				client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", System.Environment.GetEnvironmentVariable("subscriptionKey", EnvironmentVariableTarget.Process));
				
				//var uri = System.Environment.GetEnvironmentVariable("subscriptionEndpoint");
				var uri = System.Environment.GetEnvironmentVariable("subscriptionEndpoint", EnvironmentVariableTarget.Process);

				HttpResponseMessage response;
				string contents = "";

				try
				{
					using (var content = new MultipartFormDataContent())
					{
						foreach (var image in imageList)
						{
							content.Add(new ByteArrayContent(image.value), image.key, image.key);
						}

						// post json
						response = await client.PostAsync(uri, content);

						// get response as string
						contents = await response.Content.ReadAsStringAsync();
					}

					// process response
					dynamic obj = JsonConvert.DeserializeObject(contents);
					log.Verbose("(.) (') (.) (') Contents: " + contents);    // process each of the results within the resposne for hits
					foreach (var result in obj.MatchResults)
					{
						try
						{
							if (result != null && result.Status.Exception == null)
							{
								if (result.IsMatch == "True")
								{
									var name = result.ContentId.ToString();
									log.Verbose("PDNAMonitor: FOUND MATCH for img: " + name);
									CloudQueueMessage mess = GetBrokeredMessage(imageList, name);
									await MailNotification(response, name, log);
									try
									{
										await queue.DeleteMessageAsync(mess);
									}
									catch(Exception e)
									{
										if (!e.Message.Contains("404")) await MailNotificationError(e.Message, log); //sometimes the message will be deleted, but the call will be repeated if the response isn't timely enough, catch the error here
									}
								}
								else if (result.IsMatch == "False")
								{
									var name = result.ContentId.ToString();
									log.Verbose("PDNAMonitor: No match found for img: " + name);
									CloudQueueMessage mess = GetBrokeredMessage(imageList, name);
									try
									{
										await queue.DeleteMessageAsync(mess);
									}
									catch (Exception e)
									{
										if (!e.Message.Contains("404")) await MailNotificationError(e.Message, log);
									}
								}
							}
							else // Some exception was included in the PDNA response or the response was empty
							{
								log.Verbose("PDNAMonitor: Proper response was not found: " + obj);

								await MailNotificationError(await response.Content.ReadAsStringAsync(), log);
								var name = result.ContentId.ToString();
								CloudQueueMessage mess = GetBrokeredMessage(imageList, name);
								try
								{
									await queue.DeleteMessageAsync(mess);
								}
								catch (Exception e)
								{
									if (e.Message.Contains("404")) await MailNotificationError(e.Message, log);
								}
								throw new Exception(" .. the proper response was not found" + obj);
							}
						}
						catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException e)
						{
							log.Verbose("PDNAMonitor: Did not receive expected response from PDNA (RuntimeBinder): " + GetAllMessage(e));
						}
					}

				}
				catch (Exception ex)
				{
					if (!ex.Message.Contains("The lock supplied is invalid"))
					{
						string err = ("And Error was thrown trying to send request to the PDNA subscription endpoint: " + GetAllMessage(ex));
						await MailNotificationError(err, log);
						log.Verbose(err);
					}
				}

			}
			catch (Exception ex)
			{
				if (!ex.Message.Contains("The lock supplied is invalid"))
				{
					string err = ("And Error was thrown trying to build http client or request to the PDNA subscription endpoint: " + GetAllMessage(ex));
					await MailNotificationError(err, log);
					log.Verbose(err);
				}
			}
		}

		private static CloudQueueMessage GetBrokeredMessage(List<HashedImage> imageList, string name)
		{
			foreach (var image in imageList)
			{
				if (image.key.Equals(name))
				{
					return image.mess;
				}
			}

			throw new Exception("Failed to FIND IMAGE FOR name");
		}

		static string GetAllMessage(Exception ex)
		{
			string s = ex.Message;
			s += "  TYPE: " + ex.GetType().ToString();
			string trace = "  STACK: " + ex.StackTrace;
			while (ex.InnerException != null)
			{
				s += " INNER: " + ex.InnerException.Message;
				ex = ex.InnerException;
			}
			s += trace;

			return s;
		}

		private static async Task MailNotification(HttpResponseMessage message, string name, OptionalLogger log)
		{
			try
			{   // POST the response message to the optional callback endpoint
				string callbackEndPoint = System.Environment.GetEnvironmentVariable("callbackEndpoint", EnvironmentVariableTarget.Process);
				if (callbackEndPoint != "" && callbackEndPoint != "N/A")
				{
					var postClient = new HttpClient();
					var result = await message.Content.ReadAsStringAsync();
					dynamic jsonResponse = JsonConvert.SerializeObject(result);
					HttpResponseMessage response;
					byte[] byteData = Encoding.UTF8.GetBytes(jsonResponse);
					using (var content = new ByteArrayContent(byteData))
					{
						// post json
						content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
						//var postResponse = await postClient.PostAsync(System.Environment.GetEnvironmentVariable("callbackEndpoint"), jsonResponse);
						response = await postClient.PostAsync(callbackEndPoint, content);
					}
				}
			}
			catch (Exception ex)
			{
				log.Verbose(GetAllMessage(ex));
			}

			try
			{   // EMAIL 'Hit' notification to the given email address via the given SMTP mailer account
				// Build emailer parameteres / body
				string fromEmail = System.Environment.GetEnvironmentVariable("senderEmail", EnvironmentVariableTarget.Process);
				string toEmail = System.Environment.GetEnvironmentVariable("receiverEmail", EnvironmentVariableTarget.Process);
				int smtpPort = 587;
				bool smtpEnableSsl = true;
				string smtpHost = System.Environment.GetEnvironmentVariable("smtpHostAddress", EnvironmentVariableTarget.Process); // your smtp host
				string smtpUser = System.Environment.GetEnvironmentVariable("smtpUserName", EnvironmentVariableTarget.Process); // your smtp user
				string smtpPass = System.Environment.GetEnvironmentVariable("smtpPassword", EnvironmentVariableTarget.Process); // your smtp password
				string subject = "Azure Image Content Warning from PhotoDNA";
				string messageBody = "An image was uploaded to Azure which was flagged for innapropiate content by PhotoDNA...  the image file: " + name + ". The File was uploaded to the storage account: " + System.Environment.GetEnvironmentVariable("targetStorageAccountName", EnvironmentVariableTarget.Process);

				MailMessage mail = new MailMessage(fromEmail, toEmail);
				SmtpClient client = new SmtpClient();
				client.Port = smtpPort;
				client.EnableSsl = smtpEnableSsl;
				client.DeliveryMethod = SmtpDeliveryMethod.Network;
				client.UseDefaultCredentials = false;
				client.Host = smtpHost;
				client.Credentials = new System.Net.NetworkCredential(smtpUser, smtpPass);
				mail.Subject = subject;

				mail.Priority = MailPriority.High;

				mail.Body = messageBody;

				try
				{
					client.Send(mail);
					log.Verbose("**  ----  ----  ---- Email sent.");
				}
				catch (Exception ex)
				{
					log.Verbose("!!  ERROR ----  ---- The email was not sent.");
					log.Verbose("!!  ERROR ----  ---- Error message: " + ex.Message + "| | |" + ex.StackTrace);
					while (ex.InnerException != null)
					{
						log.Verbose("!!  ERROR ----  ---- Error message: " + ex.Message + "| | |" + ex.StackTrace);
						ex = ex.InnerException;
					}
				}

			}
			catch (Exception ex)
			{
				log.Verbose(" ...The email was not sent.");
				log.Verbose(" ...Error message: " + ex.Message);
			}

		}

		private static async Task MailNotificationError(string err, OptionalLogger log)
		{
			try
			{	// POST the response message to the optional callback endpoint
				string callbackEndPoint = System.Environment.GetEnvironmentVariable("callbackEndpoint", EnvironmentVariableTarget.Process);
				if (callbackEndPoint != "" && callbackEndPoint != "N/A")
				{
					var postClient = new HttpClient();
					var result = err;
					dynamic jsonResponse = JsonConvert.SerializeObject(result);
					HttpResponseMessage response;
					byte[] byteData = Encoding.UTF8.GetBytes(jsonResponse);
					using (var content = new ByteArrayContent(byteData))
					{
						// post json
						content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
						//var postResponse = await postClient.PostAsync(System.Environment.GetEnvironmentVariable("callbackEndpoint"), jsonResponse);
						response = await postClient.PostAsync(callbackEndPoint, content);
					}
				}
			}
			catch (Exception ex)
			{
				log.Verbose("Error posting to Callback endpoint: " + ex);
			}

			try
			{
				// EMAIL 'Hit' notification to the given email address via the given SMTP mailer account
				// Build emailer parameteres / body
				string fromEmail = System.Environment.GetEnvironmentVariable("senderEmail", EnvironmentVariableTarget.Process);
				string toEmail = System.Environment.GetEnvironmentVariable("receiverEmail", EnvironmentVariableTarget.Process);
				int smtpPort = 587;
				bool smtpEnableSsl = true;
				string smtpHost = System.Environment.GetEnvironmentVariable("smtpHostAddress", EnvironmentVariableTarget.Process); // your smtp host
				string smtpUser = System.Environment.GetEnvironmentVariable("smtpUserName", EnvironmentVariableTarget.Process); // your smtp user
				string smtpPass = System.Environment.GetEnvironmentVariable("smtpPassword", EnvironmentVariableTarget.Process); // your smtp password
				string subject = "Error was thrown by PhotoDNA Monitoring";
				string messageBody = "An error was thrown attempting to scan an object uploaded to your blob storage account. " + err + ". </br> The File was uploaded to the storage account: " + System.Environment.GetEnvironmentVariable("targetStorageAccountName", EnvironmentVariableTarget.Process); ;

				MailMessage mail = new MailMessage(fromEmail, toEmail);
				SmtpClient client = new SmtpClient();
				client.Port = smtpPort;
				client.EnableSsl = smtpEnableSsl;
				client.DeliveryMethod = SmtpDeliveryMethod.Network;
				client.UseDefaultCredentials = false;
				client.Host = smtpHost;
				client.Credentials = new System.Net.NetworkCredential(smtpUser, smtpPass);
				mail.Subject = subject;

				mail.Priority = MailPriority.High;

				mail.Body = messageBody;

				try
				{
					client.Send(mail);
					log.Verbose("Error catch Email sent.");
				}
				catch (Exception ex)
				{
					log.Verbose("!!  ERROR ----  ---- The email was not sent.");
					log.Verbose("!!  ERROR ----  ---- Error message: " + ex.Message);
				}
				
			}
			catch (Exception ex)
			{
				log.Verbose("The email was not sent.");
				log.Verbose("Error message: " + ex.Message);
			}

		}