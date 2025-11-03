using CommandLine;
using FilePrepper.CLI.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Reflection;


namespace FilePrepper.CLI;

public static class ExitCodes
{
    public const int Success = 0;
    public const int Error = 1;
    public const int InvalidHandler = 2;
    public const int ConfigurationError = 3;
    public const int ValidationError = 4;
}

public class Program
{
    private const string _toolCommandName = "fileprepper";

    private static ILoggerFactory _loggerFactory = null!;
    private static ILogger<Program> _logger = null!;

    private static readonly Dictionary<string, string> _commandDescriptions = new()
    {
        { "add-columns", "Add new columns with specified values" },
        { "aggregate", "Aggregate data based on grouping columns" },
        { "stats", "Calculate basic statistics on numeric columns" },
        { "column-interaction", "Perform operations between columns" },
        { "create-lag-features", "Create lag features from time series data" },
        { "data-sampling", "Sample data using various methods" },
        { "convert-type", "Convert data types of columns" },
        { "extract-date", "Extract components from date columns" },
        { "drop-duplicates", "Remove duplicate rows" },
        { "convert-format", "Convert file format (CSV/TSV/JSON/XML)" },
        { "fill-missing", "Fill missing values using various methods" },
        { "filter-rows", "Filter rows based on conditions" },
        { "merge", "Merge multiple CSV files" },
        { "normalize", "Normalize numeric columns" },
        { "one-hot-encoding", "Perform one-hot encoding on categorical columns" },
        { "remove-columns", "Remove specified columns" },
        { "rename-columns", "Rename columns using mapping" },
        { "reorder-columns", "Reorder columns in specified order" },
        { "scale", "Scale numeric columns" },
        { "replace", "Replace values in columns" }
    };

    static Program()
    {
        ConfigureLogging(LogLevel.Error);
    }

