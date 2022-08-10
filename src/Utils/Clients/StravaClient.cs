﻿using Uroskur.Models.Strava;

namespace Uroskur.Utils.Clients;

public class StravaClient : IStravaClient
{
    private const string GrantTypeAuthorizationCode = "authorization_code";
    private const string GrantTypeRefreshToken = "refresh_token";
    private const int MaxRetryAttempts = 3;
    private const int PauseBetweenFailures = 2;
    private readonly AppSettings? _appSettings;
    private readonly HttpClient? _httpClient;

    public StravaClient(AppSettings? appSettings, HttpClient? httpClient)
    {
        _appSettings = appSettings;
        _httpClient = httpClient;
    }

    public async Task<AuthorizationToken?> GetAuthorizationTokenAsync(string? clientId,
        string? clientSecret)
    {
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
        {
            throw new ArgumentException("Client ID or Client secret are invalid.");
        }

        var authorizationTokenMobileUrl = _appSettings?.StravaAuthorizationTokenMobileUrl;
        if (string.IsNullOrEmpty(authorizationTokenMobileUrl))
        {
            throw new ArgumentException("Authorization token mobile url is invalid.");
        }

        var authorizationRedirectUrl = _appSettings?.StravaAuthorizationRedirectUrl;
        if (string.IsNullOrEmpty(authorizationRedirectUrl))
        {
            throw new ArgumentException("Authorization redirect url is invalid.");
        }

        var stravaAuthorizationTokenUrl = _appSettings?.StravaAuthorizationTokenUrl;
        if (string.IsNullOrEmpty(stravaAuthorizationTokenUrl))
        {
            throw new ArgumentException("Strava authorization token url is invalid.");
        }

        authorizationTokenMobileUrl = authorizationTokenMobileUrl.Replace("@ClientId", clientId).Replace("@RedirectUri", authorizationRedirectUrl)
            .Replace("@RequestScope", "read_all");

        try
        {
            var authenticatorResult = await WebAuthenticator.AuthenticateAsync(
                new Uri(authorizationTokenMobileUrl),
                new Uri(authorizationRedirectUrl));

            if (authenticatorResult == null)
            {
                throw new ArgumentException("Authenticator request failed.");
            }

            var code = authenticatorResult.Properties["code"];
            if (!authenticatorResult.Properties.ContainsKey("code"))
            {
                throw new ArgumentException("Code is blank.");
            }

            var formUrlEncodedContent = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "client_id", clientId },
                { "client_secret", clientSecret },
                { "code", code },
                { "grant_type", GrantTypeAuthorizationCode }
            });

            var response = await _httpClient?.PostAsync(stravaAuthorizationTokenUrl, formUrlEncodedContent)!;
            response.EnsureSuccessStatusCode();
            var responseBody = await response.Content.ReadAsStringAsync();

            return !string.IsNullOrEmpty(responseBody) ? AuthorizationToken.FromJson(responseBody) : null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Get authorization token async failed: {ex.Message} {ex.StackTrace}");
        }

        return null;
    }

    public async Task<string?> CreateSubscriptionAsync(string? clientId, string? clientSecret)
    {
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
        {
            throw new ArgumentException("Client ID and Client secret are invalid.");
        }

        var callbackUrl = _appSettings?.StravaCallbackUrl;
        if (string.IsNullOrEmpty(callbackUrl))
        {
            throw new ArgumentException("Callback url is invalid.");
        }

        var subscriptionUrl = _appSettings?.StravaSubscriptionUrl;
        if (string.IsNullOrEmpty(subscriptionUrl))
        {
            throw new ArgumentException("Subscription url is invalid.");
        }

        var formUrlEncodedContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "client_id", clientId },
            { "client_secret", clientSecret },
            { "callback_url", callbackUrl },
            { "verify_token", "uroskur" }
        });

        var response = await _httpClient?.PostAsync(subscriptionUrl, formUrlEncodedContent)!;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
    }

    public async Task<bool?> DeleteSubscriptionAsync(string? clientId, string? clientSecret)
    {
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
        {
            throw new ArgumentException("Client ID and Client secret are invalid.");
        }

        var subscriptionUrl = _appSettings?.StravaSubscriptionUrl;
        if (string.IsNullOrEmpty(subscriptionUrl))
        {
            throw new ArgumentException("Subscription url is invalid.");
        }

        var subscription = await ViewSubscriptionAsync(clientId, clientSecret);

        var pauseBetweenFailures = TimeSpan.FromSeconds(PauseBetweenFailures);
        var retryPolicy = Policy
            .Handle<HttpRequestException>()
            .WaitAndRetryAsync(MaxRetryAttempts, _ => pauseBetweenFailures);

        using var request = new HttpRequestMessage(HttpMethod.Delete,
            $"{subscriptionUrl}/{subscription?.Id}?client_id={clientId}&client_secret={clientSecret}");

        var isDeleted = false;
        await retryPolicy.ExecuteAsync(async () =>
        {
            var response = await GetResponseAsync(request);
            if (response.StatusCode == HttpStatusCode.NoContent)
            {
                isDeleted = true;
            }
        });

        return isDeleted;
    }

    public async Task<Subscription?> ViewSubscriptionAsync(string? clientId, string? clientSecret)
    {
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
        {
            throw new ArgumentException("Client ID and Client secret are invalid.");
        }

        var subscriptionUrl = _appSettings?.StravaSubscriptionUrl;
        if (string.IsNullOrEmpty(subscriptionUrl))
        {
            throw new ArgumentException("Subscription url is invalid.");
        }

        var pauseBetweenFailures = TimeSpan.FromSeconds(PauseBetweenFailures);
        var retryPolicy = Policy
            .Handle<HttpRequestException>()
            .WaitAndRetryAsync(MaxRetryAttempts, _ => pauseBetweenFailures);

        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"{subscriptionUrl}?client_id={clientId}&client_secret={clientSecret}");

        var subscriptions = new List<Subscription>();
        await retryPolicy.ExecuteAsync(async () =>
        {
            var response = await GetResponseAsync(request);
            if (response.IsSuccessStatusCode)
            {
                subscriptions = Subscription.FromJson(await response.Content.ReadAsStringAsync());
            }
        });

        return subscriptions.FirstOrDefault();
    }

    public async Task<AuthorizationToken?> GetRefreshTokenAsync(string? refreshToken, string? clientId,
        string? clientSecret)
    {
        if (string.IsNullOrEmpty(refreshToken) || string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
        {
            throw new ArgumentException("Refresh token, Client ID and Client secret are invalid.");
        }

        var authorizationTokenUrl = _appSettings?.StravaAuthorizationTokenUrl;
        if (string.IsNullOrEmpty(authorizationTokenUrl))
        {
            throw new ArgumentException("Authorization token url is invalid.");
        }

        var formUrlEncodedContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "client_id", clientId },
            { "client_secret", clientSecret },
            { "grant_type", GrantTypeRefreshToken },
            { "refresh_token", refreshToken }
        });

        var response = await _httpClient?.PostAsync(authorizationTokenUrl, formUrlEncodedContent)!;
        response.EnsureSuccessStatusCode();
        var responseBody = await response.Content.ReadAsStringAsync();

        return !string.IsNullOrEmpty(responseBody) ? AuthorizationToken.FromJson(responseBody) : null;
    }

    public async Task<IEnumerable<Routes>> GetRoutesAsync(string? athleteId, string? authorizationToken)
    {
        if (string.IsNullOrEmpty(authorizationToken))
        {
            throw new ArgumentException("Authorization token is invalid.");
        }

        var routesUrl = _appSettings?.StravaRoutesUrl;
        if (string.IsNullOrEmpty(routesUrl))
        {
            throw new ArgumentException("Routes url is invalid.");
        }

        var pauseBetweenFailures = TimeSpan.FromSeconds(PauseBetweenFailures);
        var retryPolicy = Policy
            .Handle<HttpRequestException>()
            .WaitAndRetryAsync(MaxRetryAttempts, _ => pauseBetweenFailures);

        var url = routesUrl.Replace("@AthleteId", athleteId);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authorizationToken);

        var routes = new List<Routes>();
        await retryPolicy.ExecuteAsync(async () =>
        {
            var response = await GetResponseAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var routesList = Routes.FromJson(await response.Content.ReadAsStringAsync());
                if (routesList != null)
                {
                    routes.AddRange(routesList);
                }
            }
        });

        return routes;
    }

    public async Task<string> GetGxpAsync(string? routeId, string? authorizationToken)
    {
        if (string.IsNullOrEmpty(authorizationToken))
        {
            throw new ArgumentException("Authorization token is invalid.");
        }

        var gxpUrl = _appSettings?.StravaGxpUrl;
        if (string.IsNullOrEmpty(gxpUrl))
        {
            throw new ArgumentException("Gxp url is invalid.");
        }

        var pauseBetweenFailures = TimeSpan.FromSeconds(PauseBetweenFailures);
        var retryPolicy = Policy
            .Handle<HttpRequestException>()
            .WaitAndRetryAsync(MaxRetryAttempts, _ => pauseBetweenFailures);

        var url = gxpUrl.Replace("@RouteId", routeId);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authorizationToken);

        var xml = string.Empty;
        await retryPolicy.ExecuteAsync(async () =>
        {
            var response = await GetResponseAsync(request);
            if (response.IsSuccessStatusCode)
            {
                xml = await response.Content.ReadAsStringAsync();
            }
        });

        return xml;
    }

    private Task<HttpResponseMessage> GetResponseAsync(HttpRequestMessage url)
    {
        return _httpClient?.SendAsync(url)!;
    }
}