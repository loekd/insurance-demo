using System.CommandLine;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace XpiritInsurance.DaprLauncher
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var assemblyVersion = assembly.GetName().Version;
            Console.WriteLine($"AssemblyVersion {assemblyVersion}");

            //required value for monitored process id
            var monitoredProcessIdOption = new Option<int>
                (name: "--monitored-process-id",
                description: "Id of monitored process.")
            {
                IsRequired = true
            };
            monitoredProcessIdOption.AddAlias("-mpid");

            //with attach -- required value for sidecar process id
            var daprSideCarProcessIdOption = new Option<int>
                (name: "--sidecar-process-id",
                description: "Process Id of running Dapr sidecar process.")
            {
                IsRequired = true
            };
            daprSideCarProcessIdOption.AddAlias("-scpid");

            //with create
            var monitoredProcessPortOption = new Option<int>
                (name: "--app-port",
                description: "Port to connect to created Dapr sidecar process.",
                getDefaultValue: () => 5001)
            {
                IsRequired = true
            };
            monitoredProcessPortOption.AddAlias("-p");

            var daprSideCarHttpPortOption = new Option<int>
                (name: "--dapr-http-port",
                description: "HTTP port to connect target created Dapr sidecar process.",
                getDefaultValue: () => 3501);

            var daprSideCarGrpcPortOption = new Option<int>
                (name: "--dapr-grpc-port",
                description: "GRPC port to connect target created Dapr sidecar process.",
                getDefaultValue: () => 50002);
            
            var daprSideCarComponentsPathOption = new Option<DirectoryInfo>
                (name: "--resources-path",
                description: "Resource (components) folder for Dapr sidecar process.");
            daprSideCarComponentsPathOption.AddAlias("-r");

            var daprSideCarConfigFileOption = new Option<FileInfo>
                (name: "--config",
                description: "Config file for Dapr sidecar process.");
            daprSideCarConfigFileOption.AddAlias("-c");

            var debugOption = new Option<bool>
                (name: "--debug",
                description: "Attach and break debugger.");
            debugOption.AddAlias("-d");

            var rootCommand = new RootCommand("Monitors a process and kills its sidecar when it exits.");

            //run dapr
            var createSideCarCommand = new Command("--create-sidecar-process", "Create side car")
            {
                monitoredProcessIdOption,
                monitoredProcessPortOption,
                daprSideCarHttpPortOption,
                daprSideCarGrpcPortOption, 
                daprSideCarComponentsPathOption, 
                daprSideCarConfigFileOption,
                debugOption
            };
            createSideCarCommand.SetHandler((monitoredProcessIdOptionValue, monitoredProcessPortOptionValue, daprSideCarHttpPortOptionValue, daprSideCarGrpcPortOptionValue, daprSideCarComponentsPathOptionValue, daprSideCarConfigFileOptionValue, debugOptionValue) =>
            {
                Console.WriteLine($"--monitored-process-id = {monitoredProcessIdOptionValue}");
                Console.WriteLine($"--app-port = {monitoredProcessPortOptionValue}");
                Console.WriteLine($"--dapr-http-portt= {daprSideCarHttpPortOptionValue}");
                Console.WriteLine($"--dapr-grpc-port= {daprSideCarGrpcPortOptionValue}");
                Console.WriteLine($"--components-path= {daprSideCarComponentsPathOptionValue?.FullName ?? "undefined"}");
                Console.WriteLine($"--config= {daprSideCarConfigFileOptionValue?.FullName ?? "undefined"}");
                Console.WriteLine($"--debug= {debugOptionValue}");

                if (debugOptionValue)
                {
                    Console.WriteLine("Attaching and launching the debugger");
                    Debugger.Launch();
                    Debugger.Break();
                }

                Console.WriteLine("Finding monitored process");
                if (!TryGetProcess(monitoredProcessIdOptionValue, out Process? monitored))
                {
                    Console.Error.WriteLine("Monitored process not found");
                    return;
                }

                Console.WriteLine("Creating dapr sidecar process");
                string appId = monitored!.ProcessName.Replace(".", "_");
                if (!TryCreateProcess(appId, monitoredProcessPortOptionValue, daprSideCarHttpPortOptionValue, daprSideCarGrpcPortOptionValue, daprSideCarComponentsPathOptionValue, daprSideCarConfigFileOptionValue, process: out Process? sidecar))
                {
                    Console.Error.WriteLine("Dapr sidecar failed to launch");
                    return;
                }

                if (TryMonitorProcess(monitored, sidecar))
                {
                    Console.WriteLine("Success");
                }
                else
                {
                    Console.WriteLine("Failed");
                }

            }, monitoredProcessIdOption, monitoredProcessPortOption, daprSideCarHttpPortOption, daprSideCarGrpcPortOption, daprSideCarComponentsPathOption, daprSideCarConfigFileOption, debugOption);

            //monitor dapr
            var attachSideCarCommand = new Command("--attach-sidecar-process", "Attach side car")
            {
                monitoredProcessIdOption,
                daprSideCarProcessIdOption
            };
            attachSideCarCommand.SetHandler((monitoredProcessIdOptionValue, daprSideCarProcessIdOptionValue) =>
            {
                Console.WriteLine($"--monitored-process-id = {monitoredProcessIdOptionValue}");
                Console.WriteLine($"--sidecar-process-id = {daprSideCarProcessIdOptionValue}");

                Console.WriteLine("Existing dapr sidecar process");
                if (!TryGetProcess(daprSideCarProcessIdOptionValue, out Process? sidecar))
                {
                    Console.Error.WriteLine("Dapr sidecar not found");
                    return;
                }
                Console.WriteLine("Finding monitored process");
                if (!TryGetProcess(monitoredProcessIdOptionValue, out Process? monitored))
                {
                    Console.Error.WriteLine("Monitored process not found");
                    return;
                }

                if (TryMonitorProcess(monitored, sidecar))
                {
                    Console.WriteLine("Success");
                }
                else
                {
                    Console.WriteLine("Failed");
                }

            }, monitoredProcessIdOption, daprSideCarProcessIdOption);

            rootCommand.Add(createSideCarCommand);
            rootCommand.Add(attachSideCarCommand);

            await rootCommand.InvokeAsync(args);
        }

        private static bool TryCreateProcess(string appId, int appPort, int daprHttpPort, int daprGrpcPort, DirectoryInfo? componentsFolder, FileInfo? configFile, [NotNullWhen(true)] out Process? process)
        {
            string arguments = $"run --app-id {appId} --app-port {appPort} --dapr-http-port {daprHttpPort} --dapr-grpc-port {daprGrpcPort}";
            if (configFile != null && configFile.Exists)
                arguments += $" --config {configFile.FullName}";

            if (componentsFolder != null && componentsFolder.Exists)
                arguments += $" --resources-path {componentsFolder.FullName}";

            var psi = new ProcessStartInfo(Environment.ExpandEnvironmentVariables(" %SystemDrive%/dapr/dapr.exe"))
            {
                UseShellExecute = false,
                Arguments = arguments,
            };
            try
            {
                process = Process.Start(psi)!;
                Console.WriteLine("Launched side car pid:{0}. Exited: {1}", process.Id, process.HasExited);

                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Failed to launch side car. Error:{0}", ex.Message);
                process = null;
                return false;
            }
        }

        private static bool TryMonitorProcess(Process monitored, Process sidecar)
        {
            try
            {
                monitored.EnableRaisingEvents = true;
                monitored.Exited += (s, e) =>
                {
                    Console.Error.WriteLine("Monitored process {0} has exited. Killing sidecar process {1}", monitored.ProcessName, sidecar.ProcessName);
                    KillSideCarProcess(sidecar);
                };
                Console.Error.WriteLine("Monitoring process {0}.", monitored.ProcessName);
                monitored.WaitForExit();
                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Failed to monitor process {0}. Error: {1}", monitored.ProcessName, ex.Message);
                return false;
            }
        }

        private static void KillSideCarProcess(Process sidecar)
        {
            try
            {
                if (sidecar.HasExited)
                    return;

                if (sidecar.CloseMainWindow())
                    return;

                if (sidecar.HasExited)
                    return;


                sidecar.Kill(true);
            }
            catch
            {
            }
            finally
            {
                sidecar.Dispose();
            }
        }

        private static bool TryGetProcess(int processId, [NotNullWhen(true)] out Process? process)
        {
            try
            {
                process = Process.GetProcessById(processId);
                return true;
            }
            catch
            { }
            process = null;
            return false;
        }
    }
}