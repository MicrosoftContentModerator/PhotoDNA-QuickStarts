using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure;
using Microsoft.Azure.Documents;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob; 
using System.IO;
using Microsoft.Azure.WebJobs;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;

namespace WorkerRole1
{
	public class WorkerRole : RoleEntryPoint
	{
		static HashSet<string> SupportedImageTypes { get; } = new HashSet<string> { ".png", ".gif", ".jpeg", ".jpg", ".tiff", ".bmp" };

		static string MatchEmailSubject = "Azure Image Content Warning from PhotoDNA";

		static string MatchEmailBody = "An image was uploaded to Azure which was flagged for innapropiate content by PhotoDNA";

		static void Main(string[] args)
		{
			JobHost host = new JobHost();
			host.RunAndBlock();
		}
		
		public static async void ImageUploadTrigger([BlobTrigger("input/{name}.{ext}")] Stream input)
		{
			using (var fileStream = File.Create(""))
			{
				input.Seek(0, SeekOrigin.Begin);
				input.CopyTo(fileStream);
				byte[] file = ReadFully(fileStream);

				if (!SupportedImageTypes.Contains(Path.GetExtension(fileStream.Name)))
				{
					Console.WriteLine("    IGNORE Object is not a supported image type");
					return;
				}

				await MakeRequest(file, Path.GetExtension(fileStream.Name));
			}

		}

		public static byte[] ReadFully(Stream input)
		{
			byte[] buffer = new byte[16 * 1024];
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

		public static async Task MakeRequest(Byte[] input, string ext)
		{
			try
			{
				var client = new HttpClient();

				Console.WriteLine("    ----  Making PDNA Request for image: ");
				// Request headers
				client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", System.Environment.GetEnvironmentVariable("subscriptionKey"));
				//client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);

				// Request parameters
				var uri = System.Environment.GetEnvironmentVariable("subscriptionEndpoint");
				//var uri = subscriptionEndpoint;

				 MediaTypeHeaderValue contentType;

				switch (ext)
				{
					case ".png":
						contentType = new MediaTypeHeaderValue("image/png");
						break;
					case ".gif":
						contentType = new MediaTypeHeaderValue("image/gif");
						break;
					case ".jpeg":
						contentType = new MediaTypeHeaderValue("image/jpeg");
						break;
					case ".jpg":
						contentType = new MediaTypeHeaderValue("image/jpg");
						break;
					case ".tiff":
						contentType = new MediaTypeHeaderValue("image/tiff");
						break;
					case ".bmp":
						contentType = new MediaTypeHeaderValue("image/bmp");
						break;
					default:
						contentType = new MediaTypeHeaderValue("application/json");
						break;
				}

				HttpResponseMessage response;
				string contents = "";

				try
				{
					using (var content = new ByteArrayContent(input))
					{
						// post json
						content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
						response = await client.PostAsync(uri, content);

						// get response as string
						contents = await response.Content.ReadAsStringAsync();
					}

					// process response
					dynamic obj = JsonConvert.DeserializeObject(contents);

					if (obj.IsMatch == "True")
					{
						Console.Write("!!  ----  ----  FOUND MATCH for img: ");
						await MailNotification(response);
					}
					else if (obj.IsMatch == "False")
					{
						Console.WriteLine("..  ----  ----  NO MATCH FOUND for img: ");
					}
					else
					{
						Console.WriteLine(".!!  ----  ----  ERROR for img: ");
						string err = (" .. the proper response was not found" + obj);
						await MailNotificationError(await response.Content.ReadAsStringAsync());
						throw new Exception(" .. the proper response was not found" + obj);
					}
				}
				catch (Exception ex)
				{
					string err = ("And Error was thrown trying to send Json to the PDNA subscription endpoint: " + ex.Message);
					await MailNotificationError(err);
					Console.WriteLine("And Error was thrown trying to send Json to the PDNA subscription endpoint: " + ex.Message);
				}

			}
			catch (Exception ex)
			{
				string err = (" An error has occured while attempting to send the image request to PDNA, Exception:  " + ex.Message);
				await MailNotificationError(err);
				Console.Write(" An error has occured while attempting to send the image request to PDNA, Exception:  " + ex.Message);
			}

		}

		private static async Task MailNotification(HttpResponseMessage message)
		{
			try
			{
				var postClient = new HttpClient();
				var result = await message.Content.ReadAsStringAsync();
				dynamic jsonResponse = JsonConvert.DeserializeObject(result);
				var postResponse = await postClient.PostAsync(System.Environment.GetEnvironmentVariable("callbackEndpoint"), jsonResponse);
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex);
			}

			try
			{
				Console.Write("!!  ----  ----  ---- FOUND MATCH: Attempting to send email for: ");

				string fromEmail = System.Environment.GetEnvironmentVariable("senderEmail");
				string toEmail = System.Environment.GetEnvironmentVariable("receiverEmail");
				int smtpPort = 587;
				bool smtpEnableSsl = true;
				string smtpHost = ""; // your smtp host
				string smtpUser = ""; // your smtp user
				string smtpPass = ""; // your smtp password
				string subject = MatchEmailSubject;
				string messageBody = MatchEmailBody;

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
					Console.WriteLine("**  ----  ----  ---- Email sent.");
				}
				catch (Exception ex)
				{
					Console.WriteLine("!!  ERROR ----  ---- The email was not sent.");
					Console.WriteLine("!!  ERROR ----  ---- Error message: " + ex.Message);
				}
			}
			catch (Exception ex)
			{
				Console.Write(" ...The email was not sent.");
				Console.WriteLine(" ...Error message: " + ex.Message);
			}

		}

		private static async Task MailNotificationError(string err)
		{
			try
			{
				var postClient = new HttpClient();
				var result = err;
				dynamic jsonResponse = JsonConvert.DeserializeObject(result);
				var postResponse = await postClient.PostAsync(System.Environment.GetEnvironmentVariable("callbackEndpoint"), jsonResponse);
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex);
			}

			try
			{
				Console.Write("!!  ----  ----  ---- FOUND MATCH: Attempting to send email for: ");

				string fromEmail = fromEmail;
				string toEmail = toEmail;
				int smtpPort = 587;
				bool smtpEnableSsl = true;
				string smtpHost = ""; // your smtp host
				string smtpUser = ""; // your smtp user
				string smtpPass = ""; // your smtp password
				string subject = MatchEmailSubject;
				string messageBody = MatchEmailBody;

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
					Console.Write("Email sent.");
				}
				catch (Exception ex)
				{
					Console.WriteLine("!!  ERROR ----  ---- The email was not sent.");
					Console.WriteLine("!!  ERROR ----  ---- Error message: " + ex.Message);
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine("The email was not sent.");
				Console.WriteLine("Error message: " + ex.Message);
			}

		}
	}
}
