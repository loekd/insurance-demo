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

            var monitoredProcessIdOption = new Option<int>
                (name: "--monitored-process-id",
                description: "Id of monitored process.")
            {
                IsRequired = true
            };
            monitoredProcessIdOption.AddAlias("-mpid");

            //with attach
            var daprSideCarProcessIdOption = new Option<int>
                (name: "--sidecar-process-id",
                description: "Process Id of running Dapr sidecar process.")
            {
                IsRequired = true
            };
            daprSideCarProcessIdOption.AddAlias("-scpid");

            //with create
            var monitoredProcessPortOption = new Option<int>
                (name: "--monitored-process-port",
                description: "Port to connect to created Dapr sidecar process.",
                getDefaultValue: () => 5001)
            {
                IsRequired = true
            };
            monitoredProcessPortOption.AddAlias("-mpp");

            var daprSideCarHttpPortOption = new Option<int>
                (name: "--sidecar-process-http-port",
                description: "HTTP port to connect target created Dapr sidecar process.",
                getDefaultValue: () => 3501);
            daprSideCarHttpPortOption.AddAlias("-schp");

            var daprSideCarGrpcPortOption = new Option<int>
                (name: "--sidecar-process-grpc-port",
                description: "GRPC port to connect target created Dapr sidecar process.",
                getDefaultValue: () => 50002);
            daprSideCarGrpcPortOption.AddAlias("-scgp");

            var rootCommand = new RootCommand("Monitors a process and kills its sidecar when it exits.");

            //run dapr
            var createSideCarCommand = new Command("--create-sidecar-process", "Create side car")
            {
                monitoredProcessIdOption,
                monitoredProcessPortOption,
                daprSideCarHttpPortOption,
                daprSideCarGrpcPortOption
            };
            createSideCarCommand.SetHandler((monitoredProcessIdOptionValue, monitoredProcessPortOptionValue, daprSideCarHttpPortOptionValue, daprSideCarGrpcPortOptionValue) =>
            {
                Console.WriteLine($"--monitored-process-id = {monitoredProcessIdOptionValue}");
                Console.WriteLine($"--monitored-process-port = {monitoredProcessPortOptionValue}");
                Console.WriteLine($"--sidecar-process-http-port= {daprSideCarHttpPortOptionValue}");
                Console.WriteLine($"--sidecar-process-grpc-port= {daprSideCarGrpcPortOptionValue}");

                Console.WriteLine("Finding monitored process");
                if (!TryGetProcess(monitoredProcessIdOptionValue, out Process? monitored))
                {
                    Console.Error.WriteLine("Monitored process not found");
                    return;
                }

                Console.WriteLine("Creating dapr sidecar process");
                if (!TryCreateProcess(monitored!.ProcessName, monitoredProcessPortOptionValue, daprSideCarHttpPortOptionValue, daprSideCarGrpcPortOptionValue, process: out Process? sidecar))
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

            }, monitoredProcessIdOption, monitoredProcessPortOption, daprSideCarHttpPortOption, daprSideCarGrpcPortOption);

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

        private static bool TryCreateProcess(string appId, int appPort, int daprHttpPort, int daprGrpcPort, [NotNullWhen(true)] out Process? process)
        {
            var psi = new ProcessStartInfo(Environment.ExpandEnvironmentVariables(" %SystemDrive%/dapr/dapr.exe"))
            {
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                Arguments = $"run --app-id {appId} --app-port {appPort} --dapr-http-port {daprHttpPort} --dapr-grpc-port {daprGrpcPort}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            try
            {
                process = Process.Start(psi)!;
                process.OutputDataReceived += (s, e) => Console.WriteLine($"DAPR: {e.Data}", ConsoleColor.Blue);
                process.ErrorDataReceived += (s, e) => Console.WriteLine($"DAPR: {e.Data}", ConsoleColor.Red);
                process.BeginErrorReadLine();
                process.BeginOutputReadLine();
                Console.Error.WriteLine("Launched side car pid:{0}", process.Id);

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