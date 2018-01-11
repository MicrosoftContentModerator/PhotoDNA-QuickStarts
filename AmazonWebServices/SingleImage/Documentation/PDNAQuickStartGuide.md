
## AWS S3 Monitoring using Microsoft PhotoDNA ##
*Quick Start Guide*

This document will guide you through the steps to set up Microsoft PhotoDNA Monitoring for the images in your existing S3 buckets on AWS. PhotoDNA is a technology developed by Microsoft to scan images for *unacceptable* content.  Before you start, visit https://myphotodna.microsoftmoderator.com/ [the PhotoDNA home  page] to learn more about PhotoDNA and create a subscription.
![](https://github.com/MicrosoftContentModerator/PhotoDNA-QuickStarts/blob/dev/AmazonWebServices/SingleImage/Documentation/SimpleArchDiagram.png?raw=true)
A brief overview of the architecture: This stack will create a Worker Lambda function that will be invoked automatically when a file is uploaded to the S3 Buckets you specify during setup. The Worker can run concurrently, up to 10 instances at one time, sending images to the Microsoft PhotoDNA Service to be analyzed for nefarious content. PhotoDNA will send back a response which will be logged in CloudWatch. Any image hits will trigger an email notification informing you of the image in question. Any errors will be caught and sent to a DeadLetter notification topic that will also email you to let you know what went wrong. 

**1)**	Click [![Launch](https://raw.githubusercontent.com/MicrosoftContentModerator/PhotoDNA-QuickStarts/dev/AmazonWebServices/Documentation/cloudformation-launch-stack.png)](https://console.aws.amazon.com/cloudformation/home?region=us-west-2#/stacks/new?stackName=S3MonitorUsingPhotoDNA&templateURL=https://s3-us-west-2.amazonaws.com/allyislambdafunctionsbucket/PhotoDNAMonitorStack_plusDeadLetterEmail.template) to be navigated to the CloudFormation service page in your AWS Management Console (you will be asked to login to your AWS account). The page should look similar to the picture below, with the S3 template URL already specified.  Click **Next** to continue.
![](https://github.com/MicrosoftContentModerator/PhotoDNA-QuickStarts/blob/dev_singleLambda/AmazonWebServices/Documentation/AWSLandingPage.PNG?raw=true)

**2)**	Next page is the "Details" page. Fill out the following fields:

![](https://github.com/MicrosoftContentModerator/PhotoDNA-QuickStarts/blob/dev_singleLambda/AmazonWebServices/Documentation/AWSFirstPageCapture.PNG?raw=true)

- **Stack name:** Name of the CloudFormation stack that you are installing. This field will auto-populate, but can be changed to your preference. (A stack is nothing but a set of AWS resources, such as lambda functions, notification topics, logs, queues, etc., that will be created and configured at the end of this process!)
- **Callback Endpoint:** A URL of *your* web service, which is capable of receiving JSON documents. If there is any error during monitoring, the stack will post the error to this endpoint. This field is optional.
- **Notification Receiver:** An email address that receives a notification when PhotoDNA finds *unacceptable* content. This address also receives notification when an error occurs during monitoring. (If you are testing this stack in a 'Sandbox' environment, then this email address must first be verified by AWS, [visit this page for instructions.](https://us-west-2.console.aws.amazon.com/ses/home?region=us-west-2#verified-senders-email ))
- **PhotoDNA Endpoint:** A URL that was provided to you when you subscribed to Microsoft PhotoDNA. This is the URL to which your images will be uploaded and scanned. This URL is specific to your subscription. To find your personal PDNA Endpoint, [visit this page](https://testpdnaui.azurewebsites.net/).  
- **PhotoDNA Key:** A unique key that was provided to you when you subscribed to Microsoft PhotoDNA. This key is specific to your subscription, and should not be shared with other users. To find your personal PDNA key, [visit this page](https://testpdnaui.azurewebsites.net/ ) 
- **Sender Email:** An email address from which the notifications will be sent. This email address must first be verified by AWS, [visit this page for instructions.](https://github.com/MicrosoftContentModerator/PhotoDNA-QuickStarts/blob/dev_singleLambda/AmazonWebServices/Documentation/AWSSecondPageCapture.PNG?raw=true) 

Then click **Next** to continue.

**3)**	Next page is the "Options" page. No user input is needed.

 ![](https://github.com/MicrosoftContentModerator/PhotoDNA-QuickStarts/blob/dev_singleLambda/AmazonWebServices/Documentation/AWSSecondPageCapture.PNG?raw=true)
The options page contains a number of parameters that can change how the stack is deployed to your AWS account. It allows you to add tags so as to differentiate between architectures, change the permissions provided to the stack during creation, or limit who can access the stacks elements. Do not edit this page unless you know how it will effect the deployment of the stack.

Click **Next** to continue.

**4)**	Next page is the "Review" page. Double-check the parameters and other options selected during the above process. Then acknowledge the terms and click the **‘Create,’** button. Your stack will then be created.

**5)**	Once the stack creation completes (this may take several minutes), navigate to your [Amazon S3 management page](https://s3.console.aws.amazon.com/s3) and select any bucket you want monitored by PhotoDNA. If you do not already have a bucket, you can create one by following the instructions found [here](https://docs.aws.amazon.com/AmazonS3/latest/gsg/CreatingABucket.html).

**6)**	From the bucket landing page, Select **Properties**, then **Events**

![](https://github.com/MicrosoftContentModerator/PhotoDNA-QuickStarts/blob/dev_singleLambda/AmazonWebServices/Documentation/BucketPage.PNG?raw=true)

**7)**	On the events tab select “**Add Notification**,” then fill in the following fields: 
![](https://github.com/MicrosoftContentModerator/PhotoDNA-QuickStarts/blob/dev/AmazonWebServices/SingleImage/Documentation/EventPageLambdaTarget.PNG?raw=true)
- **Name:** A name for your event (you can provide any name you want)
- Select **Put and Post** (this indicates that you want to monitor both create as well as update of images in this bucket)
- Select **Lambda Function** under the “**Send To**” dropdown
- Select the **Lambda Function created by the stack** (The function name will begin with the stack name that you provided in the second step above)

**8)**	Select **Save**
Your bucket is now ready to be monitored by PDNA, repeat steps 6-8 for each bucket you want monitored by PDNAs services. 

**9)** Navigate to your AWS accounts **Lambda** page using the Services tab under "Compute." Once at the index, you should see the newly created lambda Function in the list of installed functions. Select the newly created Lambda and once the overview of the function loads, scroll to the bottom of the page where you'll see a section for **Concurrency.** Check the button for "Reserve concurrency" and enter **10** into the field. Then click **Save** at the top right
![](https://github.com/MicrosoftContentModerator/PhotoDNA-QuickStarts/blob/dev_singleLambda/AmazonWebServices/Documentation/LambdaConcurrencyPage.PNG?raw=true)

**10)** Finally, check the Notification Receiver email's inbox (used above). An email confirmation link should be waiting in your in-box once the Stack completes. **Click on the link to confirm the subscription to the Error Catching Topic.** You are confirming that you are willing to receive email for errors that occurred during the scan. 
