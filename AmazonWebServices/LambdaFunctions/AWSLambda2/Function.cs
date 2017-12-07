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
using Amazon.Rekognition;

using Amazon.S3;
using Amazon.S3.Model;
using Newtonsoft.Json;
using Amazon.Lambda.S3Events;
using Amazon.Lambda.Model;
using Amazon;
using Amazon.SQS.Model;
using Amazon.Lambda;


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

		//private string subscriptionKey = "884a6e4f5cc74558a32d5c759dd43e70";
		//private string emailAddress = "jnnortz@gmail.com"; //notification receiver
		// This address must be verified with Amazon SES. It will be the address used to send the emails
		//private string senderAddress = "jnnortz@gmail.com";

		IAmazonS3 S3Client { get; }

		//public string myQueueURL = "https://sqs.us-west-2.amazonaws.com/249673612814/testqueue"; 

		float MinConfidence { get; set; } = DEFAULT_MIN_CONFIDENCE;

		HashSet<string> SupportedImageTypes { get; } = new HashSet<string> { ".png", ".gif", ".jpeg", ".jpg", ".tiff", ".bmp" };


		// The subject line for the email.
		static readonly string subject = "Amazon SES test (AWS SDK for .NET)";

		// The email body for recipients with non-HTML email clients.
		static readonly string textBody = "Amazon SES Test (.NET)\r\n"
										+ "This email was sent through Amazon SES "
										+ "using the AWS SDK for .NET."
										+ "An Image was uploaded to one of your S3 buckets which was flagged by PhotoDNA for its content:";

		// The HTML body of the email.
		static readonly string htmlBody = @"<html>
											<head></head>
											<body>
												<h1>WARNING</h1>
												<p>An Image was uploaded to one of your S3 buckets which was flagged by PhotoDNA for its content:</p>
												<br>";
		static readonly string htmlBodyEndCap =  @"</body>
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
			if (!string.IsNullOrWhiteSpace(environmentMinConfidence))
			{
				float value;
				if (float.TryParse(environmentMinConfidence, out value))
				{
					this.MinConfidence = value;
					Console.WriteLine($"Setting minimum confidence to {this.MinConfidence}");
				}
				else
				{
					Console.WriteLine($"Failed to parse value {environmentMinConfidence} for minimum confidence. Reverting back to default of {this.MinConfidence}");
				}
			}
			else
			{
				Console.WriteLine($"Using default minimum confidence of {this.MinConfidence}");
			}
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

		/// <summary>
		/// A function for responding to S3 create events. it will send image file URLs to PDNAs service and get results. On hits this function will email the given address with a notification
		/// </summary>
		/// <param name="input"></param>
		/// <param name="context"></param>
		/// <returns></returns>
		public async Task FunctionHandler(S3Event input, ILambdaContext context)
		{
			DateTime invocationStart = DateTime.Now;
			while (invocationStart.AddSeconds(285) > (DateTime.Now))
			{
				ReceiveMessageRequest receiveMessageRequest = new ReceiveMessageRequest();
				Amazon.SQS.AmazonSQSClient amazonSQSClient = new Amazon.SQS.AmazonSQSClient();
				receiveMessageRequest.QueueUrl = System.Environment.GetEnvironmentVariable("sourceQueueURL");
				receiveMessageRequest.MaxNumberOfMessages = 10;
				receiveMessageRequest.WaitTimeSeconds = 20;
				ReceiveMessageResponse receiveMessageResponse = await amazonSQSClient.ReceiveMessageAsync(receiveMessageRequest);

				Console.WriteLine(" ----  STARTING query WITH: " + receiveMessageResponse.Messages.Count + " messages in the SQS");

				int c = receiveMessageResponse.Messages.Count;
				if (c == 0)
				{
					Console.WriteLine("[:.: :] ending with empty queue");
					return;
				}
				// TODO: break up messages into groups of 5 for prehashing
				if (c > 5) c = 5;
				for (int i = 0; i < c; i++)
				{
					dynamic body = JsonConvert.DeserializeObject(receiveMessageResponse.Messages[i].Body);
					string bucket = body.Records[0].s3.bucket.name;
					string key = body.Records[0].s3["object"].key;
					if (!SupportedImageTypes.Contains(Path.GetExtension(key)))
					{
						Console.WriteLine($"Object {bucket}:{key} is not a supported image type");
						continue;
					}
					//Console.WriteLine($"sending image from {input.Records[0].S3.Bucket.Name}:{input.Records[0].S3.Object.Key}");

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
					string url = S3Client.GetPreSignedURL(expiryUrlRequest);

					HttpResponseMessage response = new HttpResponseMessage();
					response = await MakeRequest(url, bucket, key);

					await amazonSQSClient.DeleteMessageAsync(System.Environment.GetEnvironmentVariable("sourceQueueURL"), receiveMessageResponse.Messages[i].ReceiptHandle);
				}
				System.Threading.Thread.Sleep(100);

			}
			
		}

		public async Task<HttpResponseMessage> MakeRequest(string objectUrl, string bucket, string key)
		{
			var client = new HttpClient();

			Console.WriteLine("  --  Making PDNA Request for image: " + key + ", from bucket: " + bucket + "Sending to sendpoint: :" + System.Environment.GetEnvironmentVariable("PhotoDNAEndpoint"));
			// Request headers
			client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", System.Environment.GetEnvironmentVariable("subscriptionKey"));
			//client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", "884a6e4f5cc74558a32d5c759dd43e70");

			// Request parameters
			var uri = System.Environment.GetEnvironmentVariable("subscriptionEndpoint");
			
			HttpResponseMessage response;
			string contents = "";

			// Request body
			var body = new URLObject();
			body.value = objectUrl;
			string json = JsonConvert.SerializeObject(body);
			//Console.Write("Sending json: " + json + "To Key: " + System.Environment.GetEnvironmentVariable("subscriptionKey"));
			byte[] byteData = Encoding.UTF8.GetBytes(json);

			using (var content = new ByteArrayContent(byteData))
			{
				content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
				response = await client.PostAsync(uri, content);
				contents = await response.Content.ReadAsStringAsync();
			}

			dynamic obj = JsonConvert.DeserializeObject(contents);
			Console.WriteLine("| | | | Exit Request, Found Match?? : " + obj.IsMatch);
			var emailResponse = new SendEmailResponse();
			if (obj.IsMatch == "True")
			{
				Console.Write("!!  ...FOUND MATCH... ");
				emailResponse = await MailNotification(bucket, key, objectUrl);
			}

			return response;
		}

		public class URLObject
		{
			public string DataRepresentation = "URL";
			public string value = "";
		}

		private async Task<SendEmailResponse> MailNotification(string bucket, string key, string url)
		{
			Console.Write("... FOUND MATCH: Attempting to send email for: " + bucket + " " + key);
			var response = new SendEmailResponse();
			// Replace USWest2 with the AWS Region you're using for Amazon SES.
			// Acceptable values are EUWest1, USEast1, and USWest2.
			using (var client = new AmazonSimpleEmailServiceClient(RegionEndpoint.USWest2))
			{
				var sendRequest = new SendEmailRequest
				{
					Source = System.Environment.GetEnvironmentVariable("senderAddress"),
					//Source = "jnnortz@gmail.com",
					Destination = new Destination
					{
						ToAddresses =
						new List<string> { System.Environment.GetEnvironmentVariable("emailAddress") }
						//new List<string> { "jnnortz@gmail.com" }
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
									"<br> The key of the image in question: " + key +
									"<br><br> link to image in question: " + url
							},
							Text = new Content
							{
								Charset = "UTF-8",
								Data = textBody +
									"\r\n The bucket the image was uploaded to: " + bucket +
									"\r\n The key of the image in question: " + key +
									"\r\n \r\n link to image in question: " + url
							}
						}
					}
				};
				Console.WriteLine("Finished Building sendRequest");
				try
				{
					Console.WriteLine("Sending email using Amazon SES...");
					response = await client.SendEmailAsync(sendRequest);
					Console.WriteLine("The email was sent successfully. ");
				}
				catch (Exception ex)
				{
					Console.WriteLine("The email was not sent.");
					Console.WriteLine("Error message: " + ex.Message);
				}
			}

			return response;
		}
		
	}
}
