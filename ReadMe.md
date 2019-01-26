# FMPostToBlogger

FMPostToBlogger is a Windows command-line utility that will send new blog posts to Google Blogger. Submissions can be individual JPEG photos or full posts written in MarkDown.

JPEG support is for photo blogs - each post consists of a title, a photo, and optional comment. For these posts, take a JPEG image and edit the metadata, giving it a title and, optionally, a comment. On Windows, you can do this by right-clicking on the photo, selecting "Properties", selecting the "Details" tab, and then editing the Title and Comments fields.

I use this form for producing [Brandt's Bollard Blog](https://bollard.brandtredd.com).

MarkDown support is for more extensive blogging when you want offline composition and better control over the content than Blogger's built-in editor. You can write your post in MarkDown and include images from your local drive. Any images you include will be uploaded to Blogger along with the content.

Invoke "FMPostToBlogger -h" to view the syntax and help text. It requires your Google credentials to gain permission to access the Blogger APIs. One command can upload multiple blog posts.

## Details

FMPostToBlogger is written in C# and built using Microsoft Visual Studio 2017.