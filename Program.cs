using Streamer.bot.Plugin.Interface;
using Streamer.bot.Plugin.Interface.Enums;
using Streamer.bot.Plugin.Interface.Model;
using System;
using System.Net;
using System.Net.Http;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Security;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Flurl;
using Flurl.Http;

public class CPHInline : CPHInlineBase // Remove " : CPHInlineBase" when pasting code into streamer.bot
{
    private readonly string LogTag = "[StreamElements API Send HTTP Request]";
    private byte[] Entropy =>
        CPH.GetGlobalVar<byte[]>("DPAPIEncryption.Entropy", true);
    private string SEEncryptedJwtToken;
    private SecureString SESecureJwtToken;
    private string SEAccountId;
    private readonly string SEBaseUrl = "https://api.streamelements.com/kappa";
    private static readonly FlurlClient client = new();

    public bool Execute()
    {
        try
        {
            CPH.LogInfo($"{LogTag} Execution started.");

            GetSEJwtToken();
            GetSEAccountId();
            GetStreamerBotArguments(out var method, out var path, out var body, out var query, out var parseResponse);

            var rawResponse = CallSEApi(method, path, body, query, true).GetAwaiter().GetResult();

            if (parseResponse)
            {
                CPH.LogInfo($"{LogTag} SetStreamerBotArguments started.");
                try
                {
                    SetStreamerBotArguments(JToken.Parse(rawResponse), "StreamElements.ParsedResponse");
                }
                catch (JsonReaderException)
                {
                    CPH.LogInfo($"{LogTag} Response is not a valid JSON, storing as plain text.");
                    CPH.SetArgument("StreamElements.ParsedResponse", rawResponse);
                }
                CPH.LogInfo($"{LogTag} SetStreamerBotArguments complated.");
            }

            CPH.LogInfo($"{LogTag} Execution completed.");
            return true;
        }
        catch (Exception ex)
        {
            CPH.LogError($"{LogTag} Execution failed: {ex.Message}");
            return false;
        }
    }

    private void GetSEAccountId()
    {
        if (SEAccountId == null)
        {
            CPH.LogInfo($"{LogTag} SEAccountId is null; fetching value.");
            var result = CallSEApi("Get", "/v2/channels/me", null, null, false).GetAwaiter().GetResult();
            dynamic json = JsonConvert.DeserializeObject(result);
            SEAccountId = json._id;
            CPH.LogInfo($"{LogTag} GetSEAccountId completed.");
        }
    }

    private void GetSEJwtToken()
    {
        CPH.LogInfo($"{LogTag} GetSEJwtToken started.");
        var encryptedToken = CPH.GetGlobalVar<string>("DPAPIEncryption.StreamElementsJwtToken", true);
        if (string.IsNullOrEmpty(encryptedToken))
            throw new Exception("Global variable 'DPAPIEncryption.StreamElementsJwtToken' was null or empty.");
        if (SEEncryptedJwtToken == null || SEEncryptedJwtToken != encryptedToken)
        {
            CPH.LogInfo($"{LogTag} Updating value for SEEncryptedJwtToken and SESecureJwtToken.");
            SEEncryptedJwtToken = encryptedToken;
            SESecureJwtToken = DecryptSecretToSecureString(SEEncryptedJwtToken, Entropy);
        }
        CPH.LogInfo($"{LogTag} GetSEJwtToken completed.");
    }

    private void GetStreamerBotArguments(out string method, out string path, out string body, out string query, out bool parseResponse)
    {
        CPH.LogInfo($"{LogTag} GetStreamerBotArguments started.");

        if (!CPH.TryGetArg("Method", out method) || string.IsNullOrEmpty(method))
        {
            CPH.LogInfo($"{LogTag} Method not provided; defaulting to GET.");
            method = HttpMethod.Get.Method;
        }
        method = method.ToUpper();
        path = ProcessPath();
        CPH.TryGetArg("Body", out body);
        CPH.TryGetArg("Query", out query);
        if (!CPH.TryGetArg("ParseResponse", out parseResponse))
            parseResponse = false;

        CPH.LogInfo($"{LogTag} Fetched Streamerbot Args: Method: '{method}' path: '{path}' body: '{body}' query: '{query}'.");
        CPH.LogInfo($"{LogTag} GetStreamerBotArguments completed.");
    }

