using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace TD.NewmanRunner
{
    class Program
    {
        static void Main(string[] args)
        {
            /*
             * newmanrunner --l:[path/to/tests]
             * 
             * will run all tests in that location using all environmental files 1 time by default
             * you can use flags to filter this
             * 
             * flags:
             *     --e:[name of environmenal file to use] : will only use this environmental file for runing the tests
             *     --t:[name of test suite to run] : will only run this test suite
             *     --i:[number of iterations] : will run the tests this many times, valid numbers (1-10)
             *     --T:[type of report output] : validoptions html, xml, json, none (meaning no report output)
             *     --n:[output path of report] : required with --rt flag, output path for the test data
             *     --N:[location of newman] : override the default location of newman, otherwise applicationw will attempt to use the global installation
             */

            var exitCode = 0;

            var envCounter = 0;
            try
            {
                var config = ConfigLoader.Load(args);

                foreach (var env in config.Environment)
                {
                    envCounter++;
                    Console.WriteLine(string.Empty);
                    Console.WriteLine("----------------------------------------------------");
                    Console.WriteLine("Setting Environment File: {0}", env);

                    foreach (var test in config.Test)
                    {
                        Console.WriteLine("Setting Test Suite File: {0}", test);

                        var command = string.Format("{0} -n {1} -e {2} -c {3}",
                                                    config.NewmanCommand, 
                                                    config.Iteration,
                                                    env, 
                                                    test);

                        if (config.ReportType != ReportType.None)
                        {
                            command += string.Format(" {0} {1}", config.ReportCode, config.ReportFileLocation + "\\testResult_" + envCounter + config.ReportFileExtension);
                        }
                        Console.WriteLine("Excecuting Command: {0}", command);

                        var tExitCode = _executeCommand(command);

                        if (exitCode == 0 && tExitCode > 0)
                        {
                            exitCode = 1;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            Environment.Exit(exitCode);
        }

        private static int _executeCommand(string command)
        {
            Console.WriteLine(string.Empty);

            var processInfo = new ProcessStartInfo("cmd.exe", "/c " + command)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };

            var process = Process.Start(processInfo);

            process.OutputDataReceived += (sender, e) => Console.WriteLine(e.Data);
            process.BeginOutputReadLine();

            process.ErrorDataReceived += (sender, e) => Console.WriteLine(e.Data);
            process.BeginErrorReadLine();

            process.WaitForExit();

            var exitCode = process.ExitCode;
            Console.WriteLine("ExitCode: {0}", exitCode);
            process.Close();
            return exitCode;

        }
    }

    public static class ConfigLoader
    {
        public static Config Load(string[] args)
        {
            var config = new Config();

            foreach (var arg in args)
            {

                var key = arg.Substring(0, 3);
                var val = arg.Substring(4);

                var argKv = new[] {key, val};
                
                if (argKv.Length == 2)
                {
                    var v = argKv[1];
                    switch (argKv[0])
                    {
                        case "--l":
                            config.Location = v;
                            break;
                        case "--e":
                            config.Environment.Add(v);
                            break;
                        case "--t":
                            config.Test.Add(v);
                            break;
                        case "--i":
                            int iteration;
                            if (int.TryParse(v, out iteration))
                            {
                                if (iteration < 1 || iteration > 10)
                                {
                                    iteration = 1;
                                }
                            }
                            else
                            {
                                iteration = 1;
                            }

                            config.Iteration = iteration;

                            break;
                        case "--T":
                            switch (v)
                            {
                                case "json":
                                    config.ReportType = ReportType.Json;
                                    config.ReportCode = string.Empty;
                                    config.ReportFileExtension = ".json";
                                    break;
                                case "html":
                                    config.ReportType = ReportType.Html;
                                    config.ReportCode = "-H";
                                    config.ReportFileExtension = ".html";
                                    break;
                                case "xml":
                                    config.ReportType = ReportType.Xml;
                                    config.ReportCode = "-t";
                                    config.ReportFileExtension = ".xml";
                                    break;
                                default:
                                    config.ReportType = ReportType.None;
                                    break;
                            }
                            break;
                        case "--n":
                            config.ReportFileLocation = v;
                            break;
                        case "--N":
                            config.NewmanCommand = v;
                            break;

                    }
                }
            }

            _checkConfig(config);
            return config;

        }

        private static void _checkConfig(Config config)
        {
            if (config.ReportType != ReportType.None && string.IsNullOrEmpty(config.ReportFileLocation))
            {
                throw new Exception("--rn:[report file name] flag is required with --rt flag");
            }

            if (!Directory.Exists(config.Location))
            {
                throw new Exception("--loc: location is invalid, directory does not exist");
            }

            if (!config.Environment.Any())
            {
                //load all the tests
                if (!Directory.Exists(config.EnvLocation))
                {
                    throw new Exception("test location does not have a valid env directory");
                }

                foreach (var file in Directory.GetFiles(config.EnvLocation))
                {
                    config.Environment.Add(file);
                }
            }

            if (!config.Test.Any())
            {
                if (!Directory.Exists(config.TestLocation))
                {
                    throw new Exception("test location does not have a valid 'tests' directory");
                }

                foreach (var file in Directory.GetFiles(config.TestLocation))
                {
                    config.Test.Add(file);
                }
            }
        }
    }

    public enum ReportType
    {
        None = 0,
        Html = 1,
        Json = 2,
        Xml = 3
    }

    public class Config
    {
        public Config()
        {
            this.Location = ConfigurationManager.AppSettings["DefaultLocation"];
            this.Environment = new List<string>();
            this.Test = new List<string>();
            this.Iteration = int.Parse(ConfigurationManager.AppSettings["DefaultIterations"]);
            this.ReportType = (ReportType)int.Parse(ConfigurationManager.AppSettings["DefaultReportType"]);
            this.ReportFileLocation = ConfigurationManager.AppSettings["DefaultReportFileLocation"];
            this.NewmanCommand = ConfigurationManager.AppSettings["DefaultNewmanCommand"];
        }

        public string Location { get; set; }
        public string EnvLocation
        {
            get { return string.Format("{0}\\env", this.Location); }
        }
        public string TestLocation
        {
            get { return string.Format("{0}\\tests", this.Location); }
        }
        public List<string> Environment { get; set; }
        public List<string> Test { get; set; }
        public int Iteration { get; set; }
        public ReportType ReportType { get; set; }
        public string ReportCode { get; set; }
        public string ReportFileLocation { get; set; }
        public string ReportFileExtension { get; set; }
        public string NewmanCommand { get; set; }

    }
}
