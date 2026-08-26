// ReSharper disable All
#pragma warning disable CS8618, CA1822
#if !ENABLE_SENTRY

internal class SentryHttpMessageHandler(HttpClientHandler handler) : DelegatingHandler(handler);

internal class SentrySdk {

    public static void Init(Action<SentrySdk>? configureOptions) { }

    public static SentrySdk StartTransaction(string name, string operation) => new ();

    public static void ConfigureScope<TArg>(Action<SentrySdk, TArg> configureScope, TArg arg) { }

    public static void CaptureException(Exception exception) { }

    public void Finish() { }

    public SentrySdk? Transaction { get; set; }
    
    public string? Release { get; set; }

    public string Dsn { get; set; }
    
    public bool Debug { get; set; }
    
    public double TracesSampleRate { get; set; }
    
    public bool AutoSessionTracking { get; set; }
    
    public string CacheDirectoryPath { get; set; }

    public void SetBeforeSend(Func<SentrySdk, object> a) { }

    public void SetBeforeSendTransaction(Func<SentrySdk, object> a) { }

    public static void AddBreadcrumb(string a, string b) { }

}

#endif
