
## Photo DNA Quick Start Guide for AWS ##

This document will guide you through the steps to set up Photo DNA Monitoring for your own existing S3 buckets.
Before you start, visit [the Photo DNA home page](https://myphotodna.microsoftmoderator.com/) and create a subscription.

**1)**	Click ![https://console.aws.amazon.com/cloudformation/home?region=us-west-2#/stacks/new?stackName=PhotoDNAMonitorStackTempalte&templateURL=https://s3-us-west-2.amazonaws.com/allyislambdafunctionsbucket/PhotoDNAMonitorStackTemplate.template](https://dmhnzl5mp9mj6.cloudfront.net/application-management_awsblog/images/cloudformation-launch-stack.png) to be navigated to your AWS accounts CloudFormation page using our CloudFormation Template. You will be navigate automatically to this page once you have logged in:
![](https://s3-us-west-2.amazonaws.com/allyislambdafunctionsbucket/AWSLandingPage.PNG)
Nothing needs to be done on this page, the URL for the CloudFormation Stack will be automatically populated into the appropriate field. Click **Next** to continue

**2)**	On the first fill out the following fields:  
![](https://s3-us-west-2.amazonaws.com/allyislambdafunctionsbucket/AWSFirstPageCapture.PNG)

- The **Stack name** will auto-populate, but can be changed to users preference.
- **Callback Endpoint** is optional, in the case of an error, the error will be Posted to the given URL
- **Notification Receiver** Email Address is the email that will be messaged when the Photo DNA scanner find an dangerous content, the email must be verified by your AWS account if your account is still in the 'Sandbox', [visit this page for instructions](https://us-west-2.console.aws.amazon.com/ses/home?region=us-west-2#verified-senders-email ) 
- **Photo DNA Endpoint** is the subscription-specific endpoint where the scanner will send the images to be analyzed. To find your personal PDNA Endpoint [visit this page](https://testpdnaui.azurewebsites.net/).  
- **Photo DNA Key** is the subscription-specific key used to identify your subscription. To find your personal PDNA key [visit this page](https://testpdnaui.azurewebsites.net/ ) 
- For **Sender Email** field, the email must be verified by your AWS account, [visit this page for instructions](https://us-west-2.console.aws.amazon.com/ses/home?region=us-west-2#verified-senders-email ) 

**3)**	On Options page, click **Next**
 ![](https://s3-us-west-2.amazonaws.com/allyislambdafunctionsbucket/AWSSecondPageCapture.PNG)
The options page contains a number of parameters that can change how the stack is deployed to your AWS account. It allows you to add tags to the different architecture created in the stack, change what permissions the stack is given during creation or limit who can access the stacks elements. Do not edit this page unless you know how it will effect the deployment of the stack

**4)**	On Review, double-check the parameters and other options selected during the process above. Then acknowledge the terms and click **‘Create,’** your stack will then be created.

**5)**	Once the stack has finished (this may take several minutes), navigate to your [Amazon S3 management page](https://s3.console.aws.amazon.com/s3) and select any bucket you want monitored by PDNA

**6)**	From the bucket landing page, Select **Properties**, then **Events**
![](https://s3-us-west-2.amazonaws.com/allyislambdafunctionsbucket/BucketPage.PNG)

**7)**	On the events page select “**Add Notification**,” then fill in the following fields: 
![](https://s3-us-west-2.amazonaws.com/allyislambdafunctionsbucket/EventsPage.PNG)

- Select a **Name** for your event
- Select **Put and Post**
- Select **SQS Queue** under the “**Send To**” dropdown
- Select the **Simple Queue Name** created by the stack.

**8)**	Select **Save**

**9)** Navigate to your Lambda page in the AWS portal. Once there, select the newly created lambda function. Once the page loads, you should see a display with a number of types of AWS architecture objects on the left. Double click on **CloudWatch Events** and you should see if create a new element in the center with the same name. Scroll down the page and just under the display should be field for the newly created CloudWatch Event, enter the following: 
for **Rule dropdown** select Create New Rule.
**Name and Describe** the rule as you wish, we will be creating a timer, so I input 'Timer' here.
for **Rule type** select **Scheduled Expression**
and in the scheduled expression field copy the following:
	rate(5 minutes)

Select Add at the bottom, then in the top left of the Lambda section select **Save**

//Put picture here

 
Your bucket is now ready to be monitored by PDNA, repeat steps 6-8 for each bucket you want monitored by PDNAs services. 
