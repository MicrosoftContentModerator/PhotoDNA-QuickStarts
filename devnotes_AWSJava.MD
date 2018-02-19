<div style="left: 32px; width: 975px; height: 753px;">

<div style="padding-left: 37.5px; padding-right: 37.5px;">

<div>

# Development procedure

Follow these steps to make code changes without changing the deployment strategy.  

* * *

</div>

<div>

### Step 1: Get tools

Ensure that you have AWS Development tools via 
**Help -> Install New Software** ->  Search https://aws.amazon.com/eclipse

* * *

</div>

<div>

### Step 2: Make you changes

LambdaFunctionHandler.java contains code that reacts the to the timer.


* * *

</div>

<div>

### Step 3: Upload your function 

Under Project or Package Explorer View, right-click on your project and select **Amazon Web Services -> Upload Function to AWS Lambda**. Then follow the steps to create a new Lambda function or upload your code to an existing function. 

Once it is on the AWS console you can add the environment variables that should be the same values that are typed into the cloud template parameters. *Note: if you upload to an existing function you will get a copy of the environment variables.*

* * *

</div>

<div>

### Step 4: Download deployment package to update deployment.zip
After making these changes you will need to update the delployment.zip package included in the repository. On the AWS Lambda Console. Use **Actions -> Export function -> download deployment package**.

* * *

</div>


</div>

</div>