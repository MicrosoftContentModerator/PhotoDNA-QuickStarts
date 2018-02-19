<div style="left: 32px; width: 975px; height: 753px;">

<div style="padding-left: 37.5px; padding-right: 37.5px;">

<div>

# Development procedure

Follow these steps to make code changes without changing the deployment strategy.  

* * *

</div>

<div>

### Step 1: Get tools

Ensure that you have Azure Function App tools for Visual Studio

* * *

</div>

<div>

### Step 2: Make you changes

PDNAMonitor.cs contains code that loads an image from the queue sends it PDNA. BlobToQueue triggers when an image is uploaded to a blob, and sends it to the queue.


* * *

</div>

<div>

### Step 3: Upload your function 

Changes to the above code must be converted from a .cs file to a .csx file. Both files need to be accompanied by a project.json, and a function.json file which set dependencies and run-time bindings respectively (See the UnpackedDeploymentPackage folder for reference). craete folders two folders, one for the PDNAMonitoring function and one for the BlobToQueue, and add the run.csx and their accompanying .json files to the folders.

to convert from .cs to .csx (see the run.csx in the above mentioned folder) create a run.csx file for each function, then copy the code from the .cs into the new file. leaving out the references to the class and namespace, leaving only the methods. Also .dll files need to referred to in a specific way, namely:  **#r "..\bin\Microsoft.Ops... .dll"**

* * *

</div>

<div>

### Step 4: Compress the the 
After making these changes you will need to update the delployment.zip package included in the repository. On the AWS Lambda Console. Use **Actions -> Export function -> download deployment package**.

* * *

</div>


</div>

</div>