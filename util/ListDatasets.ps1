﻿# This sample script calls the Power BI API to progammtically trigger a refresh for the dataset
# It then calls the Power BI API to progammatically to get the refresh history for that dataset
# For full documentation on the REST APIs, see:
# https://msdn.microsoft.com/en-us/library/mt203551.aspx 

# Instructions:
# 1. Install PowerShell (https://msdn.microsoft.com/en-us/powershell/scripting/setup/installing-windows-powershell) and the Azure PowerShell cmdlets (https://aka.ms/webpi-azps)
# 2. Set up a dataset for refresh in the Power BI service - make sure that the dataset can be 
# updated successfully
# 3. Fill in the parameters below
# 4. Run the PowerShell script

# Parameters - fill these in before running the script!
# =====================================================

# An easy way to get group and dataset ID is to go to dataset settings and click on the dataset
# that you'd like to refresh. Once you do, the URL in the address bar will show the group ID and 
# dataset ID, in the format: 
# app.powerbi.com/groups/{groupID}/settings/datasets/{datasetID} 

$groupID = "dc6d9dce-24d1-49e1-8770-0ea791e02fe8" # the ID of the group that hosts the dataset. Use "me" if this is your My Workspace
$datasetID = "08b628a8-5c94-4ab6-94f9-5090ff54660b" # the ID of the dataset that hosts the dataset

# AAD Client ID
# To get this, go to the following page and follow the steps to provision an app
# https://dev.powerbi.com/apps
# To get the sample to work, ensure that you have the following fields:
# App Type: Native app
# Redirect URL: urn:ietf:wg:oauth:2.0:oob
#  Level of access: all dataset APIs
$clientId = "b137613f-0e3b-4628-acac-99be20327157" 

# End Parameters =======================================

# Calls the Active Directory Authentication Library (ADAL) to authenticate against AAD
function GetAuthToken
{
       # $adal = "${env:ProgramFiles(x86)}\Microsoft SDKs\Azure\PowerShell\ServiceManagement\Azure\Services\Microsoft.IdentityModel.Clients.ActiveDirectory.dll"
 
       # $adalforms = "${env:ProgramFiles(x86)}\Microsoft SDKs\Azure\PowerShell\ServiceManagement\Azure\Services\Microsoft.IdentityModel.Clients.ActiveDirectory.WindowsForms.dll"
 
       $adal = "C:\Program Files\WindowsPowerShell\Modules\AzureAD\2.0.1.6\Microsoft.IdentityModel.Clients.ActiveDirectory.dll"
       $adalPlatform = "C:\Program Files\WindowsPowerShell\Modules\AzureAD\2.0.1.6\Microsoft.IdentityModel.Clients.ActiveDirectory.Platform.dll"

       [System.Reflection.Assembly]::LoadFrom($adal) | Out-Null
 
       [System.Reflection.Assembly]::LoadFrom($adalPlatform) | Out-Null
 
       $redirectUri = "urn:ietf:wg:oauth:2.0:oob"
 
       $resourceAppIdURI = "https://analysis.windows.net/powerbi/api"

 
       #$authority = "https://login.microsoftonline.com/common/oauth2/authorize";
       $authority = "https://login.windows.net/common/oauth2/authorize";
 
       $authContext = New-Object "Microsoft.IdentityModel.Clients.ActiveDirectory.AuthenticationContext" -ArgumentList $authority

       #$Credential = Get-Credential npinto@dovercorp.com
       #$userCredentials = New-Object "Microsoft.IdentityModel.Clients.ActiveDirectory.UserPasswordCredential" -ArgumentList $Credential.UserName,$Credential.Password
       
       $userCredentials = New-Object "Microsoft.IdentityModel.Clients.ActiveDirectory.UserPasswordCredential" -ArgumentList "npinto@dovercorp.com","Xingu@1821"



       #$authResult = $authContext.AcquireToken($resourceAppIdURI, $clientId, $redirectUri, "Auto")
       $authResult = [Microsoft.IdentityModel.Clients.ActiveDirectory.AuthenticationContextIntegratedAuthExtensions]::AcquireTokenAsync($authContext,$resourceAppIdURI,$clientId,$userCredentials).Result

       return $authResult
}


# Get the auth token from AAD
$authenticationResult = GetAuthToken

echo "Authentication Result Object=[$authenticationResult]"
echo "   AccessTokenType=$($authenticationResult.AccessTokenType)"
echo "   TenantId=$($authenticationResult.TenantId)"
echo "   TokenId=$($authenticationResult.IdToken)"
echo "   Token=$($authenticationResult.AccessToken)"

# Compose the access token type and access token for authorization header 
$authorizationHeader = $authenticationResult.AccessTokenType + " " + $authenticationResult.AccessToken 

# echo "Authorization Header=$authorizationHeader"
 
# Building Rest API header with authorization token
$authHeader = @{
   'Content-Type'='application/json'
   'Authorization'= $authorizationHeader 
}

# properly format groups path
$groupsPath = ""
if ($groupID -eq "me") {
    $groupsPath = "myorg"
} else {
    $groupsPath = "myorg/groups/$groupID"
}

# List datasets in a group
$uri = "https://api.powerbi.com/v1.0/$groupsPath/datasets"
Invoke-RestMethod -Uri $uri –Headers $authHeader –Method GET –Verbose