    private static void ConfigureLogging(LogLevel minLevel)
    {
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .AddConsole()
                .SetMinimumLevel(minLevel);
        });

        _logger = _loggerFactory.CreateLogger<Program>();
    }

    static async Task<int> Main(string[] args)
    {
        try
        {
#if DEBUG
            if (args.Length == 0)
            {
                args =
                [
                    "merge",
                    @"D:\data\ML-Research\CNC 머신 AI 데이터셋\03. Dataset_CNC\dataset\CNC 학습통합데이터_1209\X_test.csv",
                    @"D:\data\ML-Research\CNC 머신 AI 데이터셋\03. Dataset_CNC\dataset\CNC 학습통합데이터_1209\X_train.csv",
                    "-t", "Vertical",
                    "-o", @"D:\data\ML-Research\CNC 머신 AI 데이터셋\03. Dataset_CNC\dataset\CNC 학습통합데이터_1209\X_merged.csv",
                    "--has-header", "false"
                ];
            }
#endif

            // 도움말 표시 시에는 로깅 레벨을 Error로 설정
            if (args == null || args.Length == 0 || args[0] == "--help" || args[0] == "-h" ||
                (args.Length >= 2 && args[1] == "--help"))
            {
                ConfigureLogging(LogLevel.Error);
            }

            _logger.LogInformation("Application starting...");

            if (args == null || args.Length == 0 || args[0] == "--help" || args[0] == "-h")
            {   
                ShowHelp();
                return ExitCodes.Success;
            }

            var services = ConfigureServices();
            var types = LoadCommandTypes();

            _logger.LogInformation("Parsing command line arguments...");

            var parser = new Parser(config =>
            {
                config.HelpWriter = null;
                config.EnableDashDash = true;
            });

            var parserResult = parser.ParseArguments(args, types);

            return await parserResult
                .MapResult(
                    async (ICommandParameters opts) =>
                    {
                        try
                        {
                            var handlerType = opts.GetHandlerType();
                            _logger.LogInformation($"Creating handler of type: {handlerType.Name}");

                            var handler = services.GetRequiredService(handlerType) as ICommandHandler;
                            if (handler == null)
                            {
                                _logger.LogError($"Could not create command handler for type: {handlerType.Name}");
                                return ExitCodes.InvalidHandler;
                            }

                            if (args.Length >= 2 && args[1] == "--help")
                            {
                                var commandType = types.First(t => t.GetCustomAttribute<VerbAttribute>()?.Name == args[0]);
                                ShowCommandHelp(args[0], commandType);
                                return ExitCodes.Success;
                            }

                            // 기본 매개변수 검증
                            if (opts is SingleInputParameters && !ValidateParameters(opts))
                            {
                                _logger.LogError("Parameter validation failed");
                                return ExitCodes.ValidationError;
                            }

                            _logger.LogInformation($"Executing handler: {handler.GetType().Name}");
                            var result = await handler.ExecuteAsync(opts);

                            _logger.LogInformation($"Handler execution completed with result: {result}");
                            return result;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"Unexpected error during handler execution: {ex.Message}");
                            _logger.LogDebug(ex.StackTrace);
                            return ExitCodes.Error;
                        }
                    },
                    errors =>
                    {
                        LogErrors(errors);

                        string command = args[0];
                        if (_commandDescriptions.ContainsKey(command))
                        {
                            var commandType = types.First(t => t.GetCustomAttribute<VerbAttribute>()?.Name == command);
                            ShowCommandHelp(command, commandType);
                        }
                        else
                        {
                            ShowHelp();
                        }
                        return Task.FromResult(ExitCodes.ValidationError);
                    });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Fatal error: {ex.Message}");
            _logger.LogDebug(ex.StackTrace);
            return ExitCodes.Error;
        }
    }

    private static void LogErrors(IEnumerable<Error> errors)
    {
        errors.ToList().ForEach(error =>
        {
            if (error is MissingRequiredOptionError missingOptionError)
            {
                _logger.LogError($"Required option missing: {missingOptionError.NameInfo.NameText}");
            }
            else
            {
                _logger.LogError($"Error: {error.Tag} - {error}");
            }
        });
    }

    private static ServiceProvider ConfigureServices(LogLevel minLevel = LogLevel.Information)
    {
        _logger.LogInformation("Configuring services...");

        try
        {
            var services = new ServiceCollection()
                .AddLogging(builder =>
                {
                    builder
                        .AddConsole()
                        .SetMinimumLevel(minLevel);
                })
                .AddSingleton(_loggerFactory);

            RegisterCommandHandlers(services);

            var serviceProvider = services.BuildServiceProvider();
            ValidateServiceConfiguration(serviceProvider);

            return serviceProvider;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Service configuration failed: {ex.Message}");
            throw new InvalidOperationException("Failed to configure services", ex);
        }
    }

    private static string[] GetCommandExamples(string command)
    {
        try
        {
            var services = ConfigureServices(LogLevel.Error);
            var commandType = LoadCommandTypes()
                .FirstOrDefault(t => t.GetCustomAttribute<VerbAttribute>()?.Name == command);

            if (commandType == null)
                return new[] { $"  {_toolCommandName} {command} [options]" };

            var parameters = Activator.CreateInstance(commandType) as ICommandParameters;
            if (parameters == null)
                return new[] { $"  {_toolCommandName} {command} [options]" };

            var handlerType = parameters.GetHandlerType();
            var handler = services.GetRequiredService(handlerType) as ICommandHandler;
            if (handler == null)
                return new[] { $"  {_toolCommandName} {command} [options]" };

            var example = handler.GetExampleCommand();
            if (example == null)
                return new[] { $"  {_toolCommandName} {command} [options]" };

            // 개별 예시로 분리하고 각각에 tool command name 추가
            return example.Split('\n')
                .Select(ex => ex.Trim())
                .Where(ex => !string.IsNullOrEmpty(ex))
                .Select(ex => $"  {_toolCommandName} {ex}")
                .ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error getting command example");
            return new[] { $"  {_toolCommandName} {command} [options]" };
        }
    }

    private static void ShowHelp()
    {
        Console.WriteLine("\nFilePrepper - CSV File Processing Tool");
        Console.WriteLine("=====================================\n");
        Console.WriteLine("Usage: FilePrepper <command> [options]\n");
        Console.WriteLine("Available Commands:");
        Console.WriteLine("------------------");

        var maxCommandLength = _commandDescriptions.Keys.Max(k => k.Length);
        foreach (var command in _commandDescriptions.OrderBy(x => x.Key))
        {
            Console.WriteLine($"  {command.Key.PadRight(maxCommandLength + 2)} {command.Value}");
        }

        Console.WriteLine("\nFor detailed help on a specific command, use:");
        Console.WriteLine("  FilePrepper <command> --help");
        Console.WriteLine("  Example: FilePrepper aggregate --help\n");
    }

    private static void ShowCommandHelp(string command, Type commandType)
    {
        ConfigureLogging(LogLevel.Error);

        var verbAttribute = commandType.GetCustomAttribute<VerbAttribute>();
        Console.WriteLine($"\nCommand: {command}");
        Console.WriteLine($"Description: {verbAttribute?.HelpText ?? _commandDescriptions[command]}");

        // Value 속성 표시 추가
        var valueProperties = commandType.GetProperties()
            .Where(p => p.GetCustomAttribute<ValueAttribute>() != null)
            .OrderBy(p => p.GetCustomAttribute<ValueAttribute>()?.Index);

        if (valueProperties.Any())
        {
            Console.WriteLine("\nArguments:");
            foreach (var prop in valueProperties)
            {
                var val = prop.GetCustomAttribute<ValueAttribute>();
                if (val == null) continue;

                var metaName = val.MetaName ?? prop.Name;
                var required = val.Required ? " [required]" : "";
                Console.WriteLine($"  {metaName.PadRight(20)} {val.HelpText}{required}");
            }
        }

        Console.WriteLine("\nCommon Options:");
        Console.WriteLine("  --ignore-errors         Whether to ignore errors during processing");
        Console.WriteLine("      Default: false");
        Console.WriteLine("  --default-value         Default value to use when encountering errors");
        Console.WriteLine("  --has-header           Whether input files have headers");
        Console.WriteLine("      Default: true");

        Console.WriteLine("\nCommand-Specific Options:");


        var optionProperties = commandType.GetProperties()
            .Where(p => p.GetCustomAttribute<OptionAttribute>() != null)
            .OrderBy(p => p.GetCustomAttribute<OptionAttribute>()?.Required == true ? 0 : 1);

        foreach (var prop in optionProperties)
        {
            var opt = prop.GetCustomAttribute<OptionAttribute>();
            if (opt == null) continue;

            var shortName = !string.IsNullOrEmpty(opt.ShortName) ? $"-{opt.ShortName}," : "   ";
            var longName = $"--{opt.LongName}";
            var required = opt.Required ? " [required]" : "";

            Console.WriteLine($"  {shortName} {longName.PadRight(20)} {opt.HelpText}{required}");

            if (opt.Default != null && opt.Default.ToString() != "")
            {
                Console.WriteLine($"      Default: {opt.Default}");
            }
        }

        Console.WriteLine("\nExamples:");
        var examples = GetCommandExamples(command);
        foreach (var example in examples)
        {
            Console.WriteLine(example);
        }

        Console.WriteLine("\nWith common options:");
        Console.WriteLine($"  ...--ignore-errors --default-value NA --has-header true");
    }

    private static ServiceProvider ConfigureServices()
    {
        _logger.LogInformation("Configuring services...");

        try
        {
            var services = new ServiceCollection()
                .AddLogging(builder =>
                {
                    builder
                        .AddConsole()
                        .SetMinimumLevel(LogLevel.Information);
                })
                .AddSingleton<ILoggerFactory, LoggerFactory>();

            RegisterCommandHandlers(services);

            var serviceProvider = services.BuildServiceProvider();

            // 서비스 구성 유효성 검사
            ValidateServiceConfiguration(serviceProvider);

            return serviceProvider;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Service configuration failed: {ex.Message}");
            throw new InvalidOperationException("Failed to configure services", ex);
        }
    }

    private static void RegisterCommandHandlers(IServiceCollection services)
    {
        _logger.LogInformation("Registering command handlers...");

        var assembly = typeof(Program).Assembly;
        var handlerTypes = assembly.GetTypes()
            .Where(t => !t.IsAbstract &&
                       !t.IsInterface &&
                       typeof(ICommandHandler).IsAssignableFrom(t))
            .ToList();

        foreach (var handlerType in handlerTypes)
        {
            _logger.LogDebug($"Registering handler: {handlerType.Name}");
            services.AddTransient(handlerType);
        }

        _logger.LogInformation($"Registered {handlerTypes.Count} command handlers");
    }

    private static void ValidateServiceConfiguration(ServiceProvider serviceProvider)
    {
        _logger.LogInformation("Validating service configuration...");

        // 필수 서비스 존재 여부 확인
        var requiredServices = new[]
        {
            typeof(ILoggerFactory),
            // 다른 필수 서비스들을 여기에 추가
        };

        foreach (var serviceType in requiredServices)
        {
            if (serviceProvider.GetService(serviceType) == null)
            {
                throw new InvalidOperationException($"Required service {serviceType.Name} is not registered");
            }
        }
    }

    private static Type[] LoadCommandTypes()
    {
        _logger.LogInformation("Loading command types...");

        try
        {
            var assembly = typeof(Program).Assembly;
            var types = assembly.GetTypes()
                .Where(t => !t.IsAbstract &&
                           !t.IsInterface &&
                           typeof(ICommandParameters).IsAssignableFrom(t))
                .ToArray();

            _logger.LogInformation($"Loaded {types.Length} command types");
            return types;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to load command types: {ex.Message}");
            throw new InvalidOperationException("Failed to load command types", ex);
        }
    }

    private static bool ValidateParameters(ICommandParameters parameters)
    {
        if (parameters == null)
        {
            _logger.LogError("Parameters object is null");
            return false;
        }

        return parameters.Validate(_logger);
    }

}