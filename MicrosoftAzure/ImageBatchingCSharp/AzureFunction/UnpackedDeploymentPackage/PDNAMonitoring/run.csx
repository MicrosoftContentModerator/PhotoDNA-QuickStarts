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
using Microsoft.WindowsAzure.Storage.Blob;
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
				DateTime invocationTime = DateTime.Now;
				var receiverFactory = MessagingFactory.CreateFromConnectionString(System.Environment.GetEnvironmentVariable("NamespaceConnectionString", EnvironmentVariableTarget.Process));
				var receiver = await receiverFactory.CreateMessageReceiverAsync("pdnamonitoringimagequeue", ReceiveMode.PeekLock);

				while ((DateTime.Now.Subtract(invocationTime)).Seconds < timeout)
				{
					var batch = receiver.ReceiveBatch(10);
					if (batch == null)
					{
						log.Verbose("PDNAMonitor: Queue returned NULL and BREAK function");
						break;
					}

					List<List<BrokeredMessage>> BatchedSets = new List<List<BrokeredMessage>>();
					List<BrokeredMessage> hashBatch = new List<BrokeredMessage>();

					int i = 0;
					foreach (var mess in batch)
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
							hashBatch = new List<BrokeredMessage>();
						}
					}

					if (BatchedSets.Count == 0)
					{
						log.Verbose("PDNAMonitor: Building Batch returned NO BATCHES:: break and stop");
						break;
					}

					List<Task> taskList = new List<Task>();

					int batchCount = 0;
					foreach (var batchedSet in BatchedSets)
					{
						List<HashedImage> hashes = new List<HashedImage>();
						foreach (var mess in batchedSet)
						{
							if (mess.Properties.ContainsKey("URI"))
							{
								object url;
								url = mess.Properties["URI"];
								System.Uri uri = new Uri(url.ToString());
								string ext = Path.GetExtension(uri.ToString());
								if (!SupportedImageTypes.Contains(ext))
								{
									var msg = string.Format("PDNAMonitor: Not a supported image type, ignored: {0}", url.ToString());
									log.Verbose(msg);
									await mess.DeadLetterAsync();
									continue;
								}

								try{
                                                                        CloudBlockBlob blob = new CloudBlockBlob(uri);
                                                                        HashedImage image = new HashedImage(blob.Name);
                                                                        image.mess = mess;
                                                                
                                                        
                                                                        Stream receiveStream = await blob.OpenReadAsync();

                                                                        image.value = PdnaClientHash.GenerateHash(receiveStream);

                                                                        receiveStream.Close();
                                                                        hashes.Add(image);
                                                                }
                                                                catch(Exception e){
                                                                        log.Verbose("Failed To read uri : " + uri + “Due to :”  + e.Message);
                                                                }

							}
							else
							{
								await mess.DeadLetterAsync();
							}
						}

						// make the batch call to pdna
						taskList.Add(MakeRequest(hashes, log));

						await Task.Delay(100);
					}

					await Task.WhenAll(taskList);
				}

			}
			catch (Exception ex)
			{
				log.Verbose("Failed to run the app: " + GetAllMessage(ex));
			}

		}

		public class HashedImage
		{
			public byte[] value;
			public string description = "";
			public string key;
			public string type = "file";
			public BrokeredMessage mess;

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

		public static async Task MakeRequest(List<HashedImage> imageList, TraceWriter log)
		{
			try
			{
				var client = new HttpClient();

				log.Verbose("PDNAMonitor: Making PDNA Request for images");
				// Request headers

				//client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", System.Environment.GetEnvironmentVariable("subscriptionKey"));
				client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", System.Environment.GetEnvironmentVariable("subscriptionKey", EnvironmentVariableTarget.Process));

				// Request parameters
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
					//log.Verbose("() () () Contents: " + contents);
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
									var mess = GetBrokeredMessage(imageList, name);
									await MailNotification(response, name, log);
									await mess.CompleteAsync();
								}
								else if (result.IsMatch == "False")
								{
									var name = result.ContentId.ToString();
									log.Verbose("PDNAMonitor: No match found for img: " + name);
									var mess = GetBrokeredMessage(imageList, name);
									await mess.CompleteAsync();
								}
							}
							else
							{
								log.Verbose("PDNAMonitor: Proper response was not found: " + obj);

								await MailNotificationError(await response.Content.ReadAsStringAsync(), log);
								var name = result.ContentId.ToString();
								var mess = GetBrokeredMessage(imageList, name);
								await mess.AbandonAsync();
								throw new Exception(" .. the proper response was not found" + obj);
							}
						}
						catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException e)
						{
							log.Verbose("PDNAMonitor: Did nto receive expected response from PDNA (RuntimeBinder): " + GetAllMessage(e));
						}
					}

				}
				catch (Exception ex)
				{
					if (!ex.Message.Contains("The lock supplied is invalid"))
					{
						string err = ("And Error was thrown trying to send Json to the PDNA subscription endpoint: " + GetAllMessage(ex));
						await MailNotificationError(err, log);
						log.Verbose(err);
					}
				}

			}
			catch (Exception ex)
			{
				if (!ex.Message.Contains("The lock supplied is invalid"))
				{
					string err = ("And Error was thrown trying to send Json to the PDNA subscription endpoint: " + GetAllMessage(ex));
					await MailNotificationError(err, log);
					log.Verbose(err);
				}
			}
		}

		private static BrokeredMessage GetBrokeredMessage(List<HashedImage> imageList, string name)
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

		private static async Task MailNotification(HttpResponseMessage message, string name, TraceWriter log)
		{
			try
			{
				string callbackEndPoint = System.Environment.GetEnvironmentVariable("callbackEndpoint", EnvironmentVariableTarget.Process);
				if (callbackEndPoint != "N/A")
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
			{
				//string fromEmail = System.Environment.GetEnvironmentVariable("senderEmail");
				//string toEmail = System.Environment.GetEnvironmentVariable("receiverEmail");
				string fromEmail = System.Environment.GetEnvironmentVariable("senderEmail", EnvironmentVariableTarget.Process);
				string toEmail = System.Environment.GetEnvironmentVariable("receiverEmail", EnvironmentVariableTarget.Process);
				int smtpPort = 587;
				bool smtpEnableSsl = true;
				string smtpHost = System.Environment.GetEnvironmentVariable("smtpHostAddress", EnvironmentVariableTarget.Process); // your smtp host
				string smtpUser = System.Environment.GetEnvironmentVariable("smtpUserName", EnvironmentVariableTarget.Process); // your smtp user
				string smtpPass = System.Environment.GetEnvironmentVariable("smtpPassword", EnvironmentVariableTarget.Process); // your smtp password
				string subject = "Azure Image Content Warning from PhotoDNA";
				string messageBody = "An image was uploaded to Azure which was flagged for innapropiate content by PhotoDNA...  the image file: " + name;

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

		private static async Task MailNotificationError(string err, TraceWriter log)
		{
			try
			{
				string callbackEndPoint = System.Environment.GetEnvironmentVariable("callbackEndpoint", EnvironmentVariableTarget.Process);
				if (callbackEndPoint != "N/A")
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
				log.Verbose("!!  ---- ERROR  ---- FOUND");

				//string fromEmail = System.Environment.GetEnvironmentVariable("senderEmail");
				//string toEmail = System.Environment.GetEnvironmentVariable("receiverEmail");
				string fromEmail = System.Environment.GetEnvironmentVariable("senderEmail", EnvironmentVariableTarget.Process);
				string toEmail = System.Environment.GetEnvironmentVariable("receiverEmail", EnvironmentVariableTarget.Process);
				int smtpPort = 587;
				bool smtpEnableSsl = true;
				string smtpHost = System.Environment.GetEnvironmentVariable("smtpHostAddress", EnvironmentVariableTarget.Process); // your smtp host
				string smtpUser = System.Environment.GetEnvironmentVariable("smtpUserName", EnvironmentVariableTarget.Process); // your smtp user
				string smtpPass = System.Environment.GetEnvironmentVariable("smtpPassword", EnvironmentVariableTarget.Process); // your smtp password
				string subject = "Error was thrown by PhotoDNA Monitoring";
				string messageBody = "An error was thrown attempting to scan an object uploaded to your blob storage account. " + err;

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
					log.Verbose("Email sent.");
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