# StreamElements API Integration for Streamer.bot

This project integrates StreamElements API with Streamer.bot using C#. It allows Streamer.bot to send HTTP requests to the StreamElements API, handle responses, and set Streamer.bot arguments accordingly.

## Features

- Authenticate with StreamElements API using a secure JWT token.
- Send HTTP requests to various endpoints of the StreamElements API.
- Parse and handle JSON responses.
- Log and set Streamer.bot arguments based on the API responses.

## Prerequisites

- Streamer.bot
- .NET Framework
- StreamElements account

## Setup

1. **Clone the repository:**

    ```bash
    git clone https://github.com/yourusername/StreamerElementsAPI.git
    ```

2. **Add your StreamElements JWT token to Streamer.bot:**
    - Set the global variable `DPAPIEncryption.StreamElementsJwtToken` in Streamer.bot with your encrypted JWT token.
    - Set the global variable `DPAPIEncryption.Entropy` with the byte array used for encryption.

3. **Install dependencies:**
    - Newtonsoft.Json
    - Flurl.Http

    You can add these via NuGet package manager:

    ```bash
    Install-Package Newtonsoft.Json
    Install-Package Flurl.Http
    ```

## Usage

1. **Update the C# script:**
    - Copy the contents of `StreamElementsAPI.cs` and paste it into a new Execute C# Action in Streamer.bot.
    - Remove `: CPHInlineBase` from the class definition.

2. **Configure the action in Streamer.bot:**
    - Set arguments `Method`, `Path`, `Body`, `Query`, and `ParseResponse` as needed for your specific API call.

3. **Run the action:**
    - The script will log the progress and outcome of the API call.
    - If `ParseResponse` is set to `true`, the script will parse the JSON response and set Streamer.bot arguments.

## Code Overview

The main script is located in `StreamElementsAPI.cs`. Key methods include:

- `Execute()`: Main execution method triggered by Streamer.bot.
- `GetSEAccountId()`: Retrieves and caches the StreamElements account ID.
- `GetSEJwtToken()`: Retrieves and decrypts the JWT token.
- `GetStreamerBotArguments()`: Retrieves necessary arguments from Streamer.bot.
- `ProcessPath()`: Processes the API endpoint path with dynamic values.
- `CallSEApi()`: Makes the HTTP request to the StreamElements API.
- `SetStreamerBotArguments()`: Parses and sets Streamer.bot arguments from the API response.

## Logging

Logs are generated throughout the script execution to provide detailed insights into the process. These logs include:

- Start and completion of major steps.
- Details of API requests and responses.
- Errors and exceptions encountered.

## Contributing

Contributions are welcome! Please fork the repository and create a pull request with your changes.

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.

