using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using System.Text;
using System.Net.Http;

using Amazon.Lambda.Core;
using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;

using Amazon.S3;
using Amazon.S3.Model;
using Newtonsoft.Json;
using Amazon.Lambda.S3Events;
using Amazon;


// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace AWSLambda2
{
	public class Function
	{
		/// <summary>
		/// The default minimum confidence used for detecting labels.
		/// </summary>
		public const float DEFAULT_MIN_CONFIDENCE = 70f;

		/// <summary>
		/// The name of the environment variable to set which will override the default minimum confidence level.
		/// </summary>
		public const string MIN_CONFIDENCE_ENVIRONMENT_VARIABLE_NAME = "MinConfidence";

		
		IAmazonS3 S3Client { get; }


		float MinConfidence { get; set; } = DEFAULT_MIN_CONFIDENCE;

		HashSet<string> SupportedImageTypes { get; } = new HashSet<string> { ".png", ".gif", ".jpeg", ".jpg", ".tiff", ".bmp" };


		// The subject line for the email.
		static readonly string subject = "Amazon SES test (AWS SDK for .NET)";

		// The email body for recipients with non-HTML email clients.
		static readonly string textBody = "Amazon SES Test (.NET)\r\n"
										+ "This email was sent through Amazon SES "
										+ "using the AWS SDK for .NET."
										+ "An Image was uploaded to one of your S3 buckets which was flagged by PhotoDNA for its content. visit https://us-west-2.console.aws.amazon.com/cloudwatch/home?region=us-west-2#logs: for logs";

		// The HTML body of the email.
		static readonly string htmlBody = @"<html>
											<head></head>
											<body>
												<h1>WARNING</h1>
												<p>An Image was uploaded to one of your S3 buckets which was flagged by PhotoDNA for its content. visit https://us-west-2.console.aws.amazon.com/cloudwatch/home?region=us-west-2#logs: for logs</p>
												<br>";
		static readonly string htmlBodyEndCap = @"</body>
												</html>";

		/// <summary>
		/// Default constructor used by AWS Lambda to construct the function. Credentials and Region information will
		/// be set by the running Lambda environment.
		/// 
		/// This constuctor will also search for the environment variable overriding the default minimum confidence level
		/// for label detection.
		/// </summary>
		public Function()
		{
			Console.WriteLine($"Function initiated");
			this.S3Client = new AmazonS3Client();
			var environmentMinConfidence = System.Environment.GetEnvironmentVariable(MIN_CONFIDENCE_ENVIRONMENT_VARIABLE_NAME);
		}

		/// <summary>
		/// Constructor used for testing which will pass in the already configured service clients.
		/// </summary>
		/// <param name="s3Client"></param>
		/// <param name="rekognitionClient"></param>
		/// <param name="minConfidence"></param>
		public Function(IAmazonS3 s3Client, float minConfidence)
		{
			this.S3Client = s3Client;
			this.MinConfidence = minConfidence;
		}

		// TODO: Enclose each method in a generic exception catcher (Exception ex), log the messages and gracefully recover

		/// <summary>
		/// Listener lambda that is called on a schedule. it will check SQS for messages, retried them and send image file URLs to PDNAs service and get results. On hits this function will email the given address with a notification
		/// </summary>
		/// <param name="input"></param>
		/// <param name="context"></param>
		/// <returns></returns>
		public async Task FunctionHandler(S3Event input, ILambdaContext context)
		{
			var invocationTime = DateTime.Now;
			string bucket = input.Records[0].S3.Bucket.Name;
			string key = input.Records[0].S3.Object.Key;

			//Console.WriteLine($"sending image from {input.Records[0].S3.Bucket.Name}:{input.Records[0].S3.Object.Key}");
			string url = BuildObjectUrl(bucket, key);

			await MakeRequest(url, bucket, key);
			if (invocationTime.AddSeconds(1) > DateTime.Now) System.Threading.Thread.Sleep(1000);
		}

		public string BuildObjectUrl(string bucket, string key)
		{
			GetObjectRequest objLocation = new GetObjectRequest
			{
				BucketName = bucket,
				Key = key
			};
			GetPreSignedUrlRequest expiryUrlRequest = new GetPreSignedUrlRequest
			{
				BucketName = (bucket),
				Key = (key),
				Expires = (DateTime.Now.AddDays(1))
			};
			return S3Client.GetPreSignedURL(expiryUrlRequest);
		}

		public async Task MakeRequest(string objectUrl, string bucket, string key)
		{
			try
			{
				var client = new HttpClient();

				Console.WriteLine("    ----  Making PDNA Request for image: " + key + ", from bucket: " + bucket + "Sending to endpoint: " + System.Environment.GetEnvironmentVariable("subscriptionEndpoint"));
				// Request headers
				client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", System.Environment.GetEnvironmentVariable("subscriptionKey"));
				//client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);

				// Request parameters
				var uri = System.Environment.GetEnvironmentVariable("subscriptionEndpoint");
				//var uri = subscriptionEndpoint;

				HttpResponseMessage response;
				string contents = "";

				// Get image from URL
				GetObjectRequest request = new GetObjectRequest();
				request.BucketName = bucket;
				request.Key = key;

				// Request body
				var body = new URLObject();
				body.value = objectUrl;

				string json = JsonConvert.SerializeObject(body);
				//Console.Write("Sending json: " + json + "To Key: " + System.Environment.GetEnvironmentVariable("subscriptionKey"));
				byte[] byteData = Encoding.UTF8.GetBytes(json);
				try
				{
					using (var content = new ByteArrayContent(byteData))
					{
						// post json
						content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
						response = await client.PostAsync(uri, content);

						// get response as string
						contents = await response.Content.ReadAsStringAsync();
					}

					// process response
					dynamic obj = JsonConvert.DeserializeObject(contents);
					var emailResponse = new SendEmailResponse();

					if (obj.IsMatch == "True")
					{
						Console.Write("!!  ----  ----  FOUND MATCH for img: " + key + " in bucket: " + bucket);
						emailResponse = await MailNotification(bucket, key, objectUrl);
					}
					else if (obj.IsMatch == "False")
					{
						Console.WriteLine("..  ----  ----  NO MATCH FOUND for img: " + key + " in bucket: " + bucket);
					}
					else
					{
						Console.WriteLine(".!!  ----  ----  ERROR for img: " + key + " in bucket: " + bucket);
						string err = (" .. the proper response was not found" + obj);
						await MailNotificationError(bucket, key, objectUrl, err);
						throw new Exception(" .. the proper response was not found" + obj);
					}
				}
				catch (Exception ex)
				{
					string err = ("And Error was thrown trying to send Json to the PDNA subscription endpoint: " + ex.Message);
					await MailNotificationError(bucket, key, objectUrl, err);
					Console.WriteLine("And Error was thrown trying to send Json to the PDNA subscription endpoint: " + ex.Message);
				}

			}
			catch (Exception ex)
			{
				string err = (" An error has occured while attempting to send the image request to PDNA, Exception:  " + ex.Message);
				await MailNotificationError(bucket, key, objectUrl, err);
				Console.Write(" An error has occured while attempting to send the image request to PDNA, Exception:  " + ex.Message);
			}

		}

		public class URLObject
		{
			public string DataRepresentation = "URL";
			public string value = "";
		}

		private async Task<SendEmailResponse> MailNotification(string bucket, string key, string url)
		{
			try
			{
				Console.Write("!!  ----  ----  ---- FOUND MATCH: Attempting to send email for: " + bucket + " " + key);
				var response = new SendEmailResponse();
				// Replace USWest2 with the AWS Region you're using for Amazon SES.
				// Acceptable values are EUWest1, USEast1, and USWest2.
				using (var client = new AmazonSimpleEmailServiceClient(RegionEndpoint.USWest2))
				{
					var sendRequest = new SendEmailRequest
					{
						Source = System.Environment.GetEnvironmentVariable("senderAddress"),
						//Source = senderAddress,
						Destination = new Destination
						{
							ToAddresses =
							new List<string> { System.Environment.GetEnvironmentVariable("emailAddress") }
							//new List<string> { emailAddress }
						},
						Message = new Amazon.SimpleEmail.Model.Message
						{
							Subject = new Content(subject),
							Body = new Body
							{
								Html = new Content
								{
									Charset = "UTF-8",
									Data = htmlBody +
										"<br> The bucket the image was uploaded to: " + bucket +
										"<br> The key of the image in question: " + key + htmlBodyEndCap
								},
								Text = new Content
								{
									Charset = "UTF-8",
									Data = textBody +
										"\r\n The bucket the image was uploaded to: " + bucket +
										"\r\n The key of the image in question: " + key
								}
							}
						}
					};
					Console.WriteLine("Finished Building sendRequest");
					try
					{
						Console.WriteLine("!!  ----  ----  ---- Sending email using Amazon SES...");
						response = await client.SendEmailAsync(sendRequest);
						Console.WriteLine("!!  ----  ----  ---- The email was sent successfully. ");
					}
					catch (Exception ex)
					{
						Console.WriteLine("!!  ERROR ----  ---- The email was not sent.");
						Console.WriteLine("!!  ERROR ----  ---- Error message: " + ex.Message);
					}
				}

				return response;
			}
			catch (Exception ex)
			{
				var response = new SendEmailResponse();

				Console.WriteLine("The email was not sent.");
				Console.WriteLine("Error message: " + ex.Message);

				return response;
			}

		}

		private async Task<SendEmailResponse> MailNotificationError(string bucket, string key, string url, string error)
		{
			try
			{
				Console.Write("!!  ----  ----  ---- FOUND ERROR: Attempting to send email for: " + bucket + " " + key);
				var response = new SendEmailResponse();
				// Replace USWest2 with the AWS Region you're using for Amazon SES.
				// Acceptable values are EUWest1, USEast1, and USWest2.
				using (var client = new AmazonSimpleEmailServiceClient(RegionEndpoint.USWest2))
				{
					var sendRequest = new SendEmailRequest
					{
						Source = System.Environment.GetEnvironmentVariable("senderAddress"),
						//Source = senderAddress,
						Destination = new Destination
						{
							ToAddresses =
							new List<string> { System.Environment.GetEnvironmentVariable("emailAddress") }
							//new List<string> { emailAddress }
						},
						Message = new Amazon.SimpleEmail.Model.Message
						{
							Subject = new Content(subject),
							Body = new Body
							{
								Html = new Content
								{
									Charset = "UTF-8",
									Data = htmlBody +
										"<br> The bucket the image was uploaded to: " + bucket +
										"<br> The key of the image in question: " + key + htmlBodyEndCap
								},
								Text = new Content
								{
									Charset = "UTF-8",
									Data = textBody +
										"\r\n The bucket the image was uploaded to: " + bucket +
										"\r\n The key of the image in question: " + key
								}
							}
						}
					};
					Console.WriteLine("Finished Building sendRequest");
					try
					{
						Console.WriteLine("!!  ERROR ----  ---- Sending Error email using Amazon SES...");
						response = await client.SendEmailAsync(sendRequest);
						Console.WriteLine("!!  ERROR ----  ---- The Error email was sent successfully. ");
					}
					catch (Exception ex)
					{
						Console.WriteLine("!!  ERROR ----  ---- The email was not sent.");
						Console.WriteLine("!!  ERROR ----  ---- Error message: " + ex.Message);
					}
				}

				return response;
			}
			catch (Exception ex)
			{
				var response = new SendEmailResponse();

				Console.WriteLine("The email was not sent.");
				Console.WriteLine("Error message: " + ex.Message);

				return response;
			}

		}

	}
}
