using Microsoft.AspNetCore.Http;
using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace joelbyford
{
    /// <summary>
    /// This middleware performs basic authentication.
    /// For details on basic authentication see RFC 2617.
    /// Based on the work by DevBridge (https://github.com/devbridge/) and Mukesh Murugan (https://github.com/iammukeshm/webapi-basic-authentication-middleware-asp.net-core-3.1)
    ///
    /// The basic operational flow is:
    ///
    /// On AuthenticateRequest:
    ///     extract the basic authentication credentials
    ///     verify the credentials
    ///     if successfull, create and send authentication cookie
    ///
    /// On SendResponseHeaders:
    ///     if there is no authentication cookie in request, clear response, add unauthorized status code (401) and
    ///     add the basic authentication challenge to trigger basic authentication.
    /// </summary>

    public class BasicAuth
    {
        /// <summary>
        /// HTTP1.1 Authorization header
        /// </summary>
        public const string HttpAuthorizationHeader = "Authorization";

        /// <summary>
        /// HTTP1.1 Basic Challenge Scheme Name
        /// </summary>
        public const string HttpBasicSchemeName = "Basic"; //

        /// <summary>
        /// HTTP1.1 Credential username and password separator
        /// </summary>
        public const char HttpCredentialSeparator = ':';

        /// <summary>
        /// HTTP1.1 Basic Challenge Scheme Name
        /// </summary>
        public const string HttpWwwAuthenticateHeader = "WWW-Authenticate";


        /// <summary>
        /// The credentials that are allowed to access the site.
        /// </summary>
        private readonly Dictionary<string, string> activeUsers;

        /// <summary>
        /// Required for asp.net core middleware to function right
        /// </summary>
        private readonly RequestDelegate next;
        private readonly string realm;
        private readonly object throttleLock = new object();
        private readonly Dictionary<string, Queue<DateTimeOffset>> failedAttemptsByIp = new Dictionary<string, Queue<DateTimeOffset>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Queue<DateTimeOffset>> failedAttemptsByUser = new Dictionary<string, Queue<DateTimeOffset>>(StringComparer.Ordinal);
        private readonly Dictionary<string, DateTimeOffset> lockedOutIps = new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DateTimeOffset> lockedOutUsers = new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal);

        private const int MaxFailedAttemptsPerIp = 10;
        private const int MaxFailedAttemptsPerUser = 5;
        private static readonly TimeSpan FailedAttemptWindow = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(10);


        /// <summary>
        /// Constructor Option A - Provide Single User with Password 
        /// - Provide a username and a password for a single user
        /// </summary>
        public BasicAuth(RequestDelegate next, string realm, string username, string password)
        {
            this.next = next;
            this.realm = realm;
            // Add a single user with the username and password
            activeUsers = new Dictionary<string, string>();
            activeUsers.Add(username, password);
        }

        /// <summary>
        /// Constructor Option B - Provide Dictionary  
        /// - Provide a simple dictionary object with a list of all the authorized users:
        /// - Dictionary<string, string>
        /// </summary>
        public BasicAuth(RequestDelegate next, string realm, Dictionary<string, string> userDict)
        {
            this.next = next;
            this.realm = realm;
            activeUsers = userDict;
        }

        /// <summary>
        /// Class entry point called by the middleware pipeline
        /// </summary>
        public async Task Invoke(HttpContext context)
        {
            if (!IsSecureRequest(context))
            {
                WriteUnauthorizedResponse(context);
                return;
            }

            string clientKey = GetClientKey(context);
            DateTimeOffset now = DateTimeOffset.UtcNow;

            if (IsLockedOut(clientKey, null, now))
            {
                WriteUnauthorizedResponse(context);
                return;
            }

            if (!TryGetCredentials(context, out string username, out string password))
            {
                WriteUnauthorizedResponse(context);
                return;
            }

            if (IsLockedOut(clientKey, username, now))
            {
                WriteUnauthorizedResponse(context);
                return;
            }

            if (IsAuthorized(username, password))
            {
                ClearFailedAttempts(clientKey, username);
                await next.Invoke(context);
                return;
            }

            RegisterFailedAttempt(clientKey, username, now);
            WriteUnauthorizedResponse(context);
        }

        public bool IsAuthorized(string username, string password)
        {
            if (!activeUsers.TryGetValue(username, out string expectedPassword))
            {
                return false;
            }

            return SecureEquals(expectedPassword, password);
        }

        private bool TryGetCredentials(HttpContext context, out string username, out string password)
        {
            username = string.Empty;
            password = string.Empty;

            if (!context.Request.Headers.TryGetValue(HttpAuthorizationHeader, out var authorizationValues))
            {
                return false;
            }

            string authorizationHeader = authorizationValues.ToString();
            if (string.IsNullOrWhiteSpace(authorizationHeader))
            {
                return false;
            }

            if (!authorizationHeader.StartsWith(HttpBasicSchemeName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string[] schemeAndToken = authorizationHeader.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (schemeAndToken.Length != 2)
            {
                return false;
            }

            string encodedCredentials = schemeAndToken[1].Trim();
            if (string.IsNullOrWhiteSpace(encodedCredentials))
            {
                return false;
            }

            byte[] decodedBytes;
            try
            {
                decodedBytes = Convert.FromBase64String(encodedCredentials);
            }
            catch (FormatException)
            {
                return false;
            }

            string decodedCredentials = Encoding.UTF8.GetString(decodedBytes);
            int separatorIndex = decodedCredentials.IndexOf(HttpCredentialSeparator);
            if (separatorIndex <= 0 || separatorIndex == decodedCredentials.Length - 1)
            {
                return false;
            }

            username = decodedCredentials.Substring(0, separatorIndex);
            password = decodedCredentials.Substring(separatorIndex + 1);

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                return false;
            }

            return true;
        }

        private void WriteUnauthorizedResponse(HttpContext context)
        {
            context.Response.Headers[HttpWwwAuthenticateHeader] = BuildAuthenticateHeaderValue();
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
        }

        private string BuildAuthenticateHeaderValue()
        {
            if (string.IsNullOrWhiteSpace(realm))
            {
                return HttpBasicSchemeName;
            }

            return $"{HttpBasicSchemeName} realm=\"{realm}\"";
        }

        private bool IsSecureRequest(HttpContext context)
        {
            if (context.Request.IsHttps)
            {
                return true;
            }

            if (context.Request.Headers.TryGetValue("X-Forwarded-Proto", out var forwardedProtoValues))
            {
                string firstForwardedProto = forwardedProtoValues.ToString()
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(value => value.Trim())
                    .FirstOrDefault();

                if (string.Equals(firstForwardedProto, "https", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private string GetClientKey(HttpContext context)
        {
            if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedForValues))
            {
                string forwardedFor = forwardedForValues.ToString()
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(value => value.Trim())
                    .FirstOrDefault();

                if (!string.IsNullOrWhiteSpace(forwardedFor))
                {
                    return forwardedFor;
                }
            }

            return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }

        private bool IsLockedOut(string clientKey, string username, DateTimeOffset now)
        {
            lock (throttleLock)
            {
                if (IsActiveLockout(lockedOutIps, clientKey, now))
                {
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(username) && IsActiveLockout(lockedOutUsers, username, now))
                {
                    return true;
                }

                return false;
            }
        }

        private static bool IsActiveLockout(Dictionary<string, DateTimeOffset> lockouts, string key, DateTimeOffset now)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            if (!lockouts.TryGetValue(key, out DateTimeOffset lockoutUntil))
            {
                return false;
            }

            if (lockoutUntil <= now)
            {
                lockouts.Remove(key);
                return false;
            }

            return true;
        }

        private void RegisterFailedAttempt(string clientKey, string username, DateTimeOffset now)
        {
            lock (throttleLock)
            {
                RegisterFailedAttemptForKey(failedAttemptsByIp, lockedOutIps, clientKey, MaxFailedAttemptsPerIp, now);

                if (!string.IsNullOrWhiteSpace(username))
                {
                    RegisterFailedAttemptForKey(failedAttemptsByUser, lockedOutUsers, username, MaxFailedAttemptsPerUser, now);
                }
            }
        }

        private void RegisterFailedAttemptForKey(
            Dictionary<string, Queue<DateTimeOffset>> attemptsByKey,
            Dictionary<string, DateTimeOffset> lockoutsByKey,
            string key,
            int maxAttempts,
            DateTimeOffset now)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            Queue<DateTimeOffset> attempts = GetOrCreateAttemptQueue(attemptsByKey, key);
            PruneAttempts(attempts, now);
            attempts.Enqueue(now);

            if (attempts.Count >= maxAttempts)
            {
                lockoutsByKey[key] = now.Add(LockoutDuration);
                attempts.Clear();
            }
        }

        private static Queue<DateTimeOffset> GetOrCreateAttemptQueue(Dictionary<string, Queue<DateTimeOffset>> attemptsByKey, string key)
        {
            if (!attemptsByKey.TryGetValue(key, out Queue<DateTimeOffset> attempts))
            {
                attempts = new Queue<DateTimeOffset>();
                attemptsByKey[key] = attempts;
            }

            return attempts;
        }

        private static void PruneAttempts(Queue<DateTimeOffset> attempts, DateTimeOffset now)
        {
            while (attempts.Count > 0 && now - attempts.Peek() > FailedAttemptWindow)
            {
                attempts.Dequeue();
            }
        }

        private void ClearFailedAttempts(string clientKey, string username)
        {
            lock (throttleLock)
            {
                failedAttemptsByIp.Remove(clientKey);
                lockedOutIps.Remove(clientKey);

                if (!string.IsNullOrWhiteSpace(username))
                {
                    failedAttemptsByUser.Remove(username);
                    lockedOutUsers.Remove(username);
                }
            }
        }

        private static bool SecureEquals(string left, string right)
        {
            byte[] leftHash = SHA256.HashData(Encoding.UTF8.GetBytes(left ?? string.Empty));
            byte[] rightHash = SHA256.HashData(Encoding.UTF8.GetBytes(right ?? string.Empty));

            bool result = CryptographicOperations.FixedTimeEquals(leftHash, rightHash);

            CryptographicOperations.ZeroMemory(leftHash);
            CryptographicOperations.ZeroMemory(rightHash);

            return result;
        }
    }
}