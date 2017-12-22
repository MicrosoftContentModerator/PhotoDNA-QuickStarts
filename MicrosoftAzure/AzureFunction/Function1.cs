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
using System.Configuration;

namespace FunctionApp1
{
	public static class Function1
    {

		static HashSet<string> SupportedImageTypes { get; } = new HashSet<string> { "png", "gif", "jpeg", "jpg", "tiff", "bmp" };

		static string MatchEmailSubject = "Azure Image Content Warning from PhotoDNA";

		static string MatchEmailBody = "An image was uploaded to Azure which was flagged for innapropiate content by PhotoDNA";

		//TODO delete all this
		static string subscriptionKey = ConfigurationManager.AppSettings["subscriptionKey"];
		static string subscriptionEndpoint = ConfigurationManager.AppSettings["subscriptionEndpoint"];
		static string senderEmail = ConfigurationManager.AppSettings["senderEmail"];
		static string receiverEmail = ConfigurationManager.AppSettings["receiverEmail"];
		static string callbackEndpoint = ConfigurationManager.AppSettings["callbackEndpoint"];

		[FunctionName("Function_1")]
		public static async void Run([BlobTrigger("allyispictureuploadfolder/{name}.{ext}", Connection = "AzureWebJobsStorage")]Stream input, string name, string ext, TraceWriter log)
		{
			byte[] file = new byte[input.Length];
			file = ReadFully(input);
			
			await MakeRequest(file, name, ext, log);

		}

		public static byte[] ReadFully(Stream input)
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
		}

		public static async Task MakeRequest(byte[] input, string name, string ext, TraceWriter log)
		{
			try
			{
				var client = new HttpClient();

				log.Verbose("    ----  Making PDNA Request for image: ");
				// Request headers
				
				//client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", System.Environment.GetEnvironmentVariable("subscriptionKey"));
				client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);

				// Request parameters
				//var uri = System.Environment.GetEnvironmentVariable("subscriptionEndpoint");
				var uri = subscriptionEndpoint;

				MediaTypeHeaderValue contentType;

				switch (ext)
				{
					case "png":
						contentType = new MediaTypeHeaderValue("image/png");
						break;
					case "gif":
						contentType = new MediaTypeHeaderValue("image/gif");
						break;
					case "jpeg":
						contentType = new MediaTypeHeaderValue("image/jpeg");
						break;
					case "jpg":
						contentType = new MediaTypeHeaderValue("image/jpeg");
						break;
					case "tiff":
						contentType = new MediaTypeHeaderValue("image/tiff");
						break;
					case "bmp":
						contentType = new MediaTypeHeaderValue("image/bmp");
						break;
					default:
						log.Verbose("File wrong format: not an image");
						return;
				}

				HttpResponseMessage response;
				string contents = "";
				byte[] byteData = Encoding.UTF8.GetBytes(input.ToString());

				try
				{
					using (var content = new ByteArrayContent(input))
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
						log.Verbose("!!  ----  ----  FOUND MATCH for img: ");
						await MailNotification(response, name, log);
					}
					else if (obj.IsMatch == "False")
					{
						log.Verbose("..  ----  ----  NO MATCH FOUND for img: ");
					}
					else
					{
						log.Verbose(".!!  ----  ----  ERROR for img: ");
						string err = (" .. the proper response was not found" + obj);
						await MailNotificationError(await response.Content.ReadAsStringAsync(), log);
						throw new Exception(" .. the proper response was not found" + obj);
					}
				}
				catch (Exception ex)
				{
					string err = ("And Error was thrown trying to send Json to the PDNA subscription endpoint: " + ex.Message);
					await MailNotificationError(err, log);
					log.Verbose("And Error was thrown trying to send Json to the PDNA subscription endpoint: " + ex.Message);
				}

			}
			catch (Exception ex)
			{
				string err = (" An error has occured while attempting to send the image request to PDNA, Exception:  " + ex.Message);
				await MailNotificationError(err, log);
				log.Verbose(" An error has occured while attempting to send the image request to PDNA, Exception:  " + ex.Message);
			}
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
					response = await postClient.PostAsync(callbackEndpoint, content);
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
				string fromEmail = senderEmail;
				string toEmail = receiverEmail;
				int smtpPort = 587;
				bool smtpEnableSsl = true;
				string smtpHost = "	smtp.sendgrid.net"; // your smtp host
				string smtpUser = "apikey"; // your smtp user
				string smtpPass = "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"; // your smtp password
				string subject = MatchEmailSubject;
				string messageBody = MatchEmailBody + "    the image file: " + name;

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
					log.Verbose("!!  ERROR ----  ---- Error message: " + ex.Message);
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
					response = await postClient.PostAsync(callbackEndpoint, content);
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
				string fromEmail = senderEmail;
				string toEmail = receiverEmail;
				int smtpPort = 587;
				bool smtpEnableSsl = true;
				string smtpHost = "	smtp.sendgrid.net"; // your smtp host
				string smtpUser = "apikey"; // your smtp user
				string smtpPass = "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"; // your smtp password
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