    private string ProcessPath()
    {
        CPH.LogInfo($"{LogTag} ProcessPath started.");

        if (!CPH.TryGetArg<string>("Path", out var path))
            throw new Exception("Path was null, empty, or not provided.");
        path = path.TrimStart('/');

        var regex = new Regex(@"\{(?<Property>[^}]+)\}");

        foreach (Match match in regex.Matches(path))
        {
            var property = match.Groups["Property"].Value;
            string replacementValue;

            if (property.Equals("channel", StringComparison.OrdinalIgnoreCase))
                replacementValue = SEAccountId;
            else
            {
                var keyValuePair = args.FirstOrDefault(kv => kv.Key.Equals(property, StringComparison.OrdinalIgnoreCase));
                if (!keyValuePair.Equals(default(KeyValuePair<string, object>)))
                    replacementValue = keyValuePair.Value.ToString();
                else
                    throw new Exception($"No matching value found for placeholder '{property}' in args.");
            }
            path = path.Replace(match.Value, replacementValue);
        }

        CPH.LogInfo($"{LogTag} ProcessPath completed.");
        return path;
    }

    private async Task<string> CallSEApi(string method, string path, string body, string query, bool setArgs)
    {
        CPH.LogInfo($"{LogTag} CallSEApi started.");

        // Construct the endpoint URL using Flurl
        string endpointUrl = SEBaseUrl
            .AppendPathSegment(path)
            .SetQueryParams(
                !string.IsNullOrEmpty(query)
                    ? JsonConvert.DeserializeObject<Dictionary<string, string>>(query)
                    : new Dictionary<string, string>()
            );

        // Log the constructed URL
        var logMessage = $"{LogTag} Calling endpoint: {method} {endpointUrl}";
        if (!string.IsNullOrEmpty(body))
            logMessage += $" with body: '{body}'";
        CPH.LogInfo(logMessage);

        string rawResponse = null;
        int statusCode = -1;
        string statusMessage = "No response";
        bool isSuccessful = false;

        try
        {
            var response = await client
                .Request(endpointUrl)
                .WithOAuthBearerToken(ConvertSecureStringToString(SESecureJwtToken))
                .SendAsync(new HttpMethod(method), body != null ? new StringContent(body, Encoding.UTF8, "application/json") : null);

            rawResponse = await response.Content.ReadAsStringAsync();
            statusCode = (int)response.StatusCode;
            statusMessage = response.StatusCode.ToString();
            isSuccessful = response.IsSuccessStatusCode;
        }
        catch (FlurlHttpException ex)
        {
            rawResponse = ex.Call.Response != null ? await ex.Call.Response.Content.ReadAsStringAsync() : "No response";
            statusCode = ex.Call.Response != null ? (int)ex.Call.Response.StatusCode : -1;
            statusMessage = ex.Call.Response != null ? ex.Call.Response.StatusCode.ToString() : "No response";
            isSuccessful = false;
        }
        finally
        {
            // Truncate response string for logging if necessary
            var truncatedResponseString = rawResponse.Length > 250
                ? $"{rawResponse.Substring(0, 250)}..."
                : rawResponse;
            CPH.LogInfo($"{LogTag} HttpMethod: {method}");
            CPH.LogInfo($"{LogTag} EndpointUrl: {endpointUrl}");
            CPH.LogInfo($"{LogTag} StatusCode: {statusCode}");
            CPH.LogInfo($"{LogTag} StatusMessage: {statusMessage}");
            CPH.LogInfo($"{LogTag} IsSuccessful: {isSuccessful}");
            CPH.LogInfo($"{LogTag} RawResponse: {truncatedResponseString}");
            if (setArgs)
            {
                CPH.SetArgument("StreamElements.HttpMethod", method);
                CPH.SetArgument("StreamElements.EndpointUrl", endpointUrl);
                CPH.SetArgument("StreamElements.StatusCode", statusCode);
                CPH.SetArgument("StreamElements.StatusMessage", statusMessage);
                CPH.SetArgument("StreamElements.IsSuccessful", isSuccessful);
                CPH.SetArgument("StreamElements.RawResponse", rawResponse);
            }
        }

        CPH.LogInfo($"{LogTag} CallSEApi completed.");
        return rawResponse;
    }

    private void SetStreamerBotArguments(JToken token, string prefix)
    {
        if (token.Type == JTokenType.Object)
            ((JObject)token).Properties().ToList().ForEach(property =>
                SetStreamerBotArguments(property.Value, $"{prefix}.{property.Name}"));
        else if (token.Type == JTokenType.Array)
            ((JArray)token).Select((value, index) => new { Value = value, Index = index }).ToList().ForEach(element =>
                SetStreamerBotArguments(element.Value, $"{prefix}[{element.Index}]"));
        else
            CPH.SetArgument(prefix, token.ToString());
    }

    private static SecureString DecryptSecretToSecureString(string encryptedSecret, byte[] entropy) =>
        new NetworkCredential(
            "",
            Encoding.UTF8.GetString(
                ProtectedData.Unprotect(
                    Convert.FromBase64String(encryptedSecret),
                    entropy,
                    DataProtectionScope.CurrentUser
                )
            )
        ).SecurePassword;

    private static string ConvertSecureStringToString(SecureString theSecureString) =>
        new NetworkCredential("", theSecureString).Password;
}