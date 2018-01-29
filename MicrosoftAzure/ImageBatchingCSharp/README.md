## Azure Resource Deployment Guide ##


This guide will lead you through the deployment of PhotoDNA Monitoring to your existing Blob-Storage accounts. This following template will create a Azure Function Application along with supporting resources that will allow that automatic monitoring of image uploads to your storage databases, checking their content for inappropriate content and letting you know if PhotoDNA finds anything suspicious. 

![Simple Diagram](https://github.com/MicrosoftContentModerator/PhotoDNA-QuickStarts/blob/dev/MicrosoftAzure/ImageBatchingCSharp/Documentation/SimpleArchDiagramAZ_placeholder.png?raw=true)

**Before you start**, download the following [.ZIP folder](https://github.com/MicrosoftContentModerator/PhotoDNA-QuickStarts/blob/dev/MicrosoftAzure/ImageBatchingCSharp/AzureFunction/PDNAMonitoringQueued.zip) and save it to a location you can access easily later on. You will need to have a SMTP mailer account such as [SendGrid](https://sendgrid.com/). Lastly, the Storage Container with your target Storage Blob (the one you want to be monitored by PDNA) needs to be publicly read-accessible. To check if it has the proper settings, navigate to the Storage Container in the Azure Portal, then click the button **Access Policy** and then select "Container"
![](https://github.com/MicrosoftContentModerator/PhotoDNA-QuickStarts/blob/dev/MicrosoftAzure/ImageBatchingCSharp/Documentation/ReadAccessPolicy.PNG?raw=true)



---


**1) Load and Build the Template:** **To being the process simply click
[Launch](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2FMicrosoftContentModerator%2FPhotoDNA-QuickStarts%2Fdev_refactoring%2FMicrosoftAzure%2FImageBatchingCSharp%2FResourceGroupTemplate%2FresourceGroupTemplate.json "Deploy in Azure")**. This will bring you to your Microsoft Azure account console deployment page. Fill out some information about your target Resource Group and PhotoDNA Subscription

![](https://github.com/MicrosoftContentModerator/PhotoDNA-QuickStarts/blob/dev/MicrosoftAzure/ImageBatchingCSharp/Documentation/TemplateLandingPage.PNG?raw=true)


- **Target Storage Account Name:** The Name of the storage account resource group that this app will be added to, the same as the one selected from the drop down above labeled "Resource Group"
- **Function App Name:** The name of the Function App resource that will be created.
- **SMTP Host Address** The host address used by your SMTP mailer account. For example SendGrid's host address will be similar to:  "smtp.sendgrid.net"
- **SMTP Password** The password used to login to your SMPT account with the user name bellow.
- **SMTP User Name** The user name for your SMTP account.
- **Receiver Email:** An email address that receives a notification when PhotoDNA finds *unacceptable* content. This address also receives notification when an error occurs during monitoring.
- **Sender Email**: An email address from which the notifications will be sent.
- **Photo DNA Subscription Endpoint:** A URL that was provided to you when you subscribed to Microsoft PhotoDNA. This is the URL to which your images will be uploaded and scanned. This URL is specific to your subscription. To find your personal PDNA Endpoint, [visit this page](https://testpdnaui.azurewebsites.net/).  
- **Photo DNA Subscription Key:** A unique key that was provided to you when you subscribed to Microsoft PhotoDNA. This key is specific to your subscription, and should not be shared with other users. To find your personal PDNA key, [visit this page](https://testpdnaui.azurewebsites.net/ ) 
- **Callback Endpoint:** A URL of *your* web service, which is capable of receiving JSON documents. If there is any error during monitoring, the stack will post the error to this endpoint. This field is optional. 

Once the parameters have been added and you have verified they are correct, Accept the terms of services and select **"Purchase"** 

**2) Upload the App Components:** Once the Resource Group has completed Deployment, navigate to your accounts **Function App Tab** and select the newly created app name. 

Once on the landing page, Select the** Platform Features **Tab, then Select **"App Service Editor"** near the bottom.
![](https://github.com/MicrosoftContentModerator/PhotoDNA-QuickStarts/blob/dev/MicrosoftAzure/ImageBatchingCSharp/Documentation/FunctionAppNavigation.PNG?raw=true)

Once the App Service Editor has loaded you can upload the function deployment package to the Function App by hovering over the WWWROOT label and clicking the "more" menu (marked "...") and then select **Upload Files**
![](https://github.com/MicrosoftContentModerator/PhotoDNA-QuickStarts/blob/dev/MicrosoftAzure/ImageBatchingCSharp/Documentation/UploadZip.png?raw=true)

**3) Extract the Files:** Once the .ZIP has been uploaded and it appears in the WWWROOT file, right click it and select **Extract All**


**4) Connect the Storage Blob to the App:** It might take a few minutes for these new files to be installed properly, once they are completed, navigate back to the Function App Overview and expand the tab for the newly created BlobToQueue function (Not the PDNAMonitoring function, it should be fully set up automatically once the .ZIP is done compiling). Select the Integrate tab and you should see the following page. You must change the **Path** to have your targeted blob's name instead of the placeholder value (keep the "**/{name}.{ext}**" and just replace the text in front of the **/**).
![](https://github.com/MicrosoftContentModerator/PhotoDNA-QuickStarts/blob/dev/MicrosoftAzure/ImageBatchingCSharp/Documentation/EnterBlobNameIntegratePage.PNG?raw=true)


Allow the function to update for a minute or two and once the new target is updated your blob will be automatically monitored by PhotoDNA.
**Please NOTE** any images already existing in the blob at the time of install will be considered 'newly uploaded' and the application should immediately begin scanning those items from blob before resuming normal  timer-triggered functionality. 

**Verify the App is functioning:** if you wish to make sure the app is working properly, you can upload some test images to blob storage account and monitor the apps progress scanning each. Visit this page to download a folder of test images, a few of which will generate hits from PhotoDNA's scans.
The Template also builds an **Application Insights resource** during deployment (there will be a link to the Application Insights from the **Monitor** tab in the Function App page of the portal). while you upload images to your blob for testing or otherwise, you can view the servers invocation log and other data from the Application Insights resource that is created in your chosen resource group. After uploading a few images the Live Streaming page in Application Insights should look like this with invocation logs in the Sample Telemetry field, and peaks of non-zero requests per second.
