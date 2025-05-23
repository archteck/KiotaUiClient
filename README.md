# KiotaUiClient
KiotaUiClient is a desktop application built with Avalonia UI that provides a graphical interface for Microsoft's Kiota OpenAPI SDK generator. This tool simplifies the process of generating API client code from OpenAPI specifications.
## Overview
This application allows you to:
- Generate client code for various programming languages from OpenAPI specifications
- Update existing client code
- Refresh client code from an existing kiota-lock.json file

![image](https://github.com/user-attachments/assets/cf094356-5027-47f7-a8ef-4de78cecb317)

## Requirements
- .NET 9.0
- Microsoft.OpenAPI.Kiota (installed automatically by the application)

## Installation
Download a Release for you OS (Windows/Mac/Linux) or build it yourself:

Steps to build:
1. Clone this repository
2. Build and run the application using the .NET SDK or your preferred IDE
3. git clone https://github.com/archteck/KiotaUiClient.git
4. cd KiotaUiClient
5. dotnet build
6. dotnet run --project KiotaUiClient

## Usage
The application interface includes several fields that correspond to Kiota's command-line options:
### Input Fields
- **URL**: The URL or file path to the OpenAPI description (required)
- **Namespace**: The namespace for the generated code
- **Client Name**: The name of the generated client class
- **Language**: The target programming language for code generation
    - Supported languages: C#, Go, Java, PHP, Python, Ruby, Shell, Swift, TypeScript

- **Access Modifier**: The type access modifier (C# only)
    - Options: Public, Internal, Protected

- **Destination Folder**: The output folder where generated code will be placed (required)

### Actions
- **Browse**: Select a destination folder
- **Generate Client**: Generate a new API client
- **Update Client**: Update an existing client with the latest version of Kiota
- **Refresh Client**: Regenerate a client using configuration from kiota-lock.json, if language and access modifier(C# only) provided it will use that instead of the values in kiota-lock.json

## How It Works
1. The application manages Kiota's installation using .NET's global tool system
2. When generating a client, the application validates your inputs and calls Kiota with appropriate command-line arguments
3. The application displays the output from Kiota in the status area

## Examples
### Generating a C# Client
1. Enter the OpenAPI description URL (e.g., `https://api.example.com/openapi.json`)
2. Set the namespace (e.g., `Example.Api.Client`)
3. Set the client name (e.g., `ExampleClient`)
4. Select "C#" as the language
5. Choose an access modifier (e.g., "Public"), only if C#
6. Select a destination folder
7. Click "Generate Client"

### Updating an Existing Client
If you've previously generated a client and have a kiota-lock.json file:
1. Select the folder containing your existing client code
2. Click "Update Client(Kiota Version)"

### Refreshing an Existing Client
If you've previously generated a client and have a kiota-lock.json file:
1. Select the folder containing your existing client code
2. Click "Refresh Client"

### Disclaimer
I don't own Kiota, Kiota is a Microsoft tool, this is just a UI to consume the tool, I think the tool is so good, but some people are more used to use UI to generate the API SDK/Client.

## License
See the [LICENSE](LICENSE) file for details.

