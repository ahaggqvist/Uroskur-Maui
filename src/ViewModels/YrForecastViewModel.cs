﻿namespace Uroskur.ViewModels;

[QueryProperty(nameof(ForecastQuery), nameof(ForecastQuery))]
public partial class YrForecastViewModel : BaseViewModel
{
    private readonly IForecastService _forecastService;
    [ObservableProperty] private LineChart? _chanceOfRainLineChart;
    [ObservableProperty] private string? _emptyViewMessage;
    [ObservableProperty] private string? _forecastIssuedAt;
    [ObservableProperty] private string? _forecastIssuedFor;
    [ObservableProperty] private ForecastQuery? _forecastQuery;
    [ObservableProperty] private LineChart? _tempLineChart;
    [ObservableProperty] private LineChart? _uvLineChart;
    [ObservableProperty] private LineChart? _windLineChart;

    public YrForecastViewModel(IForecastService forecastService)
    {
        _forecastService = forecastService;
    }

    public ObservableCollection<LocationForecast> LocationForecasts { get; } = new();

    public async Task GetForecastAsync()
    {
        Title = _forecastQuery?.Routes?.Name;

        try
        {
            var today = DateTime.Today;
            if (_forecastQuery is { Day: "Tomorrow" })
            {
                today = today.AddDays(1);
            }

            var timeSpan = _forecastQuery!.Time;
            var hour = timeSpan!.Value.Hours;
            var issuedFor = today.AddHours(hour).AddMinutes(0).AddSeconds(0).ToLocalTime();
            var issuedForUnixTimestamp = DateTimeHelper.DateTimeToUnixTimestamp(issuedFor);
            var route = _forecastQuery?.Routes;
            var athlete = route?.Athlete;
            var athleteId = athlete?.Id.ToString();
            var routeId = route?.Id.ToString();
            var forecasts = await _forecastService.FindYrForecastsAsync(routeId, athleteId);

            var forecastsArray = forecasts.ToImmutableArray();
            if (forecastsArray.Length > 0)
            {
                var hourlyForecast = forecastsArray[0].HourlyForecasts.ElementAt(0);
                var issuedAt = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Local).AddSeconds(hourlyForecast.UnixTimestamp);

                _forecastIssuedAt = $"Yr Forecast Issued at {issuedAt:ddd, d MMM H:mm}";
                OnPropertyChanged(nameof(ForecastIssuedAt));


                _forecastIssuedFor = $"{issuedFor:dddd, d MMM}";
                OnPropertyChanged(nameof(ForecastIssuedFor));
            }

            foreach (var (forecast, index) in forecastsArray.WithIndex())
            {
                var km = index * 10 + 10;
                var speed = _forecastQuery!.Speed!.Value;
                var time = km / speed;
                var seconds = 3600 * time + issuedForUnixTimestamp;
                var hourlyForecast = forecast.HourlyForecasts.ToImmutableList().Find(h => Math.Abs(h.UnixTimestamp - seconds) < 0.000000001);

                if (hourlyForecast == null)
                {
                    Debug.WriteLine("Hourly forecast is null");
                    continue;
                }

                var windDeg = hourlyForecast?.WindDeg ?? 0L;
                var windIconId = WindDirection[(int)Math.Round(windDeg / 22.5, 0)];
                var locationDt = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Local)
                    .AddSeconds(3600 * ((double)km / speed) + issuedForUnixTimestamp).ToLocalTime();

                var locationForecast = new LocationForecast
                {
                    Km = km,
                    HourlyForecast = hourlyForecast!,
                    Dt = locationDt,
                    WeatherIcon = hourlyForecast!.Icon,
                    WindIcon = WindIconsDictionary[windIconId],
                    WindIconId = windIconId
                };

                LocationForecasts.Add(locationForecast);
            }

