﻿using Newtonsoft.Json;
using Oracle.ManagedDataAccess.Client;
using Pivet.Data.Connection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pivet.Data
{
    class ProfileRunner
    {
        [ThreadStatic]
        public static string profileRepoPath;

        static double lastProgress;
        public static Tuple<bool,string> Run(ProfileConfig profile, EnvironmentConfig config)
        {
            OracleConnection _conn;
            List<IDataProcessor> Processors = new List<IDataProcessor>();

            Stopwatch sw = new Stopwatch();
            sw.Start();
            Logger.Write($"Processing Environment '{config.Name}'");

            /* ensure root directory exists */
            Directory.CreateDirectory(profile.OutputFolder);

            VersionController versionController = new VersionController();
            versionController.InitRepository(profile.OutputFolder, profile.Repository);

	    profileRepoPath = profile.OutputFolder;

            /* First thing is to get DB connection */
            var connectionProvider = config.Connection.Provider;
            Logger.Write("Getting database connection...");
            var providerType = Type.GetType("Pivet.Data.Connection." + connectionProvider + "Connection");
            if (providerType == null)
            {
                Logger.Write("Unable to find the specified DB provider.");
                return new Tuple<bool, string>(false, "Unable to find the specified DB provider.");
            }

            var dbProvider = Activator.CreateInstance(providerType) as IConnectionProvider;

            Dictionary<string, string> dbParams = new Dictionary<string, string>();
            dbProvider.SetParameters(config.Connection);
            var connectionResult = dbProvider.GetConnection();

            if (connectionResult.Item2 == false)
            {
                Logger.Write("Error connecting to database: " + connectionResult.Item3);
                return new Tuple<bool, string>(false, "Error connecting to database: " + connectionResult.Item3);
            }

            _conn = connectionResult.Item1;
            Logger.Write("Connected to Database.");

            /* run each processor */
            var availableProcessors = FindProviders();
            foreach (var provider in profile.DataProviders)
            {
                IDataProcessor processor = availableProcessors.Where(p => p.ProcessorID == provider).FirstOrDefault();
                if (processor == null)
                {
                    Logger.Write("Could not find the data processor: " + provider);
                } else
                {
                    if (Program.ShowProgress)
                    {
                        processor.ProgressChanged += Processor_ProgressChanged;
                    }
                    Processors.Add(processor);
                    //int itemCount = processor.LoadItems(_conn, config.Filters, config.ModifyThreshold,versionState);
                    int itemCount = processor.LoadItems(_conn, profile.Filters);
                    Logger.Write("Found " + itemCount + " " + provider + " Definitions");
                }
            }

            Logger.Write("Definitions collected.");

            Logger.Write("Cleaning working directory.");
            foreach(var p in Processors)
            {
                p.ProcessDeletes(profile.OutputFolder);
            }
            
            Logger.Write("Processing items.");
            List<ChangedItem> changedItems = new List<ChangedItem>();
            foreach (var p in Processors)
            {
                Console.WriteLine($"Saving {p.ItemName} Definitions..." );
                Console.WriteLine();
                changedItems.AddRange(p.SaveToDisk(profile.OutputFolder));
                Console.CursorLeft = 0;
                Processor_ProgressChanged(new ProgressEvent() { Progress = 100 });
            }

            Logger.Write("Definitions saved to disk.");

            versionController.ProcessChanges(changedItems);
            sw.Stop();
            Logger.Write("Environment processed in: " + sw.Elapsed.TotalSeconds + " seconds");
            return new Tuple<bool, string>(true, "");
        }

        private static void Processor_ProgressChanged(ProgressEvent evt)
        {
            if (lastProgress != evt.Progress)
            {
                Console.CursorLeft = 0;
                Console.CursorTop--;

                Console.WriteLine("Progress: " + string.Format("{0:N2}%", evt.Progress));
                lastProgress = evt.Progress;
            }

        }

        static List<IDataProcessor> FindProviders()
        {
            var type = typeof(IDataProcessor);
            return AppDomain.CurrentDomain.GetAssemblies().Where(a => Program.LoadedAssemblies.Contains(a)).SelectMany(s => s.GetTypes()).Where(p => type.IsAssignableFrom(p) && !p.IsInterface && !p.IsAbstract).Select(s => Activator.CreateInstance(s) as IDataProcessor).ToList();
        }
    }
}
