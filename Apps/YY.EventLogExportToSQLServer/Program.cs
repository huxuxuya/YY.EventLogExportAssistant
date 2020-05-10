﻿using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Linq;
using System.Threading;
using YY.EventLogExportAssistant;
using YY.EventLogExportAssistant.SQLServer;
using YY.EventLogExportAssistant.SQLServer.Models;
using YY.EventLogReaderAssistant;

namespace YY.EventLogExportToSQLServer
{
    class Program
    {
        #region Private Static Member Variables

        private static long _totalRows = 0;
        private static long _lastPortionRows = 0;
        private static DateTime _beginPortionExport;
        private static DateTime _endPortionExport;

        #endregion

        #region Static Methods

        static void Main()
        {
            IConfiguration Configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .Build();

            IConfigurationSection eventLogSection = Configuration.GetSection("EventLog");
            string eventLogPath = eventLogSection.GetValue("SourcePath", string.Empty);
            int watchPeriodSeconds = eventLogSection.GetValue("WatchPeriod", 60);
            int watchPeriodSecondsMs = watchPeriodSeconds * 1000;
            bool useWatchMode = eventLogSection.GetValue("UseWatchMode", false);
            int portion = eventLogSection.GetValue("Portion", 1000);

            IConfigurationSection inforamtionSystemSection = Configuration.GetSection("InformationSystem");
            string inforamtionSystemName = inforamtionSystemSection.GetValue("Name", string.Empty);
            string inforamtionSystemDescription = inforamtionSystemSection.GetValue("Description", string.Empty);

            if (string.IsNullOrEmpty(eventLogPath))
            {
                Console.WriteLine("Не указан каталог с файлами данных журнала регистрации.");
                Console.WriteLine("Для выхода нажмите любую клавишу...");
                Console.Read();
                return;
            }

            Console.WriteLine();
            Console.WriteLine();

            string connectionString = Configuration.GetConnectionString("EventLogDatabase");
            DbContextOptions<EventLogContext> options = new DbContextOptions<EventLogContext>();
            var optionsBuilder = new DbContextOptionsBuilder<EventLogContext>();
            optionsBuilder.UseSqlServer(connectionString);

            EventLogExportMaster exporter = new EventLogExportMaster();
            exporter.SetEventLogPath(eventLogPath);

            EventLogOnSQLServer target = new EventLogOnSQLServer(optionsBuilder.Options, portion);
            target.SetInformationSystem(new InformationSystemsBase()
            {
                Name = inforamtionSystemName,
                Description = inforamtionSystemDescription
            });
            exporter.SetTarget(target);

            exporter.BeforeExportData += BeforeExportData;
            exporter.AfterExportData += AfterExportData;         

            if (useWatchMode)
            {
                while (true)
                {
                    if (Console.KeyAvailable)
                        if (Console.ReadKey().KeyChar == 'q')
                            break;

                    while (exporter.NewDataAvailiable())
                    {
                        exporter.SendData();
                        Thread.Sleep(watchPeriodSecondsMs);
                    }                    
                }
            } else
                while (exporter.NewDataAvailiable())
                    exporter.SendData();

            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("Для выхода нажмите любую клавишу...");
            Console.Read();
        }

        #endregion

        #region Events

        private static void BeforeExportData(BeforeExportDataEventArgs e)
        {
            _beginPortionExport = DateTime.Now;
            _lastPortionRows = e.Rows.Count;
            _totalRows += e.Rows.Count;

            Console.SetCursorPosition(0, 0);
            Console.WriteLine("[{0}] Last read: {1}             ", DateTime.Now, e.Rows.Count);
        }
        private static void AfterExportData(AfterExportDataEventArgs e)
        {
            _endPortionExport = DateTime.Now;
            var duration = _endPortionExport - _beginPortionExport;

            Console.WriteLine("[{0}] Total read: {1}            ", DateTime.Now, _totalRows);
            Console.WriteLine("[{0}] {1} / {2} (sec.)           ", DateTime.Now, _lastPortionRows, duration.TotalSeconds);
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("Нажмите 'q' для завершения отслеживания изменений...");
        }

        #endregion
    }
}