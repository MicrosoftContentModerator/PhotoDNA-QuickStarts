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
using System.Text;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.ServiceBus.Messaging;

namespace FunctionApp1
{
	public static class Function1
    {

		static HashSet<string> SupportedImageTypes { get; } = new HashSet<string> { ".png", ".gif", ".jpeg", ".jpg", ".tiff", ".bmp" };
		static int timeout = 280;

		[FunctionName("PDNAMonitoring")]
		public static async void Run([TimerTrigger("0 */5 * * * *")]TimerInfo myTimer, TraceWriter log)
		{
			DateTime invocationTime = DateTime.Now;
			var receiverFactory = MessagingFactory.CreateFromConnectionString(System.Environment.GetEnvironmentVariable("NamespaceConnectionString", EnvironmentVariableTarget.Process));
			var receiver = await receiverFactory.CreateMessageReceiverAsync("PDNAMonitoringImageQueue", ReceiveMode.PeekLock);

			while ((DateTime.Now.Subtract(invocationTime)).Seconds < timeout)
			{
				var batch = await receiver.ReceiveBatchAsync(10);
				if (batch == null) break;
				List<Task> taskList = new List<Task>();
				
				foreach(var mess in batch)
				{
					log.Verbose("    ----  Function BATCHING ::" + mess.ToString());
					object url;

					if (mess.Properties.ContainsKey("URI"))
					{
						url = mess.Properties["URI"];

						System.Uri uri = new Uri(url.ToString());

						CloudBlockBlob blob = new CloudBlockBlob(uri);
						string name = blob.Name;

						string ext = Path.GetExtension(uri.ToString());
						if (!SupportedImageTypes.Contains(ext))
						{
							log.Verbose("    IGNORE Object is not a supported image type");
							continue;
						}

						taskList.Add(MakeRequest(uri.ToString(), name, ext, log, mess));
					}
					else
					{
						await mess.DeadLetterAsync();
					}

					await Task.Delay(100);
					//remove the running message after completion
				}
				
				await Task.WhenAll(taskList);
			}


		}

		/*public static byte[] ReadFully(Stream input)
		{
			byte[] buffer = new byte[input.Length];
			using (MemoryStream ms = new MemoryStream())
			{
				int read;
				while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
				{
					ms.Write(buffer, 0, read);
				}
				return ms.ToArray();
			}
		}*/
		
		public class URLObject
		{
			public string DataRepresentation = "URL";
			public string value = "";
		}

		public static async Task MakeRequest(string input, string name, string ext, TraceWriter log, BrokeredMessage mess)
		{
			try
			{
				var client = new HttpClient();

				log.Verbose("    ----  Making PDNA Request for image: ");
				// Request headers
				
				//client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", System.Environment.GetEnvironmentVariable("subscriptionKey"));
				client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", System.Environment.GetEnvironmentVariable("subscriptionKey", EnvironmentVariableTarget.Process));

				// Request parameters
				//var uri = System.Environment.GetEnvironmentVariable("subscriptionEndpoint");
				var uri = System.Environment.GetEnvironmentVariable("subscriptionEndpoint", EnvironmentVariableTarget.Process);

				MediaTypeHeaderValue contentType = new MediaTypeHeaderValue("application/json");;
				
				var body = new URLObject();
				body.value = input;
				
				HttpResponseMessage response;
				string contents = "";
				string json = JsonConvert.SerializeObject(body);

				byte[] byteData = Encoding.UTF8.GetBytes(json);

				try
				{
					using (var content = new ByteArrayContent(byteData))
					{
						// post json
						content.Headers.ContentType = contentType;
						response = await client.PostAsync(uri, content);

						// get response as string
						contents = await response.Content.ReadAsStringAsync();
					}

					// process response
					dynamic obj = JsonConvert.DeserializeObject(contents);

					if (obj.IsMatch == "True")
					{
						log.Verbose("!!  ----  ----  FOUND MATCH for img: " + name + "." + ext);
						await MailNotification(response, name, log);
						await mess.CompleteAsync();
					}
					else if (obj.IsMatch == "False")
					{
						log.Verbose("..  ----  ----  NO MATCH FOUND for img: " + name + "." + ext);
						await mess.CompleteAsync();
					}
					else
					{
						log.Verbose(".!!  ----  ----  ERROR for img: " + name + "." + ext);
						string err = (" .. the proper response was not found" + obj);
						await MailNotificationError(await response.Content.ReadAsStringAsync(), log);
						await mess.AbandonAsync();
						throw new Exception(" .. the proper response was not found" + obj);
					}
				}
				catch (Exception ex)
				{
					string err = ("And Error was thrown trying to send Json to the PDNA subscription endpoint: " + GetAllMessage(ex));
					await MailNotificationError(err, log);
					log.Verbose("And Error was thrown trying to send Json to the PDNA subscription endpoint: " + GetAllMessage(ex));
				}

			}
			catch (Exception ex)
			{
				string err = (" An error has occured while attempting to send the image request to PDNA, Exception:  " + GetAllMessage(ex));
				await MailNotificationError(err, log);
				log.Verbose(" An error has occured while attempting to send the image request to PDNA, Exception:  " + GetAllMessage(ex));
			}
		}

		static string GetAllMessage(Exception ex)
		{
			string s = ex.Message;
			s += "  TYPE: " + ex.GetType().ToString();
			while(ex.InnerException != null)
			{
				s += " INNER: " + ex.InnerException.Message;
				ex = ex.InnerException;
			}
			
			return s;
		}

		private static async Task MailNotification(HttpResponseMessage message, string name, TraceWriter log)
		{
			try
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
					response = await postClient.PostAsync(System.Environment.GetEnvironmentVariable("callbackEndpoint", EnvironmentVariableTarget.Process), content);
				}

			}
			catch (Exception ex)
			{
				Console.WriteLine(ex);
			}

			try
			{
				Console.Write("!!  ----  ----  ---- FOUND MATCH: Attempting to send email for: ");

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
					while(ex.InnerException != null)
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
					response = await postClient.PostAsync(System.Environment.GetEnvironmentVariable("callbackEndpoint", EnvironmentVariableTarget.Process), content);
				}
			}
			catch (Exception ex)
			{
				log.Verbose("Error posting to Callback endpoint: " + ex);
			}

			try
			{
				log.Verbose("!!  ----  ----  ---- FOUND MATCH: Attempting to send email for: ");

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
				string messageBody = "An error was thrown attempting to scan an object uploaded to your blob storage account. This is an example email";

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
	}
}