            if (LocationForecasts.Count != 0)
            {
                CreateTempChart();
                CreateChanceOfRainChart();
                CreateUvChart();
                CreateWindChart();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Unable to get routes: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            IsRefreshing = false;
        }
    }

    public void CreateTempChart()
    {
        TempLineChart = new LineChart
        {
            LineMode = LineMode.Spline,
            LineAreaAlpha = 32,
            LabelOrientation = Orientation.Horizontal,
            ValueLabelOrientation = Orientation.Horizontal,
            IsAnimated = true,
            BackgroundColor = SKColor.Parse("#fff"),
            LabelColor = SKColor.Parse("#000"),
            LabelTextSize = 25,
            ValueLabelTextSize = 25,
            SerieLabelTextSize = 25,
            LegendOption = SeriesLegendOption.Top,
            ShowYAxisLines = false,
            ShowYAxisText = false,
            EnableYFadeOutGradient = false,
            Series = new List<ChartSerie>
            {
                new()
                {
                    Name = "Temp °C",
                    Color = SKColor.Parse("#FC4C02"),
                    Entries = TempEntries()
                }
            }
        };
    }

    public void CreateChanceOfRainChart()
    {
        ChanceOfRainLineChart = new LineChart
        {
            LineMode = LineMode.Spline,
            LineAreaAlpha = 32,
            LabelOrientation = Orientation.Horizontal,
            ValueLabelOrientation = Orientation.Horizontal,
            IsAnimated = true,
            BackgroundColor = SKColor.Parse("#fff"),
            LabelColor = SKColor.Parse("#000"),
            LabelTextSize = 25,
            ValueLabelTextSize = 25,
            SerieLabelTextSize = 25,
            LegendOption = SeriesLegendOption.Top,
            ShowYAxisLines = false,
            ShowYAxisText = false,
            EnableYFadeOutGradient = false,
            Series = new List<ChartSerie>
            {
                new()
                {
                    Name = "Chance of Rain %",
                    Color = SKColor.Parse("#FC4C02"),
                    Entries = ChanceOfRainEntries()
                }
            }
        };
    }

    private void CreateUvChart()
    {
        UvLineChart = new LineChart
        {
            LineMode = LineMode.Spline,
            LineAreaAlpha = 32,
            LabelOrientation = Orientation.Horizontal,
            ValueLabelOrientation = Orientation.Horizontal,
            IsAnimated = true,
            BackgroundColor = SKColor.Parse("#fff"),
            LabelColor = SKColor.Parse("#000"),
            LabelTextSize = 25,
            ValueLabelTextSize = 25,
            SerieLabelTextSize = 25,
            LegendOption = SeriesLegendOption.Top,
            ShowYAxisLines = false,
            ShowYAxisText = false,
            EnableYFadeOutGradient = false,
            Series = new List<ChartSerie>
            {
                new()
                {
                    Name = "UVI 0 (low) to 11+ (extreme)",
                    Color = SKColor.Parse("#FC4C02"),
                    Entries = UvEntries()
                },
                new()
                {
                    Name = "Cloudiness %",
                    Color = SKColor.Parse("#4dc9fe"),
                    Entries = CloudinessEntries(false)
                }
            }
        };
    }

    private void CreateWindChart()
    {
        WindLineChart = new LineChart
        {
            LineMode = LineMode.Spline,
            LineAreaAlpha = 32,
            LabelOrientation = Orientation.Horizontal,
            ValueLabelOrientation = Orientation.Horizontal,
            IsAnimated = true,
            BackgroundColor = SKColor.Parse("#fff"),
            LabelColor = SKColor.Parse("#000"),
            LabelTextSize = 25,
            ValueLabelTextSize = 25,
            SerieLabelTextSize = 25,
            LegendOption = SeriesLegendOption.Top,
            ShowYAxisLines = false,
            ShowYAxisText = false,
            EnableYFadeOutGradient = false,
            Series = new List<ChartSerie>
            {
                new()
                {
                    Name = "Wind Speed m/s",
                    Color = SKColor.Parse("#FC4C02"),
                    Entries = WindSpeedEntries()
                },
                new()
                {
                    Name = "Wind Gust m/s",
                    Color = SKColor.Parse("#4dc9fe"),
                    Entries = WindGustEntries(false)
                }
            }
        };
    }

    private IEnumerable<ChartEntry> TempEntries(bool withLabel = true)
    {
        var chartEntries = new List<ChartEntry>();

        foreach (var hourlyForecast in LocationForecasts.Select(l => l.HourlyForecast))
        {
            var temp = Math.Round(hourlyForecast.Temp, 1);
            var chartEntry = new ChartEntry((float?)temp)
            {
                ValueLabel = temp.ToString(CultureInfo.InvariantCulture),
                Label = withLabel ? hourlyForecast.Dt.ToString("HH:mm") : null
            };

            chartEntries.Add(chartEntry);
        }

        return chartEntries;
    }

    private IEnumerable<ChartEntry> ChanceOfRainEntries(bool withLabel = true)
    {
        var chartEntries = new List<ChartEntry>();

        foreach (var hourlyForecast in LocationForecasts.Select(l => l.HourlyForecast))
        {
            var chanceOfRain = Math.Round(hourlyForecast.Pop * 100);
            var chartEntry = new ChartEntry((float?)chanceOfRain)
            {
                ValueLabel = chanceOfRain.ToString(CultureInfo.InvariantCulture),
                Label = withLabel ? hourlyForecast.Dt.ToString("HH:mm") : null
            };

            chartEntries.Add(chartEntry);
        }

        return chartEntries;
    }

    private IEnumerable<ChartEntry> CloudinessEntries(bool withLabel = true)
    {
        var chartEntries = new List<ChartEntry>();

        foreach (var hourlyForecast in LocationForecasts.Select(l => l.HourlyForecast))
        {
            var cloudiness = hourlyForecast.Cloudiness;
            var chartEntry = new ChartEntry((float?)cloudiness)
            {
                ValueLabel = cloudiness.ToString(CultureInfo.InvariantCulture),
                Label = withLabel ? hourlyForecast.Dt.ToString("HH:mm") : null
            };

            chartEntries.Add(chartEntry);
        }

        return chartEntries;
    }

    private IEnumerable<ChartEntry> UvEntries(bool withLabel = true)
    {
        var chartEntries = new List<ChartEntry>();

        foreach (var hourlyForecast in LocationForecasts.Select(l => l.HourlyForecast))
        {
            var uvi = hourlyForecast.Uvi;
            var chartEntry = new ChartEntry((float?)uvi)
            {
                ValueLabel = uvi.ToString(CultureInfo.InvariantCulture),
                Label = withLabel ? hourlyForecast.Dt.ToString("HH:mm") : null
            };

            chartEntries.Add(chartEntry);
        }

        return chartEntries;
    }

    private IEnumerable<ChartEntry> WindSpeedEntries(bool withLabel = true)
    {
        var chartEntries = new List<ChartEntry>();

        foreach (var hourlyForecast in LocationForecasts.Select(l => l.HourlyForecast))
        {
            var windSpeed = hourlyForecast.WindSpeed;
            var chartEntry = new ChartEntry((float?)windSpeed)
            {
                ValueLabel = windSpeed.ToString(CultureInfo.InvariantCulture),
                Label = withLabel ? hourlyForecast.Dt.ToString("HH:mm") : null
            };

            chartEntries.Add(chartEntry);
        }

        return chartEntries;
    }

    private IEnumerable<ChartEntry> WindGustEntries(bool withLabel = true)
    {
        var chartEntries = new List<ChartEntry>();

        foreach (var hourlyForecast in LocationForecasts.Select(l => l.HourlyForecast))
        {
            var windGust = hourlyForecast.WindGust;
            var chartEntry = new ChartEntry((float?)windGust)
            {
                ValueLabel = windGust.ToString(CultureInfo.InvariantCulture),
                Label = withLabel ? hourlyForecast.Dt.ToString("HH:mm") : null
            };

            chartEntries.Add(chartEntry);
        }

        return chartEntries;
    }
}