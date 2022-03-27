# Content Hub Large File (asset) Upload Integration
Azure Function App - Sitecore Content Hub 4.1 integration for uplaoding large files (assets)

## Description
This Azure Function App uploads large files to a designated Sitecore Content Hub instance by chunking the files into 1000000 byte chunks and uploading them in sequence in accordance with Sitecore Content Hub documenation regarding large files: https://docs.stylelabs.com/contenthub/4.1.x/content/integrations/rest-api/upload/upload-large-files.html

## Dependencies
1. Access token for Sitecore Content Hub instance

## Limitations
1. Sitecore Content Hub
a. Content Hub upload requirements and limitations for 4.1x can be found here: https://docs.stylelabs.com/contenthub/4.1.x/content/integrations/rest-api/upload/upload-api-v2.html
b. Content Hub large file upload requirements and limitations for 4.1x can be found here: https://docs.stylelabs.com/contenthub/4.1.x/content/integrations/rest-api/upload/upload-large-files.html

## Usage
This function app can be used stand-alone, but was orginally intented for use with,
1. Dropbox.com Integration https://github.com/PatrickJonesXcentium/ContentHubDropboxIntegration
2. Box.com Integration https://github.com/PatrickJonesXcentium/ContentHubBoxIntegration

The function app workflow is triggered by a HTTP POST request to the endpoint defined when the Azure Function is initially created.
The HTTP trigger is a POST request with the following schema and example payload,
**Schema:**
```
{
  "$schema": "http://json-schema.org/draft-04/schema#",
  "type": "object",
  "properties": {
    "Filename": {
      "type": "string"
    },
    "ContentHubHostName": {
      "type": "string"
    },
    "ContentHubToken": {
      "type": "string"
    },
    "uploadConfiguration": {
      "type": "string"
    },
    "FileSize": {
      "type": "integer"
    },
    "MediaType": {
      "type": "string"
    },
    "FileContent": {
      "type": "string"
    }
  },
  "required": [
    "Filename",
    "ContentHubHostName",
    "ContentHubToken",
    "uploadConfiguration",
    "FileSize",
    "MediaType",
    "FileContent"
  ]
}
```

**Payload**
```
{
    "Filename":"blueprints.jpg",
    "ContentHubHostName":"<sitecore-content-hub-instance>",
    "ContentHubToken":"<content-hub-access-token>",
    "uploadConfiguration":"<content-hub-upload-configuration>",
    "FileSize":1234,
    "MediaType": "image/jpeg",
    "FileContent": "<base64-encoded-payload>"
}

```

**"Filename"** - name of the file with extension being uploaded.<br />
**"ContentHubHostName"** - this is the Content Hub instance host url, i.e. https://1324.sitecoresandbox.cloud.<br />
**"ContentHubToken"** - Content Hub access token. OAuth tokens expire after 1 hour, so for account with alot of files to upload use temporary user generated token that has no expiration and after uploads have completed simply remove the token.<br />
**"ContentHubUploadConfiguration"** - Content Hub defined upload configuration. If not specified the Content Hub default **"AssetUploadConfiguration"** upload configuration will be used.<br />
**"FileSize"** - file size in bytes of file being uploaded.<br />
**"MediaType"** - Media (mime-type) of file being uploaded.<br />
**"FileContent"** - Base64 encoded contents of file being uploaded.

## Reponses
**200** - File uploaded successfully<br />
**204** - Function process completed, but no file was uploaded to Content Hub (possible because Content Hub reponse was not successful)<br />
**400** - Bad request<br />
**422** - Unprocessable entity (Azure function thew exception, file contents not base64 format, etc.)

