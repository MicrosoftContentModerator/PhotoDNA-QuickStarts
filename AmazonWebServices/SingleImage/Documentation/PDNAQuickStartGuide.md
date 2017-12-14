
## Photo DNA Quick Start Guide for AWS ##

This document will guide you through the steps to set up Photo DNA Monitoring for your own existing S3 buckets.
Before you start, visit [the Photo DNA home page](https://myphotodna.microsoftmoderator.com/) and create a subscription.

**1)**	Click ![https://console.aws.amazon.com/cloudformation/home?region=us-west-2#/stacks/new?stackName=S3MonitorUsingPhotoDNA&templateURL=PhotoDNA-QuickStarts/AmazonWebServices/CloudFormationTemplate/PhotoDNAMonitorStackTemplate.template](https://dmhnzl5mp9mj6.cloudfront.net/application-management_awsblog/images/cloudformation-launch-stack.png) to be navigated to your AWS accounts CloudFormation page using our CloudFormation Template. You will be navigate automatically to this page once you have logged in:
![](https://s3-us-west-2.amazonaws.com/allyislambdafunctionsbucket/AWSLandingPage.PNG)
Nothing needs to be done on this page, the URL for the CloudFormation Stack will be automatically populated into the appropriate field. Click **Next** to continue
![](https://github.com/MicrosoftContentModerator/PhotoDNA-QuickStarts/blob/dev_singleLambda/AmazonWebServices/Documentation/AWSLandingPage.PNG?raw=true)

**2)**	On the first fill out the following fields:  
![](https://github.com/MicrosoftContentModerator/PhotoDNA-QuickStarts/blob/dev_singleLambda/AmazonWebServices/Documentation/AWSFirstPageCapture.PNG?raw=true)

- The **Stack name** will auto-populate, but can be changed to users preference.
- **Callback Endpoint** is optional, in the case of an error, the error will be Posted to the given URL
- **Notification Receiver** Email Address is the email that will be messaged when the Photo DNA scanner find an dangerous content, the email must be verified by your AWS account if your account is still in the 'Sandbox', [visit this page for instructions](https://us-west-2.console.aws.amazon.com/ses/home?region=us-west-2#verified-senders-email ) 
- **Photo DNA Endpoint** is the subscription-specific endpoint where the scanner will send the images to be analyzed. To find your personal PDNA Endpoint [visit this page](https://testpdnaui.azurewebsites.net/).  
- **Photo DNA Key** is the subscription-specific key used to identify your subscription. To find your personal PDNA key [visit this page](https://testpdnaui.azurewebsites.net/ ) 
- For **Sender Email** field, the email must be verified by your AWS account, [visit this page for instructions](https://github.com/MicrosoftContentModerator/PhotoDNA-QuickStarts/blob/dev_singleLambda/AmazonWebServices/Documentation/AWSSecondPageCapture.PNG?raw=true) 

**3)**	On Options page, click **Next**
 ![](https://github.com/MicrosoftContentModerator/PhotoDNA-QuickStarts/blob/dev_singleLambda/AmazonWebServices/Documentation/AWSSecondPageCapture.PNG?raw=true)
The options page contains a number of parameters that can change how the stack is deployed to your AWS account. It allows you to add tags to the different architecture created in the stack, change what permissions the stack is given during creation or limit who can access the stacks elements. Do not edit this page unless you know how it will effect the deployment of the stack

**4)**	On Review, double-check the parameters and other options selected during the process above. Then acknowledge the terms and click **‘Create,’** your stack will then be created.

**5)**	Once the stack has finished (this may take several minutes), navigate to your [Amazon S3 management page](https://s3.console.aws.amazon.com/s3) and select any bucket you want monitored by PDNA

**6)**	From the bucket landing page, Select **Properties**, then **Events**
![](https://github.com/MicrosoftContentModerator/PhotoDNA-QuickStarts/blob/dev_singleLambda/AmazonWebServices/Documentation/BucketPage.PNG?raw=true)

**7)**	On the events page select “**Add Notification**,” then fill in the following fields: 
![](https://github.com/MicrosoftContentModerator/PhotoDNA-QuickStarts/blob/dev_singleLambda/AmazonWebServices/Documentation/EventsPage.PNG?raw=true)

- Select a **Name** for your event
- Select **Put and Post**
- Select **SQS Queue** under the “**Send To**” dropdown
- Select the **Simple Queue Name** created by the stack.

**8)**	Select **Save**
Your bucket is now ready to be monitored by PDNA, repeat steps 6-8 for each bucket you want monitored by PDNAs services. 

**9)** Navigate to your AWS accounts **Lambda** page using the Services tab under "Compute." Once at the index, you should see the newly created lambda Function in the list of installed functions. Select the newly created Lambda and once the overview of the function loads, scroll to the bottom of the page where you'll see a section for **Concurrency.** Check the button for "Reserve concurrency" and enter **10** into the field. Then click **Save** at the top right
![](https://github.com/MicrosoftContentModerator/PhotoDNA-QuickStarts/blob/dev_singleLambda/AmazonWebServices/Documentation/LambdaConcurrencyPage.PNG?raw=true)

**10)** Finally, just check your email (the Notification Receiver email used above) and confirm the subscription to the Error Catching Topic. This will inform the email address whenever an error occurs during a scan. The email confirmation link should be waiting in your in-box once the Stack completes.