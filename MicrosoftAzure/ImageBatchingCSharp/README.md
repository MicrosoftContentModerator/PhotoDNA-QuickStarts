## Azure Resource Deployment Guide ##


This guide will lead you through the deployment of PhotoDNA Monitoring to your existing Blob-Storage accounts. This following template will create a Azure Function Application along with supporting resources that will allow that automatic monitoring of image uploads to your storage databases, checking their content for inappropriate content and letting you know if PhotoDNA finds anything suspicious. Before you start, download the following .ZIP folder and save it to a location you can access easily later on. 

![Simple Diagram](https://github.com/MicrosoftContentModerator/PhotoDNA-QuickStarts/blob/dev/MicrosoftAzure/Documentation/SimpleArchDiagramAZ_placeholder.png?raw=true)

To being the process simply click
[Launch](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2FMicrosoftContentModerator%2FPhotoDNA-QuickStarts%2Fdev%2FMicrosoftAzure%2FResourceGroupTemplate%2FresourceGroupTemplate.json "Deploy in Azure"). This will bring you to your Microsoft Azure account console deployment page. Fill out some information about your target Resource Group and PhotoDNA Subscription

![](https://github.com/MicrosoftContentModerator/PhotoDNA-QuickStarts/blob/dev/MicrosoftAzure/Documentation/TemplateLandingPage.PNG?raw=true)

- **Callback Endpoint:** A URL of *your* web service, which is capable of receiving JSON documents. If there is any error during monitoring, the stack will post the error to this endpoint. This field is optional.
- **Function App Name:** The name of the Function App resource that will be created.
- **PDNA Storage Account Type:** The subscription type for the storage account.
- **Receiver Email:** An email address that receives a notification when PhotoDNA finds *unacceptable* content. This address also receives notification when an error occurs during monitoring.
- **Sender Email**: An email address from which the notifications will be sent.
- **Storage Account Name:** The Name of the storage account resource group that this app will be added to, the same as the one selected from the drop down above labeled "Resource Group"
- **Target Storage Blob Name:** This should be the name of the Storage blob that will be monitored. Uploading to this blob will trigger the function to scan all upload images.
- **Subscription Endpoint:** A URL that was provided to you when you subscribed to Microsoft PhotoDNA. This is the URL to which your images will be uploaded and scanned. This URL is specific to your subscription. To find your personal PDNA Endpoint, [visit this page](https://testpdnaui.azurewebsites.net/).  
- **Subscription Key:** A unique key that was provided to you when you subscribed to Microsoft PhotoDNA. This key is specific to your subscription, and should not be shared with other users. To find your personal PDNA key, [visit this page](https://testpdnaui.azurewebsites.net/ )  

Once the parameters have been added and you have verified they are correct, Accept the terms of services and select **"Purchase"** 

Once the Resource Group has completed Deployment, navigate to your accounts **Function App Tab** and select the newly created app name. 
Once on the landing page, Select the** Platomm Features **Tab, then Select **"App Service Editor"** near the bottom.
![](https://github.com/MicrosoftContentModerator/PhotoDNA-QuickStarts/blob/dev/MicrosoftAzure/Documentation/FunctionAppNavigation.PNG?raw=true)

Once the App Service Editor has loaded you can upload the function deployment package to the Function App by hovering over the WWWROOT label and clicking the "more" menu (marked "...") and then select **Upload Files**

![](https://github.com/MicrosoftContentModerator/PhotoDNA-QuickStarts/blob/dev/MicrosoftAzure/Documentation/UploadZip.png?raw=true)

Once the .ZIP has been uploaded, right click it and select **Extract All**
![](https://github.com/MicrosoftContentModerator/PhotoDNA-QuickStarts/blob/dev/MicrosoftAzure/Documentation/ZipExtract.png?raw=true)


It might take a few minutes for these new files to be installed properly, once they are completed, navigate back to the Function App Overview and expand the tab for the newly created Function_1. Select the Integrate tab and you should see the following page. You must change the **Path** to have your targeted blob's name instead of the placeholder value (keep the "**/{name}.{ext}**" and just replace the text in front of the **/**).
![](https://github.com/MicrosoftContentModerator/PhotoDNA-QuickStarts/blob/dev/MicrosoftAzure/Documentation/IntegrationPage.PNG?raw=true)


Allow the function to update for a minute or two and once the new target is updated your blob will be automatically monitored by PhotoDNA
